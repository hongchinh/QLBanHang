import type { StockTotals } from '@/pages/stock-vouchers/utils/compute-stock-line';

const fmt = new Intl.NumberFormat('vi-VN');

export type TotalsJumpField = 'freight' | 'orderDiscount' | 'paidAmount';

export interface StockTotalsBarProps {
  totals: StockTotals;
  freight: number;
  orderDiscount: number;
  paidAmount: number;
  lineCount: number;
  // Opens the payment tab on the matching input.
  onJump: (field: TotalsJumpField) => void;
}

// Read-only totals under the grid; the amounts are edited in the payment tab.
export function StockTotalsBar({ totals, freight, orderDiscount, paidAmount, lineCount, onJump }: StockTotalsBarProps) {
  return (
    <div className="flex flex-wrap items-center justify-between gap-x-6 gap-y-1 px-3 py-2 text-sm">
      <div className="flex flex-wrap items-center gap-x-5 gap-y-1 text-muted-foreground">
        <span data-testid="stock-totals-lines">{lineCount} dòng</span>
        <Amount label="Tiền hàng" value={totals.goodsAmount} testId="stock-totals-goods" />
        <Amount label="CK" value={totals.lineDiscountTotal} testId="stock-totals-discount" />
        <Amount label="VAT" value={totals.vatTotal} testId="stock-totals-vat" />
        <JumpAmount label="Phí VC" value={freight} onClick={() => onJump('freight')} />
        <JumpAmount label="CK đơn" value={orderDiscount} onClick={() => onJump('orderDiscount')} />
        <JumpAmount label="Đã thanh toán" value={paidAmount} onClick={() => onJump('paidAmount')} />
      </div>
      <div className="text-base font-extrabold text-primary">
        Tổng thanh toán: <span className="tabular-nums" data-testid="stock-totals-total">{fmt.format(totals.total)}</span>
      </div>
    </div>
  );
}

function Amount({ label, value, testId }: { label: string; value: number; testId: string }) {
  return (
    <span>
      {label} <b className="font-semibold tabular-nums text-foreground" data-testid={testId}>{fmt.format(value)}</b>
    </span>
  );
}

function JumpAmount({ label, value, onClick }: { label: string; value: number; onClick: () => void }) {
  return (
    <button type="button" className="hover:underline" onClick={onClick}>
      {label} <b className="font-semibold tabular-nums text-foreground">{fmt.format(value)}</b>
    </button>
  );
}
