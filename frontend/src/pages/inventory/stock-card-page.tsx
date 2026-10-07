import { useEffect, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { format } from 'date-fns';
import { useStockCard } from '@/features/inventory-reports/hooks';
import type { StockCardRow } from '@/features/inventory-reports/types';
import type { ProductSuggestion } from '@/features/products/types';
import { useWarehouses } from '@/features/warehouses/hooks';
import { ProductTypeaheadCell } from '@/pages/quotations/components/product-typeahead-cell';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { formatStockQuantity } from '@/lib/stock-quantity';
import { firstDayOfMonthYmd, todayYmd } from '@/lib/vn-datetime';

const ALL = 'all';
const money = new Intl.NumberFormat('en-US', { maximumFractionDigits: 0 });
const fmtMoney = (value?: number | null) => (value == null ? '' : money.format(value));

function voucherLink(row: StockCardRow): string | null {
  if (row.sourceType === 'StockIn') return `/stock-in/${row.sourceId}`;
  if (row.sourceType === 'StockOut') return `/stock-out/${row.sourceId}`;
  return null;
}

// Route permission: reports.inventory. Value fields come back only with inventory.view_cost (canViewCost).
export function StockCardPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const productId = searchParams.get('productId') ?? '';
  const warehouseId = searchParams.get('warehouseId') ?? ALL;
  const { data: warehouses = [] } = useWarehouses();

  const [productText, setProductText] = useState('');
  const [from, setFrom] = useState(() => firstDayOfMonthYmd());
  const [to, setTo] = useState(() => todayYmd());

  const { data: card, isLoading } = useStockCard({
    productId,
    warehouseId: warehouseId === ALL ? undefined : warehouseId,
    from,
    to,
  });

  // A product given through the URL shows its code once the card arrives.
  useEffect(() => {
    if (card && card.productId === productId && !productText) setProductText(card.productCode);
  }, [card, productId, productText]);

  const setParam = (key: string, value: string | null) => {
    const next = new URLSearchParams(searchParams);
    if (value) next.set(key, value);
    else next.delete(key);
    setSearchParams(next, { replace: true });
  };

  const selectProduct = (s: ProductSuggestion) => {
    setProductText(s.code);
    setParam('productId', s.id);
  };

  const canViewCost = card?.canViewCost ?? false;
  const showRunningValue = canViewCost && !card?.valuesAtScopeOnly;

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center gap-2">
        <h1 className="text-xl font-semibold">Thẻ kho</h1>
        {canViewCost && card?.isProvisional && <Badge variant="warning">Giá vốn tạm tính</Badge>}
      </div>

      <Card>
        <CardContent className="grid gap-4 pt-6 sm:grid-cols-2 lg:grid-cols-4">
          <div className="space-y-1">
            <Label htmlFor="stock-card-product">Hàng hóa</Label>
            <ProductTypeaheadCell
              inputId="stock-card-product"
              value={productText}
              onChange={setProductText}
              onSelect={selectProduct}
              placeholder="Mã hoặc tên hàng"
            />
          </div>
          <div className="space-y-1">
            <Label htmlFor="stock-card-warehouse">Kho</Label>
            <Select value={warehouseId} onValueChange={(v) => setParam('warehouseId', v === ALL ? null : v)}>
              <SelectTrigger id="stock-card-warehouse">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={ALL}>Tất cả kho</SelectItem>
                {warehouses.map((w) => (
                  <SelectItem key={w.id} value={w.id}>
                    {w.code} — {w.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-1">
            <Label htmlFor="stock-card-from">Từ ngày</Label>
            <Input id="stock-card-from" type="date" value={from} onChange={(e) => setFrom(e.target.value)} />
          </div>
          <div className="space-y-1">
            <Label htmlFor="stock-card-to">Đến ngày</Label>
            <Input id="stock-card-to" type="date" value={to} onChange={(e) => setTo(e.target.value)} />
          </div>
        </CardContent>
      </Card>

      {!productId ? (
        <p className="text-sm text-muted-foreground">Chọn hàng hóa để xem thẻ kho</p>
      ) : card ? (
        <>
          <Card>
            <CardContent className="pt-6">
              <div data-testid="stock-card-summary" className="grid gap-4 text-sm sm:grid-cols-4">
                {[
                  { label: 'Tồn đầu', qty: card.openingQty, value: showRunningValue ? card.openingValue : null },
                  { label: 'Nhập', qty: card.inQty, value: canViewCost ? card.inValue : null },
                  { label: 'Xuất', qty: card.outQty, value: canViewCost ? card.outValue : null },
                  { label: 'Tồn cuối', qty: card.closingQty, value: showRunningValue ? card.closingValue : null },
                ].map((s) => (
                  <div key={s.label}>
                    <div className="text-muted-foreground">{s.label}</div>
                    <div className="font-semibold tabular-nums">
                      {formatStockQuantity(s.qty)} {card.unitName}
                    </div>
                    {s.value != null && <div className="tabular-nums">{money.format(s.value)}</div>}
                  </div>
                ))}
              </div>
              {canViewCost && card.valuesAtScopeOnly && (
                <p className="mt-3 text-xs text-muted-foreground">
                  Giá trị tồn tính theo chi nhánh — bỏ lọc kho để xem
                </p>
              )}
            </CardContent>
          </Card>

          <Card>
            <CardContent className="overflow-x-auto pt-6">
              <table className="w-full min-w-[960px] text-sm">
                <thead>
                  <tr className="border-b text-left text-muted-foreground">
                    <th className="px-2 py-2">Ngày giờ</th>
                    <th className="px-2 py-2">Số CT</th>
                    <th className="px-2 py-2">Lý do</th>
                    <th className="px-2 py-2">Đối tượng</th>
                    <th className="px-2 py-2">Kho</th>
                    <th className="px-2 py-2 text-right">Nhập</th>
                    <th className="px-2 py-2 text-right">Xuất</th>
                    <th className="px-2 py-2 text-right">Tồn</th>
                    {canViewCost && <th className="px-2 py-2 text-right">Đơn giá</th>}
                    {canViewCost && <th className="px-2 py-2 text-right">Giá trị nhập</th>}
                    {canViewCost && <th className="px-2 py-2 text-right">Giá vốn xuất</th>}
                    {showRunningValue && <th className="px-2 py-2 text-right">Giá trị tồn</th>}
                  </tr>
                </thead>
                <tbody>
                  {card.rows.map((row, idx) => {
                    const href = voucherLink(row);
                    return (
                      <tr key={`${row.sourceId}-${idx}`} className="border-b">
                        <td className="whitespace-nowrap px-2 py-2">{format(new Date(row.postedAt), 'dd/MM/yyyy HH:mm')}</td>
                        <td className="px-2 py-2">
                          {href ? (
                            <Link to={href} className="text-primary hover:underline">
                              {row.sourceCode}
                            </Link>
                          ) : (
                            row.reasonName
                          )}
                        </td>
                        <td className="px-2 py-2">{row.reasonName}</td>
                        <td className="px-2 py-2">{row.partnerName}</td>
                        <td className="px-2 py-2">{row.warehouseCode}</td>
                        <td className="px-2 py-2 text-right tabular-nums">{row.qtyIn ? formatStockQuantity(row.qtyIn) : ''}</td>
                        <td className="px-2 py-2 text-right tabular-nums">{row.qtyOut ? formatStockQuantity(row.qtyOut) : ''}</td>
                        <td className="px-2 py-2 text-right tabular-nums">{formatStockQuantity(row.runningQty)}</td>
                        {canViewCost && <td className="px-2 py-2 text-right tabular-nums">{fmtMoney(row.unitCost)}</td>}
                        {canViewCost && <td className="px-2 py-2 text-right tabular-nums">{fmtMoney(row.inValue)}</td>}
                        {canViewCost && <td className="px-2 py-2 text-right tabular-nums">{fmtMoney(row.costAmount)}</td>}
                        {showRunningValue && (
                          <td className="px-2 py-2 text-right tabular-nums">{fmtMoney(row.runningValue)}</td>
                        )}
                      </tr>
                    );
                  })}
                  {card.rows.length === 0 && (
                    <tr>
                      <td colSpan={12} className="px-2 py-6 text-center text-muted-foreground">
                        Không có phát sinh trong khoảng thời gian này
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </CardContent>
          </Card>
        </>
      ) : (
        isLoading && <p className="text-sm text-muted-foreground">Đang tải…</p>
      )}
    </div>
  );
}
