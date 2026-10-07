import { useEffect, useState } from 'react';
import { Plus, Save, Trash2 } from 'lucide-react';
import { useOpeningStock, useSaveOpeningStock } from '@/features/opening-stock/hooks';
import type { OpeningStockGrid, SaveOpeningStockRequest } from '@/features/opening-stock/types';
import type { ProductSuggestion } from '@/features/products/types';
import { useWarehouses } from '@/features/warehouses/hooks';
import { selectableWarehouses } from '@/features/warehouses/utils';
import { ProductTypeaheadCell } from '@/pages/quotations/components/product-typeahead-cell';
import { formatMoneyForDisplay, parseMoneyInput } from '@/pages/quotations/utils/money-input';
import { NegativeStockDialog } from '@/pages/stock-vouchers/components/negative-stock-dialog';
import { toShortages, type NegativeStockShortage } from '@/pages/stock-vouchers/components/negative-stock';
import { Button } from '@/components/ui/button';
import { ButtonLoader } from '@/components/ui/button-loader';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { formatApiErrorDetails, getApiError, getErrorMessage } from '@/lib/api-client';
import { roundAwayFromZero } from '@/lib/round';
import { formatStockQuantity, parseQuantityInput, stockUnitName } from '@/lib/stock-quantity';
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

// Same parsers as the voucher grid: quantity is dot-decimal, amount accepts vi-VN grouping ("1.500.000").
// An empty cell counts as 0; anything unparsable is flagged inline and blocks the save.
const quantityOf = (r: Row) => parseQuantityInput(r.quantity) ?? 0;
const amountOf = (r: Row) => parseMoneyInput(r.amount) ?? 0;
const quantityError = (r: Row) =>
  r.quantity.trim() !== '' && parseQuantityInput(r.quantity) === undefined ? 'Số lượng không hợp lệ' : undefined;
const amountError = (r: Row) =>
  r.amount.trim() !== '' && parseMoneyInput(r.amount) === undefined ? 'Giá trị không hợp lệ' : undefined;

// Route permission: inventory.opening_stock. Opening values are part of that permission (D36),
// so no inventory.view_cost check here.
export function OpeningStockPage() {
  const { data: warehouses = [] } = useWarehouses();
  const [warehouseId, setWarehouseId] = useState<string>();
  const grid = useOpeningStock(warehouseId);
  const save = useSaveOpeningStock();

  const [openingDate, setOpeningDate] = useState(() => firstDayOfMonthYmd());
  const [rows, setRows] = useState<Row[]>([]);
  // The warehouse whose grid is in `rows`; saving is allowed only once it matches the selection,
  // so lines of one warehouse can never be saved under another.
  const [loadedFor, setLoadedFor] = useState<string>();
  const [lineErrors, setLineErrors] = useState<Record<string, string>>({});
  const [negative, setNegative] = useState<{ blocked: boolean; shortages: NegativeStockShortage[] } | null>(null);

  useEffect(() => {
    if (warehouseId) return;
    const firstActive = warehouses.find((w) => w.isActive);
    if (firstActive) setWarehouseId(firstActive.id);
  }, [warehouses, warehouseId]);

  useEffect(() => {
    if (!grid.data || grid.data.warehouseId !== warehouseId) return;
    setRows(toRows(grid.data));
    setOpeningDate(grid.data.openingDate ?? firstDayOfMonthYmd());
    setLineErrors({});
    setLoadedFor(warehouseId);
  }, [grid.data, warehouseId]);

  const loaded = !!warehouseId && loadedFor === warehouseId;

  const changeWarehouse = (id: string) => {
    if (id === warehouseId) return;
    setWarehouseId(id);
    setRows([]);
    setLineErrors({});
    setNegative(null);
  };

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
    if (!warehouseId || !loaded) return;
    if (filled.some((r) => quantityError(r) || amountError(r))) {
      toast({ variant: 'destructive', title: 'Số liệu không hợp lệ', description: 'Kiểm tra lại các ô được đánh dấu' });
      return;
    }
    const body: SaveOpeningStockRequest = {
      warehouseId,
      openingDate,
      acknowledgeNegativeStock,
      lines: filled.map((r) => ({ productId: r.productId, quantity: quantityOf(r), amount: amountOf(r) })),
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

  const statusMessage = !warehouseId
    ? 'Chọn kho để nhập tồn đầu kỳ'
    : grid.isError
      ? getErrorMessage(grid.error)
      : 'Đang tải…';

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h1 className="text-xl font-semibold">Tồn đầu kỳ</h1>
        <Button onClick={() => void submit(false)} disabled={!loaded || save.isPending}>
          {save.isPending ? <ButtonLoader /> : <Save className="h-4 w-4" />}
          Lưu
        </Button>
      </div>

      <Card>
        <CardContent className="grid gap-4 pt-6 sm:grid-cols-2 lg:grid-cols-4">
          <div className="space-y-1">
            <Label htmlFor="opening-warehouse">Kho</Label>
            <Select value={warehouseId ?? ''} onValueChange={changeWarehouse} disabled={save.isPending}>
              <SelectTrigger id="opening-warehouse">
                <SelectValue placeholder="Chọn kho" />
              </SelectTrigger>
              <SelectContent>
                {selectableWarehouses(warehouses, warehouseId).map((w) => (
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
              {!loaded && (
                <tr>
                  <td
                    colSpan={8}
                    className={`px-2 py-6 text-center ${grid.isError ? 'text-destructive' : 'text-muted-foreground'}`}
                  >
                    {statusMessage}
                  </td>
                </tr>
              )}
              {loaded &&
                rows.map((row, idx) => {
                  const quantity = quantityOf(row);
                  const unitValue = quantity > 0 ? amountOf(row) / quantity : null;
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
                        <NumberInput
                          id={`opening-quantity-${idx}`}
                          label="Số lượng"
                          text={row.quantity}
                          parse={parseQuantityInput}
                          format={formatStockQuantity}
                          error={quantityError(row)}
                          onChange={(quantity) => updateRow(row.key, { quantity })}
                        />
                      </td>
                      <td className="px-2 py-1">
                        <NumberInput
                          id={`opening-amount-${idx}`}
                          label="Giá trị"
                          text={row.amount}
                          parse={parseMoneyInput}
                          format={formatMoneyForDisplay}
                          error={amountError(row)}
                          onChange={(amount) => updateRow(row.key, { amount })}
                        />
                      </td>
                      <td className="px-2 py-2 text-right tabular-nums">
                        {unitValue == null ? '' : formatMoneyForDisplay(roundAwayFromZero(unitValue))}
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
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    disabled={!loaded}
                    onClick={() => setRows((c) => [...c, emptyRow()])}
                  >
                    <Plus className="h-4 w-4" />
                    Thêm dòng
                  </Button>
                </td>
                <td className="px-2 py-2 text-right tabular-nums">
                  {loaded && formatStockQuantity(filled.reduce((sum, r) => sum + quantityOf(r), 0))}
                </td>
                <td className="px-2 py-2 text-right tabular-nums">
                  {loaded && formatMoneyForDisplay(filled.reduce((sum, r) => sum + amountOf(r), 0))}
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

interface NumberInputProps {
  id: string;
  label: string;
  text: string;
  parse: (text: string) => number | undefined;
  format: (value: number) => string;
  error?: string;
  onChange: (text: string) => void;
}

// Formatted (vi-VN money / stock quantity) when idle; the raw text while focused or when unparsable.
function NumberInput({ id, label, text, parse, format, error, onChange }: NumberInputProps) {
  const [focused, setFocused] = useState(false);
  const parsed = parse(text);
  return (
    <>
      <Input
        id={id}
        inputMode="decimal"
        className="text-right tabular-nums"
        aria-label={label}
        aria-invalid={error ? true : undefined}
        value={focused || parsed === undefined ? text : format(parsed)}
        onFocus={() => setFocused(true)}
        onBlur={() => setFocused(false)}
        onChange={(e) => onChange(e.target.value)}
      />
      {error && <p className="mt-1 text-xs text-destructive">{error}</p>}
    </>
  );
}
