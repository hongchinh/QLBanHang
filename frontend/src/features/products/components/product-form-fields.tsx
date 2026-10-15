import { useEffect, useRef, useState } from 'react';
import { Controller, useForm, type Resolver, type UseFormReturn } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Link } from 'react-router-dom';
import { AppIcon } from '@/components/icons';
import {
  productSchema,
  type ProductFormParsed,
  type ProductFormValues,
} from '@/features/products/schema';
import type { LookupItem, Product } from '@/features/products/types';
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

const statuses = [
  { value: 'Active', label: 'Đang bán' },
  { value: 'Inactive', label: 'Ngừng bán' },
] as const;

const pricingModes = [
  { value: 'PerUnit', label: 'Theo đơn vị' },
  { value: 'PerSquareMeter', label: 'Theo m²' },
  { value: 'PerLinearMeter', label: 'Theo m dài' },
  { value: 'PerCubicMeter', label: 'Theo m³' },
] as const;

// Field ids in visual order. Missing or disabled ones (status on create, locked fields) are skipped.
const ENTER_ORDER = [
  'code', 'name', 'productGroupId', 'unitId', 'specification', 'pricingMode', 'status',
  'length', 'width', 'thickness', 'density',
  'defaultPrice', 'costPrice', 'defaultTaxRate',
  'trackInventory', 'priceIncludesVat', 'purchaseDiscountRate', 'salesDiscountRate',
  'note',
] as const;
const SELECT_IDS: readonly string[] = ['productGroupId', 'pricingMode', 'status'];

export interface ProductFormFieldsProps {
  isEdit: boolean;
  initial?: Product;
  groups: { id: string; code: string; name: string }[];
  units: { id: string; code: string; name: string }[];
  submitting: boolean;
  submitError: string;
  hasSubmitError: boolean;
  onSubmit: (parsed: ProductFormParsed) => void | Promise<void>;
  onCancel: () => void;
  /** Back link + title. The quick add dialog has its own title, so it turns this off. */
  showHeader?: boolean;
  /** Prefill for a new product; ignored when `initial` is given. */
  initialCode?: string;
  initialName?: string;
  /** Enter moves to the next field, Ctrl+Enter saves (quick add dialog). */
  enterNavigation?: boolean;
  /** Overrides the submit button text. */
  submitLabel?: string;
}

export function ProductFormFields({
  isEdit,
  initial,
  groups,
  units,
  submitting,
  submitError,
  hasSubmitError,
  onSubmit,
  onCancel,
  showHeader = true,
  initialCode,
  initialName,
  enterNavigation,
  submitLabel,
}: ProductFormFieldsProps) {
  const form = useForm<ProductFormValues, unknown, ProductFormParsed>({
    // zodResolver in @hookform/resolvers@3.9 only types TFieldValues, so its
    // returned resolver looks like Resolver<Input, _, Input> instead of
    // Resolver<Input, _, Output>. The runtime value passed to handleSubmit
    // *is* the parsed (transformed) shape, so this cast just aligns the types.
    resolver: zodResolver(productSchema) as unknown as Resolver<ProductFormValues, unknown, ProductFormParsed>,
    defaultValues: toFormDefaults(initial, initialCode, initialName),
  });
  const locked = !!initial?.hasInventoryActivity;

  // One submit at a time: react-hook-form does not block a second handleSubmit while the first onSubmit runs,
  // and the ref is checked before validation so that time is covered too (state would lag a repeating key).
  const busy = useRef(false);
  const submitGuarded = form.handleSubmit(async (parsed) => {
    try {
      await onSubmit(parsed);
    } finally {
      busy.current = false;
    }
  });
  async function guardedSubmit(e?: React.BaseSyntheticEvent) {
    if (busy.current) {
      e?.preventDefault();
      return;
    }
    busy.current = true;
    await submitGuarded(e);
    busy.current = false; // validation failed: onSubmit never ran
  }

  function handleEnterKeys(e: React.KeyboardEvent<HTMLFormElement>) {
    if (e.key !== 'Enter' || e.nativeEvent.isComposing || e.altKey || e.metaKey) return;
    const submit = () => {
      if (!submitting) void guardedSubmit();
    };
    if (e.ctrlKey) {
      if (e.shiftKey) return;
      e.preventDefault();
      e.stopPropagation();
      submit();
      return;
    }
    const target = e.target as HTMLElement;
    if (!(ENTER_ORDER as readonly string[]).includes(target.id)) return; // buttons keep their own Enter
    if (SELECT_IDS.includes(target.id) && target.getAttribute('aria-expanded') === 'true') return; // an open Select is Radix's
    e.preventDefault();
    e.stopPropagation(); // capture phase: the Radix trigger would otherwise open on Enter
    const step = focusNextInOrder(ENTER_ORDER, target.id, e.shiftKey ? -1 : 1);
    if (step === 'end' && !e.shiftKey) submit();
  }

  // The dialog scrolls, so its error sits above the first card; the page keeps it at the bottom.
  const errorBox = (
    <div className="rounded-md border border-destructive/30 bg-destructive/10 p-3 text-sm text-destructive">
      {submitError}
    </div>
  );

  return (
    <div className="space-y-4">
      {showHeader && (
        <div className="flex items-center gap-2">
          <Button variant="ghost" size="icon" asChild aria-label="Quay lại">
            <Link to="/products"><AppIcon name="back" /></Link>
          </Button>
          <h1 className="text-2xl font-bold">{isEdit ? 'Chỉnh sửa hàng hóa' : 'Thêm hàng hóa'}</h1>
        </div>
      )}

      <form
        onSubmit={guardedSubmit}
        onKeyDownCapture={enterNavigation ? handleEnterKeys : undefined}
        className="space-y-4"
      >
        {hasSubmitError && !showHeader && errorBox}

        <Card>
          <CardHeader><CardTitle>Thông tin chung</CardTitle></CardHeader>
          <CardContent className="grid gap-4 md:grid-cols-2">
            <Field label="Mã hàng hóa" hint="Để trống để tự sinh" name="code" form={form} />
            <Field label="Tên hàng hóa *" name="name" form={form} />

            <LookupField
              label="Nhóm hàng hóa *"
              name="productGroupId"
              placeholder="Chọn nhóm"
              options={groups}
              form={form}
            />

            <div className="space-y-2">
              <Label htmlFor="unitId">Đơn vị tính *</Label>
              <Controller
                control={form.control}
                name="unitId"
                render={({ field, fieldState }) => (
                  <UnitCombobox
                    units={units}
                    value={field.value}
                    onChange={field.onChange}
                    error={fieldState.error}
                    disabled={locked}
                  />
                )}
              />
              {locked && <LockedHint />}
            </div>

            <Field label="Quy cách / mô tả" name="specification" form={form} className="md:col-span-2" />

            <div className="space-y-2">
              <Label htmlFor="pricingMode">Loại giá *</Label>
              <Controller
                control={form.control}
                name="pricingMode"
                render={({ field }) => (
                  <Select value={field.value || 'PerUnit'} onValueChange={field.onChange} disabled={locked}>
                    <SelectTrigger id="pricingMode" aria-label="Loại giá">
                      <SelectValue placeholder="Chọn loại giá" />
                    </SelectTrigger>
                    <SelectContent>
                      {pricingModes.map((m) => (
                        <SelectItem key={m.value} value={m.value}>{m.label}</SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                )}
              />
              {locked && <LockedHint />}
            </div>

            {isEdit && (
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

        <Card>
          <CardHeader><CardTitle>Kích thước & quy cách</CardTitle></CardHeader>
          <CardContent className="grid gap-4 md:grid-cols-4">
            <Field label="Dài" name="length" type="number" step="any" form={form} />
            <Field label="Rộng" name="width" type="number" step="any" form={form} />
            <Field label="Dày" name="thickness" type="number" step="any" form={form} />
            <Field label="Khối lượng riêng" name="density" type="number" step="any" form={form} />
          </CardContent>
        </Card>

        <Card>
          <CardHeader><CardTitle>Giá & thuế</CardTitle></CardHeader>
          <CardContent className="grid gap-4 md:grid-cols-3">
            <Field label="Giá bán mặc định" name="defaultPrice" type="number" step="any" form={form} />
            <Field label="Giá vốn" name="costPrice" type="number" step="any" form={form} />
            <Field label="Thuế suất (%)" name="defaultTaxRate" type="number" step="any" form={form} />
          </CardContent>
        </Card>

        <Card>
          <CardHeader><CardTitle>Kho & chiết khấu</CardTitle></CardHeader>
          <CardContent className="grid gap-4 md:grid-cols-2">
            <CheckboxField label="Theo dõi tồn kho" name="trackInventory" disabled={locked} form={form} />
            <CheckboxField label="Giá bán đã gồm VAT" name="priceIncludesVat" disabled={locked} form={form} />
            <Field label="% CK mua" name="purchaseDiscountRate" type="number" step="any" form={form} />
            <Field label="% CK bán" name="salesDiscountRate" type="number" step="any" form={form} />
          </CardContent>
        </Card>

        <Card>
          <CardHeader><CardTitle>Ghi chú</CardTitle></CardHeader>
          <CardContent>
            <Field label="Ghi chú" name="note" form={form} multiline />
          </CardContent>
        </Card>

        {hasSubmitError && showHeader && errorBox}

        <div className="flex justify-end gap-2">
          <Button type="button" variant="outline" onClick={onCancel}>Hủy</Button>
          <Button type="submit" disabled={submitting}>
            {submitting ? 'Đang lưu...' : (submitLabel ?? (isEdit ? 'Cập nhật' : 'Tạo mới'))}
          </Button>
        </div>
      </form>
    </div>
  );
}

function toFormDefaults(p?: Product, initialCode?: string, initialName?: string): ProductFormValues {
  return {
    code: p?.code ?? initialCode ?? '',
    name: p?.name ?? initialName ?? '',
    productGroupId: p?.productGroupId ?? '',
    unitId: p?.unitId ?? '',
    specification: p?.specification ?? '',
    length: p?.length ?? '',
    width: p?.width ?? '',
    thickness: p?.thickness ?? '',
    density: p?.density ?? '',
    defaultPrice: p?.defaultPrice ?? '',
    costPrice: p?.costPrice ?? '',
    defaultTaxRate: p?.defaultTaxRate ?? '',
    note: p?.note ?? '',
    status: p?.status ?? 'Active',
    pricingMode: p?.pricingMode ?? 'PerUnit',
    trackInventory: p?.trackInventory ?? true,
    priceIncludesVat: p?.priceIncludesVat ?? false,
    purchaseDiscountRate: p?.purchaseDiscountRate ?? '',
    salesDiscountRate: p?.salesDiscountRate ?? '',
  };
}

type TextFieldName = 'code' | 'name' | 'specification' | 'note';
type NumberFieldName =
  | 'length'
  | 'width'
  | 'thickness'
  | 'density'
  | 'defaultPrice'
  | 'costPrice'
  | 'defaultTaxRate'
  | 'purchaseDiscountRate'
  | 'salesDiscountRate';
type CheckboxFieldName = 'trackInventory' | 'priceIncludesVat';
type LookupFieldName = 'productGroupId';

interface FieldProps {
  label: string;
  name: TextFieldName | NumberFieldName;
  type?: string;
  step?: string;
  hint?: string;
  className?: string;
  multiline?: boolean;
  form: UseFormReturn<ProductFormValues, unknown, ProductFormParsed>;
}

function Field({ label, name, type = 'text', step, hint, className, multiline, form }: FieldProps) {
  const error = form.formState.errors[name];
  return (
    <div className={cn('space-y-2', className)}>
      <Label htmlFor={name}>{label}</Label>
      {multiline ? (
        <Textarea id={name} rows={3} {...form.register(name)} />
      ) : (
        <Input id={name} type={type} step={step} {...form.register(name)} />
      )}
      {hint && !error && <p className="text-xs text-muted-foreground">{hint}</p>}
      {error && <p className="text-sm text-destructive">{String(error.message)}</p>}
    </div>
  );
}

function LockedHint() {
  return <p className="text-xs text-muted-foreground">Hàng đã phát sinh kho — không đổi được</p>;
}

interface CheckboxFieldProps {
  label: string;
  name: CheckboxFieldName;
  disabled?: boolean;
  form: UseFormReturn<ProductFormValues, unknown, ProductFormParsed>;
}

function CheckboxField({ label, name, disabled, form }: CheckboxFieldProps) {
  return (
    <div className="space-y-1">
      <div className="flex items-center gap-2">
        <input id={name} type="checkbox" className="h-4 w-4" disabled={disabled} {...form.register(name)} />
        <Label htmlFor={name}>{label}</Label>
      </div>
      {disabled && <LockedHint />}
    </div>
  );
}

interface LookupFieldProps {
  label: string;
  name: LookupFieldName;
  placeholder: string;
  options: { id: string; name: string }[];
  form: UseFormReturn<ProductFormValues, unknown, ProductFormParsed>;
}

function LookupField({ label, name, placeholder, options, form }: LookupFieldProps) {
  const error = form.formState.errors[name];
  return (
    <div className="space-y-2">
      <Label htmlFor={name}>{label}</Label>
      <Controller
        control={form.control}
        name={name}
        render={({ field }) => (
          <Select value={field.value || undefined} onValueChange={field.onChange}>
            <SelectTrigger id={name} aria-label={label}>
              <SelectValue placeholder={placeholder} />
            </SelectTrigger>
            <SelectContent>
              {options.map((o) => (
                <SelectItem key={o.id} value={o.id}>{o.name}</SelectItem>
              ))}
            </SelectContent>
          </Select>
        )}
      />
      {error && <p className="text-sm text-destructive">{String(error.message)}</p>}
    </div>
  );
}

interface UnitComboboxProps {
  units: LookupItem[];
  value: string;
  onChange: (value: string) => void;
  error?: { message?: string };
  disabled?: boolean;
}

function UnitCombobox({ units, value, onChange, error, disabled }: UnitComboboxProps) {
  const nameOf = (val: string) => units.find((u) => u.id === val)?.name ?? val;

  const [input, setInput] = useState(() => nameOf(value));
  const [open, setOpen] = useState(false);

  useEffect(() => {
    setInput(nameOf(value));
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [value, units]);

  const filtered = input.trim()
    ? units.filter((u) => u.name.toLowerCase().includes(input.toLowerCase()))
    : units;

  const handleInputChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const val = e.target.value;
    setInput(val);
    const match = units.find((u) => u.name.toLowerCase() === val.toLowerCase());
    onChange(match ? match.id : val);
    setOpen(true);
  };

  const handleSelect = (unit: LookupItem) => {
    setInput(unit.name);
    onChange(unit.id);
    setOpen(false);
  };

  return (
    <div className="relative">
      <Input
        id="unitId"
        value={input}
        onChange={handleInputChange}
        onFocus={() => setOpen(true)}
        onBlur={() => setTimeout(() => setOpen(false), 150)}
        placeholder="Nhập hoặc chọn đơn vị tính"
        autoComplete="off"
        disabled={disabled}
      />
      {open && (
        <div className="absolute z-50 w-full rounded-md border bg-popover shadow-md mt-1 max-h-56 overflow-auto">
          {filtered.length > 0 ? (
            filtered.map((u) => (
              <button
                key={u.id}
                type="button"
                className="w-full text-left px-3 py-2 text-sm hover:bg-accent"
                onMouseDown={() => handleSelect(u)}
              >
                {u.name}
              </button>
            ))
          ) : input.trim() ? (
            <div className="px-3 py-2 text-sm text-muted-foreground">
              Sẽ thêm mới: <strong>{input.trim()}</strong>
            </div>
          ) : null}
        </div>
      )}
      {error && <p className="text-sm text-destructive">{String(error.message)}</p>}
    </div>
  );
}
