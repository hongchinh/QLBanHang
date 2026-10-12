import { useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { format } from 'date-fns';
import { ArrowLeft, Ban, Eraser, Save, Sparkles, Trash2 } from 'lucide-react';
import { DEBT_SIDE_LABELS } from '@/features/cash-common/labels';
import { useOpenItems } from '@/features/debt/hooks';
import { suggestMatching, type MatchRow } from '@/features/debt/matching';
import type { OpenItem } from '@/features/debt/types';
import {
  useCancelDebtOffset,
  useCreateDebtOffset,
  useDebtOffset,
  useDebtOffsetActivities,
  useDebtOffsetDefaults,
  useDeleteDebtOffset,
} from '@/features/debt-offsets/hooks';
import { toCreatePayload } from '@/features/debt-offsets/payload';
import type { DebtOffset, DebtOffsetDefaults } from '@/features/debt-offsets/types';
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
import { roundAwayFromZero } from '@/lib/round';
import { toast } from '@/lib/use-toast';
import { toDateTimeLocalValue } from '@/lib/vn-datetime';
import { useAuthStore } from '@/stores/auth-store';
import { StockVoucherActivityHistory } from '@/pages/stock-vouchers/components/stock-voucher-activity-history';
import { DebtPartnerPicker } from './components/debt-partner-picker';
import { OpenItemGrid } from './components/open-item-grid';

const BASE_PATH = '/debt/offsets';
const TITLE = 'Phiếu bù trừ công nợ';
const fmt = new Intl.NumberFormat('vi-VN');

const CONCURRENCY_MESSAGE = 'Phiếu đã được người khác cập nhật. Đã tải lại dữ liệu, vui lòng thử lại.';

const sumOf = (rows: MatchRow[]) =>
  roundAwayFromZero(
    rows.reduce((sum, row) => sum + row.amount, 0),
    2,
  );

// Rows of entries still listed: an entry gone after a refresh (settled elsewhere) drops out of the selection.
function listedRows(rows: MatchRow[], items: OpenItem[]): MatchRow[] {
  const ids = new Set(items.map((item) => item.entryId));
  return rows.filter((row) => ids.has(row.entryId));
}

// A row the grid flags: negative or above the open amount. The payload drops non-positive rows, so the sums
// shown would no longer be what is sent.
function hasInvalidRow(rows: MatchRow[], items: OpenItem[]): boolean {
  const open = new Map(items.map((item) => [item.entryId, item.open]));
  return rows.some((row) => row.amount < 0 || row.amount > (open.get(row.entryId) ?? 0));
}

const toAllocatable = (items: OpenItem[]) => items.map((item) => ({ ...item, dueDate: item.dueDate ?? null }));

// `yyyy-MM-dd` (DateOnly) → `dd/MM/yyyy`, without going through Date (no time-zone shift).
function formatDateOnly(value: string): string {
  const [y, m, d] = value.split('-');
  return `${d}/${m}/${y}`;
}

function formatDateTime(iso: string) {
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? iso : format(d, 'dd/MM/yyyy HH:mm');
}

const partnerLabel = (code: string | null | undefined, name: string) => (code ? `${code} - ${name}` : name);

// Bù trừ công nợ: `new` creates, `:id` views. There is no edit and no restore (P23).
export function DebtOffsetFormPage() {
  const { id } = useParams<{ id: string }>();
  if (id && id !== 'new') return <DebtOffsetView key={id} id={id} />;
  return <DebtOffsetCreatePage />;
}

// ---- Create -----------------------------------------------------------------------------------

function DebtOffsetCreatePage() {
  // Only this first defaults load (server "now" and next number) gates the form mount.
  const defaultsQuery = useDebtOffsetDefaults();
  if (!defaultsQuery.isFetchedAfterMount) {
    return <div className="text-sm text-muted-foreground">Đang tải...</div>;
  }
  return <DebtOffsetCreateForm defaults={defaultsQuery.data} />;
}

function DebtOffsetCreateForm({ defaults }: { defaults?: DebtOffsetDefaults }) {
  const navigate = useNavigate();
  // GET /debt-offsets/defaults needs only debt_offset.view, so creating is gated here too.
  const canCreate = useAuthStore((s) => s.hasPermission('debt_offset.create'));
  const create = useCreateDebtOffset();

  const [voucherAt, setVoucherAt] = useState(() =>
    toDateTimeLocalValue(defaults?.voucherAt ?? new Date().toISOString()),
  );
  const [note, setNote] = useState('');
  const [partner, setPartner] = useState<{ id: string; label: string } | null>(null);
  const [receivableRows, setReceivableRows] = useState<MatchRow[]>([]);
  const [payableRows, setPayableRows] = useState<MatchRow[]>([]);
  const [busy, setBusy] = useState(false);

  // Disabled until a partner is chosen; no placeholder, so another partner's items never show.
  const partnerId = partner?.id ?? '';
  const receivable = useOpenItems({ partnerId, side: 'Receivable', direction: 'Increase' });
  const payable = useOpenItems({ partnerId, side: 'Payable', direction: 'Increase' });
  const receivableItems = partner ? (receivable.data ?? []) : [];
  const payableItems = partner ? (payable.data ?? []) : [];

  const receivables = listedRows(receivableRows, receivableItems);
  const payables = listedRows(payableRows, payableItems);
  const receivableSum = sumOf(receivables);
  const payableSum = sumOf(payables);
  const invalidRow = hasInvalidRow(receivables, receivableItems) || hasInvalidRow(payables, payableItems);
  const canSave =
    canCreate && !!partner && !!voucherAt && receivableSum > 0 && receivableSum === payableSum && !invalidRow && !busy;
  const loadError = partner ? (receivable.error ?? payable.error) : null;

  const clear = () => {
    setReceivableRows([]);
    setPayableRows([]);
  };

  // Both sides up to min(Σ open receivables, Σ open payables), oldest first.
  const suggest = () => {
    const suggestion = suggestMatching(toAllocatable(receivableItems), toAllocatable(payableItems));
    setReceivableRows(suggestion.decreases);
    setPayableRows(suggestion.increases);
  };

  async function save() {
    if (!canSave || !partner) return;
    setBusy(true);
    try {
      const saved = await create.mutateAsync(
        toCreatePayload({ voucherAt, partnerId: partner.id, note, receivables, payables }),
      );
      toast({ variant: 'success', title: 'Đã tạo phiếu bù trừ công nợ', description: saved.code });
      navigate(`${BASE_PATH}/${saved.id}`, { replace: true });
    } catch (err) {
      // 400: `receivables[i].*` / `payables[i].*` details name the rejected rows.
      toast({ variant: 'destructive', title: 'Không thể lưu phiếu', description: formatApiErrorDetails(err) });
    } finally {
      setBusy(false);
    }
  }

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
            <h1 className="truncate text-xl font-bold">Thêm {TITLE.toLowerCase()}</h1>
            {defaults?.nextCode && (
              <span className="text-sm text-muted-foreground">Số phiếu dự kiến: {defaults.nextCode}</span>
            )}
          </div>
          {canCreate && (
            <Button type="button" size="sm" onClick={() => void save()} disabled={!canSave}>
              {busy ? <ButtonLoader className="mr-2" /> : <Save className="mr-2 h-4 w-4" />}
              Lưu
            </Button>
          )}
        </div>
      </div>

      {!canCreate && (
        <div className="rounded-md border border-amber-300 bg-amber-50 p-3 text-sm text-amber-900">
          Bạn không có quyền tạo phiếu bù trừ công nợ.
        </div>
      )}

      <Card>
        <CardHeader className="flex h-9 flex-row items-center border-b bg-blue-50 p-1 px-3">
          <CardTitle>Thông tin chung</CardTitle>
        </CardHeader>
        <CardContent className="px-4 pb-3 pt-2">
          <fieldset disabled={!canCreate} className="m-0 grid min-w-0 gap-3 border-0 p-0 sm:grid-cols-2 lg:grid-cols-3">
            <div className="space-y-1">
              <Label htmlFor="debt-offset-at" className="required">
                Ngày giờ
              </Label>
              <Input
                id="debt-offset-at"
                type="datetime-local"
                className="h-8"
                value={voucherAt}
                onChange={(e) => setVoucherAt(e.target.value)}
              />
            </div>
            <div className="space-y-1">
              <Label htmlFor="debt-offset-partner" className="required">
                Đối tượng
              </Label>
              {/* Only partners that are both customer and supplier can be offset. */}
              <DebtPartnerPicker
                id="debt-offset-partner"
                bothRoles
                selectedLabel={partner?.label ?? ''}
                onSelect={(p) => {
                  if (p.id === partner?.id) return;
                  setPartner({ id: p.id, label: partnerLabel(p.code, p.name) });
                  clear();
                }}
              />
            </div>
            <div className="space-y-1">
              <Label htmlFor="debt-offset-note">Ghi chú</Label>
              <Input
                id="debt-offset-note"
                className="h-8"
                maxLength={2000}
                value={note}
                onChange={(e) => setNote(e.target.value)}
              />
            </div>
          </fieldset>
        </CardContent>
      </Card>

      {partner ? (
        <div className="space-y-3">
          {loadError && (
            <div className="rounded-md border border-destructive/30 bg-destructive/10 p-3 text-sm text-destructive">
              {getErrorMessage(loadError)}
            </div>
          )}
          <div className="grid gap-4 lg:grid-cols-2">
            <OpenItemGrid
              title="Chứng từ phải thu"
              items={receivableItems}
              value={receivables}
              onChange={setReceivableRows}
              isLoading={receivable.isLoading}
            />
            <OpenItemGrid
              title="Chứng từ phải trả"
              items={payableItems}
              value={payables}
              onChange={setPayableRows}
              isLoading={payable.isLoading}
            />
          </div>
          <div className="flex flex-wrap items-center justify-between gap-3 border-t pt-3 text-sm">
            <div className="flex flex-wrap items-baseline gap-x-4 gap-y-1">
              <span>
                Σ phải thu:{' '}
                <span data-testid="sum-receivable" className="font-semibold tabular-nums">
                  {fmt.format(receivableSum)}
                </span>
              </span>
              <span>
                Σ phải trả:{' '}
                <span data-testid="sum-payable" className="font-semibold tabular-nums">
                  {fmt.format(payableSum)}
                </span>
              </span>
              {receivableSum !== payableSum && (
                <span role="status" className="text-destructive">
                  Hai bên chưa bằng nhau
                </span>
              )}
            </div>
            <div className="flex flex-wrap gap-2">
              <Button
                type="button"
                variant="outline"
                onClick={suggest}
                disabled={!canCreate || receivableItems.length === 0 || payableItems.length === 0}
              >
                <Sparkles className="mr-1 h-4 w-4 text-violet-600" />
                Gợi ý
              </Button>
              <Button
                type="button"
                variant="outline"
                onClick={clear}
                disabled={receivables.length === 0 && payables.length === 0}
              >
                <Eraser className="mr-1 h-4 w-4 text-red-600" />
                Bỏ chọn
              </Button>
            </div>
          </div>
        </div>
      ) : (
        <p className="text-sm text-muted-foreground">Chọn đối tượng để chọn chứng từ bù trừ.</p>
      )}
    </div>
  );
}

// ---- View -------------------------------------------------------------------------------------

type OffsetActionKind = 'cancel' | 'delete';

interface PendingAction {
  kind: OffsetActionKind;
  version: number;
}

// A 422 waiting for the user: the action and the flags already confirmed are resent on "continue" (P10).
interface PendingConfirmation {
  action: PendingAction;
  flags: ConfirmFlags;
  request: ConfirmationRequest;
}

const CONFIRM_LABELS: Record<OffsetActionKind, string> = { cancel: 'Vẫn hủy phiếu', delete: 'Vẫn xóa' };
const ERROR_TITLES: Record<OffsetActionKind, string> = {
  cancel: 'Không thể hủy phiếu',
  delete: 'Không thể xóa phiếu',
};

function DebtOffsetView({ id }: { id: string }) {
  const offsetQuery = useDebtOffset(id);
  const activitiesQuery = useDebtOffsetActivities(id);

  if (offsetQuery.isLoading) {
    return <div className="text-sm text-muted-foreground">Đang tải...</div>;
  }
  if (!offsetQuery.data) {
    return <div className="text-sm text-destructive">Không tìm thấy phiếu.</div>;
  }

  return (
    <DebtOffsetDetail
      offset={offsetQuery.data}
      refetch={() => void offsetQuery.refetch()}
      activities={
        <StockVoucherActivityHistory
          activities={activitiesQuery.data ?? []}
          isLoading={activitiesQuery.isLoading}
          isError={activitiesQuery.isError}
          errorMessage={getErrorMessage(activitiesQuery.error)}
          onRetry={() => void activitiesQuery.refetch()}
        />
      }
    />
  );
}

interface DetailProps {
  offset: DebtOffset;
  refetch: () => void;
  activities: React.ReactNode;
}

function DebtOffsetDetail({ offset, refetch, activities }: DetailProps) {
  const navigate = useNavigate();
  const cancel = useCancelDebtOffset();
  const remove = useDeleteDebtOffset();
  const [busy, setBusy] = useState(false);
  const [confirmKind, setConfirmKind] = useState<OffsetActionKind | null>(null);
  const [pendingConfirm, setPendingConfirm] = useState<PendingConfirmation | null>(null);
  const isCancelled = offset.status === 'Cancelled';

  async function runAction(action: PendingAction, flags: ConfirmFlags) {
    setBusy(true);
    const body = {
      version: action.version,
      confirmRemoveSettlements: flags.confirmRemoveSettlements ?? false,
      confirmedSettlements: flags.confirmedSettlements,
    };
    try {
      if (action.kind === 'delete') {
        await remove.mutateAsync({ id: offset.id, body });
        toast({ variant: 'success', title: 'Đã xóa phiếu' });
        navigate(BASE_PATH);
      } else {
        await cancel.mutateAsync({ id: offset.id, body });
        toast({ variant: 'success', title: 'Đã hủy phiếu' });
      }
    } catch (err) {
      handleError(err, action, flags);
    } finally {
      setBusy(false);
    }
  }

  function handleError(err: unknown, action: PendingAction, flags: ConfirmFlags) {
    // DEBT_SETTLEMENTS_WILL_BE_REMOVED: another document reused an entry the offset no longer covers.
    const request = toConfirmationRequest(err);
    if (request) {
      setPendingConfirm({ action, flags, request });
      return;
    }
    if (getApiError(err)?.code === 'CONCURRENCY') {
      refetch();
      toast({ variant: 'destructive', title: CONCURRENCY_MESSAGE });
      return;
    }
    // PERIOD_LOCKED and the other errors carry their own message.
    toast({ variant: 'destructive', title: ERROR_TITLES[action.kind], description: formatApiErrorDetails(err) });
  }

  function confirmAction(kind: OffsetActionKind) {
    setConfirmKind(null);
    void runAction({ kind, version: offset.version }, {});
  }

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
            <h1 className="truncate text-xl font-bold">{`${TITLE} ${offset.code}`}</h1>
            {isCancelled && <Badge variant="destructive">Đã hủy</Badge>}
          </div>
          <div className="flex flex-wrap justify-end gap-2">
            {offset.canCancel && !isCancelled && (
              <Button type="button" variant="outline" size="sm" onClick={() => setConfirmKind('cancel')} disabled={busy}>
                <Ban className="mr-2 h-4 w-4 text-red-600" />
                Hủy phiếu
              </Button>
            )}
            {/* The server deletes an offset in any status. */}
            {offset.canDelete && (
              <Button type="button" variant="outline" size="sm" onClick={() => setConfirmKind('delete')} disabled={busy}>
                <Trash2 className="mr-2 h-4 w-4 text-red-600" />
                Xóa
              </Button>
            )}
          </div>
        </div>
      </div>

      <Card>
        <CardHeader className="flex h-9 flex-row items-center border-b bg-blue-50 p-1 px-3">
          <CardTitle>Thông tin chung</CardTitle>
        </CardHeader>
        <CardContent className="px-4 pb-3 pt-3">
          <dl className="grid gap-x-6 gap-y-2 text-sm sm:grid-cols-2 lg:grid-cols-3">
            <Field label="Số phiếu">{offset.code}</Field>
            <Field label="Ngày giờ">
              <span className="tabular-nums">{formatDateTime(offset.voucherAt)}</span>
            </Field>
            <Field label="Đối tượng">{partnerLabel(offset.partnerCode, offset.partnerName)}</Field>
            {/* The server amount: it can exceed Σ items once a document it offset was cancelled. */}
            <Field label="Số tiền">
              <span data-testid="debt-offset-amount" className="font-semibold tabular-nums">
                {fmt.format(offset.amount)}
              </span>
            </Field>
            <Field label="Người lập">{offset.ownerName ?? ''}</Field>
            <Field label="Ghi chú">{offset.note ?? ''}</Field>
          </dl>
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="flex h-9 flex-row items-center border-b bg-blue-50 p-1 px-3">
          <CardTitle>Chứng từ bù trừ</CardTitle>
        </CardHeader>
        <CardContent className="pt-3">
          <div className="overflow-x-auto rounded-md border">
            <table aria-label="Chứng từ bù trừ" className="w-full min-w-[480px] text-sm">
              <thead className="bg-muted/50">
                <tr>
                  <th className="px-3 py-2 text-left font-medium">Phía</th>
                  <th className="px-3 py-2 text-left font-medium">Số CT</th>
                  <th className="px-3 py-2 text-left font-medium">Ngày</th>
                  <th className="px-3 py-2 text-right font-medium">Số tiền</th>
                </tr>
              </thead>
              <tbody>
                {offset.items.map((item) => (
                  <tr key={`${item.side}:${item.entryId}`} className="border-t">
                    <td className="px-3 py-2">{DEBT_SIDE_LABELS[item.side]}</td>
                    <td className="px-3 py-2">{item.docCode}</td>
                    <td className="px-3 py-2 tabular-nums">{formatDateOnly(item.docDate)}</td>
                    <td className="px-3 py-2 text-right tabular-nums">{fmt.format(item.amount)}</td>
                  </tr>
                ))}
                {offset.items.length === 0 && (
                  <tr>
                    <td colSpan={4} className="px-3 py-6 text-center text-muted-foreground">
                      {isCancelled ? 'Phiếu đã hủy, không còn chứng từ bù trừ.' : 'Không có chứng từ.'}
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="flex h-9 flex-row items-center border-b bg-blue-50 p-1 px-3">
          <CardTitle>Lịch sử</CardTitle>
        </CardHeader>
        <CardContent className="pt-3">{activities}</CardContent>
      </Card>

      <ConfirmDialog
        open={confirmKind === 'cancel'}
        onOpenChange={(open) => !open && setConfirmKind(null)}
        title="Hủy phiếu?"
        description={`Phiếu ${offset.code} sẽ bị hủy và các chứng từ đã bù trừ sẽ được mở lại.`}
        confirmLabel="Hủy phiếu"
        cancelLabel="Quay lại"
        destructive
        onConfirm={() => confirmAction('cancel')}
      />
      <ConfirmDialog
        open={confirmKind === 'delete'}
        onOpenChange={(open) => !open && setConfirmKind(null)}
        title="Xóa phiếu?"
        description={`Phiếu ${offset.code} sẽ bị xóa. Thao tác này không thể hoàn tác.`}
        confirmLabel="Xóa"
        cancelLabel="Quay lại"
        destructive
        onConfirm={() => confirmAction('delete')}
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

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="flex min-w-0 gap-2">
      <dt className="w-24 shrink-0 text-muted-foreground">{label}</dt>
      <dd className="m-0 min-w-0 break-words">{children}</dd>
    </div>
  );
}
