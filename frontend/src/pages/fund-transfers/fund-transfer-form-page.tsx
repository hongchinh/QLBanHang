import { useEffect, useRef, useState } from 'react';
import { useForm, useWatch, type Resolver } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { ArrowLeft, Ban, RotateCcw, Save, Trash2 } from 'lucide-react';
import {
  useCancelFundTransfer,
  useCreateFundTransfer,
  useDeleteFundTransfer,
  useFundTransfer,
  useFundTransferActivities,
  useFundTransferDefaults,
  useRestoreFundTransfer,
  useUpdateFundTransfer,
} from '@/features/fund-transfers/hooks';
import {
  fundTransferSchema,
  type FundTransferFormParsed,
  type FundTransferFormValues,
} from '@/features/fund-transfers/schema';
import { toFormDefaults, toUpsertPayload } from '@/features/fund-transfers/payload';
import type { FundTransfer, FundTransferDefaults } from '@/features/fund-transfers/types';
import { useFunds } from '@/features/funds/hooks';
import type { Fund } from '@/features/funds/types';
import { selectableFunds } from '@/features/funds/utils';
import { FUND_KIND_LABELS } from '@/features/cash-common/labels';
import type { FundKind } from '@/features/cash-common/types';
import { ActivityHistory } from '@/components/activity-history/activity-history';
import { ConfirmRequiredDialog } from '@/components/confirm-required-dialog/confirm-required-dialog';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { ButtonLoader } from '@/components/ui/button-loader';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { formatApiErrorDetails, getApiError, getErrorMessage } from '@/lib/api-client';
import {
  toConfirmationRequest,
  withConfirmed,
  type ConfirmationRequest,
  type ConfirmFlags,
} from '@/lib/confirm-required';
import { formatMoneyForDisplay, parseMoneyInput } from '@/lib/money-input';
import { toast } from '@/lib/use-toast';
import { useAuthStore } from '@/stores/auth-store';

const BASE_PATH = '/fund-transfers';
const TITLE = 'Phiếu chuyển quỹ';

type SubmitIntent = 'save-stay' | 'save-exit' | 'update';
type TransferActionKind = 'cancel' | 'restore' | 'delete';
type PendingAction =
  | { kind: 'save'; intent: SubmitIntent; values: FundTransferFormParsed }
  | { kind: TransferActionKind; version?: number };

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

// Server validation keys that map onto a header field.
const FIELD_KEYS = ['voucherAt', 'fromFundId', 'toFundId', 'amount', 'description', 'note'] as const;
type FieldKey = (typeof FIELD_KEYS)[number];

const SELECT_CLASS =
  'h-7 w-full rounded-md border border-input bg-background px-2 text-sm disabled:cursor-not-allowed disabled:opacity-50';

export function FundTransferFormPage() {
  const { id } = useParams<{ id: string }>();
  const isEdit = !!id && id !== 'new';
  const transferQuery = useFundTransfer(isEdit ? id : undefined);
  // Only this first defaults load (server "now" and next number) gates the form mount.
  const defaultsQuery = useFundTransferDefaults({ enabled: !isEdit });

  if (isEdit && transferQuery.isLoading) {
    return <div className="text-sm text-muted-foreground">Đang tải...</div>;
  }
  if (isEdit && !transferQuery.data) {
    return <div className="text-sm text-destructive">Không tìm thấy phiếu.</div>;
  }
  if (!isEdit && !defaultsQuery.isFetchedAfterMount) {
    return <div className="text-sm text-muted-foreground">Đang tải...</div>;
  }

  return (
    <FundTransferForm
      key={isEdit ? id : 'new'}
      id={isEdit ? id : undefined}
      transfer={isEdit ? transferQuery.data : undefined}
      defaults={isEdit ? undefined : defaultsQuery.data}
      refetchTransfer={async () => {
        // A failed refetch still carries the old cached data (and version): report it as no data.
        const result = await transferQuery.refetch();
        return result.status === 'success' ? result.data : undefined;
      }}
    />
  );
}

interface FormProps {
  id?: string;
  transfer?: FundTransfer;
  defaults?: FundTransferDefaults;
  refetchTransfer: () => Promise<FundTransfer | undefined>;
}

// A 422 waiting for the user: the action and the flags already confirmed are resent on "continue" (P10).
interface PendingConfirmation {
  action: PendingAction;
  flags: ConfirmFlags;
  request: ConfirmationRequest;
}

function FundTransferForm({ id, transfer, defaults, refetchTransfer }: FormProps) {
  const isEdit = !!id;
  const navigate = useNavigate();
  // GET /fund-transfers/defaults needs only fund_transfers.view, so creating is gated here too.
  const canCreate = useAuthStore((s) => s.hasPermission('fund_transfers.create'));

  const create = useCreateFundTransfer();
  const update = useUpdateFundTransfer();
  const cancel = useCancelFundTransfer();
  const restore = useRestoreFundTransfer();
  const remove = useDeleteFundTransfer();

  const form = useForm<FundTransferFormValues, unknown, FundTransferFormParsed>({
    resolver: zodResolver(fundTransferSchema) as unknown as Resolver<
      FundTransferFormValues,
      unknown,
      FundTransferFormParsed
    >,
    defaultValues: toFormDefaults(transfer, defaults),
  });

  const [busy, setBusy] = useState(false);
  // `busy` is set only after async validation; the ref blocks a second submit (Ctrl+S key repeat) at once.
  const submittingRef = useRef(false);
  const [confirmKind, setConfirmKind] = useState<TransferActionKind | null>(null);
  const [pendingConfirm, setPendingConfirm] = useState<PendingConfirmation | null>(null);

  const status = transfer?.status ?? 'Active';
  const readOnly = isEdit ? status === 'Cancelled' || !transfer?.canEdit : !canCreate;

  // A background refetch resets the form only when it is not dirty (D29).
  const lastVersionRef = useRef(transfer?.version);
  const isDirty = form.formState.isDirty;
  useEffect(() => {
    if (!transfer || transfer.version === lastVersionRef.current) return;
    lastVersionRef.current = transfer.version;
    if (isDirty) return;
    form.reset(toFormDefaults(transfer));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [transfer?.version]);

  const { data: funds = [] } = useFunds();
  const fromFundId = useWatch({ control: form.control, name: 'fromFundId' });
  const toFundId = useWatch({ control: form.control, name: 'toFundId' });

  const activitiesQuery = useFundTransferActivities(id, isEdit);

  function resetTo(fresh: FundTransfer) {
    lastVersionRef.current = fresh.version;
    form.reset(toFormDefaults(fresh));
  }

  async function runAction(action: PendingAction, flags: ConfirmFlags) {
    setBusy(true);
    try {
      if (action.kind === 'save') {
        const payload = toUpsertPayload(action.values, flags);
        if (id) {
          const saved = await update.mutateAsync({ id, data: payload });
          resetTo(saved);
          toast({ variant: 'success', title: 'Đã cập nhật phiếu', description: saved.code });
        } else {
          const saved = await create.mutateAsync(payload);
          toast({ variant: 'success', title: 'Đã tạo phiếu chuyển quỹ', description: saved.code });
          if (action.intent === 'save-exit') navigate(BASE_PATH);
          else navigate(`${BASE_PATH}/${saved.id}`, { replace: true });
        }
      } else if (id) {
        const body = { version: action.version, acknowledgeNegativeFund: flags.acknowledgeNegativeFund ?? false };
        if (action.kind === 'delete') {
          await remove.mutateAsync({ id, body });
          toast({ variant: 'success', title: 'Đã xóa phiếu' });
          navigate(BASE_PATH);
        } else {
          const saved = await (action.kind === 'cancel' ? cancel : restore).mutateAsync({ id, body });
          resetTo(saved);
          toast({ variant: 'success', title: action.kind === 'cancel' ? 'Đã hủy phiếu' : 'Đã khôi phục phiếu' });
        }
      }
    } catch (err) {
      await handleError(err, action, flags);
    } finally {
      setBusy(false);
    }
  }

  async function handleError(err: unknown, action: PendingAction, flags: ConfirmFlags) {
    const request = toConfirmationRequest(err);
    if (request) {
      setPendingConfirm({ action, flags, request });
      return;
    }
    const apiError = getApiError(err);
    switch (apiError?.code) {
      case 'CONCURRENCY': {
        // D29: reload and drop local edits; never resend stale values with the new version.
        const fresh = await refetchTransfer().catch(() => undefined);
        if (!fresh) {
          // Nothing reloaded: keep the user's edits rather than resetting to the stale cache.
          toast({ variant: 'destructive', title: CONCURRENCY_RELOAD_FAILED_MESSAGE });
          return;
        }
        resetTo(fresh);
        toast({ variant: 'destructive', title: CONCURRENCY_MESSAGE });
        return;
      }
      case 'VALIDATION':
        if (apiError.details && applyFieldErrors(apiError.details)) {
          toast({ variant: 'destructive', title: ERROR_TITLES[action.kind], description: apiError.message });
          return;
        }
        break;
    }
    toast({ variant: 'destructive', title: ERROR_TITLES[action.kind], description: formatApiErrorDetails(err) });
  }

  // Maps header keys onto the fields; returns true when every key was a header key.
  function applyFieldErrors(details: Record<string, string[]>): boolean {
    let allMapped = true;
    for (const [key, messages] of Object.entries(details)) {
      if ((FIELD_KEYS as readonly string[]).includes(key)) {
        form.setError(key as FieldKey, { type: 'server', message: messages[0] });
      } else {
        allMapped = false;
      }
    }
    return allMapped;
  }

  function submitWithIntent(intent: SubmitIntent) {
    if (submittingRef.current || busy || readOnly) return;
    submittingRef.current = true;
    void form
      .handleSubmit((values) => runAction({ kind: 'save', intent, values }, {}))()
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

  function confirmTransferAction(kind: TransferActionKind) {
    setConfirmKind(null);
    void runAction({ kind, version: form.getValues('version') }, {});
  }

  const errors = form.formState.errors;
  const fromOptions = fundOptions(funds, fromFundId, transfer && {
    id: transfer.fromFundId,
    code: transfer.fromFundCode,
    name: transfer.fromFundName,
    kind: transfer.fromFundKind,
  });
  const toOptions = fundOptions(funds, toFundId, transfer && {
    id: transfer.toFundId,
    code: transfer.toFundCode,
    name: transfer.toFundName,
    kind: transfer.toFundKind,
  });
  const amountField = form.register('amount', {
    onBlur: (e: React.FocusEvent<HTMLInputElement>) => {
      const amount = parseMoneyInput(e.target.value);
      if (amount !== undefined) form.setValue('amount', formatMoneyForDisplay(amount));
    },
  });

  return (
    <div className="space-y-4">
      <div className="sticky top-0 z-30 -mx-4 border-b bg-background/95 px-4 py-3 shadow-sm backdrop-blur md:-mx-3 md:px-3">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div className="flex min-w-0 flex-wrap items-center gap-2">
            <Button variant="ghost" size="icon" asChild aria-label="Quay lại">
              <Link to={BASE_PATH}>
                <ArrowLeft className="h-4 w-4 text-slate-500" />
              </Link>
            </Button>
            <h1 className="truncate text-xl font-bold">
              {isEdit ? `${TITLE} ${transfer?.code ?? ''}`.trim() : `Thêm ${TITLE.toLowerCase()}`}
            </h1>
            {status === 'Cancelled' && <Badge variant="destructive">Đã hủy</Badge>}
            {!isEdit && defaults?.nextCode && (
              <span className="text-sm text-muted-foreground">Số phiếu dự kiến: {defaults.nextCode}</span>
            )}
          </div>
          <div className="flex flex-wrap justify-end gap-2">
            {!isEdit && !readOnly && (
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
            {isEdit && transfer?.canCancel && status === 'Active' && (
              <Button type="button" variant="outline" size="sm" onClick={() => setConfirmKind('cancel')} disabled={busy}>
                <Ban className="mr-2 h-4 w-4 text-red-600" />
                Hủy phiếu
              </Button>
            )}
            {isEdit && transfer?.canCancel && status === 'Cancelled' && (
              <Button type="button" variant="outline" size="sm" onClick={() => setConfirmKind('restore')} disabled={busy}>
                <RotateCcw className="mr-2 h-4 w-4 text-emerald-600" />
                Khôi phục
              </Button>
            )}
            {/* The server deletes a transfer in any status. */}
            {isEdit && transfer?.canDelete && (
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
      {!isEdit && readOnly && (
        <div className="rounded-md border border-amber-300 bg-amber-50 p-3 text-sm text-amber-900">
          Bạn không có quyền tạo phiếu chuyển quỹ.
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
        <Card>
          <CardHeader className="flex h-9 flex-row items-center border-b bg-blue-50 p-1 px-3">
            <CardTitle>Thông tin chung</CardTitle>
          </CardHeader>
          <CardContent className="px-4 pb-3 pt-2">
            <fieldset disabled={readOnly} className="m-0 min-w-0 space-y-[6px] border-0 p-0">
              <div className="form-inline-grid form-cols-2">
                <Label htmlFor="fund-transfer-code" className="field-label">Số phiếu</Label>
                <Input
                  id="fund-transfer-code"
                  className="h-7 bg-muted"
                  readOnly
                  tabIndex={-1}
                  value={transfer?.code ?? ''}
                  placeholder="Tự động"
                />
                <Label htmlFor="fund-transfer-at" className="field-label required">Ngày giờ</Label>
                <Input id="fund-transfer-at" type="datetime-local" className="h-7" {...form.register('voucherAt')} />
                <FieldError message={errors.voucherAt?.message} />
              </div>

              <div className="form-inline-grid form-cols-2">
                <Label htmlFor="fund-transfer-from" className="field-label required">Quỹ đi</Label>
                <select
                  id="fund-transfer-from"
                  className={SELECT_CLASS}
                  value={fromFundId}
                  onChange={(e) =>
                    form.setValue('fromFundId', e.target.value, {
                      shouldDirty: true,
                      shouldValidate: form.formState.isSubmitted,
                    })
                  }
                >
                  <option value="">-- Chọn quỹ --</option>
                  {fromOptions.map((f) => (
                    <option key={f.id} value={f.id}>
                      {fundLabel(f)}
                    </option>
                  ))}
                </select>
                <Label htmlFor="fund-transfer-to" className="field-label required">Quỹ đến</Label>
                <select
                  id="fund-transfer-to"
                  className={SELECT_CLASS}
                  value={toFundId}
                  onChange={(e) =>
                    form.setValue('toFundId', e.target.value, {
                      shouldDirty: true,
                      shouldValidate: form.formState.isSubmitted,
                    })
                  }
                >
                  <option value="">-- Chọn quỹ --</option>
                  {toOptions.map((f) => (
                    <option key={f.id} value={f.id}>
                      {fundLabel(f)}
                    </option>
                  ))}
                </select>
                <FieldError message={errors.fromFundId?.message ?? errors.toFundId?.message} />
              </div>

              <div className="form-inline-grid form-cols-2">
                <Label htmlFor="fund-transfer-amount" className="field-label required">Số tiền</Label>
                <Input
                  id="fund-transfer-amount"
                  inputMode="decimal"
                  className="h-7 text-right tabular-nums"
                  {...amountField}
                />
                <Label htmlFor="fund-transfer-description" className="field-label">Diễn giải</Label>
                <Input id="fund-transfer-description" className="h-7" {...form.register('description')} />
                <FieldError message={errors.amount?.message ?? errors.description?.message} />
              </div>

              <div className="form-inline-grid">
                <Label htmlFor="fund-transfer-note" className="field-label">Ghi chú</Label>
                <Input id="fund-transfer-note" className="h-7" {...form.register('note')} />
                <FieldError message={errors.note?.message} />
              </div>
            </fieldset>
          </CardContent>
        </Card>
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
        description={`Phiếu ${transfer?.code ?? ''} sẽ bị hủy và không còn tính vào số dư quỹ.`}
        confirmLabel="Hủy phiếu"
        cancelLabel="Quay lại"
        destructive
        onConfirm={() => confirmTransferAction('cancel')}
      />
      <ConfirmDialog
        open={confirmKind === 'restore'}
        onOpenChange={(open) => !open && setConfirmKind(null)}
        title="Khôi phục phiếu?"
        description={`Phiếu ${transfer?.code ?? ''} sẽ được tính lại vào số dư quỹ.`}
        confirmLabel="Khôi phục"
        cancelLabel="Quay lại"
        onConfirm={() => confirmTransferAction('restore')}
      />
      <ConfirmDialog
        open={confirmKind === 'delete'}
        onOpenChange={(open) => !open && setConfirmKind(null)}
        title="Xóa phiếu?"
        description={`Phiếu ${transfer?.code ?? ''} sẽ bị xóa. Thao tác này không thể hoàn tác.`}
        confirmLabel="Xóa"
        cancelLabel="Quay lại"
        destructive
        onConfirm={() => confirmTransferAction('delete')}
      />

      <ConfirmRequiredDialog
        request={pendingConfirm?.request ?? null}
        confirmLabel={pendingConfirm ? CONFIRM_LABELS[pendingConfirm.action.kind] : undefined}
        onConfirm={() => {
          if (!pendingConfirm) return;
          const { action, flags, request } = pendingConfirm;
          setPendingConfirm(null);
          void runAction(action, withConfirmed(flags, request));
        }}
        onClose={() => setPendingConfirm(null)}
      />
    </div>
  );
}

function FieldError({ message }: { message?: string }) {
  if (!message) return null;
  return <p className="field-message field-message-code text-destructive">{message}</p>;
}

type FundOption = Pick<Fund, 'id' | 'code' | 'name' | 'kind'>;

// Active funds of every kind plus the current selection; a stored fund that is no longer listed
// (soft-deleted) is still shown from the transfer's own snapshot.
function fundOptions(funds: readonly Fund[], selectedId: string, stored?: FundOption): FundOption[] {
  const options: FundOption[] = selectableFunds(funds, undefined, selectedId);
  if (stored && stored.id === selectedId && !options.some((f) => f.id === stored.id)) options.push(stored);
  return options;
}

function fundLabel(fund: { code: string; name: string; kind: FundKind }): string {
  return `${fund.code} — ${fund.name} (${FUND_KIND_LABELS[fund.kind]})`;
}
