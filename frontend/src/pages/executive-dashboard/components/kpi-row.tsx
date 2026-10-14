import { useId } from 'react';
import type { ReactNode } from 'react';
import { StatTile } from '@/features/dashboard/components/stat-tile';
import { formatNumber, formatPct, formatSignedCompact, formatVndCompact } from '@/features/dashboard/format';
import { statTileState } from '@/features/dashboard/stat-tile-state';
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

// The width of a group on wide screens: as many of the seven columns as it has tiles (static class names,
// so Tailwind keeps them).
const XL_SPAN: Record<number, string> = {
  1: 'xl:col-span-1',
  2: 'xl:col-span-2',
  3: 'xl:col-span-3',
  4: 'xl:col-span-4',
};

// "Tỷ suất 14,5% (▲ 0,8 điểm)": the margin and its change against the previous period, in points.
function marginHint(sales: SalesSummary): string {
  const margin = `Tỷ suất ${formatPct(sales.marginPct)}`;
  if (sales.marginPct == null || sales.prevMarginPct == null) return margin;
  // Rounded half away from zero to one decimal, worked in hundredths (the server sends two decimals): the
  // floating-point difference of 14,56 and 13,71 is 0,8499…, which would round to 0,8 instead of 0,9.
  const hundredths = Math.round(sales.marginPct * 100) - Math.round(sales.prevMarginPct * 100);
  const change = (Math.sign(hundredths) * Math.round(Math.abs(hundredths) / 10)) / 10;
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
        {...statTileState(sales)}
      />,
      <StatTile
        key="gross-profit"
        label="Lãi gộp"
        value={s && canViewCost && s.grossProfit != null ? formatVndCompact(s.grossProfit) : null}
        badge={s?.isProvisional && canViewCost ? 'tạm tính' : undefined}
        hint={s && canViewCost ? marginHint(s) : undefined}
        {...statTileState(sales)}
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
        {...statTileState(cash)}
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
        {...statTileState(cash)}
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
        {...statTileState(debt)}
      />,
      <StatTile
        key="payable"
        label="Phải trả"
        value={d ? formatVndCompact(d.payable.balance) : null}
        hint={d ? `Đến hạn 7 ngày: ${formatVndCompact(d.payable.dueSoon)}` : undefined}
        hintTone={d && d.payable.dueSoon > 0 ? 'warn' : 'default'}
        to={executiveLinks.debtBalance('Payable', params.from, params.to)}
        title={linkTitle}
        {...statTileState(debt)}
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
        {...statTileState(inventory)}
      />,
    );
  }

  if (periodTiles.length === 0 && nowTiles.length === 0) return null;

  // One grid of seven columns on wide screens, where the two groups sit side by side, each as wide as its
  // tiles. Narrower, each group takes the full width. A group shares the columns of the row (subgrid), so its
  // label spans exactly its own tiles.
  return (
    <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4 xl:grid-cols-7">
      {periodTiles.length > 0 && <KpiGroup label="Trong kỳ">{periodTiles}</KpiGroup>}
      {nowTiles.length > 0 && <KpiGroup label="Hiện tại">{nowTiles}</KpiGroup>}
    </div>
  );
}

function KpiGroup({ label, children }: { label: string; children: ReactNode[] }) {
  const id = useId();
  return (
    <div
      role="group"
      aria-labelledby={id}
      className={cn('col-span-full grid grid-cols-subgrid content-start gap-3', XL_SPAN[children.length])}
    >
      <h2 id={id} className="col-span-full text-xs font-medium uppercase tracking-wide text-muted-foreground">
        {label}
      </h2>
      {children}
    </div>
  );
}
