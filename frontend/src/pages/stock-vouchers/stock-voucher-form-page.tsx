import { useEffect, useRef, useState } from 'react';
import { useForm, useWatch, type FieldPath, type Resolver } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { ArrowLeft, Ban, RotateCcw, Save, Trash2, X } from 'lucide-react';
import {
  useCancelStockVoucher,
  useCreateStockVoucher,
  useDeleteStockVoucher,
  useRestoreStockVoucher,
  useStockAt,
  useStockVoucher,
  useStockVoucherActivities,
  useStockVoucherDefaults,
  useUpdateStockVoucher,
} from '@/features/stock-vouchers/hooks';
import {
  stockVoucherSchema,
  type StockLineFormValues,
  type StockVoucherFormParsed,
  type StockVoucherFormValues,
} from '@/features/stock-vouchers/schema';
import { toFormDefaults, toUpsertPayload } from '@/features/stock-vouchers/payload';
import {
  readStockVoucherDraft,
  useStockVoucherDraft,
  type StockVoucherDraftPartner,
  type StockVoucherDraftStorage,
} from '@/features/stock-vouchers/use-stock-voucher-draft';
import type {
  PartnerSearchItem,
  StockAtItem,
  StockDirection,
  StockVoucher,
  StockVoucherDefaults,
  UpsertStockVoucherRequest,
} from '@/features/stock-vouchers/types';
import { useWarehouses } from '@/features/warehouses/hooks';
import { useStockReasons } from '@/features/stock-reasons/hooks';
import { usePaymentMethods } from '@/features/payment-methods/hooks';
import { useInventorySettings } from '@/features/inventory-settings/hooks';
import { PartnerAutocomplete } from '@/components/partner-autocomplete/partner-autocomplete';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { ButtonLoader } from '@/components/ui/button-loader';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { formatApiErrorDetails, getApiError, getErrorMessage } from '@/lib/api-client';
import { toast } from '@/lib/use-toast';
import { useDebouncedValue } from '@/lib/use-debounced-value';
import { fromDateTimeLocalValue } from '@/lib/vn-datetime';
import { useAuthStore } from '@/stores/auth-store';
import { useBranchStore } from '@/stores/branch-store';
import { StockLineGrid } from './components/stock-line-grid';
import { StockTotalsPanel } from './components/stock-totals-panel';
import { NegativeStockDialog, toShortages, type NegativeStockShortage } from './components/negative-stock-dialog';
import { StockVoucherActivityHistory } from './components/stock-voucher-activity-history';
import { computeStockVoucher, type StockLineLike } from './utils/compute-stock-line';
import { STOCK_VOUCHER_LABELS, type StockVoucherLabels } from './labels';

type SubmitIntent = 'save-stay' | 'save-exit' | 'update';
type VoucherActionKind = 'cancel' | 'restore' | 'delete';
type PendingAction =
  | { kind: 'save'; intent: SubmitIntent; payload: UpsertStockVoucherRequest }
  | { kind: VoucherActionKind; version?: number };

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

const LINE_ERROR_FIELDS = new Set<string>([
  'productId',
  'warehouseId',
  'sheetCount',
  'length',
  'width',
  'thickness',
  'quantity',
  'unitPrice',
  'discountRate',
  'discountAmount',
  'vatRate',
  'note',
]);

const SELECT_CLASS =
  'h-7 w-full rounded-md border border-input bg-background px-2 text-sm disabled:cursor-not-allowed disabled:opacity-50';

export function StockVoucherFormPage({ type }: { type: StockDirection }) {
  const { id } = useParams<{ id: string }>();
  const isEdit = !!id && id !== 'new';
  const voucherQuery = useStockVoucher(isEdit ? id : undefined);
  // Only this first defaults load (server "now") gates the form mount.
  const defaultsQuery = useStockVoucherDefaults(type, undefined, { enabled: !isEdit });

  if (isEdit && voucherQuery.isLoading) {
    return <div className="text-sm text-muted-foreground">Đang tải...</div>;
  }
  if (isEdit && !voucherQuery.data) {
    return <div className="text-sm text-destructive">Không tìm thấy phiếu.</div>;
  }
  if (!isEdit && (!defaultsQuery.data || defaultsQuery.isPlaceholderData) && !defaultsQuery.isError) {
    return <div className="text-sm text-muted-foreground">Đang tải...</div>;
  }

  return (
    <StockVoucherForm
      key={`${type}:${isEdit ? id : 'new'}`}
      type={type}
      id={isEdit ? id : undefined}
      voucher={isEdit ? voucherQuery.data : undefined}
      defaults={isEdit ? undefined : defaultsQuery.data}
      refetchVoucher={async () => (await voucherQuery.refetch()).data}
    />
  );
}

interface FormProps {
  type: StockDirection;
  id?: string;
  voucher?: StockVoucher;
  defaults?: StockVoucherDefaults;
  refetchVoucher: () => Promise<StockVoucher | undefined>;
}

function StockVoucherForm({ type, id, voucher, defaults, refetchVoucher }: FormProps) {
  const isEdit = !!id;
  const labels: StockVoucherLabels = STOCK_VOUCHER_LABELS[type];
  const navigate = useNavigate();

  const create = useCreateStockVoucher();
  const update = useUpdateStockVoucher();
  const cancel = useCancelStockVoucher();
  const restore = useRestoreStockVoucher();
  const remove = useDeleteStockVoucher();

  // Draft key parts are read once: a draft belongs to the user and branch it was typed in.
  const [mountData] = useState<{ draft: StockVoucherDraftStorage | null; userId: string; branchId: string }>(() => {
    const userId = useAuthStore.getState().user?.id ?? '';
    const branchId = useBranchStore.getState().workingBranchId ?? '';
    return { draft: isEdit ? null : readStockVoucherDraft(type, userId, branchId), userId, branchId };
  });

  const form = useForm<StockVoucherFormValues, unknown, StockVoucherFormParsed>({
    resolver: zodResolver(stockVoucherSchema) as unknown as Resolver<
      StockVoucherFormValues,
      unknown,
      StockVoucherFormParsed
    >,
    defaultValues: mountData.draft?.values ?? toFormDefaults(voucher, defaults),
  });

  const [selectedPartner, setSelectedPartner] = useState<StockVoucherDraftPartner | null>(
    () => mountData.draft?.selectedPartner ?? partnerOf(voucher),
  );
  const [busy, setBusy] = useState(false);
  const [confirmKind, setConfirmKind] = useState<VoucherActionKind | null>(null);
  const [negative, setNegative] = useState<{
    action: PendingAction;
    blocked: boolean;
    shortages: NegativeStockShortage[];
  } | null>(null);

  const { hasDraft, draftSavedAt, clearDraft } = useStockVoucherDraft({
    form,
    type,
    userId: mountData.userId,
    branchId: mountData.branchId,
    isEdit,
    getSelectedPartner: () => selectedPartner,
    initialHasDraft: !!mountData.draft,
    initialSavedAt: mountData.draft ? new Date(mountData.draft.savedAt) : null,
  });

  const status = voucher?.status ?? 'Active';
  const readOnly = isEdit && (status === 'Cancelled' || !voucher?.canEdit);

  // A background refetch resets the form only when it is not dirty (D29).
  const lastVersionRef = useRef(voucher?.version);
  const isDirty = form.formState.isDirty;
  useEffect(() => {
    if (!voucher || voucher.version === lastVersionRef.current) return;
    lastVersionRef.current = voucher.version;
    if (isDirty) return;
    form.reset(toFormDefaults(voucher));
    setSelectedPartner(partnerOf(voucher));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [voucher?.version]);

  const { data: warehouses = [] } = useWarehouses();
  const { data: allReasons = [] } = useStockReasons({ direction: type });
  const reasons = allReasons.filter((r) => r.direction === type);
  const { data: paymentMethods = [] } = usePaymentMethods();
  const { data: settings } = useInventorySettings();

  const watchedLines = (useWatch({ control: form.control, name: 'lines' }) ?? []) as StockLineFormValues[];
  const freight = toNum(useWatch({ control: form.control, name: 'freight' })) ?? 0;
  const orderDiscount = toNum(useWatch({ control: form.control, name: 'orderDiscount' })) ?? 0;
  const paidAmount = toNum(useWatch({ control: form.control, name: 'paidAmount' }));
  const paidAmountTouched = useWatch({ control: form.control, name: 'paidAmountTouched' });
  const voucherAt = useWatch({ control: form.control, name: 'voucherAt' });
  const warehouseId = useWatch({ control: form.control, name: 'warehouseId' });
  const reasonId = useWatch({ control: form.control, name: 'reasonId' });
  const selectedReason = reasons.find((r) => r.id === reasonId);

  const computed = computeStockVoucher(
    { direction: type, freight, orderDiscount, netExcludesVat: settings?.netExcludesVat ?? false },
    watchedLines.map(toStockLineLike),
  );
  const total = computed.totals.total;

  // paidAmount follows the total until the user edits it.
  useEffect(() => {
    if (paidAmountTouched || paidAmount === total) return;
    form.setValue('paidAmount', total);
  }, [form, paidAmountTouched, paidAmount, total]);

  // Expected number for the chosen date; never resets or unmounts the form (review m5).
  const initialVoucherAtRef = useRef(voucherAt);
  const debouncedVoucherAt = useDebouncedValue(voucherAt, 400);
  const datedDefaults = useStockVoucherDefaults(type, debouncedVoucherAt, {
    enabled: !isEdit && !!debouncedVoucherAt && debouncedVoucherAt !== initialVoucherAtRef.current,
  });
  const expectedCode = datedDefaults.data?.nextCode ?? defaults?.nextCode;

  const atIso = toIsoOrEmpty(voucherAt);
  const stockAtItems = atIso ? distinctTrackedPairs(watchedLines) : [];
  const stockAtQuery = useStockAt({ type, at: atIso, excludeVoucherId: id, items: stockAtItems });
  const stockAt: Record<string, number> = {};
  for (const row of stockAtQuery.data ?? []) stockAt[`${row.productId}:${row.warehouseId}`] = row.quantity;

  const usedWarehouseIds = new Set([warehouseId, ...watchedLines.map((l) => l.warehouseId)]);
  const headerWarehouses = warehouses.filter((w) => w.isActive || w.id === warehouseId);
  const gridWarehouses = warehouses.filter((w) => w.isActive || usedWarehouseIds.has(w.id));

  const activitiesQuery = useStockVoucherActivities(id, isEdit);

  function resetTo(fresh: StockVoucher) {
    lastVersionRef.current = fresh.version;
    form.reset(toFormDefaults(fresh));
    setSelectedPartner(partnerOf(fresh));
  }

  async function runAction(action: PendingAction, acknowledgeNegativeStock: boolean) {
    setBusy(true);
    try {
      if (action.kind === 'save') {
        const payload = { ...action.payload, acknowledgeNegativeStock };
        if (id) {
          const saved = await update.mutateAsync({ id, data: payload });
          resetTo(saved);
          toast({ variant: 'success', title: 'Đã cập nhật phiếu', description: saved.code });
        } else {
          const saved = await create.mutateAsync(payload);
          clearDraft();
          toast({ variant: 'success', title: `Đã tạo ${labels.title.toLowerCase()}`, description: saved.code });
          if (action.intent === 'save-exit') navigate(labels.basePath);
          else navigate(`${labels.basePath}/${saved.id}`, { replace: true });
        }
      } else if (id) {
        const body = { version: action.version, acknowledgeNegativeStock };
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
    } catch (err) {
      await handleError(err, action);
    } finally {
      setBusy(false);
    }
  }

  async function handleError(err: unknown, action: PendingAction) {
    const apiError = getApiError(err);
    switch (apiError?.code) {
      case 'NEGATIVE_STOCK_WARNING':
      case 'NEGATIVE_STOCK_BLOCKED':
        setNegative({
          action,
          blocked: apiError.code === 'NEGATIVE_STOCK_BLOCKED',
          shortages: toShortages(apiError.details),
        });
        return;
      case 'CONCURRENCY': {
        // D29: reload and drop local edits; never resend stale values with the new version.
        const fresh = await refetchVoucher().catch(() => undefined);
        if (fresh) resetTo(fresh);
        toast({ variant: 'destructive', title: CONCURRENCY_MESSAGE });
        return;
      }
      case 'VALIDATION':
        if (apiError.details && applyLineErrors(apiError.details)) {
          toast({ variant: 'destructive', title: ERROR_TITLES[action.kind], description: apiError.message });
          return;
        }
        break;
    }
    toast({ variant: 'destructive', title: ERROR_TITLES[action.kind], description: formatApiErrorDetails(err) });
  }

  // Maps `lines[i].field` keys onto the grid; returns true when every key was a line key.
  function applyLineErrors(details: Record<string, string[]>): boolean {
    let allMapped = true;
    for (const [key, messages] of Object.entries(details)) {
      const match = /^lines\[(\d+)\]\.(\w+)$/.exec(key);
      if (match && LINE_ERROR_FIELDS.has(match[2])) {
        const path = `lines.${match[1]}.${match[2]}` as FieldPath<StockVoucherFormValues>;
        form.setError(path, { type: 'server', message: messages[0] });
      } else {
        allMapped = false;
      }
    }
    return allMapped;
  }

  // Trailing rows the user never picked a product for are dropped instead of failing validation.
  function dropBlankLines() {
    const lines = form.getValues('lines');
    const kept = lines.filter((l) => !!l.productId);
    if (kept.length > 0 && kept.length < lines.length) form.setValue('lines', kept, { shouldDirty: true });
  }

  function submitWithIntent(intent: SubmitIntent) {
    if (busy || readOnly) return;
    dropBlankLines();
    void form.handleSubmit((parsed) =>
      runAction({ kind: 'save', intent, payload: toUpsertPayload(type, parsed) }, false),
    )();
  }

  function handleFormKeyDown(e: React.KeyboardEvent<HTMLFormElement>) {
    if (e.defaultPrevented) return;
    if (e.key === 's' && e.ctrlKey && !e.shiftKey && !e.altKey) {
      e.preventDefault();
      submitWithIntent(isEdit ? 'update' : 'save-stay');
    }
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
    setSelectedPartner({ id: p.id, code: p.code, name: p.name });
  }

  function clearPartner() {
    form.setValue('partnerId', '', { shouldDirty: true });
    form.setValue('partnerName', '', { shouldDirty: true });
    form.setValue('partnerAddress', '', { shouldDirty: true });
    form.setValue('partnerTaxCode', '', { shouldDirty: true });
    setSelectedPartner(null);
  }

  // Lines that still follow the header warehouse move with it; lines set to another warehouse stay.
  function handleWarehouseChange(nextId: string) {
    const previous = form.getValues('warehouseId');
    form.setValue('warehouseId', nextId, { shouldDirty: true, shouldValidate: form.formState.isSubmitted });
    form.getValues('lines').forEach((line, i) => {
      if (!line.warehouseId || line.warehouseId === previous) {
        form.setValue(`lines.${i}.warehouseId`, nextId, { shouldDirty: true });
      }
    });
  }

  function handleReasonChange(nextId: string) {
    form.setValue('reasonId', nextId, { shouldDirty: true, shouldValidate: form.formState.isSubmitted });
    if (reasons.find((r) => r.id === nextId)?.partnerType === 'None') clearPartner();
  }

  const errors = form.formState.errors;
  const linesError = errors.lines as { message?: string; root?: { message?: string } } | undefined;
  const linesRootMessage = linesError?.root?.message ?? linesError?.message;

  return (
    <div className="space-y-4">
      <div className="sticky top-0 z-30 -mx-4 border-b bg-background/95 px-4 py-3 shadow-sm backdrop-blur md:-mx-3 md:px-3">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div className="flex min-w-0 flex-wrap items-center gap-2">
            <Button variant="ghost" size="icon" asChild aria-label="Quay lại">
              <Link to={labels.basePath}>
                <ArrowLeft className="h-4 w-4 text-slate-500" />
              </Link>
            </Button>
            <h1 className="truncate text-xl font-bold">
              {isEdit ? `${labels.title} ${voucher?.code ?? ''}`.trim() : `Thêm ${labels.title.toLowerCase()}`}
            </h1>
            {status === 'Cancelled' && <Badge variant="destructive">Đã hủy</Badge>}
            {!isEdit && expectedCode && (
              <span className="text-sm text-muted-foreground">Số phiếu dự kiến: {expectedCode}</span>
            )}
            {!isEdit && hasDraft && (
              <div className="flex items-center gap-1 rounded-full border border-amber-300 bg-amber-50 px-2.5 py-0.5 text-xs text-amber-800">
                <span>Nháp chưa lưu{draftSavedAt ? ` từ ${formatDraftTime(draftSavedAt)}` : ''}</span>
                <button
                  type="button"
                  className="ml-0.5 hover:text-amber-900"
                  onClick={() => {
                    clearDraft();
                    form.reset(toFormDefaults(undefined, defaults));
                    setSelectedPartner(null);
                  }}
                  aria-label="Xóa nháp"
                >
                  <X className="h-3 w-3" />
                </button>
              </div>
            )}
          </div>
          <div className="flex flex-wrap justify-end gap-2">
            {!isEdit && (
              <>
                <Button type="button" variant="outline" size="sm" onClick={() => submitWithIntent('save-exit')} disabled={busy}>
                  Lưu và thoát
                </Button>
                <Button type="button" size="sm" onClick={() => submitWithIntent('save-stay')} disabled={busy}>
                  {busy ? <ButtonLoader className="mr-2" /> : <Save className="mr-2 h-4 w-4" />}
                  Lưu tạm
                </Button>
              </>
            )}
            {isEdit && !readOnly && (
              <Button type="button" size="sm" onClick={() => submitWithIntent('update')} disabled={busy}>
                {busy ? <ButtonLoader className="mr-2" /> : <Save className="mr-2 h-4 w-4" />}
                Cập nhật
              </Button>
            )}
            {isEdit && voucher?.canCancel && status === 'Active' && (
              <Button type="button" variant="outline" size="sm" onClick={() => setConfirmKind('cancel')} disabled={busy}>
                <Ban className="mr-2 h-4 w-4 text-red-600" />
                Hủy phiếu
              </Button>
            )}
            {isEdit && voucher?.canCancel && status === 'Cancelled' && (
              <Button type="button" variant="outline" size="sm" onClick={() => setConfirmKind('restore')} disabled={busy}>
                <RotateCcw className="mr-2 h-4 w-4 text-emerald-600" />
                Khôi phục
              </Button>
            )}
            {isEdit && voucher?.canDelete && status === 'Active' && (
              <Button type="button" variant="outline" size="sm" onClick={() => setConfirmKind('delete')} disabled={busy}>
                <Trash2 className="mr-2 h-4 w-4 text-red-600" />
                Xóa
              </Button>
            )}
          </div>
        </div>
      </div>

      {isEdit && status === 'Active' && readOnly && (
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
        <div className="grid items-stretch gap-4 lg:grid-cols-[1fr_320px]">
          <Card>
            <CardHeader className="flex h-9 flex-row items-center border-b bg-blue-50 p-1 px-3">
              <CardTitle>Thông tin chung</CardTitle>
            </CardHeader>
            <CardContent className="px-4 pb-3 pt-2">
              <fieldset disabled={readOnly} className="m-0 min-w-0 space-y-[6px] border-0 p-0">
                <div className="form-inline-grid form-cols-3">
                  <Label htmlFor="stock-voucher-code" className="field-label">Số phiếu</Label>
                  <Input
                    id="stock-voucher-code"
                    className="h-7 bg-muted"
                    readOnly
                    tabIndex={-1}
                    value={voucher?.code ?? ''}
                    placeholder="Tự động"
                  />
                  <Label htmlFor="stock-voucher-at" className="field-label required">{labels.dateLabel}</Label>
                  <Input id="stock-voucher-at" type="datetime-local" className="h-7" {...form.register('voucherAt')} />
                  <Label htmlFor="stock-warehouse" className="field-label required">Kho</Label>
                  <select
                    id="stock-warehouse"
                    className={SELECT_CLASS}
                    value={warehouseId}
                    onChange={(e) => handleWarehouseChange(e.target.value)}
                  >
                    <option value="">-- Chọn kho --</option>
                    {headerWarehouses.map((w) => (
                      <option key={w.id} value={w.id}>
                        {w.name}
                      </option>
                    ))}
                  </select>
                  <FieldError message={errors.voucherAt?.message ?? errors.warehouseId?.message} />
                </div>

                <div className="form-inline-grid form-cols-customer">
                  <Label htmlFor="stock-reason" className="field-label required">{labels.reasonLabel}</Label>
                  <select
                    id="stock-reason"
                    className={SELECT_CLASS}
                    value={reasonId}
                    onChange={(e) => handleReasonChange(e.target.value)}
                  >
                    <option value="">-- Chọn lý do --</option>
                    {reasons.map((r) => (
                      <option key={r.id} value={r.id}>
                        {r.name}
                      </option>
                    ))}
                  </select>
                  <Label htmlFor="stock-partner" className="field-label">Đối tượng</Label>
                  <PartnerAutocomplete
                    inputId="stock-partner"
                    type={type}
                    reasonId={reasonId || undefined}
                    partnerType={selectedReason?.partnerType}
                    value={selectedPartner}
                    onSelect={handleSelectPartner}
                    disabled={readOnly}
                  />
                  <FieldError message={errors.reasonId?.message} />
                </div>

                <div className="form-inline-grid form-cols-2">
                  <Label htmlFor="stock-partner-name" className="field-label">Tên đối tượng</Label>
                  <Input id="stock-partner-name" className="h-7" {...form.register('partnerName')} />
                  <Label htmlFor="stock-partner-tax-code" className="field-label">MST</Label>
                  <Input id="stock-partner-tax-code" className="h-7" {...form.register('partnerTaxCode')} />
                </div>

                <div className="form-inline-grid">
                  <Label htmlFor="stock-partner-address" className="field-label">Địa chỉ</Label>
                  <Input id="stock-partner-address" className="h-7" {...form.register('partnerAddress')} />
                </div>

                <div className="form-inline-grid form-cols-2">
                  <Label htmlFor="stock-handler-name" className="field-label">{labels.handlerLabel}</Label>
                  <Input id="stock-handler-name" className="h-7" {...form.register('handlerName')} />
                  <Label htmlFor="stock-payment-method" className="field-label">HTTT</Label>
                  <select id="stock-payment-method" className={SELECT_CLASS} {...form.register('paymentMethodId')}>
                    <option value="">-- Hình thức thanh toán --</option>
                    {paymentMethods.map((m) => (
                      <option key={m.id} value={m.id}>
                        {m.name}
                      </option>
                    ))}
                  </select>
                </div>

                <div className="form-inline-grid">
                  <Label htmlFor="stock-voucher-note" className="field-label">Ghi chú</Label>
                  <Input id="stock-voucher-note" className="h-7" {...form.register('note')} />
                </div>
              </fieldset>
            </CardContent>
          </Card>

          <StockTotalsPanel
            type={type}
            totals={computed.totals}
            freight={freight}
            orderDiscount={orderDiscount}
            paidAmount={paidAmountTouched ? (paidAmount ?? 0) : total}
            onFreightChange={(v) => form.setValue('freight', v, { shouldDirty: true })}
            onOrderDiscountChange={(v) => form.setValue('orderDiscount', v, { shouldDirty: true })}
            onPaidAmountChange={(v) => {
              form.setValue('paidAmount', v, { shouldDirty: true });
              form.setValue('paidAmountTouched', true, { shouldDirty: true });
            }}
            onApplyVatToAll={
              type === 'In'
                ? (rate) =>
                    form.getValues('lines').forEach((_, i) =>
                      form.setValue(`lines.${i}.vatRate`, rate, { shouldDirty: true }),
                    )
                : undefined
            }
            readOnly={readOnly}
          />
        </div>

        <Card>
          <CardContent className="p-0">
            <StockLineGrid
              form={form}
              type={type}
              warehouses={gridWarehouses}
              computed={computed.lines}
              stockAt={stockAt}
              readOnly={readOnly}
            />
            {linesRootMessage && <p className="px-6 pb-4 pt-2 text-sm text-destructive">{linesRootMessage}</p>}
          </CardContent>
        </Card>
      </form>

      {isEdit && (
        <Card>
          <CardHeader className="flex h-9 flex-row items-center border-b bg-blue-50 p-1 px-3">
            <CardTitle>Lịch sử</CardTitle>
          </CardHeader>
          <CardContent className="pt-3">
            <StockVoucherActivityHistory
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
        description={`Phiếu ${voucher?.code ?? ''} sẽ bị hủy và không còn tính vào tồn kho.`}
        confirmLabel="Hủy phiếu"
        cancelLabel="Quay lại"
        destructive
        onConfirm={() => confirmVoucherAction('cancel')}
      />
      <ConfirmDialog
        open={confirmKind === 'restore'}
        onOpenChange={(open) => !open && setConfirmKind(null)}
        title="Khôi phục phiếu?"
        description={`Phiếu ${voucher?.code ?? ''} sẽ được tính lại vào tồn kho.`}
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

      <NegativeStockDialog
        open={!!negative}
        blocked={negative?.blocked ?? false}
        shortages={negative?.shortages ?? []}
        confirmLabel={negative ? CONFIRM_LABELS[negative.action.kind] : undefined}
        onConfirm={() => {
          if (!negative) return;
          const { action } = negative;
          setNegative(null);
          void runAction(action, true);
        }}
        onClose={() => setNegative(null)}
      />
    </div>
  );

  function confirmVoucherAction(kind: VoucherActionKind) {
    setConfirmKind(null);
    void runAction({ kind, version: form.getValues('version') }, false);
  }
}

function FieldError({ message }: { message?: string }) {
  if (!message) return null;
  return <p className="field-message field-message-code text-destructive">{message}</p>;
}

function partnerOf(voucher?: StockVoucher): StockVoucherDraftPartner | null {
  if (!voucher?.partnerId) return null;
  return { id: voucher.partnerId, code: voucher.partnerCode ?? '', name: voucher.partnerName ?? '' };
}

function toNum(v: unknown): number | undefined {
  if (v === undefined || v === null || v === '') return undefined;
  const n = typeof v === 'number' ? v : Number(v);
  return Number.isFinite(n) ? n : undefined;
}

function toStockLineLike(line: StockLineFormValues): StockLineLike {
  return {
    trackInventory: line.trackInventory,
    pricingMode: line.pricingMode,
    priceIncludesVat: line.priceIncludesVat,
    sheetCount: toNum(line.sheetCount),
    length: toNum(line.length),
    width: toNum(line.width),
    thickness: toNum(line.thickness),
    quantity: toNum(line.quantity) ?? 0,
    unitPrice: toNum(line.unitPrice) ?? 0,
    discountRate: toNum(line.discountRate) ?? 0,
    discountAmount: toNum(line.discountAmount),
    discountManual: line.discountManual,
    vatRate: toNum(line.vatRate) ?? 0,
  };
}

function toIsoOrEmpty(local: string | undefined): string {
  if (!local) return '';
  const date = new Date(local);
  return Number.isNaN(date.getTime()) ? '' : fromDateTimeLocalValue(local);
}

// Only tracked products have stock; 'new' rows without a product or warehouse are skipped.
function distinctTrackedPairs(lines: StockLineFormValues[]): StockAtItem[] {
  const seen = new Set<string>();
  const items: StockAtItem[] = [];
  for (const line of lines) {
    if (!line.trackInventory || !line.productId || !line.warehouseId) continue;
    const key = `${line.productId}:${line.warehouseId}`;
    if (seen.has(key)) continue;
    seen.add(key);
    items.push({ productId: line.productId, warehouseId: line.warehouseId });
  }
  return items;
}

function formatDraftTime(date: Date): string {
  return date.toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' });
}
