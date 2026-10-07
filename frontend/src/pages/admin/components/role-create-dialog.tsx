import { useEffect, useMemo, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { toast } from '@/lib/use-toast';
import { getErrorMessage } from '@/lib/api-client';
import { useCreateAdminRole } from '@/features/admin-roles/hooks';
import type { PermissionDto, PermissionModule } from '@/features/admin-roles/types';

const MODULE_LABEL: Record<PermissionModule, string> = {
  system: 'Hệ thống',
  catalog: 'Danh mục',
  sales: 'Bán hàng',
  inventory: 'Kho',
  report: 'Báo cáo',
};

const RESERVED_CODES = new Set(['ADMIN', 'SALES', 'ACCOUNTANT', 'WAREHOUSE', 'MANAGER']);

const schema = z.object({
  code: z
    .string()
    .regex(/^[A-Z_][A-Z0-9_]{1,29}$/, 'Mã vai trò: 2–30 ký tự, in hoa hoặc underscore, không bắt đầu bằng số.')
    .refine((v) => !RESERVED_CODES.has(v.toUpperCase()), {
      message: 'Mã vai trò trùng với role hệ thống.',
    }),
  name: z.string().trim().min(1, 'Tên vai trò là bắt buộc.').max(200),
  description: z.string().trim().max(500).optional().nullable(),
});

type Values = z.infer<typeof schema>;

interface Props {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  permissions: PermissionDto[];
}

export function RoleCreateDialog({ open, onOpenChange, permissions }: Props) {
  const create = useCreateAdminRole();
  const [selected, setSelected] = useState<Set<string>>(new Set());

  const grouped = useMemo(() => {
    const map = new Map<PermissionModule, PermissionDto[]>();
    for (const p of permissions) {
      const list = map.get(p.module) ?? [];
      list.push(p);
      map.set(p.module, list);
    }
    return Array.from(map.entries()).sort(([a], [b]) => a.localeCompare(b));
  }, [permissions]);

  const form = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: { code: '', name: '', description: '' },
  });

  useEffect(() => {
    if (open) {
      form.reset({ code: '', name: '', description: '' });
      setSelected(new Set());
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open]);

  const togglePerm = (code: string, checked: boolean) => {
    setSelected((prev) => {
      const next = new Set(prev);
      if (checked) next.add(code);
      else next.delete(code);
      return next;
    });
  };

  const onSubmit = form.handleSubmit(async (values) => {
    try {
      await create.mutateAsync({
        code: values.code,
        name: values.name,
        description: values.description || null,
        permissionCodes: Array.from(selected),
      });
      toast({ variant: 'success', title: 'Đã tạo vai trò', description: values.code });
      onOpenChange(false);
    } catch (err) {
      toast({ variant: 'destructive', title: 'Tạo vai trò thất bại', description: getErrorMessage(err) });
    }
  });

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-2xl">
        <DialogHeader>
          <DialogTitle>Tạo vai trò mới</DialogTitle>
        </DialogHeader>
        <form onSubmit={onSubmit} className="space-y-4">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
            <div className="space-y-1">
              <Label htmlFor="code">Mã vai trò</Label>
              <Input
                id="code"
                {...form.register('code')}
                placeholder="TEST_LEAD"
                autoComplete="off"
              />
              {form.formState.errors.code && (
                <p className="text-xs text-destructive">{form.formState.errors.code.message}</p>
              )}
            </div>
            <div className="space-y-1">
              <Label htmlFor="name">Tên hiển thị</Label>
              <Input id="name" {...form.register('name')} placeholder="Trưởng nhóm" />
              {form.formState.errors.name && (
                <p className="text-xs text-destructive">{form.formState.errors.name.message}</p>
              )}
            </div>
          </div>
          <div className="space-y-1">
            <Label htmlFor="description">Mô tả</Label>
            <Textarea id="description" rows={2} {...form.register('description')} />
          </div>

          <div className="space-y-2">
            <Label>Quyền hạn ({selected.size} đã chọn)</Label>
            <div className="border rounded-md max-h-72 overflow-auto divide-y">
              {grouped.map(([module, perms]) => (
                <div key={module} className="p-2">
                  <div className="font-medium text-sm mb-1">{MODULE_LABEL[module]}</div>
                  <div className="grid grid-cols-1 md:grid-cols-2 gap-1">
                    {perms.map((p) => (
                      <label
                        key={p.code}
                        className="flex items-center gap-2 px-2 py-1 rounded hover:bg-muted cursor-pointer"
                      >
                        <input
                          type="checkbox"
                          className="h-4 w-4 rounded border-input accent-primary"
                          checked={selected.has(p.code)}
                          onChange={(e) => togglePerm(p.code, e.target.checked)}
                        />
                        <span className="text-xs font-mono text-muted-foreground">{p.code}</span>
                        <span className="text-sm">— {p.name}</span>
                      </label>
                    ))}
                  </div>
                </div>
              ))}
            </div>
          </div>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
              Hủy
            </Button>
            <Button type="submit" disabled={create.isPending}>
              {create.isPending ? 'Đang lưu…' : 'Tạo vai trò'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
