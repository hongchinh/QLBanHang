import { useMemo, useState } from 'react';
import { flexRender, getCoreRowModel, useReactTable, type ColumnDef } from '@tanstack/react-table';
import { Pencil, Plus, Trash2 } from 'lucide-react';
import { useDeleteWarehouse, useWarehouses } from '@/features/warehouses/hooks';
import type { Warehouse } from '@/features/warehouses/types';
import { useBranches } from '@/features/branches/hooks';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Card, CardContent } from '@/components/ui/card';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { Can } from '@/components/auth/can';
import { getErrorMessage } from '@/lib/api-client';
import { toast } from '@/lib/use-toast';
import { useAuthStore } from '@/stores/auth-store';
import { WarehouseFormDialog } from './warehouse-form-dialog';

const ALL_BRANCHES = 'all';

export function WarehouseListPage() {
  const hasPermission = useAuthStore((s) => s.hasPermission);
  const canFilterBranch = hasPermission('branches.access_all');
  const [branchFilter, setBranchFilter] = useState(ALL_BRANCHES);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editTarget, setEditTarget] = useState<Warehouse | undefined>();
  const [pendingDelete, setPendingDelete] = useState<Warehouse | null>(null);

  const branchId = canFilterBranch && branchFilter !== ALL_BRANCHES ? branchFilter : undefined;
  const { data, isLoading, isError, error } = useWarehouses(branchId ? { branchId } : undefined);
  const { data: branches } = useBranches();
  const remove = useDeleteWarehouse();

  const openCreate = () => {
    setEditTarget(undefined);
    setDialogOpen(true);
  };

  const columns = useMemo<ColumnDef<Warehouse>[]>(
    () => [
      { header: 'Mã', accessorKey: 'code' },
      { header: 'Tên kho', accessorKey: 'name' },
      {
        header: 'Chi nhánh',
        accessorKey: 'branchName',
        cell: ({ row }) => `${row.original.branchCode} — ${row.original.branchName}`,
      },
      {
        header: 'Trạng thái',
        accessorKey: 'isActive',
        cell: ({ row }) =>
          row.original.isActive ? (
            <Badge variant="success">Hoạt động</Badge>
          ) : (
            <Badge variant="secondary">Không hoạt động</Badge>
          ),
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
                aria-label="Sửa kho"
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
                aria-label="Xóa kho"
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
        toast({ variant: 'success', title: 'Đã xóa kho', description: target.name });
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
          <h1 className="text-2xl font-bold">Kho hàng</h1>
          <p className="text-sm text-muted-foreground">Quản lý danh mục kho theo chi nhánh</p>
        </div>
        <Can permission="inventory.catalogs.manage">
          <Button onClick={openCreate}>
            <Plus className="mr-2 h-4 w-4 text-cyan-600" /> Thêm kho
          </Button>
        </Can>
      </div>

      <Card>
        <CardContent className="p-4">
          {canFilterBranch && (
            <div className="mb-4 max-w-xs">
              <Select value={branchFilter} onValueChange={setBranchFilter}>
                <SelectTrigger aria-label="Lọc chi nhánh">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value={ALL_BRANCHES}>Tất cả chi nhánh</SelectItem>
                  {(branches ?? []).map((b) => (
                    <SelectItem key={b.id} value={b.id}>
                      {b.code} — {b.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          )}

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
                    Chưa có kho nào.
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

      <WarehouseFormDialog open={dialogOpen} onOpenChange={setDialogOpen} initial={editTarget} />

      <ConfirmDialog
        open={!!pendingDelete}
        onOpenChange={(open) => !open && setPendingDelete(null)}
        title="Xóa kho?"
        description={
          pendingDelete ? (
            <>
              Bạn chắc chắn muốn xóa kho <strong>{pendingDelete.name}</strong>?
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
