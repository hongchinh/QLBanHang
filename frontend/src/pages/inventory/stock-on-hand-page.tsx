import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useStockOnHand } from '@/features/inventory-reports/hooks';
import type { StockOnHandParams } from '@/features/inventory-reports/types';
import { useProductGroups } from '@/features/products/hooks';
import { useWarehouses } from '@/features/warehouses/hooks';
import { selectableWarehouses } from '@/features/warehouses/utils';
import { formatMoneyForDisplay } from '@/pages/quotations/utils/money-input';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { getErrorMessage } from '@/lib/api-client';
import { useDebouncedValue } from '@/lib/use-debounced-value';
import { formatStockQuantity } from '@/lib/stock-quantity';
import { fromDateTimeLocalValue } from '@/lib/vn-datetime';

const ALL = 'all';

// Route permission: reports.inventory. Values come back only with inventory.view_cost (canViewCost).
export function StockOnHandPage() {
  const navigate = useNavigate();
  const { data: warehouses = [] } = useWarehouses();
  const { data: groups = [] } = useProductGroups();

  const [at, setAt] = useState('');
  const [warehouseId, setWarehouseId] = useState(ALL);
  const [productGroupId, setProductGroupId] = useState(ALL);
  const [search, setSearch] = useState('');
  const debouncedSearch = useDebouncedValue(search, 300);

  const params: StockOnHandParams = {
    at: at ? fromDateTimeLocalValue(at) : undefined,
    warehouseId: warehouseId === ALL ? undefined : warehouseId,
    productGroupId: productGroupId === ALL ? undefined : productGroupId,
    search: debouncedSearch.trim() || undefined,
  };
  const { data: report, isLoading, isError, error } = useStockOnHand(params);
  const canViewCost = report?.canViewCost ?? false;
  const rows = report?.rows ?? [];

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center gap-2">
        <h1 className="text-xl font-semibold">Tồn kho</h1>
        {canViewCost && report?.isProvisional && <Badge variant="warning">Giá trị tạm tính</Badge>}
      </div>

      <Card>
        <CardContent className="grid gap-4 pt-6 sm:grid-cols-2 lg:grid-cols-4">
          <div className="space-y-1">
            <Label htmlFor="soh-at">Thời điểm</Label>
            <Input id="soh-at" type="datetime-local" value={at} onChange={(e) => setAt(e.target.value)} />
            {!at && <p className="text-xs text-muted-foreground">Hiện tại</p>}
          </div>
          <div className="space-y-1">
            <Label htmlFor="soh-warehouse">Kho</Label>
            <Select value={warehouseId} onValueChange={setWarehouseId}>
              <SelectTrigger id="soh-warehouse">
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
            <Label htmlFor="soh-group">Nhóm hàng</Label>
            <Select value={productGroupId} onValueChange={setProductGroupId}>
              <SelectTrigger id="soh-group">
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
            <Label htmlFor="soh-search">Tìm kiếm</Label>
            <Input
              id="soh-search"
              placeholder="Mã hoặc tên hàng"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardContent className="overflow-x-auto pt-6">
          <table className="w-full min-w-[760px] text-sm">
            <thead>
              <tr className="border-b text-left text-muted-foreground">
                <th className="px-2 py-2">Mã hàng</th>
                <th className="px-2 py-2">Tên hàng</th>
                <th className="px-2 py-2">Nhóm</th>
                <th className="px-2 py-2">ĐVT</th>
                <th className="px-2 py-2">Kho</th>
                <th className="px-2 py-2 text-right">Số lượng</th>
                {canViewCost && <th className="px-2 py-2 text-right">Giá trị</th>}
              </tr>
            </thead>
            <tbody>
              {rows.map((r) => (
                <tr
                  key={`${r.productId}-${r.warehouseId}`}
                  className="cursor-pointer border-b hover:bg-muted/50"
                  onClick={() =>
                    navigate(`/inventory/stock-card?productId=${r.productId}&warehouseId=${r.warehouseId}`)
                  }
                >
                  <td className="px-2 py-2 font-medium">{r.productCode}</td>
                  <td className="px-2 py-2">{r.productName}</td>
                  <td className="px-2 py-2">{r.productGroupName}</td>
                  <td className="px-2 py-2">{r.unitName}</td>
                  <td className="px-2 py-2">{r.warehouseCode}</td>
                  <td className="px-2 py-2 text-right tabular-nums">{formatStockQuantity(r.quantity)}</td>
                  {canViewCost && (
                    <td className="px-2 py-2 text-right tabular-nums">
                      {formatMoneyForDisplay(r.value)}
                    </td>
                  )}
                </tr>
              ))}
              {isError && (
                <tr>
                  <td colSpan={canViewCost ? 7 : 6} className="px-2 py-6 text-center text-destructive">
                    {getErrorMessage(error)}
                  </td>
                </tr>
              )}
              {!isLoading && !isError && rows.length === 0 && (
                <tr>
                  <td colSpan={canViewCost ? 7 : 6} className="px-2 py-6 text-center text-muted-foreground">
                    Không có hàng tồn
                  </td>
                </tr>
              )}
            </tbody>
            {canViewCost && !isError && (
              <tfoot>
                <tr className="font-semibold">
                  <td colSpan={6} className="px-2 py-2 text-right">
                    Tổng giá trị
                  </td>
                  <td className="px-2 py-2 text-right tabular-nums">{formatMoneyForDisplay(report?.totalValue ?? 0)}</td>
                </tr>
              </tfoot>
            )}
          </table>
        </CardContent>
      </Card>
    </div>
  );
}
