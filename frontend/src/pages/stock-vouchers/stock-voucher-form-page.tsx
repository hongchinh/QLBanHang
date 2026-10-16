import { useEffect, useRef, useState } from 'react';
import { useForm, useWatch, type FieldErrors, type FieldPath, type Resolver } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Link, Navigate, useNavigate, useParams } from 'react-router-dom';
import { addDays, format } from 'date-fns';
import { AppIcon } from '@/components/icons';
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
import { createEmptyStockLine, toFormDefaults, toUpsertPayload } from '@/features/stock-vouchers/payload';
import {
  readStockVoucherDraft,
  useStockVoucherDraft,
  type StockVoucherDraftPartner,
  type StockVoucherDraftStorage,
} from '@/features/stock-vouchers/use-stock-voucher-draft';
import type { FundKind } from '@/features/cash-common/types';
import { matchReasonForPartner, reasonSwitchMessage } from '@/features/cash-common/reason-match';
import { noReasonFits, reasonNoticeOf } from '@/features/cash-common/reason-notice';
import { useReasonSwitchNotice } from '@/features/cash-common/use-reason-notice';
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
import { useFunds } from '@/features/funds/hooks';
import { defaultFund, selectableFunds } from '@/features/funds/utils';
import { useInventorySettings } from '@/features/inventory-settings/hooks';
import { ActivityHistory } from '@/components/activity-history/activity-history';
import { ConfirmRequiredDialog } from '@/components/confirm-required-dialog/confirm-required-dialog';
import { PaymentQrButton } from '@/components/payment-qr/payment-qr-button';
import { useUnpickedPartnerGuard } from '@/components/partner-autocomplete/use-unpicked-partner-guard';
import { VoucherPrintButton } from '@/components/voucher-print/voucher-print-button';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { ButtonLoader } from '@/components/ui/button-loader';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { useDocumentActions } from '@/hooks/useDocumentActions';
import { getErrorMessage } from '@/lib/api-client';
import { DIALOG_SELECTOR, PAGE_AREA_SELECTOR } from '@/lib/focus';
import type { ConfirmFlags } from '@/lib/confirm-required';
import { isDimensionField, usesDimension } from '@/lib/dimension-columns';
import { toOptionalNumber } from '@/lib/to-number';
import { toast } from '@/lib/use-toast';
import { useDebouncedValue } from '@/lib/use-debounced-value';
import { fromDateTimeLocalValue, toDateTimeLocalValue } from '@/lib/vn-datetime';
import { useAuthStore } from '@/stores/auth-store';
import { useBranchStore } from '@/stores/branch-store';
import { PartnerDebtStrip } from './components/partner-debt-strip';
import { StockLineGrid } from './components/stock-line-grid';
import { StockTotalsBar, type TotalsJumpField } from './components/stock-totals-bar';
import { StockVoucherMainFields, FieldError, SELECT_CLASS } from './components/stock-voucher-main-fields';
import { StockVoucherPaymentTab } from './components/stock-voucher-payment-tab';
import { StockVoucherTabs } from './components/stock-voucher-tabs';
import { savedImpactOf } from './utils/debt-after-voucher';
import {
  HEADER_ERROR_FIELDS,
  LINE_ERROR_FIELDS,
  QUANTITY_SOURCES,
  staleServerErrorKeys,
} from './utils/server-field-errors';
import { errorTabs, firstErrorTab, tabOfField, type StockVoucherTab } from './utils/voucher-tabs';
import { computeStockVoucher, type StockLineLike } from './utils/compute-stock-line';
import { STOCK_VOUCHER_LABELS, type StockVoucherLabels } from './labels';

type SubmitIntent = 'save-stay' | 'save-exit' | 'update';
type VoucherActionKind = 'cancel' | 'restore' | 'delete';
// `flags` accumulates the confirmations (P10) the user already accepted; every resend carries all of them.
type PendingAction =
  | { kind: 'save'; intent: SubmitIntent; payload: UpsertStockVoucherRequest; flags: ConfirmFlags }
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

const ALT_TABS: Record<string, StockVoucherTab> = { '1': 'lines', '2': 'other', '3': 'payment', '4': 'history' };

// What the shortcuts read of a key event, React's or the DOM's.
type ShortcutKeyEvent = Pick<
  KeyboardEvent,
  'key' | 'ctrlKey' | 'shiftKey' | 'altKey' | 'metaKey' | 'repeat' | 'target' | 'preventDefault'
>;

// Ctrl+S saves (with Caps Lock too, not with Shift); Alt+1..4 switch tabs, Alt+4 only on a saved voucher.
function shortcutOf(e: ShortcutKeyEvent, isEdit: boolean): 'save' | StockVoucherTab | undefined {
  // Chrome sends a keydown with no key when an autofill entry is picked; React's handler sees 'Unidentified'.
  const key: string = typeof e.key === 'string' ? e.key : '';
  if (key.toLowerCase() === 's' && e.ctrlKey && !e.shiftKey && !e.altKey) return 'save';
  const altTab = e.altKey && !e.ctrlKey && !e.shiftKey && !e.metaKey ? ALT_TABS[key] : undefined;
  return altTab === 'history' && !isEdit ? undefined : altTab;
}

// Radix wraps the content of a popover in it. The grid's popovers are role="dialog" too, but they do not block the page.
const POPOVER = '[data-radix-popper-content-wrapper]';

// A modal dialog owns its keys, also one that sits inside the form in the React tree (the product catalog of a Mã
// cell), whose keys reach the form's handler.
function inModalDialog(target: EventTarget | null): boolean {
  const dialog = target instanceof Element ? target.closest(DIALOG_SELECTOR) : null;
  return !!dialog && !dialog.closest(POPOVER);
}

const JUMP_IDS: Record<TotalsJumpField, string> = {
  freight: 'stock-freight',
  orderDiscount: 'stock-order-discount',
  paidAmount: 'stock-paid-amount',
};

// Form key -> element id for the fields that live in a tab or in the main area.
const FIELD_FOCUS_IDS: Record<string, string> = {
  partnerId: 'stock-partner',
  partnerTaxCode: 'stock-partner-tax-code',
  handlerName: 'stock-handler-name',
  note: 'stock-voucher-note',
  freight: 'stock-freight',
  orderDiscount: 'stock-order-discount',
  paidAmount: 'stock-paid-amount',
  fundId: 'stock-fund',
  dueDate: 'stock-due-date',
};
// Line form field -> cell id part; cell ids are `stock-line-<cell>-<row>` (`cellId` in stock-line-grid.tsx).
const LINE_CELL_IDS: Record<string, string> = {
  productId: 'product-code',
  warehouseId: 'warehouse',
  sheetCount: 'sheet-count',
  length: 'length',
  width: 'width',
  thickness: 'thickness',
  quantity: 'quantity',
  unitPrice: 'unit-price',
  discountRate: 'discount-rate',
  discountAmount: 'discount-amount',
  vatRate: 'vat-rate',
  note: 'note',
};

// Flattens react-hook-form errors into keys (`note`, `lines.2.width`); a non-array `lines` error (min 1 line) stays `lines`.
function errorKeysOf(errors: FieldErrors<StockVoucherFormValues>): string[] {
  const keys: string[] = [];
  for (const [key, value] of Object.entries(errors)) {
    if (key === 'lines' && Array.isArray(value)) {
      value.forEach((row, i) => {
        for (const field of Object.keys(row ?? {})) keys.push(`lines.${i}.${field}`);
      });
    } else {
      keys.push(key);
    }
  }
  return keys;
}

// The ids of the controls that correct the error of a key, best first. Accepts form keys and server keys
// (`lines[3].width`). Given the lines, a dimension that the line's mode does not use maps to the line's Mã hàng: its
// cell is a disabled "—", and only a re-pick fixes the line.
function focusIdsOf(key: string, lines?: readonly { pricingMode?: unknown }[]): string[] {
  const line = /^lines(?:\.|\[)(\d+)(?:\]\.|\.)(\w+)$/.exec(key);
  if (line) {
    if (lines && unusedDimensionCellId(key, lines)) return [`stock-line-product-code-${line[1]}`];
    // SL of a line priced by size is computed, and disabled: the cells it is computed from correct its error.
    const fields = line[2] === 'quantity' ? ['quantity', ...QUANTITY_SOURCES] : [line[2]];
    return fields.filter((field) => LINE_CELL_IDS[field]).map((field) => `stock-line-${LINE_CELL_IDS[field]}-${line[1]}`);
  }
  const id = key === 'lines' ? 'stock-line-product-code-0' : FIELD_FOCUS_IDS[key];
  return id ? [id] : [];
}

// The "—" cell of a dimension error on a line whose mode does not use that dimension, or null.
function unusedDimensionCellId(key: string, lines: readonly { pricingMode?: unknown }[]): string | null {
  const line = /^lines(?:\.|\[)(\d+)(?:\]\.|\.)(\w+)$/.exec(key);
  const cell = line ? LINE_CELL_IDS[line[2]] : undefined;
  if (!line || !cell || !isDimensionField(cell) || usesDimension(lines[Number(line[1])]?.pricingMode, cell)) return null;
  return `stock-line-${cell}-${line[1]}`;
}

function productCodeCell(row: number): HTMLElement | null {
  return document.getElementById(`stock-line-product-code-${row}`);
}

// A focused cell must not go down with its row: the focus would fall on <body>, where the grid keys do not reach,
// and after a 422 dialog there would be nothing to give it back to. Call before rows without a product are dropped,
// while at least one row stays: a focus inside a row that goes moves to the product code of the last row that stays.
function moveFocusOffBlankRows(lines: readonly StockLineFormValues[]) {
  const active = document.activeElement;
  const inBlankRow = lines.some(
    (line, row) => !line.productId && !!productCodeCell(row)?.closest('tr')?.contains(active),
  );
  if (!inBlankRow) return;
  const lastKept = lines.reduce((last, line, row) => (line.productId ? row : last), -1);
  productCodeCell(lastKept)?.focus();
}

export function StockVoucherFormPage({ type }: { type: StockDirection }) {
  const { id } = useParams<{ id: string }>();
  const isEdit = !!id && id !== 'new';
  const voucherQuery = useStockVoucher(isEdit ? id : undefined);
  // Only this first defaults load (server "now") gates the form mount. It is always fetched on
  // mount and never cached, so a new voucher cannot start from a stale "now".
  const defaultsQuery = useStockVoucherDefaults(type, undefined, { enabled: !isEdit, fresh: true });

  if (isEdit && voucherQuery.isLoading) {
    return <div className="text-sm text-muted-foreground">Đang tải...</div>;
  }
  if (isEdit && !voucherQuery.data) {
    return <div className="text-sm text-destructive">Không tìm thấy phiếu.</div>;
  }
  // /stock-in/<id of a stock-out> (or the reverse) opens the voucher under its own route.
  if (isEdit && voucherQuery.data && voucherQuery.data.type !== type) {
    return <Navigate to={`${STOCK_VOUCHER_LABELS[voucherQuery.data.type].basePath}/${id}`} replace />;
  }
  if (!isEdit && !defaultsQuery.isFetchedAfterMount) {
    return <div className="text-sm text-muted-foreground">Đang tải...</div>;
  }

  return (
    <StockVoucherForm
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
  type: StockDirection;
  id?: string;
  voucher?: StockVoucher;
  defaults?: StockVoucherDefaults;
  refetchVoucher: () => Promise<StockVoucher | undefined>;
}

function StockVoucherForm({ type, id, voucher, defaults, refetchVoucher }: FormProps) {
  const isEdit = !!id;
  const [activeTab, setActiveTab] = useState<StockVoucherTab>('lines');
  // Slot on the tab row that hosts the grid toolbar.
  const [toolbarSlot, setToolbarSlot] = useState<HTMLElement | null>(null);
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

  const [initialValues] = useState<StockVoucherFormValues>(
    () => mountData.draft?.values ?? toFormDefaults(voucher, defaults),
  );
  const form = useForm<StockVoucherFormValues, unknown, StockVoucherFormParsed>({
    resolver: zodResolver(stockVoucherSchema) as unknown as Resolver<
      StockVoucherFormValues,
      unknown,
      StockVoucherFormParsed
    >,
    defaultValues: initialValues,
  });
  // A fund or due date that is already set counts as chosen by the user: defaults never overwrite it.
  const fundPickedRef = useRef(!!initialValues.fundId);
  const dueDateTouchedRef = useRef(!!initialValues.dueDate);

  const [selectedPartner, setSelectedPartner] = useState<StockVoucherDraftPartner | null>(
    () => mountData.draft?.selectedPartner ?? partnerOf(voucher),
  );
  const switchNotice = useReasonSwitchNotice();
  const guard = useUnpickedPartnerGuard('stock-partner');
  // `busy` is set only after async validation; the ref blocks a second submit (Ctrl+S key repeat) at once.
  const submittingRef = useRef(false);
  const [confirmKind, setConfirmKind] = useState<VoucherActionKind | null>(null);

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

  // A server error describes the value that was sent: it goes as soon as that value (or what drives it) changes.
  // Client (zod) errors are left to the resolver.
  useEffect(() => {
    const copyLines = () => form.getValues('lines').map((line) => ({ ...line }));
    // The lines before the change being handled: a product pick replaces a whole line at once.
    let previousLines = copyLines();
    const subscription = form.watch((_values, { name }) => {
      if (name) {
        for (const key of staleServerErrorKeys(name, form.getValues(), previousLines)) {
          const path = key as FieldPath<StockVoucherFormValues>;
          if (form.getFieldState(path).error?.type === 'server') form.clearErrors(path);
        }
      }
      previousLines = copyLines();
    });
    return () => subscription.unsubscribe();
  }, [form]);

  const status = voucher?.status ?? 'Active';
  const readOnly = isEdit && (status === 'Cancelled' || !voucher?.canEdit);

  // A background refetch resets the form only when it is not dirty (D29).
  const lastVersionRef = useRef(voucher?.version);
  const isDirty = form.formState.isDirty;
  useEffect(() => {
    if (!voucher || voucher.version === lastVersionRef.current) return;
    lastVersionRef.current = voucher.version;
    if (isDirty) return;
    resetTo(voucher);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [voucher?.version]);

  const { data: warehouses = [] } = useWarehouses();
  const { data: allReasons = [], isSuccess: reasonsLoaded } = useStockReasons({ direction: type });
  const reasons = allReasons.filter((r) => r.direction === type);
  const { data: paymentMethods = [] } = usePaymentMethods();
  const { data: funds } = useFunds();
  const { data: settings } = useInventorySettings();

  const watchedLines = (useWatch({ control: form.control, name: 'lines' }) ?? []) as StockLineFormValues[];
  const freight = toOptionalNumber(useWatch({ control: form.control, name: 'freight' })) ?? 0;
  const orderDiscount = toOptionalNumber(useWatch({ control: form.control, name: 'orderDiscount' })) ?? 0;
  const paidAmount = toOptionalNumber(useWatch({ control: form.control, name: 'paidAmount' }));
  const paidAmountTouched = useWatch({ control: form.control, name: 'paidAmountTouched' });
  const voucherAt = useWatch({ control: form.control, name: 'voucherAt' });
  const warehouseId = useWatch({ control: form.control, name: 'warehouseId' });
  const reasonId = useWatch({ control: form.control, name: 'reasonId' });
  const paymentMethodId = useWatch({ control: form.control, name: 'paymentMethodId' });
  const fundId = useWatch({ control: form.control, name: 'fundId' }) ?? '';
  const selectedReason = reasons.find((r) => r.id === reasonId);
  // undefined while the chosen method is not loaded yet: the paid amount and fund are then left alone.
  const methodFundKind: FundKind | undefined = paymentMethodId
    ? paymentMethods.find((m) => m.id === paymentMethodId)?.fundKind
    : 'None';
  const methodHasFund = methodFundKind === 'Cash' || methodFundKind === 'Bank';

  const computed = computeStockVoucher(
    { direction: type, freight, orderDiscount, netExcludesVat: settings?.netExcludesVat ?? false },
    watchedLines.map(toStockLineLike),
  );
  const total = computed.totals.total;

  // Until the user edits it, paidAmount follows the total for a method with a fund and is 0 otherwise (P9).
  // A read-only voucher always shows what is stored.
  const defaultPaid = readOnly || methodFundKind === undefined ? undefined : methodHasFund ? total : 0;
  // A method without a fund moves no money: the server rejects a paid amount for it, so the input is locked at 0.
  const paidLocked = !readOnly && methodFundKind !== undefined && !methodHasFund;
  useEffect(() => {
    // An edit made under another method is dropped; the amount follows the default again.
    if (paidLocked && paidAmountTouched) {
      form.setValue('paidAmountTouched', false);
      return;
    }
    if (paidAmountTouched || defaultPaid === undefined || paidAmount === defaultPaid) return;
    form.setValue('paidAmount', defaultPaid);
  }, [form, paidLocked, paidAmountTouched, paidAmount, defaultPaid]);
  const effectivePaid = (paidAmountTouched && !paidLocked) || defaultPaid === undefined ? (paidAmount ?? 0) : defaultPaid;

  // "Quỹ": only money that moves needs a fund. The branch's default fund of the method's kind is used
  // until the user picks one; a picked fund of another kind gives way to the new kind's default.
  const fundShown = effectivePaid > 0 && methodHasFund;
  const fundOptions = funds && methodHasFund ? selectableFunds(funds, methodFundKind, fundId) : [];
  useEffect(() => {
    if (readOnly || !funds || !methodHasFund) return;
    const current = funds.find((f) => f.id === fundId);
    if (fundPickedRef.current && current?.kind === methodFundKind) return;
    fundPickedRef.current = false;
    const next = defaultFund(funds, methodFundKind)?.id ?? '';
    if (next !== fundId) form.setValue('fundId', next);
  }, [form, readOnly, funds, methodHasFund, methodFundKind, fundId]);

  // "Hạn thanh toán" (P27): debt-increasing reasons only; pre-filled with voucher date + partner credit days.
  const dueDateShown =
    selectedReason?.debtEffect === 'ReceivableIncrease' || selectedReason?.debtEffect === 'PayableIncrease';
  const creditDays = selectedPartner?.creditDays;
  useEffect(() => {
    if (readOnly || dueDateTouchedRef.current || !dueDateShown) return;
    const next = creditDays == null ? '' : addDaysToLocalDate(voucherAt, creditDays);
    if (next !== (form.getValues('dueDate') ?? '')) form.setValue('dueDate', next, { shouldDirty: true });
  }, [form, readOnly, dueDateShown, creditDays, voucherAt]);

  // Expected number for the chosen date; never resets or unmounts the form (review m5).
  // `defaults` holds the number for server "now"; any other date (a restored draft's included) asks again.
  const defaultsVoucherAt = defaults ? toDateTimeLocalValue(defaults.voucherAt) : undefined;
  const debouncedVoucherAt = useDebouncedValue(voucherAt, 400);
  const usesDatedDefaults = !isEdit && !!debouncedVoucherAt && debouncedVoucherAt !== defaultsVoucherAt;
  const datedDefaults = useStockVoucherDefaults(type, debouncedVoucherAt, { enabled: usesDatedDefaults });
  const expectedCode = usesDatedDefaults ? datedDefaults.data?.nextCode : defaults?.nextCode;

  const atIso = toIsoOrEmpty(voucherAt);
  const stockAtItems = atIso ? distinctTrackedPairs(watchedLines) : [];
  const stockAtQuery = useStockAt({ type, at: atIso, excludeVoucherId: id, items: stockAtItems });
  const stockAt: Record<string, number> = {};
  for (const row of stockAtQuery.data ?? []) stockAt[`${row.productId}:${row.warehouseId}`] = row.quantity;

  const usedWarehouseIds = new Set([warehouseId, ...watchedLines.map((l) => l.warehouseId)]);
  const headerWarehouses = warehouses.filter((w) => w.isActive || w.id === warehouseId);
  const gridWarehouses = warehouses.filter((w) => w.isActive || usedWarehouseIds.has(w.id));

  const activitiesQuery = useStockVoucherActivities(id, isEdit);

  // Partner debt strip (P6): a picked partner, for debt.partner_summary or reports.debt (D9).
  const hasPermission = useAuthStore((s) => s.hasPermission);
  const canSeePartnerDebt = hasPermission('debt.partner_summary') || hasPermission('reports.debt');
  const debtStripPartnerId = canSeePartnerDebt ? selectedPartner?.id : undefined;
  // Stored impact of the voucher being edited (Active, same partner).
  const savedImpact = savedImpactOf(voucher, selectedPartner?.id);
  const lineCount = watchedLines.filter((l) => !!l.productId).length;

  function resetTo(fresh: StockVoucher) {
    lastVersionRef.current = fresh.version;
    const values = toFormDefaults(fresh);
    fundPickedRef.current = !!values.fundId;
    dueDateTouchedRef.current = !!values.dueDate;
    form.reset(values);
    setSelectedPartner(partnerOf(fresh));
    // The notice named a partner of the form before the reset.
    switchNotice.clear();
  }

  const { busy, run: runAction, confirmDialogProps } = useDocumentActions<PendingAction>({
    perform,
    reload: refetchVoucher,
    onReloaded: (fresh) => resetTo(fresh as StockVoucher),
    applyValidationErrors: applyServerErrors,
    errorTitles: ERROR_TITLES,
    confirmLabels: CONFIRM_LABELS,
    concurrencyMessage: CONCURRENCY_MESSAGE,
    concurrencyReloadFailedMessage: CONCURRENCY_RELOAD_FAILED_MESSAGE,
  });

  async function perform(action: PendingAction) {
    if (action.kind === 'save') {
      const payload = { ...action.payload, ...action.flags };
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
      const body = { version: action.version, ...action.flags };
      if (action.kind === 'delete') {
        await remove.mutateAsync({ id, type, body });
        toast({ variant: 'success', title: 'Đã xóa phiếu' });
        navigate(labels.basePath);
      } else {
        const saved = await (action.kind === 'cancel' ? cancel : restore).mutateAsync({ id, body });
        resetTo(saved);
        toast({ variant: 'success', title: action.kind === 'cancel' ? 'Đã hủy phiếu' : 'Đã khôi phục phiếu' });
      }
    }
  }

  // Maps `lines[i].field` keys onto the grid and the header keys onto their fields; returns true when every key was mapped.
  function applyServerErrors(details: Record<string, string[]>): boolean {
    let allMapped = true;
    const mappedKeys: string[] = [];
    for (const [key, messages] of Object.entries(details)) {
      const match = /^lines\[(\d+)\]\.(\w+)$/.exec(key);
      if (match && LINE_ERROR_FIELDS.has(match[2])) {
        const path = `lines.${match[1]}.${match[2]}` as FieldPath<StockVoucherFormValues>;
        form.setError(path, { type: 'server', message: messages[0] });
        mappedKeys.push(key);
      } else if (HEADER_ERROR_FIELDS.has(key)) {
        form.setError(key as FieldPath<StockVoucherFormValues>, { type: 'server', message: messages[0] });
        mappedKeys.push(key);
      } else {
        allMapped = false;
      }
    }
    focusFirstError(mappedKeys);
    return allMapped;
  }

  // Switches to the first tab with an error and focuses its first erroring field. A main-area field with an error
  // (the partner) is tried before any tab field; the tab is still shown so its dot and panel are visible.
  function focusFirstError(keys: string[]) {
    const tab = firstErrorTab(keys);
    if (tab) setActiveTab(tab);
    const lines = form.getValues('lines');
    const firstIdsOf = (match: (key: string) => boolean) =>
      keys.filter(match).map((key) => focusIdsOf(key, lines)).find((ids) => ids.length > 0) ?? [];
    const mainIds = firstIdsOf((k) => tabOfField(k) === null);
    const tabIds = tab ? firstIdsOf((k) => tabOfField(k) === tab) : [];
    // Mã hàng is pinned, so focusing it scrolls nothing: bring the "—" cell of a remapped error and its message into
    // view. The key is the one `tabIds` was taken from; a main-area error (the partner) skips this.
    const firstTabKey = tab ? keys.find((k) => tabOfField(k) === tab && focusIdsOf(k, lines).length > 0) : undefined;
    const revealId = mainIds.length === 0 && firstTabKey ? unusedDimensionCellId(firstTabKey, lines) : null;
    if (mainIds.length === 0 && !tab) return;
    // After the panel is shown; `form.setFocus` cannot reach these controlled inputs and grid cells.
    setTimeout(() => {
      if (revealId) document.getElementById(revealId)?.scrollIntoView({ block: 'nearest', inline: 'nearest' });
      const candidates = [
        ...[...mainIds, ...tabIds, 'stock-line-add'].map((id) => document.getElementById(id)),
        document.querySelector<HTMLElement>('[role="tab"][data-state="active"]'),
      ];
      // A control in a hidden panel, or a disabled one (the partner locked by a reason without a partner), refuses
      // focus, so take the first candidate that actually receives it.
      for (const el of candidates) {
        el?.focus();
        if (el && document.activeElement === el) return;
      }
    }, 0);
  }

  function jumpTo(field: TotalsJumpField) {
    setActiveTab('payment');
    setTimeout(() => document.getElementById(JUMP_IDS[field])?.focus(), 0);
  }

  // Trailing rows the user never picked a product for are dropped instead of failing validation.
  function dropBlankLines() {
    const lines = form.getValues('lines');
    const kept = lines.filter((l) => !!l.productId);
    if (kept.length === 0 || kept.length === lines.length) return;
    moveFocusOffBlankRows(lines);
    form.setValue('lines', kept, { shouldDirty: true });
  }

  function submitWithIntent(intent: SubmitIntent) {
    if (submittingRef.current || busy || readOnly) return;
    if (!guard.assertPicked()) return;
    submittingRef.current = true;
    dropBlankLines();
    void form
      .handleSubmit(
        (parsed) => {
        // Hidden fields are not sent: the server applies its own defaults.
        const visible = {
          ...parsed,
          fundId: fundShown ? parsed.fundId : undefined,
          dueDate: dueDateShown ? parsed.dueDate : undefined,
        };
        return runAction({ kind: 'save', intent, payload: toUpsertPayload(type, visible), flags: {} });
        },
        (invalid) => focusFirstError(errorKeysOf(invalid)),
      )()
      .finally(() => {
        submittingRef.current = false;
      });
  }

  // Takes a shortcut key (shortcutOf) from the browser and runs its shortcut, unless the key was typed in a modal
  // dialog; returns whether it took the key. A key held down repeats: the repeats are taken too but run nothing, or a
  // held Ctrl+S would save again each time the save before it ended.
  function runShortcut(e: ShortcutKeyEvent): boolean {
    const shortcut = shortcutOf(e, isEdit);
    if (!shortcut || inModalDialog(e.target)) return false;
    e.preventDefault();
    if (e.repeat) return true;
    if (shortcut === 'save') {
      submitWithIntent(isEdit ? 'update' : 'save-stay');
    } else {
      setActiveTab(shortcut);
      // The focused cell may sit in a panel that is about to be hidden; keep the focus in the form so Ctrl+S / Alt+n keep working.
      setTimeout(() => document.querySelector<HTMLElement>('[role="tab"][data-state="active"]')?.focus(), 0);
    }
    return true;
  }

  function handleFormKeyDown(e: React.KeyboardEvent<HTMLFormElement>) {
    if (e.defaultPrevented || runShortcut(e)) return;
    // D7: Enter never submits the form.
    const tag = (e.target as HTMLElement).tagName;
    if (e.key === 'Enter' && (tag === 'INPUT' || tag === 'SELECT')) e.preventDefault();
  }

  // The form hears its keys only while the focus is inside it. After a save that opened the saved voucher, after a
  // title-bar button or a dialog whose opener is gone, the focus is on <body> or in the title bar: the shortcuts are
  // taken from the document there. A key inside the form is the form's own; the app header and sidebar, and every
  // key while a dialog is open, are left alone.
  const formRef = useRef<HTMLFormElement>(null);
  // The shortcuts of the latest render: they read busy, readOnly and which fields are shown.
  const runShortcutRef = useRef(runShortcut);
  runShortcutRef.current = runShortcut;
  useEffect(() => {
    function handleDocumentKeyDown(e: KeyboardEvent) {
      // Every key pressed anywhere comes here, each one typed in the grid too: the cheap checks go first, and the
      // search of the whole page for an open dialog last.
      if (e.defaultPrevented || !shortcutOf(e, isEdit)) return;
      const target = e.target instanceof Element ? e.target : null;
      const inPageOutsideForm = !!target?.closest(PAGE_AREA_SELECTOR) && !formRef.current?.contains(target);
      if (target !== document.body && !inPageOutsideForm) return;
      if (!document.querySelector(DIALOG_SELECTOR)) runShortcutRef.current(e);
    }
    document.addEventListener('keydown', handleDocumentKeyDown);
    return () => document.removeEventListener('keydown', handleDocumentKeyDown);
  }, [isEdit]);

  // Enter from the main area: the first line without a product, or a new one.
  function enterGrid() {
    setActiveTab('lines');
    const lines = form.getValues('lines');
    let idx = lines.findIndex((l) => !l.productId);
    if (idx === -1) {
      idx = lines.length;
      form.setValue('lines', [...lines, createEmptyStockLine(idx, form.getValues('warehouseId'))], { shouldDirty: true });
    }
    setTimeout(() => productCodeCell(idx)?.focus(), 0);
  }

  function clearPartnerFields() {
    form.setValue('partnerId', '', { shouldDirty: true });
    form.setValue('partnerName', '', { shouldDirty: true });
    form.setValue('partnerAddress', '', { shouldDirty: true });
    form.setValue('partnerTaxCode', '', { shouldDirty: true });
    setSelectedPartner(null);
  }

  // A picked partner (list, quick add or catalog) fills the header and, when the reason does not fit its role,
  // switches the reason to one that does.
  function handleSelectPartner(p: PartnerSearchItem | null) {
    if (!p) {
      clearPartnerFields();
      switchNotice.clear();
      return;
    }
    form.setValue('partnerId', p.id, { shouldDirty: true });
    form.setValue('partnerName', p.name, { shouldDirty: true });
    form.setValue('partnerAddress', p.companyAddress ?? '', { shouldDirty: true });
    form.setValue('partnerTaxCode', p.taxCode ?? '', { shouldDirty: true });
    setSelectedPartner({
      id: p.id,
      code: p.code,
      name: p.name,
      isCustomer: p.isCustomer,
      isSupplier: p.isSupplier,
      creditDays: p.creditDays,
    });
    switchNotice.clear();
    const match = matchReasonForPartner(p, form.getValues('reasonId'), reasons);
    if (match.kind === 'switch') {
      form.setValue('reasonId', match.reasonId, { shouldDirty: true, shouldValidate: form.formState.isSubmitted });
      switchNotice.show(reasonSwitchMessage(p.code, p));
    }
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

  // The partner stays when the new reason needs another role (a red line says so); a reason without a partner clears it.
  function handleReasonChange(nextId: string) {
    form.setValue('reasonId', nextId, { shouldDirty: true, shouldValidate: form.formState.isSubmitted });
    switchNotice.clear();
    if (reasons.find((r) => r.id === nextId)?.partnerType === 'None') clearPartnerFields();
  }

  const errors = form.formState.errors;
  // Derived: roles are known only for a partner picked in this session (or restored from a draft).
  const reasonNotice = reasonNoticeOf(
    selectedPartner,
    selectedReason,
    switchNotice.notice,
    noReasonFits(selectedPartner, reasons, reasonsLoaded),
  );
  const linesError = errors.lines as { message?: string; root?: { message?: string } } | undefined;
  const linesRootMessage = linesError?.root?.message ?? linesError?.message;

  // Height chain: main#main-content (scrolls) > this root (h-full) > form (flex-1, no min-h-0) > Tabs (no min-h-0)
  // > lines panel (min-h-[280px]; the grid wrapper has contain:size so its rows never size the page).
  // The panel minimum therefore propagates up: with room the page fills the window and the grid scrolls
  // inside; in a short window main scrolls and the totals bar sits below the grid.
  return (
    <div className="flex h-full flex-col gap-1.5">
      <div className="sticky top-0 z-30 shrink-0 -mx-4 border-b bg-background/95 px-4 py-2 shadow-sm backdrop-blur md:-mx-3 md:px-3">
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
                    fundPickedRef.current = false;
                    dueDateTouchedRef.current = false;
                    form.reset(toFormDefaults(undefined, defaults));
                    setSelectedPartner(null);
                    switchNotice.clear();
                  }}
                  aria-label="Xóa nháp"
                >
                  <AppIcon name="close" size={12} />
                </button>
              </div>
            )}
          </div>
          <div className="flex flex-wrap justify-end gap-2">
            {isEdit && voucher && (
              <VoucherPrintButton
                kind="stock"
                type={type}
                vouchers={[{ id: voucher.id, code: voucher.code }]}
                disabled={isDirty || busy}
                disabledReason={isDirty ? 'Lưu thay đổi trước khi in' : undefined}
              />
            )}
            {isEdit && voucher && voucher.type === 'Out' && (
              <PaymentQrButton
                kind="stock"
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
                <Button type="button" variant="outline" size="sm" onClick={() => submitWithIntent('save-exit')} disabled={busy} title="Ctrl+S">
                  Lưu và thoát
                </Button>
                <Button type="button" size="sm" onClick={() => submitWithIntent('save-stay')} disabled={busy} title="Ctrl+S">
                  {busy ? <ButtonLoader className="mr-2" /> : <AppIcon name="save" tone="none" className="mr-2" />}
                  Lưu tạm
                </Button>
              </>
            )}
            {isEdit && !readOnly && (
              <Button type="button" size="sm" onClick={() => submitWithIntent('update')} disabled={busy} title="Ctrl+S">
                {busy ? <ButtonLoader className="mr-2" /> : <AppIcon name="save" tone="none" className="mr-2" />}
                Cập nhật
              </Button>
            )}
            {isEdit && voucher?.canCancel && status === 'Active' && (
              <Button type="button" variant="outline" size="sm" onClick={() => setConfirmKind('cancel')} disabled={busy}>
                <AppIcon name="cancelVoucher" className="mr-2" />
                Hủy phiếu
              </Button>
            )}
            {isEdit && voucher?.canCancel && status === 'Cancelled' && (
              <Button type="button" variant="outline" size="sm" onClick={() => setConfirmKind('restore')} disabled={busy}>
                <AppIcon name="restore" className="mr-2" />
                Khôi phục
              </Button>
            )}
            {isEdit && voucher?.canDelete && status === 'Active' && (
              <Button type="button" variant="outline" size="sm" onClick={() => setConfirmKind('delete')} disabled={busy}>
                <AppIcon name="delete" className="mr-2" />
                Xóa
              </Button>
            )}
          </div>
        </div>
      </div>

      {isEdit && status === 'Active' && readOnly && (
        <div className="shrink-0 rounded-md border border-amber-300 bg-amber-50 p-3 text-sm text-amber-900">
          Bạn không có quyền sửa phiếu này.
        </div>
      )}

      {/* eslint-disable-next-line jsx-a11y/no-noninteractive-element-interactions -- Form-level Ctrl+S shortcut delegates to the submit handler. */}
      <form
        ref={formRef}
        onSubmit={(e) => {
          e.preventDefault();
          submitWithIntent(isEdit ? 'update' : 'save-stay');
        }}
        onKeyDown={handleFormKeyDown}
        className="flex flex-1 flex-col gap-1.5"
      >
        <StockVoucherMainFields
          form={form}
          type={type}
          labels={labels}
          readOnly={readOnly}
          warehouses={headerWarehouses}
          reasons={reasons}
          paymentMethods={paymentMethods}
          warehouseId={warehouseId}
          reasonId={reasonId}
          paymentMethodId={paymentMethodId}
          selectedReason={selectedReason}
          selectedPartner={selectedPartner}
          onWarehouseChange={handleWarehouseChange}
          onReasonChange={handleReasonChange}
          onSelectPartner={handleSelectPartner}
          reasonNotice={reasonNotice}
          reasonFlash={switchNotice.flash}
          partnerError={guard.error ?? errors.partnerId?.message}
          onUnpickedPartnerChange={guard.onUnpickedChange}
          onEnterGrid={enterGrid}
        />

        <StockVoucherTabs
          value={activeTab}
          onValueChange={setActiveTab}
          lineCount={lineCount}
          toolbarRef={setToolbarSlot}
          errorTabs={errorTabs(errorKeysOf(errors))}
          lines={
            <>
              <StockLineGrid
                form={form}
                type={type}
                warehouses={gridWarehouses}
                computed={computed.lines}
                stockAt={stockAt}
                readOnly={readOnly}
                toolbarSlot={toolbarSlot}
              />
              {linesRootMessage && <p className="shrink-0 px-6 pb-4 pt-2 text-sm text-destructive">{linesRootMessage}</p>}
            </>
          }
          other={
            <fieldset disabled={readOnly} className="m-0 min-w-0 space-y-[6px] border-0 p-0">
              <div className="form-inline-grid form-cols-2 p-3">
                <Label htmlFor="stock-partner-tax-code" className="field-label">MST</Label>
                <Input id="stock-partner-tax-code" className="h-7" {...form.register('partnerTaxCode')} />
                <Label htmlFor="stock-handler-name" className="field-label">{labels.handlerLabel}</Label>
                <Input id="stock-handler-name" className="h-7" {...form.register('handlerName')} />
              </div>
              <div className="form-inline-grid px-3 pb-3">
                <Label htmlFor="stock-voucher-note" className="field-label">Ghi chú</Label>
                <Input id="stock-voucher-note" className="h-7" {...form.register('note')} />
              </div>
            </fieldset>
          }
          payment={
            <StockVoucherPaymentTab
              type={type}
              freight={freight}
              orderDiscount={orderDiscount}
              paidAmount={effectivePaid}
              paidAmountDisabled={paidLocked}
              paidAmountError={errors.paidAmount?.message}
              freightError={errors.freight?.message}
              orderDiscountError={errors.orderDiscount?.message}
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
              autoVoucher={autoVoucherOf(voucher)}
              readOnly={readOnly}
            >
              <fieldset disabled={readOnly} className="m-0 min-w-0 space-y-[6px] border-0 p-0">
                {fundShown && (
                  <div className="form-inline-grid">
                    <Label htmlFor="stock-fund" className="field-label">Quỹ</Label>
                    <select
                      id="stock-fund"
                      className={SELECT_CLASS}
                      value={fundId}
                      onChange={(e) => {
                        fundPickedRef.current = true;
                        form.setValue('fundId', e.target.value, { shouldDirty: true });
                      }}
                    >
                      {!fundId && <option value="">-- Chọn quỹ --</option>}
                      {fundOptions.map((f) => (
                        <option key={f.id} value={f.id}>
                          {`${f.code} - ${f.name}`}
                        </option>
                      ))}
                    </select>
                    <FieldError message={errors.fundId?.message} />
                  </div>
                )}
                {dueDateShown && (
                  <div className="form-inline-grid">
                    <Label htmlFor="stock-due-date" className="field-label">Hạn thanh toán</Label>
                    <div className="flex items-center gap-2">
                      <Input
                        id="stock-due-date"
                        type="date"
                        className="h-7"
                        {...form.register('dueDate', {
                          onChange: () => {
                            dueDateTouchedRef.current = true;
                          },
                        })}
                      />
                      {creditDays != null && <span className="whitespace-nowrap text-xs text-muted-foreground">({creditDays} ngày)</span>}
                    </div>
                    <FieldError message={errors.dueDate?.message} />
                  </div>
                )}
              </fieldset>
            </StockVoucherPaymentTab>
          }
          history={
            isEdit ? (
              <ActivityHistory
                activities={activitiesQuery.data ?? []}
                isLoading={activitiesQuery.isLoading}
                isError={activitiesQuery.isError}
                errorMessage={getErrorMessage(activitiesQuery.error)}
                onRetry={() => void activitiesQuery.refetch()}
              />
            ) : undefined
          }
        />

        <div className="shrink-0 rounded-md border bg-card shadow-[0_-2px_6px_rgba(0,0,0,0.06)]">
          <StockTotalsBar
            totals={computed.totals}
            freight={freight}
            orderDiscount={orderDiscount}
            paidAmount={effectivePaid}
            lineCount={lineCount}
            onJump={jumpTo}
          />
          {debtStripPartnerId && selectedPartner && (
            <PartnerDebtStrip
              partnerId={debtStripPartnerId}
              partnerLabel={`${selectedPartner.code} — ${selectedPartner.name}`}
              current={{ effect: selectedReason?.debtEffect ?? 'None', total, paid: effectivePaid }}
              saved={savedImpact}
              cancelled={status === 'Cancelled'}
            />
          )}
        </div>
      </form>

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

      <ConfirmRequiredDialog {...confirmDialogProps} />
    </div>
  );

  function confirmVoucherAction(kind: VoucherActionKind) {
    setConfirmKind(null);
    void runAction({ kind, version: form.getValues('version'), flags: {} });
  }
}

function autoVoucherOf(voucher?: StockVoucher) {
  if (!voucher?.autoCashVoucherId || !voucher.autoCashVoucherCode || !voucher.autoCashVoucherType) return undefined;
  return { id: voucher.autoCashVoucherId, code: voucher.autoCashVoucherCode, type: voucher.autoCashVoucherType };
}

// datetime-local value (local VN time) + days → yyyy-MM-dd, without going through UTC.
function addDaysToLocalDate(local: string | undefined, days: number): string {
  if (!local) return '';
  const date = new Date(local);
  return Number.isNaN(date.getTime()) ? '' : format(addDays(date, days), 'yyyy-MM-dd');
}

function partnerOf(voucher?: StockVoucher): StockVoucherDraftPartner | null {
  if (!voucher?.partnerId) return null;
  return { id: voucher.partnerId, code: voucher.partnerCode ?? '', name: voucher.partnerName ?? '' };
}

function toStockLineLike(line: StockLineFormValues): StockLineLike {
  return {
    trackInventory: line.trackInventory,
    pricingMode: line.pricingMode,
    priceIncludesVat: line.priceIncludesVat,
    sheetCount: toOptionalNumber(line.sheetCount),
    length: toOptionalNumber(line.length),
    width: toOptionalNumber(line.width),
    thickness: toOptionalNumber(line.thickness),
    quantity: toOptionalNumber(line.quantity) ?? 0,
    unitPrice: toOptionalNumber(line.unitPrice) ?? 0,
    discountRate: toOptionalNumber(line.discountRate) ?? 0,
    discountAmount: toOptionalNumber(line.discountAmount),
    discountManual: line.discountManual,
    vatRate: toOptionalNumber(line.vatRate) ?? 0,
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
