import { Link } from 'react-router-dom';
import { usePartnerDebtSummary } from '@/features/debt/hooks';
import { cn } from '@/lib/utils';
import { useAuthStore } from '@/stores/auth-store';
import { debtAfterVoucher, type VoucherDebtImpact } from '@/pages/stock-vouchers/utils/debt-after-voucher';

const fmt = new Intl.NumberFormat('vi-VN');
const UNLIMITED = 'Không giới hạn';

export interface PartnerDebtStripProps {
  partnerId: string;
  partnerLabel: string;
  current: VoucherDebtImpact;
  // The stored impact of the voucher being edited (Active, same partner).
  saved?: VoucherDebtImpact;
  cancelled: boolean;
}

// Debt line under the totals bar (P6): the partner's position in the working branch and after this voucher.
// Display only; the form decides when it is shown (partner picked, permission).
export function PartnerDebtStrip({ partnerId, partnerLabel, current, saved, cancelled }: PartnerDebtStripProps) {
  const { data, isLoading, isError } = usePartnerDebtSummary(partnerId, { enabled: true });
  const canViewOverdueReport = useAuthStore((s) => s.hasPermission('reports.debt'));

  if (isError || (!data && !isLoading)) return null;
  if (!data) {
    return <div className="h-7 animate-pulse rounded bg-muted" data-testid="partner-debt-skeleton" aria-busy="true" />;
  }

  const after = cancelled ? null : debtAfterVoucher(data, current, saved);
  const available = after?.receivableChanged ? after.availableAfter : data.available ?? null;
  return (
    <div className="flex flex-wrap items-center gap-x-3 gap-y-0.5 whitespace-nowrap border-t border-dashed bg-muted/40 px-3 py-1.5 text-xs text-muted-foreground">
      <span className="max-w-[28ch] truncate font-semibold text-foreground" title={partnerLabel}>{partnerLabel}</span>
      <span>
        Phải thu <Value testId="partner-debt-receivable" value={data.receivableBalance} />
        {after?.receivableChanged && <> → sau phiếu <Value testId="partner-debt-receivable-after" value={after.receivableAfter} /></>}
      </span>
      <span>
        Phải trả <Value testId="partner-debt-payable" value={data.payableBalance} />
        {after?.payableChanged && <> → sau phiếu <Value testId="partner-debt-payable-after" value={after.payableAfter} /></>}
      </span>
      <span>
        Hạn mức <b className="text-foreground" data-testid="partner-debt-limit">{data.creditLimit == null ? UNLIMITED : fmt.format(data.creditLimit)}</b>
      </span>
      <span>
        {after?.receivableChanged ? 'Còn được nợ sau phiếu' : 'Còn được nợ'}{' '}
        <b
          className={cn('tabular-nums text-foreground', available != null && available < 0 && 'text-destructive dark:text-red-400')}
          data-testid="partner-debt-available"
        >
          {available == null ? UNLIMITED : fmt.format(available)}
        </b>
      </span>
      <span>
        Quá hạn{' '}
        <b className={cn('tabular-nums text-foreground', data.overdueAmount > 0 && 'text-destructive dark:text-red-400')} data-testid="partner-debt-overdue">
          {fmt.format(data.overdueAmount)}
        </b>
        {data.overdueAmount > 0 && canViewOverdueReport && (
          <>
            {' '}
            <Link
              to={`/reports/debt-overdue?side=Receivable&partnerId=${partnerId}`}
              target="_blank"
              rel="noopener noreferrer"
              className="text-primary underline"
            >
              Xem
            </Link>
          </>
        )}
      </span>
      <span>
        Số ngày nợ <b className="text-foreground" data-testid="partner-debt-credit-days">{data.creditDays == null ? '—' : String(data.creditDays)}</b>
      </span>
    </div>
  );
}

function Value({ value, testId }: { value: number; testId: string }) {
  return <b className="tabular-nums text-foreground" data-testid={testId}>{fmt.format(value)}</b>;
}
