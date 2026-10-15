import { useEffect } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useBanks } from '@/features/banks/hooks';
import { useCreateFund, useUpdateFund } from '@/features/funds/hooks';
import type { Fund } from '@/features/funds/types';
import { FUND_KIND_LABELS } from '@/features/cash-common/labels';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { formatApiErrorDetails } from '@/lib/api-client';
import { applyApiFieldErrors } from '@/lib/apply-api-field-errors';
import { toast } from '@/lib/use-toast';

// A fund is a cash box or a bank account; 'None' is a payment method value only.
const FUND_KINDS = ['Cash', 'Bank'] as const;

const baseSchema = z.object({
  code: z.string(),
  name: z.string().trim().min(1, 'Tên quỹ không được để trống').max(255, 'Tối đa 255 ký tự'),
  kind: z.enum(FUND_KINDS),
  bankName: z.string().trim().max(255, 'Tối đa 255 ký tự'),
  accountNumber: z.string().trim().max(50, 'Tối đa 50 ký tự'),
  bankId: z.string(), // empty = no bank chosen
  accountHolderName: z.string().trim().max(255, 'Tối đa 255 ký tự'),
  isDefault: z.boolean(),
  isActive: z.boolean(),
});

type FormValues = z.infer<typeof baseSchema>;

// A bank fund with a chosen bank prints a QR, so its account number must be a valid VietQR account.
function checkQrAccountNumber(values: FormValues, ctx: z.RefinementCtx) {
  if (values.kind === 'Bank' && values.bankId !== '' && !/^[0-9A-Za-z]{1,19}$/.test(values.accountNumber)) {
    ctx.addIssue({
      code: 'custom',
      path: ['accountNumber'],
      message: 'Số tài khoản chỉ gồm chữ và số, tối đa 19 ký tự.',
    });
  }
}

const editSchema = baseSchema.superRefine(checkQrAccountNumber);

// The code is entered only on create; it cannot change afterwards.
const createSchema = baseSchema
  .extend({
    code: z.string().trim().min(1, 'Mã quỹ không được để trống').max(20, 'Tối đa 20 ký tự'),
  })
  .superRefine(checkQrAccountNumber);

const FIELDS = [
  'code',
  'name',
  'kind',
  'bankName',
  'accountNumber',
  'bankId',
  'accountHolderName',
  'isDefault',
  'isActive',
] as const;

const NO_BANK = 'none';

interface Props {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** Pass an existing fund to enter edit mode; omit for create mode. */
  initial?: Fund;
}

export function FundFormDialog({ open, onOpenChange, initial }: Props) {
  const isEdit = !!initial;
  const { data: banks } = useBanks();
  const create = useCreateFund();
  const update = useUpdateFund();

  const form = useForm<FormValues>({
    resolver: zodResolver(isEdit ? editSchema : createSchema),
    defaultValues: toDefaults(initial),
  });

  useEffect(() => {
    if (open) form.reset(toDefaults(initial));
  }, [open, initial]); // eslint-disable-line react-hooks/exhaustive-deps

  const isPending = create.isPending || update.isPending;
  const errors = form.formState.errors;
  const isBank = form.watch('kind') === 'Bank';

  const onSubmit = async (values: FormValues) => {
    // Bank details belong to bank funds only.
    const bank = values.kind === 'Bank';
    const data = {
      name: values.name.trim(),
      kind: values.kind,
      bankName: bank ? values.bankName.trim() || null : null,
      accountNumber: bank ? values.accountNumber.trim() || null : null,
      bankId: bank && values.bankId ? values.bankId : null,
      accountHolderName: bank ? values.accountHolderName.trim() || null : null,
      isDefault: values.isDefault,
      isActive: values.isActive,
    };
    try {
      if (initial) {
        await update.mutateAsync({ id: initial.id, data });
        toast({ variant: 'success', title: 'Đã cập nhật quỹ' });
      } else {
        await create.mutateAsync({ code: values.code.trim(), ...data });
        toast({ variant: 'success', title: 'Đã tạo quỹ' });
      }
      onOpenChange(false);
    } catch (err) {
      applyApiFieldErrors(err, form.setError, FIELDS);
      toast({ variant: 'destructive', title: 'Không thể lưu', description: formatApiErrorDetails(err) });
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>{isEdit ? 'Chỉnh sửa quỹ' : 'Thêm quỹ'}</DialogTitle>
        </DialogHeader>

        <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4 pt-2">
          {!isEdit && (
            <div className="space-y-2">
              <Label htmlFor="code">Mã quỹ *</Label>
              <Input id="code" {...form.register('code')} />
              {errors.code && <p className="text-sm text-destructive">{errors.code.message}</p>}
            </div>
          )}

          <div className="space-y-2">
            <Label htmlFor="name">Tên quỹ *</Label>
            <Input id="name" {...form.register('name')} />
            {errors.name && <p className="text-sm text-destructive">{errors.name.message}</p>}
          </div>

          <div className="space-y-2">
            <Label htmlFor="kind">Loại quỹ</Label>
            <Controller
              control={form.control}
              name="kind"
              render={({ field }) => (
                <Select value={field.value} onValueChange={field.onChange}>
                  <SelectTrigger id="kind" aria-label="Loại quỹ">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {FUND_KINDS.map((k) => (
                      <SelectItem key={k} value={k}>
                        {FUND_KIND_LABELS[k]}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              )}
            />
            {errors.kind && <p className="text-sm text-destructive">{errors.kind.message}</p>}
          </div>

          {isBank && (
            <>
              <div className="grid gap-4 sm:grid-cols-2">
                <div className="space-y-2">
                  <Label htmlFor="bankName">Ngân hàng</Label>
                  <Input id="bankName" {...form.register('bankName')} />
                  {errors.bankName && <p className="text-sm text-destructive">{errors.bankName.message}</p>}
                </div>
                <div className="space-y-2">
                  <Label htmlFor="accountNumber">Số tài khoản</Label>
                  <Input id="accountNumber" {...form.register('accountNumber')} />
                  {errors.accountNumber && (
                    <p className="text-sm text-destructive">{errors.accountNumber.message}</p>
                  )}
                </div>
              </div>
              <div className="grid gap-4 sm:grid-cols-2">
                <div className="space-y-2">
                  <Label htmlFor="bankId">Ngân hàng (cho QR)</Label>
                  <Controller
                    control={form.control}
                    name="bankId"
                    render={({ field }) => (
                      <Select
                        value={field.value || NO_BANK}
                        onValueChange={(v) => field.onChange(v === NO_BANK ? '' : v)}
                      >
                        <SelectTrigger id="bankId" aria-label="Ngân hàng (cho QR)">
                          <SelectValue />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value={NO_BANK}>Chưa chọn</SelectItem>
                          {banks?.map((bank) => (
                            <SelectItem key={bank.id} value={bank.id}>
                              {`${bank.shortName ?? bank.code} · ${bank.bin}`}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    )}
                  />
                  {errors.bankId && <p className="text-sm text-destructive">{errors.bankId.message}</p>}
                </div>
                <div className="space-y-2">
                  <Label htmlFor="accountHolderName">Chủ tài khoản</Label>
                  <Input id="accountHolderName" {...form.register('accountHolderName')} />
                  {errors.accountHolderName && (
                    <p className="text-sm text-destructive">{errors.accountHolderName.message}</p>
                  )}
                </div>
              </div>
              <p className="text-xs text-muted-foreground">
                Có đủ ngân hàng, số tài khoản và chủ tài khoản thì phiếu thu/xuất in được QR.
              </p>
            </>
          )}

          <div className="space-y-1">
            <div className="flex items-center gap-2">
              <input id="isDefault" type="checkbox" className="h-4 w-4" {...form.register('isDefault')} />
              <Label htmlFor="isDefault">Quỹ mặc định của loại này</Label>
            </div>
            {errors.isDefault && <p className="text-sm text-destructive">{errors.isDefault.message}</p>}
          </div>

          <div className="space-y-1">
            <div className="flex items-center gap-2">
              <input id="isActive" type="checkbox" className="h-4 w-4" {...form.register('isActive')} />
              <Label htmlFor="isActive">Đang dùng</Label>
            </div>
            {errors.isActive && <p className="text-sm text-destructive">{errors.isActive.message}</p>}
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

function toDefaults(item?: Fund): FormValues {
  return {
    code: item?.code ?? '',
    name: item?.name ?? '',
    kind: item?.kind === 'Bank' ? 'Bank' : 'Cash',
    bankName: item?.bankName ?? '',
    accountNumber: item?.accountNumber ?? '',
    bankId: item?.bankId ?? '',
    accountHolderName: item?.accountHolderName ?? '',
    isDefault: item?.isDefault ?? false,
    isActive: item?.isActive ?? true,
  };
}
