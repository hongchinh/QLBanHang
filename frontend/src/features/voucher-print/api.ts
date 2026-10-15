import { requestBlob } from '@/lib/blob-download';
import type { VoucherPrintRequest } from './types';

export function fetchVoucherPrint(request: VoucherPrintRequest): Promise<Blob> {
  const base = request.kind === 'cash' ? '/cash-vouchers' : '/stock-vouchers';
  const priceMode = request.kind === 'stock' && request.priceMode ? request.priceMode : undefined;
  const includeQr = request.includeQr === true;
  const params = { format: request.format, ...(priceMode ? { priceMode } : {}), ...(includeQr ? { includeQr } : {}) };

  if (request.vouchers.length === 1) {
    return requestBlob({ method: 'get', url: `${base}/${request.vouchers[0].id}/print`, params });
  }
  return requestBlob({
    method: 'post',
    url: `${base}/print`,
    params: { format: request.format },
    body: { ids: request.vouchers.map((v) => v.id), ...(priceMode ? { priceMode } : {}), ...(includeQr ? { includeQr } : {}) },
  });
}
