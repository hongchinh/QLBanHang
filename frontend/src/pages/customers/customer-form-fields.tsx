import { Controller, useForm, type Resolver, type UseFormReturn } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Link } from 'react-router-dom';
import { AppIcon } from '@/components/icons';
import {
  customerSchema,
  partnerSchema,
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
import { focusNextInOrder } from '@/lib/enter-focus';
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

// 'free' mode (the quick add dialog): neither role belongs to a screen, so the labels are generic.
const FREE_TEXT: (typeof ROLE_TEXT)[PartnerRole] = {
  noun: 'đối tượng',
  code: 'Mã',
  name: 'Tên *',
  group: 'Nhóm',
  backTo: '/customers',
  module: 'customers',
};

// Enter order of the quick add dialog; ids missing (hidden role box) or disabled are skipped.
const QUICK_ADD_ORDER = [
  'code',
  'name',
  'taxCode',
  'phoneNumber',
  'email',
  'group',
  'creditDays',
  'contactPerson',
  'companyAddress',
  'defaultShippingAddress',
  'note',
  'role-customer',
  'role-supplier',
] as const;

export interface PartnerInitialRoles {
  isCustomer: boolean;
  isSupplier: boolean;
}

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
  /**
   * 'locked' (default): the screen's own role is locked on.
   * 'free': both roles are free (each needs its own create permission), take their flags only from
   * `initialRoles` and the labels are generic; for new records (the quick add dialog).
   */
  rolesMode?: 'locked' | 'free';
  /** Prefills Tên when there is no `initial` partner. */
  initialName?: string;
  /** 'free' mode only; default customer only. */
  initialRoles?: PartnerInitialRoles;
  /** Enter walks the fields (Shift+Enter back, last field submits), Ctrl+Enter submits. */
  enterNavigation?: boolean;
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
  rolesMode = 'locked',
  initialName,
  initialRoles,
  enterNavigation = false,
}: CustomerFormFieldsProps) {
  const hasPermission = useAuthStore((s) => s.hasPermission);
  const free = rolesMode === 'free';
  const form = useForm<CustomerFormValues, unknown, CustomerFormParsed>({
    // zodResolver@3 types only the input side; the parsed output (role flags filled
    // by their defaults) is what handleSubmit receives (same cast as product-form-page).
    resolver: zodResolver(
      free ? partnerSchema : role === 'supplier' ? supplierSchema : customerSchema,
    ) as unknown as Resolver<CustomerFormValues, unknown, CustomerFormParsed>,
    defaultValues: toFormDefaults(
      role,
      initial,
      free ? (initialRoles ?? { isCustomer: true, isSupplier: false }) : undefined,
      initialName,
    ),
  });

  const text = free ? FREE_TEXT : ROLE_TEXT[role];
  const otherRole: PartnerRole = role === 'customer' ? 'supplier' : 'customer';
  const other = ROLE_TEXT[otherRole];
  const otherFlag = otherRole === 'customer' ? 'isCustomer' : 'isSupplier';
  // D2: turning the other role on needs its create (new) / update (edit) permission;
  // editing a partner that already has the other role needs that role's update permission.
  const canToggleOther = hasPermission(`${other.module}.${isEdit ? 'update' : 'create'}` as Permission);
  const lockedByOtherRole =
    !free && isEdit && !!initial?.[otherFlag] && !hasPermission(`${other.module}.update` as Permission);

  const finalSubmitLabel = submitLabel ?? (isEdit ? 'Cập nhật' : 'Tạo mới');

  function handleEnterKeys(e: React.KeyboardEvent<HTMLFormElement>) {
    if (e.key !== 'Enter' || e.nativeEvent.isComposing || e.altKey || e.metaKey) return;
    const submit = () => {
      if (!submitting && !lockedByOtherRole) void form.handleSubmit(onSubmit)();
    };
    if (e.ctrlKey) {
      if (e.shiftKey) return;
      e.preventDefault();
      e.stopPropagation();
      submit();
      return;
    }
    const target = e.target as HTMLElement;
    if (!(QUICK_ADD_ORDER as readonly string[]).includes(target.id)) return; // buttons keep their own Enter
    if (target.id === 'group' && target.getAttribute('aria-expanded') === 'true') return; // an open Select is Radix's
    e.preventDefault();
    e.stopPropagation(); // capture phase: the Radix trigger would otherwise open on Enter
    const step = focusNextInOrder(QUICK_ADD_ORDER, target.id, e.shiftKey ? -1 : 1);
    if (step === 'end' && !e.shiftKey) submit();
  }

  return (
    <div className="space-y-4">
      {showHeader && (
        <div className="flex items-center gap-2">
          <Button variant="ghost" size="icon" asChild aria-label="Quay lại">
            <Link to={text.backTo}><AppIcon name="back" /></Link>
          </Button>
          <h1 className="text-2xl font-bold">{isEdit ? 'Chỉnh sửa' : 'Thêm'} {text.noun}</h1>
        </div>
      )}

      <form
        onSubmit={form.handleSubmit(onSubmit)}
        onKeyDownCapture={enterNavigation ? handleEnterKeys : undefined}
        className="space-y-4"
      >
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
            <Field label="Số ngày được nợ" name="creditDays" type="number" form={form} />
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
                    free ? (
                      <Controller
                        key={r}
                        control={form.control}
                        name={r === 'customer' ? 'isCustomer' : 'isSupplier'}
                        render={({ field }) => (
                          <RoleCheckbox
                            role={r}
                            checked={!!field.value}
                            disabled={!hasPermission(`${ROLE_TEXT[r].module}.create` as Permission)}
                            onChange={field.onChange}
                          />
                        )}
                      />
                    ) : r === role ? (
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

function toFormDefaults(
  role: PartnerRole,
  customer?: Customer,
  freeRoles?: PartnerInitialRoles,
  initialName?: string,
): CustomerFormValues {
  return {
    code: customer?.code ?? '',
    name: customer?.name ?? initialName ?? '',
    taxCode: customer?.taxCode ?? '',
    companyAddress: customer?.companyAddress ?? '',
    defaultShippingAddress: customer?.defaultShippingAddress ?? '',
    contactPerson: customer?.contactPerson ?? '',
    phoneNumber: customer?.phoneNumber ?? '',
    email: customer?.email ?? '',
    group: customer?.group ?? 'Company',
    note: customer?.note ?? '',
    status: customer?.status ?? 'Active',
    // Locked mode: the screen's own role is always on. Free mode: only the initial roles.
    isCustomer: freeRoles ? freeRoles.isCustomer : role === 'customer' || (customer?.isCustomer ?? false),
    isSupplier: freeRoles ? freeRoles.isSupplier : role === 'supplier' || (customer?.isSupplier ?? false),
    creditDays: customer?.creditDays ?? '',
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
    | 'note'
    | 'creditDays';
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
      {error && (
        <p className="text-sm text-destructive" data-testid={`${name}-error`}>
          {String(error.message)}
        </p>
      )}
    </div>
  );
}
