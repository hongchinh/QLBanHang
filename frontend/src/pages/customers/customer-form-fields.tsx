import { Controller, useForm, type Resolver, type UseFormReturn } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Link } from 'react-router-dom';
import { ArrowLeft } from 'lucide-react';
import {
  customerSchema,
  supplierSchema,
  type CustomerFormParsed,
  type CustomerFormValues,
} from '@/features/customers/schema';
import type { Customer } from '@/features/customers/types';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { cn } from '@/lib/utils';
import type { Permission } from '@/lib/permissions';
import { useAuthStore } from '@/stores/auth-store';

const groups = [
  { value: 'Company', label: 'Công ty' },
  { value: 'Agent', label: 'Đại lý' },
  { value: 'Retail', label: 'Khách lẻ' },
  { value: 'Project', label: 'Công trình' },
] as const;

const statuses = [
  { value: 'Active', label: 'Đang sử dụng' },
  { value: 'Inactive', label: 'Ngừng sử dụng' },
] as const;

export type PartnerRole = 'customer' | 'supplier';

const ROLE_TEXT: Record<
  PartnerRole,
  { noun: string; code: string; name: string; group: string; backTo: string; module: 'customers' | 'suppliers' }
> = {
  customer: {
    noun: 'khách hàng',
    code: 'Mã khách hàng',
    name: 'Tên khách hàng *',
    group: 'Nhóm khách hàng',
    backTo: '/customers',
    module: 'customers',
  },
  supplier: {
    noun: 'nhà cung cấp',
    code: 'Mã nhà cung cấp',
    name: 'Tên nhà cung cấp *',
    group: 'Nhóm nhà cung cấp',
    backTo: '/suppliers',
    module: 'suppliers',
  },
};

export interface CustomerFormFieldsProps {
  isEdit: boolean;
  initial?: Customer;
  submitting: boolean;
  submitError: string;
  hasSubmitError: boolean;
  onSubmit: (parsed: CustomerFormParsed) => Promise<void> | void;
  onCancel: () => void;
  submitLabel?: string;
  cancelLabel?: string;
  showStatusField?: boolean;
  showHeader?: boolean;
  /** The screen's own role: drives labels, the back link and the locked role checkbox. */
  role?: PartnerRole;
  /** Hidden in the quotation quick-add dialog (a customer created there is customer-only). */
  showRoles?: boolean;
}

export function CustomerFormFields({
  isEdit,
  initial,
  submitting,
  submitError,
  hasSubmitError,
  onSubmit,
  onCancel,
  submitLabel,
  cancelLabel = 'Hủy',
  showStatusField = isEdit,
  showHeader = true,
  role = 'customer',
  showRoles = true,
}: CustomerFormFieldsProps) {
  const hasPermission = useAuthStore((s) => s.hasPermission);
  const form = useForm<CustomerFormValues, unknown, CustomerFormParsed>({
    // zodResolver@3 types only the input side; the parsed output (role flags filled
    // by their defaults) is what handleSubmit receives (same cast as product-form-page).
    resolver: zodResolver(role === 'supplier' ? supplierSchema : customerSchema) as unknown as Resolver<
      CustomerFormValues,
      unknown,
      CustomerFormParsed
    >,
    defaultValues: toFormDefaults(role, initial),
  });

  const text = ROLE_TEXT[role];
  const otherRole: PartnerRole = role === 'customer' ? 'supplier' : 'customer';
  const other = ROLE_TEXT[otherRole];
  const otherFlag = otherRole === 'customer' ? 'isCustomer' : 'isSupplier';
  // D2: turning the other role on needs its create (new) / update (edit) permission;
  // editing a partner that already has the other role needs that role's update permission.
  const canToggleOther = hasPermission(`${other.module}.${isEdit ? 'update' : 'create'}` as Permission);
  const lockedByOtherRole =
    isEdit && !!initial?.[otherFlag] && !hasPermission(`${other.module}.update` as Permission);

  const finalSubmitLabel = submitLabel ?? (isEdit ? 'Cập nhật' : 'Tạo mới');

  return (
    <div className="space-y-4">
      {showHeader && (
        <div className="flex items-center gap-2">
          <Button variant="ghost" size="icon" asChild aria-label="Quay lại">
            <Link to={text.backTo}><ArrowLeft className="h-4 w-4 text-slate-500" /></Link>
          </Button>
          <h1 className="text-2xl font-bold">{isEdit ? 'Chỉnh sửa' : 'Thêm'} {text.noun}</h1>
        </div>
      )}

      <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
        <Card>
          <CardHeader><CardTitle>Thông tin chung</CardTitle></CardHeader>
          <CardContent className="grid gap-4 md:grid-cols-2">
            <Field label={text.code} hint="Để trống để tự sinh" name="code" form={form} />
            <Field label={text.name} name="name" form={form} />
            <Field label="Mã số thuế" name="taxCode" form={form} />
            <Field label="Số điện thoại" name="phoneNumber" form={form} />
            <Field label="Email" name="email" type="email" form={form} />
            <div className="space-y-2">
              <Label htmlFor="group">{text.group}</Label>
              <Controller
                control={form.control}
                name="group"
                render={({ field }) => (
                  <Select value={field.value} onValueChange={field.onChange}>
                    <SelectTrigger id="group" aria-label={text.group}>
                      <SelectValue placeholder="Chọn nhóm" />
                    </SelectTrigger>
                    <SelectContent>
                      {groups.map((g) => (
                        <SelectItem key={g.value} value={g.value}>{g.label}</SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                )}
              />
            </div>
            <Field label="Người liên hệ" name="contactPerson" form={form} className="md:col-span-2" />
            <Field
              label="Địa chỉ công ty"
              name="companyAddress"
              form={form}
              className="md:col-span-2"
              multiline
            />
            <Field
              label="Địa chỉ giao hàng mặc định"
              name="defaultShippingAddress"
              form={form}
              className="md:col-span-2"
              multiline
            />
            <Field label="Ghi chú" name="note" form={form} className="md:col-span-2" multiline />

            {showRoles && (
              <fieldset className="space-y-2 md:col-span-2">
                <legend className="text-sm font-medium">Vai trò</legend>
                <div className="flex flex-wrap gap-6 pt-1">
                  {(['customer', 'supplier'] as const).map((r) =>
                    r === role ? (
                      <RoleCheckbox key={r} role={r} checked disabled onChange={() => {}} />
                    ) : (
                      <Controller
                        key={r}
                        control={form.control}
                        name={otherFlag}
                        render={({ field }) => (
                          <RoleCheckbox
                            role={r}
                            checked={!!field.value}
                            disabled={!canToggleOther}
                            onChange={field.onChange}
                          />
                        )}
                      />
                    ),
                  )}
                </div>
                {form.formState.errors.isCustomer && (
                  <p className="text-sm text-destructive">{form.formState.errors.isCustomer.message}</p>
                )}
              </fieldset>
            )}

            {showStatusField && (
              <div className="space-y-2">
                <Label htmlFor="status">Trạng thái</Label>
                <Controller
                  control={form.control}
                  name="status"
                  render={({ field }) => (
                    <Select value={field.value ?? 'Active'} onValueChange={field.onChange}>
                      <SelectTrigger id="status" aria-label="Trạng thái">
                        <SelectValue placeholder="Chọn trạng thái" />
                      </SelectTrigger>
                      <SelectContent>
                        {statuses.map((s) => (
                          <SelectItem key={s.value} value={s.value}>{s.label}</SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  )}
                />
              </div>
            )}
          </CardContent>
        </Card>

        {lockedByOtherRole && (
          <div className="rounded-md border border-amber-300 bg-amber-50 p-3 text-sm text-amber-800">
            Đối tượng này cũng là {other.noun} — cần quyền {other.module}.update để sửa.
          </div>
        )}

        {hasSubmitError && (
          <div className="rounded-md border border-destructive/30 bg-destructive/10 p-3 text-sm text-destructive">
            {submitError}
          </div>
        )}

        <div className="flex justify-end gap-2">
          <Button type="button" variant="outline" onClick={onCancel}>
            {cancelLabel}
          </Button>
          <Button type="submit" disabled={submitting || lockedByOtherRole}>
            {submitting ? 'Đang lưu...' : finalSubmitLabel}
          </Button>
        </div>
      </form>
    </div>
  );
}

function RoleCheckbox({
  role,
  checked,
  disabled,
  onChange,
}: {
  role: PartnerRole;
  checked: boolean;
  disabled: boolean;
  onChange: (checked: boolean) => void;
}) {
  const id = `role-${role}`;
  return (
    <div className="flex items-center gap-2">
      <input
        id={id}
        type="checkbox"
        className="h-4 w-4"
        checked={checked}
        disabled={disabled}
        onChange={(e) => onChange(e.target.checked)}
      />
      <Label htmlFor={id}>{role === 'customer' ? 'Là khách hàng' : 'Là nhà cung cấp'}</Label>
    </div>
  );
}

function toFormDefaults(role: PartnerRole, customer?: Customer): CustomerFormValues {
  return {
    code: customer?.code ?? '',
    name: customer?.name ?? '',
    taxCode: customer?.taxCode ?? '',
    companyAddress: customer?.companyAddress ?? '',
    defaultShippingAddress: customer?.defaultShippingAddress ?? '',
    contactPerson: customer?.contactPerson ?? '',
    phoneNumber: customer?.phoneNumber ?? '',
    email: customer?.email ?? '',
    group: customer?.group ?? 'Company',
    note: customer?.note ?? '',
    status: customer?.status ?? 'Active',
    // The screen's own role is always on.
    isCustomer: role === 'customer' || (customer?.isCustomer ?? false),
    isSupplier: role === 'supplier' || (customer?.isSupplier ?? false),
  };
}

interface FieldProps {
  label: string;
  name:
    | 'code'
    | 'name'
    | 'taxCode'
    | 'companyAddress'
    | 'defaultShippingAddress'
    | 'contactPerson'
    | 'phoneNumber'
    | 'email'
    | 'note';
  type?: string;
  hint?: string;
  className?: string;
  multiline?: boolean;
  form: UseFormReturn<CustomerFormValues, unknown, CustomerFormParsed>;
}

function Field({ label, name, type = 'text', hint, className, multiline, form }: FieldProps) {
  const error = form.formState.errors[name];
  return (
    <div className={cn('space-y-2', className)}>
      <Label htmlFor={name}>{label}</Label>
      {multiline ? (
        <Textarea id={name} rows={3} {...form.register(name)} />
      ) : (
        <Input id={name} type={type} {...form.register(name)} />
      )}
      {hint && !error && <p className="text-xs text-muted-foreground">{hint}</p>}
      {error && <p className="text-sm text-destructive">{String(error.message)}</p>}
    </div>
  );
}
