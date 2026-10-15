import { useMemo, useState } from 'react';
import { flexRender, getCoreRowModel, useReactTable, type ColumnDef } from '@tanstack/react-table';
import { AppIcon } from '@/components/icons';
import { useCashReasons, useDeleteCashReason } from '@/features/cash-reasons/hooks';
import type { CashReason } from '@/features/cash-reasons/types';
import { CASH_VOUCHER_TYPE_LABELS, DEBT_EFFECT_LABELS } from '@/features/cash-common/labels';
import type { CashVoucherType } from '@/features/cash-common/types';
import { PARTNER_TYPE_LABELS } from '@/features/stock-reasons/types';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Card, CardContent } from '@/components/ui/card';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Can } from '@/components/auth/can';
import { getErrorMessage } from '@/lib/api-client';
import { toast } from '@/lib/use-toast';
import { CashReasonFormDialog } from './cash-reason-form-dialog';

const ALL = 'All';
type TypeFilter = CashVoucherType | typeof ALL;

export function CashReasonListPage() {
  const [typeFilter, setTypeFilter] = useState<TypeFilter>(ALL);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editTarget, setEditTarget] = useState<CashReason | undefined>();
  const [pendingDelete, setPendingDelete] = useState<CashReason | null>(null);

  const type = typeFilter === ALL ? undefined : typeFilter;
  // Automatic reasons (TPX/CPN) are listed too, read-only, so the catalog shows every reason.
  const { data, isLoading, isError, error } = useCashReasons({ type, includeAuto: true });
  const remove = useDeleteCashReason();

  const openCreate = () => {
    setEditTarget(undefined);
    setDialogOpen(true);
  };

  const columns = useMemo<ColumnDef<CashReason>[]>(
    () => [
      { header: 'Mã', accessorKey: 'code' },
      { header: 'Tên', accessorKey: 'name' },
      {
        header: 'Loại phiếu',
        accessorKey: 'type',
        cell: ({ row }) => CASH_VOUCHER_TYPE_LABELS[row.original.type],
      },
      {
        header: 'Đối tượng',
        accessorKey: 'partnerType',
        cell: ({ row }) => PARTNER_TYPE_LABELS[row.original.partnerType],
      },
      {
        header: 'Ảnh hưởng công nợ',
        accessorKey: 'debtEffect',
        cell: ({ row }) => DEBT_EFFECT_LABELS[row.original.debtEffect],
      },
      {
        header: 'Hệ thống',
        accessorKey: 'isSystem',
        cell: ({ row }) => (
          <div className="flex gap-1">
            {row.original.isSystem && <Badge variant="secondary">Hệ thống</Badge>}
            {row.original.isAuto && <Badge variant="info">Tự động</Badge>}
          </div>
        ),
      },
      {
        id: 'actions',
        header: '',
        cell: ({ row }) =>
          // Automatic reasons belong to the stock vouchers: the backend refuses any change.
          row.original.isAuto ? null : (
            <Can permission="cash.catalogs.manage">
              <div className="flex justify-end gap-2">
                <Button
                  variant="ghost"
                  size="icon"
                  aria-label="Sửa lý do"
                  onClick={() => {
                    setEditTarget(row.original);
                    setDialogOpen(true);
                  }}
                >
                  <AppIcon name="edit" />
                </Button>
                {!row.original.isSystem && (
                  <Button
                    variant="ghost"
                    size="icon"
                    aria-label="Xóa lý do"
                    onClick={() => setPendingDelete(row.original)}
                  >
                    <AppIcon name="delete" />
                  </Button>
                )}
              </div>
            </Can>
          ),
      },
    ],
    [],
  );

  const table = useReactTable({ data: data ?? [], columns, getCoreRowModel: getCoreRowModel() });

  const onConfirmDelete = () => {
    if (!pendingDelete) return;
    const target = pendingDelete;
    remove.mutate(target.id, {
      onSuccess: () => {
        toast({ variant: 'success', title: 'Đã xóa lý do thu/chi', description: target.name });
        setPendingDelete(null);
      },
      onError: (err) => {
        toast({ variant: 'destructive', title: 'Không thể xóa', description: getErrorMessage(err) });
      },
    });
  };

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold">Lý do thu/chi</h1>
          <p className="text-sm text-muted-foreground">Quản lý danh mục lý do phiếu thu / phiếu chi</p>
        </div>
        <Can permission="cash.catalogs.manage">
          <Button onClick={openCreate}>
            <AppIcon name="add" className="mr-2" /> Thêm lý do
          </Button>
        </Can>
      </div>

      <Card>
        <CardContent className="space-y-3 p-4">
          <div className="w-48">
            <Select value={typeFilter} onValueChange={(v) => setTypeFilter(v as TypeFilter)}>
              <SelectTrigger aria-label="Lọc loại phiếu">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={ALL}>Tất cả loại phiếu</SelectItem>
                <SelectItem value="Receipt">{CASH_VOUCHER_TYPE_LABELS.Receipt}</SelectItem>
                <SelectItem value="Payment">{CASH_VOUCHER_TYPE_LABELS.Payment}</SelectItem>
              </SelectContent>
            </Select>
          </div>

          {isError && (
            <div className="rounded-md border border-destructive/30 bg-destructive/10 p-3 text-sm text-destructive">
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
                    Chưa có lý do nào.
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
        </CardContent>
      </Card>

      <CashReasonFormDialog
        open={dialogOpen}
        onOpenChange={setDialogOpen}
        initial={editTarget}
        defaultType={type}
      />

      <ConfirmDialog
        open={!!pendingDelete}
        onOpenChange={(open) => !open && setPendingDelete(null)}
        title="Xóa lý do thu/chi?"
        description={
          pendingDelete ? (
            <>
              Bạn chắc chắn muốn xóa lý do <strong>{pendingDelete.name}</strong>?
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
