import { useMemo, useState } from 'react';
import { Calculator, X } from 'lucide-react';
import { useInventorySettings, useRecalcCost } from '@/features/inventory-settings/hooks';
import type { CostingPeriod } from '@/features/inventory-settings/types';
import { listPeriodStarts, RECALC_TIMEOUT_TOAST } from '@/features/inventory-settings/utils';
import { useWarehouses } from '@/features/warehouses/hooks';
import { selectableWarehouses } from '@/features/warehouses/utils';
import type { ProductSuggestion } from '@/features/products/types';
import { ProductTypeaheadCell } from '@/pages/quotations/components/product-typeahead-cell';
import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';
import { Card, CardContent } from '@/components/ui/card';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { formatApiErrorDetails, getErrorMessage, isRequestTimeout } from '@/lib/api-client';
import { toast } from '@/lib/use-toast';
import { useAuthStore } from '@/stores/auth-store';

const ALL_WAREHOUSES = 'all';
const PERIOD_COUNT = 24;

function periodLabel(period: CostingPeriod, start: string): string {
  const [y, m] = start.split('-');
  if (period === 'Year') return `Năm ${y}`;
  if (period === 'Quarter') return `Quý ${Math.floor((Number(m) - 1) / 3) + 1}/${y}`;
  return `Tháng ${m}/${y}`;
}

// Route permission: inventory.recalc_cost. The optional product picker also needs products.view
// (the typeahead calls /products/search), so it is hidden without it.
export function RecalcCostPage() {
  const canPickProduct = useAuthStore((s) => s.hasPermission('products.view'));
  const settings = useInventorySettings();
  const { data: warehouses = [] } = useWarehouses();
  const recalc = useRecalcCost();

  const costingPeriod = settings.data?.costingPeriod ?? 'Month';
  const periodStarts = useMemo(
    () => listPeriodStarts(costingPeriod, new Date(), PERIOD_COUNT),
    [costingPeriod],
  );

  const [fromPeriodStart, setFromPeriodStart] = useState<string>();
  const [warehouseId, setWarehouseId] = useState(ALL_WAREHOUSES);
  const [productQuery, setProductQuery] = useState('');
  const [product, setProduct] = useState<ProductSuggestion | null>(null);
  const [confirmOpen, setConfirmOpen] = useState(false);

  // Default to the current period; a costing-period change resets an option that no longer exists.
  const selectedPeriod =
    fromPeriodStart && periodStarts.includes(fromPeriodStart) ? fromPeriodStart : periodStarts[0];

  const onSelectProduct = (s: ProductSuggestion) => {
    setProduct(s);
    setProductQuery('');
  };

  const onConfirm = async () => {
    try {
      const result = await recalc.mutateAsync({
        fromPeriodStart: selectedPeriod,
        warehouseId: warehouseId === ALL_WAREHOUSES ? undefined : warehouseId,
        productId: product?.id,
      });
      toast({ variant: 'success', title: `Đã tính lại giá vốn cho ${result.scopeCount} phạm vi hàng hóa` });
      setConfirmOpen(false);
    } catch (err) {
      if (isRequestTimeout(err)) {
        toast(RECALC_TIMEOUT_TOAST);
        setConfirmOpen(false);
        return;
      }
      toast({ variant: 'destructive', title: 'Không thể tính lại giá vốn', description: formatApiErrorDetails(err) });
    }
  };

  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-2xl font-bold">Tính lại giá vốn</h1>
        <p className="text-sm text-muted-foreground">
          Tính lại giá vốn bình quân từ kỳ đã chọn đến nay cho chi nhánh làm việc.
        </p>
      </div>

      {settings.isError && (
        <div className="rounded-md border border-destructive/30 bg-destructive/10 p-3 text-sm text-destructive">
          {getErrorMessage(settings.error)}
        </div>
      )}

      <Card>
        <CardContent className="grid gap-4 p-4 md:grid-cols-3">
          <div className="space-y-2">
            <Label htmlFor="fromPeriodStart">Từ kỳ</Label>
            <Select value={selectedPeriod} onValueChange={setFromPeriodStart}>
              <SelectTrigger id="fromPeriodStart" aria-label="Từ kỳ">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {periodStarts.map((p) => (
                  <SelectItem key={p} value={p}>
                    {periodLabel(costingPeriod, p)}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="space-y-2">
            <Label htmlFor="warehouseId">Kho</Label>
            <Select value={warehouseId} onValueChange={setWarehouseId}>
              <SelectTrigger id="warehouseId" aria-label="Kho">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={ALL_WAREHOUSES}>Tất cả kho</SelectItem>
                {selectableWarehouses(warehouses, warehouseId).map((w) => (
                  <SelectItem key={w.id} value={w.id}>
                    {w.code} — {w.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          {canPickProduct && (
            <div className="space-y-2">
              <Label htmlFor="recalc-product">Hàng hóa</Label>
              {product ? (
                <div className="flex items-center justify-between gap-2 rounded-md border px-3 py-2 text-sm">
                  <span>
                    {product.code} — {product.name}
                  </span>
                  <Button
                    type="button"
                    variant="ghost"
                    size="icon"
                    aria-label="Bỏ chọn hàng hóa"
                    onClick={() => setProduct(null)}
                  >
                    <X className="h-4 w-4 text-red-600" />
                  </Button>
                </div>
              ) : (
                <ProductTypeaheadCell
                  inputId="recalc-product"
                  value={productQuery}
                  onChange={setProductQuery}
                  onSelect={onSelectProduct}
                  placeholder="Tất cả hàng hóa"
                />
              )}
            </div>
          )}

          <div className="flex justify-end md:col-span-3">
            {/* The period list depends on the costing period: wait for the settings. */}
            <Button
              type="button"
              onClick={() => setConfirmOpen(true)}
              disabled={!settings.data || !selectedPeriod || recalc.isPending}
            >
              <Calculator className="mr-2 h-4 w-4 text-blue-600" /> Tính lại giá vốn
            </Button>
          </div>
        </CardContent>
      </Card>

      <ConfirmDialog
        open={confirmOpen}
        onOpenChange={setConfirmOpen}
        title="Tính lại giá vốn?"
        description={`Giá vốn sẽ được tính lại từ ${periodLabel(costingPeriod, selectedPeriod ?? '')} đến nay.`}
        confirmLabel="Tính lại"
        loading={recalc.isPending}
        onConfirm={() => void onConfirm()}
      />
    </div>
  );
}
