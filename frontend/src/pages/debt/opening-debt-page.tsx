import { useMemo, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { flexRender, getCoreRowModel, useReactTable, type ColumnDef } from '@tanstack/react-table';
import { AppIcon } from '@/components/icons';
import { PartnerAutocomplete } from '@/components/partner-autocomplete/partner-autocomplete';
import { ConfirmRequiredDialog } from '@/components/confirm-required-dialog/confirm-required-dialog';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { DEBT_SIDE_LABELS, GONE_OWNER_LABEL, OPENING_DEBT_KIND_LABELS } from '@/features/cash-common/labels';
import type { DebtSide } from '@/features/cash-common/types';
import {
  useDeleteOpeningDebt,
  useOpeningDateNotSetHandler,
  useOpeningDebtPartnerSearch,
  useOpeningDebts,
} from '@/features/opening-debts/hooks';
import type { OpeningDebt } from '@/features/opening-debts/types';
import { getErrorMessage } from '@/lib/api-client';
import { toConfirmationRequest, withConfirmed, type ConfirmationRequest, type ConfirmFlags } from '@/lib/confirm-required';
import { formatDateOnly } from '@/lib/date-only';
import { formatMoneyForDisplay } from '@/lib/money-input';
import { useDebouncedValue } from '@/lib/use-debounced-value';
import { toast } from '@/lib/use-toast';
import { OpeningDebtFormDialog } from './opening-debt-form-dialog';

const PAGE_SIZE = 20;
const SELECT_CLASS = 'h-9 rounded-md border border-input bg-background px-3 text-sm';

function parseSide(value: string | null): DebtSide | undefined {
  return value === 'Receivable' || value === 'Payable' ? value : undefined;
}

// A delete waiting for DEBT_SETTLEMENTS_WILL_BE_REMOVED to be confirmed (P10).
interface PendingRemoval {
  request: ConfirmationRequest;
  row: OpeningDebt;
  flags: ConfirmFlags;
}

// Route permission: debt.opening_balance (every opening debt endpoint needs it).
export function OpeningDebtPage() {
  const handleOpeningDateNotSet = useOpeningDateNotSetHandler();
  const [params, setParams] = useSearchParams();
  const search = params.get('q') ?? '';
  const side = parseSide(params.get('side'));
  const partnerId = params.get('partnerId') ?? '';
  const partnerCode = params.get('partnerCode') ?? '';
  const pageParam = Number(params.get('page'));
  const page = Number.isInteger(pageParam) && pageParam > 0 ? pageParam : 1;
  const debouncedSearch = useDebouncedValue(search, 300);

  // One URL write per change; a filter change goes back to page 1.
  const updateParams = (patch: Record<string, string | undefined>) => {
    setParams(
      (prev) => {
        const next = new URLSearchParams(prev);
        for (const [key, value] of Object.entries(patch)) {
          if (value) next.set(key, value);
          else next.delete(key);
        }
        if (!('page' in patch)) next.delete('page');
        return next;
      },
      { replace: true },
    );
  };

  const [dialogOpen, setDialogOpen] = useState(false);
  const [editTarget, setEditTarget] = useState<OpeningDebt | undefined>();
  const [pendingDelete, setPendingDelete] = useState<OpeningDebt | null>(null);
  const [pendingRemoval, setPendingRemoval] = useState<PendingRemoval | null>(null);

  const { data, isLoading, isError, error } = useOpeningDebts({
    partnerId: partnerId || undefined,
    side,
    search: debouncedSearch || undefined,
    page,
    pageSize: PAGE_SIZE,
  });
  const remove = useDeleteOpeningDebt();
  const loaded = data !== undefined;
  const openingDate = data?.openingDate ?? null;

  const columns = useMemo<ColumnDef<OpeningDebt>[]>(
    () => [
      {
        id: 'partner',
        header: 'Đối tượng',
        cell: ({ row }) => `${row.original.partnerCode} - ${row.original.partnerName}`,
      },
      { id: 'side', header: 'Phía', cell: ({ row }) => DEBT_SIDE_LABELS[row.original.side] },
      { id: 'kind', header: 'Loại', cell: ({ row }) => OPENING_DEBT_KIND_LABELS[row.original.kind] },
      { header: 'Số CT', accessorKey: 'docNo' },
      { id: 'docDate', header: 'Ngày CT', cell: ({ row }) => formatDateOnly(row.original.docDate) },
      { id: 'dueDate', header: 'Hạn TT', cell: ({ row }) => formatDateOnly(row.original.dueDate) },
      {
        id: 'amount',
        header: () => <div className="text-right">Số tiền</div>,
        cell: ({ row }) => (
          <span className="block text-right tabular-nums">{formatMoneyForDisplay(row.original.amount)}</span>
        ),
      },
      {
        id: 'settled',
        header: () => <div className="text-right">Đã đối trừ</div>,
        cell: ({ row }) => (
          <span className="block text-right tabular-nums">{formatMoneyForDisplay(row.original.settled)}</span>
        ),
      },
      {
        id: 'owner',
        header: 'Nhân viên',
        cell: ({ row }) => row.original.ownerName ?? (row.original.ownerUserId ? GONE_OWNER_LABEL : '—'),
      },
      { id: 'note', header: 'Ghi chú', cell: ({ row }) => row.original.note ?? '' },
      {
        id: 'actions',
        header: '',
        cell: ({ row }) => (
          <div className="flex justify-end gap-2">
            <Button
              variant="ghost"
              size="icon"
              aria-label="Sửa nợ đầu kỳ"
              onClick={() => {
                setEditTarget(row.original);
                setDialogOpen(true);
              }}
            >
              <AppIcon name="edit" />
            </Button>
            <Button
              variant="ghost"
              size="icon"
              aria-label="Xóa nợ đầu kỳ"
              onClick={() => setPendingDelete(row.original)}
            >
              <AppIcon name="delete" />
            </Button>
          </div>
        ),
      },
    ],
    [],
  );

  const table = useReactTable({ data: data?.items ?? [], columns, getCoreRowModel: getCoreRowModel() });

  const deleteRow = async (row: OpeningDebt, flags: ConfirmFlags = {}) => {
    try {
      await remove.mutateAsync({
        id: row.id,
        version: row.version,
        confirmRemoveSettlements: flags.confirmRemoveSettlements ?? false,
        confirmedSettlements: flags.confirmedSettlements,
      });
      toast({ variant: 'success', title: 'Đã xóa nợ đầu kỳ', description: row.docNo });
    } catch (err) {
      const request = toConfirmationRequest(err);
      if (request) setPendingRemoval({ request, row, flags });
      else if (!handleOpeningDateNotSet(err))
        toast({ variant: 'destructive', title: 'Không thể xóa', description: getErrorMessage(err) });
    } finally {
      setPendingDelete(null);
    }
  };

  const onConfirmRemoval = () => {
    if (!pendingRemoval) return;
    const { row, request, flags } = pendingRemoval;
    setPendingRemoval(null);
    void deleteRow(row, withConfirmed(flags, request));
  };

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div>
          <h1 className="text-2xl font-bold">Nợ đầu kỳ</h1>
          {openingDate && (
            <p className="text-sm text-muted-foreground">{`Ngày đầu kỳ: ${formatDateOnly(openingDate)}`}</p>
          )}
        </div>
        <Button
          onClick={() => {
            setEditTarget(undefined);
            setDialogOpen(true);
          }}
          disabled={!openingDate}
        >
          <AppIcon name="add" className="mr-2" /> Thêm
        </Button>
      </div>

      {loaded && !openingDate && (
        <div
          role="alert"
          className="flex items-start gap-2 rounded-md border border-amber-300 bg-amber-50 p-3 text-sm text-amber-900"
        >
          <AppIcon name="warning" tone="none" className="mt-0.5 shrink-0" />
          <span>
            Chi nhánh chưa có ngày đầu kỳ. Vào{' '}
            <Link to="/settings/branches" className="font-medium underline">
              Cài đặt → Chi nhánh
            </Link>{' '}
            để đặt ngày đầu kỳ trước khi nhập nợ đầu kỳ.
          </span>
        </div>
      )}

      <Card>
        <CardContent className="p-4">
          <div className="mb-4 flex flex-wrap items-center gap-2">
            <div className="relative w-full max-w-sm flex-1">
              <AppIcon name="search" className="absolute left-3 top-1/2 -translate-y-1/2" />
              <Input
                placeholder="Tìm theo số CT / mã / tên đối tượng..."
                aria-label="Tìm nợ đầu kỳ"
                value={search}
                onChange={(e) => updateParams({ q: e.target.value })}
                className="pl-9"
              />
            </div>
            <div className="w-60">
              <Label htmlFor="opening-debt-filter-partner" className="sr-only">
                Lọc đối tượng
              </Label>
              <PartnerAutocomplete
                inputId="opening-debt-filter-partner"
                useSearch={useOpeningDebtPartnerSearch}
                requireReason={false}
                placeholder="Lọc theo đối tượng..."
                value={partnerId ? { id: partnerId, code: partnerCode || partnerId, name: partnerCode } : null}
                onSelect={(p) => updateParams({ partnerId: p?.id, partnerCode: p?.code })}
              />
            </div>
            <select
              aria-label="Lọc phía"
              className={SELECT_CLASS}
              value={side ?? ''}
              onChange={(e) => updateParams({ side: e.target.value })}
            >
              <option value="">Tất cả phía</option>
              <option value="Receivable">{DEBT_SIDE_LABELS.Receivable}</option>
              <option value="Payable">{DEBT_SIDE_LABELS.Payable}</option>
            </select>
          </div>

          {isError && (
            <div className="mb-3 rounded-md border border-destructive/30 bg-destructive/10 p-3 text-sm text-destructive">
              {getErrorMessage(error)}
            </div>
          )}

          <Table>
            <TableHeader>
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
                    Chưa có nợ đầu kỳ nào.
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

          {data && data.totalPages > 1 && (
            <div className="mt-4 flex items-center justify-between text-sm">
              <div className="text-muted-foreground">
                Trang {data.page} / {data.totalPages} • Tổng {data.totalItems}
              </div>
              <div className="flex gap-2">
                <Button
                  variant="outline"
                  size="sm"
                  disabled={!data.hasPreviousPage}
                  onClick={() => updateParams({ page: page - 1 > 1 ? String(page - 1) : undefined })}
                >
                  Trước
                </Button>
                <Button
                  variant="outline"
                  size="sm"
                  disabled={!data.hasNextPage}
                  onClick={() => updateParams({ page: String(page + 1) })}
                >
                  Sau
                </Button>
              </div>
            </div>
          )}
        </CardContent>
      </Card>

      <OpeningDebtFormDialog
        open={dialogOpen}
        onOpenChange={setDialogOpen}
        initial={editTarget}
        openingDate={openingDate}
      />

      <ConfirmDialog
        open={!!pendingDelete}
        onOpenChange={(open) => !open && setPendingDelete(null)}
        title="Xóa nợ đầu kỳ?"
        description={
          pendingDelete ? (
            <>
              Bạn chắc chắn muốn xóa chứng từ <strong>{pendingDelete.docNo}</strong> của{' '}
              <strong>{pendingDelete.partnerName}</strong>?
            </>
          ) : null
        }
        destructive
        confirmLabel="Xóa"
        loading={remove.isPending}
        onConfirm={() => {
          if (pendingDelete) void deleteRow(pendingDelete);
        }}
      />

      <ConfirmRequiredDialog
        request={pendingRemoval?.request ?? null}
        onConfirm={onConfirmRemoval}
        onClose={() => setPendingRemoval(null)}
      />
    </div>
  );
}
