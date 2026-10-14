import { useMemo, useState } from 'react';
import { flexRender, getCoreRowModel, useReactTable, type ColumnDef } from '@tanstack/react-table';
import { AppIcon } from '@/components/icons';
import { useDeleteFund, useFunds } from '@/features/funds/hooks';
import type { Fund } from '@/features/funds/types';
import type { FundKind } from '@/features/cash-common/types';
import { FUND_KIND_LABELS } from '@/features/cash-common/labels';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Card, CardContent } from '@/components/ui/card';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { getErrorMessage } from '@/lib/api-client';
import { toast } from '@/lib/use-toast';
import { cn, formatCurrencyVnd } from '@/lib/utils';
import { useAuthStore } from '@/stores/auth-store';
import { FundFormDialog } from './fund-form-dialog';
import { FundOpeningBalanceDialog } from './fund-opening-balance-dialog';

type KindFilter = 'All' | Exclude<FundKind, 'None'>;

const NUMERIC = 'text-right tabular-nums';

// Reachable with cash.catalogs.manage or cash.opening_balance; each action follows its own permission.
export function FundListPage() {
  const hasPermission = useAuthStore((s) => s.hasPermission);
  const canManage = hasPermission('cash.catalogs.manage');
  const canSetOpening = hasPermission('cash.opening_balance');

  const [kindFilter, setKindFilter] = useState<KindFilter>('All');
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editTarget, setEditTarget] = useState<Fund | undefined>();
  const [openingTarget, setOpeningTarget] = useState<Fund | undefined>();
  const [pendingDelete, setPendingDelete] = useState<Fund | null>(null);

  const { data, isLoading, isError, error } = useFunds({ kind: kindFilter === 'All' ? undefined : kindFilter });
  const remove = useDeleteFund();

  const openCreate = () => {
    setEditTarget(undefined);
    setDialogOpen(true);
  };

  const columns = useMemo<ColumnDef<Fund>[]>(
    () => [
      { header: 'Mã', accessorKey: 'code' },
      { header: 'Tên', accessorKey: 'name' },
      { header: 'Loại', accessorKey: 'kind', cell: ({ row }) => FUND_KIND_LABELS[row.original.kind] },
      { header: 'Ngân hàng', accessorKey: 'bankName' },
      { header: 'Số tài khoản', accessorKey: 'accountNumber' },
      {
        header: 'Mặc định',
        accessorKey: 'isDefault',
        cell: ({ row }) => (row.original.isDefault ? <Badge variant="info">Mặc định</Badge> : null),
      },
      {
        id: 'openingAmount',
        header: () => <div className={NUMERIC}>Số dư đầu kỳ</div>,
        cell: ({ row }) => (
          <div className={NUMERIC}>
            {row.original.openingAmount === null ? '—' : formatCurrencyVnd(row.original.openingAmount)}
          </div>
        ),
      },
      {
        header: 'Đang dùng',
        accessorKey: 'isActive',
        cell: ({ row }) =>
          row.original.isActive ? (
            <Badge variant="success">Đang dùng</Badge>
          ) : (
            <Badge variant="secondary">Ngừng</Badge>
          ),
      },
      {
        id: 'actions',
        header: '',
        cell: ({ row }) => (
          <div className="flex justify-end gap-2">
            {canSetOpening && (
              <Button
                variant="ghost"
                size="icon"
                aria-label="Số dư đầu kỳ"
                title="Số dư đầu kỳ"
                onClick={() => setOpeningTarget(row.original)}
              >
                <AppIcon name="openingBalance" />
              </Button>
            )}
            {canManage && (
              <>
                <Button
                  variant="ghost"
                  size="icon"
                  aria-label="Sửa quỹ"
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
                  aria-label="Xóa quỹ"
                  onClick={() => setPendingDelete(row.original)}
                >
                  <AppIcon name="delete" />
                </Button>
              </>
            )}
          </div>
        ),
      },
    ],
    [canManage, canSetOpening],
  );

  const table = useReactTable({ data: data ?? [], columns, getCoreRowModel: getCoreRowModel() });

  const onConfirmDelete = () => {
    if (!pendingDelete) return;
    const target = pendingDelete;
    remove.mutate(target.id, {
      onSuccess: () => {
        toast({ variant: 'success', title: 'Đã xóa quỹ', description: target.name });
        setPendingDelete(null);
      },
      onError: (err) => {
        toast({ variant: 'destructive', title: 'Không thể xóa', description: getErrorMessage(err) });
      },
    });
  };

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-bold">Quỹ</h1>
          <p className="text-sm text-muted-foreground">Quỹ tiền mặt và tài khoản ngân hàng của chi nhánh</p>
        </div>
        <div className="flex items-center gap-2">
          <Select value={kindFilter} onValueChange={(v) => setKindFilter(v as KindFilter)}>
            <SelectTrigger className="w-40" aria-label="Lọc loại quỹ">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="All">Tất cả loại</SelectItem>
              <SelectItem value="Cash">{FUND_KIND_LABELS.Cash}</SelectItem>
              <SelectItem value="Bank">{FUND_KIND_LABELS.Bank}</SelectItem>
            </SelectContent>
          </Select>
          {canManage && (
            <Button onClick={openCreate}>
              <AppIcon name="add" className="mr-2" /> Thêm quỹ
            </Button>
          )}
        </div>
      </div>

      <Card>
        <CardContent className="p-4">
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
                    Chưa có quỹ nào.
                  </TableCell>
                </TableRow>
              ) : (
                table.getRowModel().rows.map((row) => (
                  <TableRow key={row.id} className={cn(!row.original.isActive && 'text-muted-foreground')}>
                    {row.getVisibleCells().map((c) => (
                      <TableCell key={c.id}>{flexRender(c.column.columnDef.cell, c.getContext())}</TableCell>
                    ))}
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      <FundFormDialog open={dialogOpen} onOpenChange={setDialogOpen} initial={editTarget} />

      <FundOpeningBalanceDialog
        open={!!openingTarget}
        onOpenChange={(open) => !open && setOpeningTarget(undefined)}
        fund={openingTarget}
      />

      <ConfirmDialog
        open={!!pendingDelete}
        onOpenChange={(open) => !open && setPendingDelete(null)}
        title="Xóa quỹ?"
        description={
          pendingDelete ? (
            <>
              Bạn chắc chắn muốn xóa quỹ <strong>{pendingDelete.name}</strong>?
            </>
          ) : null
        }
        destructive
        confirmLabel="Xóa"
        loading={remove.isPending}
        onConfirm={onConfirmDelete}
      />
    </div>
  );
}
