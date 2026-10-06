import { useEffect } from 'react';
import { useForm, Controller } from 'react-hook-form';
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
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { toast } from '@/lib/use-toast';
import { getErrorMessage } from '@/lib/api-client';
import {
  useAdminUserDetail,
  useCreateAdminUser,
  useUpdateAdminUser,
} from '@/features/admin-users/hooks';
import type {
  CreateUserPayload,
  UpdateUserPayload,
  UserStatus,
} from '@/features/admin-users/types';
import { useBranches } from '@/features/branches/hooks';

const ROLE_OPTIONS = ['ADMIN', 'SALES', 'MANAGER', 'ACCOUNTANT', 'WAREHOUSE'] as const;
const STATUS_OPTIONS: { value: UserStatus; label: string }[] = [
  { value: 'Active', label: 'Đang dùng' },
  { value: 'Disabled', label: 'Đã nghỉ' },
];

const baseShape = {
  fullName: z.string().trim().min(1, 'Họ tên là bắt buộc.').max(200),
  email: z.string().trim().min(1, 'Email là bắt buộc.').email('Email không hợp lệ.').max(255),
  phoneNumber: z.string().trim().max(20).optional().or(z.literal('')),
  roleCode: z.enum(ROLE_OPTIONS),
  status: z.enum(['Active', 'Disabled'] as const),
};

const createSchema = z.object({
  ...baseShape,
  username: z
    .string()
    .trim()
    .min(3, 'Username từ 3 ký tự.')
    .max(50)
    .regex(/^[a-zA-Z0-9._-]+$/, 'Chỉ chứa chữ, số và . _ -'),
  password: z
    .string()
    .min(8, 'Mật khẩu tối thiểu 8 ký tự.')
    .regex(/(?=.*[A-Za-z])(?=.*\d)/, 'Phải có cả chữ và số.'),
  // Optional: the dialog mounts before branches load; omitted → main branch (backend).
  defaultBranchId: z.string().uuid().optional(),
});

const updateSchema = z.object({
  ...baseShape,
  defaultBranchId: z.string().uuid('Chọn chi nhánh mặc định.'),
});

type CreateValues = z.infer<typeof createSchema>;
type UpdateValues = z.infer<typeof updateSchema>;

interface Props {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  mode: 'create' | 'edit';
  userId?: string;
}

export function UserFormDialog({ open, onOpenChange, mode, userId }: Props) {
  if (mode === 'edit' && userId) {
    return <EditUserDialog key={userId} open={open} onOpenChange={onOpenChange} userId={userId} />;
  }
  return <CreateUserDialog open={open} onOpenChange={onOpenChange} />;
}

function CreateUserDialog({ open, onOpenChange }: { open: boolean; onOpenChange: (o: boolean) => void }) {
  const create = useCreateAdminUser();

  const form = useForm<CreateValues>({
    resolver: zodResolver(createSchema),
    defaultValues: {
      username: '',
      email: '',
      fullName: '',
      phoneNumber: '',
      roleCode: 'SALES',
      password: '',
      status: 'Active',
    },
  });

  useEffect(() => {
    if (open) form.reset();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open]);

  const onSubmit = form.handleSubmit(async (values) => {
    const payload: CreateUserPayload = {
      username: values.username,
      email: values.email,
      fullName: values.fullName,
      phoneNumber: values.phoneNumber ? values.phoneNumber : null,
      roleCode: values.roleCode,
      password: values.password,
      status: values.status,
      ...(values.defaultBranchId ? { defaultBranchId: values.defaultBranchId } : {}),
    };
    try {
      await create.mutateAsync(payload);
      toast({ variant: 'success', title: 'Đã tạo user', description: payload.username });
      onOpenChange(false);
    } catch (err) {
      toast({ variant: 'destructive', title: 'Tạo user thất bại', description: getErrorMessage(err) });
    }
  });

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-lg">
        <DialogHeader>
          <DialogTitle>Thêm user</DialogTitle>
        </DialogHeader>

        <form onSubmit={onSubmit} className="space-y-3">
          <Field label="Username" error={form.formState.errors.username?.message}>
            <Input {...form.register('username')} />
          </Field>
          <Field label="Họ tên" error={form.formState.errors.fullName?.message}>
            <Input {...form.register('fullName')} />
          </Field>
          <Field label="Email" error={form.formState.errors.email?.message}>
            <Input type="email" {...form.register('email')} />
          </Field>
          <Field label="Số điện thoại" error={form.formState.errors.phoneNumber?.message}>
            <Input {...form.register('phoneNumber')} />
          </Field>
          <Field label="Vai trò" error={form.formState.errors.roleCode?.message}>
            <Controller
              control={form.control}
              name="roleCode"
              render={({ field }) => (
                <Select value={field.value} onValueChange={field.onChange}>
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {ROLE_OPTIONS.map((r) => (
                      <SelectItem key={r} value={r}>{r}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              )}
            />
          </Field>
          <Field label="Trạng thái" error={form.formState.errors.status?.message}>
            <Controller
              control={form.control}
              name="status"
              render={({ field }) => (
                <Select value={field.value} onValueChange={field.onChange}>
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {STATUS_OPTIONS.map((s) => (
                      <SelectItem key={s.value} value={s.value}>{s.label}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              )}
            />
          </Field>
          <Field label="Chi nhánh mặc định" error={form.formState.errors.defaultBranchId?.message}>
            <Controller
              control={form.control}
              name="defaultBranchId"
              render={({ field }) => (
                <BranchSelect
                  value={field.value}
                  onChange={field.onChange}
                  placeholder="Chi nhánh chính (mặc định)"
                />
              )}
            />
          </Field>
          <Field label="Mật khẩu" error={form.formState.errors.password?.message}>
            <Input type="password" {...form.register('password')} />
          </Field>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
              Hủy
            </Button>
            <Button type="submit" disabled={create.isPending}>
              {create.isPending ? 'Đang lưu...' : 'Tạo'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

function EditUserDialog({
  open,
  onOpenChange,
  userId,
}: {
  open: boolean;
  onOpenChange: (o: boolean) => void;
  userId: string;
}) {
  const { data: detail, isLoading } = useAdminUserDetail(open ? userId : undefined);
  const update = useUpdateAdminUser(userId);

  const form = useForm<UpdateValues>({
    resolver: zodResolver(updateSchema),
    defaultValues: {
      fullName: '',
      email: '',
      phoneNumber: '',
      roleCode: 'SALES',
      status: 'Active',
      defaultBranchId: '',
    },
  });

  useEffect(() => {
    if (detail) {
      const role = (ROLE_OPTIONS as readonly string[]).includes(detail.roleCode ?? '')
        ? (detail.roleCode as (typeof ROLE_OPTIONS)[number])
        : 'SALES';
      form.reset({
        fullName: detail.fullName,
        email: detail.email,
        phoneNumber: detail.phoneNumber ?? '',
        roleCode: role,
        status: detail.status,
        defaultBranchId: detail.defaultBranchId,
      });
    }
  }, [detail, form]);

  const onSubmit = form.handleSubmit(async (values) => {
    const payload: UpdateUserPayload = {
      fullName: values.fullName,
      email: values.email,
      phoneNumber: values.phoneNumber ? values.phoneNumber : null,
      roleCode: values.roleCode,
      status: values.status,
      defaultBranchId: values.defaultBranchId,
    };
    try {
      await update.mutateAsync(payload);
      toast({ variant: 'success', title: 'Đã cập nhật user' });
      onOpenChange(false);
    } catch (err) {
      toast({ variant: 'destructive', title: 'Cập nhật thất bại', description: getErrorMessage(err) });
    }
  });

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-lg">
        <DialogHeader>
          <DialogTitle>Sửa user{detail ? ` — ${detail.username}` : ''}</DialogTitle>
        </DialogHeader>

        {isLoading || !detail ? (
          <div className="py-8 text-center text-sm text-muted-foreground">Đang tải...</div>
        ) : (
          <form onSubmit={onSubmit} className="space-y-3">
            <Field label="Username">
              <Input value={detail.username} disabled />
            </Field>
            <Field label="Họ tên" error={form.formState.errors.fullName?.message}>
              <Input {...form.register('fullName')} />
            </Field>
            <Field label="Email" error={form.formState.errors.email?.message}>
              <Input type="email" {...form.register('email')} />
            </Field>
            <Field label="Số điện thoại" error={form.formState.errors.phoneNumber?.message}>
              <Input {...form.register('phoneNumber')} />
            </Field>
            <Field label="Vai trò" error={form.formState.errors.roleCode?.message}>
              <Controller
                control={form.control}
                name="roleCode"
                render={({ field }) => (
                  <Select value={field.value} onValueChange={field.onChange}>
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {ROLE_OPTIONS.map((r) => (
                        <SelectItem key={r} value={r}>{r}</SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                )}
              />
            </Field>
            <Field label="Trạng thái" error={form.formState.errors.status?.message}>
              <Controller
                control={form.control}
                name="status"
                render={({ field }) => (
                  <Select value={field.value} onValueChange={field.onChange}>
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {STATUS_OPTIONS.map((s) => (
                        <SelectItem key={s.value} value={s.value}>{s.label}</SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                )}
              />
            </Field>
            <Field label="Chi nhánh mặc định" error={form.formState.errors.defaultBranchId?.message}>
              <Controller
                control={form.control}
                name="defaultBranchId"
                render={({ field }) => (
                  <BranchSelect value={field.value} onChange={field.onChange} />
                )}
              />
            </Field>

            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
                Hủy
              </Button>
              <Button type="submit" disabled={update.isPending}>
                {update.isPending ? 'Đang lưu...' : 'Lưu'}
              </Button>
            </DialogFooter>
          </form>
        )}
      </DialogContent>
    </Dialog>
  );
}

function BranchSelect({
  value,
  onChange,
  placeholder,
}: {
  value: string | undefined;
  onChange: (value: string) => void;
  placeholder?: string;
}) {
  const { data: branches } = useBranches();
  return (
    // '' keeps the Select controlled and shows the placeholder.
    <Select value={value ?? ''} onValueChange={onChange}>
      <SelectTrigger aria-label="Chi nhánh mặc định">
        <SelectValue placeholder={placeholder} />
      </SelectTrigger>
      <SelectContent>
        {(branches ?? []).map((b) => (
          <SelectItem key={b.id} value={b.id}>
            {b.code} — {b.name}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
}

function Field({
  label,
  error,
  children,
}: {
  label: string;
  error?: string;
  children: React.ReactNode;
}) {
  return (
    <div className="space-y-1">
      <Label>{label}</Label>
      {children}
      {error && <p className="text-xs text-destructive">{error}</p>}
    </div>
  );
}
