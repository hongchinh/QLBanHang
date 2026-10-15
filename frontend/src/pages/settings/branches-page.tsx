import { useMemo, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { flexRender, getCoreRowModel, useReactTable, type ColumnDef } from '@tanstack/react-table';
import { AppIcon } from '@/components/icons';
import { branchesApi } from '@/features/branches/api';
import { useBranches, useDeleteBranch } from '@/features/branches/hooks';
import { branchKeys } from '@/features/branches/keys';
import { useSwitchWorkingBranch } from '@/features/branches/use-switch-working-branch';
import type { Branch } from '@/features/branches/types';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Card, CardContent } from '@/components/ui/card';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { getErrorMessage } from '@/lib/api-client';
import { formatDateOnly } from '@/lib/date-only';
import { toast } from '@/lib/use-toast';
import { useBranchStore } from '@/stores/branch-store';
import { BranchFormDialog, BranchOpeningDateDialog } from './branch-form-dialog';
import { DemoBranchDeleteDialog } from './demo-branch-delete-dialog';

export function BranchesPage() {
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editTarget, setEditTarget] = useState<Branch | undefined>();
  const [pendingDelete, setPendingDelete] = useState<Branch | null>(null);
  const [pendingDemoDelete, setPendingDemoDelete] = useState<Branch | null>(null);
  const [openingDateTarget, setOpeningDateTarget] = useState<Branch | null>(null);

  const { data, isLoading, isError, error } = useBranches();
  const remove = useDeleteBranch();
  const queryClient = useQueryClient();
  const switchWorkingBranch = useSwitchWorkingBranch();

  // Deleting the working branch: the backend would silently fall back to the default branch while
  // the switcher still shows the deleted one, so move to the default branch like the header switcher.
  const leaveDeletedWorkingBranch = async (deletedId: string) => {
    if (useBranchStore.getState().workingBranchId !== deletedId) return;
    try {
      const me = await queryClient.fetchQuery({
        queryKey: branchKeys.me(),
        queryFn: () => branchesApi.me(),
        staleTime: 0,
      });
      const next = me.branches.some((b) => b.id === me.defaultBranchId) ? me.defaultBranchId : me.workingBranchId;
      await switchWorkingBranch(next);
    } catch (err) {
      toast({ variant: 'destructive', title: 'Không thể chuyển chi nhánh làm việc', description: getErrorMessage(err) });
    }
  };

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
        header: 'Loại',
        id: 'kind',
        cell: ({ row }) => {
          const branch = row.original;
          if (!branch.isDemo) return null;
          return (
            <div className="flex flex-wrap items-center gap-1">
              <Badge variant="info">Demo</Badge>
              {branch.sampleDataStatus === 'Running' && (
                <Badge variant="warning">Đang tạo dữ liệu {branch.sampleDataProgress ?? 0}%</Badge>
              )}
              {branch.sampleDataStatus === 'Failed' && (
                <Badge variant="destructive" title={branch.sampleDataError ?? undefined}>
                  Lỗi tạo dữ liệu
                </Badge>
              )}
            </div>
          );
        },
      },
      {
        header: 'Khóa sổ đến',
        accessorKey: 'lockedUntil',
        cell: ({ row }) => formatDateOnly(row.original.lockedUntil),
      },
      {
        header: 'Ngày đầu kỳ',
        accessorKey: 'openingDate',
        cell: ({ row }) => formatDateOnly(row.original.openingDate),
      },
      {
        id: 'actions',
        header: '',
        cell: ({ row }) => (
          <div className="flex justify-end gap-2">
            <Button
              variant="ghost"
              size="icon"
              aria-label="Đặt ngày đầu kỳ"
              onClick={() => setOpeningDateTarget(row.original)}
            >
              <AppIcon name="calendar" className="text-slate-600" />
            </Button>
            <Button
              variant="ghost"
              size="icon"
              aria-label="Sửa chi nhánh"
              onClick={() => {
                setEditTarget(row.original);
                setDialogOpen(true);
              }}
            >
              <AppIcon name="edit" />
            </Button>
            {row.original.isDemo ? (
              <Button
                variant="ghost"
                size="icon"
                aria-label="Xóa chi nhánh demo"
                className="disabled:pointer-events-auto disabled:cursor-not-allowed"
                disabled={row.original.sampleDataStatus === 'Running'}
                title={row.original.sampleDataStatus === 'Running' ? 'Đang tạo dữ liệu mẫu' : undefined}
                onClick={() => setPendingDemoDelete(row.original)}
              >
                <AppIcon name="delete" />
              </Button>
            ) : (
              <Button
                variant="ghost"
                size="icon"
                aria-label="Xóa chi nhánh"
                onClick={() => setPendingDelete(row.original)}
              >
                <AppIcon name="delete" />
              </Button>
            )}
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
        void leaveDeletedWorkingBranch(target.id);
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
          <AppIcon name="add" className="mr-2" /> Thêm chi nhánh
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

      <BranchOpeningDateDialog branch={openingDateTarget} onClose={() => setOpeningDateTarget(null)} />

      <DemoBranchDeleteDialog
        branch={pendingDemoDelete}
        onClose={() => setPendingDemoDelete(null)}
        onDeleted={(id) => void leaveDeletedWorkingBranch(id)}
      />

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
