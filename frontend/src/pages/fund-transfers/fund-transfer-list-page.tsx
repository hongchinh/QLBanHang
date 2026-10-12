import { useMemo } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { flexRender, getCoreRowModel, useReactTable, type ColumnDef } from '@tanstack/react-table';
import { format } from 'date-fns';
import { Loader2, Plus, Search } from 'lucide-react';
import { useFundTransfers } from '@/features/fund-transfers/hooks';
import type { FundTransferListItem } from '@/features/fund-transfers/types';
import type { DocumentStatusFilter } from '@/features/cash-common/types';
import { useFunds } from '@/features/funds/hooks';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Table, TableBody, TableCell, TableColGroup, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Can } from '@/components/auth/can';
import { getErrorMessage } from '@/lib/api-client';
import { useColumnSizingPersist } from '@/lib/use-column-sizing-persist';
import { useDebouncedValue } from '@/lib/use-debounced-value';
import { useSearchParamNumber, useSearchParamString } from '@/lib/use-search-param-state';
import { QuotationDateFilter } from '@/pages/quotations/components/quotation-date-filter';

const BASE_PATH = '/fund-transfers';
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
  code: 110,
  voucherAt: 130,
  fromFund: 200,
  toFund: 200,
  amount: 130,
  description: 240,
  status: 110,
  ownerName: 140,
};

const SELECT_CLASS = 'h-9 rounded-md border border-input bg-background px-2 text-sm';

function textColumn(
  id: string,
  label: string,
  value: (item: FundTransferListItem) => string | null | undefined,
): ColumnDef<FundTransferListItem> {
  return {
    id,
    header: label,
    size: COLUMN_SIZING_DEFAULTS[id],
    minSize: COLUMN_MIN_SIZE,
    cell: ({ row }) => {
      const text = value(row.original) ?? '';
      return (
        <span className="block truncate" title={text || undefined}>
          {text}
        </span>
      );
    },
  };
}

function formatDateTime(iso: string) {
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? iso : format(d, 'dd/MM/yyyy HH:mm');
}

export function FundTransferListPage() {
  const navigate = useNavigate();
  const [, setParams] = useSearchParams();
  // Filter changes go through updateParams (one URL write that also resets the page).
  const [search] = useSearchParamString('q');
  const [page, setPage] = useSearchParamNumber('page', 1);
  const [sizeParam] = useSearchParamNumber('size', DEFAULT_PAGE_SIZE);
  const [fromDate] = useSearchParamString('from');
  const [toDate] = useSearchParamString('to');
  const [fundId] = useSearchParamString('fundId');
  const [statusParam] = useSearchParamString('status');
  const debouncedSearch = useDebouncedValue(search, 300);

  const pageSize = (PAGE_SIZE_OPTIONS as readonly number[]).includes(sizeParam) ? sizeParam : DEFAULT_PAGE_SIZE;
  const status: DocumentStatusFilter = VALID_STATUSES.has(statusParam)
    ? (statusParam as DocumentStatusFilter)
    : 'Active';

  const { data, isLoading, isFetching, isError, error } = useFundTransfers({
    page,
    pageSize,
    search: debouncedSearch || undefined,
    from: fromDate || undefined,
    to: toDate || undefined,
    fundId: fundId || undefined,
    status,
  });

  // Every fund of the branch, inactive ones included: old transfers can still be filtered by them.
  const { data: funds = [] } = useFunds();
  const fundOptions = useMemo(() => [...funds].sort((a, b) => a.code.localeCompare(b.code)), [funds]);

  const [columnSizing, setColumnSizing] = useColumnSizingPersist('fund-transfer-list', COLUMN_SIZING_DEFAULTS);

  const columns = useMemo<ColumnDef<FundTransferListItem>[]>(
    () => [
      {
        id: 'code',
        header: 'Số phiếu',
        size: COLUMN_SIZING_DEFAULTS.code,
        minSize: COLUMN_MIN_SIZE,
        cell: ({ row }) => (
          <Link
            to={`${BASE_PATH}/${row.original.id}`}
            className="font-medium text-blue-700 hover:underline"
            onClick={(e) => e.stopPropagation()}
          >
            {row.original.code}
          </Link>
        ),
      },
      {
        id: 'voucherAt',
        header: 'Ngày',
        size: COLUMN_SIZING_DEFAULTS.voucherAt,
        minSize: COLUMN_MIN_SIZE,
        cell: ({ row }) => <span className="tabular-nums">{formatDateTime(row.original.voucherAt)}</span>,
      },
      textColumn('fromFund', 'Quỹ đi', (t) => `${t.fromFundCode} — ${t.fromFundName}`),
      textColumn('toFund', 'Quỹ đến', (t) => `${t.toFundCode} — ${t.toFundName}`),
      {
        id: 'amount',
        header: () => <div className="text-right">Số tiền</div>,
        size: COLUMN_SIZING_DEFAULTS.amount,
        minSize: COLUMN_MIN_SIZE,
        cell: ({ row }) => (
          <span className="block text-right tabular-nums">{currency.format(row.original.amount)}</span>
        ),
      },
      textColumn('description', 'Diễn giải', (t) => t.description),
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
      textColumn('ownerName', 'Người lập', (t) => t.ownerName),
    ],
    [],
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

  const displayPages = Math.max(data?.totalPages ?? 0, 1);

  return (
    <div className="flex h-full min-h-0 flex-col gap-4">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold">Danh sách phiếu chuyển quỹ</h1>
        <Can permission="fund_transfers.create">
          <Button asChild>
            <Link to={`${BASE_PATH}/new`}>
              <Plus className="mr-2 h-4 w-4 text-cyan-600" /> Tạo phiếu chuyển quỹ
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
                placeholder="Tìm theo số phiếu / diễn giải..."
                value={search}
                onChange={(e) => updateParams({ q: e.target.value })}
                className="pl-9"
              />
            </div>

            <div className="flex flex-col gap-0.5">
              <span className="text-xs leading-none text-muted-foreground">Ngày chuyển</span>
              <QuotationDateFilter from={fromDate} to={toDate} onChange={(f, t) => updateParams({ from: f, to: t })} />
            </div>

            <select
              aria-label="Quỹ"
              className={SELECT_CLASS}
              value={fundId}
              onChange={(e) => updateParams({ fundId: e.target.value })}
            >
              <option value="">Tất cả quỹ</option>
              {fundOptions.map((f) => (
                <option key={f.id} value={f.id}>
                  {f.code} — {f.name}
                </option>
              ))}
            </select>

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
          </div>

          {isError && (
            <div className="rounded-md border border-destructive/30 bg-destructive/10 p-3 text-sm text-destructive">
              {getErrorMessage(error)}
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
                      onClick={() => navigate(`${BASE_PATH}/${row.original.id}`)}
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
            {/* The server total of the whole filtered set (cancelled transfers included when status = All). */}
            <div className="text-muted-foreground" role="group" aria-label="Tổng kết phiếu">
              Tổng <strong className="text-foreground">{data?.totalItems ?? 0}</strong> phiếu
              {' • '}
              <span className="font-semibold text-foreground">
                Số tiền{' '}
                {isFetching ? (
                  <span
                    className="inline-block h-3 w-16 animate-pulse rounded bg-muted align-middle"
                    aria-label="đang tải"
                  />
                ) : (
                  <span className="tabular-nums">
                    {isError || data === undefined ? '—' : currency.format(data.totalAmount)}
                  </span>
                )}
              </span>
            </div>
            <div className="flex items-center gap-2">
              <Select
                value={String(pageSize)}
                onValueChange={(v) => {
                  const next = Number(v);
                  updateParams({ size: next === DEFAULT_PAGE_SIZE ? '' : String(next) });
                }}
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
                Trang {page}/{displayPages}
              </span>
              <Button
                variant="outline"
                size="sm"
                disabled={!(data?.hasPreviousPage ?? false)}
                onClick={() => setPage(page - 1)}
              >
                Trước
              </Button>
              <Button
                variant="outline"
                size="sm"
                disabled={!(data?.hasNextPage ?? false)}
                onClick={() => setPage(page + 1)}
              >
                Sau
              </Button>
            </div>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
