import { useMemo, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import {
  flexRender,
  getCoreRowModel,
  useReactTable,
  type ColumnDef,
} from '@tanstack/react-table';
import { AppIcon } from '@/components/icons';
import {
  useCloneQuotation,
  useQuotationOwners,
  useQuotations,
  useTransitionQuotation,
} from '@/features/quotations/hooks';
import { quotationsApi } from '@/features/quotations/api';
import type {
  QuotationAction,
  QuotationListItem,
  QuotationStatus,
} from '@/features/quotations/types';
import { Can } from '@/components/auth/can';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { Input } from '@/components/ui/input';
import { MultiSelect } from '@/components/ui/multi-select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { getErrorMessage } from '@/lib/api-client';
import { useDebouncedValue } from '@/lib/use-debounced-value';
import { toast } from '@/lib/use-toast';
import { useAuthStore } from '@/stores/auth-store';
import { ListFooter } from '@/pages/quotations/components/list-footer';
import { StatusPill } from '@/pages/quotations/components/status-pill';
import { parseOwnerIds } from '@/pages/quotations/utils/owner-ids';

const PAGE_SIZE_OPTIONS = [10, 20, 50, 100] as const;
const DEFAULT_PAGE_SIZE = 10;

const currency = new Intl.NumberFormat('vi-VN');

const STATUS_OPTIONS: ReadonlyArray<{ value: QuotationStatus; label: string }> = [
  { value: 'Draft', label: 'Nháp' },
  { value: 'Sent', label: 'Đã gửi' },
  { value: 'Confirmed', label: 'Đã xác nhận' },
  { value: 'Cancelled', label: 'Đã hủy' },
];

const DEFAULT_ACTIVE_STATUSES: ReadonlyArray<QuotationStatus> = ['Draft', 'Sent', 'Confirmed'];

interface CustomerQuotationsSectionProps {
  customerId: string;
}

export function CustomerQuotationsSection({ customerId }: CustomerQuotationsSectionProps) {
  const navigate = useNavigate();
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState<number>(DEFAULT_PAGE_SIZE);
  const [statuses, setStatuses] = useState<QuotationStatus[]>([...DEFAULT_ACTIVE_STATUSES]);
  const [fromDate, setFromDate] = useState('');
  const [toDate, setToDate] = useState('');
  const [ownerIdsParam, setOwnerIdsParam] = useState('');
  const debouncedSearch = useDebouncedValue(search, 300);
  const hasViewAll = useAuthStore((s) => s.hasPermission('quotations.view_all'));
  const canViewCost = useAuthStore((s) => s.hasPermission('quotations.view_cost'));

  const ownerIds = useMemo<string[]>(
    () => (hasViewAll ? parseOwnerIds(ownerIdsParam) : []),
    [hasViewAll, ownerIdsParam],
  );

  const [pendingTransition, setPendingTransition] = useState<{
    item: QuotationListItem;
    action: QuotationAction;
  } | null>(null);

  const { data, isLoading, isFetching, isError, error } = useQuotations({
    page,
    pageSize,
    customerId,
    search: debouncedSearch || undefined,
    statuses: statuses.length > 0 ? statuses : undefined,
    ownerUserIds: ownerIds.length > 0 ? ownerIds : undefined,
    from: fromDate || undefined,
    to: toDate || undefined,
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
    const items = data?.items ?? [];
    return {
      subtotal: data?.aggregates?.subtotal ?? 0,
      discount: data?.aggregates?.discount ?? 0,
      freight: data?.aggregates?.freight ?? 0,
      advancePayment: data?.aggregates?.advancePayment ?? 0,
      total: data?.aggregates?.total ?? 0,
      totalCost: canViewCost
        ? data?.aggregates?.totalCost ?? items.reduce((sum, item) => sum + (item.totalCost ?? 0), 0)
        : null,
      grossProfit: canViewCost
        ? data?.aggregates?.grossProfit ?? items.reduce((sum, item) => sum + (item.grossProfit ?? 0), 0)
        : null,
    };
  }, [canViewCost, data?.aggregates, data?.items]);

  const columns = useMemo<ColumnDef<QuotationListItem>[]>(
    () => [
      { header: 'Số báo giá', accessorKey: 'code' },
      {
        header: 'Ngày',
        accessorKey: 'quotationDate',
        cell: ({ row }) => formatDate(row.original.quotationDate),
      },
      { header: 'SĐT', accessorKey: 'contactPhone' },
      {
        header: () => moneyHeader('Tổng tiền hàng'),
        accessorKey: 'subtotal',
        cell: ({ row }) => moneyCell(row.original.subtotal),
      },
      {
        header: () => moneyHeader('Chiết khấu'),
        accessorKey: 'discount',
        cell: ({ row }) => moneyCell(row.original.discount),
      },
      {
        header: () => moneyHeader('Vận chuyển'),
        accessorKey: 'freight',
        cell: ({ row }) => moneyCell(row.original.freight),
      },
      {
        header: () => moneyHeader('Tổng tiền'),
        accessorKey: 'total',
        cell: ({ row }) => moneyCell(row.original.total),
      },
      ...(canViewCost
        ? [
            {
              header: () => moneyHeader('Tổng nhập'),
              accessorKey: 'totalCost',
              cell: ({ row }: { row: { original: QuotationListItem } }) => moneyCell(row.original.totalCost),
            } as ColumnDef<QuotationListItem>,
            {
              header: () => moneyHeader('Tổng LN'),
              accessorKey: 'grossProfit',
              cell: ({ row }: { row: { original: QuotationListItem } }) => moneyCell(row.original.grossProfit),
            } as ColumnDef<QuotationListItem>,
          ]
        : []),
      {
        header: 'Trạng thái',
        accessorKey: 'status',
        cell: ({ row }) => <StatusPill status={row.original.status} />,
      },
      ...(hasViewAll
        ? [{
            header: 'Chủ sở hữu',
            id: 'owner',
            cell: ({ row }: { row: { original: QuotationListItem } }) => (
              <span>
                {row.original.ownerFullName ?? '-'}
                {row.original.isOwnerDeleted && (
                  <span className="ml-2 rounded bg-amber-100 px-2 py-0.5 text-xs text-amber-900">
                    đã nghỉ
                  </span>
                )}
              </span>
            ),
          } as ColumnDef<QuotationListItem>]
        : []),
      { header: 'Người lập', accessorKey: 'createdByName' },
      {
        id: 'actions',
        header: '',
        cell: ({ row }) => {
          const q = row.original;
          const canSend = q.status === 'Draft';
          const canConfirm = q.status === 'Sent';
          const canCancel = q.status !== 'Cancelled';
          return (
            <div className="flex justify-end gap-1">
              <Can permission="quotations.update">
                <Button asChild variant="ghost" size="icon" aria-label="Sửa">
                  <Link to={`/quotations/${q.id}`}><AppIcon name="edit" /></Link>
                </Button>
              </Can>
              <DropdownMenu>
                <DropdownMenuTrigger asChild>
                  <Button variant="ghost" size="icon" aria-label="Thao tác khác">
                    <AppIcon name="more" tone="muted" />
                  </Button>
                </DropdownMenuTrigger>
                <DropdownMenuContent align="end">
                  <Can permission="quotations.update">
                    <DropdownMenuItem
                      disabled={!canSend}
                      title={canSend ? undefined : 'Chỉ gửi được báo giá đang ở trạng thái Nháp'}
                      onClick={() => setPendingTransition({ item: q, action: 'Send' })}
                    >
                      <AppIcon name="send" className="mr-2" /> Gửi
                    </DropdownMenuItem>
                    <DropdownMenuItem
                      disabled={!canConfirm}
                      title={canConfirm ? undefined : 'Chỉ xác nhận được báo giá đã gửi'}
                      onClick={() => setPendingTransition({ item: q, action: 'Confirm' })}
                    >
                      <AppIcon name="confirm" className="mr-2" /> Xác nhận
                    </DropdownMenuItem>
                  </Can>
                  <Can permission="quotations.create">
                    <DropdownMenuItem
                      onClick={() => {
                        clone.mutate(q.id, {
                          onSuccess: (cloned) => {
                            toast({ variant: 'success', title: 'Đã nhân bản báo giá', description: cloned.code });
                            navigate(`/quotations/${cloned.id}`);
                          },
                          onError: (err) =>
                            toast({ variant: 'destructive', title: 'Không thể nhân bản', description: getErrorMessage(err) }),
                        });
                      }}
                    >
                      <AppIcon name="clone" className="mr-2" /> Nhân bản
                    </DropdownMenuItem>
                  </Can>
                  <Can permission="quotations.print">
                    <DropdownMenuItem
                      onClick={() => {
                        downloadPdf(q.id, q.code).catch((err) =>
                          toast({ variant: 'destructive', title: 'Không tải được PDF', description: getErrorMessage(err) }),
                        );
                      }}
                    >
                      <AppIcon name="print" className="mr-2" /> In PDF
                    </DropdownMenuItem>
                  </Can>
                  <Can permission="quotations.update">
                    <DropdownMenuItem
                      className="text-destructive focus:text-destructive"
                      disabled={!canCancel}
                      title={canCancel ? undefined : 'Báo giá đã hủy'}
                      onClick={() => setPendingTransition({ item: q, action: 'Cancel' })}
                    >
                      <AppIcon name="cancelVoucher" className="mr-2" /> Hủy
                    </DropdownMenuItem>
                  </Can>
                </DropdownMenuContent>
              </DropdownMenu>
            </div>
          );
        },
      },
    ],
    [canViewCost, clone, hasViewAll, navigate],
  );

  const table = useReactTable({
    data: data?.items ?? [],
    columns,
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

  const resetPage = () => {
    if (page !== 1) setPage(1);
  };

  return (
    <Can permission="quotations.view">
      <Card className="flex min-h-[520px] flex-col">
        <CardHeader className="flex flex-row items-center justify-between gap-3 space-y-0">
          <div>
            <CardTitle>Báo giá của khách hàng</CardTitle>
            <p className="mt-1 text-sm text-muted-foreground">Danh sách báo giá đã lập cho khách hàng này</p>
          </div>
          <Can permission="quotations.create">
            <Button asChild>
              <Link to="/quotations/new">
                <AppIcon name="add" className="mr-2" /> Thêm báo giá
              </Link>
            </Button>
          </Can>
        </CardHeader>
        <CardContent className="flex min-h-0 flex-1 flex-col gap-3">
          <div className="flex flex-wrap items-center gap-2">
            <div className="relative max-w-sm flex-1">
              <AppIcon name="search" className="absolute left-3 top-1/2 -translate-y-1/2" />
              <Input
                placeholder="Tìm theo số báo giá..."
                value={search}
                onChange={(e) => {
                  setSearch(e.target.value);
                  resetPage();
                }}
                className="pl-9"
              />
            </div>

            <MultiSelect<QuotationStatus>
              options={STATUS_OPTIONS}
              value={statuses}
              onChange={(next) => {
                setStatuses(next);
                resetPage();
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
                  resetPage();
                }}
                placeholder="Chủ sở hữu"
                triggerClassName="w-56"
                ariaLabel="Chủ sở hữu"
              />
            )}

            <Input
              type="date"
              value={fromDate}
              onChange={(e) => {
                setFromDate(e.target.value);
                resetPage();
              }}
              className="w-44"
              aria-label="Từ ngày"
            />
            <Input
              type="date"
              value={toDate}
              onChange={(e) => {
                setToDate(e.target.value);
                resetPage();
              }}
              className="w-44"
              aria-label="Đến ngày"
            />
          </div>

          {isError && (
            <div className="rounded-md border border-destructive/30 bg-destructive/10 p-3 text-sm text-destructive">
              {getErrorMessage(error)}
            </div>
          )}

          <div className="relative min-h-[300px] flex-1 overflow-hidden rounded-md border">
            {isFetching && !isLoading && (
              <div
                className="absolute inset-0 z-20 flex items-center justify-center bg-background/60 backdrop-blur-[1px]"
                role="status"
                aria-live="polite"
              >
                <div className="flex items-center gap-2 rounded-md border bg-background px-3 py-2 text-sm text-muted-foreground shadow-sm">
                  <AppIcon name="loading" />
                  Đang tải...
                </div>
              </div>
            )}
            <Table containerClassName="h-full">
              <TableHeader className="sticky top-0 z-10">
                {table.getHeaderGroups().map((hg) => (
                  <TableRow key={hg.id}>
                    {hg.headers.map((h) => (
                      <TableHead key={h.id}>{flexRender(h.column.columnDef.header, h.getContext())}</TableHead>
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
                    <TableRow key={row.id}>
                      {row.getVisibleCells().map((c) => (
                        <TableCell key={c.id}>{flexRender(c.column.columnDef.cell, c.getContext())}</TableCell>
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
              setPageSize(next);
              resetPage();
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
    </Can>
  );
}

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
  return typeof value === 'number' ? currency.format(value) : '-';
}

function moneyCell(value?: number | null) {
  return <span className="block text-right tabular-nums">{formatNullableCurrency(value)}</span>;
}

async function downloadPdf(id: string, code: string) {
  const blob = await quotationsApi.downloadPdf(id);
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = `BaoGia_${code}.pdf`;
  document.body.appendChild(a);
  a.click();
  a.remove();
  URL.revokeObjectURL(url);
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
              Báo giá <strong>{p.item.code}</strong> đã được xác nhận, hủy sẽ trừ doanh thu của sale. Cần quyền <code>quotations.cancel_confirmed</code>. Tiếp tục?
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
