import { useMemo } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { flexRender, getCoreRowModel, useReactTable, type ColumnDef } from '@tanstack/react-table';
import { format } from 'date-fns';
import { Loader2, Plus, Search } from 'lucide-react';
import { useStockVoucherOwners, useStockVouchers } from '@/features/stock-vouchers/hooks';
import type {
  PartnerSearchItem,
  StockDirection,
  StockVoucherListItem,
  StockVoucherStatusFilter,
} from '@/features/stock-vouchers/types';
import { useWarehouses } from '@/features/warehouses/hooks';
import { useStockReasons } from '@/features/stock-reasons/hooks';
import { PartnerAutocomplete } from '@/components/partner-autocomplete/partner-autocomplete';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { MultiSelect } from '@/components/ui/multi-select';
import { Table, TableBody, TableCell, TableColGroup, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Can } from '@/components/auth/can';
import { getErrorMessage } from '@/lib/api-client';
import { useColumnSizingPersist } from '@/lib/use-column-sizing-persist';
import { useDebouncedValue } from '@/lib/use-debounced-value';
import { useSearchParamNumber, useSearchParamString } from '@/lib/use-search-param-state';
import { useUiStore } from '@/stores/ui-store';
import { QuotationDateFilter } from '@/pages/quotations/components/quotation-date-filter';
import { StockListFooter } from './components/stock-list-footer';
import { STOCK_VOUCHER_LABELS } from './labels';

const PAGE_SIZE_OPTIONS = [10, 20, 50, 100] as const;
const DEFAULT_PAGE_SIZE = 20;
const COLUMN_MIN_SIZE = 60;
const currency = new Intl.NumberFormat('vi-VN');

const STATUS_OPTIONS: ReadonlyArray<{ value: StockVoucherStatusFilter; label: string }> = [
  { value: 'Active', label: 'Đang hiệu lực' },
  { value: 'Cancelled', label: 'Đã hủy' },
  { value: 'all', label: 'Tất cả' },
];
const VALID_STATUSES = new Set<string>(STATUS_OPTIONS.map((o) => o.value));

const COLUMN_SIZING_DEFAULTS: Record<string, number> = {
  code: 110,
  voucherAt: 130,
  warehouseName: 130,
  partnerName: 200,
  reasonName: 140,
  paymentMethodName: 110,
  goodsAmount: 120,
  discountTotal: 110,
  vatTotal: 100,
  freight: 100,
  total: 120,
  paidAmount: 120,
  status: 110,
  ownerName: 140,
};

const SELECT_CLASS = 'h-9 rounded-md border border-input bg-background px-2 text-sm';

function moneyColumn(key: keyof StockVoucherListItem & string, label: string): ColumnDef<StockVoucherListItem> {
  return {
    id: key,
    header: () => <div className="text-right">{label}</div>,
    size: COLUMN_SIZING_DEFAULTS[key],
    minSize: COLUMN_MIN_SIZE,
    cell: ({ row }) => (
      <span className="block text-right tabular-nums">{currency.format(Number(row.original[key] ?? 0))}</span>
    ),
  };
}

function textColumn(key: keyof StockVoucherListItem & string, label: string): ColumnDef<StockVoucherListItem> {
  return {
    id: key,
    header: label,
    size: COLUMN_SIZING_DEFAULTS[key],
    minSize: COLUMN_MIN_SIZE,
    cell: ({ row }) => {
      const value = row.original[key];
      return (
        <span className="block truncate" title={value ? String(value) : undefined}>
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

export function StockVoucherListPage({ type }: { type: StockDirection }) {
  const labels = STOCK_VOUCHER_LABELS[type];
  const navigate = useNavigate();
  const [, setParams] = useSearchParams();
  // Filter changes go through updateParams (one URL write that also resets the page): two setters in
  // one handler would each write the URL and the second would drop the first.
  const [search] = useSearchParamString('q');
  const [page, setPage] = useSearchParamNumber('page', 1);
  const [sizeParam] = useSearchParamNumber('size', DEFAULT_PAGE_SIZE);
  const [fromDate] = useSearchParamString('from');
  const [toDate] = useSearchParamString('to');
  const [warehouseId] = useSearchParamString('warehouseId');
  const [reasonId] = useSearchParamString('reasonId');
  const [partnerId] = useSearchParamString('partnerId');
  const [partnerName] = useSearchParamString('partnerName');
  const [statusParam] = useSearchParamString('status');
  const [ownersParam] = useSearchParamString('owners');
  const debouncedSearch = useDebouncedValue(search, 300);

  const savedStatus = useUiStore((s) => s.stockVoucherStatusFilter[type]);
  const setSavedStatus = useUiStore((s) => s.setStockVoucherStatusFilter);

  const pageSize = (PAGE_SIZE_OPTIONS as readonly number[]).includes(sizeParam) ? sizeParam : DEFAULT_PAGE_SIZE;
  // The URL wins, then the persisted choice, then Active.
  const status: StockVoucherStatusFilter = VALID_STATUSES.has(statusParam)
    ? (statusParam as StockVoucherStatusFilter)
    : (savedStatus ?? 'Active');
  const ownerIds = useMemo(() => ownersParam.split(',').filter(Boolean), [ownersParam]);

  const { data, isLoading, isFetching, isError, error } = useStockVouchers({
    type,
    page,
    pageSize,
    search: debouncedSearch || undefined,
    from: fromDate || undefined,
    to: toDate || undefined,
    warehouseId: warehouseId || undefined,
    partnerId: partnerId || undefined,
    reasonId: reasonId || undefined,
    status,
    ownerUserIds: ownerIds.length > 0 ? ownerIds : undefined,
  });

  const { data: warehouses = [] } = useWarehouses();
  const { data: allReasons = [] } = useStockReasons({ direction: type });
  const reasons = allReasons.filter((r) => r.direction === type);
  const ownersQuery = useStockVoucherOwners(type);
  const ownerOptions = useMemo(
    () => (ownersQuery.data ?? []).map((o) => ({ value: o.id, label: o.fullName })),
    [ownersQuery.data],
  );

  const [columnSizing, setColumnSizing] = useColumnSizingPersist(
    `stock-voucher-list-${type}`,
    COLUMN_SIZING_DEFAULTS,
  );

  const columns = useMemo<ColumnDef<StockVoucherListItem>[]>(
    () => [
      {
        id: 'code',
        header: 'Số phiếu',
        size: COLUMN_SIZING_DEFAULTS.code,
        minSize: COLUMN_MIN_SIZE,
        cell: ({ row }) => (
          <Link
            to={`${labels.basePath}/${row.original.id}`}
            className="font-medium text-blue-700 hover:underline"
            onClick={(e) => e.stopPropagation()}
          >
            {row.original.code}
          </Link>
        ),
      },
      {
        id: 'voucherAt',
        header: 'Ngày giờ',
        size: COLUMN_SIZING_DEFAULTS.voucherAt,
        minSize: COLUMN_MIN_SIZE,
        cell: ({ row }) => <span className="tabular-nums">{formatDateTime(row.original.voucherAt)}</span>,
      },
      textColumn('warehouseName', 'Kho'),
      textColumn('partnerName', 'Đối tượng'),
      textColumn('reasonName', 'Lý do'),
      textColumn('paymentMethodName', 'HTTT'),
      moneyColumn('goodsAmount', 'Tiền hàng'),
      moneyColumn('discountTotal', 'Chiết khấu'),
      moneyColumn('vatTotal', 'VAT'),
      moneyColumn('freight', 'Phí VC'),
      moneyColumn('total', 'Tổng TT'),
      moneyColumn('paidAmount', 'Đã TT'),
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
      textColumn('ownerName', 'Người tạo'),
    ],
    [labels.basePath],
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

  function handlePartnerSelect(p: PartnerSearchItem | null) {
    updateParams({ partnerId: p?.id ?? '', partnerName: p?.name ?? '' });
  }

  return (
    <div className="flex h-full min-h-0 flex-col gap-4">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold">{labels.listTitle}</h1>
        <Can permission={`${labels.permissionPrefix}.create`}>
          <Button asChild>
            <Link to={`${labels.basePath}/new`}>
              <Plus className="mr-2 h-4 w-4 text-cyan-600" /> Tạo {labels.title.toLowerCase()}
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
              <span className="text-xs leading-none text-muted-foreground">{labels.dateLabel}</span>
              <QuotationDateFilter from={fromDate} to={toDate} onChange={(f, t) => updateParams({ from: f, to: t })} />
            </div>

            <select
              aria-label="Kho"
              className={SELECT_CLASS}
              value={warehouseId}
              onChange={(e) => updateParams({ warehouseId: e.target.value })}
            >
              <option value="">Tất cả kho</option>
              {warehouses.map((w) => (
                <option key={w.id} value={w.id}>
                  {w.name}
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
              <PartnerAutocomplete
                type={type}
                requireReason={false}
                placeholder="Lọc theo đối tượng..."
                value={partnerId ? { id: partnerId, code: partnerName || partnerId, name: partnerName } : null}
                onSelect={handlePartnerSelect}
              />
            </div>

            <select
              aria-label="Trạng thái"
              className={SELECT_CLASS}
              value={status}
              onChange={(e) => {
                const next = e.target.value as StockVoucherStatusFilter;
                setSavedStatus(type, next);
                updateParams({ status: next });
              }}
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
              placeholder="Người tạo"
              triggerClassName="w-48"
              ariaLabel="Người tạo"
            />
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
                      onClick={() => navigate(`${labels.basePath}/${row.original.id}`)}
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

          <StockListFooter
            totalItems={data?.totalItems ?? 0}
            aggregates={data?.aggregates}
            page={page}
            totalPages={data?.totalPages ?? 0}
            pageSize={pageSize}
            pageSizeOptions={PAGE_SIZE_OPTIONS}
            hasPrev={data?.hasPreviousPage ?? false}
            hasNext={data?.hasNextPage ?? false}
            onPageChange={setPage}
            onPageSizeChange={(next) => updateParams({ size: next === DEFAULT_PAGE_SIZE ? '' : String(next) })}
            note={status === 'all' ? 'tổng tiền không gồm phiếu đã hủy' : undefined}
            loading={isFetching}
            errored={isError}
          />
        </CardContent>
      </Card>
    </div>
  );
}
