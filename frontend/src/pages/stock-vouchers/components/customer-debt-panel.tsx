import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { usePartnerDebtSummary } from '@/features/debt/hooks';
import { cn } from '@/lib/utils';

const fmt = new Intl.NumberFormat('vi-VN');
const UNLIMITED = 'Không giới hạn';

// Debt panel of the stock-out form (P6): the customer's receivable position in the working branch.
// Display only; the form decides when it is shown (sale reason, partner picked, permission).
export function CustomerDebtPanel({ partnerId }: { partnerId: string }) {
  const { data, isLoading, isError } = usePartnerDebtSummary(partnerId, { enabled: true });

  if (isError) return null;
  if (!data && !isLoading) return null;

  return (
    <Card>
      <CardHeader className="flex-row items-center border-b p-1 px-3 h-9 bg-blue-50">
        <CardTitle>Công nợ khách hàng</CardTitle>
      </CardHeader>
      <CardContent className="space-y-1.5 px-3.5 pt-2.5 pb-3">
        {!data ? (
          <div className="space-y-1.5" data-testid="customer-debt-skeleton" aria-busy="true">
            {Array.from({ length: 5 }, (_, i) => (
              <div key={i} className="h-5 animate-pulse rounded bg-muted" />
            ))}
          </div>
        ) : (
          <>
            <DebtRow label="Dư nợ phải thu" testId="customer-debt-balance" value={fmt.format(data.receivableBalance)} />
            <DebtRow
              label="Hạn mức"
              testId="customer-debt-limit"
              value={data.creditLimit == null ? UNLIMITED : fmt.format(data.creditLimit)}
            />
            <DebtRow
              label="Còn được nợ"
              testId="customer-debt-available"
              value={data.available == null ? UNLIMITED : fmt.format(data.available)}
              danger={data.available != null && data.available < 0}
            />
            <DebtRow
              label="Nợ quá hạn"
              testId="customer-debt-overdue"
              value={fmt.format(data.overdueAmount)}
              danger={data.overdueAmount > 0}
            />
            <DebtRow
              label="Số ngày được nợ"
              testId="customer-debt-credit-days"
              value={data.creditDays == null ? '—' : String(data.creditDays)}
            />
          </>
        )}
      </CardContent>
    </Card>
  );
}

function DebtRow({ label, value, testId, danger = false }: { label: string; value: string; testId: string; danger?: boolean }) {
  return (
    <div className="flex items-center justify-between gap-2">
      <span className="text-sm text-muted-foreground">{label}</span>
      <span className={cn('text-sm font-semibold tabular-nums', danger && 'text-destructive')} data-testid={testId}>
        {value}
      </span>
    </div>
  );
}
