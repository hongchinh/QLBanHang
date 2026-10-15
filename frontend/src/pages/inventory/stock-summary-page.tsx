import { useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { useStockSummary } from '@/features/inventory-reports/hooks';
import type { StockSummaryParams, StockSummaryRow } from '@/features/inventory-reports/types';
import { useProductGroups } from '@/features/products/hooks';
import { tt133FileBase, type Tt133ExportForm } from '@/features/tt133-export/types';
import { useWarehouses } from '@/features/warehouses/hooks';
import { selectableWarehouses } from '@/features/warehouses/utils';
import { useFromHere } from '@/components/layout/back-link';
import { PeriodPresetSelect } from '@/components/period-preset-select/period-preset-select';
import { ReportScopeSelect } from '@/components/report-scope-select/report-scope-select';
import { useReportScope } from '@/components/report-scope-select/use-report-scope';
import { Tt133ExportButton } from '@/components/tt133-export/tt133-export-button';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { getErrorMessage } from '@/lib/api-client';
import { formatMoneyForDisplay } from '@/lib/money-input';
import { formatStockQuantity } from '@/lib/stock-quantity';
import { useDebouncedValue } from '@/lib/use-debounced-value';
import { useSearchParamString } from '@/lib/use-search-param-state';
import { firstDayOfMonthYmd, todayYmd } from '@/lib/vn-datetime';

const ALL = 'all';
const NUMERIC_CELL = 'px-2 py-2 text-right tabular-nums';

const GROUPS = ['Tồn đầu kỳ', 'Nhập trong kỳ', 'Xuất trong kỳ', 'Tồn cuối kỳ'] as const;

function quantityValuePairs(row: StockSummaryRow) {
  return [
    [row.openingQty, row.openingValue],
    [row.inQty, row.inValue],
    [row.outQty, row.outValue],
    [row.closingQty, row.closingValue],
  ] as const;
}

// Tổng hợp nhập – xuất – tồn. Route permission: reports.inventory. Values come back only with
// inventory.view_cost (canViewCost) and not for a warehouse filter under Branch costing (valuesAtScopeOnly).
export function StockSummaryPage() {
  const navigate = useNavigate();
  const fromHere = useFromHere();
  const [, setSearchParams] = useSearchParams();
  const { data: warehouses = [] } = useWarehouses();
  const { data: groups = [] } = useProductGroups();

  const [from, setFrom] = useSearchParamString('from', firstDayOfMonthYmd());
  const [to, setTo] = useSearchParamString('to', todayYmd());
  const [scope, setScope] = useReportScope();
  const [warehouseState, setWarehouseId] = useState(ALL);
  const [productGroupId, setProductGroupId] = useState(ALL);
  const [search, setSearch] = useState('');
  const debouncedSearch = useDebouncedValue(search, 300);

  // The warehouse filter belongs to the branch scope; a company-wide read never sends it.
  const companyScope = scope === 'Company';
  const warehouseId = companyScope ? ALL : warehouseState;

  // yyyy-MM-dd strings compare chronologically.
  const rangeError = !from || !to ? 'Chọn đủ Từ ngày và Đến ngày' : from > to ? 'Từ ngày không được sau Đến ngày' : null;

  const params: StockSummaryParams = {
    from,
    to,
    scope,
    warehouseId: warehouseId === ALL ? undefined : warehouseId,
    productGroupId: productGroupId === ALL ? undefined : productGroupId,
    search: debouncedSearch.trim() || undefined,
  };
  const { data: report, isLoading, isError, error } = useStockSummary(params, { enabled: !rangeError });
  const rows = report?.rows ?? [];
  const showValues = !!report && report.canViewCost && !report.valuesAtScopeOnly;
  const columnCount = showValues ? 11 : 7;

  const exportForm: Tt133ExportForm = {
    code: 'S07-DNN',
    report: 'stock-summary',
    params: { ...params },
    fileBase: tt133FileBase('S07-DNN', from, to),
  };

  const openCard = (row: StockSummaryRow) => {
    const query = new URLSearchParams({ productId: row.productId });
    if (warehouseId !== ALL) query.set('warehouseId', warehouseId);
    navigate(`/inventory/stock-card?${query.toString()}`, { state: fromHere });
  };

  const totals = [report?.totalOpeningValue, report?.totalInValue, report?.totalOutValue, report?.totalClosingValue];

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div className="flex flex-wrap items-center gap-2">
          <h1 className="text-xl font-semibold">Tổng hợp nhập – xuất – tồn</h1>
          {showValues && report?.isProvisional && <Badge variant="warning">Giá trị tạm tính</Badge>}
        </div>
        <Tt133ExportButton forms={[exportForm]} disabled={!!rangeError} />
      </div>

      <Card>
        <CardContent className="grid gap-4 pt-6 sm:grid-cols-2 lg:grid-cols-4">
          <PeriodPresetSelect
            id="ss-period"
            onChange={(range) =>
              // One update for both keys: two setSearchParams calls in a row would keep only the last.
              setSearchParams(
                (prev) => {
                  const next = new URLSearchParams(prev);
                  next.set('from', range.from);
                  next.set('to', range.to);
                  return next;
                },
                { replace: true },
              )
            }
          />
          <div className="space-y-1">
            <Label htmlFor="ss-from">Từ ngày</Label>
            <Input id="ss-from" type="date" value={from} onChange={(e) => setFrom(e.target.value)} />
          </div>
          <div className="space-y-1">
            <Label htmlFor="ss-to">Đến ngày</Label>
            <Input id="ss-to" type="date" value={to} onChange={(e) => setTo(e.target.value)} />
          </div>
          <ReportScopeSelect
            id="ss-scope"
            value={scope}
            onChange={(next) => {
              setScope(next);
              if (next === 'Company') setWarehouseId(ALL);
            }}
          />
          <div className="space-y-1">
            <Label htmlFor="ss-warehouse">Kho</Label>
            <Select value={warehouseId} onValueChange={setWarehouseId} disabled={companyScope}>
              <SelectTrigger id="ss-warehouse">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={ALL}>Tất cả kho</SelectItem>
                {selectableWarehouses(warehouses, warehouseId).map((w) => (
                  <SelectItem key={w.id} value={w.id}>
                    {w.code} — {w.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-1">
            <Label htmlFor="ss-group">Nhóm hàng</Label>
            <Select value={productGroupId} onValueChange={setProductGroupId}>
              <SelectTrigger id="ss-group">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={ALL}>Tất cả nhóm</SelectItem>
                {groups.map((g) => (
                  <SelectItem key={g.id} value={g.id}>
                    {g.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-1">
            <Label htmlFor="ss-search">Tìm kiếm</Label>
            <Input
              id="ss-search"
              placeholder="Mã hoặc tên hàng"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
          </div>
        </CardContent>
      </Card>

      {report?.valuesAtScopeOnly && (
        <p className="text-sm text-muted-foreground">Giá trị tính theo chi nhánh. Bỏ lọc kho để xem giá trị.</p>
      )}

      {rangeError ? (
        <p className="text-sm text-destructive">{rangeError}</p>
      ) : (
        <Card>
          <CardContent className="overflow-x-auto pt-6">
            <table className="w-full min-w-[960px] text-sm">
              <thead>
                <tr className="border-b text-left text-muted-foreground">
                  <th rowSpan={2} className="px-2 py-2">
                    Mã hàng
                  </th>
                  <th rowSpan={2} className="px-2 py-2">
                    Tên hàng
                  </th>
                  <th rowSpan={2} className="px-2 py-2">
                    ĐVT
                  </th>
                  {GROUPS.map((g) => (
                    <th key={g} colSpan={showValues ? 2 : 1} className="px-2 py-2 text-center">
                      {g}
                    </th>
                  ))}
                </tr>
                <tr className="border-b text-muted-foreground">
                  {GROUPS.map((g) => (
                    <GroupSubHeaders key={g} showValues={showValues} />
                  ))}
                </tr>
              </thead>
              <tbody>
                {rows.map((r) => (
                  <tr key={r.productId} className="cursor-pointer border-b hover:bg-muted/50" onClick={() => openCard(r)}>
                    <td className="px-2 py-2 font-medium">{r.productCode}</td>
                    <td className="px-2 py-2">{r.productName}</td>
                    <td className="px-2 py-2">{r.unitName}</td>
                    {quantityValuePairs(r).flatMap(([qty, value], i) => [
                      <td key={`q${i}`} className={NUMERIC_CELL}>
                        {formatStockQuantity(qty)}
                      </td>,
                      ...(showValues
                        ? [
                            <td key={`v${i}`} className={NUMERIC_CELL}>
                              {formatMoneyForDisplay(value)}
                            </td>,
                          ]
                        : []),
                    ])}
                  </tr>
                ))}
                {isError && (
                  <tr>
                    <td colSpan={columnCount} className="px-2 py-6 text-center text-destructive">
                      {getErrorMessage(error)}
                    </td>
                  </tr>
                )}
                {isLoading && (
                  <tr>
                    <td colSpan={columnCount} className="px-2 py-6 text-center text-muted-foreground">
                      Đang tải…
                    </td>
                  </tr>
                )}
                {report && !isError && rows.length === 0 && (
                  <tr>
                    <td colSpan={columnCount} className="px-2 py-6 text-center text-muted-foreground">
                      Không có phát sinh trong kỳ
                    </td>
                  </tr>
                )}
              </tbody>
              {showValues && !isError && (
                <tfoot>
                  <tr className="font-semibold">
                    <td colSpan={3} className="px-2 py-2">
                      Cộng
                    </td>
                    {totals.flatMap((total, i) => [
                      <td key={`q${i}`} className="px-2 py-2" />,
                      <td key={`v${i}`} className={NUMERIC_CELL}>
                        {formatMoneyForDisplay(total ?? 0)}
                      </td>,
                    ])}
                  </tr>
                </tfoot>
              )}
            </table>
          </CardContent>
        </Card>
      )}
    </div>
  );
}

function GroupSubHeaders({ showValues }: { showValues: boolean }) {
  return (
    <>
      <th className="px-2 py-2 text-right">SL</th>
      {showValues && <th className="px-2 py-2 text-right">Thành tiền</th>}
    </>
  );
}
