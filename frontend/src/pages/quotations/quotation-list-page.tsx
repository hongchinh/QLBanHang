import { useEffect, useMemo, useRef, useState } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import {
  flexRender,
  getCoreRowModel,
  useReactTable,
  type ColumnDef,
} from '@tanstack/react-table';
import { Plus, Pencil, Printer, Ban, Search, Copy, MoreHorizontal, Send, CheckCircle2, BadgeCheck, Loader2, FileSpreadsheet } from 'lucide-react';
import {
  useQuotations,
  useTransitionQuotation,
  useCloneQuotation,
  useQuotationOwners,
} from '@/features/quotations/hooks';
import { useAuthStore } from '@/stores/auth-store';
import { quotationsApi } from '@/features/quotations/api';
import type {
  QuotationAction,
  QuotationListItem,
  QuotationStatus,
} from '@/features/quotations/types';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { MultiSelect } from '@/components/ui/multi-select';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow, TableColGroup } from '@/components/ui/table';
import { Card, CardContent } from '@/components/ui/card';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { Can } from '@/components/auth/can';
import { useDebouncedValue } from '@/lib/use-debounced-value';
import { useSearchParamNumber, useSearchParamString } from '@/lib/use-search-param-state';
import { useColumnSizingPersist } from '@/lib/use-column-sizing-persist';
import { toast } from '@/lib/use-toast';
import { getErrorMessage } from '@/lib/api-client';
import { StatusPill } from './components/status-pill';
import { ListFooter } from './components/list-footer';
import { QuotationDateFilter } from './components/quotation-date-filter';
import { parseOwnerIds } from './utils/owner-ids';
import {
  QUOTATION_LIST_STORAGE_KEY,
  QUOTATION_LIST_COLUMN_SIZING_DEFAULTS,
  COLUMN_MIN_SIZE,
} from './column-sizing-defaults';

const PAGE_SIZE_OPTIONS = [10, 20, 50, 100] as const;
const DEFAULT_PAGE_SIZE = 20;

const currency = new Intl.NumberFormat('vi-VN');

const STATUS_OPTIONS: ReadonlyArray<{ value: QuotationStatus; label: string }> = [
  { value: 'Draft', label: 'Nháp' },
  { value: 'Sent', label: 'Đã gửi' },
  { value: 'Confirmed', label: 'Đã xác nhận' },
  { value: 'AccountingConfirmed', label: 'KT xác nhận' },
  { value: 'Cancelled', label: 'Đã hủy' },
];
const VALID_STATUSES: ReadonlySet<QuotationStatus> = new Set(STATUS_OPTIONS.map((o) => o.value));
const DEFAULT_ACTIVE_STATUSES: ReadonlyArray<QuotationStatus> = ['Draft', 'Sent', 'Confirmed', 'AccountingConfirmed'];

function moneyHeader(label: string) {
  return <div className="text-right">{label}</div>;
}

function formatDate(iso?: string) {
  if (!iso) return '';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return iso;
  return d.toLocaleDateString('vi-VN');
}

function formatNullableCurrency(value?: number | null) {
  return typeof value === 'number' ? currency.format(value) : '—';
}

function moneyCell(value?: number | null) {
  return <span className="block text-right tabular-nums">{formatNullableCurrency(value)}</span>;
}

async function openQuotationPdf(id: string) {
  const blob = await quotationsApi.downloadPdf(id);
  const url = URL.createObjectURL(blob);
  window.open(url, '_blank', 'noopener,noreferrer');
  window.setTimeout(() => URL.revokeObjectURL(url), 60_000);
}

async function downloadQuotationExcel(id: string, code: string) {
  const blob = await quotationsApi.downloadExcel(id);
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = `BaoGia_${code}.xlsx`;
  document.body.appendChild(a);
  a.click();
  a.remove();
  URL.revokeObjectURL(url);
}

async function openHandoverPdf(id: string, withPrice: boolean) {
  const blob = withPrice
    ? await quotationsApi.downloadHandoverWithPricePdf(id)
    : await quotationsApi.downloadHandoverNoPricePdf(id);
  const url = URL.createObjectURL(blob);
  window.open(url, '_blank', 'noopener,noreferrer');
  window.setTimeout(() => URL.revokeObjectURL(url), 60_000);
}

async function downloadHandoverExcel(id: string, code: string, withPrice: boolean) {
  const blob = withPrice
    ? await quotationsApi.downloadHandoverWithPriceExcel(id)
    : await quotationsApi.downloadHandoverNoPriceExcel(id);
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = `BBBG_${code}.xlsx`;
  document.body.appendChild(a);
  a.click();
  a.remove();
  URL.revokeObjectURL(url);
}

type ExportKey =
  | 'quotation-pdf'
  | 'quotation-excel'
  | 'handover-pdf-with'
  | 'handover-pdf-no'
  | 'handover-excel-with'
  | 'handover-excel-no';

function QuotationActionsCell({
  q,
  onTransition,
  onClone,
}: {
  q: QuotationListItem;
  onTransition: (action: QuotationAction) => void;
  onClone: () => void;
}) {
  const [loadingExport, setLoadingExport] = useState<ExportKey | null>(null);
  const canSend = q.status === 'Draft';
  const canConfirm = q.status === 'Sent';
  const canCancel = q.status !== 'Cancelled';
  const busy = loadingExport !== null;

  const runExport = (key: ExportKey, fn: () => Promise<void>) => {
    if (busy) return;
    setLoadingExport(key);
    fn()
      .catch((err) =>
        toast({ variant: 'destructive', title: 'Lỗi xuất file', description: getErrorMessage(err) }),
      )
      .finally(() => setLoadingExport(null));
  };

  return (
    <div className="flex justify-end gap-1">
      <Can permission="quotations.update">
        <Button asChild variant="ghost" size="icon" aria-label="Sửa">
          <Link to={`/quotations/${q.id}`}><Pencil className="h-4 w-4 text-blue-600" /></Link>
        </Button>
      </Can>
      <Can permission="quotations.accounting_confirm">
        {q.status === 'Confirmed' && (
          <Button
            variant="ghost"
            size="icon"
            aria-label="KT xác nhận đã nhận tiền"
            title="KT xác nhận đã nhận tiền"
            onClick={() => onTransition('AccountingConfirm')}
          >
            <BadgeCheck className="h-4 w-4 text-emerald-600" />
          </Button>
        )}
      </Can>
      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <Button variant="ghost" size="icon" aria-label="Thao tác khác">
            {busy
              ? <Loader2 className="h-4 w-4 animate-spin text-slate-400" />
              : <MoreHorizontal className="h-4 w-4 text-slate-500" />
            }
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end">
          <Can permission="quotations.update">
            <DropdownMenuItem
              disabled={!canSend}
              title={canSend ? undefined : 'Chỉ gửi được báo giá đang ở trạng thái Nháp'}
              onClick={() => onTransition('Send')}
            >
              <Send className="mr-2 h-4 w-4 text-cyan-600" /> Gửi
            </DropdownMenuItem>
            <DropdownMenuItem
              disabled={!canConfirm}
              title={canConfirm ? undefined : 'Chỉ xác nhận được báo giá đã gửi'}
              onClick={() => onTransition('Confirm')}
            >
              <CheckCircle2 className="mr-2 h-4 w-4 text-emerald-600" /> Xác nhận
            </DropdownMenuItem>
          </Can>
          <Can permission="quotations.create">
            <DropdownMenuItem onClick={onClone}>
              <Copy className="mr-2 h-4 w-4 text-violet-600" /> Nhân bản
            </DropdownMenuItem>
          </Can>
          <Can permission="quotations.update">
            <DropdownMenuItem
              className="text-destructive focus:text-destructive"
              disabled={!canCancel}
              title={canCancel ? undefined : 'Báo giá đã hủy'}
              onClick={() => onTransition('Cancel')}
            >
              <Ban className="mr-2 h-4 w-4 text-red-600" /> Hủy
            </DropdownMenuItem>
          </Can>
          <Can permission="quotations.print">
            <DropdownMenuSeparator />
            <DropdownMenuItem
              disabled={busy}
              onClick={() => runExport('quotation-pdf', () => openQuotationPdf(q.id))}
            >
              {loadingExport === 'quotation-pdf'
                ? <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                : <Printer className="mr-2 h-4 w-4 text-indigo-600" />
              }
              Báo giá
            </DropdownMenuItem>
            <DropdownMenuItem
              disabled={busy}
              onClick={() => runExport('handover-pdf-with', () => openHandoverPdf(q.id, true))}
            >
              {loadingExport === 'handover-pdf-with'
                ? <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                : <Printer className="mr-2 h-4 w-4 text-indigo-600" />
              }
              Biên bản bàn giao (có tiền)
            </DropdownMenuItem>
            <DropdownMenuItem
              disabled={busy}
              onClick={() => runExport('handover-pdf-no', () => openHandoverPdf(q.id, false))}
            >
              {loadingExport === 'handover-pdf-no'
                ? <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                : <Printer className="mr-2 h-4 w-4 text-indigo-600" />
              }
              Biên bản bàn giao (không tiền)
            </DropdownMenuItem>
            <DropdownMenuSeparator />
            <DropdownMenuItem
              disabled={busy}
              onClick={() => runExport('quotation-excel', () => downloadQuotationExcel(q.id, q.code))}
            >
              {loadingExport === 'quotation-excel'
                ? <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                : <FileSpreadsheet className="mr-2 h-4 w-4 text-emerald-700" />
              }
              Báo giá
            </DropdownMenuItem>
            <DropdownMenuItem
              disabled={busy}
              onClick={() => runExport('handover-excel-with', () => downloadHandoverExcel(q.id, q.code, true))}
            >
              {loadingExport === 'handover-excel-with'
                ? <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                : <FileSpreadsheet className="mr-2 h-4 w-4 text-emerald-700" />
              }
              Biên bản bàn giao (có tiền)
            </DropdownMenuItem>
            <DropdownMenuItem
              disabled={busy}
              onClick={() => runExport('handover-excel-no', () => downloadHandoverExcel(q.id, q.code, false))}
            >
              {loadingExport === 'handover-excel-no'
                ? <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                : <FileSpreadsheet className="mr-2 h-4 w-4 text-emerald-700" />
              }
              Biên bản bàn giao (không tiền)
            </DropdownMenuItem>
          </Can>
        </DropdownMenuContent>
      </DropdownMenu>
    </div>
  );
}

export function QuotationListPage() {
  const navigate = useNavigate();
  const [search, setSearch] = useSearchParamString('q');
  const [page, setPage] = useSearchParamNumber('page', 1);
  const [sizeParam, setSizeParam] = useSearchParamNumber('size', DEFAULT_PAGE_SIZE);
  const [statusParam, setStatusParam] = useSearchParamString('status');
  const [fromDate] = useSearchParamString('from');
  const [toDate] = useSearchParamString('to');
  const [, setDateRangeParams] = useSearchParams();
  const [ownerIdsParam, setOwnerIdsParam] = useSearchParamString('ownerUserIds');
  const [revDateFrom] = useSearchParamString('revDateFrom');
  const [revDateTo] = useSearchParamString('revDateTo');
  const [dlvDateFrom] = useSearchParamString('dlvDateFrom');
  const [dlvDateTo] = useSearchParamString('dlvDateTo');
  const debouncedSearch = useDebouncedValue(search, 300);
  const hasViewAll = useAuthStore((s) => s.hasPermission('quotations.view_all'));
  const canViewCost = useAuthStore((s) => s.hasPermission('quotations.view_cost'));
  const currentUser = useAuthStore((s) => s.user);
  const isAdmin = useAuthStore((s) => s.isInRole('ADMIN'));

  const ownerInitialized = useRef(false);
  useEffect(() => {
    if (ownerInitialized.current || !hasViewAll || isAdmin || !currentUser?.id || ownerIdsParam) return;
    ownerInitialized.current = true;
    setOwnerIdsParam(currentUser.id);
  }, [hasViewAll, isAdmin, currentUser?.id, ownerIdsParam, setOwnerIdsParam]);

  const pageSize = (PAGE_SIZE_OPTIONS as readonly number[]).includes(sizeParam)
    ? sizeParam
    : DEFAULT_PAGE_SIZE;

  const statuses = useMemo<QuotationStatus[]>(
    () =>
      statusParam
        ? statusParam
            .split(',')
            .filter((s): s is QuotationStatus => VALID_STATUSES.has(s as QuotationStatus))
        : [...DEFAULT_ACTIVE_STATUSES],
    [statusParam],
  );

  const ownerIds = useMemo<string[]>(
    () => (hasViewAll ? parseOwnerIds(ownerIdsParam) : []),
    [hasViewAll, ownerIdsParam],
  );

  const [pendingTransition, setPendingTransition] = useState<{
    item: QuotationListItem;
    action: QuotationAction;
  } | null>(null);

  const [columnSizing, setColumnSizing] = useColumnSizingPersist(
    QUOTATION_LIST_STORAGE_KEY,
    QUOTATION_LIST_COLUMN_SIZING_DEFAULTS,
  );

  const { data, isLoading, isFetching, isError, error } = useQuotations({
    page,
    pageSize,
    search: debouncedSearch || undefined,
    statuses: statuses.length > 0 ? statuses : undefined,
    ownerUserIds: ownerIds.length > 0 ? ownerIds : undefined,
    from: fromDate || undefined,
    to: toDate || undefined,
    revenueDateFrom: revDateFrom || undefined,
    revenueDateTo: revDateTo || undefined,
    deliveryDateFrom: dlvDateFrom || undefined,
    deliveryDateTo: dlvDateTo || undefined,
  });
  const transition = useTransitionQuotation();
  const clone = useCloneQuotation();

  const ownersQuery = useQuotationOwners({ enabled: hasViewAll });
  const ownerOptions = useMemo(() => {
    const list = ownersQuery.data ?? [];
    return list.map((o) => ({
      value: o.id,
      label: o.isDeleted ? `${o.fullName} (đã nghỉ)` : o.fullName,
    }));
  }, [ownersQuery.data]);

  const allTotals = useMemo(() => {
    return {
      subtotal: data?.aggregates?.subtotal ?? 0,
      discount: data?.aggregates?.discount ?? 0,
      freight: data?.aggregates?.freight ?? 0,
      total: data?.aggregates?.total ?? 0,
      advancePayment: data?.aggregates?.advancePayment ?? 0,
      totalCost: canViewCost ? (data?.aggregates?.totalCost ?? null) : null,
      grossProfit: canViewCost ? (data?.aggregates?.grossProfit ?? null) : null,
    };
  }, [canViewCost, data?.aggregates]);

  const columns = useMemo<ColumnDef<QuotationListItem>[]>(
    () => [
      {
        header: 'Số báo giá',
        accessorKey: 'code',
        size: QUOTATION_LIST_COLUMN_SIZING_DEFAULTS.code,
        minSize: COLUMN_MIN_SIZE,
      },
      {
        header: 'Ngày',
        accessorKey: 'quotationDate',
        size: QUOTATION_LIST_COLUMN_SIZING_DEFAULTS.quotationDate,
        minSize: COLUMN_MIN_SIZE,
        cell: ({ row }) => formatDate(row.original.quotationDate),
      },
      {
        header: 'N. Doanh thu',
        accessorKey: 'revenueDate',
        size: QUOTATION_LIST_COLUMN_SIZING_DEFAULTS.revenueDate,
        minSize: COLUMN_MIN_SIZE,
        cell: ({ row }) => formatDate(row.original.revenueDate),
      },
      {
        header: 'Ngày Giao',
        accessorKey: 'deliveryDate',
        size: QUOTATION_LIST_COLUMN_SIZING_DEFAULTS.deliveryDate,
        minSize: COLUMN_MIN_SIZE,
        cell: ({ row }) => formatDate(row.original.deliveryDate),
      },
      {
        header: 'Khách hàng',
        accessorKey: 'customerName',
        size: QUOTATION_LIST_COLUMN_SIZING_DEFAULTS.customerName,
        minSize: COLUMN_MIN_SIZE,
        cell: ({ row }) => (
          <span
            className="block max-w-[160px] truncate"
            title={row.original.customerName}
          >
            {row.original.customerName}
          </span>
        ),
      },
      {
        header: () => moneyHeader('Tổng tiền hàng'),
        accessorKey: 'subtotal',
        size: QUOTATION_LIST_COLUMN_SIZING_DEFAULTS.subtotal,
        minSize: COLUMN_MIN_SIZE,
        cell: ({ row }) => moneyCell(row.original.subtotal),
      },
      {
        header: () => moneyHeader('Chiết khấu'),
        accessorKey: 'discount',
        size: QUOTATION_LIST_COLUMN_SIZING_DEFAULTS.discount,
        minSize: COLUMN_MIN_SIZE,
        cell: ({ row }) => moneyCell(row.original.discount),
      },
      {
        header: () => moneyHeader('Vận chuyển'),
        accessorKey: 'freight',
        size: QUOTATION_LIST_COLUMN_SIZING_DEFAULTS.freight,
        minSize: COLUMN_MIN_SIZE,
        cell: ({ row }) => moneyCell(row.original.freight),
      },
      {
        header: () => <div className="text-right">% VAT</div>,
        accessorKey: 'taxRate',
        size: QUOTATION_LIST_COLUMN_SIZING_DEFAULTS.taxRate,
        minSize: COLUMN_MIN_SIZE,
        cell: ({ row }) => (
          <span className="block text-right tabular-nums">
            {row.original.taxRate > 0 ? `${row.original.taxRate}%` : '—'}
          </span>
        ),
      },
      {
        header: () => moneyHeader('Tiền thuế'),
        accessorKey: 'taxAmount',
        size: QUOTATION_LIST_COLUMN_SIZING_DEFAULTS.taxAmount,
        minSize: COLUMN_MIN_SIZE,
        cell: ({ row }) => moneyCell(row.original.taxAmount > 0 ? row.original.taxAmount : null),
      },
      {
        header: () => moneyHeader('Tổng cộng'),
        accessorKey: 'total',
        size: QUOTATION_LIST_COLUMN_SIZING_DEFAULTS.total,
        minSize: COLUMN_MIN_SIZE,
        cell: ({ row }) => moneyCell(row.original.total),
      },
      {
        header: () => moneyHeader('Tạm ứng'),
        accessorKey: 'advancePayment',
        size: QUOTATION_LIST_COLUMN_SIZING_DEFAULTS.advancePayment,
        minSize: COLUMN_MIN_SIZE,
        cell: ({ row }) => moneyCell(row.original.advancePayment),
      },
      ...(canViewCost
        ? [
            {
              header: () => moneyHeader('Tổng nhập'),
              accessorKey: 'totalCost',
              size: QUOTATION_LIST_COLUMN_SIZING_DEFAULTS.totalCost,
              minSize: COLUMN_MIN_SIZE,
              cell: ({ row }: { row: { original: QuotationListItem } }) => moneyCell(row.original.totalCost),
            } as ColumnDef<QuotationListItem>,
            {
              header: () => moneyHeader('Tổng LN'),
              accessorKey: 'grossProfit',
              size: QUOTATION_LIST_COLUMN_SIZING_DEFAULTS.grossProfit,
              minSize: COLUMN_MIN_SIZE,
              cell: ({ row }: { row: { original: QuotationListItem } }) => moneyCell(row.original.grossProfit),
            } as ColumnDef<QuotationListItem>,
          ]
        : []),
      {
        header: 'Trạng thái',
        accessorKey: 'status',
        size: QUOTATION_LIST_COLUMN_SIZING_DEFAULTS.status,
        minSize: COLUMN_MIN_SIZE,
        cell: ({ row }) => <StatusPill status={row.original.status} />,
      },
      ...(hasViewAll
        ? [{
            header: 'Chủ sở hữu',
            id: 'owner',
            size: QUOTATION_LIST_COLUMN_SIZING_DEFAULTS.owner,
            minSize: COLUMN_MIN_SIZE,
            cell: ({ row }: { row: { original: QuotationListItem } }) => (
              <span>
                {row.original.ownerFullName ?? '—'}
                {row.original.isOwnerDeleted && (
                  <span className="ml-2 rounded bg-amber-100 px-2 py-0.5 text-xs text-amber-900">
                    đã nghỉ
                  </span>
                )}
              </span>
            ),
          } as ColumnDef<QuotationListItem>]
        : []),
      {
        header: 'Người lập',
        accessorKey: 'createdByName',
        size: QUOTATION_LIST_COLUMN_SIZING_DEFAULTS.createdByName,
        minSize: COLUMN_MIN_SIZE,
      },
      {
        header: 'SĐT',
        accessorKey: 'contactPhone',
        size: QUOTATION_LIST_COLUMN_SIZING_DEFAULTS.contactPhone,
        minSize: COLUMN_MIN_SIZE,
      },
      {
        id: 'actions',
        header: '',
        size: QUOTATION_LIST_COLUMN_SIZING_DEFAULTS.actions,
        minSize: QUOTATION_LIST_COLUMN_SIZING_DEFAULTS.actions,
        enableResizing: false,
        cell: ({ row }) => (
          <QuotationActionsCell
            q={row.original}
            onTransition={(action) => setPendingTransition({ item: row.original, action })}
            onClone={() => {
              clone.mutate(row.original.id, {
                onSuccess: (cloned) => {
                  toast({ variant: 'success', title: 'Đã nhân bản báo giá', description: cloned.code });
                  navigate(`/quotations/${cloned.id}`);
                },
                onError: (err) =>
                  toast({ variant: 'destructive', title: 'Không thể nhân bản', description: getErrorMessage(err) }),
              });
            }}
          />
        ),
      },
    ],
    [canViewCost, hasViewAll, clone, navigate],
  );

  const table = useReactTable({
    data: data?.items ?? [],
    columns,
    state: { columnSizing },
    onColumnSizingChange: setColumnSizing,
    columnResizeMode: 'onChange',
    enableColumnResizing: true,
    getCoreRowModel: getCoreRowModel(),
  });

  const onConfirmTransition = () => {
    if (!pendingTransition) return;
    const { item, action } = pendingTransition;
    transition.mutate(
      { id: item.id, action },
      {
        onSuccess: () => {
          toast({ variant: 'success', title: successToastTitle(action), description: item.code });
          setPendingTransition(null);
        },
        onError: (err) => {
          toast({
            variant: 'destructive',
            title: errorToastTitle(action),
            description: getErrorMessage(err),
          });
        },
      },
    );
  };

  return (
    <div className="flex h-full min-h-0 flex-col gap-4">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold">Báo giá</h1>
          <p className="text-sm text-muted-foreground">Quản lý báo giá gửi khách</p>
        </div>
        <Can permission="quotations.create">
          <Button asChild>
            <Link to="/quotations/new">
              <Plus className="mr-2 h-4 w-4 text-cyan-600" /> Thêm báo giá
            </Link>
          </Button>
        </Can>
      </div>

      <Card className="flex flex-1 min-h-0 flex-col">
        <CardContent className="flex flex-1 min-h-0 flex-col gap-3 p-4">
          <div className="flex flex-wrap items-center gap-2">
            <div className="relative max-w-sm flex-1">
              <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-500" />
              <Input
                placeholder="Tìm theo số / tên khách..."
                value={search}
                onChange={(e) => { setSearch(e.target.value); if (page !== 1) setPage(1); }}
                className="pl-9"
              />
            </div>

            <MultiSelect<QuotationStatus>
              options={STATUS_OPTIONS}
              value={statuses}
              onChange={(next) => {
                setStatusParam(next.join(','));
                if (page !== 1) setPage(1);
              }}
              placeholder="Trạng thái"
              triggerClassName="w-44"
              ariaLabel="Trạng thái"
            />

            {hasViewAll && (
              <MultiSelect<string>
                options={ownerOptions}
                value={ownerIds}
                onChange={(next) => {
                  setOwnerIdsParam(next.join(','));
                  if (page !== 1) setPage(1);
                }}
                placeholder="Chủ sở hữu"
                triggerClassName="w-56"
                ariaLabel="Chủ sở hữu"
              />
            )}

            <div className="flex flex-col gap-0.5">
              <span className="text-xs text-muted-foreground leading-none">Ngày báo giá</span>
              <QuotationDateFilter
                from={fromDate}
                to={toDate}
                onChange={(f, t) => {
                  setDateRangeParams((prev) => {
                    const out = new URLSearchParams(prev);
                    if (!f) out.delete('from'); else out.set('from', f);
                    if (!t) out.delete('to'); else out.set('to', t);
                    out.delete('page');
                    return out;
                  }, { replace: true });
                }}
              />
            </div>

            <div className="flex flex-col gap-0.5">
              <span className="text-xs text-muted-foreground leading-none">N. Doanh thu</span>
              <QuotationDateFilter
                from={revDateFrom}
                to={revDateTo}
                onChange={(f, t) => {
                  setDateRangeParams((prev) => {
                    const out = new URLSearchParams(prev);
                    if (!f) out.delete('revDateFrom'); else out.set('revDateFrom', f);
                    if (!t) out.delete('revDateTo'); else out.set('revDateTo', t);
                    out.delete('page');
                    return out;
                  }, { replace: true });
                }}
              />
            </div>

            <div className="flex flex-col gap-0.5">
              <span className="text-xs text-muted-foreground leading-none">Ngày Giao</span>
              <QuotationDateFilter
                from={dlvDateFrom}
                to={dlvDateTo}
                onChange={(f, t) => {
                  setDateRangeParams((prev) => {
                    const out = new URLSearchParams(prev);
                    if (!f) out.delete('dlvDateFrom'); else out.set('dlvDateFrom', f);
                    if (!t) out.delete('dlvDateTo'); else out.set('dlvDateTo', t);
                    out.delete('page');
                    return out;
                  }, { replace: true });
                }}
              />
            </div>
          </div>

          {isError && (
            <div className="rounded-md border border-destructive/30 bg-destructive/10 p-3 text-sm text-destructive">
              {getErrorMessage(error)}
            </div>
          )}

          <div className="relative flex-1 min-h-0 rounded-md border overflow-hidden">
            {isFetching && !isLoading && (
              <div
                className="absolute inset-0 z-20 flex items-center justify-center bg-background/60 backdrop-blur-[1px]"
                role="status"
                aria-live="polite"
              >
                <div className="flex items-center gap-2 rounded-md border bg-background px-3 py-2 text-sm text-muted-foreground shadow-sm">
                  <Loader2 className="h-4 w-4 animate-spin text-blue-600" />
                  Đang tải...
                </div>
              </div>
            )}
            <Table
              containerClassName="h-full"
              className="min-w-max table-fixed"
              style={{ width: table.getTotalSize() }}
            >
              <TableColGroup widths={table.getHeaderGroups()[0].headers.map((h) => h.getSize())} />
              <TableHeader className="sticky top-0 z-10">
                {table.getHeaderGroups().map((hg) => (
                  <TableRow key={hg.id}>
                    {hg.headers.map((h) => (
                      <TableHead
                        key={h.id}
                        className={h.column.id === 'actions' ? 'sticky right-0 z-20 bg-background shadow-[-2px_0_4px_-1px_rgba(0,0,0,0.1)]' : ''}
                        resizable={
                          h.column.getCanResize()
                            ? { onResizeStart: h.getResizeHandler(), isResizing: h.column.getIsResizing() }
                            : undefined
                        }
                      >
                        {flexRender(h.column.columnDef.header, h.getContext())}
                      </TableHead>
                    ))}
                  </TableRow>
                ))}
              </TableHeader>
              <TableBody>
                {isLoading ? (
                  <TableRow>
                    <TableCell colSpan={columns.length} className="h-24 text-center text-muted-foreground">
                      Đang tải...
                    </TableCell>
                  </TableRow>
                ) : table.getRowModel().rows.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={columns.length} className="h-24 text-center text-muted-foreground">
                      Chưa có báo giá nào.
                    </TableCell>
                  </TableRow>
                ) : (
                  table.getRowModel().rows.map((row) => (
                    <TableRow key={row.id} className="even:bg-muted/50">
                      {row.getVisibleCells().map((c) => (
                        <TableCell
                          key={c.id}
                          className={c.column.id === 'actions' ? 'sticky right-0 bg-background shadow-[-2px_0_4px_-1px_rgba(0,0,0,0.1)]' : ''}
                        >
                          {flexRender(c.column.columnDef.cell, c.getContext())}
                        </TableCell>
                      ))}
                    </TableRow>
                  ))
                )}
              </TableBody>
            </Table>
          </div>

          <ListFooter
            totalItems={data?.totalItems ?? 0}
            aggregates={allTotals}
            page={page}
            totalPages={data?.totalPages ?? 0}
            pageSize={pageSize}
            pageSizeOptions={PAGE_SIZE_OPTIONS}
            hasPrev={data?.hasPreviousPage ?? false}
            hasNext={data?.hasNextPage ?? false}
            onPageChange={setPage}
            onPageSizeChange={(next) => {
              setSizeParam(next);
              if (page !== 1) setPage(1);
            }}
            showCostProfit={canViewCost}
            loading={isFetching}
            errored={isError}
          />
        </CardContent>
      </Card>

      <ConfirmDialog
        open={!!pendingTransition}
        onOpenChange={(open) => !open && setPendingTransition(null)}
        title={pendingTransition ? dialogContent(pendingTransition).title : ''}
        description={pendingTransition ? dialogContent(pendingTransition).description : null}
        destructive={pendingTransition?.action === 'Cancel'}
        confirmLabel={pendingTransition ? dialogContent(pendingTransition).confirmLabel : undefined}
        loading={transition.isPending}
        onConfirm={onConfirmTransition}
      />
    </div>
  );
}

function dialogContent(p: { item: QuotationListItem; action: QuotationAction }): {
  title: string;
  confirmLabel: string;
  description: React.ReactNode;
} {
  switch (p.action) {
    case 'Send':
      return {
        title: 'Gửi báo giá?',
        confirmLabel: 'Gửi',
        description: (
          <>
            Gửi báo giá <strong>{p.item.code}</strong> cho khách hàng. Sau khi gửi, báo giá chuyển sang trạng thái "Đã gửi".
          </>
        ),
      };
    case 'Confirm':
      return {
        title: 'Xác nhận báo giá?',
        confirmLabel: 'Xác nhận',
        description: (
          <>
            Xác nhận báo giá <strong>{p.item.code}</strong>. Doanh thu sẽ được ghi nhận cho sale phụ trách.
          </>
        ),
      };
    case 'AccountingConfirm':
      return {
        title: 'KT xác nhận đã nhận tiền?',
        confirmLabel: 'KT xác nhận',
        description: (
          <>
            Báo giá <strong>{p.item.code}</strong> sẽ chuyển sang trạng thái "KT xác nhận".
          </>
        ),
      };
    case 'Cancel':
      return {
        title: 'Hủy báo giá?',
        confirmLabel: 'Hủy báo giá',
        description:
          p.item.status === 'Confirmed' ? (
            <>
              Báo giá <strong>{p.item.code}</strong> đã được xác nhận — hủy sẽ trừ doanh thu của sale. Cần quyền <code>quotations.cancel_confirmed</code>. Tiếp tục?
            </>
          ) : (
            <>
              Bạn chắc chắn muốn hủy báo giá <strong>{p.item.code}</strong>? Báo giá đã hủy không thể chỉnh sửa lại.
            </>
          ),
      };
  }
}

function successToastTitle(action: QuotationAction): string {
  switch (action) {
    case 'Send': return 'Đã gửi báo giá';
    case 'Confirm': return 'Đã xác nhận báo giá';
    case 'AccountingConfirm': return 'KT đã xác nhận';
    case 'Cancel': return 'Đã hủy báo giá';
  }
}

function errorToastTitle(action: QuotationAction): string {
  switch (action) {
    case 'Send': return 'Không thể gửi';
    case 'Confirm': return 'Không thể xác nhận';
    case 'AccountingConfirm': return 'Không thể KT xác nhận';
    case 'Cancel': return 'Không thể hủy';
  }
}
