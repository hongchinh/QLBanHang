import { useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import {
  flexRender,
  getCoreRowModel,
  useReactTable,
  type ColumnDef,
} from '@tanstack/react-table';
import { AppIcon } from '@/components/icons';
import {
  useDeleteProduct,
  useProductGroups,
  useProducts,
  useUnits,
} from '@/features/products/hooks';
import type { PricingMode, ProductListItem, ProductStatus } from '@/features/products/types';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Card, CardContent } from '@/components/ui/card';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { Can } from '@/components/auth/can';
import { useDebouncedValue } from '@/lib/use-debounced-value';
import { useSearchParamNumber, useSearchParamString } from '@/lib/use-search-param-state';
import { toast } from '@/lib/use-toast';
import { getErrorMessage } from '@/lib/api-client';

const PAGE_SIZE = 20;
const ALL = '__all__';

const currencyFormatter = new Intl.NumberFormat('vi-VN');

function formatCurrency(value?: number) {
  return value === undefined || value === null ? '' : currencyFormatter.format(value);
}

const PRICING_MODE_LABEL: Record<PricingMode, string> = {
  PerUnit: 'Theo ĐV',
  PerSquareMeter: 'Theo m²',
  PerLinearMeter: 'Theo m dài',
  PerCubicMeter: 'Theo m³',
};

export function ProductListPage() {
  const [search, setSearch] = useSearchParamString('q');
  const [page, setPage] = useSearchParamNumber('page', 1);
  const [groupId, setGroupId] = useSearchParamString('group');
  const [unitId, setUnitId] = useSearchParamString('unit');
  const [statusFilter, setStatusFilter] = useSearchParamString('status');
  const debouncedSearch = useDebouncedValue(search, 300);

  const [pendingDelete, setPendingDelete] = useState<ProductListItem | null>(null);

  const groups = useProductGroups();
  const units = useUnits();

  const { data, isLoading, isError, error } = useProducts({
    page,
    pageSize: PAGE_SIZE,
    search: debouncedSearch || undefined,
    productGroupId: groupId || undefined,
    unitId: unitId || undefined,
    status: (statusFilter as ProductStatus) || undefined,
  });
  const remove = useDeleteProduct();

  const columns = useMemo<ColumnDef<ProductListItem>[]>(
    () => [
      { header: 'Mã HH', accessorKey: 'code' },
      { header: 'Tên hàng hóa', accessorKey: 'name' },
      { header: 'Nhóm', accessorKey: 'productGroupName' },
      { header: 'ĐVT', accessorKey: 'unitName' },
      { header: 'Quy cách', accessorKey: 'specification' },
      {
        header: 'Loại giá',
        accessorKey: 'pricingMode',
        cell: ({ row }) => PRICING_MODE_LABEL[row.original.pricingMode] ?? '',
      },
      {
        header: 'Giá bán',
        accessorKey: 'defaultPrice',
        cell: ({ row }) => (
          <span className="tabular-nums">{formatCurrency(row.original.defaultPrice)}</span>
        ),
      },
      {
        header: 'Giá vốn',
        accessorKey: 'costPrice',
        cell: ({ row }) => (
          <span className="tabular-nums">{formatCurrency(row.original.costPrice)}</span>
        ),
      },
      {
        header: 'Trạng thái',
        accessorKey: 'status',
        cell: ({ row }) =>
          row.original.status === 'Active' ? (
            <Badge variant="success">Đang bán</Badge>
          ) : (
            <Badge variant="secondary">Ngừng</Badge>
          ),
      },
      {
        id: 'actions',
        header: '',
        cell: ({ row }) => (
          <div className="flex justify-end gap-2">
            <Can permission="products.update">
              <Button asChild variant="ghost" size="icon" aria-label="Sửa hàng hóa">
                <Link to={`/products/${row.original.id}`}>
                  <AppIcon name="edit" />
                </Link>
              </Button>
            </Can>
            <Can permission="products.delete">
              <Button
                variant="ghost"
                size="icon"
                aria-label="Xóa hàng hóa"
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
        toast({ variant: 'success', title: 'Đã xóa hàng hóa', description: target.name });
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
          <h1 className="text-2xl font-bold">Hàng hóa</h1>
          <p className="text-sm text-muted-foreground">Quản lý danh mục hàng hóa</p>
        </div>
        <Can permission="products.create">
          <Button asChild>
            <Link to="/products/new">
              <AppIcon name="add" className="mr-2" /> Thêm hàng hóa
            </Link>
          </Button>
        </Can>
      </div>

      <Card>
        <CardContent className="p-4">
          <div className="mb-4 flex flex-wrap items-center gap-2">
            <div className="relative max-w-sm flex-1">
              <AppIcon name="search" className="absolute left-3 top-1/2 -translate-y-1/2" />
              <Input
                placeholder="Tìm theo tên / mã / quy cách..."
                value={search}
                onChange={(e) => {
                  setSearch(e.target.value);
                  if (page !== 1) setPage(1);
                }}
                className="pl-9"
              />
            </div>

            <Select
              value={groupId || ALL}
              onValueChange={(v) => {
                setGroupId(v === ALL ? '' : v);
                if (page !== 1) setPage(1);
              }}
            >
              <SelectTrigger className="w-44" aria-label="Nhóm hàng hóa">
                <SelectValue placeholder="Tất cả nhóm" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={ALL}>Tất cả nhóm</SelectItem>
                {(groups.data ?? []).map((g) => (
                  <SelectItem key={g.id} value={g.id}>{g.name}</SelectItem>
                ))}
              </SelectContent>
            </Select>

            <Select
              value={unitId || ALL}
              onValueChange={(v) => {
                setUnitId(v === ALL ? '' : v);
                if (page !== 1) setPage(1);
              }}
            >
              <SelectTrigger className="w-36" aria-label="Đơn vị tính">
                <SelectValue placeholder="Tất cả ĐVT" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={ALL}>Tất cả ĐVT</SelectItem>
                {(units.data ?? []).map((u) => (
                  <SelectItem key={u.id} value={u.id}>{u.name}</SelectItem>
                ))}
              </SelectContent>
            </Select>

            <Select
              value={statusFilter || ALL}
              onValueChange={(v) => {
                setStatusFilter(v === ALL ? '' : v);
                if (page !== 1) setPage(1);
              }}
            >
              <SelectTrigger className="w-36" aria-label="Trạng thái">
                <SelectValue placeholder="Tất cả trạng thái" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={ALL}>Tất cả</SelectItem>
                <SelectItem value="Active">Đang bán</SelectItem>
                <SelectItem value="Inactive">Ngừng</SelectItem>
              </SelectContent>
            </Select>
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
                    Chưa có hàng hóa nào.
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
        title="Xóa hàng hóa?"
        description={
          pendingDelete ? (
            <>
              Bạn chắc chắn muốn xóa hàng hóa <strong>{pendingDelete.name}</strong>? Hành động này không thể hoàn tác.
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
