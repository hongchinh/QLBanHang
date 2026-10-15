import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useCompanyProfile, useUpdateCompanyProfile } from '@/features/company-profile/hooks';
import type { CompanyProfile, UpdateCompanyProfileRequest } from '@/features/company-profile/types';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { formatApiErrorDetails, getErrorMessage } from '@/lib/api-client';
import { applyApiFieldErrors } from '@/lib/apply-api-field-errors';
import { toast } from '@/lib/use-toast';
import { BackLink } from '@/components/layout/back-link';

const schema = z.object({
  name: z.string().trim().min(1, 'Vui lòng nhập tên đơn vị').max(255, 'Tên đơn vị tối đa 255 ký tự'),
  address: z.string().trim().max(500, 'Địa chỉ tối đa 500 ký tự'),
  taxCode: z
    .string()
    .trim()
    .refine((v) => v === '' || /^\d{10}(-\d{3})?$/.test(v), 'Mã số thuế gồm 10 số, hoặc 10 số, dấu gạch và 3 số'),
  chiefAccountantName: z.string().trim().max(150, 'Họ tên tối đa 150 ký tự'),
  legalRepresentativeName: z.string().trim().max(150, 'Họ tên tối đa 150 ký tự'),
  accountingServiceNote: z.string().trim().max(500, 'Ghi chú tối đa 500 ký tự'),
});

type FormValues = z.infer<typeof schema>;

const FIELDS = [
  'name',
  'address',
  'taxCode',
  'chiefAccountantName',
  'legalRepresentativeName',
  'accountingServiceNote',
] as const;

const toNullable = (value: string) => (value === '' ? null : value);

// Route permission: settings.company.
export function CompanyProfilePage() {
  const { data, isLoading, isError, error } = useCompanyProfile();

  return (
    <div className="space-y-4">
      <div className="flex items-start gap-2">
        <BackLink to="/settings" label="Cấu hình" />
        <div>
          <h1 className="text-2xl font-bold">Thông tin đơn vị</h1>
          <p className="text-sm text-muted-foreground">
            Tên, địa chỉ, mã số thuế và người ký in trên các sổ theo mẫu TT133
          </p>
        </div>
      </div>

      {isError && (
        <div className="rounded-md border border-destructive/30 bg-destructive/10 p-3 text-sm text-destructive">
          {getErrorMessage(error)}
        </div>
      )}
      {isLoading && <div className="text-sm text-muted-foreground">Đang tải...</div>}
      {data && <CompanyProfileForm initial={data} />}
    </div>
  );
}

function CompanyProfileForm({ initial }: { initial: CompanyProfile }) {
  const update = useUpdateCompanyProfile();
  const form = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      name: initial.name ?? '',
      address: initial.address ?? '',
      taxCode: initial.taxCode ?? '',
      chiefAccountantName: initial.chiefAccountantName ?? '',
      legalRepresentativeName: initial.legalRepresentativeName ?? '',
      accountingServiceNote: initial.accountingServiceNote ?? '',
    },
  });
  const errors = form.formState.errors;

  const onSubmit = async (values: FormValues) => {
    const body: UpdateCompanyProfileRequest = {
      name: values.name,
      address: toNullable(values.address),
      taxCode: toNullable(values.taxCode),
      chiefAccountantName: toNullable(values.chiefAccountantName),
      legalRepresentativeName: toNullable(values.legalRepresentativeName),
      accountingServiceNote: toNullable(values.accountingServiceNote),
    };
    try {
      await update.mutateAsync(body);
      toast({ variant: 'success', title: 'Đã lưu thông tin đơn vị' });
    } catch (err) {
      applyApiFieldErrors(err, form.setError, FIELDS);
      toast({ variant: 'destructive', title: 'Không thể lưu', description: formatApiErrorDetails(err) });
    }
  };

  return (
    <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
      <Card>
        <CardHeader>
          <CardTitle>Đơn vị</CardTitle>
        </CardHeader>
        <CardContent className="grid gap-4 md:grid-cols-2">
          <div className="space-y-2 md:col-span-2">
            <Label htmlFor="name">Tên đơn vị</Label>
            <Input id="name" {...form.register('name')} />
            {errors.name && <p className="text-sm text-destructive">{errors.name.message}</p>}
          </div>
          <div className="space-y-2 md:col-span-2">
            <Label htmlFor="address">Địa chỉ</Label>
            <Input id="address" {...form.register('address')} />
            {errors.address && <p className="text-sm text-destructive">{errors.address.message}</p>}
          </div>
          <div className="space-y-2">
            <Label htmlFor="taxCode">Mã số thuế</Label>
            <Input id="taxCode" {...form.register('taxCode')} />
            {errors.taxCode && <p className="text-sm text-destructive">{errors.taxCode.message}</p>}
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Người ký</CardTitle>
        </CardHeader>
        <CardContent className="grid gap-4 md:grid-cols-2">
          <div className="space-y-2">
            <Label htmlFor="chiefAccountantName">Kế toán trưởng</Label>
            <Input id="chiefAccountantName" {...form.register('chiefAccountantName')} />
            {errors.chiefAccountantName && (
              <p className="text-sm text-destructive">{errors.chiefAccountantName.message}</p>
            )}
          </div>
          <div className="space-y-2">
            <Label htmlFor="legalRepresentativeName">Người đại diện theo pháp luật</Label>
            <Input id="legalRepresentativeName" {...form.register('legalRepresentativeName')} />
            {errors.legalRepresentativeName && (
              <p className="text-sm text-destructive">{errors.legalRepresentativeName.message}</p>
            )}
          </div>
          <div className="space-y-2 md:col-span-2">
            <Label htmlFor="accountingServiceNote">Ghi chú dịch vụ kế toán</Label>
            <Textarea id="accountingServiceNote" rows={3} {...form.register('accountingServiceNote')} />
            {errors.accountingServiceNote && (
              <p className="text-sm text-destructive">{errors.accountingServiceNote.message}</p>
            )}
          </div>
        </CardContent>
      </Card>

      <div className="flex justify-end">
        <Button type="submit" disabled={update.isPending}>
          {update.isPending ? 'Đang lưu...' : 'Lưu'}
        </Button>
      </div>
    </form>
  );
}
