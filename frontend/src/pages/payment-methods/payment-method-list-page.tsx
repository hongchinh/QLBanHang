import { useMemo, useState } from 'react';
import { flexRender, getCoreRowModel, useReactTable, type ColumnDef } from '@tanstack/react-table';
import { Pencil, Plus, Trash2 } from 'lucide-react';
import { useDeletePaymentMethod, usePaymentMethods } from '@/features/payment-methods/hooks';
import type { PaymentMethod } from '@/features/payment-methods/types';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Card, CardContent } from '@/components/ui/card';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { Can } from '@/components/auth/can';
import { getErrorMessage } from '@/lib/api-client';
import { toast } from '@/lib/use-toast';
import { PaymentMethodFormDialog } from './payment-method-form-dialog';

export function PaymentMethodListPage() {
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editTarget, setEditTarget] = useState<PaymentMethod | undefined>();
  const [pendingDelete, setPendingDelete] = useState<PaymentMethod | null>(null);

  const { data, isLoading, isError, error } = usePaymentMethods();
  const remove = useDeletePaymentMethod();

  const openCreate = () => {
    setEditTarget(undefined);
    setDialogOpen(true);
  };

  const columns = useMemo<ColumnDef<PaymentMethod>[]>(
    () => [
      { header: 'Mã', accessorKey: 'code' },
      { header: 'Tên phương thức', accessorKey: 'name' },
      {
        header: 'Tiền mặt',
        accessorKey: 'isCash',
        cell: ({ row }) => (row.original.isCash ? <Badge variant="success">Có</Badge> : null),
      },
      {
        id: 'actions',
        header: '',
        cell: ({ row }) => (
          <Can permission="inventory.catalogs.manage">
            <div className="flex justify-end gap-2">
              <Button
                variant="ghost"
                size="icon"
                aria-label="Sửa phương thức"
                onClick={() => {
                  setEditTarget(row.original);
                  setDialogOpen(true);
                }}
              >
                <Pencil className="h-4 w-4 text-blue-600" />
              </Button>
              <Button
                variant="ghost"
                size="icon"
                aria-label="Xóa phương thức"
                onClick={() => setPendingDelete(row.original)}
              >
                <Trash2 className="h-4 w-4 text-red-600" />
              </Button>
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
        toast({ variant: 'success', title: 'Đã xóa phương thức thanh toán', description: target.name });
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
          <h1 className="text-2xl font-bold">Phương thức thanh toán</h1>
          <p className="text-sm text-muted-foreground">Quản lý danh mục phương thức thanh toán</p>
        </div>
        <Can permission="inventory.catalogs.manage">
          <Button onClick={openCreate}>
            <Plus className="mr-2 h-4 w-4 text-cyan-600" /> Thêm phương thức
          </Button>
        </Can>
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
                    Chưa có phương thức thanh toán nào.
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

      <PaymentMethodFormDialog open={dialogOpen} onOpenChange={setDialogOpen} initial={editTarget} />

      <ConfirmDialog
        open={!!pendingDelete}
        onOpenChange={(open) => !open && setPendingDelete(null)}
        title="Xóa phương thức thanh toán?"
        description={
          pendingDelete ? (
            <>
              Bạn chắc chắn muốn xóa <strong>{pendingDelete.name}</strong>?
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
