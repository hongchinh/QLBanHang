import type { UseFormReturn } from 'react-hook-form';
import { PartnerAutocomplete } from '@/components/partner-autocomplete/partner-autocomplete';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import type { StockVoucherFormParsed, StockVoucherFormValues } from '@/features/stock-vouchers/schema';
import type { StockVoucherDraftPartner } from '@/features/stock-vouchers/use-stock-voucher-draft';
import type { PartnerSearchItem, StockDirection } from '@/features/stock-vouchers/types';
import type { PaymentMethod } from '@/features/payment-methods/types';
import type { StockReason } from '@/features/stock-reasons/types';
import type { Warehouse } from '@/features/warehouses/types';
import type { StockVoucherLabels } from '../labels';

export const SELECT_CLASS =
  'h-7 w-full rounded-md border border-input bg-background px-2 text-sm disabled:cursor-not-allowed disabled:opacity-50';

export function FieldError({ message }: { message?: string }) {
  if (!message) return null;
  return <p className="field-message field-message-code text-destructive">{message}</p>;
}

export interface StockVoucherMainFieldsProps {
  form: UseFormReturn<StockVoucherFormValues, unknown, StockVoucherFormParsed>;
  type: StockDirection;
  labels: StockVoucherLabels;
  readOnly: boolean;
  // Header list (active + current).
  warehouses: Warehouse[];
  reasons: StockReason[];
  paymentMethods: PaymentMethod[];
  warehouseId: string;
  reasonId: string;
  paymentMethodId?: string;
  selectedReason?: StockReason;
  selectedPartner: StockVoucherDraftPartner | null;
  onWarehouseChange: (id: string) => void;
  onReasonChange: (id: string) => void;
  onSelectPartner: (p: PartnerSearchItem | null) => void;
  // Enter on the last field.
  onEnterGrid: () => void;
}

// Enter order (brainstorm section 1); disabled fields are skipped.
const MAIN_FIELD_IDS = [
  'stock-voucher-at',
  'stock-reason',
  'stock-warehouse',
  'stock-payment-method',
  'stock-partner',
  'stock-partner-name',
  'stock-partner-address',
];

export function StockVoucherMainFields({
  form,
  type,
  labels,
  readOnly,
  warehouses,
  reasons,
  paymentMethods,
  warehouseId,
  reasonId,
  paymentMethodId,
  selectedReason,
  selectedPartner,
  onWarehouseChange,
  onReasonChange,
  onSelectPartner,
  onEnterGrid,
}: StockVoucherMainFieldsProps) {
  const errors = form.formState.errors;

  function handleKeyDown(e: React.KeyboardEvent<HTMLElement>) {
    // `isComposing`: the Enter that commits a Telex/VNI composition must not jump to the next field.
    if (e.defaultPrevented || e.key !== 'Enter' || e.shiftKey || e.ctrlKey || e.altKey || e.metaKey || e.nativeEvent.isComposing) return;
    const at = MAIN_FIELD_IDS.indexOf((e.target as HTMLElement).id);
    if (at === -1) return;
    e.preventDefault();
    const next = MAIN_FIELD_IDS.slice(at + 1)
      .map((id) => document.getElementById(id) as HTMLInputElement | HTMLSelectElement | null)
      .find((el) => el && !el.disabled);
    if (next) next.focus();
    else onEnterGrid();
  }

  // PartnerAutocomplete swaps its input when a value is set, which drops the focus: put it back so Enter can go on.
  function handleSelectPartner(p: PartnerSearchItem | null) {
    onSelectPartner(p);
    if (p) setTimeout(() => document.getElementById('stock-partner')?.focus(), 0);
  }

  return (
    // eslint-disable-next-line jsx-a11y/no-noninteractive-element-interactions -- Enter walks the main fields.
    <fieldset
      disabled={readOnly}
      className="m-0 min-w-0 shrink-0 space-y-[6px] rounded-md border bg-card px-3 py-2"
      onKeyDown={handleKeyDown}
    >
      <div className="form-inline-grid form-cols-stock-main-1">
        <Label htmlFor="stock-voucher-at" className="field-label required">{labels.dateLabel}</Label>
        <div>
          <Input id="stock-voucher-at" type="datetime-local" className="h-7" {...form.register('voucherAt')} />
          <FieldError message={errors.voucherAt?.message} />
        </div>
        <Label htmlFor="stock-reason" className="field-label required">{labels.reasonLabel}</Label>
        <div>
          <select
            id="stock-reason"
            className={SELECT_CLASS}
            value={reasonId}
            onChange={(e) => onReasonChange(e.target.value)}
          >
            <option value="">-- Chọn lý do --</option>
            {reasons.map((r) => (
              <option key={r.id} value={r.id}>
                {r.name}
              </option>
            ))}
          </select>
          <FieldError message={errors.reasonId?.message} />
        </div>
        <Label htmlFor="stock-warehouse" className="field-label required">Kho</Label>
        <div>
          <select
            id="stock-warehouse"
            className={SELECT_CLASS}
            value={warehouseId}
            onChange={(e) => onWarehouseChange(e.target.value)}
          >
            <option value="">-- Chọn kho --</option>
            {warehouses.map((w) => (
              <option key={w.id} value={w.id}>
                {w.name}
              </option>
            ))}
          </select>
          <FieldError message={errors.warehouseId?.message} />
        </div>
        <Label htmlFor="stock-payment-method" className="field-label">HTTT</Label>
        <div>
          <select
            id="stock-payment-method"
            className={SELECT_CLASS}
            value={paymentMethodId ?? ''}
            onChange={(e) => form.setValue('paymentMethodId', e.target.value, { shouldDirty: true })}
          >
            <option value="">-- Hình thức thanh toán --</option>
            {paymentMethods.map((m) => (
              <option key={m.id} value={m.id}>
                {m.name}
              </option>
            ))}
          </select>
          <FieldError message={errors.paymentMethodId?.message} />
        </div>
      </div>

      <div className="form-inline-grid form-cols-stock-main-2">
        <Label htmlFor="stock-partner" className="field-label">Mã đối tượng</Label>
        <PartnerAutocomplete
          inputId="stock-partner"
          type={type}
          reasonId={reasonId || undefined}
          partnerType={selectedReason?.partnerType}
          value={selectedPartner}
          onSelect={handleSelectPartner}
          disabled={readOnly}
        />
        <Label htmlFor="stock-partner-name" className="field-label">Tên đối tượng</Label>
        <Input id="stock-partner-name" className="h-7" {...form.register('partnerName')} />
        <Label htmlFor="stock-partner-address" className="field-label">Địa chỉ</Label>
        <Input id="stock-partner-address" className="h-7" {...form.register('partnerAddress')} />
      </div>
    </fieldset>
  );
}
