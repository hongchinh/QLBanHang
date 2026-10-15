import { useEffect, useRef, useState } from 'react';
import { useForm, useWatch, type FieldPath, type Resolver } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Link, Navigate, useNavigate, useParams } from 'react-router-dom';
import { AppIcon } from '@/components/icons';
import {
  useCancelCashVoucher,
  useCashVoucher,
  useCashVoucherActivities,
  useCashVoucherDefaults,
  useCreateCashVoucher,
  useDeleteCashVoucher,
  useRestoreCashVoucher,
  useUpdateCashVoucher,
} from '@/features/cash-vouchers/hooks';
import {
  cashVoucherSchema,
  type CashAllocationFormValues,
  type CashLineFormValues,
  type CashVoucherFormParsed,
  type CashVoucherFormValues,
} from '@/features/cash-vouchers/schema';
import { isOwnAllocation, toFormDefaults, toUpsertPayload } from '@/features/cash-vouchers/payload';
import { allocationTotal, checkAllocations, suggestAllocations } from '@/features/cash-vouchers/allocation';
import type {
  CashVoucher,
  CashVoucherAllocation,
  CashVoucherDefaults,
  CashVoucherType,
  UpsertCashVoucherRequest,
} from '@/features/cash-vouchers/types';
import type { DebtDirection, DebtEffect, DebtSide, SettlementOrigin } from '@/features/cash-common/types';
import { useCashReasons } from '@/features/cash-reasons/hooks';
import { useFunds } from '@/features/funds/hooks';
import { selectableFunds } from '@/features/funds/utils';
import { useOpenItems } from '@/features/debt/hooks';
import type { PartnerType } from '@/features/stock-reasons/types';
import type { PartnerSearchItem } from '@/features/stock-vouchers/types';
import { ActivityHistory } from '@/components/activity-history/activity-history';
import { ConfirmRequiredDialog } from '@/components/confirm-required-dialog/confirm-required-dialog';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { ButtonLoader } from '@/components/ui/button-loader';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { PaymentQrButton } from '@/components/payment-qr/payment-qr-button';
import { VoucherPrintButton } from '@/components/voucher-print/voucher-print-button';
import { useDocumentActions } from '@/hooks/useDocumentActions';
import { getErrorMessage } from '@/lib/api-client';
import type { ConfirmFlags } from '@/lib/confirm-required';
import { formatDateOnly } from '@/lib/date-only';
import { roundAwayFromZero } from '@/lib/round';
import { toNumberOrZero } from '@/lib/to-number';
import { toast } from '@/lib/use-toast';
import { AllocationGrid } from './components/allocation-grid';
import { CashLineGrid } from './components/cash-line-grid';
import {
  CashVoucherHeader,
  type CashFundOption,
  type CashReasonOption,
  type CashVoucherSelectedPartner,
} from './components/cash-voucher-header';
import { CASH_VOUCHER_LABELS, type CashVoucherLabels } from './labels';

type SubmitIntent = 'save-stay' | 'save-exit' | 'update';
type VoucherActionKind = 'cancel' | 'restore' | 'delete';
// `flags` are the confirmations already given for this action (P10); a resend keeps them.
type PendingAction =
  | { kind: 'save'; intent: SubmitIntent; payload: UpsertCashVoucherRequest; flags: ConfirmFlags }
  | { kind: VoucherActionKind; version?: number; flags: ConfirmFlags };

const CONFIRM_LABELS: Record<PendingAction['kind'], string> = {
  save: 'Vẫn lưu',
  cancel: 'Vẫn hủy phiếu',
  restore: 'Vẫn khôi phục',
  delete: 'Vẫn xóa',
};

const ERROR_TITLES: Record<PendingAction['kind'], string> = {
  save: 'Không thể lưu phiếu',
  cancel: 'Không thể hủy phiếu',
  restore: 'Không thể khôi phục phiếu',
  delete: 'Không thể xóa phiếu',
};

const CONCURRENCY_MESSAGE =
  'Phiếu đã được người khác cập nhật. Đã tải lại dữ liệu — các thay đổi chưa lưu đã bị bỏ.';
const CONCURRENCY_RELOAD_FAILED_MESSAGE =
  'Phiếu đã được người khác cập nhật nhưng không tải lại được dữ liệu. Các thay đổi của bạn vẫn được giữ — vui lòng thử lại.';

const LINE_ERROR_FIELDS = new Set<string>(['description', 'amount', 'note']);
// Header fields that show their own error message.
const HEADER_ERROR_FIELDS = new Set<string>(['voucherAt', 'fundId', 'reasonId', 'partnerId']);

// The voucher's own debt entry: its side and direction follow the reason's effect.
const OWN_ENTRY: Record<DebtEffect, { side: DebtSide; direction: DebtDirection } | null> = {
  None: null,
  ReceivableDecrease: { side: 'Receivable', direction: 'Decrease' },
  ReceivableIncrease: { side: 'Receivable', direction: 'Increase' },
  PayableDecrease: { side: 'Payable', direction: 'Decrease' },
  PayableIncrease: { side: 'Payable', direction: 'Increase' },
};

const SETTLEMENT_ORIGIN_LABELS: Record<SettlementOrigin, string> = {
  AtVoucher: 'Tại phiếu',
  Manual: 'Đối trừ chứng từ',
  Offset: 'Bù trừ công nợ',
  Auto: 'Tự động',
};

const moneyFormat = new Intl.NumberFormat('vi-VN');
const round2 = (value: number) => roundAwayFromZero(value, 2);

export function CashVoucherFormPage({ type }: { type: CashVoucherType }) {
  const { id } = useParams<{ id: string }>();
  const isEdit = !!id && id !== 'new';
  const voucherQuery = useCashVoucher(isEdit ? id : undefined);
  // Only this first defaults load (server "now" and the next code) gates the form mount; it is never cached.
  const defaultsQuery = useCashVoucherDefaults(type, { enabled: !isEdit, fresh: true });

  if (isEdit && voucherQuery.isLoading) {
    return <div className="text-sm text-muted-foreground">Đang tải...</div>;
  }
  if (isEdit && !voucherQuery.data) {
    return <div className="text-sm text-destructive">Không tìm thấy phiếu.</div>;
  }
  // /receipts/<id of a payment> (or the reverse) opens the voucher under its own route.
  if (isEdit && voucherQuery.data && voucherQuery.data.type !== type) {
    return <Navigate to={`${CASH_VOUCHER_LABELS[voucherQuery.data.type].basePath}/${id}`} replace />;
  }
  if (!isEdit && !defaultsQuery.isFetchedAfterMount) {
    return <div className="text-sm text-muted-foreground">Đang tải...</div>;
  }

  return (
    <CashVoucherForm
      key={`${type}:${isEdit ? id : 'new'}`}
      type={type}
      id={isEdit ? id : undefined}
      voucher={isEdit ? voucherQuery.data : undefined}
      defaults={isEdit ? undefined : defaultsQuery.data}
      refetchVoucher={async () => {
        // A failed refetch still carries the old cached data (and version): report it as no data.
        const result = await voucherQuery.refetch();
        return result.status === 'success' ? result.data : undefined;
      }}
    />
  );
}

interface FormProps {
  type: CashVoucherType;
  id?: string;
  voucher?: CashVoucher;
  defaults?: CashVoucherDefaults;
  refetchVoucher: () => Promise<CashVoucher | undefined>;
}

function CashVoucherForm({ type, id, voucher, defaults, refetchVoucher }: FormProps) {
  const isEdit = !!id;
  const labels: CashVoucherLabels = CASH_VOUCHER_LABELS[type];
  const navigate = useNavigate();

  const create = useCreateCashVoucher();
  const update = useUpdateCashVoucher();
  const cancel = useCancelCashVoucher();
  const restore = useRestoreCashVoucher();
  const remove = useDeleteCashVoucher();

  const form = useForm<CashVoucherFormValues, unknown, CashVoucherFormParsed>({
    resolver: zodResolver(cashVoucherSchema) as unknown as Resolver<
      CashVoucherFormValues,
      unknown,
      CashVoucherFormParsed
    >,
    defaultValues: toFormDefaults(voucher, defaults, type),
  });

  const [selectedPartner, setSelectedPartner] = useState<CashVoucherSelectedPartner | null>(() =>
    partnerOf(voucher),
  );
  // `busy` is set only after async validation; the ref blocks a second submit (Ctrl+S key repeat) at once.
  const submittingRef = useRef(false);
  const [confirmKind, setConfirmKind] = useState<VoucherActionKind | null>(null);
  // Server messages of `allocations[i].*` (by entryId) and of `allocations`.
  const [allocationErrors, setAllocationErrors] = useState<Record<string, string>>({});
  const [allocationRootError, setAllocationRootError] = useState<string | undefined>();

  const status = voucher?.status ?? 'Active';
  const isManaged = !!voucher?.isManaged;
  const readOnly = isEdit && (isManaged || status === 'Cancelled' || !voucher?.canEdit);

  // A background refetch resets the form only when it is not dirty (D29).
  const lastVersionRef = useRef(voucher?.version);
  const isDirty = form.formState.isDirty;
  useEffect(() => {
    if (!voucher || voucher.version === lastVersionRef.current) return;
    lastVersionRef.current = voucher.version;
    if (isDirty) return;
    form.reset(toFormDefaults(voucher, undefined, type));
    setSelectedPartner(partnerOf(voucher));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [voucher?.version]);

  const fundId = useWatch({ control: form.control, name: 'fundId' });
  const reasonId = useWatch({ control: form.control, name: 'reasonId' });
  const partnerId = useWatch({ control: form.control, name: 'partnerId' });
  const watchedLines = (useWatch({ control: form.control, name: 'lines' }) ?? []) as CashLineFormValues[];
  const allocations = (useWatch({ control: form.control, name: 'allocations' }) ?? []) as CashAllocationFormValues[];
  const allocationsTouched = useWatch({ control: form.control, name: 'allocationsTouched' });

  const { data: allFunds = [] } = useFunds();
  const fundOptions: CashFundOption[] = selectableFunds(allFunds, undefined, fundId);
  // A fund the list no longer offers (deleted, other branch) still shows on the voucher that uses it.
  if (voucher && fundId === voucher.fundId && !fundOptions.some((f) => f.id === fundId)) {
    fundOptions.push({ id: voucher.fundId, code: voucher.fundCode, name: voucher.fundName, kind: voucher.fundKind });
  }

  // Automatic reasons (TPX/CPN) are never offered on a manual voucher.
  const { data: allReasons = [] } = useCashReasons({ type });
  const reasons = allReasons.filter((r) => r.type === type && !r.isAuto);
  const selectedReason = reasons.find((r) => r.id === reasonId);
  const reasonOptions: CashReasonOption[] = [...reasons];
  if (voucher && reasonId === voucher.reasonId && !selectedReason) {
    reasonOptions.push({ id: voucher.reasonId, name: voucher.reasonName });
  }
  const debtEffectOf = (id: string): DebtEffect =>
    reasons.find((r) => r.id === id)?.debtEffect ??
    (voucher && id === voucher.reasonId ? voucher.reasonDebtEffect : 'None');
  const debtEffect = debtEffectOf(reasonId);
  const ownEntry = OWN_ENTRY[debtEffect];
  const showAllocations = !!ownEntry && !!partnerId;

  // Display only: the server computes Total = Σ lines.
  const total = round2(watchedLines.reduce((sum, line) => sum + toNumberOrZero(line.amount), 0));
  // Settlements this form does not edit (Manual/Offset/Auto, another voucher's AtVoucher) use part of its amount.
  const otherSettled = allocationTotal((voucher?.allocations ?? []).filter((a) => !isOwnAllocation(a)));

  // Open items use the opposite direction; on edit they add back the room this voucher already uses (P26).
  const openItemsQuery = useOpenItems(
    {
      partnerId: partnerId ?? '',
      side: ownEntry?.side ?? 'Receivable',
      direction: ownEntry?.direction === 'Increase' ? 'Decrease' : 'Increase',
      forSourceType: type,
      forSourceId: id,
    },
    { enabled: showAllocations && !readOnly },
  );
  const openItems = openItemsQuery.data;

  // The allocations follow the earliest-due suggestion until the user edits them.
  useEffect(() => {
    if (readOnly || allocationsTouched || !showAllocations || !openItems) return;
    const next = suggestAllocations(
      openItems.map((item) => ({ ...item, dueDate: item.dueDate ?? null })),
      round2(total - otherSettled),
    );
    if (sameAllocations(form.getValues('allocations'), next)) return;
    form.setValue('allocations', next, { shouldDirty: true });
  }, [form, readOnly, allocationsTouched, showAllocations, openItems, total, otherSettled]);

  // The grid flags these rows; the server would reject them, so saving is blocked until they are fixed.
  const allocationsInvalid =
    showAllocations &&
    !readOnly &&
    !!openItems &&
    checkAllocations(
      openItems,
      allocations.map((row) => ({ entryId: row.entryId, amount: toNumberOrZero(row.amount) })),
      total,
      otherSettled,
    ).invalid;

  const activitiesQuery = useCashVoucherActivities(id, isEdit);

  function resetTo(fresh: CashVoucher) {
    lastVersionRef.current = fresh.version;
    form.reset(toFormDefaults(fresh, undefined, type));
    setSelectedPartner(partnerOf(fresh));
    clearAllocationErrors();
  }

  function clearAllocationErrors() {
    setAllocationErrors({});
    setAllocationRootError(undefined);
  }

  const { busy, run: runAction, confirmDialogProps } = useDocumentActions<PendingAction>({
    perform,
    reload: refetchVoucher,
    onReloaded: (fresh) => resetTo(fresh as CashVoucher),
    applyValidationErrors,
    errorTitles: ERROR_TITLES,
    confirmLabels: CONFIRM_LABELS,
    concurrencyMessage: CONCURRENCY_MESSAGE,
    concurrencyReloadFailedMessage: CONCURRENCY_RELOAD_FAILED_MESSAGE,
  });

  async function perform(action: PendingAction, flags: ConfirmFlags) {
    clearAllocationErrors();
    if (action.kind === 'save') {
      const payload = { ...action.payload, ...flags };
      if (id) {
        const saved = await update.mutateAsync({ id, data: payload });
        resetTo(saved);
        toast({ variant: 'success', title: 'Đã cập nhật phiếu', description: saved.code });
      } else {
        const saved = await create.mutateAsync(payload);
        toast({ variant: 'success', title: `Đã tạo ${labels.title.toLowerCase()}`, description: saved.code });
        if (action.intent === 'save-exit') navigate(labels.basePath);
        else navigate(`${labels.basePath}/${saved.id}`, { replace: true });
      }
    } else if (id) {
      const body = { version: action.version, ...flags };
      if (action.kind === 'delete') {
        await remove.mutateAsync({ id, body });
        toast({ variant: 'success', title: 'Đã xóa phiếu' });
        navigate(labels.basePath);
      } else {
        const saved = await (action.kind === 'cancel' ? cancel : restore).mutateAsync({ id, body });
        resetTo(saved);
        toast({ variant: 'success', title: action.kind === 'cancel' ? 'Đã hủy phiếu' : 'Đã khôi phục phiếu' });
      }
    }
  }

  // Maps line, allocation and header keys onto the form; returns true when every key was mapped.
  // `allocations[i]` indexes the sent payload, so it is resolved to the entry through it.
  function applyValidationErrors(details: Record<string, string[]>, action: PendingAction): boolean {
    const sentAllocations = action.kind === 'save' ? (action.payload.allocations ?? []) : [];
    const byEntry: Record<string, string> = {};
    let allMapped = true;
    for (const [key, messages] of Object.entries(details)) {
      const line = /^lines\[(\d+)\]\.(\w+)$/.exec(key);
      const allocation = /^allocations\[(\d+)\]\.(\w+)$/.exec(key);
      if (line && LINE_ERROR_FIELDS.has(line[2])) {
        const path = `lines.${line[1]}.${line[2]}` as FieldPath<CashVoucherFormValues>;
        form.setError(path, { type: 'server', message: messages[0] });
      } else if (allocation && sentAllocations[Number(allocation[1])]) {
        const entryId = sentAllocations[Number(allocation[1])].entryId;
        byEntry[entryId] ??= messages[0];
      } else if (key === 'allocations') {
        setAllocationRootError(messages[0]);
      } else if (HEADER_ERROR_FIELDS.has(key)) {
        form.setError(key as FieldPath<CashVoucherFormValues>, { type: 'server', message: messages[0] });
      } else {
        allMapped = false;
      }
    }
    setAllocationErrors(byEntry);
    return allMapped;
  }

  // Trailing rows the user never filled are dropped instead of failing validation.
  function dropBlankLines() {
    const lines = form.getValues('lines');
    const kept = lines.filter((l) => !!l.description?.trim() || toNumberOrZero(l.amount) !== 0);
    if (kept.length > 0 && kept.length < lines.length) form.setValue('lines', kept, { shouldDirty: true });
  }

  function submitWithIntent(intent: SubmitIntent) {
    if (submittingRef.current || busy || readOnly || allocationsInvalid) return;
    submittingRef.current = true;
    dropBlankLines();
    void form
      .handleSubmit((parsed) =>
        runAction({ kind: 'save', intent, payload: toUpsertPayload(parsed, debtEffect), flags: {} }),
      )()
      .finally(() => {
        submittingRef.current = false;
      });
  }

  function handleFormKeyDown(e: React.KeyboardEvent<HTMLFormElement>) {
    if (e.defaultPrevented) return;
    if (e.key === 's' && e.ctrlKey && !e.shiftKey && !e.altKey) {
      e.preventDefault();
      submitWithIntent(isEdit ? 'update' : 'save-stay');
    }
  }

  // Allocations belong to one partner and side: a change starts over from the suggestion.
  function resetAllocations() {
    form.setValue('allocations', [], { shouldDirty: true });
    form.setValue('allocationsTouched', false, { shouldDirty: true });
    clearAllocationErrors();
  }

  function handleSelectPartner(p: PartnerSearchItem | null) {
    if (!p) {
      clearPartner();
      return;
    }
    form.setValue('partnerId', p.id, { shouldDirty: true });
    form.setValue('partnerName', p.name, { shouldDirty: true });
    form.setValue('partnerAddress', p.companyAddress ?? '', { shouldDirty: true });
    form.setValue('partnerTaxCode', p.taxCode ?? '', { shouldDirty: true });
    setSelectedPartner({ id: p.id, code: p.code, name: p.name, isCustomer: p.isCustomer, isSupplier: p.isSupplier });
    resetAllocations();
  }

  function clearPartner() {
    form.setValue('partnerId', '', { shouldDirty: true });
    form.setValue('partnerName', '', { shouldDirty: true });
    form.setValue('partnerAddress', '', { shouldDirty: true });
    form.setValue('partnerTaxCode', '', { shouldDirty: true });
    setSelectedPartner(null);
    resetAllocations();
  }

  // A partner that does not fit the new reason's role is dropped (the server would reject it).
  function handleReasonChange(nextId: string) {
    const previousType = selectedReason?.partnerType;
    const nextType = reasons.find((r) => r.id === nextId)?.partnerType;
    const effectChanged = debtEffectOf(nextId) !== debtEffect;
    form.setValue('reasonId', nextId, { shouldDirty: true, shouldValidate: form.formState.isSubmitted });
    if (nextType === 'None') clearPartner();
    else if (selectedPartner && !partnerFits(selectedPartner, nextType, previousType)) clearPartner();
    else if (effectChanged) resetAllocations();
  }

  function handleAllocationsChange(rows: CashAllocationFormValues[]) {
    form.setValue('allocations', rows, { shouldDirty: true });
    form.setValue('allocationsTouched', true, { shouldDirty: true });
    clearAllocationErrors();
  }

  // "Gợi ý" puts the allocations back on the suggestion, which then follows the amount again.
  function handleSuggest(rows: CashAllocationFormValues[]) {
    form.setValue('allocations', rows, { shouldDirty: true });
    form.setValue('allocationsTouched', false, { shouldDirty: true });
    clearAllocationErrors();
  }

  const expectedCode = defaults?.nextCode;

  return (
    <div className="space-y-4">
      <div className="sticky top-0 z-30 -mx-4 border-b bg-background/95 px-4 py-3 shadow-sm backdrop-blur md:-mx-3 md:px-3">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div className="flex min-w-0 flex-wrap items-center gap-2">
            <Button variant="ghost" size="icon" asChild aria-label="Quay lại">
              <Link to={labels.basePath}>
                <AppIcon name="back" />
              </Link>
            </Button>
            <h1 className="truncate text-xl font-bold">
              {isEdit ? `${labels.title} ${voucher?.code ?? ''}`.trim() : `Thêm ${labels.title.toLowerCase()}`}
            </h1>
            {status === 'Cancelled' && <Badge variant="destructive">Đã hủy</Badge>}
            {isManaged && <Badge variant="secondary">Tự động</Badge>}
            {!isEdit && expectedCode && (
              <span className="text-sm text-muted-foreground">Số phiếu dự kiến: {expectedCode}</span>
            )}
          </div>
          <div className="flex flex-wrap justify-end gap-2">
            {isEdit && voucher && (
              <VoucherPrintButton
                kind="cash"
                type={type}
                vouchers={[{ id: voucher.id, code: voucher.code }]}
                disabled={isDirty || busy}
                disabledReason={isDirty ? 'Lưu thay đổi trước khi in' : undefined}
              />
            )}
            {isEdit && voucher && voucher.type === 'Receipt' && (
              <PaymentQrButton
                kind="cash"
                id={voucher.id}
                code={voucher.code}
                disabled={isDirty || busy || status === 'Cancelled'}
                disabledReason={
                  status === 'Cancelled' ? 'Phiếu đã hủy' : isDirty ? 'Lưu thay đổi trước khi xem QR' : undefined
                }
              />
            )}
            {!isEdit && (
              <>
                <Button type="button" variant="outline" size="sm" onClick={() => submitWithIntent('save-exit')} disabled={busy || allocationsInvalid}>
                  Lưu và thoát
                </Button>
                <Button type="button" size="sm" onClick={() => submitWithIntent('save-stay')} disabled={busy || allocationsInvalid}>
                  {busy ? <ButtonLoader className="mr-2" /> : <AppIcon name="save" tone="none" className="mr-2" />}
                  Lưu tạm
                </Button>
              </>
            )}
            {isEdit && isManaged && (
              <Button type="button" variant="outline" size="sm" onClick={() => navigate(labels.basePath)}>
                <AppIcon name="closeForm" className="mr-2" />
                Đóng
              </Button>
            )}
            {isEdit && !isManaged && !readOnly && (
              <Button type="button" size="sm" onClick={() => submitWithIntent('update')} disabled={busy || allocationsInvalid}>
                {busy ? <ButtonLoader className="mr-2" /> : <AppIcon name="save" tone="none" className="mr-2" />}
                Cập nhật
              </Button>
            )}
            {isEdit && !isManaged && voucher?.canCancel && status === 'Active' && (
              <Button type="button" variant="outline" size="sm" onClick={() => setConfirmKind('cancel')} disabled={busy}>
                <AppIcon name="cancelVoucher" className="mr-2" />
                Hủy phiếu
              </Button>
            )}
            {isEdit && !isManaged && voucher?.canCancel && status === 'Cancelled' && (
              <Button type="button" variant="outline" size="sm" onClick={() => setConfirmKind('restore')} disabled={busy}>
                <AppIcon name="restore" className="mr-2" />
                Khôi phục
              </Button>
            )}
            {isEdit && !isManaged && voucher?.canDelete && (
              <Button type="button" variant="outline" size="sm" onClick={() => setConfirmKind('delete')} disabled={busy}>
                <AppIcon name="delete" className="mr-2" />
                Xóa
              </Button>
            )}
          </div>
        </div>
      </div>

      {isManaged && voucher && (
        <div className="flex flex-wrap items-center gap-x-3 gap-y-1 rounded-md border border-amber-300 bg-amber-50 p-3 text-sm text-amber-900">
          <span>{labels.managedBanner(voucher.sourceStockVoucherCode ?? '')}</span>
          {voucher.sourceStockVoucherId && (
            <Link
              to={`${labels.stockVoucherBasePath}/${voucher.sourceStockVoucherId}`}
              className="font-medium text-blue-700 underline underline-offset-2 hover:text-blue-900"
            >
              Mở phiếu kho
            </Link>
          )}
        </div>
      )}
      {isEdit && !isManaged && status === 'Active' && readOnly && (
        <div className="rounded-md border border-amber-300 bg-amber-50 p-3 text-sm text-amber-900">
          Bạn không có quyền sửa phiếu này.
        </div>
      )}

      {/* eslint-disable-next-line jsx-a11y/no-noninteractive-element-interactions -- Form-level Ctrl+S shortcut delegates to the submit handler. */}
      <form
        onSubmit={(e) => {
          e.preventDefault();
          submitWithIntent(isEdit ? 'update' : 'save-stay');
        }}
        onKeyDown={handleFormKeyDown}
        className="space-y-4"
      >
        <CashVoucherHeader
          form={form}
          type={type}
          labels={labels}
          code={voucher?.code}
          funds={fundOptions}
          reasons={reasonOptions}
          partnerType={selectedReason?.partnerType}
          selectedPartner={selectedPartner}
          onSelectPartner={handleSelectPartner}
          onReasonChange={handleReasonChange}
          readOnly={readOnly}
        />

        <Card>
          <CardContent className="p-0">
            <CashLineGrid form={form} readOnly={readOnly} />
          </CardContent>
        </Card>

        {showAllocations && (
          <Card>
            <CardHeader className="flex h-9 flex-row items-center border-b bg-blue-50 p-1 px-3">
              <CardTitle>Phân bổ chứng từ</CardTitle>
            </CardHeader>
            <CardContent className="px-4 pb-3 pt-3">
              {readOnly ? (
                <StoredAllocations rows={voucher?.allocations ?? []} />
              ) : openItemsQuery.isLoading ? (
                <div className="text-sm text-muted-foreground">Đang tải chứng từ còn nợ...</div>
              ) : openItemsQuery.isError ? (
                <div className="text-sm text-destructive">{getErrorMessage(openItemsQuery.error)}</div>
              ) : (
                <AllocationGrid
                  items={openItems ?? []}
                  value={allocations}
                  total={total}
                  otherSettled={otherSettled}
                  readOnly={readOnly}
                  onChange={handleAllocationsChange}
                  onSuggest={handleSuggest}
                  errors={allocationErrors}
                />
              )}
              {allocationRootError && <p className="mt-2 text-sm text-destructive">{allocationRootError}</p>}
            </CardContent>
          </Card>
        )}
      </form>

      {isEdit && (
        <Card>
          <CardHeader className="flex h-9 flex-row items-center border-b bg-blue-50 p-1 px-3">
            <CardTitle>Lịch sử</CardTitle>
          </CardHeader>
          <CardContent className="pt-3">
            <ActivityHistory
              activities={activitiesQuery.data ?? []}
              isLoading={activitiesQuery.isLoading}
              isError={activitiesQuery.isError}
              errorMessage={getErrorMessage(activitiesQuery.error)}
              onRetry={() => void activitiesQuery.refetch()}
            />
          </CardContent>
        </Card>
      )}

      <ConfirmDialog
        open={confirmKind === 'cancel'}
        onOpenChange={(open) => !open && setConfirmKind(null)}
        title="Hủy phiếu?"
        description={`Phiếu ${voucher?.code ?? ''} sẽ bị hủy và không còn tính vào quỹ và công nợ.`}
        confirmLabel="Hủy phiếu"
        cancelLabel="Quay lại"
        destructive
        onConfirm={() => confirmVoucherAction('cancel')}
      />
      <ConfirmDialog
        open={confirmKind === 'restore'}
        onOpenChange={(open) => !open && setConfirmKind(null)}
        title="Khôi phục phiếu?"
        description={`Phiếu ${voucher?.code ?? ''} sẽ được tính lại vào quỹ và công nợ.`}
        confirmLabel="Khôi phục"
        cancelLabel="Quay lại"
        onConfirm={() => confirmVoucherAction('restore')}
      />
      <ConfirmDialog
        open={confirmKind === 'delete'}
        onOpenChange={(open) => !open && setConfirmKind(null)}
        title="Xóa phiếu?"
        description={`Phiếu ${voucher?.code ?? ''} sẽ bị xóa. Thao tác này không thể hoàn tác.`}
        confirmLabel="Xóa"
        cancelLabel="Quay lại"
        destructive
        onConfirm={() => confirmVoucherAction('delete')}
      />

      <ConfirmRequiredDialog {...confirmDialogProps} />
    </div>
  );

  function confirmVoucherAction(kind: VoucherActionKind) {
    setConfirmKind(null);
    void runAction({ kind, version: form.getValues('version'), flags: {} });
  }
}

// Read-only list of every settlement of the voucher's own entry (any origin).
function StoredAllocations({ rows }: { rows: CashVoucherAllocation[] }) {
  if (rows.length === 0) {
    return <div className="text-sm text-muted-foreground">Chưa đối trừ chứng từ nào.</div>;
  }
  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b text-left text-muted-foreground">
            <th className="px-2 py-1 font-medium">Số CT</th>
            <th className="px-2 py-1 font-medium">Ngày CT</th>
            <th className="px-2 py-1 font-medium">Nguồn</th>
            <th className="px-2 py-1 text-right font-medium">Số tiền</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <tr key={row.settlementId} className="border-b last:border-0">
              <td className="px-2 py-1">{row.docCode}</td>
              <td className="px-2 py-1">{formatDateOnly(row.docDate)}</td>
              <td className="px-2 py-1">
                {row.origin === 'AtVoucher' && !row.isOwn ? 'Tại phiếu khác' : SETTLEMENT_ORIGIN_LABELS[row.origin]}
              </td>
              <td className="px-2 py-1 text-right tabular-nums">{moneyFormat.format(row.amount)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function partnerOf(voucher?: CashVoucher): CashVoucherSelectedPartner | null {
  if (!voucher?.partnerId) return null;
  return { id: voucher.partnerId, code: voucher.partnerCode ?? '', name: voucher.partnerName ?? '' };
}

// Role flags are known only for a partner picked in this session; otherwise the partner is kept
// only while the required role stays the same.
function partnerFits(
  partner: CashVoucherSelectedPartner,
  nextType: PartnerType | undefined,
  previousType: PartnerType | undefined,
): boolean {
  if (nextType !== 'Customer' && nextType !== 'Supplier') return true;
  const role = nextType === 'Customer' ? partner.isCustomer : partner.isSupplier;
  return role ?? nextType === previousType;
}

function sameAllocations(a: CashAllocationFormValues[], b: CashAllocationFormValues[]): boolean {
  return a.length === b.length && a.every((row, i) => row.entryId === b[i].entryId && toNumberOrZero(row.amount) === b[i].amount);
}

