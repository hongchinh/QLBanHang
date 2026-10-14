import { useId } from 'react';
import type { ReactNode } from 'react';
import { StatTile } from '@/features/dashboard/components/stat-tile';
import { formatNumber, formatPct, formatSignedCompact, formatVndCompact } from '@/features/dashboard/format';
import { useCashSummary, useDebtSummary, useInventorySummary, useSalesSummary } from '@/features/executive-dashboard/hooks';
import { executiveLinks, reportLinkTitle } from '@/features/executive-dashboard/links';
import type { ExecutiveParams, SalesSummary } from '@/features/executive-dashboard/types';
import { useExecutiveAccess } from '@/features/executive-dashboard/use-executive-access';
import { useExecutiveScope } from '@/features/executive-dashboard/use-executive-scope';
import { cn } from '@/lib/utils';
import { todayYmd } from '@/lib/vn-datetime';

interface KpiRowProps {
  params: ExecutiveParams;
}

// What a tile reads of its query.
interface SummaryQuery {
  isLoading: boolean;
  isError: boolean;
  data?: unknown;
  refetch: () => unknown;
}

function queryState(query: SummaryQuery) {
  return {
    loading: query.isLoading,
    // A failed background refresh keeps the figures on screen.
    error: query.isError && query.data === undefined,
    onRetry: () => void query.refetch(),
  };
}

// "Tỷ suất 14,5% (▲ 0,8 điểm)": the margin and its change against the previous period, in points.
function marginHint(sales: SalesSummary): string {
  const margin = `Tỷ suất ${formatPct(sales.marginPct)}`;
  if (sales.marginPct == null || sales.prevMarginPct == null) return margin;
  const change = Math.round((sales.marginPct - sales.prevMarginPct) * 10) / 10;
  return `${margin} (${change >= 0 ? '▲' : '▼'} ${formatNumber(Math.abs(change))} điểm)`;
}

// The KPI tiles of the Tổng quan tab: "Trong kỳ" follows the period, "Hiện tại" is the balance of today.
// A tile shows only when the user may see its domain; the request of a hidden domain is not sent.
export function KpiRow({ params }: KpiRowProps) {
  const access = useExecutiveAccess();
  const scope = useExecutiveScope();
  const sales = useSalesSummary(params, { enabled: access.sales });
  const cash = useCashSummary(params, { enabled: access.cash });
  const debt = useDebtSummary({ branchId: params.branchId }, { enabled: access.debt });
  const inventory = useInventorySummary(params, { enabled: access.inventory });

  const today = todayYmd();
  const linkTitle = reportLinkTitle(scope.mode, scope.branches.length, scope.workingBranch?.code);
  const s = sales.data;
  const c = cash.data;
  const d = debt.data;
  const inv = inventory.data;
  const canViewCost = s?.canViewCost === true;

  const periodTiles: ReactNode[] = [];
  const nowTiles: ReactNode[] = [];

  if (access.sales) {
    periodTiles.push(
      <StatTile
        key="revenue"
        label="Doanh thu xuất bán"
        value={s ? formatVndCompact(s.revenue) : null}
        delta={s ? s.revenueDeltaPct : undefined}
        hint="so với kỳ trước"
        {...queryState(sales)}
      />,
      <StatTile
        key="gross-profit"
        label="Lãi gộp"
        value={s && canViewCost && s.grossProfit != null ? formatVndCompact(s.grossProfit) : null}
        badge={s?.isProvisional && canViewCost ? 'tạm tính' : undefined}
        hint={s && canViewCost ? marginHint(s) : undefined}
        {...queryState(sales)}
      />,
    );
  }
  if (access.cash) {
    periodTiles.push(
      <StatTile
        key="net"
        label="Thu − chi ròng"
        value={c ? formatSignedCompact(c.net) : null}
        delta={c ? c.netDeltaPct : undefined}
        hint={c ? `Thu ${formatVndCompact(c.receipts)} · Chi ${formatVndCompact(c.payments)}` : undefined}
        {...queryState(cash)}
      />,
    );
    nowTiles.push(
      <StatTile
        key="funds"
        label="Tồn quỹ"
        value={c ? formatVndCompact(c.balance) : null}
        hint={c ? `TM ${formatVndCompact(c.cashBalance)} · NH ${formatVndCompact(c.bankBalance)}` : undefined}
        hintTone={c && c.balance < 0 ? 'bad' : 'default'}
        to={executiveLinks.cashBook()}
        title={linkTitle}
        {...queryState(cash)}
      />,
    );
  }
  if (access.debt) {
    nowTiles.push(
      <StatTile
        key="receivable"
        label="Phải thu"
        value={d ? formatVndCompact(d.receivable.balance) : null}
        hint={d ? `Quá hạn ${formatVndCompact(d.receivable.overdue)}` : undefined}
        hintTone={d && d.receivable.overdue > 0 ? 'bad' : 'default'}
        to={executiveLinks.debtOverdue('Receivable', today)}
        title={linkTitle}
        {...queryState(debt)}
      />,
      <StatTile
        key="payable"
        label="Phải trả"
        value={d ? formatVndCompact(d.payable.balance) : null}
        hint={d ? `Đến hạn 7 ngày: ${formatVndCompact(d.payable.dueSoon)}` : undefined}
        hintTone={d && d.payable.dueSoon > 0 ? 'warn' : 'default'}
        to={executiveLinks.debtBalance('Payable', params.from, params.to)}
        title={linkTitle}
        {...queryState(debt)}
      />,
    );
  }
  if (access.inventory) {
    nowTiles.push(
      <StatTile
        key="stock"
        label="Giá trị tồn kho"
        value={inv?.canViewCost && inv.stockValue != null ? formatVndCompact(inv.stockValue) : null}
        hint={inv ? `${formatNumber(inv.productCount)} mã · ${formatNumber(inv.warehouseCount)} kho` : undefined}
        to={executiveLinks.stockOnHand()}
        title={linkTitle}
        {...queryState(inventory)}
      />,
    );
  }

  if (periodTiles.length === 0 && nowTiles.length === 0) return null;

  // One grid of seven columns on wide screens: the two group labels share the first row, the tiles the
  // second. Narrower, each label spans the row above its own tiles.
  return (
    <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4 xl:grid-cols-7">
      {periodTiles.length > 0 && (
        <KpiGroup label="Trong kỳ" span="xl:col-span-3">
          {periodTiles}
        </KpiGroup>
      )}
      {nowTiles.length > 0 && (
        <KpiGroup label="Hiện tại" span="xl:col-span-4">
          {nowTiles}
        </KpiGroup>
      )}
    </div>
  );
}

function KpiGroup({ label, span, children }: { label: string; span: string; children: ReactNode }) {
  const id = useId();
  return (
    // `contents`: the label and the tiles are items of the parent grid.
    <div role="group" aria-labelledby={id} className="contents">
      <h2
        id={id}
        className={cn('col-span-full text-xs font-medium uppercase tracking-wide text-muted-foreground xl:row-start-1', span)}
      >
        {label}
      </h2>
      {children}
    </div>
  );
}
