import { useState, type ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { useCanAccessRoute } from '@/hooks/useCanAccessRoute';
import type { CashVoucherType } from '@/features/cash-common/types';
import type { StockDirection } from '@/features/stock-reasons/types';
import { formatMoneyForDisplay, parseMoneyInput } from '@/lib/money-input';

export interface AutoCashVoucherLink {
  id: string;
  code: string;
  type: CashVoucherType;
}

const AUTO_VOUCHER_LINKS: Record<CashVoucherType, { label: string; basePath: string }> = {
  Receipt: { label: 'Phiếu thu tự động', basePath: '/receipts' },
  Payment: { label: 'Phiếu chi tự động', basePath: '/payments' },
};

export interface StockVoucherPaymentTabProps {
  type: StockDirection;
  freight: number;
  orderDiscount: number;
  paidAmount: number;
  // The chosen payment method has no fund: nothing can be paid.
  paidAmountDisabled?: boolean;
  // Server message for the paid amount.
  paidAmountError?: string;
  onFreightChange: (value: number) => void;
  onOrderDiscountChange: (value: number) => void;
  onPaidAmountChange: (value: number) => void;
  // Stock-in only (section-02 #2).
  onApplyVatToAll?: (rate: number) => void;
  // The automatic receipt/payment of the paid amount (P20).
  autoVoucher?: AutoCashVoucherLink;
  readOnly: boolean;
  // The fund and due-date fields the page renders.
  children?: ReactNode;
}

export function StockVoucherPaymentTab({
  type,
  freight,
  orderDiscount,
  paidAmount,
  paidAmountDisabled = false,
  paidAmountError,
  onFreightChange,
  onOrderDiscountChange,
  onPaidAmountChange,
  onApplyVatToAll,
  autoVoucher,
  readOnly,
  children,
}: StockVoucherPaymentTabProps) {
  const canAccess = useCanAccessRoute();
  const [vatToAll, setVatToAll] = useState('');
  const vatToAllRate = Number(vatToAll);
  const autoVoucherText = autoVoucher
    ? `${AUTO_VOUCHER_LINKS[autoVoucher.type].label}: ${autoVoucher.code}`
    : '';
  const canApplyVat = vatToAll.trim() !== '' && Number.isFinite(vatToAllRate) && vatToAllRate >= 0 && vatToAllRate <= 100;

  return (
    <div className="grid gap-x-8 gap-y-1.5 p-3 md:grid-cols-2">
      <div className="space-y-1.5">
        <EditableRow id="stock-freight" label="Phí vận chuyển" value={freight} onChange={onFreightChange} disabled={readOnly} />
        <EditableRow
          id="stock-order-discount"
          label="CK cả đơn"
          value={orderDiscount}
          onChange={onOrderDiscountChange}
          disabled={readOnly}
        />
        <EditableRow
          id="stock-paid-amount"
          label="Số tiền thanh toán"
          value={paidAmount}
          onChange={onPaidAmountChange}
          disabled={readOnly || paidAmountDisabled}
        />
        {paidAmountError && <p className="text-right text-xs text-destructive">{paidAmountError}</p>}

        {autoVoucher && (
          <div className="text-right text-sm">
            {canAccess(`${AUTO_VOUCHER_LINKS[autoVoucher.type].basePath}/${autoVoucher.id}`) ? (
              <Link
                to={`${AUTO_VOUCHER_LINKS[autoVoucher.type].basePath}/${autoVoucher.id}`}
                className="text-blue-600 hover:underline"
              >
                {autoVoucherText}
              </Link>
            ) : (
              <span>{autoVoucherText}</span>
            )}
          </div>
        )}

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
      </div>
      <div className="space-y-1.5">{children}</div>
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
        value={!disabled && draft !== null ? draft : formatMoneyForDisplay(value)}
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
