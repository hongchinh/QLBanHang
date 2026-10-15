import { useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import {
  flexRender,
  getCoreRowModel,
  useReactTable,
  type ColumnDef,
} from '@tanstack/react-table';
import { AppIcon } from '@/components/icons';
import { useCustomers, useDeleteCustomer } from '@/features/customers/hooks';
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

export function CustomerListPage() {
  const [search, setSearch] = useSearchParamString('q');
  const [page, setPage] = useSearchParamNumber('page', 1);
  const debouncedSearch = useDebouncedValue(search, 300);

  const [pendingDelete, setPendingDelete] = useState<CustomerListItem | null>(null);

  const { data, isLoading, isError, error } = useCustomers({
    page,
    pageSize: PAGE_SIZE,
    search: debouncedSearch || undefined,
  });
  const remove = useDeleteCustomer();

  const columns = useMemo<ColumnDef<CustomerListItem>[]>(
    () => [
      { header: 'Mã KH', accessorKey: 'code' },
      { header: 'Tên khách hàng', accessorKey: 'name' },
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
            <Can permission="customers.update">
              <Button asChild variant="ghost" size="icon" aria-label="Sửa khách hàng">
                <Link to={`/customers/${row.original.id}`}>
                  <AppIcon name="edit" />
                </Link>
              </Button>
            </Can>
            <Can permission="customers.delete">
              <Button
                variant="ghost"
                size="icon"
                aria-label="Xóa khách hàng"
                onClick={() => setPendingDelete(row.original)}
              >
                <AppIcon name="delete" />
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
          title: 'Đã xóa khách hàng',
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
          <h1 className="text-2xl font-bold">Khách hàng</h1>
          <p className="text-sm text-muted-foreground">Quản lý danh mục khách hàng</p>
        </div>
        <Can permission="customers.create">
          <Button asChild>
            <Link to="/customers/new">
              <AppIcon name="add" className="mr-2" /> Thêm khách hàng
            </Link>
          </Button>
        </Can>
      </div>

      <Card>
        <CardContent className="p-4">
          <div className="mb-4 flex items-center gap-2">
            <div className="relative max-w-sm flex-1">
              <AppIcon name="search" className="absolute left-3 top-1/2 -translate-y-1/2" />
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
                    Chưa có khách hàng nào.
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
        title="Xóa khách hàng?"
        description={
          pendingDelete ? (
            <>
              Bạn chắc chắn muốn xóa khách hàng <strong>{pendingDelete.name}</strong>? Hành động này không thể hoàn tác.
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
