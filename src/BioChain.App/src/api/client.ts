// SpacetimeDB HTTP API client

export interface SpacetimeConfig {
  host: string;
  database: string;
  token?: string;
}

let config: SpacetimeConfig = {
  host: 'http://localhost:3000',
  database: 'biochain',
};

export function setConfig(c: SpacetimeConfig) {
  config = c;
}

export function getConfig(): SpacetimeConfig {
  return config;
}

function headers(): Record<string, string> {
  const h: Record<string, string> = { 'Content-Type': 'application/json' };
  if (config.token) h['Authorization'] = `Bearer ${config.token}`;
  return h;
}

export async function callReducer(reducer: string, args: unknown[]): Promise<string> {
  const url = `${config.host}/v1/database/${config.database}/call/${reducer}`;
  const res = await fetch(url, {
    method: 'POST',
    headers: headers(),
    body: JSON.stringify(args),
  });
  if (!res.ok) {
    const text = await res.text();
    throw new Error(`Reducer ${reducer} failed (${res.status}): ${text}`);
  }
  return res.text();
}

// Schema-aware decoder for SpacetimeDB's positional array format.
// Rows are arrays of values; Option<T> is [variant_idx, value] where 0=Some, 1=None.
// Product types (structs) are arrays matching element order.
// eslint-disable-next-line @typescript-eslint/no-explicit-any
function decodeValue(val: any, schema: any): any {
  if (val === null || val === undefined) return val;

  // Sum type (Option<T>, enums) — [variant_index, inner_value]
  if (schema?.Sum) {
    const variants = schema.Sum.variants;
    if (Array.isArray(val) && val.length === 2 && typeof val[0] === 'number') {
      const idx = val[0];
      const inner = val[1];
      const variant = variants[idx];
      const variantName = variant?.name?.some ?? variant?.name;
      if (variantName === 'none') return null;
      if (variantName === 'some') return decodeValue(inner, variant.algebraic_type);
      // Named enum variant — return as object
      return { [variantName]: decodeValue(inner, variant.algebraic_type) };
    }
    return val;
  }

  // Product type (struct) — array of values matching elements
  if (schema?.Product) {
    const elements = schema.Product.elements;
    if (Array.isArray(val) && elements.length > 0) {
      const obj: Record<string, unknown> = {};
      elements.forEach((el: any, i: number) => {
        const name = el.name?.some ?? el.name ?? `_${i}`;
        obj[name] = decodeValue(val[i], el.algebraic_type);
      });
      return obj;
    }
    return val;
  }

  // Array type — decode each element
  if (schema?.Array) {
    if (Array.isArray(val)) {
      return val.map((v: any) => decodeValue(v, schema.Array));
    }
    return val;
  }

  // Primitive types (U64, U32, I64, String, Bool, F32, etc.)
  return val;
}

// eslint-disable-next-line @typescript-eslint/no-explicit-any
function parseStdbResponse(data: any[]): Record<string, unknown>[] {
  if (!data || data.length === 0) return [];
  const table = data[0];
  if (!table.schema || !table.rows) return [];

  const elements = table.schema.elements;
  const columns: string[] = elements.map(
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    (el: any) => el.name?.some ?? el.name ?? 'unknown'
  );

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  return table.rows.map((row: any[]) => {
    const obj: Record<string, unknown> = {};
    columns.forEach((col: string, i: number) => {
      obj[col] = decodeValue(row[i], elements[i].algebraic_type);
    });
    return obj;
  });
}

export async function sql<T = Record<string, unknown>>(query: string): Promise<T[]> {
  const url = `${config.host}/v1/database/${config.database}/sql`;
  const res = await fetch(url, {
    method: 'POST',
    headers: { ...headers(), 'Content-Type': 'text/plain' },
    body: query,
  });
  if (!res.ok) {
    const text = await res.text();
    throw new Error(`SQL failed (${res.status}): ${text}`);
  }
  const raw = await res.json();
  return parseStdbResponse(raw) as T[];
}

export async function checkConnection(): Promise<boolean> {
  try {
    const url = `${config.host}/v1/ping`;
    const res = await fetch(url);
    return res.ok;
  } catch {
    return false;
  }
}
