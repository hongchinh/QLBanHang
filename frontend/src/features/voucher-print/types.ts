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
}

const FILE_PREFIX: Record<string, string> = {
  Receipt: 'PhieuThu',
  Payment: 'PhieuChi',
  In: 'PhieuNhapKho',
  Out: 'PhieuXuatKho',
};

const pad = (n: number) => String(n).padStart(2, '0');

// One voucher -> its code; several -> `{prefix}_{yyyyMMdd-HHmm}` in local time.
export function voucherPrintFileBase(
  request: Pick<VoucherPrintRequest, 'kind' | 'type' | 'vouchers'>,
  now: Date = new Date(),
): string {
  if (request.vouchers.length === 1) return request.vouchers[0].code;
  const stamp = `${now.getFullYear()}${pad(now.getMonth() + 1)}${pad(now.getDate())}-${pad(now.getHours())}${pad(now.getMinutes())}`;
  return `${FILE_PREFIX[request.type]}_${stamp}`;
}
