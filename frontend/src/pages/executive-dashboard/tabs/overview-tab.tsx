import { TileCard } from '@/features/dashboard/components/tile-card';
import { TopListCard } from '@/features/dashboard/components/top-list-card';
import { formatVndCompact } from '@/features/dashboard/format';
import {
  useAttention,
  useBranchComparison,
  useCashSeries,
  useCashSummary,
  useDebtSummary,
  useRecentDocuments,
  useSalesSeries,
  useSalesTop,
} from '@/features/executive-dashboard/hooks';
import { executiveLinks, reportLinkTitle } from '@/features/executive-dashboard/links';
import { isDayGranularityAllowed } from '@/features/executive-dashboard/period';
import { isCashSeriesEmpty, isSalesSeriesEmpty, usePeriodGranularity } from '@/features/executive-dashboard/series';
import type { CashPoint, ExecutiveParams, ExecutiveTab, SalesPoint } from '@/features/executive-dashboard/types';
import { useExecutiveAccess } from '@/features/executive-dashboard/use-executive-access';
import { useExecutiveScope } from '@/features/executive-dashboard/use-executive-scope';
import { todayYmd } from '@/lib/vn-datetime';
import { AttentionList } from '../components/attention-list';
import { BranchComparisonTable } from '../components/branch-comparison-table';
import { CashFlowChart } from '../components/cash-flow-chart';
import { DebtTermBar } from '../components/debt-term-bar';
import { FundBalanceTable } from '../components/fund-balance-table';
import { KpiRow } from '../components/kpi-row';
import { ProfitChart } from '../components/profit-chart';
import { RecentFeed } from '../components/recent-feed';

interface OverviewTabProps {
  params: ExecutiveParams;
  // The other tabs on the bar; the "→" buttons open one of them.
  openTabs: ExecutiveTab[];
  onOpenTab(tab: ExecutiveTab): void;
}

const NO_SALES_POINTS: SalesPoint[] = [];
const NO_CASH_POINTS: CashPoint[] = [];

// The Tổng quan tab: the KPI row, then the charts and tables, with "Cần chú ý" and "Vừa phát sinh" on the
// right. A tile the user may not see is not rendered and its request is not sent.
export function OverviewTab({ params, openTabs, onOpenTab }: OverviewTabProps) {
  const access = useExecutiveAccess();
  const scope = useExecutiveScope();
  const { from, to, branchId } = params;
  // One granularity for both charts.
  const [granularity, setGranularity] = usePeriodGranularity(from, to);
  const dayDisabled = !isDayGranularityAllowed(from, to);
  const companyView = scope.canSwitch && scope.mode === 'company';
  const linkTitle = reportLinkTitle(scope.mode, scope.branches.length, scope.workingBranch?.code);
  const today = todayYmd();

  const salesSeries = useSalesSeries({ ...params, granularity }, { enabled: access.sales });
  const cashSeries = useCashSeries({ ...params, granularity }, { enabled: access.cash });
  const cashSummary = useCashSummary(params, { enabled: access.cash });
  const debtSummary = useDebtSummary({ branchId }, { enabled: access.debt });
  const branches = useBranchComparison({ from, to }, { enabled: companyView });
  const top = useSalesTop({ ...params, by: 'Customer', limit: 5 }, { enabled: access.sales });
  const attention = useAttention({ branchId });
  const recent = useRecentDocuments({ branchId, limit: 10 });

  const salesTab = openTabs.includes('sales');
  const openSales = (label: string) => (salesTab ? { label, onClick: () => onOpenTab('sales') } : undefined);

  const salesPoints = salesSeries.data?.points ?? NO_SALES_POINTS;
  const cashPoints = cashSeries.data?.points ?? NO_CASH_POINTS;
  const funds = cashSummary.data?.funds ?? [];
  const noCashMovement = isCashSeriesEmpty(cashPoints);

  return (
    <div className="space-y-4">
      <KpiRow params={params} />
      <div className="grid gap-4 xl:grid-cols-[minmax(0,1fr)_280px]">
        <div className="grid content-start gap-4 lg:grid-cols-2">
          {access.sales && (
            <TileCard
              title="Doanh thu – giá vốn – lãi gộp"
              className="lg:col-span-2"
              query={salesSeries}
              isEmpty={isSalesSeriesEmpty(salesPoints)}
              action={openSales('Chi tiết →')}
            >
              <ProfitChart
                points={salesPoints}
                canViewCost={salesPoints.some((p) => p.cost !== null)}
                granularity={granularity}
                onGranularityChange={setGranularity}
                dayDisabled={dayDisabled}
              />
            </TileCard>
          )}
          {access.cash && (
            <TileCard
              title="Dòng tiền thu – chi"
              query={cashSeries}
              // A period without movement still shows the fund balances.
              isEmpty={noCashMovement && funds.length === 0}
              action={{ label: 'Sổ quỹ →', to: executiveLinks.cashBook(), title: linkTitle }}
            >
              <div className="space-y-3">
                {noCashMovement ? (
                  <p className="text-sm text-muted-foreground">Chưa có dữ liệu.</p>
                ) : (
                  <CashFlowChart
                    points={cashPoints}
                    granularity={granularity}
                    // The profit chart carries the toggle; without it this chart does.
                    onGranularityChange={access.sales ? undefined : setGranularity}
                    dayDisabled={dayDisabled}
                  />
                )}
                {funds.length > 0 && <FundBalanceTable funds={funds} limit={5} showBranch={companyView} />}
              </div>
            </TileCard>
          )}
          {access.debt && (
            <TileCard
              title="Cơ cấu nợ phải thu"
              query={debtSummary}
              action={{ label: 'Nợ quá hạn →', to: executiveLinks.debtOverdue('Receivable', today), title: linkTitle }}
            >
              {debtSummary.data && (
                <DebtTermBar side={debtSummary.data.receivable} buckets={debtSummary.data.overdueBuckets} />
              )}
            </TileCard>
          )}
          {companyView && (
            <TileCard
              title="So sánh chi nhánh"
              query={branches}
              isEmpty={(branches.data ?? []).length === 0}
              action={openSales('Kinh doanh →')}
            >
              <BranchComparisonTable rows={branches.data ?? []} onSelectBranch={scope.selectBranch} />
            </TileCard>
          )}
          {access.sales && (
            <TileCard
              title="Top khách hàng theo doanh thu"
              query={top}
              isEmpty={(top.data ?? []).length === 0}
              action={openSales('Xem thêm →')}
            >
              <TopListCard
                items={(top.data ?? []).map((row) => ({
                  id: row.id,
                  name: row.name,
                  primary: row.revenue,
                  secondary: row.grossProfit != null ? `LG ${formatVndCompact(row.grossProfit)}` : undefined,
                }))}
              />
            </TileCard>
          )}
        </div>
        <div className="space-y-4">
          <TileCard title="Cần chú ý" query={attention}>
            <AttentionList attention={attention.data} today={today} from={from} to={to} linkTitle={linkTitle} />
          </TileCard>
          <TileCard
            title="Vừa phát sinh"
            query={recent}
            isEmpty={(recent.data ?? []).length === 0}
            emptyText="Hôm nay chưa có chứng từ."
          >
            <RecentFeed
              items={recent.data ?? []}
              workingBranchCode={scope.workingBranch?.code}
              showBranch={companyView}
            />
          </TileCard>
        </div>
      </div>
    </div>
  );
}
