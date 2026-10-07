import { useEffect } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useCreateStockReason, useUpdateStockReason } from '@/features/stock-reasons/hooks';
import {
  PARTNER_TYPE_LABELS,
  STOCK_DIRECTION_LABELS,
  type PartnerType,
  type StockDirection,
  type StockReason,
} from '@/features/stock-reasons/types';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { formatApiErrorDetails } from '@/lib/api-client';
import { applyApiFieldErrors } from '@/lib/apply-api-field-errors';
import { toast } from '@/lib/use-toast';

const DIRECTIONS: StockDirection[] = ['In', 'Out'];
const PARTNER_TYPES: PartnerType[] = ['Supplier', 'Customer', 'Any', 'None'];

const editSchema = z.object({
  code: z.string(),
  name: z.string().trim().min(1, 'Tên lý do không được để trống').max(255, 'Tối đa 255 ký tự'),
  direction: z.enum(['In', 'Out']),
  partnerType: z.enum(['None', 'Customer', 'Supplier', 'Any']),
});

const createSchema = editSchema.extend({
  code: z.string().trim().min(1, 'Mã lý do không được để trống').max(50, 'Tối đa 50 ký tự'),
});

type FormValues = z.infer<typeof editSchema>;

interface Props {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  initial?: StockReason;
}

export function StockReasonFormDialog({ open, onOpenChange, initial }: Props) {
  const isEdit = !!initial;
  // System reasons keep their direction and partner type (the backend rejects a change).
  const locked = !!initial?.isSystem;
  const create = useCreateStockReason();
  const update = useUpdateStockReason();

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
    const body = { name: values.name.trim(), direction: values.direction, partnerType: values.partnerType };
    try {
      if (initial) {
        await update.mutateAsync({ id: initial.id, data: body });
        toast({ variant: 'success', title: 'Đã cập nhật lý do nhập xuất' });
      } else {
        await create.mutateAsync({ code: values.code.trim(), ...body });
        toast({ variant: 'success', title: 'Đã tạo lý do nhập xuất' });
      }
      onOpenChange(false);
    } catch (err) {
      applyApiFieldErrors(err, form.setError, ['code', 'name', 'direction', 'partnerType']);
      toast({ variant: 'destructive', title: 'Không thể lưu', description: formatApiErrorDetails(err) });
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>{isEdit ? 'Chỉnh sửa lý do nhập xuất' : 'Thêm lý do nhập xuất'}</DialogTitle>
        </DialogHeader>

        <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4 pt-2">
          {!isEdit && (
            <div className="space-y-2">
              <Label htmlFor="code">Mã lý do *</Label>
              <Input id="code" {...form.register('code')} />
              {errors.code && <p className="text-sm text-destructive">{errors.code.message}</p>}
            </div>
          )}

          <div className="space-y-2">
            <Label htmlFor="name">Tên lý do *</Label>
            <Input id="name" {...form.register('name')} />
            {errors.name && <p className="text-sm text-destructive">{errors.name.message}</p>}
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label htmlFor="direction">Loại</Label>
              <Controller
                control={form.control}
                name="direction"
                render={({ field }) => (
                  <Select value={field.value} onValueChange={field.onChange} disabled={locked}>
                    <SelectTrigger id="direction" aria-label="Loại">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {DIRECTIONS.map((d) => (
                        <SelectItem key={d} value={d}>{STOCK_DIRECTION_LABELS[d]}</SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                )}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="partnerType">Đối tượng</Label>
              <Controller
                control={form.control}
                name="partnerType"
                render={({ field }) => (
                  <Select value={field.value} onValueChange={field.onChange} disabled={locked}>
                    <SelectTrigger id="partnerType" aria-label="Đối tượng">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {PARTNER_TYPES.map((t) => (
                        <SelectItem key={t} value={t}>{PARTNER_TYPE_LABELS[t]}</SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                )}
              />
            </div>
          </div>
          {locked && (
            <p className="text-xs text-muted-foreground">
              Lý do hệ thống: không đổi được loại nhập/xuất và đối tượng.
            </p>
          )}

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

function toDefaults(item?: StockReason): FormValues {
  return {
    code: item?.code ?? '',
    name: item?.name ?? '',
    direction: item?.direction ?? 'In',
    partnerType: item?.partnerType ?? 'Any',
  };
}
