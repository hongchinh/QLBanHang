import { useState } from 'react';
import type { TileQuery } from '@/features/dashboard/components/tile-card';
import { TileCard } from '@/features/dashboard/components/tile-card';
import { formatNumber } from '@/features/dashboard/format';
import { useBranchComparison, useSalesSeries, useSalesSummary, useSalesTop } from '@/features/executive-dashboard/hooks';
import { codeName, formatAmount } from '@/features/executive-dashboard/labels';
import { isDayGranularityAllowed } from '@/features/executive-dashboard/period';
import { isSalesSeriesEmpty, usePeriodGranularity } from '@/features/executive-dashboard/series';
import type { ExecutiveParams, SalesPoint, SalesTopRow, SalesTopSort } from '@/features/executive-dashboard/types';
import { useExecutiveScope } from '@/features/executive-dashboard/use-executive-scope';
import { BranchComparisonTable } from '../components/branch-comparison-table';
import { DataTable } from '../components/data-table';
import type { DataColumn } from '../components/data-table';
import { ProfitChart } from '../components/profit-chart';
import { SegmentedToggle } from '../components/segmented-toggle';
import type { SegmentedOption } from '../components/segmented-toggle';

interface SalesTabProps {
  params: ExecutiveParams;
}

const NO_POINTS: SalesPoint[] = [];
const NO_ROWS: SalesTopRow[] = [];

// What the three lists are ranked by.
const SORTS: SegmentedOption<SalesTopSort>[] = [
  { key: 'Revenue', label: 'Doanh thu' },
  { key: 'GrossProfit', label: 'Lãi gộp' },
];

// The columns of a top list; the gross profit only for a user who may see the cost.
function topColumns(
  header: string,
  name: (row: SalesTopRow) => string,
  canViewCost: boolean,
): DataColumn<SalesTopRow>[] {
  const columns: DataColumn<SalesTopRow>[] = [
    { key: 'name', header, cell: name },
    { key: 'revenue', header: 'Doanh thu', align: 'right', cell: (row) => formatNumber(row.revenue) },
  ];
  if (canViewCost) {
    columns.push({ key: 'gross-profit', header: 'Lãi gộp', align: 'right', cell: (row) => formatAmount(row.grossProfit) });
  }
  return columns;
}

// The Kinh doanh tab: revenue, cost and gross profit over the period, the top customers, products and voucher
// creators, and the branches side by side in company scope.
export function SalesTab({ params }: SalesTabProps) {
  const scope = useExecutiveScope();
  const { from, to } = params;
  const [granularity, setGranularity] = usePeriodGranularity(from, to);
  const [sortChoice, setSortChoice] = useState<SalesTopSort>('Revenue');
  const companyView = scope.canSwitch && scope.mode === 'company';

  const summary = useSalesSummary(params);
  const canViewCost = summary.data?.canViewCost === true;
  // Without the cost there is no gross profit to sort by.
  const sort: SalesTopSort = canViewCost ? sortChoice : 'Revenue';

  const series = useSalesSeries({ ...params, granularity });
  const customers = useSalesTop({ ...params, by: 'Customer', sort, limit: 10 });
  const products = useSalesTop({ ...params, by: 'Product', sort, limit: 10 });
  const employees = useSalesTop({ ...params, by: 'Employee', sort, limit: 10 });
  const branches = useBranchComparison({ from, to }, { enabled: companyView });

  const points = series.data?.points ?? NO_POINTS;
  const partnerName = (row: SalesTopRow) => codeName(row.code, row.name);

  return (
    <div className="space-y-4">
      <TileCard title="Doanh thu – giá vốn – lãi gộp" query={series} isEmpty={isSalesSeriesEmpty(points)}>
        <ProfitChart
          points={points}
          canViewCost={canViewCost}
          granularity={granularity}
          onGranularityChange={setGranularity}
          dayDisabled={!isDayGranularityAllowed(from, to)}
          height={320}
        />
      </TileCard>
      {canViewCost && (
        <SegmentedToggle<SalesTopSort> label="Xếp hạng theo" options={SORTS} value={sort} onChange={setSortChoice} />
      )}
      <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
        <TopTile title="Top khách hàng" query={customers} columns={topColumns('Khách hàng', partnerName, canViewCost)} />
        <TopTile title="Top hàng hoá" query={products} columns={topColumns('Hàng hoá', partnerName, canViewCost)} />
        <TopTile
          title="Theo nhân viên lập phiếu"
          query={employees}
          columns={topColumns('Nhân viên', (row) => row.name, canViewCost)}
        />
        {companyView && (
          <TileCard title="So sánh chi nhánh" query={branches} isEmpty={(branches.data ?? []).length === 0}>
            <BranchComparisonTable rows={branches.data ?? []} onSelectBranch={scope.selectBranch} />
          </TileCard>
        )}
      </div>
    </div>
  );
}

function TopTile({
  title,
  query,
  columns,
}: {
  title: string;
  query: TileQuery & { data?: SalesTopRow[] };
  columns: DataColumn<SalesTopRow>[];
}) {
  const rows = query.data ?? NO_ROWS;
  return (
    <TileCard title={title} query={query} isEmpty={rows.length === 0}>
      <DataTable columns={columns} rows={rows} rowKey={(row) => row.id} />
    </TileCard>
  );
}
