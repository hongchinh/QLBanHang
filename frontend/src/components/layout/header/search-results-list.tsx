import { Loader2, Search } from 'lucide-react';
import type { CustomerSearchItem, GlobalSearchResult } from '@/features/search/api';
import { cn } from '@/lib/utils';

interface SearchResultsListProps {
  data: GlobalSearchResult | undefined;
  isLoading: boolean;
  activeIndex: number;
  onSelectCustomer: (item: CustomerSearchItem) => void;
  onSelectSupplier: (item: CustomerSearchItem) => void;
  onSelectQuotation: (id: string) => void;
}

const STATUS_LABEL: Record<string, string> = {
  Draft: 'Nháp',
  Sent: 'Đã gửi',
  Confirmed: 'Đã xác nhận',
  Cancelled: 'Đã huỷ',
};

function formatCurrency(value: number): string {
  return new Intl.NumberFormat('vi-VN').format(value);
}

export function SearchResultsList({
  data,
  isLoading,
  activeIndex,
  onSelectCustomer,
  onSelectSupplier,
  onSelectQuotation,
}: SearchResultsListProps) {
  if (isLoading) {
    return (
      <div className="flex items-center gap-2 px-2 py-4 text-sm text-muted-foreground">
        <Loader2 className="h-4 w-4 animate-spin" />
        Đang tìm…
      </div>
    );
  }

  const customers = data?.customers ?? [];
  const suppliers = data?.suppliers ?? [];
  const quotations = data?.quotations ?? [];

  if (customers.length === 0 && suppliers.length === 0 && quotations.length === 0) {
    return (
      <div className="px-2 py-4 text-sm text-muted-foreground">Không có kết quả.</div>
    );
  }

  let index = 0;
  const partnerGroup = (title: string, items: CustomerSearchItem[], onSelect: (item: CustomerSearchItem) => void) =>
    items.length > 0 && (
      <div className="mb-1">
        <div className="px-2 py-1 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
          {title}
        </div>
        {items.map((c) => {
          const i = index++;
          const isActive = i === activeIndex;
          return (
            <button
              type="button"
              role="option"
              aria-selected={isActive}
              key={c.id}
              onClick={() => onSelect(c)}
              className={cn(
                'flex w-full items-center gap-3 rounded-sm px-2 py-2 text-left text-sm',
                isActive ? 'bg-accent text-accent-foreground' : 'hover:bg-accent/60',
              )}
            >
              <Search className="h-4 w-4 shrink-0 text-muted-foreground" />
              <div className="min-w-0 flex-1">
                <div className="truncate font-medium">{c.name}</div>
                <div className="truncate text-xs text-muted-foreground">
                  {c.code}
                  {c.phoneNumber ? ` · ${c.phoneNumber}` : ''}
                </div>
              </div>
            </button>
          );
        })}
      </div>
    );

  return (
    <div role="listbox" className="max-h-[60vh] overflow-y-auto">
      {partnerGroup('Khách hàng', customers, onSelectCustomer)}
      {partnerGroup('Nhà cung cấp', suppliers, onSelectSupplier)}
      {quotations.length > 0 && (
        <div>
          <div className="px-2 py-1 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
            Báo giá
          </div>
          {quotations.map((q) => {
            const i = index++;
            const isActive = i === activeIndex;
            return (
              <button
                type="button"
                role="option"
                aria-selected={isActive}
                key={q.id}
                onClick={() => onSelectQuotation(q.id)}
                className={cn(
                  'flex w-full items-center gap-3 rounded-sm px-2 py-2 text-left text-sm',
                  isActive ? 'bg-accent text-accent-foreground' : 'hover:bg-accent/60',
                )}
              >
                <Search className="h-4 w-4 shrink-0 text-muted-foreground" />
                <div className="min-w-0 flex-1">
                  <div className="flex items-center justify-between gap-2">
                    <span className="truncate font-medium">{q.code}</span>
                    <span className="shrink-0 rounded-sm bg-muted px-1.5 py-0.5 text-xs text-muted-foreground">
                      {STATUS_LABEL[q.status] ?? q.status}
                    </span>
                  </div>
                  <div className="truncate text-xs text-muted-foreground">
                    {q.customerName} · {formatCurrency(q.total)} đ
                  </div>
                </div>
              </button>
            );
          })}
        </div>
      )}
    </div>
  );
}

