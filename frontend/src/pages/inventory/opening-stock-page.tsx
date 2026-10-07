import { useEffect, useState } from 'react';
import { Plus, Save, Trash2 } from 'lucide-react';
import { useOpeningStock, useSaveOpeningStock } from '@/features/opening-stock/hooks';
import type { OpeningStockGrid, SaveOpeningStockRequest } from '@/features/opening-stock/types';
import type { ProductSuggestion } from '@/features/products/types';
import { useWarehouses } from '@/features/warehouses/hooks';
import { ProductTypeaheadCell } from '@/pages/quotations/components/product-typeahead-cell';
import {
  NegativeStockDialog,
  toShortages,
  type NegativeStockShortage,
} from '@/pages/stock-vouchers/components/negative-stock-dialog';
import { Button } from '@/components/ui/button';
import { ButtonLoader } from '@/components/ui/button-loader';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { formatApiErrorDetails, getApiError } from '@/lib/api-client';
import { formatStockQuantity, stockUnitName } from '@/lib/stock-quantity';
import { toast } from '@/lib/use-toast';
import { firstDayOfMonthYmd } from '@/lib/vn-datetime';

interface Row {
  key: string;
  productId: string;
  code: string;
  productName: string;
  unitName: string;
  quantity: string;
  amount: string;
}

const money = new Intl.NumberFormat('en-US', { maximumFractionDigits: 0 });
let rowSeq = 0;
const nextKey = () => `row-${++rowSeq}`;

const emptyRow = (): Row => ({
  key: nextKey(),
  productId: '',
  code: '',
  productName: '',
  unitName: '',
  quantity: '',
  amount: '',
});

function toRows(grid: OpeningStockGrid): Row[] {
  return grid.lines.map((l) => ({
    key: nextKey(),
    productId: l.productId,
    code: l.productCode,
    productName: l.productName,
    unitName: l.unitName,
    quantity: String(l.quantity),
    amount: String(l.amount),
  }));
}

const toNumber = (value: string) => (value.trim() === '' ? 0 : Number(value));

// Route permission: inventory.opening_stock. Opening values are part of that permission (D36),
// so no inventory.view_cost check here.
export function OpeningStockPage() {
  const { data: warehouses = [] } = useWarehouses();
  const [warehouseId, setWarehouseId] = useState<string>();
  const grid = useOpeningStock(warehouseId);
  const save = useSaveOpeningStock();

  const [openingDate, setOpeningDate] = useState(() => firstDayOfMonthYmd());
  const [rows, setRows] = useState<Row[]>([]);
  const [lineErrors, setLineErrors] = useState<Record<string, string>>({});
  const [negative, setNegative] = useState<{ blocked: boolean; shortages: NegativeStockShortage[] } | null>(null);

  useEffect(() => {
    if (!warehouseId && warehouses.length > 0) setWarehouseId(warehouses[0].id);
  }, [warehouses, warehouseId]);

  useEffect(() => {
    if (!grid.data) return;
    setRows(toRows(grid.data));
    setOpeningDate(grid.data.openingDate ?? firstDayOfMonthYmd());
    setLineErrors({});
  }, [grid.data]);

  const updateRow = (key: string, patch: Partial<Row>) =>
    setRows((current) => current.map((r) => (r.key === key ? { ...r, ...patch } : r)));

  const selectProduct = (row: Row, s: ProductSuggestion) => {
    if (!s.trackInventory) {
      toast({ variant: 'destructive', title: 'Hàng không theo dõi tồn kho', description: s.code });
      return;
    }
    if (rows.some((r) => r.key !== row.key && r.productId === s.id)) {
      toast({ variant: 'destructive', title: 'Hàng đã có trong danh sách', description: s.code });
      return;
    }
    updateRow(row.key, {
      productId: s.id,
      code: s.code,
      productName: s.name,
      unitName: stockUnitName(s.pricingMode, s.unitName),
    });
  };

  const filled = rows.filter((r) => r.productId);

  const submit = async (acknowledgeNegativeStock: boolean) => {
    if (!warehouseId) return;
    const body: SaveOpeningStockRequest = {
      warehouseId,
      openingDate,
      acknowledgeNegativeStock,
      lines: filled.map((r) => ({ productId: r.productId, quantity: toNumber(r.quantity), amount: toNumber(r.amount) })),
    };
    try {
      await save.mutateAsync(body);
      setNegative(null);
      setLineErrors({});
      toast({ variant: 'success', title: 'Đã lưu tồn đầu kỳ' });
    } catch (err) {
      const apiError = getApiError(err);
      if (apiError?.code === 'NEGATIVE_STOCK_WARNING' || apiError?.code === 'NEGATIVE_STOCK_BLOCKED') {
        setNegative({ blocked: apiError.code === 'NEGATIVE_STOCK_BLOCKED', shortages: toShortages(apiError.details) });
        return;
      }
      setNegative(null);
      // details keyed lines[i].… point at the i-th saved line, i.e. the i-th filled row.
      const errors: Record<string, string> = {};
      Object.entries(apiError?.details ?? {}).forEach(([key, messages]) => {
        const match = /^lines\[(\d+)\]/.exec(key);
        const row = match ? filled[Number(match[1])] : undefined;
        if (row && !errors[row.key]) errors[row.key] = messages[0];
      });
      setLineErrors(errors);
      toast({ variant: 'destructive', title: 'Không thể lưu', description: formatApiErrorDetails(err) });
    }
  };

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h1 className="text-xl font-semibold">Tồn đầu kỳ</h1>
        <Button onClick={() => void submit(false)} disabled={!warehouseId || save.isPending}>
          {save.isPending ? <ButtonLoader /> : <Save className="h-4 w-4" />}
          Lưu
        </Button>
      </div>

      <Card>
        <CardContent className="grid gap-4 pt-6 sm:grid-cols-2 lg:grid-cols-4">
          <div className="space-y-1">
            <Label htmlFor="opening-warehouse">Kho</Label>
            <Select value={warehouseId ?? ''} onValueChange={setWarehouseId}>
              <SelectTrigger id="opening-warehouse">
                <SelectValue placeholder="Chọn kho" />
              </SelectTrigger>
              <SelectContent>
                {warehouses.map((w) => (
                  <SelectItem key={w.id} value={w.id}>
                    {w.code} — {w.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-1">
            <Label htmlFor="opening-date">Ngày tồn đầu</Label>
            <Input
              id="opening-date"
              type="date"
              value={openingDate}
              onChange={(e) => setOpeningDate(e.target.value)}
            />
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardContent className="overflow-x-auto pt-6">
          <table className="w-full min-w-[860px] text-sm">
            <thead>
              <tr className="border-b text-left text-muted-foreground">
                <th className="w-12 px-2 py-2">STT</th>
                <th className="w-48 px-2 py-2">Mã hàng</th>
                <th className="px-2 py-2">Tên hàng</th>
                <th className="w-20 px-2 py-2">ĐVT</th>
                <th className="w-36 px-2 py-2 text-right">Số lượng</th>
                <th className="w-40 px-2 py-2 text-right">Giá trị</th>
                <th className="w-36 px-2 py-2 text-right">Đơn giá</th>
                <th className="w-12 px-2 py-2" />
              </tr>
            </thead>
            <tbody>
              {rows.map((row, idx) => {
                const quantity = toNumber(row.quantity);
                const unitValue = quantity > 0 ? toNumber(row.amount) / quantity : null;
                return (
                  <tr key={row.key} className="border-b align-top">
                    <td className="px-2 py-2">{idx + 1}</td>
                    <td className="px-2 py-1">
                      <ProductTypeaheadCell
                        variant="cell"
                        inputId={`opening-product-code-${idx}`}
                        value={row.code}
                        onChange={(code) => updateRow(row.key, { code })}
                        onSelect={(s) => selectProduct(row, s)}
                        placeholder="Mã hàng"
                      />
                      {lineErrors[row.key] && (
                        <p className="mt-1 text-xs text-destructive">{lineErrors[row.key]}</p>
                      )}
                    </td>
                    <td className="px-2 py-2">{row.productName}</td>
                    <td className="px-2 py-2">{row.unitName}</td>
                    <td className="px-2 py-1">
                      <Input
                        id={`opening-quantity-${idx}`}
                        inputMode="decimal"
                        className="text-right"
                        aria-label="Số lượng"
                        value={row.quantity}
                        onChange={(e) => updateRow(row.key, { quantity: e.target.value })}
                      />
                    </td>
                    <td className="px-2 py-1">
                      <Input
                        id={`opening-amount-${idx}`}
                        inputMode="decimal"
                        className="text-right"
                        aria-label="Giá trị"
                        value={row.amount}
                        onChange={(e) => updateRow(row.key, { amount: e.target.value })}
                      />
                    </td>
                    <td className="px-2 py-2 text-right tabular-nums">
                      {unitValue == null ? '' : money.format(unitValue)}
                    </td>
                    <td className="px-2 py-1">
                      <Button
                        type="button"
                        variant="ghost"
                        size="icon"
                        aria-label="Xóa dòng"
                        onClick={() => setRows((current) => current.filter((r) => r.key !== row.key))}
                      >
                        <Trash2 className="h-4 w-4" />
                      </Button>
                    </td>
                  </tr>
                );
              })}
            </tbody>
            <tfoot>
              <tr>
                <td colSpan={4} className="px-2 py-2">
                  <Button type="button" variant="outline" size="sm" onClick={() => setRows((c) => [...c, emptyRow()])}>
                    <Plus className="h-4 w-4" />
                    Thêm dòng
                  </Button>
                </td>
                <td className="px-2 py-2 text-right tabular-nums">
                  {formatStockQuantity(filled.reduce((sum, r) => sum + toNumber(r.quantity), 0))}
                </td>
                <td className="px-2 py-2 text-right tabular-nums">
                  {money.format(filled.reduce((sum, r) => sum + toNumber(r.amount), 0))}
                </td>
                <td colSpan={2} />
              </tr>
            </tfoot>
          </table>
        </CardContent>
      </Card>

      <NegativeStockDialog
        open={negative !== null}
        blocked={negative?.blocked ?? false}
        shortages={negative?.shortages ?? []}
        onConfirm={() => void submit(true)}
        onClose={() => setNegative(null)}
      />
    </div>
  );
}
