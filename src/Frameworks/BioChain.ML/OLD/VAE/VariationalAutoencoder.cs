using BioChain.ML.OLD;

namespace BioChain.ML.OLD.VAE;

/// <summary>
/// Variational Autoencoder for latent personality factor discovery.
/// Input: 24 dimension scores → encoder → latent space → decoder → reconstruction.
/// Discovers nonlinear personality factors that linear factor analysis misses.
/// Supports both training (SGD with reparameterization trick) and inference.
/// </summary>
public class VariationalAutoencoder
{
    private readonly int _inputDim;
    private readonly int _hiddenDim;
    private readonly int _latentDim;
    private readonly float _learningRate;
    private readonly Random _rng = new(42);

    // Encoder: input → hidden → (mu, logvar)
    private float[][] _encW1 = null!;
    private float[] _encB1 = null!;
    private float[][] _muW = null!;
    private float[] _muB = null!;
    private float[][] _logvarW = null!;
    private float[] _logvarB = null!;

    // Decoder: latent → hidden → output
    private float[][] _decW1 = null!;
    private float[] _decB1 = null!;
    private float[][] _decW2 = null!;
    private float[] _decB2 = null!;

    public record LatentFactors(float[] Mu, float[] LogVar, float[] Z, float[] Reconstruction, float ReconLoss, float KlLoss);

    public record TrainResult(float FinalLoss, float FinalReconLoss, float FinalKlLoss, int Epochs, List<float> LossHistory);

    public VariationalAutoencoder(int inputDim = 24, int hiddenDim = 48, int latentDim = 8, float learningRate = 0.001f)
    {
        _inputDim = inputDim;
        _hiddenDim = hiddenDim;
        _latentDim = latentDim;
        _learningRate = learningRate;
        InitWeights();
    }

    /// <summary>Encode input to latent factors (inference only — no training).</summary>
    public LatentFactors Encode(float[] input)
    {
        var (hidden, mu, logvar) = Forward_Encode(input);
        var z = Reparameterize(mu, logvar);
        var recon = Forward_Decode(z);

        var reconLoss = 0f;
        for (var i = 0; i < _inputDim; i++)
            reconLoss += (input[i] - recon[i]) * (input[i] - recon[i]);
        reconLoss /= _inputDim;

        var klLoss = 0f;
        for (var i = 0; i < _latentDim; i++)
            klLoss += -0.5f * (1f + logvar[i] - mu[i] * mu[i] - MathF.Exp(logvar[i]));

        return new LatentFactors(mu, logvar, z, recon, reconLoss, klLoss);
    }

    /// <summary>Batch encode multiple inputs, returning latent coordinates for clustering.</summary>
    public float[][] BatchEncode(IReadOnlyList<float[]> inputs)
    {
        var result = new float[inputs.Count][];
        for (var i = 0; i < inputs.Count; i++)
            result[i] = Encode(inputs[i]).Mu; // use mean (deterministic) for downstream
        return result;
    }

    /// <summary>Train on dimension score data.</summary>
    public TrainResult Train(IReadOnlyList<float[]> data, int epochs = 500, float klWeight = 0.1f)
    {
        var history = new List<float>();
        float lastRecon = 0, lastKl = 0;

        for (var epoch = 0; epoch < epochs; epoch++)
        {
            float totalLoss = 0, totalRecon = 0, totalKl = 0;

            // Shuffle
            var indices = Enumerable.Range(0, data.Count).OrderBy(_ => _rng.Next()).ToArray();

            foreach (var idx in indices)
            {
                var x = data[idx];
                var (reconLoss, klLoss) = TrainStep(x, klWeight);
                totalLoss += reconLoss + klWeight * klLoss;
                totalRecon += reconLoss;
                totalKl += klLoss;
            }

            totalLoss /= data.Count;
            totalRecon /= data.Count;
            totalKl /= data.Count;
            history.Add(totalLoss);
            lastRecon = totalRecon;
            lastKl = totalKl;
        }

        return new TrainResult(history[^1], lastRecon, lastKl, epochs, history);
    }

    /// <summary>Decode from latent space back to dimension scores.</summary>
    public float[] Decode(float[] z) => Forward_Decode(z);

    // ── Forward pass ────────────────────────────────────────────

    private (float[] hidden, float[] mu, float[] logvar) Forward_Encode(float[] input)
    {
        // Hidden = ReLU(input @ W1 + b1)
        var hidden = new float[_hiddenDim];
        for (var j = 0; j < _hiddenDim; j++)
        {
            float sum = _encB1[j];
            for (var i = 0; i < _inputDim; i++) sum += input[i] * _encW1[i][j];
            hidden[j] = LinearAlgebra.ReLU(sum);
        }

        // Mu = hidden @ muW + muB
        var mu = new float[_latentDim];
        for (var j = 0; j < _latentDim; j++)
        {
            float sum = _muB[j];
            for (var i = 0; i < _hiddenDim; i++) sum += hidden[i] * _muW[i][j];
            mu[j] = sum;
        }

        // LogVar = hidden @ logvarW + logvarB
        var logvar = new float[_latentDim];
        for (var j = 0; j < _latentDim; j++)
        {
            float sum = _logvarB[j];
            for (var i = 0; i < _hiddenDim; i++) sum += hidden[i] * _logvarW[i][j];
            logvar[j] = MathF.Min(sum, 10f); // clamp for stability
        }

        return (hidden, mu, logvar);
    }

    private float[] Forward_Decode(float[] z)
    {
        // Hidden = ReLU(z @ W1 + b1)
        var hidden = new float[_hiddenDim];
        for (var j = 0; j < _hiddenDim; j++)
        {
            float sum = _decB1[j];
            for (var i = 0; i < _latentDim; i++) sum += z[i] * _decW1[i][j];
            hidden[j] = LinearAlgebra.ReLU(sum);
        }

        // Output = sigmoid(hidden @ W2 + b2) scaled to [0, 100]
        var output = new float[_inputDim];
        for (var j = 0; j < _inputDim; j++)
        {
            float sum = _decB2[j];
            for (var i = 0; i < _hiddenDim; i++) sum += hidden[i] * _decW2[i][j];
            output[j] = LinearAlgebra.Sigmoid(sum) * 100f;
        }

        return output;
    }

    private float[] Reparameterize(float[] mu, float[] logvar)
    {
        var z = new float[_latentDim];
        for (var i = 0; i < _latentDim; i++)
        {
            var eps = (float)(_rng.NextDouble() * 2 - 1) * 1.0f; // ~N(0,1) approximation
            z[i] = mu[i] + MathF.Exp(0.5f * logvar[i]) * eps;
        }
        return z;
    }

    // ── Training step (manual backprop) ─────────────────────────

    private (float reconLoss, float klLoss) TrainStep(float[] x, float klWeight)
    {
        // Forward
        var (hidden, mu, logvar) = Forward_Encode(x);
        var z = Reparameterize(mu, logvar);

        // Decoder forward (with intermediates for backprop)
        var decHidden = new float[_hiddenDim];
        var decHiddenPre = new float[_hiddenDim];
        for (var j = 0; j < _hiddenDim; j++)
        {
            float sum = _decB1[j];
            for (var i = 0; i < _latentDim; i++) sum += z[i] * _decW1[i][j];
            decHiddenPre[j] = sum;
            decHidden[j] = LinearAlgebra.ReLU(sum);
        }

        var outputPre = new float[_inputDim];
        var output = new float[_inputDim];
        for (var j = 0; j < _inputDim; j++)
        {
            float sum = _decB2[j];
            for (var i = 0; i < _hiddenDim; i++) sum += decHidden[i] * _decW2[i][j];
            outputPre[j] = sum;
            var sig = LinearAlgebra.Sigmoid(sum);
            output[j] = sig * 100f;
        }

        // Losses
        var reconLoss = 0f;
        for (var i = 0; i < _inputDim; i++)
            reconLoss += (x[i] - output[i]) * (x[i] - output[i]);
        reconLoss /= _inputDim;

        var klLoss = 0f;
        for (var i = 0; i < _latentDim; i++)
            klLoss += -0.5f * (1f + logvar[i] - mu[i] * mu[i] - MathF.Exp(logvar[i]));

        // ── Backprop through decoder ────────────────────────────

        // d(reconLoss)/d(output) = 2(output - x) / inputDim
        var dOutput = new float[_inputDim];
        for (var i = 0; i < _inputDim; i++)
        {
            var sig = LinearAlgebra.Sigmoid(outputPre[i]);
            dOutput[i] = 2f * (output[i] - x[i]) / _inputDim * 100f * sig * (1f - sig);
        }

        // Decoder W2, b2
        for (var j = 0; j < _inputDim; j++)
        {
            _decB2[j] -= _learningRate * dOutput[j];
            for (var i = 0; i < _hiddenDim; i++)
                _decW2[i][j] -= _learningRate * decHidden[i] * dOutput[j];
        }

        // Decoder hidden gradient
        var dDecHidden = new float[_hiddenDim];
        for (var i = 0; i < _hiddenDim; i++)
        {
            float sum = 0;
            for (var j = 0; j < _inputDim; j++) sum += _decW2[i][j] * dOutput[j];
            dDecHidden[i] = sum * (decHiddenPre[i] > 0 ? 1f : 0f); // ReLU grad
        }

        // Decoder W1, b1
        for (var j = 0; j < _hiddenDim; j++)
        {
            _decB1[j] -= _learningRate * dDecHidden[j];
            for (var i = 0; i < _latentDim; i++)
                _decW1[i][j] -= _learningRate * z[i] * dDecHidden[j];
        }

        // Gradient through z to mu/logvar (reparameterization)
        var dZ = new float[_latentDim];
        for (var i = 0; i < _latentDim; i++)
        {
            float sum = 0;
            for (var j = 0; j < _hiddenDim; j++) sum += _decW1[i][j] * dDecHidden[j];
            dZ[i] = sum;
        }

        // ── Backprop through encoder ────────────────────────────

        // dMu = dZ + klWeight * mu (KL gradient)
        var dMu = new float[_latentDim];
        var dLogvar = new float[_latentDim];
        for (var i = 0; i < _latentDim; i++)
        {
            dMu[i] = dZ[i] + klWeight * mu[i];
            var eps = (z[i] - mu[i]) / (MathF.Exp(0.5f * logvar[i]) + 1e-8f);
            dLogvar[i] = dZ[i] * 0.5f * MathF.Exp(0.5f * logvar[i]) * eps
                         + klWeight * 0.5f * (MathF.Exp(logvar[i]) - 1f);
        }

        // Mu weights
        for (var j = 0; j < _latentDim; j++)
        {
            _muB[j] -= _learningRate * dMu[j];
            for (var i = 0; i < _hiddenDim; i++)
                _muW[i][j] -= _learningRate * hidden[i] * dMu[j];
        }

        // LogVar weights
        for (var j = 0; j < _latentDim; j++)
        {
            _logvarB[j] -= _learningRate * dLogvar[j];
            for (var i = 0; i < _hiddenDim; i++)
                _logvarW[i][j] -= _learningRate * hidden[i] * dLogvar[j];
        }

        // Encoder hidden gradient
        var dEncHidden = new float[_hiddenDim];
        for (var i = 0; i < _hiddenDim; i++)
        {
            float sum = 0;
            for (var j = 0; j < _latentDim; j++)
                sum += _muW[i][j] * dMu[j] + _logvarW[i][j] * dLogvar[j];
            dEncHidden[i] = sum * (hidden[i] > 0 ? 1f : 0f); // ReLU grad on original hidden pre-activation
        }

        // Encoder W1, b1
        for (var j = 0; j < _hiddenDim; j++)
        {
            _encB1[j] -= _learningRate * dEncHidden[j];
            for (var i = 0; i < _inputDim; i++)
                _encW1[i][j] -= _learningRate * x[i] * dEncHidden[j];
        }

        return (reconLoss, klLoss);
    }

    // ── Weight initialization (Xavier) ──────────────────────────

    private void InitWeights()
    {
        _encW1 = XavierInit(_inputDim, _hiddenDim);
        _encB1 = new float[_hiddenDim];
        _muW = XavierInit(_hiddenDim, _latentDim);
        _muB = new float[_latentDim];
        _logvarW = XavierInit(_hiddenDim, _latentDim);
        _logvarB = new float[_latentDim];
        _decW1 = XavierInit(_latentDim, _hiddenDim);
        _decB1 = new float[_hiddenDim];
        _decW2 = XavierInit(_hiddenDim, _inputDim);
        _decB2 = new float[_inputDim];
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
