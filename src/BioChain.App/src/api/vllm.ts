// VLLM OpenAI-compatible API client (dual GPU — max 2 parallel)

let vllmEndpoint = 'http://localhost:8000';
let vllmModel = '/models/Qwen3.5-A3B';
const MAX_PARALLEL = 2;

export function setVllmConfig(endpoint: string, model: string) {
  vllmEndpoint = endpoint;
  vllmModel = model;
}

export interface ChatMessage {
  role: 'system' | 'user' | 'assistant';
  content: string;
}

interface ChatCompletionResponse {
  choices: { message: { content: string } }[];
}

// Semaphore for GPU concurrency control
let running = 0;
const queue: (() => void)[] = [];

function acquire(): Promise<void> {
  if (running < MAX_PARALLEL) {
    running++;
    return Promise.resolve();
  }
  return new Promise(resolve => queue.push(resolve));
}

function release() {
  running--;
  const next = queue.shift();
  if (next) {
    running++;
    next();
  }
}

export async function chatCompletion(
  messages: ChatMessage[],
  options?: { temperature?: number; maxTokens?: number }
): Promise<string> {
  await acquire();
  try {
    const res = await fetch(`${vllmEndpoint}/v1/chat/completions`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        model: vllmModel,
        messages,
        temperature: options?.temperature ?? 0.3,
        max_tokens: options?.maxTokens ?? 8192,
        repetition_penalty: 1.15,
        top_p: 0.9,
        stop: ['\n\n\n'],
      }),
    });

    if (!res.ok) {
      const text = await res.text();
      throw new Error(`VLLM error (${res.status}): ${text}`);
    }

    const data: ChatCompletionResponse = await res.json();
    return data.choices[0]?.message?.content ?? '';
  } finally {
    release();
  }
}

/** Run multiple completions with concurrency limited to MAX_PARALLEL. */
export function chatCompletionBatch(
  requests: { messages: ChatMessage[]; options?: { temperature?: number; maxTokens?: number } }[]
): Promise<string[]> {
  return Promise.all(requests.map(r => chatCompletion(r.messages, r.options)));
}

export async function checkVllm(): Promise<boolean> {
  try {
    const res = await fetch(`${vllmEndpoint}/v1/models`);
    return res.ok;
  } catch {
    return false;
  }
}
