import { useWatch, type UseFormReturn } from 'react-hook-form';
import {
  PartnerAutocomplete,
  type PartnerSearchHook,
} from '@/components/partner-autocomplete/partner-autocomplete';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { FUND_KIND_LABELS } from '@/features/cash-common/labels';
import type { CashReason } from '@/features/cash-reasons/types';
import { useCashPartnerSearch } from '@/features/cash-vouchers/hooks';
import type { CashVoucherFormParsed, CashVoucherFormValues } from '@/features/cash-vouchers/schema';
import type { CashVoucherType } from '@/features/cash-vouchers/types';
import type { Fund } from '@/features/funds/types';
import type { PartnerType } from '@/features/stock-reasons/types';
import type { PartnerSearchItem } from '@/features/stock-vouchers/types';
import type { CashVoucherLabels } from '../labels';

const SELECT_CLASS =
  'h-7 w-full rounded-md border border-input bg-background px-2 text-sm disabled:cursor-not-allowed disabled:opacity-50';

// GET /cash-vouchers/partners of the voucher type; one stable hook per type.
function useReceiptPartnerSearch(keyword: string, reasonId?: string) {
  return useCashPartnerSearch('Receipt', keyword, reasonId);
}
function usePaymentPartnerSearch(keyword: string, reasonId?: string) {
  return useCashPartnerSearch('Payment', keyword, reasonId);
}
const PARTNER_SEARCH: Record<CashVoucherType, PartnerSearchHook> = {
  Receipt: useReceiptPartnerSearch,
  Payment: usePaymentPartnerSearch,
};

export type CashFundOption = Pick<Fund, 'id' | 'code' | 'name' | 'kind'>;
export type CashReasonOption = Pick<CashReason, 'id' | 'name'>;

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
  // Partner role of the selected reason; None disables the partner.
  partnerType?: PartnerType;
  selectedPartner: CashVoucherSelectedPartner | null;
  onSelectPartner: (partner: PartnerSearchItem | null) => void;
  onReasonChange: (reasonId: string) => void;
  readOnly: boolean;
}

export function CashVoucherHeader({
  form,
  type,
  labels,
  code,
  funds,
  reasons,
  partnerType,
  selectedPartner,
  onSelectPartner,
  onReasonChange,
  readOnly,
}: CashVoucherHeaderProps) {
  const fundId = useWatch({ control: form.control, name: 'fundId' });
  const reasonId = useWatch({ control: form.control, name: 'reasonId' });
  const selectedFund = funds.find((f) => f.id === fundId);
  const errors = form.formState.errors;

  return (
    <Card>
      <CardHeader className="flex h-9 flex-row items-center border-b bg-blue-50 p-1 px-3">
        <CardTitle>Thông tin chung</CardTitle>
      </CardHeader>
      <CardContent className="px-4 pb-3 pt-2">
        <fieldset disabled={readOnly} className="m-0 min-w-0 space-y-[6px] border-0 p-0">
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

          <div className="form-inline-grid form-cols-customer">
            <Label htmlFor="cash-reason" className="field-label required">Lý do</Label>
            <select
              id="cash-reason"
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
            <Label htmlFor="cash-partner" className="field-label">Đối tượng</Label>
            <PartnerAutocomplete
              inputId="cash-partner"
              useSearch={PARTNER_SEARCH[type]}
              requireReason
              reasonId={reasonId || undefined}
              partnerType={partnerType}
              value={selectedPartner}
              onSelect={onSelectPartner}
              disabled={readOnly}
            />
            <FieldError message={errors.reasonId?.message ?? errors.partnerId?.message} />
          </div>

          <div className="form-inline-grid form-cols-2">
            <Label htmlFor="cash-partner-name" className="field-label">Tên đối tượng</Label>
            <Input id="cash-partner-name" className="h-7" {...form.register('partnerName')} />
            <Label htmlFor="cash-partner-tax-code" className="field-label">MST</Label>
            <Input id="cash-partner-tax-code" className="h-7" {...form.register('partnerTaxCode')} />
          </div>

          <div className="form-inline-grid">
            <Label htmlFor="cash-partner-address" className="field-label">Địa chỉ</Label>
            <Input id="cash-partner-address" className="h-7" {...form.register('partnerAddress')} />
          </div>

          <div className="form-inline-grid form-cols-2">
            <Label htmlFor="cash-person-name" className="field-label">{labels.personLabel}</Label>
            <Input id="cash-person-name" className="h-7" {...form.register('personName')} />
            <Label htmlFor="cash-attached-docs" className="field-label">Kèm theo</Label>
            <Input
              id="cash-attached-docs"
              className="h-7"
              placeholder="Số chứng từ gốc"
              {...form.register('attachedDocs')}
            />
          </div>

          <div className="form-inline-grid">
            <Label htmlFor="cash-description" className="field-label">{labels.descriptionLabel}</Label>
            <Input id="cash-description" className="h-7" {...form.register('description')} />
          </div>

          <div className="form-inline-grid">
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
