import { useEffect, useMemo, useState } from 'react';
import { AppIcon } from '@/components/icons';
import {
  useAdminRoleDetails,
  useAdminRoles,
  usePermissionsCatalog,
} from '@/features/admin-roles/hooks';
import { adminRolesApi } from '@/features/admin-roles/api';
import { adminRolesKeys } from '@/features/admin-roles/keys';
import type { PermissionModule, RoleListItem } from '@/features/admin-roles/types';
import { useAuthStore } from '@/stores/auth-store';
import { useQueryClient } from '@tanstack/react-query';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Can } from '@/components/auth/can';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { toast } from '@/lib/use-toast';
import { getErrorMessage } from '@/lib/api-client';
import { RoleMatrixTable } from '@/pages/admin/components/role-matrix-table';
import { RoleCreateDialog } from '@/pages/admin/components/role-create-dialog';
import { RoleRenameDialog } from '@/pages/admin/components/role-rename-dialog';
import { RoleDeleteConfirm } from '@/pages/admin/components/role-delete-confirm';

const ROLES_MANAGE = 'roles.manage';

export function RolesMatrixPage() {
  const rolesQ = useAdminRoles();
  const permsQ = usePermissionsCatalog();
  const roleIds = useMemo(() => rolesQ.data?.map((r) => r.id) ?? [], [rolesQ.data]);
  const detailsQ = useAdminRoleDetails(roleIds);
  const qc = useQueryClient();
  const hasPermission = useAuthStore((s) => s.hasPermission);
  const isInRole = useAuthStore((s) => s.isInRole);
  const canManage = hasPermission(ROLES_MANAGE);

  // Edit state: Map<roleId, Set<permissionCode>>. Initialised/refreshed from server detail data.
  const [state, setState] = useState<Map<string, Set<string>>>(new Map());
  // Track which roles have been mutated since last sync — drives "save" button + toast count.
  const [dirty, setDirty] = useState<Set<string>>(new Set());
  const [pendingLockoutConfirm, setPendingLockoutConfirm] = useState(false);

  const [createOpen, setCreateOpen] = useState(false);
  const [renameTarget, setRenameTarget] = useState<RoleListItem | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<RoleListItem | null>(null);

  // Initialise/sync state when detail data arrives or list refreshes.
  useEffect(() => {
    if (detailsQ.isLoading || detailsQ.data.length === 0) return;
    if (detailsQ.data.length !== roleIds.length) return;
    setState((prev) => {
      const next = new Map(prev);
      let changed = false;
      for (const detail of detailsQ.data) {
        if (dirty.has(detail.id)) continue;
        const incoming = new Set(detail.permissionCodes);
        const current = next.get(detail.id);
        if (!current || !setsEqual(current, incoming)) {
          next.set(detail.id, incoming);
          changed = true;
        }
      }
      return changed ? next : prev;
    });
  }, [detailsQ.data, detailsQ.isLoading, roleIds.length, dirty]);

  const handleToggle = (roleId: string, code: string, checked: boolean) => {
    const role = rolesQ.data?.find((r) => r.id === roleId);
    if (!role || role.code === 'ADMIN') return;
    setState((prev) => {
      const next = new Map(prev);
      const current = new Set(next.get(roleId) ?? new Set<string>());
      if (checked) current.add(code);
      else current.delete(code);
      next.set(roleId, current);
      return next;
    });
    setDirty((prev) => new Set(prev).add(roleId));
  };

  const handleToggleModule = (roleId: string, module: PermissionModule, checked: boolean) => {
    const role = rolesQ.data?.find((r) => r.id === roleId);
    if (!role || role.code === 'ADMIN') return;
    const perms = permsQ.data?.filter((p) => p.module === module) ?? [];
    setState((prev) => {
      const next = new Map(prev);
      const current = new Set(next.get(roleId) ?? new Set<string>());
      for (const p of perms) {
        if (checked) current.add(p.code);
        else current.delete(p.code);
      }
      next.set(roleId, current);
      return next;
    });
    setDirty((prev) => new Set(prev).add(roleId));
  };

  const handleReset = () => {
    setState(() => {
      const next = new Map<string, Set<string>>();
      for (const detail of detailsQ.data) {
        next.set(detail.id, new Set(detail.permissionCodes));
      }
      return next;
    });
    setDirty(new Set());
  };

  // Self-lockout: about to remove roles.manage from a role the current user belongs to.
  const wouldLockOutCurrentUser = useMemo(() => {
    if (!rolesQ.data) return false;
    for (const roleId of dirty) {
      const role = rolesQ.data.find((r) => r.id === roleId);
      if (!role) continue;
      if (!isInRole(role.code)) continue;
      const current = state.get(roleId);
      if (current && !current.has(ROLES_MANAGE)) return true;
    }
    return false;
  }, [dirty, rolesQ.data, state, isInRole]);

  const doSave = async () => {
    if (dirty.size === 0) return;
    const items = Array.from(dirty).map((roleId) => ({
      roleId,
      codes: Array.from(state.get(roleId) ?? new Set<string>()),
    }));

    const results = await Promise.allSettled(
      items.map(({ roleId, codes }) =>
        adminRolesApi.updatePermissions(roleId, { permissionCodes: codes }),
      ),
    );

    const fulfilled: string[] = [];
    const rejected: { roleId: string; error: unknown }[] = [];
    results.forEach((r, idx) => {
      const { roleId } = items[idx];
      if (r.status === 'fulfilled') fulfilled.push(roleId);
      else rejected.push({ roleId, error: r.reason });
    });

    // Clear dirty flag for fulfilled roles only — rejected stay dirty so user can retry.
    setDirty((prev) => {
      const next = new Set(prev);
      for (const id of fulfilled) next.delete(id);
      return next;
    });

    // Invalidate impacted queries.
    qc.invalidateQueries({ queryKey: adminRolesKeys.lists() });
    for (const id of fulfilled) {
      qc.invalidateQueries({ queryKey: adminRolesKeys.detail(id) });
    }

    if (fulfilled.length > 0) {
      toast({
        variant: 'success',
        title: `Đã lưu ${fulfilled.length} vai trò`,
        description: 'Thay đổi áp dụng cho user đang đăng nhập sau khi access token làm mới (tối đa 60 phút).',
      });
    }
    if (rejected.length > 0) {
      toast({
        variant: 'destructive',
        title: `${rejected.length} vai trò lưu thất bại`,
        description: getErrorMessage(rejected[0].error),
      });
    }
  };

  const handleSaveClick = () => {
    if (wouldLockOutCurrentUser) {
      setPendingLockoutConfirm(true);
      return;
    }
    void doSave();
  };

  if (rolesQ.isLoading || permsQ.isLoading) {
    return <div className="p-6 text-muted-foreground">Đang tải…</div>;
  }
  if (rolesQ.isError || permsQ.isError) {
    return (
      <div className="p-6 text-destructive">
        Lỗi tải dữ liệu: {getErrorMessage(rolesQ.error ?? permsQ.error)}
      </div>
    );
  }
  const roles = rolesQ.data ?? [];
  const permissions = permsQ.data ?? [];

  return (
    <div className="p-4 space-y-4">
      <div className="flex flex-wrap items-center gap-2 justify-between">
        <div>
          <h1 className="text-2xl font-semibold">Phân quyền</h1>
          <p className="text-sm text-muted-foreground">
            Quản lý ma trận Role × Permission. ADMIN luôn có toàn bộ quyền và không thể chỉnh sửa.
          </p>
        </div>
        <div className="flex items-center gap-2">
          {dirty.size > 0 && (
            <>
              <Badge variant="secondary">{dirty.size} thay đổi chưa lưu</Badge>
              <Button variant="outline" size="sm" onClick={handleReset}>
                <AppIcon name="discardChanges" className="mr-1" /> Hủy
              </Button>
              <Button size="sm" onClick={handleSaveClick} disabled={!canManage}>
                <AppIcon name="save" tone="none" className="mr-1" /> Lưu thay đổi
              </Button>
            </>
          )}
          <Can permission="roles.manage">
            <Button size="sm" onClick={() => setCreateOpen(true)}>
              <AppIcon name="add" tone="none" className="mr-1" /> Thêm role
            </Button>
          </Can>
        </div>
      </div>

      <RoleMatrixTable
        roles={roles}
        details={detailsQ.data}
        permissions={permissions}
        state={state}
        canManage={canManage}
        onToggle={handleToggle}
        onToggleModule={handleToggleModule}
        onRequestRename={(r) => setRenameTarget(r)}
        onRequestDelete={(r) => setDeleteTarget(r)}
      />

      <RoleCreateDialog
        open={createOpen}
        onOpenChange={setCreateOpen}
        permissions={permissions}
      />
      <RoleRenameDialog
        open={renameTarget !== null}
        onOpenChange={(o) => !o && setRenameTarget(null)}
        role={renameTarget}
      />
      <RoleDeleteConfirm
        open={deleteTarget !== null}
        onOpenChange={(o) => !o && setDeleteTarget(null)}
        role={deleteTarget}
      />

      <ConfirmDialog
        open={pendingLockoutConfirm}
        onOpenChange={setPendingLockoutConfirm}
        title="Bạn sắp bỏ quyền quản lý vai trò khỏi role của chính mình"
        description="Sau khi access token hết hạn (tối đa 60 phút) bạn sẽ không vào lại được màn hình này. Tiếp tục?"
        destructive
        confirmLabel="Tiếp tục lưu"
        onConfirm={async () => {
          setPendingLockoutConfirm(false);
          await doSave();
        }}
      />
    </div>
  );
}

function setsEqual(a: Set<string>, b: Set<string>): boolean {
  if (a.size !== b.size) return false;
  for (const v of a) if (!b.has(v)) return false;
  return true;
}
