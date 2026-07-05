import { useEffect, useRef, useState } from 'react';
import { useForm, useWatch, Controller, type Resolver } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Link, useNavigate, useParams } from 'react-router-dom';
import {
  ArrowLeft,
  ArrowRightLeft,
  BadgeCheck,
  Ban,
  CheckCircle2,
  ChevronDown,
  CirclePlus,
  Clock,
  Copy,
  FileSpreadsheet,
  Loader2,
  Printer,
  Save,
  Send,
  X,
} from 'lucide-react';
import {
  useCloneQuotation,
  useCreateQuotation,
  useQuotation,
  useQuotationActivities,
  useTransitionQuotation,
  useUpdateQuotation,
} from '@/features/quotations/hooks';
import { quotationsApi } from '@/features/quotations/api';
import {
  quotationSchema,
  type QuotationFormParsed,
  type QuotationFormValues,
  type QuotationLineFormValues,
} from '@/features/quotations/schema';
import type {
  Quotation,
  QuotationAction,
  QuotationActivity,
  QuotationActivityAction,
  QuotationStatus,
  UpsertQuotationLineRequest,
  UpsertQuotationRequest,
} from '@/features/quotations/types';
import { useCustomer } from '@/features/customers/hooks';
import type { Customer, CustomerSearchItem } from '@/features/customers/types';
import { CustomerAutocomplete } from '@/components/customer-autocomplete/customer-autocomplete';
import { CustomerQuickAddDialog } from '@/components/customer-autocomplete/customer-quick-add-dialog';
import { Button } from '@/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Tabs, TabsContent } from '@/components/ui/tabs';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { ButtonLoader } from '@/components/ui/button-loader';
import { Can } from '@/components/auth/can';
import { cn } from '@/lib/utils';
import { getErrorMessage } from '@/lib/api-client';
import { toast } from '@/lib/use-toast';
import { useAuthStore } from '@/stores/auth-store';
import {
  readQuotationDraft,
  useQuotationDraft,
  type QuotationDraftCustomer,
  type QuotationDraftStorage,
} from '@/features/quotations/use-quotation-draft';
import { StatusPill } from './components/status-pill';
import { createEmptyLine, LineItemsGrid, type LineItemsGridHandle } from './components/line-items-grid';
import { TotalsPanel } from './components/totals-panel';
import { computeLineQuantity } from './utils/compute-line';
import type { HeaderLike, LineLike } from './utils/compute-line';

type QuotationSubmitIntent = 'save-exit' | 'save-stay' | 'save-print';
type QuotationButtonAction = 'send' | 'confirm' | 'cancel' | 'accounting-confirm' | 'clone' | 'print' | 'excel' | 'excel-handover-price' | 'excel-handover-no-price' | 'print-handover-price' | 'print-handover-no-price';

export function QuotationFormPage() {
  const { id } = useParams<{ id: string }>();
  const isEdit = !!id && id !== 'new';
  const navigate = useNavigate();

  const { data: quotation, isLoading } = useQuotation(isEdit ? id : undefined);
  const { data: initialSelectedCustomer } = useCustomer(quotation?.customerId);
  const create = useCreateQuotation();
  const update = useUpdateQuotation();
  const transition = useTransitionQuotation();

  const submitting = create.isPending || update.isPending || transition.isPending;
  const submitError = getErrorMessage(create.error ?? update.error ?? transition.error);
  const hasError = create.isError || update.isError || transition.isError;

  if (isEdit && isLoading) {
    return <div className="text-sm text-muted-foreground">Đang tải...</div>;
  }

  return (
    <QuotationFormInner
      isEdit={isEdit}
      initial={quotation}
      initialSelectedCustomer={initialSelectedCustomer}
      submitting={submitting}
      submitError={submitError}
      hasSubmitError={hasError}
      onSubmit={async (parsed, intent) => {
        try {
          if (isEdit && id) {
            await update.mutateAsync({ id, data: toPayload(parsed) });
            toast({ variant: 'success', title: 'Đã cập nhật báo giá' });
            navigate('/quotations');
          } else {
            const result = await create.mutateAsync(toPayload(parsed));
            toast({ variant: 'success', title: 'Đã tạo báo giá', description: result.code });
            if (intent === 'save-print') {
              try {
                await openPdfInNewTab(`BG-${result.code}`, () => quotationsApi.downloadPdf(result.id));
              } catch (err) {
                toast({ variant: 'destructive', title: 'Không mở được PDF', description: getErrorMessage(err) });
              }
              navigate(`/quotations/${result.id}`);
            } else if (intent === 'save-stay') {
              navigate(`/quotations/${result.id}`);
            } else {
              navigate('/quotations');
            }
          }
        } catch (err) {
          toast({ variant: 'destructive', title: 'Không thể lưu', description: getErrorMessage(err) });
        }
      }}
      onTransition={async (action) => {
        if (!id || !isEdit) return;
        try {
          await transition.mutateAsync({ id, action });
          toast({ variant: 'success', title: actionLabel(action) + ' thành công' });
        } catch (err) {
          toast({ variant: 'destructive', title: 'Không thể chuyển trạng thái', description: getErrorMessage(err) });
        }
      }}
      onPrint={async () => {
        if (!id || !isEdit) return;
        try {
          await openPdfInNewTab(
            quotation ? `BG-${quotation.code}` : 'Báo giá',
            () => quotationsApi.downloadPdf(id),
          );
        } catch (err) {
          toast({ variant: 'destructive', title: 'Không mở được PDF', description: getErrorMessage(err) });
        }
      }}
      onDownloadExcel={async () => {
        if (!id || !isEdit || !quotation) return;
        try {
          const blob = await quotationsApi.downloadExcel(id);
          const url = URL.createObjectURL(blob);
          const a = document.createElement('a');
          a.href = url;
          a.download = `BaoGia_${quotation.code}.xlsx`;
          document.body.appendChild(a);
          a.click();
          a.remove();
          URL.revokeObjectURL(url);
        } catch (err) {
          toast({ variant: 'destructive', title: 'Không tải được Excel', description: getErrorMessage(err) });
        }
      }}
      onPrintHandoverWithPrice={async () => {
        if (!id || !isEdit || !quotation) return;
        try {
          await openPdfInNewTab(
            `BBBG-${quotation.code}`,
            () => quotationsApi.downloadHandoverWithPricePdf(id),
          );
        } catch (err) {
          toast({ variant: 'destructive', title: 'Không mở được PDF', description: getErrorMessage(err) });
        }
      }}
      onPrintHandoverNoPrice={async () => {
        if (!id || !isEdit || !quotation) return;
        try {
          await openPdfInNewTab(
            `BBBG-${quotation.code}`,
            () => quotationsApi.downloadHandoverNoPricePdf(id),
          );
        } catch (err) {
          toast({ variant: 'destructive', title: 'Không mở được PDF', description: getErrorMessage(err) });
        }
      }}
      onDownloadHandoverWithPriceExcel={async () => {
        if (!id || !isEdit || !quotation) return;
        try {
          const blob = await quotationsApi.downloadHandoverWithPriceExcel(id);
          const url = URL.createObjectURL(blob);
          const a = document.createElement('a');
          a.href = url;
          a.download = `BBBG_${quotation.code}.xlsx`;
          document.body.appendChild(a);
          a.click();
          a.remove();
          URL.revokeObjectURL(url);
        } catch (err) {
          toast({ variant: 'destructive', title: 'Không tải được Excel', description: getErrorMessage(err) });
        }
      }}
      onDownloadHandoverNoPriceExcel={async () => {
        if (!id || !isEdit || !quotation) return;
        try {
          const blob = await quotationsApi.downloadHandoverNoPriceExcel(id);
          const url = URL.createObjectURL(blob);
          const a = document.createElement('a');
          a.href = url;
          a.download = `BBBG_${quotation.code}.xlsx`;
          document.body.appendChild(a);
          a.click();
          a.remove();
          URL.revokeObjectURL(url);
        } catch (err) {
          toast({ variant: 'destructive', title: 'Không tải được Excel', description: getErrorMessage(err) });
        }
      }}
    />
  );
}

// Mở PDF trong tab mới với title đúng.
// window.open PHẢI gọi trước await để không bị Chrome popup-blocker chặn.
async function openPdfInNewTab(title: string, fetchBlob: () => Promise<Blob>) {
  const newTab = window.open('about:blank', '_blank'); // đồng bộ — trước mọi await

  if (newTab) {
    newTab.document.write(
      `<!DOCTYPE html><html><head><title>${title}</title></head>` +
      `<body style="margin:0;background:#525659;color:#fff;display:flex;align-items:center;` +
      `justify-content:center;height:100vh;font-family:sans-serif">Đang tải...</body></html>`
    );
    newTab.document.close();
  }

  const blob = await fetchBlob();
  const pdfUrl = URL.createObjectURL(blob);

  if (newTab) {
    newTab.document.open();
    newTab.document.write(
      `<!DOCTYPE html><html><head><title>${title}</title></head>` +
      `<body style="margin:0;padding:0;overflow:hidden">` +
      `<iframe src="${pdfUrl}" style="display:block;width:100vw;height:100vh;border:none;"></iframe>` +
      `</body></html>`
    );
    newTab.document.close();
  } else {
    // fallback nếu popup bị chặn
    window.open(pdfUrl, '_blank', 'noopener,noreferrer');
  }

  window.setTimeout(() => URL.revokeObjectURL(pdfUrl), 60_000);
}

interface InnerProps {
  isEdit: boolean;
  initial?: Quotation;
  initialSelectedCustomer?: Customer;
  submitting: boolean;
  submitError: string;
  hasSubmitError: boolean;
  onSubmit: (parsed: QuotationFormParsed, intent: QuotationSubmitIntent) => Promise<void>;
  onTransition: (action: QuotationAction) => Promise<void>;
  onPrint: () => Promise<void>;
  onDownloadExcel: () => Promise<void>;
  onPrintHandoverWithPrice: () => Promise<void>;
  onPrintHandoverNoPrice: () => Promise<void>;
  onDownloadHandoverWithPriceExcel: () => Promise<void>;
  onDownloadHandoverNoPriceExcel: () => Promise<void>;
}

function QuotationFormInner({
  isEdit,
  initial,
  initialSelectedCustomer,
  submitting,
  submitError,
  hasSubmitError,
  onSubmit,
  onTransition,
  onPrint,
  onDownloadExcel,
  onPrintHandoverWithPrice,
  onPrintHandoverNoPrice,
  onDownloadHandoverWithPriceExcel,
  onDownloadHandoverNoPriceExcel,
}: InnerProps) {
  const navigateInner = useNavigate();
  const clone = useCloneQuotation();

  // Read draft and userId exactly once at mount via useState initializer.
  // useAuthStore.getState() is Zustand's static getter — NOT a React hook,
  // so calling it inside a useState initializer is valid and won't trip rules-of-hooks.
  const [mountData] = useState<{ draft: QuotationDraftStorage | null; userId: string }>(() => {
    const uid = useAuthStore.getState().user?.id ?? '';
    return { draft: !isEdit ? readQuotationDraft(uid) : null, userId: uid };
  });

  const form = useForm<QuotationFormValues, unknown, QuotationFormParsed>({
    resolver: zodResolver(quotationSchema) as unknown as Resolver<QuotationFormValues, unknown, QuotationFormParsed>,
    defaultValues: (!isEdit && mountData.draft?.values) ? mountData.draft.values : toFormDefaults(initial),
  });

  // Narrow watches so this component only re-renders when fields used in render
  // actually change. (Previously useWatch({ control }) without `name` triggered
  // a re-render of the whole form on every keystroke anywhere.)
  const watchedLines = useWatch({ control: form.control, name: 'lines' }) as
    | QuotationLineFormValues[]
    | undefined;
  const watchedTaxRate = useWatch({ control: form.control, name: 'taxRate' }) as number | undefined;
  const watchedDiscount = useWatch({ control: form.control, name: 'discount' }) as number | undefined;
  const watchedFreight = useWatch({ control: form.control, name: 'freight' }) as number | undefined;
  const watchedAdvancePayment = useWatch({ control: form.control, name: 'advancePayment' }) as number | undefined;
  const status: QuotationStatus = initial?.status ?? 'Draft';
  const activitiesQuery = useQuotationActivities(initial?.id, isEdit && !!initial?.id);
  const revenueDateText = formatRevenueDate(initial?.confirmedAt);

  const [activeTab, setActiveTab] = useState<'general' | 'history'>('general');

  const [selectedCustomerView, setSelectedCustomerView] = useState<{ id: string; code: string; name: string } | null>(
    () => {
      if (!isEdit && mountData.draft?.selectedCustomer) return mountData.draft.selectedCustomer;
      return initialSelectedCustomer
        ? { id: initialSelectedCustomer.id, code: initialSelectedCustomer.code, name: initialSelectedCustomer.name }
        : null;
    },
  );
  const [quickAddOpen, setQuickAddOpen] = useState(false);
  const [confirmCloneOpen, setConfirmCloneOpen] = useState(false);
  const [confirmAccountingConfirmOpen, setConfirmAccountingConfirmOpen] = useState(false);
  const [pendingSubmitIntent, setPendingSubmitIntent] = useState<QuotationSubmitIntent | null>(null);
  const [pendingButtonAction, setPendingButtonAction] = useState<QuotationButtonAction | null>(null);

  const { hasDraft, draftSavedAt, clearDraft } = useQuotationDraft({
    form,
    userId: mountData.userId,
    isEdit,
    getSelectedCustomer: () => selectedCustomerView as QuotationDraftCustomer | null,
    initialHasDraft: !!mountData.draft,
    initialSavedAt: mountData.draft ? new Date(mountData.draft.savedAt) : null,
  });

  const lineItemsGridRef = useRef<LineItemsGridHandle>(null);
  const isSubmitBusy = submitting || clone.isPending || pendingSubmitIntent != null || pendingButtonAction != null;

  const GENERAL_INFO_FIELD_ORDER = [
    'quotationDate',
    'deliveryDate',
    'customerId',
    'customerName',
    'deliveryAddress',
    'deliveryRecipient',
    'deliveryPhone',
    'transportVehicleNumber',
    'deliveryNote',
    'internalNote',
  ] as const;

  function handleGeneralInfoKeyDown(e: React.KeyboardEvent<HTMLDivElement>) {
    if (e.defaultPrevented) return;
    if (e.key !== 'Enter' || e.ctrlKey || e.altKey) return;
    const target = e.target as HTMLInputElement;
    if (target.id === 'quotationDate' || target.id === 'deliveryDate') return;
    const currentId = target.id;
    const order = GENERAL_INFO_FIELD_ORDER;
    const idx = order.indexOf(currentId as typeof order[number]);
    if (idx === -1) return;
    e.preventDefault();
    if (!e.shiftKey && idx === order.length - 1) {
      lineItemsGridRef.current?.ensureFirstLineAndFocusProductCode();
      return;
    }
    const nextIdx = e.shiftKey ? idx - 1 : idx + 1;
    if (nextIdx < 0 || nextIdx >= order.length) return;
    document.getElementById(order[nextIdx])?.focus();
  }

  function handleFormKeyDown(e: React.KeyboardEvent<HTMLFormElement>) {
    if (e.defaultPrevented) return;
    if (e.key === 's' && e.ctrlKey && !e.shiftKey && !e.altKey) {
      e.preventDefault();
      submitWithIntent(isEdit ? 'save-exit' : 'save-stay');
    }
  }

  function submitWithIntent(intent: QuotationSubmitIntent) {
    if (isSubmitBusy) return;
    setPendingSubmitIntent(intent);
    void form.handleSubmit(
      async (parsed) => {
        try {
          await onSubmit(parsed, intent);
          if (!isEdit) clearDraft();
        } finally {
          setPendingSubmitIntent(null);
        }
      },
      () => {
        setPendingSubmitIntent(null);
      },
    )();
  }

  useEffect(() => {
    if (!initial) return;
    form.reset(toFormDefaults(initial));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [initial?.id]);

  useEffect(() => {
    if (initialSelectedCustomer) {
      setSelectedCustomerView({
        id: initialSelectedCustomer.id,
        code: initialSelectedCustomer.code,
        name: initialSelectedCustomer.name,
      });
    }
  }, [initialSelectedCustomer?.id]); // eslint-disable-line react-hooks/exhaustive-deps

  function handleSelectCustomer(c: CustomerSearchItem) {
    form.setValue('customerId', c.id, { shouldDirty: true, shouldValidate: true });
    form.setValue('customerName', c.name, { shouldDirty: true });
    setSelectedCustomerView({ id: c.id, code: c.code, name: c.name });

    const cur = form.getValues();
    if (!cur.deliveryAddress?.trim()) {
      form.setValue('deliveryAddress', c.defaultShippingAddress ?? c.companyAddress ?? '', { shouldDirty: true });
    }
    if (!cur.deliveryRecipient?.trim()) {
      form.setValue('deliveryRecipient', c.contactPerson ?? '', { shouldDirty: true });
    }
    if (!cur.deliveryPhone?.trim()) {
      form.setValue('deliveryPhone', c.phoneNumber ?? '', { shouldDirty: true });
    }

    setTimeout(() => document.getElementById('customerName')?.focus(), 0);
  }

  function handleClearCustomer() {
    form.setValue('customerId', '', { shouldDirty: true, shouldValidate: true });
    form.setValue('customerName', '', { shouldDirty: true });
    setSelectedCustomerView(null);
  }

  async function doClone() {
    if (!initial) return;
    try {
      const cloned = await clone.mutateAsync(initial.id);
      toast({ variant: 'success', title: 'Đã nhân bản báo giá', description: cloned.code });
      navigateInner(`/quotations/${cloned.id}`);
    } catch (err) {
      toast({ variant: 'destructive', title: 'Không thể nhân bản', description: getErrorMessage(err) });
    }
  }

  function handleCloneClick() {
    if (form.formState.isDirty) {
      setConfirmCloneOpen(true);
    } else {
      runButtonAction('clone', doClone);
    }
  }

  function runButtonAction(action: QuotationButtonAction, task: () => Promise<void>) {
    if (isSubmitBusy) return;
    setPendingButtonAction(action);
    void task().finally(() => setPendingButtonAction(null));
  }

  const lineLikes: LineLike[] = (watchedLines ?? []).map(toLineLike);
  const header: HeaderLike = {
    taxRate: Number(watchedTaxRate ?? 0) || 0,
    discount: Number(watchedDiscount ?? 0) || 0,
    freight: Number(watchedFreight ?? 0) || 0,
    advancePayment: Number(watchedAdvancePayment ?? 0) || 0,
  };

  const onHeaderChange = (patch: Partial<HeaderLike>) => {
    if (patch.discount !== undefined) form.setValue('discount', patch.discount as never, { shouldDirty: true });
    if (patch.freight !== undefined) form.setValue('freight', patch.freight as never, { shouldDirty: true });
    if (patch.taxRate !== undefined) form.setValue('taxRate', patch.taxRate as never, { shouldDirty: true });
    if (patch.advancePayment !== undefined) form.setValue('advancePayment', patch.advancePayment as never, { shouldDirty: true });
  };

  return (
    <div className="space-y-4">
      <div className="sticky top-0 z-30 -mx-4 border-b bg-background/95 px-4 py-3 shadow-sm backdrop-blur md:-mx-3 md:px-3">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div className="flex min-w-0 items-center gap-2">
          <Button variant="ghost" size="icon" asChild aria-label="Quay lại">
            <Link to="/quotations"><ArrowLeft className="h-4 w-4 text-slate-500" /></Link>
          </Button>
          <h1 className="truncate text-xl font-bold">{isEdit ? 'Chỉnh sửa báo giá' : 'Thêm báo giá'}</h1>
          {isEdit && <StatusPill status={status} />}
          {!isEdit && hasDraft && (
            <div className="flex items-center gap-1 rounded-full border border-amber-300 bg-amber-50 px-2.5 py-0.5 text-xs text-amber-800">
              <span>Nháp chưa lưu{draftSavedAt ? ` từ ${formatDraftTime(draftSavedAt)}` : ''}</span>
              <button
                type="button"
                className="ml-0.5 hover:text-amber-900"
                onClick={() => {
                  clearDraft();
                  form.reset(toFormDefaults(undefined));
                  setSelectedCustomerView(null);
                }}
                aria-label="Xóa nháp"
              >
                <X className="h-3 w-3" />
              </button>
            </div>
          )}
        </div>
          <div className="flex flex-wrap justify-end gap-2">
            <Button type="button" variant="outline" size="sm" asChild>
              <Link to="/quotations">Quay lại danh sách báo giá</Link>
            </Button>
            {isEdit ? (
              <Button
                type="button"
                size="sm"
                onClick={() => submitWithIntent('save-exit')}
                disabled={isSubmitBusy}
                aria-busy={pendingSubmitIntent === 'save-exit' || submitting}
              >
                {(pendingSubmitIntent === 'save-exit' || submitting) && <ButtonLoader className="mr-2" />}
                {pendingSubmitIntent === 'save-exit' || submitting ? 'Đang lưu...' : 'Cập nhật'}
              </Button>
            ) : (
              <>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() => submitWithIntent('save-print')}
                  disabled={isSubmitBusy}
                  aria-busy={pendingSubmitIntent === 'save-print'}
                >
                  {pendingSubmitIntent === 'save-print' ? <ButtonLoader className="mr-2" /> : <Printer className="mr-2 h-4 w-4 text-indigo-600" />}
                  {pendingSubmitIntent === 'save-print' ? 'Đang xử lý...' : 'Lưu và in'}
                </Button>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() => submitWithIntent('save-exit')}
                  disabled={isSubmitBusy}
                  aria-busy={pendingSubmitIntent === 'save-exit'}
                >
                  {pendingSubmitIntent === 'save-exit' && <ButtonLoader className="mr-2" />}
                  {pendingSubmitIntent === 'save-exit' ? 'Đang lưu...' : 'Lưu và thoát'}
                </Button>
                <Button
                  type="button"
                  size="sm"
                  onClick={() => submitWithIntent('save-stay')}
                  disabled={isSubmitBusy}
                  aria-busy={pendingSubmitIntent === 'save-stay'}
                >
                  {pendingSubmitIntent === 'save-stay' && <ButtonLoader className="mr-2" />}
                  {pendingSubmitIntent === 'save-stay' ? 'Đang lưu...' : 'Lưu tạm'}
                </Button>
              </>
            )}
            {isEdit && (
              <>
            {status === 'Draft' && (
              <Button
                variant="outline"
                size="sm"
                onClick={() => runButtonAction('send', () => onTransition('Send'))}
                disabled={isSubmitBusy}
                aria-busy={pendingButtonAction === 'send'}
              >
                {pendingButtonAction === 'send' ? <ButtonLoader className="mr-2" /> : <Send className="mr-2 h-4 w-4 text-cyan-600" />}
                {pendingButtonAction === 'send' ? 'Đang gửi...' : 'Gửi'}
              </Button>
            )}
            {status === 'Sent' && (
              <Button
                variant="outline"
                size="sm"
                onClick={() => runButtonAction('confirm', () => onTransition('Confirm'))}
                disabled={isSubmitBusy}
                aria-busy={pendingButtonAction === 'confirm'}
              >
                {pendingButtonAction === 'confirm' ? <ButtonLoader className="mr-2" /> : <CheckCircle2 className="mr-2 h-4 w-4 text-emerald-600" />}
                {pendingButtonAction === 'confirm' ? 'Đang xác nhận...' : 'Xác nhận'}
              </Button>
            )}
            {status === 'Confirmed' && (
              <Can permission="quotations.accounting_confirm">
                <Button
                  variant="default"
                  size="sm"
                  onClick={() => setConfirmAccountingConfirmOpen(true)}
                  disabled={isSubmitBusy}
                  aria-busy={pendingButtonAction === 'accounting-confirm'}
                >
                  {pendingButtonAction === 'accounting-confirm' ? <ButtonLoader className="mr-2" /> : <BadgeCheck className="mr-2 h-4 w-4" />}
                  {pendingButtonAction === 'accounting-confirm' ? 'Đang xác nhận...' : 'KT xác nhận'}
                </Button>
              </Can>
            )}
            {(status === 'Draft' || status === 'Sent' || status === 'Confirmed') && (
              <Button
                variant="outline"
                size="sm"
                onClick={() => {
                  if (status === 'Confirmed') {
                    const ok = window.confirm(
                      'Báo giá đã xác nhận — hủy sẽ ghi nhận giảm doanh thu của sale. Cần quyền quotations.cancel_confirmed. Tiếp tục?',
                    );
                    if (!ok) return;
                  }
                  runButtonAction('cancel', () => onTransition('Cancel'));
                }}
                disabled={isSubmitBusy}
                aria-busy={pendingButtonAction === 'cancel'}
              >
                {pendingButtonAction === 'cancel' ? <ButtonLoader className="mr-2" /> : <Ban className="mr-2 h-4 w-4 text-red-600" />}
                {pendingButtonAction === 'cancel' ? 'Đang hủy...' : 'Hủy'}
              </Button>
            )}
            {status === 'AccountingConfirmed' && (
              <Can permission="quotations.cancel_accounting_confirmed">
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => runButtonAction('cancel', () => onTransition('Cancel'))}
                  disabled={isSubmitBusy}
                  aria-busy={pendingButtonAction === 'cancel'}
                >
                  {pendingButtonAction === 'cancel' ? <ButtonLoader className="mr-2" /> : <Ban className="mr-2 h-4 w-4 text-red-600" />}
                  {pendingButtonAction === 'cancel' ? 'Đang hủy...' : 'Hủy'}
                </Button>
              </Can>
            )}
            <Can permission="quotations.create">
              <Button
                variant="outline"
                size="sm"
                onClick={handleCloneClick}
                disabled={isSubmitBusy || !initial}
                aria-busy={pendingButtonAction === 'clone' || clone.isPending}
              >
                {pendingButtonAction === 'clone' || clone.isPending ? <ButtonLoader className="mr-2" /> : <Copy className="mr-2 h-4 w-4 text-violet-600" />}
                {pendingButtonAction === 'clone' || clone.isPending ? 'Đang nhân bản...' : 'Nhân bản'}
              </Button>
            </Can>
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <Button
                  variant="outline"
                  size="sm"
                  disabled={isSubmitBusy}
                >
                  <FileSpreadsheet className="mr-2 h-4 w-4 text-emerald-700" />
                  Excel
                  <ChevronDown className="ml-1 h-3 w-3" />
                </Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end">
                <DropdownMenuItem onClick={() => runButtonAction('excel', onDownloadExcel)}>
                  Báo giá
                </DropdownMenuItem>
                <DropdownMenuItem onClick={() => runButtonAction('excel-handover-price', onDownloadHandoverWithPriceExcel)}>
                  Biên bản bàn giao (có tiền)
                </DropdownMenuItem>
                <DropdownMenuItem onClick={() => runButtonAction('excel-handover-no-price', onDownloadHandoverNoPriceExcel)}>
                  Biên bản bàn giao (không tiền)
                </DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <Button
                  variant="outline"
                  size="sm"
                  disabled={isSubmitBusy}
                >
                  <Printer className="mr-2 h-4 w-4 text-indigo-600" />
                  In
                  <ChevronDown className="ml-1 h-3 w-3" />
                </Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end">
                <DropdownMenuItem onClick={() => runButtonAction('print', onPrint)}>
                  Báo giá
                </DropdownMenuItem>
                <DropdownMenuItem onClick={() => runButtonAction('print-handover-price', onPrintHandoverWithPrice)}>
                  Biên bản bàn giao (có tiền)
                </DropdownMenuItem>
                <DropdownMenuItem onClick={() => runButtonAction('print-handover-no-price', onPrintHandoverNoPrice)}>
                  Biên bản bàn giao (không tiền)
                </DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>
              </>
            )}
          </div>
        </div>
      </div>

      {isEdit && initial && !initial.canEdit && (
        <div className="rounded-md border border-amber-300 bg-amber-50 p-3 text-sm text-amber-900">
          {initial.isOwnerDeleted
            ? 'Chủ sở hữu báo giá đã ngừng hoạt động — chỉ có thể nhân bản.'
            : initial.status === 'Cancelled'
            ? 'Báo giá đã huỷ — không thể chỉnh sửa.'
            : `Báo giá đang ở trạng thái ${initial.status} — cấu hình khoá của bạn không cho phép sửa.`}
          {initial.canClone && (
            <Button
              className="ml-3"
              size="sm"
              variant="outline"
              onClick={() => {
                clone.mutate(initial.id, {
                  onSuccess: (cloned) => {
                    toast({ variant: 'success', title: 'Đã nhân bản báo giá', description: cloned.code });
                    navigateInner(`/quotations/${cloned.id}`);
                  },
                  onError: (err) => toast({ variant: 'destructive', title: 'Nhân bản thất bại', description: getErrorMessage(err) }),
                });
              }}
              disabled={clone.isPending}
            >
              {clone.isPending && <ButtonLoader className="mr-2" />}
              {clone.isPending ? 'Đang nhân bản...' : 'Nhân bản'}
            </Button>
          )}
        </div>
      )}

      {/* eslint-disable-next-line jsx-a11y/no-noninteractive-element-interactions -- Form-level Ctrl+S shortcut delegates to the existing submit handler. */}
      <form
        onSubmit={(e) => {
          e.preventDefault();
          submitWithIntent(isEdit ? 'save-exit' : 'save-stay');
        }}
        onKeyDown={handleFormKeyDown}
        className="space-y-4"
      >
        <div className="grid gap-4 lg:grid-cols-[1fr_320px] items-stretch">
          <Card>
            <Tabs value={activeTab} onValueChange={(v) => setActiveTab(v as 'general' | 'history')}>
              <CardHeader className="flex flex-row items-center justify-between gap-3 space-y-0 border-b p-1 px-3 h-9 bg-blue-50">
                <CardTitle>Thông tin chung</CardTitle>
                {isEdit && (
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    className="h-7 gap-1.5 px-2.5 text-xs text-muted-foreground"
                    onClick={() => setActiveTab(activeTab === 'general' ? 'history' : 'general')}
                  >
                    <Clock className="h-3.5 w-3.5" />
                    {activeTab === 'general' ? 'Lịch sử' : '← Thông tin chung'}
                  </Button>
                )}
              </CardHeader>
              <TabsContent value="general" className="mt-0">
                <CardContent className="space-y-[6px] px-4 pt-2 pb-3" onKeyDown={handleGeneralInfoKeyDown}>
              <div className="form-inline-grid form-cols-3">
                <Label htmlFor="quotationDate" className="field-label required">Ngày báo giá</Label>
                <Controller
                  control={form.control}
                  name="quotationDate"
                  render={({ field }) => (
                    <DatePickerInput
                      id="quotationDate"
                      value={field.value}
                      onChange={field.onChange}
                      onBlur={field.onBlur}
                      className="h-7 max-w-[150px]"
                    />
                  )}
                />
                <Label htmlFor="revenueDate" className="field-label">Ngày doanh thu</Label>
                <div>
                  <Input
                    id="revenueDate"
                    value={revenueDateText}
                    readOnly
                    tabIndex={-1}
                    className="h-7 max-w-[200px] bg-muted text-muted-foreground"
                  />
                  {initial?.accountingConfirmedAt && (
                    <p className="mt-1 text-xs text-muted-foreground">
                      KT xác nhận: {formatRevenueDate(initial.accountingConfirmedAt)}
                      {initial.accountingConfirmedByName && ` bởi ${initial.accountingConfirmedByName}`}
                    </p>
                  )}
                </div>
                <Label htmlFor="deliveryDate" className="field-label">Ngày giao</Label>
                <Controller
                  control={form.control}
                  name="deliveryDate"
                  render={({ field }) => (
                    <DatePickerInput
                      id="deliveryDate"
                      value={field.value ?? ''}
                      onChange={field.onChange}
                      onBlur={field.onBlur}
                      className="h-7 max-w-[200px]"
                    />
                  )}
                />
                {form.formState.errors.quotationDate && (
                  <p className="field-message field-message-code text-destructive">
                    {String(form.formState.errors.quotationDate.message)}
                  </p>
                )}
              </div>

              <div className="form-inline-grid form-cols-customer">
                <Label htmlFor="customerId" className="field-label required">Mã KH</Label>
                <CustomerAutocomplete
                  inputId="customerId"
                  inputAriaLabel="Mã khách hàng"
                  placeholder="Nhập mã / tên / MST / địa chỉ / SĐT..."
                  value={selectedCustomerView}
                  errorMessage={
                    form.formState.errors.customerId
                      ? String(form.formState.errors.customerId.message)
                      : undefined
                  }
                  onSelect={handleSelectCustomer}
                  onClear={handleClearCustomer}
                  onAddNewClick={() => setQuickAddOpen(true)}
                />
                <Label htmlFor="customerName" className="field-label">Tên KH</Label>
                <Input
                  id="customerName"
                  className="h-7"
                  {...form.register('customerName', {
                    onBlur: (e) => {
                      if (!e.target.value.trim() && selectedCustomerView) {
                        form.setValue('customerName', selectedCustomerView.name, { shouldDirty: true });
                      }
                    },
                  })}
                />
                {isEdit && initialSelectedCustomer?.status === 'Inactive' && (
                  <p className="field-message field-message-code text-amber-600">Khách hàng đã ngừng sử dụng</p>
                )}
              </div>

              <div className="form-inline-grid">
                <Label htmlFor="deliveryAddress" className="field-label">Địa chỉ giao</Label>
                <Input id="deliveryAddress" className="h-7" {...form.register('deliveryAddress')} />
              </div>

              <div className="form-inline-grid form-cols-3">
                <Label htmlFor="deliveryRecipient" className="field-label">Người nhận</Label>
                <Input id="deliveryRecipient" className="h-7" {...form.register('deliveryRecipient')} />
                <Label htmlFor="deliveryPhone" className="field-label">Điện thoại</Label>
                <Input id="deliveryPhone" className="h-7" {...form.register('deliveryPhone')} />
                <Label htmlFor="transportVehicleNumber" className="field-label">Số xe</Label>
                <Input id="transportVehicleNumber" className="h-7" {...form.register('transportVehicleNumber')} />
              </div>

              <div className="form-inline-grid form-cols-2">
                <Label htmlFor="deliveryNote" className="field-label">Ghi chú GH</Label>
                <Input id="deliveryNote" className="h-7" {...form.register('deliveryNote')} />
                <Label htmlFor="internalNote" className="field-label">Ghi chú NB</Label>
                <Input id="internalNote" className="h-7" {...form.register('internalNote')} />
              </div>
                </CardContent>
              </TabsContent>
              {isEdit && (
                <TabsContent value="history" className="mt-0">
                  <CardContent>
                    <QuotationActivityHistory
                      activities={activitiesQuery.data ?? []}
                      isLoading={activitiesQuery.isLoading}
                      isError={activitiesQuery.isError}
                      errorMessage={getErrorMessage(activitiesQuery.error)}
                      onRetry={() => void activitiesQuery.refetch()}
                    />
                  </CardContent>
                </TabsContent>
              )}
            </Tabs>
          </Card>

          <TotalsPanel lines={lineLikes} header={header} onHeaderChange={onHeaderChange} />
        </div>

          <Card>
            <CardContent className="p-0">
              <LineItemsGrid ref={lineItemsGridRef} form={form} />
              {form.formState.errors.lines && (
                <div className="px-6 pb-4 pt-2 text-sm text-destructive">
                  {(() => {
                    const rootMsg = (form.formState.errors.lines as { message?: string }).message;
                    if (rootMsg) return <p>{rootMsg}</p>;
                    const lineErrors = collectLineFieldErrors(form.formState.errors.lines);
                    if (lineErrors.length === 0) return <p>Báo giá chưa hợp lệ.</p>;
                    return (
                      <div className="space-y-1">
                        <p className="font-medium">Một số dòng hàng chưa hợp lệ:</p>
                        <ul className="list-disc pl-5 space-y-0.5">
                          {lineErrors.map(({ rowNum, problems }) => (
                            <li key={rowNum}>
                              <span className="font-medium">Dòng {rowNum}:</span>{' '}
                              {problems.join(' · ')}
                            </li>
                          ))}
                        </ul>
                      </div>
                    );
                  })()}
                </div>
              )}
            </CardContent>
          </Card>

          {hasSubmitError && (
            <div className="rounded-md border border-destructive/30 bg-destructive/10 p-3 text-sm text-destructive">
              {submitError}
            </div>
          )}

      </form>

      <CustomerQuickAddDialog
        open={quickAddOpen}
        onOpenChange={setQuickAddOpen}
        onCreated={(c) =>
          handleSelectCustomer({
            id: c.id,
            code: c.code,
            name: c.name,
            taxCode: c.taxCode,
            companyAddress: c.companyAddress,
            defaultShippingAddress: c.defaultShippingAddress,
            contactPerson: c.contactPerson,
            phoneNumber: c.phoneNumber,
            status: c.status,
          })
        }
      />

      <ConfirmDialog
        open={confirmCloneOpen}
        onOpenChange={setConfirmCloneOpen}
        title="Có thay đổi chưa lưu"
        description="Bạn có thay đổi chưa lưu — bản nhân bản sẽ không bao gồm các thay đổi này. Tiếp tục?"
        confirmLabel="Nhân bản"
        loading={clone.isPending}
        onConfirm={() => {
          setConfirmCloneOpen(false);
          void doClone();
        }}
      />

      <ConfirmDialog
        open={confirmAccountingConfirmOpen}
        onOpenChange={setConfirmAccountingConfirmOpen}
        title="Xác nhận kế toán đã nhận tiền?"
        description={`Báo giá ${initial?.code} sẽ chuyển sang trạng thái "KT xác nhận". Thao tác này không thể hoàn tác trực tiếp.`}
        confirmLabel="KT xác nhận"
        loading={pendingButtonAction === 'accounting-confirm'}
        onConfirm={() => {
          setConfirmAccountingConfirmOpen(false);
          setPendingButtonAction('accounting-confirm');
          void onTransition('AccountingConfirm').finally(() => setPendingButtonAction(null));
        }}
      />
    </div>
  );
}

interface QuotationActivityHistoryProps {
  activities: QuotationActivity[];
  isLoading: boolean;
  isError: boolean;
  errorMessage: string;
  onRetry: () => void;
}

function QuotationActivityHistory({
  activities,
  isLoading,
  isError,
  errorMessage,
  onRetry,
}: QuotationActivityHistoryProps) {
  if (isLoading) {
    return (
      <div className="flex items-center gap-2 py-6 text-sm text-muted-foreground">
        <Loader2 className="h-4 w-4 animate-spin text-blue-600" />
        Đang tải lịch sử...
      </div>
    );
  }

  if (isError) {
    return (
      <div className="rounded-md border border-destructive/30 bg-destructive/10 p-3 text-sm text-destructive">
        <div>{errorMessage}</div>
        <Button type="button" variant="outline" size="sm" className="mt-2" onClick={onRetry}>
          Thử lại
        </Button>
      </div>
    );
  }

  if (activities.length === 0) {
    return (
      <div className="rounded-md border border-dashed p-6 text-center text-sm text-muted-foreground">
        Chưa có lịch sử phát sinh sau khi bật tính năng này.
      </div>
    );
  }

  return (
    <ul className="divide-y rounded-md border">
      {activities.map((activity) => (
        <li key={activity.id} className="flex gap-3 p-3">
          <div className="mt-0.5">{activityIcon(activity.action)}</div>
          <div className="min-w-0 flex-1">
            <div className="flex flex-wrap items-center gap-x-2 gap-y-1">
              <span className="font-medium">{activityLabel(activity.action)}</span>
              <span className="text-xs text-muted-foreground">{formatActivityTime(activity.occurredAt)}</span>
            </div>
            <p className="mt-1 text-sm text-foreground">{activity.description}</p>
            <p className="mt-1 text-xs text-muted-foreground">{activity.actorName ?? 'Người dùng không xác định'}</p>
          </div>
        </li>
      ))}
    </ul>
  );
}

interface DatePickerInputProps {
  id?: string;
  name?: string;
  value: string;
  onChange: (value: string) => void;
  onBlur?: () => void;
  className?: string;
}

function DatePickerInput({ id, name, value, onChange, onBlur, className }: DatePickerInputProps) {
  return (
    <div className="relative">
      <div className="pointer-events-none absolute inset-0 z-[1] flex items-center px-3 text-sm">
        {value
          ? isoToDisplay(value)
          : <span className="text-xs text-muted-foreground">dd/MM/yyyy</span>
        }
      </div>
      <Input
        id={id}
        name={name}
        type="date"
        value={value || ''}
        onChange={(e) => onChange(e.target.value)}
        onBlur={onBlur}
        className={cn('text-transparent', className)}
      />
    </div>
  );
}

function isoToDisplay(iso: string): string {
  if (!iso) return '';
  const parts = iso.split('-');
  if (parts.length !== 3) return iso;
  const [y, m, d] = parts;
  return `${d}/${m}/${y}`;
}

function activityIcon(action: QuotationActivityAction) {
  switch (action) {
    case 'Created':
      return <CirclePlus className="h-4 w-4 text-cyan-600" />;
    case 'Updated':
      return <Save className="h-4 w-4 text-blue-600" />;
    case 'Sent':
      return <Send className="h-4 w-4 text-cyan-600" />;
    case 'Confirmed':
      return <CheckCircle2 className="h-4 w-4 text-emerald-600" />;
    case 'AccountingConfirmed':
      return <CheckCircle2 className="h-4 w-4 text-sky-600" />;
    case 'Cancelled':
      return <Ban className="h-4 w-4 text-red-600" />;
    case 'OwnerTransferred':
      return <ArrowRightLeft className="h-4 w-4 text-violet-600" />;
    case 'Cloned':
      return <Copy className="h-4 w-4 text-violet-600" />;
  }
}

function activityLabel(action: QuotationActivityAction) {
  switch (action) {
    case 'Created':
      return 'Tạo báo giá';
    case 'Updated':
      return 'Cập nhật';
    case 'Sent':
      return 'Gửi';
    case 'Confirmed':
      return 'Xác nhận';
    case 'AccountingConfirmed':
      return 'KT xác nhận';
    case 'Cancelled':
      return 'Hủy';
    case 'OwnerTransferred':
      return 'Chuyển chủ sở hữu';
    case 'Cloned':
      return 'Nhân bản';
  }
}

function formatActivityTime(value: string) {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  return date.toLocaleString('vi-VN', {
    hour: '2-digit',
    minute: '2-digit',
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
  });
}

function formatRevenueDate(value?: string) {
  if (!value) return 'Chưa ghi nhận';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  return date.toLocaleDateString('vi-VN', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
  });
}

function toFormDefaults(q?: Quotation): QuotationFormValues {
  const today = new Date();
  const tomorrow = new Date(today);
  tomorrow.setDate(today.getDate() + 1);
  const defaultQuotationDate = today.toISOString().slice(0, 10);
  const defaultDeliveryDate = tomorrow.toISOString().slice(0, 10);
  return {
    customerId: q?.customerId ?? '',
    customerName: q?.customerName ?? '',
    quotationDate: q?.quotationDate ?? defaultQuotationDate,
    deliveryAddress: q?.deliveryAddress ?? '',
    deliveryRecipient: q?.deliveryRecipient ?? '',
    deliveryPhone: q?.deliveryPhone ?? '',
    transportVehicleNumber: q?.transportVehicleNumber ?? '',
    deliveryDate: q?.deliveryDate ?? (q ? '' : defaultDeliveryDate),
    deliveryNote: q?.deliveryNote ?? '',
    taxRate: (q?.taxRate ?? 0) as number,
    discount: (q?.discount ?? 0) as number,
    freight: (q?.freight ?? 0) as number,
    advancePayment: (q?.advancePayment ?? 0) as number,
    internalNote: q?.internalNote ?? '',
    lines: q
      ? (q.lines ?? []).map((l, idx) => ({
          _uiKey: l.id ?? crypto.randomUUID(),
          id: l.id,
          sortOrder: l.sortOrder ?? idx,
          productId: l.productId,
          productCode: l.productCode ?? '',
          productName: l.productName,
          specification: l.specification ?? '',
          unitName: l.unitName,
          pricingMode: l.pricingMode,
          length: l.length ?? '',
          width: l.width ?? '',
          thickness: l.thickness ?? '',
          density: l.density ?? '',
          sheetCount: l.sheetCount ?? '',
          quantity: l.quantity,
          unitPrice: l.unitPrice,
          lineTotal: l.lineTotal,
          unitCost: l.unitCost ?? '',
          lineCost: l.lineCost ?? '',
          lineProfit: l.lineProfit ?? '',
          note: l.note ?? '',
        })) as QuotationLineFormValues[]
      : [createEmptyLine(0)],
  };
}

function toLineLike(line: QuotationLineFormValues): LineLike {
  return {
    pricingMode: line.pricingMode,
    length: toNum(line.length),
    width: toNum(line.width),
    thickness: toNum(line.thickness),
    density: toNum(line.density),
    sheetCount: toNum(line.sheetCount),
    quantity: toNum(line.quantity) ?? 0,
    unitPrice: toNum(line.unitPrice) ?? 0,
    unitCost: toNum(line.unitCost),
  };
}

function toNum(v: unknown): number | undefined {
  if (v === undefined || v === null || v === '') return undefined;
  const n = typeof v === 'number' ? v : Number(v);
  return Number.isFinite(n) ? n : undefined;
}

function toPayload(parsed: QuotationFormParsed): UpsertQuotationRequest {
  return {
    customerId: parsed.customerId,
    customerName: parsed.customerName?.trim() || undefined,
    quotationDate: parsed.quotationDate,
    deliveryAddress: parsed.deliveryAddress,
    deliveryRecipient: parsed.deliveryRecipient,
    deliveryPhone: parsed.deliveryPhone,
    transportVehicleNumber: parsed.transportVehicleNumber,
    deliveryDate: parsed.deliveryDate,
    deliveryNote: parsed.deliveryNote,
    taxRate: parsed.taxRate,
    discount: parsed.discount,
    freight: parsed.freight,
    advancePayment: parsed.advancePayment,
    internalNote: parsed.internalNote,
    lines: parsed.lines.map<UpsertQuotationLineRequest>((l, idx) => {
      const lineLike = toLineLike(l);
      return {
        id: l.id,
        sortOrder: l.sortOrder ?? idx,
        productId: l.productId,
        productCode: l.productCode,
        productName: l.productName,
        specification: l.specification,
        unitName: l.unitName,
        pricingMode: l.pricingMode,
        length: l.length,
        width: l.width,
        thickness: l.thickness,
        density: l.density,
        sheetCount: l.sheetCount,
        quantity: computeLineQuantity(lineLike),
        unitPrice: l.unitPrice,
        unitCost: l.unitCost,
        note: l.note,
      };
    }),
  };
}

function collectLineFieldErrors(
  linesErrors: unknown,
): Array<{ rowNum: number; problems: string[] }> {
  if (!Array.isArray(linesErrors)) return [];
  const result: Array<{ rowNum: number; problems: string[] }> = [];
  for (let i = 0; i < linesErrors.length; i++) {
    const lineErr = linesErrors[i] as Record<string, { message?: string } | undefined> | null | undefined;
    if (!lineErr || typeof lineErr !== 'object') continue;
    const problems: string[] = [];
    if (lineErr.productName?.message) problems.push(`Tên hàng: ${lineErr.productName.message}`);
    if (lineErr.unitName?.message) problems.push(`ĐVT: ${lineErr.unitName.message}`);
    if (lineErr.quantity?.message) problems.push(`Số lượng: ${lineErr.quantity.message}`);
    if (lineErr.unitPrice?.message) problems.push(`Đơn giá: ${lineErr.unitPrice.message}`);
    if (problems.length > 0) result.push({ rowNum: i + 1, problems });
  }
  return result;
}

function formatDraftTime(date: Date): string {
  return date.toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' });
}

function actionLabel(action: QuotationAction) {
  switch (action) {
    case 'Send': return 'Gửi báo giá';
    case 'Confirm': return 'Xác nhận';
    case 'AccountingConfirm': return 'Kế toán xác nhận';
    case 'Cancel': return 'Hủy';
  }
}
