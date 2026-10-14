import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { AppIcon } from '@/components/icons';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { Button } from '@/components/ui/button';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { UserFormDialog } from '@/pages/admin/components/user-form-dialog';
import { ResetPasswordDialog } from '@/pages/admin/components/reset-password-dialog';
import { useAuthStore } from '@/stores/auth-store';
import { useDeleteAdminUser, useSetAdminUserStatus } from '@/features/admin-users/hooks';
import type { AdminUserListItem } from '@/features/admin-users/types';
import { toast } from '@/lib/use-toast';
import { ApiCallError, getErrorMessage } from '@/lib/api-client';

interface Props {
  user: AdminUserListItem;
}

export function UserActionsMenu({ user }: Props) {
  const navigate = useNavigate();
  const currentUserId = useAuthStore((s) => s.user?.id);
  const hasPermission = useAuthStore((s) => s.hasPermission);

  const canUpdate = hasPermission('users.update');
  const canDelete = hasPermission('users.delete');
  const canManageSettings = hasPermission('user_settings.manage');
  const canTransferAny = hasPermission('quotations.transfer_any');

  const [editOpen, setEditOpen] = useState(false);
  const [resetOpen, setResetOpen] = useState(false);
  const [toggleStatusOpen, setToggleStatusOpen] = useState(false);
  const [deleteOpen, setDeleteOpen] = useState(false);

  const setStatus = useSetAdminUserStatus(user.id);
  const remove = useDeleteAdminUser();

  const isSelf = currentUserId === user.id;
  const nextStatus = user.isActive ? 'Disabled' : 'Active';

  const handleToggleStatus = async () => {
    try {
      await setStatus.mutateAsync({ status: nextStatus });
      toast({
        variant: 'success',
        title: user.isActive ? 'Đã khoá tài khoản' : 'Đã mở khoá tài khoản',
        description: user.username,
      });
      setToggleStatusOpen(false);
    } catch (err) {
      toast({ variant: 'destructive', title: 'Thao tác thất bại', description: getErrorMessage(err) });
    }
  };

  const handleDelete = async () => {
    try {
      await remove.mutateAsync(user.id);
      toast({ variant: 'success', title: 'Đã xoá user', description: user.username });
      setDeleteOpen(false);
    } catch (err) {
      if (err instanceof ApiCallError && err.code === 'CONFLICT') {
        toast({
          variant: 'destructive',
          title: 'Không thể xoá',
          description: `${err.message} Mở trang chuyển nhượng để xử lý.`,
        });
        setDeleteOpen(false);
        if (canTransferAny) navigate(`/admin/users/${user.id}/transfer-quotations`);
        return;
      }
      toast({ variant: 'destructive', title: 'Xoá thất bại', description: getErrorMessage(err) });
    }
  };

  return (
    <>
      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <Button variant="ghost" size="icon" aria-label="Thao tác">
            <AppIcon name="more" />
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end">
          {canUpdate && (
            <DropdownMenuItem onSelect={() => setEditOpen(true)}>Sửa</DropdownMenuItem>
          )}
          {canUpdate && (
            <DropdownMenuItem onSelect={() => setResetOpen(true)}>Đặt lại mật khẩu</DropdownMenuItem>
          )}
          {canUpdate && (
            <DropdownMenuItem
              disabled={isSelf && user.isActive}
              onSelect={() => setToggleStatusOpen(true)}
            >
              {user.isActive ? 'Khoá tài khoản' : 'Mở khoá tài khoản'}
            </DropdownMenuItem>
          )}
          {(canManageSettings || canTransferAny) && <DropdownMenuSeparator />}
          {canManageSettings && (
            <DropdownMenuItem onSelect={() => navigate(`/admin/user-settings/${user.id}`)}>
              Cấu hình báo giá
            </DropdownMenuItem>
          )}
          {canTransferAny && (
            <DropdownMenuItem onSelect={() => navigate(`/admin/users/${user.id}/transfer-quotations`)}>
              Chuyển nhượng báo giá
            </DropdownMenuItem>
          )}
          {canDelete && <DropdownMenuSeparator />}
          {canDelete && (
            <DropdownMenuItem
              disabled={isSelf}
              onSelect={() => setDeleteOpen(true)}
              className="text-destructive focus:text-destructive"
            >
              Xoá
            </DropdownMenuItem>
          )}
        </DropdownMenuContent>
      </DropdownMenu>

      <UserFormDialog mode="edit" open={editOpen} onOpenChange={setEditOpen} userId={user.id} />
      <ResetPasswordDialog
        open={resetOpen}
        onOpenChange={setResetOpen}
        userId={user.id}
        username={user.username}
      />
      <ConfirmDialog
        open={toggleStatusOpen}
        onOpenChange={setToggleStatusOpen}
        title={user.isActive ? 'Khoá tài khoản?' : 'Mở khoá tài khoản?'}
        description={
          user.isActive
            ? `Sau khi khoá, ${user.username} sẽ không đăng nhập được và bị đăng xuất khỏi mọi thiết bị.`
            : `Mở khoá ${user.username} để cho phép đăng nhập trở lại.`
        }
        confirmLabel={user.isActive ? 'Khoá' : 'Mở khoá'}
        destructive={user.isActive}
        loading={setStatus.isPending}
        onConfirm={handleToggleStatus}
      />
      <ConfirmDialog
        open={deleteOpen}
        onOpenChange={setDeleteOpen}
        title="Xoá user?"
        description={`Thao tác này sẽ vô hiệu hoá tài khoản ${user.username} và đăng xuất khỏi mọi thiết bị. Có thể khôi phục thủ công qua DB.`}
        confirmLabel="Xoá"
        destructive
        loading={remove.isPending}
        onConfirm={handleDelete}
      />
    </>
  );
}
