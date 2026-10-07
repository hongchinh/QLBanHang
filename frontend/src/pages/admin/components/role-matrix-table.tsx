import { useEffect, useRef } from 'react';
import { MoreVertical } from 'lucide-react';
import type { PermissionDto, PermissionModule, RoleDetail, RoleListItem } from '@/features/admin-roles/types';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';

const ADMIN_CODE = 'ADMIN';

const MODULE_LABEL: Record<PermissionModule, string> = {
  system: 'Hệ thống',
  catalog: 'Danh mục',
  sales: 'Bán hàng',
  inventory: 'Kho',
  report: 'Báo cáo',
};

const MODULE_ORDER: PermissionModule[] = ['system', 'catalog', 'sales', 'inventory', 'report'];

interface Props {
  roles: RoleListItem[];
  details: RoleDetail[];
  permissions: PermissionDto[];
  // Map<roleId, Set<permissionCode>> — current edit state (includes dirty).
  state: Map<string, Set<string>>;
  canManage: boolean;
  onToggle: (roleId: string, permissionCode: string, checked: boolean) => void;
  onToggleModule: (roleId: string, module: PermissionModule, checked: boolean) => void;
  onRequestRename: (role: RoleListItem) => void;
  onRequestDelete: (role: RoleListItem) => void;
}

export function RoleMatrixTable({
  roles,
  permissions,
  state,
  canManage,
  onToggle,
  onToggleModule,
  onRequestRename,
  onRequestDelete,
}: Props) {
  // Modules the frontend doesn't know yet go last with their raw name, so a new
  // backend module never disappears from the matrix.
  const unknownModules = [...new Set(permissions.map((p) => p.module))].filter(
    (m) => !MODULE_ORDER.includes(m),
  );
  const grouped = [...MODULE_ORDER, ...unknownModules]
    .map((module) => ({
      module,
      label: MODULE_LABEL[module] ?? module,
      perms: permissions.filter((p) => p.module === module),
    }))
    .filter((g) => g.perms.length > 0);

  return (
    <div className="overflow-auto max-h-[calc(100vh-220px)] border rounded-md">
      <table className="min-w-full text-sm">
        <thead className="sticky top-0 bg-background z-10 border-b">
          <tr>
            <th className="sticky left-0 bg-background text-left px-3 py-2 min-w-[260px] border-r">
              Quyền
            </th>
            {roles.map((role) => {
              const isAdmin = role.code === ADMIN_CODE;
              return (
                <th
                  key={role.id}
                  className="text-center px-2 py-2 min-w-[140px] align-top"
                >
                  <div className="flex items-center justify-center gap-1">
                    <div className="flex flex-col items-center">
                      <span
                        className="font-medium"
                        title={role.name}
                      >
                        {role.code}
                      </span>
                      <span className="text-xs text-muted-foreground truncate max-w-[120px]">
                        {isAdmin ? 'Luôn full quyền' : role.name}
                      </span>
                    </div>
                    {!role.isSystem && canManage && (
                      <DropdownMenu>
                        <DropdownMenuTrigger asChild>
                          <Button
                            variant="ghost"
                            size="icon"
                            className="h-7 w-7"
                            aria-label={`Hành động cho role ${role.code}`}
                          >
                            <MoreVertical className="h-4 w-4" />
                          </Button>
                        </DropdownMenuTrigger>
                        <DropdownMenuContent align="end">
                          <DropdownMenuItem onSelect={() => onRequestRename(role)}>
                            Đổi tên
                          </DropdownMenuItem>
                          <DropdownMenuItem
                            onSelect={() => onRequestDelete(role)}
                            className="text-destructive"
                          >
                            Xoá role
                          </DropdownMenuItem>
                        </DropdownMenuContent>
                      </DropdownMenu>
                    )}
                  </div>
                </th>
              );
            })}
          </tr>
        </thead>
        <tbody>
          {grouped.map(({ module, label, perms }) => (
            <ModuleGroup
              key={module}
              module={module}
              label={label}
              perms={perms}
              roles={roles}
              state={state}
              canManage={canManage}
              onToggle={onToggle}
              onToggleModule={onToggleModule}
            />
          ))}
        </tbody>
      </table>
    </div>
  );
}

function ModuleGroup({
  module,
  label,
  perms,
  roles,
  state,
  canManage,
  onToggle,
  onToggleModule,
}: {
  module: PermissionModule;
  label: string;
  perms: PermissionDto[];
  roles: RoleListItem[];
  state: Map<string, Set<string>>;
  canManage: boolean;
  onToggle: (roleId: string, permissionCode: string, checked: boolean) => void;
  onToggleModule: (roleId: string, module: PermissionModule, checked: boolean) => void;
}) {
  const colSpan = 1 + roles.length;
  return (
    <>
      <tr className="bg-muted/60">
        <td className="sticky left-0 bg-muted/60 px-3 py-1.5 font-medium border-r" colSpan={1}>
          {label}
        </td>
        {roles.map((role) => {
          const isAdmin = role.code === ADMIN_CODE;
          const current = state.get(role.id) ?? new Set<string>();
          const checkedCount = perms.filter((p) => current.has(p.code)).length;
          const allChecked = checkedCount === perms.length;
          const someChecked = checkedCount > 0 && !allChecked;
          return (
            <td key={role.id} className="text-center px-2 py-1.5 bg-muted/60">
              <ModuleSelectAllCheckbox
                checked={isAdmin ? true : allChecked}
                indeterminate={isAdmin ? false : someChecked}
                disabled={isAdmin || !canManage}
                title={isAdmin ? 'ADMIN luôn có toàn bộ quyền' : `Chọn tất cả ${label}`}
                onChange={(next) => onToggleModule(role.id, module, next)}
              />
            </td>
          );
        })}
      </tr>
      {perms.map((perm) => (
        <tr key={perm.code} className="border-b last:border-0">
          <td className="sticky left-0 bg-background px-3 py-1.5 border-r">
            <div className="flex flex-col">
              <span className="font-mono text-xs text-muted-foreground">{perm.code}</span>
              <span>{perm.name}</span>
            </div>
          </td>
          {roles.map((role) => {
            const isAdmin = role.code === ADMIN_CODE;
            const current = state.get(role.id) ?? new Set<string>();
            const checked = isAdmin ? true : current.has(perm.code);
            return (
              <td key={role.id} className="text-center px-2 py-1.5">
                <input
                  type="checkbox"
                  className={cn(
                    'h-4 w-4 rounded border-input accent-primary',
                    (isAdmin || !canManage) && 'cursor-not-allowed opacity-60',
                  )}
                  checked={checked}
                  disabled={isAdmin || !canManage}
                  title={isAdmin ? 'ADMIN luôn có toàn bộ quyền' : `${role.code} — ${perm.code}`}
                  aria-label={`${role.code} ${perm.code}`}
                  onChange={(e) => onToggle(role.id, perm.code, e.target.checked)}
                />
              </td>
            );
          })}
        </tr>
      ))}
      <tr aria-hidden="true">
        <td colSpan={colSpan} className="h-2" />
      </tr>
    </>
  );
}

function ModuleSelectAllCheckbox({
  checked,
  indeterminate,
  disabled,
  title,
  onChange,
}: {
  checked: boolean;
  indeterminate: boolean;
  disabled: boolean;
  title: string;
  onChange: (next: boolean) => void;
}) {
  const ref = useRef<HTMLInputElement>(null);
  useEffect(() => {
    if (ref.current) ref.current.indeterminate = indeterminate;
  }, [indeterminate]);

  return (
    <input
      ref={ref}
      type="checkbox"
      className={cn(
        'h-4 w-4 rounded border-input accent-primary',
        disabled && 'cursor-not-allowed opacity-60',
      )}
      checked={checked}
      disabled={disabled}
      title={title}
      onChange={(e) => onChange(e.target.checked)}
    />
  );
}
