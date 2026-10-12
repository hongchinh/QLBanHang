import type { CashVoucherType } from '@/features/cash-common/types';

export interface CashVoucherLabels {
  title: string;
  listTitle: string;
  personLabel: string;
  descriptionLabel: string;
  basePath: '/receipts' | '/payments';
  permissionPrefix: 'receipts' | 'payments';
  // A receipt is created by a stock-out and a payment by a stock-in (P20).
  stockVoucherBasePath: '/stock-out' | '/stock-in';
  managedBanner: (stockVoucherCode: string) => string;
}

export const CASH_VOUCHER_LABELS: Record<CashVoucherType, CashVoucherLabels> = {
  Receipt: {
    title: 'Phiếu thu',
    listTitle: 'Danh sách phiếu thu',
    personLabel: 'Người nộp',
    descriptionLabel: 'Lý do nộp',
    basePath: '/receipts',
    permissionPrefix: 'receipts',
    stockVoucherBasePath: '/stock-out',
    managedBanner: (code) => `Phiếu thu tự động theo phiếu xuất ${code} — sửa trên phiếu kho.`,
  },
  Payment: {
    title: 'Phiếu chi',
    listTitle: 'Danh sách phiếu chi',
    personLabel: 'Người nhận',
    descriptionLabel: 'Lý do chi',
    basePath: '/payments',
    permissionPrefix: 'payments',
    stockVoucherBasePath: '/stock-in',
    managedBanner: (code) => `Phiếu chi tự động theo phiếu nhập ${code} — sửa trên phiếu kho.`,
  },
};
