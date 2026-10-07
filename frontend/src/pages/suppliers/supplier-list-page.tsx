import { useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import {
  flexRender,
  getCoreRowModel,
  useReactTable,
  type ColumnDef,
} from '@tanstack/react-table';
import { Plus, Pencil, Trash2, Search } from 'lucide-react';
import { useDeleteSupplier, useSuppliers } from '@/features/suppliers/hooks';
import type { CustomerGroup, CustomerListItem } from '@/features/customers/types';
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

const groupLabels: Record<CustomerGroup, string> = {
  Company: 'Công ty',
  Agent: 'Đại lý',
  Retail: 'Khách lẻ',
  Project: 'Công trình',
};

const PAGE_SIZE = 20;

// Same structure as CustomerListPage, over the supplier role of the partner catalog (D1).
export function SupplierListPage() {
  const [search, setSearch] = useSearchParamString('q');
  const [page, setPage] = useSearchParamNumber('page', 1);
  const debouncedSearch = useDebouncedValue(search, 300);

  const [pendingDelete, setPendingDelete] = useState<CustomerListItem | null>(null);

  const { data, isLoading, isError, error } = useSuppliers({
    page,
    pageSize: PAGE_SIZE,
    search: debouncedSearch || undefined,
  });
  const remove = useDeleteSupplier();

  const columns = useMemo<ColumnDef<CustomerListItem>[]>(
    () => [
      { header: 'Mã NCC', accessorKey: 'code' },
      { header: 'Tên nhà cung cấp', accessorKey: 'name' },
      { header: 'MST', accessorKey: 'taxCode' },
      { header: 'Người liên hệ', accessorKey: 'contactPerson' },
      { header: 'Điện thoại', accessorKey: 'phoneNumber' },
      {
        header: 'Nhóm',
        accessorKey: 'group',
        cell: ({ row }) => groupLabels[row.original.group] ?? row.original.group,
      },
      {
        header: 'Trạng thái',
        accessorKey: 'status',
        cell: ({ row }) =>
          row.original.status === 'Active' ? (
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
            <Can permission="suppliers.update">
              <Button asChild variant="ghost" size="icon" aria-label="Sửa nhà cung cấp">
                <Link to={`/suppliers/${row.original.id}`}>
                  <Pencil className="h-4 w-4 text-blue-600" />
                </Link>
              </Button>
            </Can>
            <Can permission="suppliers.delete">
              <Button
                variant="ghost"
                size="icon"
                aria-label="Xóa nhà cung cấp"
                onClick={() => setPendingDelete(row.original)}
              >
                <Trash2 className="h-4 w-4 text-red-600" />
              </Button>
            </Can>
          </div>
        ),
      },
    ],
    [],
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
        toast({
          variant: 'success',
          title: 'Đã xóa nhà cung cấp',
          description: target.name,
        });
        setPendingDelete(null);
      },
      onError: (err) => {
        toast({
          variant: 'destructive',
          title: 'Không thể xóa',
          description: getErrorMessage(err),
        });
      },
    });
  };

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold">Nhà cung cấp</h1>
          <p className="text-sm text-muted-foreground">Quản lý danh mục nhà cung cấp</p>
        </div>
        <Can permission="suppliers.create">
          <Button asChild>
            <Link to="/suppliers/new">
              <Plus className="mr-2 h-4 w-4 text-cyan-600" /> Thêm nhà cung cấp
            </Link>
          </Button>
        </Can>
      </div>

      <Card>
        <CardContent className="p-4">
          <div className="mb-4 flex items-center gap-2">
            <div className="relative max-w-sm flex-1">
              <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-500" />
              <Input
                placeholder="Tìm theo tên / mã / SĐT / MST..."
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
                    Chưa có nhà cung cấp nào.
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

      <ConfirmDialog
        open={!!pendingDelete}
        onOpenChange={(open) => !open && setPendingDelete(null)}
        title="Xóa nhà cung cấp?"
        description={
          pendingDelete ? (
            <>
              Bạn chắc chắn muốn xóa nhà cung cấp <strong>{pendingDelete.name}</strong>? Hành động này không thể hoàn tác.
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
