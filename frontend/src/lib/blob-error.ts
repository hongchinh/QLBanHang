import axios from 'axios';

function readBlobText(blob: Blob): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(String(reader.result));
    reader.onerror = () => reject(reader.error);
    reader.readAsText(blob);
  });
}

// A request made with responseType 'blob' delivers an error body as a Blob. When it holds JSON
// (the ApiResponse envelope), put the parsed object back so getApiError can read it. Anything else is returned as is.
export async function unwrapBlobError(error: unknown): Promise<unknown> {
  if (!axios.isAxiosError(error)) return error;
  const data: unknown = error.response?.data;
  if (!(data instanceof Blob) || !data.type.includes('json')) return error;
  try {
    const parsed: unknown = JSON.parse(await readBlobText(data));
    if (error.response) error.response.data = parsed;
  } catch {
    // Not JSON: leave the error as it was.
  }
  return error;
}
