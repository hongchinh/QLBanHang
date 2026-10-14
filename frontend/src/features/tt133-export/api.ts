import { requestBlob, saveBlob } from '@/lib/blob-download';
import type { ExportFormat, Tt133Report } from './types';

// The multi-target forms send their request as a JSON body (the id lists can be long); the others use the query string.
export const TT133_POST_REPORTS: Tt133Report[] = ['debt-ledger', 'stock-card', 'stock-detail'];

export async function downloadTt133(
  report: Tt133Report,
  format: ExportFormat,
  params: Record<string, unknown>,
  fileBase: string,
): Promise<void> {
  const url = `/reports/${report}/export`;
  const blob = await requestBlob(
    TT133_POST_REPORTS.includes(report)
      ? { method: 'post', url, params: { format }, body: params }
      : { method: 'get', url, params: { ...params, format } },
  );
  saveBlob(blob, `${fileBase}.${format}`);
}
