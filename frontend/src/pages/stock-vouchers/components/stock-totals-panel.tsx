import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import type { StockDirection } from '@/features/stock-reasons/types';
import { formatMoneyForDisplay, parseMoneyInput } from '@/pages/quotations/utils/money-input';
import type { StockTotals } from '@/pages/stock-vouchers/utils/compute-stock-line';

const fmt = new Intl.NumberFormat('vi-VN');

export interface StockTotalsPanelProps {
  type: StockDirection;
  totals: StockTotals;
  freight: number;
  orderDiscount: number;
  paidAmount: number;
  onFreightChange: (value: number) => void;
  onOrderDiscountChange: (value: number) => void;
  onPaidAmountChange: (value: number) => void;
  // Stock-in only (section-02 #2).
  onApplyVatToAll?: (rate: number) => void;
  readOnly: boolean;
}

export function StockTotalsPanel({
  type,
  totals,
  freight,
  orderDiscount,
  paidAmount,
  onFreightChange,
  onOrderDiscountChange,
  onPaidAmountChange,
  onApplyVatToAll,
  readOnly,
}: StockTotalsPanelProps) {
  const [vatToAll, setVatToAll] = useState('');
  const vatToAllRate = Number(vatToAll);
  const canApplyVat = vatToAll.trim() !== '' && Number.isFinite(vatToAllRate) && vatToAllRate >= 0 && vatToAllRate <= 100;

  return (
    <Card>
      <CardHeader className="flex-row items-center border-b p-1 px-3 h-9 bg-blue-50">
        <CardTitle>Tổng cộng</CardTitle>
      </CardHeader>
      <CardContent className="space-y-1.5 px-3.5 pt-2.5 pb-3">
        <TotalRow label="Tiền hàng" testId="stock-totals-goods" value={totals.goodsAmount} />
        <TotalRow label="Tổng chiết khấu" testId="stock-totals-discount" value={totals.discountTotal} />
        <TotalRow label="Tổng VAT" testId="stock-totals-vat" value={totals.vatTotal} />

        <EditableRow id="stock-freight" label="Phí vận chuyển" value={freight} onChange={onFreightChange} disabled={readOnly} />
        <EditableRow
          id="stock-order-discount"
          label="CK cả đơn"
          value={orderDiscount}
          onChange={onOrderDiscountChange}
          disabled={readOnly}
        />

        <div className="flex items-baseline justify-between border-t-2 border-foreground/70 pt-2 mt-1">
          <span className="text-sm font-bold">Tổng thanh toán</span>
          <span className="text-base font-extrabold tabular-nums" data-testid="stock-totals-total">
            {fmt.format(totals.total)}
          </span>
        </div>

        <EditableRow
          id="stock-paid-amount"
          label="Số tiền thanh toán"
          value={paidAmount}
          onChange={onPaidAmountChange}
          disabled={readOnly}
        />

        {type === 'In' && onApplyVatToAll && !readOnly && (
          <div className="flex items-center justify-between gap-2 border-t pt-1.5 mt-0.5">
            <label htmlFor="stock-vat-to-all" className="text-sm text-muted-foreground">
              % VAT cho mọi dòng
            </label>
            <div className="flex items-center gap-1.5">
              <Input
                id="stock-vat-to-all"
                type="number"
                step="any"
                min={0}
                max={100}
                value={vatToAll}
                onChange={(e) => setVatToAll(e.target.value)}
                className="h-[26px] w-[64px] px-1.5 text-right tabular-nums"
              />
              <Button
                type="button"
                variant="outline"
                size="sm"
                className="h-[26px]"
                disabled={!canApplyVat}
                onClick={() => onApplyVatToAll(vatToAllRate)}
              >
                Áp dụng
              </Button>
            </div>
          </div>
        )}
      </CardContent>
    </Card>
  );
}

function TotalRow({ label, value, testId }: { label: string; value: number; testId: string }) {
  return (
    <div className="flex items-center justify-between">
      <span className="text-sm text-muted-foreground">{label}</span>
      <span className="text-sm font-semibold tabular-nums" data-testid={testId}>
        {fmt.format(value)}
      </span>
    </div>
  );
}

interface EditableRowProps {
  id: string;
  label: string;
  value: number;
  onChange: (value: number) => void;
  disabled: boolean;
}

// Same editing behaviour as the quotation totals panel: formatted when idle, raw while focused.
function EditableRow({ id, label, value, onChange, disabled }: EditableRowProps) {
  const [draft, setDraft] = useState<string | null>(null);
  return (
    <div className="flex items-center justify-between gap-2">
      <label htmlFor={id} className="text-sm text-muted-foreground">
        {label}
      </label>
      <Input
        id={id}
        type="text"
        inputMode="decimal"
        autoComplete="off"
        disabled={disabled}
        value={draft ?? formatMoneyForDisplay(value)}
        onFocus={(e) => {
          setDraft(String(value ?? ''));
          e.currentTarget.select();
        }}
        onBlur={() => setDraft(null)}
        onChange={(e) => {
          setDraft(e.target.value);
          onChange(parseMoneyInput(e.target.value) ?? 0);
        }}
        className="h-[26px] w-[120px] px-1.5 text-right tabular-nums"
      />
    </div>
  );
}
