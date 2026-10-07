import type { StockDirection } from '@/features/stock-vouchers/types';

export interface StockVoucherLabels {
  title: string;
  listTitle: string;
  dateLabel: string;
  reasonLabel: string;
  handlerLabel: string;
  basePath: '/stock-in' | '/stock-out';
  permissionPrefix: 'stock_in' | 'stock_out';
}

export const STOCK_VOUCHER_LABELS: Record<StockDirection, StockVoucherLabels> = {
  In: {
    title: 'Phiếu nhập kho',
    listTitle: 'Danh sách phiếu nhập kho',
    dateLabel: 'Ngày nhập',
    reasonLabel: 'Lý do nhập',
    handlerLabel: 'Người giao hàng',
    basePath: '/stock-in',
    permissionPrefix: 'stock_in',
  },
  Out: {
    title: 'Phiếu xuất kho',
    listTitle: 'Danh sách phiếu xuất kho',
    dateLabel: 'Ngày xuất',
    reasonLabel: 'Lý do xuất',
    handlerLabel: 'Người nhận hàng',
    basePath: '/stock-out',
    permissionPrefix: 'stock_out',
  },
};
