using NeuroGateway.ML.OLD;

namespace NeuroGateway.ML.OLD.TCN;

/// <summary>
/// Temporal Convolutional Network with Attention for trajectory prediction.
/// Input: time-ordered sequences of dimension scores + embeddings.
/// Output: predicted future states, attention maps (causal influence), change-point detection.
/// Architecture: dilated causal convolutions → attention pooling → prediction head.
/// </summary>
public class TemporalConvNet
{
    private readonly int _inputDim;
    private readonly int _channelDim;
    private readonly int _numLayers;
    private readonly int _attentionDim;
    private readonly float _learningRate;
    private readonly Random _rng = new(42);

    // Convolutional layers (dilated causal): kernel size 3, dilation doubles each layer
    private readonly ConvLayer[] _convLayers;

    // Attention: query/key/value projections
    private float[][] _attnQ; // channelDim × attentionDim
    private float[][] _attnK; // channelDim × attentionDim
    private float[][] _attnV; // channelDim × channelDim

    // Prediction head: channelDim → inputDim
    private float[][] _predW;
    private float[] _predB;

    public record Prediction(float[] PredictedNext, float[] AttentionWeights, float Confidence);
    public record ChangePoint(int TimeIndex, float Magnitude, string Direction);

    public record TcnResult(
        Prediction NextState,
        List<ChangePoint> ChangePoints,
        float[][] AttentionMap,    // seqLen × seqLen causal attention
        float[][] HiddenStates);   // seqLen × channelDim

    public TemporalConvNet(int inputDim = 24, int channelDim = 48, int numLayers = 3, int attentionDim = 16, float learningRate = 0.001f)
    {
        _inputDim = inputDim;
        _channelDim = channelDim;
        _numLayers = numLayers;
        _attentionDim = attentionDim;
        _learningRate = learningRate;

        // Init conv layers with increasing dilation
        _convLayers = new ConvLayer[numLayers];
        _convLayers[0] = new ConvLayer(inputDim, channelDim, dilation: 1, _rng);
        for (var i = 1; i < numLayers; i++)
            _convLayers[i] = new ConvLayer(channelDim, channelDim, dilation: 1 << i, _rng);

        // Attention
        _attnQ = XavierInit(channelDim, attentionDim);
        _attnK = XavierInit(channelDim, attentionDim);
        _attnV = XavierInit(channelDim, channelDim);

        // Prediction head
        _predW = XavierInit(channelDim, inputDim);
        _predB = new float[inputDim];
    }

    /// <summary>Forward pass: predict next state from sequence.</summary>
    public TcnResult Forward(IReadOnlyList<float[]> sequence)
    {
        var seqLen = sequence.Count;
        if (seqLen == 0)
            return new TcnResult(
                new Prediction(new float[_inputDim], [], 0f),
                [], LinearAlgebra.NewMatrix(0, 0), LinearAlgebra.NewMatrix(0, 0));

        // 1. Causal convolutions
        var hidden = ApplyConvLayers(sequence, seqLen);

        // 2. Causal self-attention
        var (attended, attnMap, attnWeights) = CausalAttention(hidden, seqLen);

        // 3. Prediction from last attended state
        var lastState = attended[seqLen - 1];
        var predicted = new float[_inputDim];
        for (var j = 0; j < _inputDim; j++)
        {
            float sum = _predB[j];
            for (var i = 0; i < _channelDim; i++) sum += lastState[i] * _predW[i][j];
            predicted[j] = LinearAlgebra.Sigmoid(sum) * 100f; // scale to score range
        }

        // Confidence from attention entropy
        var entropy = 0f;
        for (var i = 0; i < seqLen; i++)
        {
            var w = attnWeights[i];
            if (w > 1e-8f) entropy -= w * MathF.Log(w);
        }
        var maxEntropy = MathF.Log(seqLen);
        var confidence = maxEntropy > 0 ? 1f - entropy / maxEntropy : 1f;

        // 4. Change-point detection
        var changePoints = DetectChangePoints(hidden, seqLen);

        return new TcnResult(
            new Prediction(predicted, attnWeights, confidence),
            changePoints,
            attnMap,
            hidden);
    }

    /// <summary>Train on sequences: predict each step t+1 from steps 0..t.</summary>
    public float Train(IReadOnlyList<float[]> sequence, int epochs = 100)
    {
        var seqLen = sequence.Count;
        if (seqLen < 2) return 0f;

        float lastLoss = 0;

        for (var epoch = 0; epoch < epochs; epoch++)
        {
            float totalLoss = 0;

            for (var t = 1; t < seqLen; t++)
            {
                var subSeq = new List<float[]>();
                for (var i = 0; i < t; i++) subSeq.Add(sequence[i]);

                var result = Forward(subSeq);
                var target = sequence[t];

                // MSE loss
                float loss = 0;
                var dPred = new float[_inputDim];
                for (var j = 0; j < _inputDim; j++)
                {
                    var diff = result.NextState.PredictedNext[j] - target[j];
                    loss += diff * diff;
                    dPred[j] = 2f * diff / _inputDim;
                }
                loss /= _inputDim;
                totalLoss += loss;

                // Backprop through prediction head
                BackpropPredictionHead(result.HiddenStates[^1], dPred);
            }

            lastLoss = totalLoss / (seqLen - 1);
        }

        return lastLoss;
    }

    // ── Convolutional layers ────────────────────────────────────

    private float[][] ApplyConvLayers(IReadOnlyList<float[]> input, int seqLen)
    {
        var current = new float[seqLen][];
        for (var t = 0; t < seqLen; t++) current[t] = input[t];

        foreach (var layer in _convLayers)
        {
            var next = new float[seqLen][];
            for (var t = 0; t < seqLen; t++)
                next[t] = layer.Forward(current, t, seqLen);

            // Residual connection (if dims match)
            if (current[0].Length == next[0].Length)
            {
                for (var t = 0; t < seqLen; t++)
                    for (var c = 0; c < next[t].Length; c++)
                        next[t][c] += current[t][c];
            }

            current = next;
        }

        return current;
    }

    // ── Causal self-attention ───────────────────────────────────

    private (float[][] attended, float[][] attnMap, float[] lastWeights) CausalAttention(float[][] hidden, int seqLen)
    {
        // Project to Q, K, V
        var Q = new float[seqLen][];
        var K = new float[seqLen][];
        var V = new float[seqLen][];

        for (var t = 0; t < seqLen; t++)
        {
            Q[t] = new float[_attentionDim];
            K[t] = new float[_attentionDim];
            V[t] = new float[_channelDim];

            for (var j = 0; j < _attentionDim; j++)
            {
                float q = 0, k = 0;
                for (var i = 0; i < _channelDim; i++)
                {
                    q += hidden[t][i] * _attnQ[i][j];
                    k += hidden[t][i] * _attnK[i][j];
                }
                Q[t][j] = q;
                K[t][j] = k;
            }

            for (var j = 0; j < _channelDim; j++)
            {
                float v = 0;
                for (var i = 0; i < _channelDim; i++) v += hidden[t][i] * _attnV[i][j];
                V[t][j] = v;
            }
        }

        // Causal attention: position t can only attend to positions 0..t
        var scale = 1f / MathF.Sqrt(_attentionDim);
        var attnMap = LinearAlgebra.NewMatrix(seqLen, seqLen);
        var attended = new float[seqLen][];

        for (var t = 0; t < seqLen; t++)
        {
            // Compute scores for positions 0..t
            var scores = new float[t + 1];
            for (var s = 0; s <= t; s++)
                scores[s] = LinearAlgebra.Dot(Q[t], K[s]) * scale;

            // Softmax over causal window
            var weights = LinearAlgebra.Softmax(scores);
            for (var s = 0; s <= t; s++) attnMap[t][s] = weights[s];

            // Weighted sum of values
            attended[t] = new float[_channelDim];
            for (var s = 0; s <= t; s++)
                for (var c = 0; c < _channelDim; c++)
                    attended[t][c] += weights[s] * V[s][c];
        }

        return (attended, attnMap, attnMap[seqLen - 1][..seqLen]);
    }

    // ── Change-point detection ──────────────────────────────────

    private List<ChangePoint> DetectChangePoints(float[][] hidden, int seqLen)
    {
        var changePoints = new List<ChangePoint>();
        if (seqLen < 3) return changePoints;

        // Detect significant shifts in hidden state
        for (var t = 1; t < seqLen; t++)
        {
            var magnitude = 0f;
            for (var c = 0; c < _channelDim; c++)
            {
                var diff = hidden[t][c] - hidden[t - 1][c];
                magnitude += diff * diff;
            }
            magnitude = MathF.Sqrt(magnitude);

            // Adaptive threshold: mean + 1.5 * std of all magnitudes
            changePoints.Add(new ChangePoint(t, magnitude, ""));
        }

        // Compute threshold
        var mags = changePoints.Select(c => c.Magnitude).ToArray();
        var mean = mags.Average();
        var std = MathF.Sqrt(mags.Select(m => (m - (float)mean) * (m - (float)mean)).Average());
        var threshold = (float)mean + 1.5f * std;

        // Filter and label
        var significant = new List<ChangePoint>();
        foreach (var cp in changePoints)
        {
            if (cp.Magnitude < threshold) continue;

            // Determine direction from dominant changing dimension
            var maxDelta = 0f;
            var direction = "shift";
            for (var c = 0; c < Math.Min(_channelDim, _inputDim); c++)
            {
                var delta = hidden[cp.TimeIndex][c] - hidden[cp.TimeIndex - 1][c];
                if (MathF.Abs(delta) > MathF.Abs(maxDelta))
                {
                    maxDelta = delta;
                    direction = maxDelta > 0 ? "increase" : "decrease";
                }
            }

            significant.Add(cp with { Direction = direction });
        }

        return significant;
    }

    // ── Backprop (prediction head only for online learning) ─────

    private void BackpropPredictionHead(float[] lastHidden, float[] dPred)
    {
        // Apply sigmoid derivative
        for (var j = 0; j < _inputDim; j++)
        {
            float sum = _predB[j];
            for (var i = 0; i < _channelDim; i++) sum += lastHidden[i] * _predW[i][j];
            var sig = LinearAlgebra.Sigmoid(sum);
            dPred[j] *= 100f * sig * (1f - sig);
        }

        for (var j = 0; j < _inputDim; j++)
        {
            _predB[j] -= _learningRate * dPred[j];
            for (var i = 0; i < _channelDim; i++)
                _predW[i][j] -= _learningRate * lastHidden[i] * dPred[j];
        }
    }

    // ── Conv layer ──────────────────────────────────────────────

    private class ConvLayer
    {
        private readonly int _inDim, _outDim, _dilation;
        private readonly float[][] _w0, _w1, _w2; // kernel size 3: w[t-2d], w[t-d], w[t]
        private readonly float[] _bias;

        public ConvLayer(int inDim, int outDim, int dilation, Random rng)
        {
            _inDim = inDim;
            _outDim = outDim;
            _dilation = dilation;

            var scale = MathF.Sqrt(2f / (inDim * 3));
            _w0 = Init(inDim, outDim, scale, rng);
            _w1 = Init(inDim, outDim, scale, rng);
            _w2 = Init(inDim, outDim, scale, rng);
            _bias = new float[outDim];
        }

        public float[] Forward(float[][] seq, int t, int seqLen)
        {
            var output = new float[_outDim];

            for (var o = 0; o < _outDim; o++)
            {
                float sum = _bias[o];

                // Causal: only look at t and before (t - dilation, t - 2*dilation)
                var t0 = t - 2 * _dilation;
                var t1 = t - _dilation;

                if (t0 >= 0)
                    for (var i = 0; i < _inDim; i++) sum += seq[t0][i] * _w0[i][o];
                if (t1 >= 0)
                    for (var i = 0; i < _inDim; i++) sum += seq[t1][i] * _w1[i][o];
                for (var i = 0; i < _inDim; i++) sum += seq[t][i] * _w2[i][o];

                output[o] = LinearAlgebra.ReLU(sum);
            }

            return output;
        }

        private static float[][] Init(int fanIn, int fanOut, float scale, Random rng)
        {
            var w = new float[fanIn][];
            for (var i = 0; i < fanIn; i++)
            {
                w[i] = new float[fanOut];
                for (var j = 0; j < fanOut; j++)
                    w[i][j] = ((float)rng.NextDouble() * 2f - 1f) * scale;
            }
            return w;
        }
    }

    private float[][] XavierInit(int fanIn, int fanOut)
    {
        var scale = MathF.Sqrt(2f / (fanIn + fanOut));
        var w = new float[fanIn][];
        for (var i = 0; i < fanIn; i++)
        {
            w[i] = new float[fanOut];
            for (var j = 0; j < fanOut; j++)
                w[i][j] = ((float)_rng.NextDouble() * 2f - 1f) * scale;
        }
        return w;
    }
}
