import api from '@/lib/api-client';
import { unwrapBlobError } from '@/lib/blob-error';
import type { ExportFormat, Tt133Report } from './types';

// The multi-target forms send their request as a JSON body (the id lists can be long); the others use the query string.
export const TT133_POST_REPORTS: Tt133Report[] = ['debt-ledger', 'stock-card', 'stock-detail'];

// PDF conversion of a large book can take a while on the server.
const EXPORT_TIMEOUT_MS = 120_000;

export async function downloadTt133(
  report: Tt133Report,
  format: ExportFormat,
  params: Record<string, unknown>,
  fileBase: string,
): Promise<void> {
  const url = `/reports/${report}/export`;
  const config = { responseType: 'blob', timeout: EXPORT_TIMEOUT_MS } as const;

  let blob: Blob;
  try {
    const res = TT133_POST_REPORTS.includes(report)
      ? await api.post(url, params, { params: { format }, ...config })
      : await api.get(url, { params: { ...params, format }, ...config });
    blob = res.data as Blob;
  } catch (error) {
    throw await unwrapBlobError(error);
  }

  const objectUrl = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = objectUrl;
  a.download = `${fileBase}.${format}`;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  URL.revokeObjectURL(objectUrl);
}
