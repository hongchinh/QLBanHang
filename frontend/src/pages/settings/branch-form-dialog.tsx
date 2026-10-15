import { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useCreateBranch, useSetBranchOpeningDate, useUpdateBranch } from '@/features/branches/hooks';
import type { Branch } from '@/features/branches/types';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { formatApiErrorDetails, getApiError } from '@/lib/api-client';
import { applyApiFieldErrors } from '@/lib/apply-api-field-errors';
import { toast } from '@/lib/use-toast';

const editSchema = z.object({
  code: z.string(),
  name: z.string().trim().min(1, 'Tên chi nhánh không được để trống').max(255, 'Tối đa 255 ký tự'),
  address: z.string().max(1000, 'Tối đa 1000 ký tự'),
  // Create only; kept in the shared shape so both resolvers type the same values.
  withSampleData: z.boolean(),
});

// The code is entered only on create; it cannot change afterwards.
const createSchema = editSchema.extend({
  code: z.string().trim().min(1, 'Mã chi nhánh không được để trống').max(50, 'Tối đa 50 ký tự'),
});

type FormValues = z.infer<typeof editSchema>;

interface Props {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** Pass an existing branch to enter edit mode; omit for create mode. */
  initial?: Branch;
}

export function BranchFormDialog({ open, onOpenChange, initial }: Props) {
  const isEdit = !!initial;
  const create = useCreateBranch();
  const update = useUpdateBranch();

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
    const address = values.address.trim() || null;
    try {
      if (initial) {
        await update.mutateAsync({ id: initial.id, data: { name: values.name.trim(), address } });
        toast({ variant: 'success', title: 'Đã cập nhật chi nhánh' });
      } else {
        await create.mutateAsync({
          code: values.code.trim(),
          name: values.name.trim(),
          address,
          withSampleData: values.withSampleData,
        });
        if (values.withSampleData) {
          toast({
            variant: 'success',
            title: 'Đã tạo chi nhánh demo',
            description: 'Dữ liệu mẫu đang được tạo, có thể mất khoảng một phút.',
          });
        } else {
          toast({ variant: 'success', title: 'Đã tạo chi nhánh' });
        }
      }
      onOpenChange(false);
    } catch (err) {
      applyApiFieldErrors(err, form.setError, ['code', 'name', 'address']);
      toast({ variant: 'destructive', title: 'Không thể lưu', description: formatApiErrorDetails(err) });
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>{isEdit ? 'Chỉnh sửa chi nhánh' : 'Thêm chi nhánh'}</DialogTitle>
        </DialogHeader>

        <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4 pt-2">
          {!isEdit && (
            <div className="space-y-2">
              <Label htmlFor="code">Mã chi nhánh *</Label>
              <Input id="code" {...form.register('code')} />
              {errors.code && <p className="text-sm text-destructive">{errors.code.message}</p>}
            </div>
          )}

          <div className="space-y-2">
            <Label htmlFor="name">Tên chi nhánh *</Label>
            <Input id="name" {...form.register('name')} />
            {errors.name && <p className="text-sm text-destructive">{errors.name.message}</p>}
          </div>

          <div className="space-y-2">
            <Label htmlFor="address">Địa chỉ</Label>
            <Input id="address" {...form.register('address')} />
            {errors.address && <p className="text-sm text-destructive">{errors.address.message}</p>}
          </div>

          {!isEdit && (
            <div className="space-y-1">
              <div className="flex items-center gap-2">
                <input id="withSampleData" type="checkbox" className="h-4 w-4" {...form.register('withSampleData')} />
                <Label htmlFor="withSampleData">Tạo kèm dữ liệu mẫu để đào tạo</Label>
              </div>
              <p className="text-xs text-muted-foreground">
                Tạo danh mục mẫu, tồn kho, công nợ, số dư quỹ đầu kỳ và khoảng 3 tháng chứng từ. Chi nhánh được đánh dấu
                Demo và xóa được bằng nút "Xóa chi nhánh demo".
              </p>
            </div>
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

function toDefaults(item?: Branch): FormValues {
  return {
    code: item?.code ?? '',
    name: item?.name ?? '',
    address: item?.address ?? '',
    withSampleData: false,
  };
}

interface OpeningDateDialogProps {
  /** The branch whose opening date is edited; null closes the dialog. */
  branch: Branch | null;
  onClose: () => void;
}

// The server accepts the change only while the branch has no documents (409 BRANCH_HAS_DOCUMENTS, P8).
export function BranchOpeningDateDialog({ branch, onClose }: OpeningDateDialogProps) {
  const setOpeningDate = useSetBranchOpeningDate();
  const [date, setDate] = useState('');
  const [error, setError] = useState<string>();

  useEffect(() => {
    if (!branch) return;
    setDate(branch.openingDate ?? '');
    setError(undefined);
  }, [branch]);

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!branch) return;
    setError(undefined);
    try {
      await setOpeningDate.mutateAsync({ id: branch.id, openingDate: date || null });
      toast({ variant: 'success', title: 'Đã lưu ngày đầu kỳ', description: `${branch.code} — ${branch.name}` });
      onClose();
    } catch (err) {
      setError(getApiError(err)?.details?.openingDate?.[0]);
      toast({ variant: 'destructive', title: 'Không thể lưu', description: formatApiErrorDetails(err) });
    }
  };

  return (
    <Dialog open={!!branch} onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>Ngày đầu kỳ{branch ? ` — ${branch.name}` : ''}</DialogTitle>
        </DialogHeader>

        <form onSubmit={onSubmit} className="space-y-4 pt-2">
          <div className="space-y-2">
            <Label htmlFor="branch-opening-date">Ngày đầu kỳ</Label>
            <Input id="branch-opening-date" type="date" value={date} onChange={(e) => setDate(e.target.value)} />
            {error ? (
              <p className="text-sm text-destructive">{error}</p>
            ) : (
              <p className="text-xs text-muted-foreground">
                Tồn đầu kỳ, công nợ đầu kỳ và số dư đầu quỹ dùng ngày này. Chỉ đổi được khi chi nhánh chưa có chứng từ.
              </p>
            )}
          </div>

          <div className="flex justify-end gap-2 pt-2">
            <Button type="button" variant="outline" onClick={onClose} disabled={setOpeningDate.isPending}>
              Hủy
            </Button>
            <Button type="submit" disabled={setOpeningDate.isPending}>
              {setOpeningDate.isPending ? 'Đang lưu...' : 'Lưu'}
            </Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  );
}
