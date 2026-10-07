import { useEffect } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useCreateWarehouse, useUpdateWarehouse } from '@/features/warehouses/hooks';
import type { Warehouse } from '@/features/warehouses/types';
import { useBranches } from '@/features/branches/hooks';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { formatApiErrorDetails } from '@/lib/api-client';
import { applyApiFieldErrors } from '@/lib/apply-api-field-errors';
import { toast } from '@/lib/use-toast';
import { useAuthStore } from '@/stores/auth-store';

const editSchema = z.object({
  code: z.string(),
  name: z.string().trim().min(1, 'Tên kho không được để trống').max(255, 'Tối đa 255 ký tự'),
  branchId: z.string(),
  isActive: z.boolean(),
});

// The code is entered only on create; it cannot change afterwards.
const createSchema = editSchema.extend({
  code: z.string().trim().min(1, 'Mã kho không được để trống').max(50, 'Tối đa 50 ký tự'),
});

type FormValues = z.infer<typeof editSchema>;

interface Props {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** Pass an existing warehouse to enter edit mode; omit for create mode. */
  initial?: Warehouse;
}

export function WarehouseFormDialog({ open, onOpenChange, initial }: Props) {
  const isEdit = !!initial;
  const hasPermission = useAuthStore((s) => s.hasPermission);
  const canPickBranch = hasPermission('branches.access_all');
  const create = useCreateWarehouse();
  const update = useUpdateWarehouse();
  const { data: branches } = useBranches();

  const form = useForm<FormValues>({
    resolver: zodResolver(isEdit ? editSchema : createSchema),
    defaultValues: toDefaults(initial),
  });

  useEffect(() => {
    if (open) form.reset(toDefaults(initial));
  }, [open, initial]); // eslint-disable-line react-hooks/exhaustive-deps

  const isPending = create.isPending || update.isPending;
  const errors = form.formState.errors;

  const onSubmit = async (values: FormValues) => {
    try {
      if (initial) {
        await update.mutateAsync({
          id: initial.id,
          data: {
            name: values.name.trim(),
            branchId: values.branchId || initial.branchId,
            isActive: values.isActive,
          },
        });
        toast({ variant: 'success', title: 'Đã cập nhật kho' });
      } else {
        await create.mutateAsync({
          code: values.code.trim(),
          name: values.name.trim(),
          branchId: values.branchId || undefined,
          isActive: values.isActive,
        });
        toast({ variant: 'success', title: 'Đã tạo kho' });
      }
      onOpenChange(false);
    } catch (err) {
      applyApiFieldErrors(err, form.setError, ['code', 'name', 'branchId']);
      toast({ variant: 'destructive', title: 'Không thể lưu', description: formatApiErrorDetails(err) });
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>{isEdit ? 'Chỉnh sửa kho' : 'Thêm kho'}</DialogTitle>
        </DialogHeader>

        <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4 pt-2">
          {!isEdit && (
            <div className="space-y-2">
              <Label htmlFor="code">Mã kho *</Label>
              <Input id="code" {...form.register('code')} />
              {errors.code && <p className="text-sm text-destructive">{errors.code.message}</p>}
            </div>
          )}

          <div className="space-y-2">
            <Label htmlFor="name">Tên kho *</Label>
            <Input id="name" {...form.register('name')} />
            {errors.name && <p className="text-sm text-destructive">{errors.name.message}</p>}
          </div>

          {canPickBranch && (
            <div className="space-y-2">
              <Label htmlFor="branchId">Chi nhánh</Label>
              <Controller
                control={form.control}
                name="branchId"
                render={({ field }) => (
                  <Select value={field.value || undefined} onValueChange={field.onChange}>
                    <SelectTrigger id="branchId" aria-label="Chi nhánh">
                      <SelectValue placeholder="Chi nhánh làm việc" />
                    </SelectTrigger>
                    <SelectContent>
                      {(branches ?? []).map((b) => (
                        <SelectItem key={b.id} value={b.id}>
                          {b.code} — {b.name}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                )}
              />
              {errors.branchId && <p className="text-sm text-destructive">{errors.branchId.message}</p>}
            </div>
          )}

          <div className="flex items-center gap-2">
            <input id="isActive" type="checkbox" className="h-4 w-4" {...form.register('isActive')} />
            <Label htmlFor="isActive">Đang hoạt động</Label>
          </div>

          <div className="flex justify-end gap-2 pt-2">
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)} disabled={isPending}>
              Hủy
            </Button>
            <Button type="submit" disabled={isPending}>
              {isPending ? 'Đang lưu...' : isEdit ? 'Cập nhật' : 'Tạo mới'}
            </Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  );
}

function toDefaults(item?: Warehouse): FormValues {
  return {
    code: item?.code ?? '',
    name: item?.name ?? '',
    branchId: item?.branchId ?? '',
    isActive: item?.isActive ?? true,
  };
}
