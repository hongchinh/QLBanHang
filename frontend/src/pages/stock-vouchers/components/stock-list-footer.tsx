import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Button } from '@/components/ui/button';
import type { StockVoucherListAggregates } from '@/features/stock-vouchers/types';

const currency = new Intl.NumberFormat('vi-VN');

// Copy of the quotation ListFooter typed to the voucher aggregates.
interface StockListFooterProps {
  totalItems: number;
  aggregates?: StockVoucherListAggregates;
  page: number;
  totalPages: number;
  pageSize: number;
  pageSizeOptions: readonly number[];
  hasPrev: boolean;
  hasNext: boolean;
  onPageChange: (page: number) => void;
  onPageSizeChange: (size: number) => void;
  note?: string;
  loading?: boolean;
  errored?: boolean;
}

interface MoneyProps {
  value?: number | null;
  loading?: boolean;
  errored?: boolean;
  strong?: boolean;
}

function Money({ value, loading, errored, strong }: MoneyProps) {
  if (loading) {
    return (
      <span
        className="inline-block h-3 w-16 animate-pulse rounded bg-muted align-middle"
        aria-label="đang tải"
      />
    );
  }
  const text = errored || typeof value !== 'number' ? '—' : currency.format(value);
  return <span className={`tabular-nums ${strong ? 'font-semibold text-foreground' : ''}`}>{text}</span>;
}

export function StockListFooter({
  totalItems,
  aggregates,
  page,
  totalPages,
  pageSize,
  pageSizeOptions,
  hasPrev,
  hasNext,
  onPageChange,
  onPageSizeChange,
  note,
  loading,
  errored,
}: StockListFooterProps) {
  const displayPages = Math.max(totalPages, 1);
  return (
    <div className="flex flex-wrap items-center justify-between gap-2 border-t pt-3 text-sm">
      <div className="text-muted-foreground" role="group" aria-label="Tổng kết phiếu">
        Tổng <strong className="text-foreground">{totalItems}</strong> phiếu
        {' • '}Tiền hàng <Money value={aggregates?.goodsAmount} loading={loading} errored={errored} />
        {' • '}CK <Money value={aggregates?.discountTotal} loading={loading} errored={errored} />
        {' • '}VAT <Money value={aggregates?.vatTotal} loading={loading} errored={errored} />
        {' • '}VC <Money value={aggregates?.freight} loading={loading} errored={errored} />
        {' • '}
        <span className="font-semibold text-foreground">
          Tổng TT <Money value={aggregates?.total} loading={loading} errored={errored} strong />
        </span>
        {' • '}Đã TT <Money value={aggregates?.paidAmount} loading={loading} errored={errored} />
        {note && <span className="ml-1 italic">({note})</span>}
      </div>
      <div className="flex items-center gap-2">
        <Select value={String(pageSize)} onValueChange={(v) => onPageSizeChange(Number(v))}>
          <SelectTrigger className="w-20" aria-label="Số dòng mỗi trang">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {pageSizeOptions.map((s) => (
              <SelectItem key={s} value={String(s)}>
                {s}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        <span className="text-muted-foreground">
          Trang {page}/{displayPages}
        </span>
        <Button variant="outline" size="sm" disabled={!hasPrev} onClick={() => onPageChange(page - 1)}>
          Trước
        </Button>
        <Button variant="outline" size="sm" disabled={!hasNext} onClick={() => onPageChange(page + 1)}>
          Sau
        </Button>
      </div>
    </div>
  );
}
