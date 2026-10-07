import { useMemo, useState } from 'react';
import { flexRender, getCoreRowModel, useReactTable, type ColumnDef } from '@tanstack/react-table';
import { Pencil, Plus, Trash2 } from 'lucide-react';
import { useBranches, useDeleteBranch } from '@/features/branches/hooks';
import type { Branch } from '@/features/branches/types';
import { Button } from '@/components/ui/button';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Card, CardContent } from '@/components/ui/card';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { getErrorMessage } from '@/lib/api-client';
import { toast } from '@/lib/use-toast';
import { BranchFormDialog } from './branch-form-dialog';

// `yyyy-MM-dd` (DateOnly) → `dd/MM/yyyy`, without going through Date (no time-zone shift).
function formatDateOnly(value?: string | null): string {
  if (!value) return '';
  const [y, m, d] = value.split('-');
  return `${d}/${m}/${y}`;
}

export function BranchesPage() {
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editTarget, setEditTarget] = useState<Branch | undefined>();
  const [pendingDelete, setPendingDelete] = useState<Branch | null>(null);

  const { data, isLoading, isError, error } = useBranches();
  const remove = useDeleteBranch();

  const openCreate = () => {
    setEditTarget(undefined);
    setDialogOpen(true);
  };

  const columns = useMemo<ColumnDef<Branch>[]>(
    () => [
      { header: 'Mã', accessorKey: 'code' },
      { header: 'Tên', accessorKey: 'name' },
      { header: 'Địa chỉ', accessorKey: 'address', cell: ({ row }) => row.original.address ?? '' },
      {
        header: 'Khóa sổ đến',
        accessorKey: 'lockedUntil',
        cell: ({ row }) => formatDateOnly(row.original.lockedUntil),
      },
      {
        id: 'actions',
        header: '',
        cell: ({ row }) => (
          <div className="flex justify-end gap-2">
            <Button
              variant="ghost"
              size="icon"
              aria-label="Sửa chi nhánh"
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
              aria-label="Xóa chi nhánh"
              onClick={() => setPendingDelete(row.original)}
            >
              <Trash2 className="h-4 w-4 text-red-600" />
            </Button>
          </div>
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
        toast({ variant: 'success', title: 'Đã xóa chi nhánh', description: target.name });
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
          <h1 className="text-2xl font-bold">Chi nhánh</h1>
          <p className="text-sm text-muted-foreground">Quản lý danh sách chi nhánh</p>
        </div>
        <Button onClick={openCreate}>
          <Plus className="mr-2 h-4 w-4 text-cyan-600" /> Thêm chi nhánh
        </Button>
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
                    Chưa có chi nhánh nào.
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

      <BranchFormDialog open={dialogOpen} onOpenChange={setDialogOpen} initial={editTarget} />

      <ConfirmDialog
        open={!!pendingDelete}
        onOpenChange={(open) => !open && setPendingDelete(null)}
        title="Xóa chi nhánh?"
        description={
          pendingDelete ? (
            <>
              Bạn chắc chắn muốn xóa chi nhánh <strong>{pendingDelete.name}</strong>?
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
