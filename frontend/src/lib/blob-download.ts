import api from '@/lib/api-client';
import { unwrapBlobError } from '@/lib/blob-error';

// PDF conversion of a large book can take a while on the server.
export const FILE_REQUEST_TIMEOUT_MS = 120_000;

export interface BlobRequest {
  method: 'get' | 'post';
  url: string;
  params?: Record<string, unknown>;
  body?: unknown;
}

export async function requestBlob(request: BlobRequest): Promise<Blob> {
  const config = {
    params: request.params,
    responseType: 'blob',
    timeout: FILE_REQUEST_TIMEOUT_MS,
  } as const;
  try {
    const res =
      request.method === 'post'
        ? await api.post(request.url, request.body, config)
        : await api.get(request.url, config);
    return res.data as Blob;
  } catch (error) {
    throw await unwrapBlobError(error);
  }
}

export function saveBlob(blob: Blob, fileName: string): void {
  const objectUrl = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = objectUrl;
  a.download = fileName;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  URL.revokeObjectURL(objectUrl);
}
