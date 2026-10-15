import type { CashVoucherType } from '@/features/cash-common/types';
import type { StockDirection } from '@/features/stock-reasons/types';
import type { ExportFormat } from '@/features/tt133-export/types';

export type VoucherPrintKind = 'cash' | 'stock';
export type StockPrintPriceMode = 'Voucher' | 'Cost' | 'None';

export interface VoucherPrintTarget {
  id: string;
  code: string;
}

export interface VoucherPrintRequest {
  kind: VoucherPrintKind;
  type: CashVoucherType | StockDirection;
  vouchers: VoucherPrintTarget[];
  format: ExportFormat;
  priceMode?: StockPrintPriceMode; // stock only
  includeQr?: boolean; // receipts and stock-out vouchers: append the transfer QR
}

const FILE_PREFIX: Record<string, string> = {
  Receipt: 'PhieuThu',
  Payment: 'PhieuChi',
  In: 'PhieuNhapKho',
  Out: 'PhieuXuatKho',
};

const pad = (n: number) => String(n).padStart(2, '0');

// One voucher -> its code; several -> `{prefix}_{yyyyMMdd-HHmm}` in Vietnam time (UTC+7, no DST), like the backend.
export function voucherPrintFileBase(
  request: Pick<VoucherPrintRequest, 'kind' | 'type' | 'vouchers'>,
  now: Date = new Date(),
): string {
  if (request.vouchers.length === 1) return request.vouchers[0].code;
  const vn = new Date(now.getTime() + 7 * 3600_000);
  const stamp = `${vn.getUTCFullYear()}${pad(vn.getUTCMonth() + 1)}${pad(vn.getUTCDate())}-${pad(vn.getUTCHours())}${pad(vn.getUTCMinutes())}`;
  return `${FILE_PREFIX[request.type]}_${stamp}`;
}
