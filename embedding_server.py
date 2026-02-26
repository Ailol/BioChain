"""Minimal OpenAI-compatible embedding server for Qwen3-VL-Embedding-2B.

Loads with Qwen3VLForConditionalGeneration, extracts last hidden state,
mean-pools over tokens, normalizes. Supports MRL dimension truncation.
"""

import time, torch, uvicorn
from fastapi import FastAPI
from pydantic import BaseModel

MODEL_NAME = "Qwen/Qwen3-VL-Embedding-2B"

app = FastAPI()
tokenizer = None
model = None


class EmbeddingRequest(BaseModel):
    input: str | list[str]
    model: str = MODEL_NAME
    dimensions: int | None = None


@app.on_event("startup")
def load_model():
    global tokenizer, model
    from transformers import AutoTokenizer, Qwen3VLForConditionalGeneration

    print(f"Loading {MODEL_NAME}...")
    tokenizer = AutoTokenizer.from_pretrained(MODEL_NAME, trust_remote_code=True)
    model = Qwen3VLForConditionalGeneration.from_pretrained(
        MODEL_NAME, trust_remote_code=True, dtype=torch.bfloat16
    ).cuda().eval()
    print(f"Model loaded on {next(model.parameters()).device}")


def embed_texts(texts: list[str], dimensions: int | None = None) -> list[list[float]]:
    inputs = tokenizer(
        texts, padding=True, truncation=True, max_length=4096, return_tensors="pt"
    ).to("cuda")
    with torch.no_grad():
        outputs = model(
            input_ids=inputs["input_ids"],
            attention_mask=inputs["attention_mask"],
            output_hidden_states=True,
        )
    # Last hidden state from the language model decoder
    hidden = outputs.hidden_states[-1]
    # Mean pool over token dimension
    mask = inputs["attention_mask"].unsqueeze(-1).to(hidden.dtype)
    embeddings = (hidden * mask).sum(1) / mask.sum(1)
    # Normalize
    embeddings = torch.nn.functional.normalize(embeddings, p=2, dim=1)
    # Truncate dimensions if requested (MRL)
    if dimensions and dimensions < embeddings.shape[1]:
        embeddings = embeddings[:, :dimensions]
        embeddings = torch.nn.functional.normalize(embeddings, p=2, dim=1)
    return embeddings.float().cpu().tolist()


@app.post("/v1/embeddings")
def create_embeddings(req: EmbeddingRequest):
    texts = [req.input] if isinstance(req.input, str) else req.input
    vectors = embed_texts(texts, req.dimensions)
    return {
        "object": "list",
        "data": [
            {"object": "embedding", "index": i, "embedding": v}
            for i, v in enumerate(vectors)
        ],
        "model": req.model,
        "usage": {"prompt_tokens": sum(len(t.split()) for t in texts), "total_tokens": sum(len(t.split()) for t in texts)},
    }


@app.get("/v1/models")
def list_models():
    return {
        "object": "list",
        "data": [{"id": MODEL_NAME, "object": "model", "created": int(time.time()), "owned_by": "local"}],
    }


@app.get("/ping")
def ping():
    return "pong"


if __name__ == "__main__":
    uvicorn.run(app, host="0.0.0.0", port=7001)
