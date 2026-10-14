import { StatTile } from '@/features/dashboard/components/stat-tile';
import { TileCard } from '@/features/dashboard/components/tile-card';
import { formatNumber, formatVndCompact } from '@/features/dashboard/format';
import { statTileState } from '@/features/dashboard/stat-tile-state';
import { useInventoryBreakdown, useInventorySummary } from '@/features/executive-dashboard/hooks';
import { codeName, formatAmount, withBranch } from '@/features/executive-dashboard/labels';
import { executiveLinks, reportLinkTitle } from '@/features/executive-dashboard/links';
import type {
  ExecutiveParams,
  NegativeStockRow,
  StockGroup,
  StockProductRow,
} from '@/features/executive-dashboard/types';
import { useExecutiveScope } from '@/features/executive-dashboard/use-executive-scope';
import { formatStockQuantity } from '@/lib/stock-quantity';
import { DataTable } from '../components/data-table';
import type { DataColumn } from '../components/data-table';

interface InventoryTabProps {
  params: ExecutiveParams;
}

// null without inventory.view_cost: the tile shows "—".
const compact = (value: number | null | undefined) => (value == null ? null : formatVndCompact(value));

const GROUP_COLUMNS: DataColumn<StockGroup>[] = [
  { key: 'group', header: 'Nhóm', cell: (row) => row.name ?? 'Chưa phân nhóm' },
  { key: 'count', header: 'Số mã', align: 'right', cell: (row) => formatNumber(row.productCount) },
  { key: 'value', header: 'Giá trị', align: 'right', cell: (row) => formatAmount(row.value) },
];

const TOP_COLUMNS: DataColumn<StockProductRow>[] = [
  { key: 'product', header: 'Hàng hoá', cell: (row) => codeName(row.code, row.name) },
  { key: 'unit', header: 'ĐVT', cell: (row) => row.unitName ?? '' },
  { key: 'quantity', header: 'Số lượng', align: 'right', cell: (row) => formatStockQuantity(row.quantity) },
  { key: 'value', header: 'Giá trị', align: 'right', cell: (row) => formatAmount(row.value) },
];

// The Kho tab: the stock value now and the stock moved in the period, then the value by warehouse and by
// product group, the products of the largest value and the negative stock.
export function InventoryTab({ params }: InventoryTabProps) {
  const scope = useExecutiveScope();
  const companyView = scope.canSwitch && scope.mode === 'company';
  const linkTitle = reportLinkTitle(scope.mode, scope.branches.length, scope.workingBranch?.code);
  // The branch code in front of a warehouse (company scope).
  const branchOf = (branchCode: string | null) => (companyView ? branchCode : null);

  const summary = useInventorySummary(params);
  const breakdown = useInventoryBreakdown({ branchId: params.branchId });
  const s = summary.data;
  const tileState = statTileState(summary);
  const byWarehouse = breakdown.data?.byWarehouse ?? [];
  const byProductGroup = breakdown.data?.byProductGroup ?? [];
  const topByValue = breakdown.data?.topByValue ?? [];
  const negative = breakdown.data?.negative ?? [];
  const stockOnHand = { label: 'Tồn kho', to: executiveLinks.stockOnHand(), title: linkTitle };

  const warehouseColumns: DataColumn<StockGroup>[] = [
    { key: 'warehouse', header: 'Kho', cell: (row) => withBranch(branchOf(row.branchCode), codeName(row.code, row.name)) },
    ...GROUP_COLUMNS.slice(1),
  ];
  const negativeColumns: DataColumn<NegativeStockRow>[] = [
    { key: 'product', header: 'Hàng hoá', cell: (row) => codeName(row.productCode, row.productName) },
    { key: 'warehouse', header: 'Kho', cell: (row) => withBranch(branchOf(row.branchCode), row.warehouseCode) },
    { key: 'unit', header: 'ĐVT', cell: (row) => row.unitName ?? '' },
    {
      key: 'quantity',
      header: 'Số lượng',
      align: 'right',
      cell: (row) => <span className="text-rose-700">{formatStockQuantity(row.quantity)}</span>,
    },
  ];

  return (
    <div className="space-y-4">
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-4">
        <StatTile
          label="Giá trị tồn kho"
          value={compact(s?.stockValue)}
          hint={s ? `${formatNumber(s.productCount)} mã · ${formatNumber(s.warehouseCount)} kho` : undefined}
          {...tileState}
        />
        <StatTile label="Nhập trong kỳ" value={compact(s?.inValue)} {...tileState} />
        <StatTile label="Giá vốn xuất trong kỳ" value={compact(s?.outCost)} {...tileState} />
        <StatTile
          label="Tồn âm"
          value={s ? String(s.negativeCount) : null}
          hint={s ? (s.negativeCount > 0 ? 'cặp mã × kho dưới 0' : 'Không có') : undefined}
          hintTone={s && s.negativeCount > 0 ? 'warn' : 'default'}
          {...tileState}
        />
      </div>
      <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
        <TileCard title="Giá trị tồn theo kho" query={breakdown} isEmpty={byWarehouse.length === 0} action={stockOnHand}>
          <DataTable columns={warehouseColumns} rows={byWarehouse} rowKey={(row, index) => row.id ?? `none-${index}`} />
        </TileCard>
        <TileCard title="Giá trị tồn theo nhóm hàng" query={breakdown} isEmpty={byProductGroup.length === 0}>
          <DataTable columns={GROUP_COLUMNS} rows={byProductGroup} rowKey={(row, index) => row.id ?? `none-${index}`} />
        </TileCard>
        {/* The products of the largest value need the cost. */}
        {breakdown.data?.canViewCost !== false && (
          <TileCard title="Hàng tồn giá trị lớn" query={breakdown} isEmpty={topByValue.length === 0}>
            <DataTable columns={TOP_COLUMNS} rows={topByValue} rowKey={(row) => row.productId} />
          </TileCard>
        )}
        <TileCard title="Mã hàng tồn âm" query={breakdown} isEmpty={negative.length === 0} action={stockOnHand}>
          <DataTable columns={negativeColumns} rows={negative} rowKey={(row) => `${row.productId}-${row.warehouseId}`} />
        </TileCard>
      </div>
    </div>
  );
}
