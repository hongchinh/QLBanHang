import { useMemo, useState } from 'react';
import {
  flexRender,
  getCoreRowModel,
  useReactTable,
  type ColumnDef,
} from '@tanstack/react-table';
import { AppIcon } from '@/components/icons';
import {
  useDeleteProductGroup,
  useProductGroups,
} from '@/features/product-groups/hooks';
import type { ProductGroupListItem } from '@/features/product-groups/types';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Card, CardContent } from '@/components/ui/card';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { Can } from '@/components/auth/can';
import { useDebouncedValue } from '@/lib/use-debounced-value';
import { useSearchParamNumber, useSearchParamString } from '@/lib/use-search-param-state';
import { toast } from '@/lib/use-toast';
import { getErrorMessage } from '@/lib/api-client';
import { ProductGroupFormDialog } from './product-group-form-dialog';

const PAGE_SIZE = 20;

export function ProductGroupListPage() {
  const [search, setSearch] = useSearchParamString('q');
  const [page, setPage] = useSearchParamNumber('page', 1);
  const debouncedSearch = useDebouncedValue(search, 300);

  const [dialogOpen, setDialogOpen] = useState(false);
  const [editTarget, setEditTarget] = useState<ProductGroupListItem | undefined>();
  const [pendingDelete, setPendingDelete] = useState<ProductGroupListItem | null>(null);

  const { data, isLoading, isError, error } = useProductGroups({
    page,
    pageSize: PAGE_SIZE,
    search: debouncedSearch || undefined,
  });

  const remove = useDeleteProductGroup();

  const openCreate = () => {
    setEditTarget(undefined);
    setDialogOpen(true);
  };

  const openEdit = (item: ProductGroupListItem) => {
    setEditTarget(item);
    setDialogOpen(true);
  };

  const columns = useMemo<ColumnDef<ProductGroupListItem>[]>(
    () => [
      { header: 'Mã', accessorKey: 'code' },
      { header: 'Tên nhóm', accessorKey: 'name' },
      { header: 'Mô tả', accessorKey: 'description' },
      { header: 'Thứ tự', accessorKey: 'sortOrder' },
      {
        header: 'Số hàng hóa',
        accessorKey: 'productCount',
        cell: ({ row }) => (
          <span className="tabular-nums">{row.original.productCount}</span>
        ),
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
          <div className="flex justify-end gap-2">
            <Can permission="products.update">
              <Button
                variant="ghost"
                size="icon"
                aria-label="Sửa nhóm hàng hóa"
                onClick={() => openEdit(row.original)}
              >
                <AppIcon name="edit" />
              </Button>
            </Can>
            <Can permission="products.delete">
              <Button
                variant="ghost"
                size="icon"
                aria-label="Xóa nhóm hàng hóa"
                onClick={() => setPendingDelete(row.original)}
              >
                <AppIcon name="delete" />
              </Button>
            </Can>
          </div>
        ),
      },
    ],
    [], // eslint-disable-line react-hooks/exhaustive-deps
  );

  const table = useReactTable({
    data: data?.items ?? [],
    columns,
    getCoreRowModel: getCoreRowModel(),
  });

  const onConfirmDelete = () => {
    if (!pendingDelete) return;
    const target = pendingDelete;
    remove.mutate(target.id, {
      onSuccess: () => {
        toast({ variant: 'success', title: 'Đã xóa nhóm hàng hóa', description: target.name });
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
          <h1 className="text-2xl font-bold">Nhóm hàng hóa</h1>
          <p className="text-sm text-muted-foreground">Quản lý danh mục nhóm hàng hóa</p>
        </div>
        <Can permission="products.create">
          <Button onClick={openCreate}>
            <AppIcon name="add" className="mr-2" /> Thêm nhóm
          </Button>
        </Can>
      </div>

      <Card>
        <CardContent className="p-4">
          <div className="mb-4 flex flex-wrap items-center gap-2">
            <div className="relative max-w-sm flex-1">
              <AppIcon name="search" className="absolute left-3 top-1/2 -translate-y-1/2" />
              <Input
                placeholder="Tìm theo tên / mã..."
                value={search}
                onChange={(e) => {
                  setSearch(e.target.value);
                  if (page !== 1) setPage(1);
                }}
                className="pl-9"
              />
            </div>
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
                    <TableHead key={h.id}>
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
                    Chưa có nhóm hàng hóa nào.
                  </TableCell>
                </TableRow>
              ) : (
                table.getRowModel().rows.map((row) => (
                  <TableRow key={row.id}>
                    {row.getVisibleCells().map((c) => (
                      <TableCell key={c.id}>
                        {flexRender(c.column.columnDef.cell, c.getContext())}
                      </TableCell>
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
                <Button variant="outline" size="sm" disabled={!data.hasPreviousPage} onClick={() => setPage(page - 1)}>
                  Trước
                </Button>
                <Button variant="outline" size="sm" disabled={!data.hasNextPage} onClick={() => setPage(page + 1)}>
                  Sau
                </Button>
              </div>
            </div>
          )}
        </CardContent>
      </Card>

      <ProductGroupFormDialog
        open={dialogOpen}
        onOpenChange={setDialogOpen}
        initial={editTarget}
      />

      <ConfirmDialog
        open={!!pendingDelete}
        onOpenChange={(open) => !open && setPendingDelete(null)}
        title="Xóa nhóm hàng hóa?"
        description={
          pendingDelete ? (
            <>
              Bạn chắc chắn muốn xóa nhóm <strong>{pendingDelete.name}</strong>?
              {pendingDelete.productCount > 0 && (
                <> Có <strong>{pendingDelete.productCount}</strong> hàng hóa liên kết sẽ bị gỡ khỏi nhóm này.</>
              )}
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
