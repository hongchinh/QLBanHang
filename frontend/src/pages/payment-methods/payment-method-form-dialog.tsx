import { useEffect } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useCreatePaymentMethod, useUpdatePaymentMethod } from '@/features/payment-methods/hooks';
import type { PaymentMethod } from '@/features/payment-methods/types';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { formatApiErrorDetails } from '@/lib/api-client';
import { applyApiFieldErrors } from '@/lib/apply-api-field-errors';
import { toast } from '@/lib/use-toast';

const editSchema = z.object({
  code: z.string(),
  name: z.string().trim().min(1, 'Tên phương thức không được để trống').max(255, 'Tối đa 255 ký tự'),
  isCash: z.boolean(),
});

const createSchema = editSchema.extend({
  code: z.string().trim().min(1, 'Mã không được để trống').max(50, 'Tối đa 50 ký tự'),
});

type FormValues = z.infer<typeof editSchema>;

interface Props {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  initial?: PaymentMethod;
}

export function PaymentMethodFormDialog({ open, onOpenChange, initial }: Props) {
  const isEdit = !!initial;
  const create = useCreatePaymentMethod();
  const update = useUpdatePaymentMethod();

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
        await update.mutateAsync({ id: initial.id, data: { name: values.name.trim(), isCash: values.isCash } });
        toast({ variant: 'success', title: 'Đã cập nhật phương thức thanh toán' });
      } else {
        await create.mutateAsync({ code: values.code.trim(), name: values.name.trim(), isCash: values.isCash });
        toast({ variant: 'success', title: 'Đã tạo phương thức thanh toán' });
      }
      onOpenChange(false);
    } catch (err) {
      applyApiFieldErrors(err, form.setError, ['code', 'name']);
      toast({ variant: 'destructive', title: 'Không thể lưu', description: formatApiErrorDetails(err) });
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>{isEdit ? 'Chỉnh sửa phương thức thanh toán' : 'Thêm phương thức thanh toán'}</DialogTitle>
        </DialogHeader>

        <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4 pt-2">
          {!isEdit && (
            <div className="space-y-2">
              <Label htmlFor="code">Mã *</Label>
              <Input id="code" {...form.register('code')} />
              {errors.code && <p className="text-sm text-destructive">{errors.code.message}</p>}
            </div>
          )}

          <div className="space-y-2">
            <Label htmlFor="name">Tên phương thức *</Label>
            <Input id="name" {...form.register('name')} />
            {errors.name && <p className="text-sm text-destructive">{errors.name.message}</p>}
          </div>

          <div className="flex items-center gap-2">
            <input id="isCash" type="checkbox" className="h-4 w-4" {...form.register('isCash')} />
            <Label htmlFor="isCash">Tiền mặt</Label>
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

function toDefaults(item?: PaymentMethod): FormValues {
  return {
    code: item?.code ?? '',
    name: item?.name ?? '',
    isCash: item?.isCash ?? false,
  };
}
