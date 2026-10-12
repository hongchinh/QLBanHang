import { useEffect, useState } from 'react';
import { useForm, useWatch, type Resolver } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import {
  PartnerAutocomplete,
  type PartnerSearchHook,
} from '@/components/partner-autocomplete/partner-autocomplete';
import { ConfirmRequiredDialog } from '@/components/confirm-required-dialog/confirm-required-dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { DEBT_SIDE_LABELS, OPENING_DEBT_KIND_LABELS } from '@/features/cash-common/labels';
import type { DebtSide, OpeningDebtKind } from '@/features/cash-common/types';
import { useDebtPartnerSearch } from '@/features/debt/hooks';
import {
  useCreateOpeningDebt,
  useOpeningDateNotSetHandler,
  useOpeningDebtOwners,
  useUpdateOpeningDebt,
} from '@/features/opening-debts/hooks';
import {
  openingDebtSchema,
  toUpsertOpeningDebtRequest,
  type OpeningDebtFormParsed,
  type OpeningDebtFormValues,
} from '@/features/opening-debts/schema';
import type { OpeningDebt } from '@/features/opening-debts/types';
import { formatApiErrorDetails, getApiError } from '@/lib/api-client';
import { applyApiFieldErrors } from '@/lib/apply-api-field-errors';
import { toConfirmationRequest, withConfirmed, type ConfirmationRequest, type ConfirmFlags } from '@/lib/confirm-required';
import { formatMoneyForDisplay, parseMoneyInput } from '@/lib/money-input';
import { toast } from '@/lib/use-toast';
import { todayYmd } from '@/lib/vn-datetime';

const SELECT_CLASS =
  'h-9 w-full rounded-md border border-input bg-background px-3 text-sm disabled:cursor-not-allowed disabled:opacity-50';

const SIDES: DebtSide[] = ['Receivable', 'Payable'];
const KINDS: OpeningDebtKind[] = ['Debt', 'Prepayment'];
const CONCURRENCY_MESSAGE =
  'Nợ đầu kỳ đã được người khác cập nhật. Đã tải lại danh sách — vui lòng mở lại để sửa.';

const GONE_OWNER_LABEL = 'Nhân viên đã ngừng hoạt động';

const SERVER_FIELDS = ['partnerId', 'docNo', 'docDate', 'dueDate', 'amount', 'note'] as const;

// GET /api/debt/partners of the chosen side; one stable hook per side.
function useReceivablePartnerSearch(keyword: string) {
  return useDebtPartnerSearch('Receivable', keyword);
}
function usePayablePartnerSearch(keyword: string) {
  return useDebtPartnerSearch('Payable', keyword);
}
const PARTNER_SEARCH: Record<DebtSide, PartnerSearchHook> = {
  Receivable: useReceivablePartnerSearch,
  Payable: usePayablePartnerSearch,
};

interface PickedPartner {
  id: string;
  code: string;
  name: string;
}

// A 422 waiting for the user: the parsed values are resent with confirmRemoveSettlements (P10).
interface PendingConfirmation {
  request: ConfirmationRequest;
  values: OpeningDebtFormParsed;
  flags: ConfirmFlags;
}

interface Props {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  initial?: OpeningDebt;
  // The branch opening date: the default document date of a new row.
  openingDate?: string | null;
}

export function OpeningDebtFormDialog({ open, onOpenChange, initial, openingDate }: Props) {
  const handleOpeningDateNotSet = useOpeningDateNotSetHandler();
  const isEdit = !!initial;
  const create = useCreateOpeningDebt();
  const update = useUpdateOpeningDebt();
  const saving = create.isPending || update.isPending;

  const [partner, setPartner] = useState<PickedPartner | null>(null);
  const [ownerUserId, setOwnerUserId] = useState<string | null>(null);
  const { data: owners } = useOpeningDebtOwners();
  const [pending, setPending] = useState<PendingConfirmation | null>(null);

  const form = useForm<OpeningDebtFormValues, unknown, OpeningDebtFormParsed>({
    resolver: zodResolver(openingDebtSchema) as unknown as Resolver<
      OpeningDebtFormValues,
      unknown,
      OpeningDebtFormParsed
    >,
    defaultValues: toDefaults(initial, openingDate),
  });

  useEffect(() => {
    if (!open) return;
    form.reset(toDefaults(initial, openingDate));
    setPartner(initial ? { id: initial.partnerId, code: initial.partnerCode, name: initial.partnerName } : null);
    setOwnerUserId(initial?.ownerUserId ?? null);
    setPending(null);
  }, [open, initial, openingDate]); // eslint-disable-line react-hooks/exhaustive-deps

  const side = useWatch({ control: form.control, name: 'side' });
  const kind = useWatch({ control: form.control, name: 'kind' });
  const errors = form.formState.errors;
  // The loaded owner may be missing from the active options (deactivated or deleted user): keep it selectable.
  const ownerOptions = owners ?? [];
  const missingOwner =
    initial?.ownerUserId && !ownerOptions.some((o) => o.id === initial.ownerUserId)
      ? { id: initial.ownerUserId, fullName: initial.ownerName ?? GONE_OWNER_LABEL }
      : null;

  const submit = async (values: OpeningDebtFormParsed, flags: ConfirmFlags = {}) => {
    const data = toUpsertOpeningDebtRequest(values, {
      ownerUserId: initial ? ownerUserId : (ownerUserId ?? undefined),
      confirmRemoveSettlements: flags.confirmRemoveSettlements ?? false,
      confirmedSettlements: flags.confirmedSettlements,
      version: initial?.version,
    });
    try {
      if (initial) await update.mutateAsync({ id: initial.id, data });
      else await create.mutateAsync(data);
      toast({ variant: 'success', title: isEdit ? 'Đã cập nhật nợ đầu kỳ' : 'Đã thêm nợ đầu kỳ', description: data.docNo });
      onOpenChange(false);
    } catch (err) {
      const request = toConfirmationRequest(err);
      if (request) {
        setPending({ request, values, flags });
        return;
      }
      if (handleOpeningDateNotSet(err)) return;
      if (getApiError(err)?.code === 'CONCURRENCY') {
        // D29: the list is reloaded by the hook; close so the next edit starts from the saved row.
        toast({ variant: 'destructive', title: CONCURRENCY_MESSAGE });
        onOpenChange(false);
        return;
      }
      applyApiFieldErrors(err, form.setError, SERVER_FIELDS);
      toast({ variant: 'destructive', title: 'Không thể lưu', description: formatApiErrorDetails(err) });
    }
  };

  const onConfirm = () => {
    if (!pending) return;
    const { values, request, flags } = pending;
    setPending(null);
    void submit(values, withConfirmed(flags, request));
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-2xl">
        <DialogHeader>
          <DialogTitle>{isEdit ? 'Sửa nợ đầu kỳ' : 'Thêm nợ đầu kỳ'}</DialogTitle>
        </DialogHeader>

        <form
          onSubmit={form.handleSubmit((values) => submit(values))}
          className="grid gap-4 pt-2 sm:grid-cols-2"
        >
          <div className="space-y-2">
            <Label htmlFor="opening-debt-side">Phía *</Label>
            <select
              id="opening-debt-side"
              className={SELECT_CLASS}
              {...form.register('side', {
                // The partner picker follows the side, so a partner of the other side is cleared.
                onChange: () => {
                  setPartner(null);
                  form.setValue('partnerId', '');
                },
              })}
            >
              {SIDES.map((s) => (
                <option key={s} value={s}>
                  {DEBT_SIDE_LABELS[s]}
                </option>
              ))}
            </select>
          </div>

          <div className="space-y-2">
            <Label htmlFor="opening-debt-kind">Loại *</Label>
            <select
              id="opening-debt-kind"
              className={SELECT_CLASS}
              {...form.register('kind', {
                // A prepayment has no due date.
                onChange: (e) => {
                  if (e.target.value === 'Prepayment') form.setValue('dueDate', '');
                },
              })}
            >
              {KINDS.map((k) => (
                <option key={k} value={k}>
                  {OPENING_DEBT_KIND_LABELS[k]}
                </option>
              ))}
            </select>
          </div>

          <div className="space-y-2 sm:col-span-2">
            <Label htmlFor="opening-debt-partner">Đối tượng *</Label>
            <PartnerAutocomplete
              inputId="opening-debt-partner"
              useSearch={PARTNER_SEARCH[side]}
              requireReason={false}
              value={partner}
              placeholder={side === 'Receivable' ? 'Nhập mã / tên / MST khách hàng...' : 'Nhập mã / tên / MST nhà cung cấp...'}
              onSelect={(p) => {
                setPartner(p ? { id: p.id, code: p.code, name: p.name } : null);
                form.setValue('partnerId', p?.id ?? '', { shouldValidate: form.formState.isSubmitted });
              }}
            />
            {partner && <p className="text-sm text-muted-foreground">{partner.name}</p>}
            {errors.partnerId && <p className="text-sm text-destructive">{errors.partnerId.message}</p>}
          </div>

          <div className="space-y-2 sm:col-span-2">
            <Label htmlFor="opening-debt-owner">Nhân viên</Label>
            <select
              id="opening-debt-owner"
              className={SELECT_CLASS}
              value={ownerUserId ?? ''}
              onChange={(e) => setOwnerUserId(e.target.value || null)}
            >
              <option value="">Không chọn</option>
              {ownerOptions.map((o) => (
                <option key={o.id} value={o.id}>
                  {o.fullName}
                </option>
              ))}
              {missingOwner && <option value={missingOwner.id}>{missingOwner.fullName}</option>}
            </select>
          </div>

          <div className="space-y-2">
            <Label htmlFor="opening-debt-doc-no">Số CT *</Label>
            <Input id="opening-debt-doc-no" maxLength={50} {...form.register('docNo')} />
            {errors.docNo && <p className="text-sm text-destructive">{errors.docNo.message}</p>}
          </div>

          <div className="space-y-2">
            <Label htmlFor="opening-debt-doc-date">Ngày CT *</Label>
            <Input id="opening-debt-doc-date" type="date" {...form.register('docDate')} />
            {errors.docDate && <p className="text-sm text-destructive">{errors.docDate.message}</p>}
          </div>

          {kind !== 'Prepayment' && (
            <div className="space-y-2">
              <Label htmlFor="opening-debt-due-date">Hạn TT</Label>
              <Input id="opening-debt-due-date" type="date" {...form.register('dueDate')} />
              {errors.dueDate && <p className="text-sm text-destructive">{errors.dueDate.message}</p>}
            </div>
          )}

          <div className="space-y-2">
            <Label htmlFor="opening-debt-amount">Số tiền *</Label>
            <Input
              id="opening-debt-amount"
              inputMode="decimal"
              className="text-right tabular-nums"
              {...form.register('amount', {
                onBlur: (e) => {
                  const parsed = parseMoneyInput(e.target.value);
                  if (parsed !== undefined && parsed > 0) form.setValue('amount', formatMoneyForDisplay(parsed));
                },
              })}
            />
            {errors.amount && <p className="text-sm text-destructive">{errors.amount.message}</p>}
          </div>

          <div className="space-y-2 sm:col-span-2">
            <Label htmlFor="opening-debt-note">Ghi chú</Label>
            <Input id="opening-debt-note" maxLength={500} {...form.register('note')} />
            {errors.note && <p className="text-sm text-destructive">{errors.note.message}</p>}
          </div>

          <div className="flex justify-end gap-2 pt-2 sm:col-span-2">
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)} disabled={saving}>
              Hủy
            </Button>
            <Button type="submit" disabled={saving}>
              {saving ? 'Đang lưu...' : 'Lưu'}
            </Button>
          </div>
        </form>

        <ConfirmRequiredDialog
          request={pending?.request ?? null}
          onConfirm={onConfirm}
          onClose={() => setPending(null)}
        />
      </DialogContent>
    </Dialog>
  );
}

function toDefaults(item: OpeningDebt | undefined, openingDate: string | null | undefined): OpeningDebtFormValues {
  if (!item)
    return {
      partnerId: '',
      side: 'Receivable',
      kind: 'Debt',
      docNo: '',
      docDate: openingDate ?? todayYmd(),
      dueDate: '',
      amount: '',
      note: '',
    };
  return {
    partnerId: item.partnerId,
    side: item.side,
    kind: item.kind,
    docNo: item.docNo,
    docDate: item.docDate,
    dueDate: item.dueDate ?? '',
    amount: formatMoneyForDisplay(item.amount),
    note: item.note ?? '',
  };
}
