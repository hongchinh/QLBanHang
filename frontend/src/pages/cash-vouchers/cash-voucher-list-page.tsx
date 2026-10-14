import { useEffect, useMemo, useState } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { flexRender, getCoreRowModel, useReactTable, type ColumnDef, type Table as TanstackTable } from '@tanstack/react-table';
import { format } from 'date-fns';
import { Loader2, Plus, Search } from 'lucide-react';
import { useCashVouchers } from '@/features/cash-vouchers/hooks';
import type {
  CashPartnerSearchItem,
  CashVoucherListItem,
  CashVoucherType,
} from '@/features/cash-vouchers/types';
import type { DocumentStatusFilter } from '@/features/cash-common/types';
import { useFunds } from '@/features/funds/hooks';
import { useCashReasons } from '@/features/cash-reasons/hooks';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { MultiSelect } from '@/components/ui/multi-select';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Table, TableBody, TableCell, TableColGroup, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Can } from '@/components/auth/can';
import { SelectionCheckbox } from '@/components/ui/selection-checkbox';
import { VoucherPrintButton } from '@/components/voucher-print/voucher-print-button';
import { getErrorMessage } from '@/lib/api-client';
import { useColumnSizingPersist } from '@/lib/use-column-sizing-persist';
import { useRowSelection } from '@/lib/use-row-selection';
import { useDebouncedValue } from '@/lib/use-debounced-value';
import { useSearchParamNumber, useSearchParamString } from '@/lib/use-search-param-state';
import { QuotationDateFilter } from '@/pages/quotations/components/quotation-date-filter';
import { CashPartnerFilter } from './components/cash-partner-filter';
import { CASH_VOUCHER_LABELS } from './labels';

const PAGE_SIZE_OPTIONS = [10, 20, 50, 100] as const;
const DEFAULT_PAGE_SIZE = 20;
const COLUMN_MIN_SIZE = 60;
const currency = new Intl.NumberFormat('vi-VN');

const STATUS_OPTIONS: ReadonlyArray<{ value: DocumentStatusFilter; label: string }> = [
  { value: 'Active', label: 'Đang hiệu lực' },
  { value: 'Cancelled', label: 'Đã hủy' },
  { value: 'All', label: 'Tất cả' },
];
const VALID_STATUSES = new Set<string>(STATUS_OPTIONS.map((o) => o.value));

const COLUMN_SIZING_DEFAULTS: Record<string, number> = {
  select: 36,
  code: 150,
  voucherAt: 130,
  fundName: 140,
  reasonName: 160,
  partnerName: 200,
  personName: 150,
  description: 220,
  total: 120,
  status: 110,
  ownerName: 140,
  actions: 48,
};

type RowSelection = ReturnType<typeof useRowSelection>;

// The selection travels through table meta so the column definitions (and their cell components) stay stable.
function selectionOf(table: TanstackTable<CashVoucherListItem>) {
  return (table.options.meta as { selection: RowSelection }).selection;
}

const SELECT_CLASS = 'h-9 rounded-md border border-input bg-background px-2 text-sm';

function textColumn(
  key: keyof CashVoucherListItem & string,
  label: string,
  titleKey?: keyof CashVoucherListItem & string,
): ColumnDef<CashVoucherListItem> {
  return {
    id: key,
    header: label,
    size: COLUMN_SIZING_DEFAULTS[key],
    minSize: COLUMN_MIN_SIZE,
    cell: ({ row }) => {
      const value = row.original[key];
      const title = titleKey ? row.original[titleKey] : value;
      return (
        <span className="block truncate" title={title ? String(title) : undefined}>
          {value ? String(value) : ''}
        </span>
      );
    },
  };
}

function formatDateTime(iso: string) {
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? iso : format(d, 'dd/MM/yyyy HH:mm');
}

// There is no owners endpoint for cash vouchers: the options are the owners met in the loaded rows,
// kept across pages and filters so a selected owner stays listed.
function useOwnerOptions(items: readonly CashVoucherListItem[] | undefined, selectedIds: readonly string[]) {
  const [known, setKnown] = useState<ReadonlyMap<string, string>>(() => new Map());
  useEffect(() => {
    if (!items?.length) return;
    setKnown((prev) => {
      const missing = items.filter((i) => !prev.has(i.ownerUserId));
      if (missing.length === 0) return prev;
      const next = new Map(prev);
      for (const i of missing) next.set(i.ownerUserId, i.ownerName ?? i.ownerUserId);
      return next;
    });
  }, [items]);

  return useMemo(() => {
    const options = [...known].map(([value, label]) => ({ value, label }));
    for (const id of selectedIds) {
      if (!known.has(id)) options.push({ value: id, label: id });
    }
    return options.sort((a, b) => a.label.localeCompare(b.label, 'vi'));
  }, [known, selectedIds]);
}

export function CashVoucherListPage({ type }: { type: CashVoucherType }) {
  const labels = CASH_VOUCHER_LABELS[type];
  const navigate = useNavigate();
  const [, setParams] = useSearchParams();
  // Filter changes go through updateParams (one URL write that also resets the page).
  const [search] = useSearchParamString('q');
  const [page, setPage] = useSearchParamNumber('page', 1);
  const [sizeParam] = useSearchParamNumber('size', DEFAULT_PAGE_SIZE);
  const [fromDate] = useSearchParamString('from');
  const [toDate] = useSearchParamString('to');
  const [fundId] = useSearchParamString('fundId');
  const [reasonId] = useSearchParamString('reasonId');
  const [partnerId] = useSearchParamString('partnerId');
  const [partnerName] = useSearchParamString('partnerName');
  const [statusParam] = useSearchParamString('status');
  const [ownersParam] = useSearchParamString('owners');
  const debouncedSearch = useDebouncedValue(search, 300);

  const pageSize = (PAGE_SIZE_OPTIONS as readonly number[]).includes(sizeParam) ? sizeParam : DEFAULT_PAGE_SIZE;
  const status: DocumentStatusFilter = VALID_STATUSES.has(statusParam)
    ? (statusParam as DocumentStatusFilter)
    : 'Active';
  const ownerIds = useMemo(() => ownersParam.split(',').filter(Boolean), [ownersParam]);

  const listParams = {
    type,
    page,
    pageSize,
    search: debouncedSearch || undefined,
    from: fromDate || undefined,
    to: toDate || undefined,
    fundId: fundId || undefined,
    reasonId: reasonId || undefined,
    partnerId: partnerId || undefined,
    status,
    ownerIds: ownerIds.length > 0 ? ownerIds : undefined,
  };
  const { data, isLoading, isFetching, isError, error } = useCashVouchers(listParams);
  const items = data?.items ?? [];
  const selection = useRowSelection(
    items.map((v) => v.id),
    JSON.stringify(listParams),
  );

  const { data: allFunds = [] } = useFunds();
  const funds = useMemo(() => [...allFunds].sort((a, b) => a.code.localeCompare(b.code)), [allFunds]);
  // Automatic reasons (TPX / CPN) are filterable too.
  const { data: allReasons = [] } = useCashReasons({ type, includeAuto: true });
  const reasons = allReasons.filter((r) => r.type === type);
  const ownerOptions = useOwnerOptions(data?.items, ownerIds);

  const [columnSizing, setColumnSizing] = useColumnSizingPersist(
    `cash-voucher-list-${type}`,
    COLUMN_SIZING_DEFAULTS,
  );

  const columns = useMemo<ColumnDef<CashVoucherListItem>[]>(
    () => [
      {
        id: 'select',
        size: COLUMN_SIZING_DEFAULTS.select,
        minSize: COLUMN_SIZING_DEFAULTS.select,
        enableResizing: false,
        header: ({ table }) => {
          const s = selectionOf(table);
          return (
            <SelectionCheckbox
              checked={s.allSelected}
              indeterminate={s.someSelected}
              onCheckedChange={s.toggleAll}
              aria-label="Chọn tất cả phiếu trong trang"
            />
          );
        },
        cell: ({ row, table }) => {
          const s = selectionOf(table);
          return (
            <SelectionCheckbox
              checked={s.isSelected(row.original.id)}
              onCheckedChange={(checked) => s.toggle(row.original.id, checked)}
              aria-label={`Chọn ${row.original.code}`}
            />
          );
        },
      },
      {
        id: 'code',
        header: 'Số phiếu',
        size: COLUMN_SIZING_DEFAULTS.code,
        minSize: COLUMN_MIN_SIZE,
        cell: ({ row }) => (
          <span className="flex items-center gap-1.5">
            <Link
              to={`${labels.basePath}/${row.original.id}`}
              className="font-medium text-blue-700 hover:underline"
              onClick={(e) => e.stopPropagation()}
            >
              {row.original.code}
            </Link>
            {row.original.isManaged && row.original.sourceStockVoucherId && (
              <Link
                to={`${labels.stockVoucherBasePath}/${row.original.sourceStockVoucherId}`}
                title={`Theo phiếu kho ${row.original.sourceStockVoucherCode ?? ''}`.trim()}
                onClick={(e) => e.stopPropagation()}
              >
                <Badge variant="secondary" className="whitespace-nowrap hover:underline">
                  Tự động
                </Badge>
              </Link>
            )}
          </span>
        ),
      },
      {
        id: 'voucherAt',
        header: 'Ngày',
        size: COLUMN_SIZING_DEFAULTS.voucherAt,
        minSize: COLUMN_MIN_SIZE,
        cell: ({ row }) => <span className="tabular-nums">{formatDateTime(row.original.voucherAt)}</span>,
      },
      textColumn('fundName', 'Quỹ', 'fundCode'),
      textColumn('reasonName', 'Lý do'),
      textColumn('partnerName', 'Đối tượng', 'partnerCode'),
      textColumn('personName', labels.personLabel),
      textColumn('description', 'Diễn giải'),
      {
        id: 'total',
        header: () => <div className="text-right">Số tiền</div>,
        size: COLUMN_SIZING_DEFAULTS.total,
        minSize: COLUMN_MIN_SIZE,
        cell: ({ row }) => (
          <span className="block text-right tabular-nums">{currency.format(row.original.total)}</span>
        ),
      },
      {
        id: 'status',
        header: 'Trạng thái',
        size: COLUMN_SIZING_DEFAULTS.status,
        minSize: COLUMN_MIN_SIZE,
        cell: ({ row }) =>
          row.original.status === 'Cancelled' ? (
            <Badge variant="destructive">Đã hủy</Badge>
          ) : (
            <Badge variant="success">Hiệu lực</Badge>
          ),
      },
      textColumn('ownerName', 'Người lập'),
      {
        id: 'actions',
        header: '',
        size: COLUMN_SIZING_DEFAULTS.actions,
        minSize: COLUMN_SIZING_DEFAULTS.actions,
        enableResizing: false,
        cell: ({ row }) => (
          <VoucherPrintButton
            variant="icon"
            kind="cash"
            type={type}
            vouchers={[{ id: row.original.id, code: row.original.code }]}
          />
        ),
      },
    ],
    [labels.basePath, labels.stockVoucherBasePath, labels.personLabel, type],
  );

  const table = useReactTable({
    data: items,
    columns,
    state: { columnSizing },
    meta: { selection },
    onColumnSizingChange: setColumnSizing,
    columnResizeMode: 'onChange',
    enableColumnResizing: true,
    getCoreRowModel: getCoreRowModel(),
  });

  // Several params at once, always back to page 1.
  function updateParams(patch: Record<string, string>) {
    setParams(
      (prev) => {
        const out = new URLSearchParams(prev);
        for (const [key, value] of Object.entries(patch)) {
          if (value) out.set(key, value);
          else out.delete(key);
        }
        out.delete('page');
        return out;
      },
      { replace: true },
    );
  }

  function handlePartnerSelect(p: CashPartnerSearchItem | null) {
    updateParams({ partnerId: p?.id ?? '', partnerName: p?.name ?? '' });
  }

  const totalPages = Math.max(data?.totalPages ?? 0, 1);

  return (
    <div className="flex h-full min-h-0 flex-col gap-4">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold">{labels.listTitle}</h1>
        <Can permission={`${labels.permissionPrefix}.create`}>
          <Button asChild>
            <Link to={`${labels.basePath}/new`}>
              <Plus className="mr-2 h-4 w-4 text-cyan-600" /> Thêm {labels.title.toLowerCase()}
            </Link>
          </Button>
        </Can>
      </div>

      <Card className="flex min-h-0 flex-1 flex-col">
        <CardContent className="flex min-h-0 flex-1 flex-col gap-3 p-4">
          <div className="flex flex-wrap items-end gap-2">
            <div className="relative max-w-xs flex-1">
              <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-500" />
              <Input
                placeholder="Tìm theo số phiếu / đối tượng..."
                value={search}
                onChange={(e) => updateParams({ q: e.target.value })}
                className="pl-9"
              />
            </div>

            <div className="flex flex-col gap-0.5">
              <span className="text-xs leading-none text-muted-foreground">Ngày</span>
              <QuotationDateFilter from={fromDate} to={toDate} onChange={(f, t) => updateParams({ from: f, to: t })} />
            </div>

            <select
              aria-label="Quỹ"
              className={SELECT_CLASS}
              value={fundId}
              onChange={(e) => updateParams({ fundId: e.target.value })}
            >
              <option value="">Tất cả quỹ</option>
              {funds.map((f) => (
                <option key={f.id} value={f.id}>
                  {f.code} – {f.name}
                </option>
              ))}
            </select>

            <select
              aria-label="Lý do"
              className={SELECT_CLASS}
              value={reasonId}
              onChange={(e) => updateParams({ reasonId: e.target.value })}
            >
              <option value="">Tất cả lý do</option>
              {reasons.map((r) => (
                <option key={r.id} value={r.id}>
                  {r.name}
                </option>
              ))}
            </select>

            <div className="w-60">
              <CashPartnerFilter
                type={type}
                value={partnerId ? { id: partnerId, code: partnerName || partnerId, name: partnerName } : null}
                onSelect={handlePartnerSelect}
              />
            </div>

            <select
              aria-label="Trạng thái"
              className={SELECT_CLASS}
              value={status}
              onChange={(e) => updateParams({ status: e.target.value })}
            >
              {STATUS_OPTIONS.map((o) => (
                <option key={o.value} value={o.value}>
                  {o.label}
                </option>
              ))}
            </select>

            <MultiSelect<string>
              options={ownerOptions}
              value={ownerIds}
              onChange={(next) => updateParams({ owners: next.join(',') })}
              placeholder="Người lập"
              triggerClassName="w-48"
              ariaLabel="Người lập"
            />
          </div>

          {isError && (
            <div className="rounded-md border border-destructive/30 bg-destructive/10 p-3 text-sm text-destructive">
              {getErrorMessage(error)}
            </div>
          )}

          {selection.selectedIds.length > 0 && (
            <div
              role="region"
              aria-label="Phiếu đã chọn"
              className="flex flex-wrap items-center gap-3 rounded-md border bg-muted/40 px-3 py-2 text-sm"
            >
              <span>Đã chọn {selection.selectedIds.length} phiếu</span>
              <VoucherPrintButton
                kind="cash"
                type={type}
                vouchers={items.filter((v) => selection.isSelected(v.id)).map((v) => ({ id: v.id, code: v.code }))}
              />
              <Button variant="ghost" size="sm" onClick={selection.clear}>
                Bỏ chọn
              </Button>
            </div>
          )}

          <div className="relative min-h-0 flex-1 overflow-hidden rounded-md border">
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
            <Table containerClassName="h-full" className="min-w-max table-fixed" style={{ width: table.getTotalSize() }}>
              <TableColGroup widths={table.getHeaderGroups()[0].headers.map((h) => h.getSize())} />
              <TableHeader className="sticky top-0 z-10">
                {table.getHeaderGroups().map((hg) => (
                  <TableRow key={hg.id}>
                    {hg.headers.map((h) => (
                      <TableHead
                        key={h.id}
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
                      Chưa có phiếu nào.
                    </TableCell>
                  </TableRow>
                ) : (
                  table.getRowModel().rows.map((row) => (
                    <TableRow
                      key={row.id}
                      className="cursor-pointer even:bg-muted/50"
                      onClick={(e) => {
                        // Clicks from portals (print menu) bubble through the React tree; ignore them.
                        if (e.currentTarget.contains(e.target as Node)) navigate(`${labels.basePath}/${row.original.id}`);
                      }}
                    >
                      {row.getVisibleCells().map((c) => (
                        <TableCell key={c.id}>{flexRender(c.column.columnDef.cell, c.getContext())}</TableCell>
                      ))}
                    </TableRow>
                  ))
                )}
              </TableBody>
            </Table>
          </div>

          <div className="flex flex-wrap items-center justify-between gap-2 border-t pt-3 text-sm">
            <div className="text-muted-foreground" role="group" aria-label="Tổng kết phiếu">
              Tổng <strong className="text-foreground">{data?.totalItems ?? 0}</strong> phiếu
              {' • '}
              <span className="font-semibold text-foreground">
                Tổng tiền{' '}
                {isFetching ? (
                  <span
                    className="inline-block h-3 w-16 animate-pulse rounded bg-muted align-middle"
                    aria-label="đang tải"
                  />
                ) : (
                  <span className="tabular-nums">
                    {isError || typeof data?.totalAmount !== 'number' ? '—' : currency.format(data.totalAmount)}
                  </span>
                )}
              </span>
              {/* The server total for status All includes cancelled vouchers; it is shown as is. */}
              {status === 'All' && <span className="ml-1 italic">(gồm cả phiếu đã hủy)</span>}
            </div>
            <div className="flex items-center gap-2">
              <Select
                value={String(pageSize)}
                onValueChange={(v) => updateParams({ size: Number(v) === DEFAULT_PAGE_SIZE ? '' : v })}
              >
                <SelectTrigger className="w-20" aria-label="Số dòng mỗi trang">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {PAGE_SIZE_OPTIONS.map((s) => (
                    <SelectItem key={s} value={String(s)}>
                      {s}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <span className="text-muted-foreground">
                Trang {page}/{totalPages}
              </span>
              <Button
                variant="outline"
                size="sm"
                disabled={!data?.hasPreviousPage}
                onClick={() => setPage(page - 1)}
              >
                Trước
              </Button>
              <Button variant="outline" size="sm" disabled={!data?.hasNextPage} onClick={() => setPage(page + 1)}>
                Sau
              </Button>
            </div>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
