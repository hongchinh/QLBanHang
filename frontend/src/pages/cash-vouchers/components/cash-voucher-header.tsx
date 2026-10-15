import { useWatch, type UseFormReturn } from 'react-hook-form';
import type { PartnerSearchHook } from '@/components/partner-autocomplete/partner-autocomplete';
import { PartnerCodeNameField } from '@/components/partner-autocomplete/partner-code-name-field';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { FUND_KIND_LABELS } from '@/features/cash-common/labels';
import { preferredPartnerRole } from '@/features/cash-common/reason-match';
import type { ReasonNotice } from '@/features/cash-common/reason-notice';
import { ReasonNoticeLine } from '@/features/cash-common/reason-notice-line';
import type { CashReason } from '@/features/cash-reasons/types';
import { useCashPartnerSearch } from '@/features/cash-vouchers/hooks';
import type { CashVoucherFormParsed, CashVoucherFormValues } from '@/features/cash-vouchers/schema';
import type { CashVoucherType } from '@/features/cash-vouchers/types';
import type { Fund } from '@/features/funds/types';
import type { PartnerSearchItem } from '@/features/stock-vouchers/types';
import { focusNextInOrder } from '@/lib/enter-focus';
import { DIALOG_SELECTOR } from '@/lib/focus';
import { cn } from '@/lib/utils';
import type { CashVoucherLabels } from '../labels';
import { CASH_HEADER_FIELD_IDS } from '../utils/cash-navigation';

const SELECT_CLASS =
  'h-7 w-full rounded-md border border-input bg-background px-2 text-sm disabled:cursor-not-allowed disabled:opacity-50';

// GET /cash-vouchers/partners of the voucher type; one stable hook per type.
function useReceiptPartnerSearch(keyword: string, activeOnly?: boolean) {
  return useCashPartnerSearch('Receipt', keyword, activeOnly);
}
function usePaymentPartnerSearch(keyword: string, activeOnly?: boolean) {
  return useCashPartnerSearch('Payment', keyword, activeOnly);
}
const PARTNER_SEARCH: Record<CashVoucherType, PartnerSearchHook> = {
  Receipt: useReceiptPartnerSearch,
  Payment: usePaymentPartnerSearch,
};

export type CashFundOption = Pick<Fund, 'id' | 'code' | 'name' | 'kind'>;
export type CashReasonOption = Pick<CashReason, 'id' | 'name' | 'partnerType' | 'debtEffect'>;

export interface CashVoucherSelectedPartner {
  id: string;
  code: string;
  name: string;
  // Known only for a partner picked in this session.
  isCustomer?: boolean;
  isSupplier?: boolean;
}

export interface CashVoucherHeaderProps {
  form: UseFormReturn<CashVoucherFormValues, unknown, CashVoucherFormParsed>;
  type: CashVoucherType;
  labels: CashVoucherLabels;
  code?: string;
  funds: CashFundOption[];
  reasons: CashReasonOption[];
  // Its partner type None locks the partner field.
  selectedReason?: CashReasonOption;
  selectedPartner: CashVoucherSelectedPartner | null;
  onSelectPartner: (partner: PartnerSearchItem | null) => void;
  onReasonChange: (reasonId: string) => void;
  // The yellow "reason changed" line or the red mismatch line under Lý do (section-01 §4).
  reasonNotice?: ReasonNotice | null;
  reasonFlash?: boolean;
  partnerError?: string;
  onUnpickedPartnerChange: (hasUnpickedText: boolean) => void;
  onPartnerNameChange: (name: string) => void;
  onDescriptionEdited: (value: string) => void;
  // Enter in Ghi chú, the last header field.
  onEnterGrid: () => void;
  readOnly: boolean;
}

export function CashVoucherHeader({
  form,
  type,
  labels,
  code,
  funds,
  reasons,
  selectedReason,
  selectedPartner,
  onSelectPartner,
  onReasonChange,
  reasonNotice,
  reasonFlash,
  partnerError,
  onUnpickedPartnerChange,
  onPartnerNameChange,
  onDescriptionEdited,
  onEnterGrid,
  readOnly,
}: CashVoucherHeaderProps) {
  const fundId = useWatch({ control: form.control, name: 'fundId' });
  const reasonId = useWatch({ control: form.control, name: 'reasonId' });
  const selectedFund = funds.find((f) => f.id === fundId);
  const errors = form.formState.errors;
  const reasonError = errors.reasonId?.message;

  // Enter / Shift+Enter walk the ten header fields (section-01 §2.1); Enter in Ghi chú goes down to the grid.
  function handleEnter(e: React.KeyboardEvent<HTMLElement>) {
    if (e.defaultPrevented || e.key !== 'Enter' || e.ctrlKey || e.altKey || e.metaKey || e.nativeEvent.isComposing) return;
    const target = e.target as HTMLElement;
    if (!CASH_HEADER_FIELD_IDS.includes(target.id) || target.closest(DIALOG_SELECTOR)) return;
    e.preventDefault();
    const step = focusNextInOrder(CASH_HEADER_FIELD_IDS, target.id, e.shiftKey ? -1 : 1);
    if (step === 'end' && !e.shiftKey) onEnterGrid();
  }

  return (
    <Card>
      <CardHeader className="flex h-9 flex-row items-center border-b bg-blue-50 p-1 px-3">
        <CardTitle>Thông tin chung</CardTitle>
      </CardHeader>
      <CardContent className="px-4 pb-3 pt-2">
        {/* eslint-disable-next-line jsx-a11y/no-noninteractive-element-interactions -- Enter walks the header fields. */}
        <fieldset
          disabled={readOnly}
          className="m-0 min-w-0 space-y-[6px] border-0 p-0"
          onKeyDown={handleEnter}
        >
          <div className="form-inline-grid form-cols-3">
            <Label htmlFor="cash-voucher-code" className="field-label">Số phiếu</Label>
            <Input
              id="cash-voucher-code"
              className="h-7 bg-muted"
              readOnly
              tabIndex={-1}
              value={code ?? ''}
              placeholder="Tự động"
            />
            <Label htmlFor="cash-voucher-at" className="field-label required">Ngày giờ</Label>
            <Input id="cash-voucher-at" type="datetime-local" className="h-7" {...form.register('voucherAt')} />
            <Label htmlFor="cash-fund" className="field-label required">Quỹ</Label>
            <div className="flex min-w-0 items-center gap-2">
              <select
                id="cash-fund"
                className={SELECT_CLASS}
                value={fundId}
                onChange={(e) =>
                  form.setValue('fundId', e.target.value, {
                    shouldDirty: true,
                    shouldValidate: form.formState.isSubmitted,
                  })
                }
              >
                <option value="">-- Chọn quỹ --</option>
                {funds.map((f) => (
                  <option key={f.id} value={f.id}>
                    {`${f.code} – ${f.name}`}
                  </option>
                ))}
              </select>
              {selectedFund && (
                <Badge variant="secondary" className="shrink-0">
                  {FUND_KIND_LABELS[selectedFund.kind]}
                </Badge>
              )}
            </div>
            <FieldError message={errors.voucherAt?.message ?? errors.fundId?.message} />
          </div>

          {/* The field renders a fragment (label, code + name, error), so it gets its own grid row. */}
          <div className="form-inline-grid form-align-top">
            <PartnerCodeNameField
              label="Đối tượng"
              scope="partner"
              preferredRole={preferredPartnerRole(selectedReason, type === 'Payment' ? 'supplier' : 'customer')}
              codeInputId="cash-partner"
              nameInputId="cash-partner-name"
              nameInputProps={form.register('partnerName', { onChange: (e) => onPartnerNameChange(e.target.value) })}
              value={selectedPartner}
              onSelect={onSelectPartner}
              useSearch={PARTNER_SEARCH[type]}
              partnerType={selectedReason?.partnerType}
              disabled={readOnly}
              error={partnerError}
              onUnpickedChange={onUnpickedPartnerChange}
            />
          </div>

          <div className="form-inline-grid form-cols-2 form-align-top">
            <Label htmlFor="cash-reason" className="field-label required">Lý do</Label>
            {/* The message stays in the select's cell, so it sits under Lý do at every width. */}
            <div className="min-w-0">
              <select
                id="cash-reason"
                className={cn(SELECT_CLASS, reasonFlash && 'ring-2 ring-amber-400')}
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
              <ReasonNoticeLine error={reasonError} notice={reasonNotice ?? null} />
            </div>
            <Label htmlFor="cash-person-name" className="field-label">{labels.personLabel}</Label>
            <Input id="cash-person-name" className="h-7" {...form.register('personName')} />
          </div>

          <div className="form-inline-grid">
            <Label htmlFor="cash-partner-address" className="field-label">Địa chỉ</Label>
            <Input id="cash-partner-address" className="h-7" {...form.register('partnerAddress')} />
          </div>

          <div className="form-inline-grid">
            <Label htmlFor="cash-description" className="field-label">{labels.descriptionLabel}</Label>
            <Input
              id="cash-description"
              className="h-7"
              {...form.register('description', { onChange: (e) => onDescriptionEdited(e.target.value) })}
            />
          </div>

          <div className="form-inline-grid form-cols-attached-note">
            <Label htmlFor="cash-attached-docs" className="field-label">Kèm theo</Label>
            <Input
              id="cash-attached-docs"
              className="h-7"
              placeholder="Số chứng từ gốc"
              {...form.register('attachedDocs')}
            />
            <Label htmlFor="cash-voucher-note" className="field-label">Ghi chú</Label>
            <Input id="cash-voucher-note" className="h-7" {...form.register('note')} />
          </div>
        </fieldset>
      </CardContent>
    </Card>
  );
}

function FieldError({ message }: { message?: string }) {
  if (!message) return null;
  return <p className="field-message field-message-code text-destructive">{message}</p>;
}
