import { useEffect } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useCreateCashReason, useUpdateCashReason } from '@/features/cash-reasons/hooks';
import { CASH_REASON_DEBT_EFFECTS, type CashReason } from '@/features/cash-reasons/types';
import { CASH_VOUCHER_TYPE_LABELS, DEBT_EFFECT_LABELS } from '@/features/cash-common/labels';
import type { CashVoucherType } from '@/features/cash-common/types';
import { PARTNER_TYPE_LABELS, type PartnerType } from '@/features/stock-reasons/types';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { formatApiErrorDetails } from '@/lib/api-client';
import { applyApiFieldErrors } from '@/lib/apply-api-field-errors';
import { toast } from '@/lib/use-toast';

const TYPES: CashVoucherType[] = ['Receipt', 'Payment'];
const PARTNER_TYPES: PartnerType[] = ['Customer', 'Supplier', 'Any', 'None'];

const editSchema = z.object({
  code: z.string(),
  name: z.string().trim().min(1, 'Tên lý do không được để trống').max(255, 'Tối đa 255 ký tự'),
  type: z.enum(['Receipt', 'Payment']),
  partnerType: z.enum(['None', 'Customer', 'Supplier', 'Any']),
  debtEffect: z.enum(['None', 'ReceivableDecrease', 'ReceivableIncrease', 'PayableDecrease', 'PayableIncrease']),
});

const createSchema = editSchema.extend({
  code: z.string().trim().min(1, 'Mã lý do không được để trống').max(20, 'Tối đa 20 ký tự'),
});

type FormValues = z.infer<typeof editSchema>;

interface Props {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  initial?: CashReason;
  /** Voucher type preselected on create (the list's type filter). */
  defaultType?: CashVoucherType;
}

export function CashReasonFormDialog({ open, onOpenChange, initial, defaultType }: Props) {
  const isEdit = !!initial;
  // System reasons keep their voucher type and partner type (the backend rejects a change).
  const locked = !!initial?.isSystem;
  const create = useCreateCashReason();
  const update = useUpdateCashReason();

  const form = useForm<FormValues>({
    resolver: zodResolver(isEdit ? editSchema : createSchema),
    defaultValues: toDefaults(initial, defaultType),
  });

  useEffect(() => {
    if (open) form.reset(toDefaults(initial, defaultType));
  }, [open, initial, defaultType]); // eslint-disable-line react-hooks/exhaustive-deps

  const type = form.watch('type');
  const effects = CASH_REASON_DEBT_EFFECTS[type];
  const isPending = create.isPending || update.isPending;
  const errors = form.formState.errors;

  const onTypeChange = (next: CashVoucherType) => {
    form.setValue('type', next, { shouldDirty: true });
    if (!CASH_REASON_DEBT_EFFECTS[next].includes(form.getValues('debtEffect'))) {
      form.setValue('debtEffect', 'None', { shouldDirty: true });
    }
  };

  const onSubmit = async (values: FormValues) => {
    const body = {
      name: values.name.trim(),
      type: values.type,
      partnerType: values.partnerType,
      debtEffect: values.debtEffect,
    };
    try {
      if (initial) {
        await update.mutateAsync({ id: initial.id, data: body });
        toast({ variant: 'success', title: 'Đã cập nhật lý do thu/chi' });
      } else {
        await create.mutateAsync({ code: values.code.trim(), ...body });
        toast({ variant: 'success', title: 'Đã tạo lý do thu/chi' });
      }
      onOpenChange(false);
    } catch (err) {
      applyApiFieldErrors(err, form.setError, ['code', 'name', 'type', 'partnerType', 'debtEffect']);
      toast({ variant: 'destructive', title: 'Không thể lưu', description: formatApiErrorDetails(err) });
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>{isEdit ? 'Chỉnh sửa lý do thu/chi' : 'Thêm lý do thu/chi'}</DialogTitle>
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
              <Label htmlFor="type">Loại phiếu</Label>
              <Controller
                control={form.control}
                name="type"
                render={({ field }) => (
                  <Select
                    value={field.value}
                    onValueChange={(v) => onTypeChange(v as CashVoucherType)}
                    disabled={locked}
                  >
                    <SelectTrigger id="type" aria-label="Loại phiếu">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {TYPES.map((t) => (
                        <SelectItem key={t} value={t}>{CASH_VOUCHER_TYPE_LABELS[t]}</SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                )}
              />
              {errors.type && <p className="text-sm text-destructive">{errors.type.message}</p>}
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
              {errors.partnerType && <p className="text-sm text-destructive">{errors.partnerType.message}</p>}
            </div>
          </div>

          <div className="space-y-2">
            <Label htmlFor="debtEffect">Ảnh hưởng công nợ</Label>
            <Controller
              control={form.control}
              name="debtEffect"
              render={({ field }) => (
                <Select value={field.value} onValueChange={field.onChange}>
                  <SelectTrigger id="debtEffect" aria-label="Ảnh hưởng công nợ">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {effects.map((e) => (
                      <SelectItem key={e} value={e}>{DEBT_EFFECT_LABELS[e]}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              )}
            />
            {errors.debtEffect && <p className="text-sm text-destructive">{errors.debtEffect.message}</p>}
          </div>
          {locked && (
            <p className="text-xs text-muted-foreground">
              Lý do hệ thống: không đổi được loại phiếu và đối tượng.
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

function toDefaults(item?: CashReason, defaultType?: CashVoucherType): FormValues {
  return {
    code: item?.code ?? '',
    name: item?.name ?? '',
    type: item?.type ?? defaultType ?? 'Receipt',
    partnerType: item?.partnerType ?? 'Any',
    debtEffect: item?.debtEffect ?? 'None',
  };
}
