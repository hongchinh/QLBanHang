import { useEffect, useState } from 'react';
import { Controller, useForm, type Resolver, type UseFormReturn } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { ArrowLeft } from 'lucide-react';
import { useQueryClient } from '@tanstack/react-query';
import {
  useCreateProduct,
  useProduct,
  useProductGroups,
  useUnits,
  useUpdateProduct,
} from '@/features/products/hooks';
import { lookupsApi } from '@/features/products/api';
import { lookupKeys } from '@/features/products/keys';
import {
  productSchema,
  type ProductFormParsed,
  type ProductFormValues,
} from '@/features/products/schema';
import type {
  CreateProductRequest,
  LookupItem,
  Product,
  UpdateProductRequest,
} from '@/features/products/types';
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
import { formatApiErrorDetails, getErrorMessage } from '@/lib/api-client';
import { toast } from '@/lib/use-toast';
import { cn } from '@/lib/utils';

const UUID_RE = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

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

export function ProductFormPage() {
  const { id } = useParams<{ id: string }>();
  const isEdit = !!id && id !== 'new';
  const navigate = useNavigate();
  const qc = useQueryClient();

  const { data: product, isLoading } = useProduct(isEdit ? id : undefined);
  const groups = useProductGroups();
  const units = useUnits();
  const create = useCreateProduct();
  const update = useUpdateProduct();

  // Wait for both the (optional) edited entity and the lookups before mounting
  // the form — react-hook-form's defaultValues are captured once on first render.
  const lookupsReady = !groups.isLoading && !units.isLoading;
  if ((isEdit && isLoading) || !lookupsReady) {
    return <div className="text-sm text-muted-foreground">Đang tải...</div>;
  }

  return (
    <ProductFormInner
      isEdit={isEdit}
      initial={product}
      groups={groups.data ?? []}
      units={units.data ?? []}
      submitting={create.isPending || update.isPending}
      submitError={getErrorMessage(create.error ?? update.error)}
      hasSubmitError={create.isError || update.isError}
      onSubmit={async (parsed) => {
        try {
          // Resolve unitId: if it's not a UUID the user typed a new name → get-or-create
          let unitId = parsed.unitId;
          if (!UUID_RE.test(unitId)) {
            const unit = await lookupsApi.getOrCreateUnit(unitId);
            unitId = String(unit.id);
            qc.invalidateQueries({ queryKey: lookupKeys.units() });
          }
          const resolved = { ...parsed, unitId };

          if (isEdit && id) {
            await update.mutateAsync({ id, data: toUpdatePayload(resolved) });
            toast({ variant: 'success', title: 'Đã cập nhật hàng hóa' });
          } else {
            await create.mutateAsync(toCreatePayload(resolved));
            toast({ variant: 'success', title: 'Đã tạo hàng hóa' });
          }
          navigate('/products');
        } catch (err) {
          toast({ variant: 'destructive', title: 'Không thể lưu', description: formatApiErrorDetails(err) });
        }
      }}
    />
  );
}

interface InnerProps {
  isEdit: boolean;
  initial?: Product;
  groups: { id: string; code: string; name: string }[];
  units: { id: string; code: string; name: string }[];
  submitting: boolean;
  submitError: string;
  hasSubmitError: boolean;
  onSubmit: (parsed: ProductFormParsed) => void;
}

function ProductFormInner({
  isEdit,
  initial,
  groups,
  units,
  submitting,
  submitError,
  hasSubmitError,
  onSubmit,
}: InnerProps) {
  const form = useForm<ProductFormValues, unknown, ProductFormParsed>({
    // zodResolver in @hookform/resolvers@3.9 only types TFieldValues, so its
    // returned resolver looks like Resolver<Input, _, Input> instead of
    // Resolver<Input, _, Output>. The runtime value passed to handleSubmit
    // *is* the parsed (transformed) shape, so this cast just aligns the types.
    resolver: zodResolver(productSchema) as unknown as Resolver<ProductFormValues, unknown, ProductFormParsed>,
    defaultValues: toFormDefaults(initial),
  });
  const locked = !!initial?.hasInventoryActivity;

  return (
    <div className="space-y-4">
      <div className="flex items-center gap-2">
        <Button variant="ghost" size="icon" asChild aria-label="Quay lại">
          <Link to="/products"><ArrowLeft className="h-4 w-4 text-slate-500" /></Link>
        </Button>
        <h1 className="text-2xl font-bold">{isEdit ? 'Chỉnh sửa hàng hóa' : 'Thêm hàng hóa'}</h1>
      </div>

      <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
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

        {hasSubmitError && (
          <div className="rounded-md border border-destructive/30 bg-destructive/10 p-3 text-sm text-destructive">
            {submitError}
          </div>
        )}

        <div className="flex justify-end gap-2">
          <Button type="button" variant="outline" asChild>
            <Link to="/products">Hủy</Link>
          </Button>
          <Button type="submit" disabled={submitting}>
            {submitting ? 'Đang lưu...' : isEdit ? 'Cập nhật' : 'Tạo mới'}
          </Button>
        </div>
      </form>
    </div>
  );
}

function toFormDefaults(p?: Product): ProductFormValues {
  return {
    code: p?.code ?? '',
    name: p?.name ?? '',
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

function toInventoryPayload(parsed: ProductFormParsed) {
  return {
    trackInventory: parsed.trackInventory,
    priceIncludesVat: parsed.priceIncludesVat,
    purchaseDiscountRate: parsed.purchaseDiscountRate ?? 0,
    salesDiscountRate: parsed.salesDiscountRate ?? 0,
  };
}

function toCreatePayload(parsed: ProductFormParsed): CreateProductRequest {
  return {
    code: parsed.code,
    name: parsed.name,
    productGroupId: parsed.productGroupId,
    unitId: parsed.unitId,
    specification: parsed.specification,
    length: parsed.length,
    width: parsed.width,
    thickness: parsed.thickness,
    density: parsed.density,
    defaultPrice: parsed.defaultPrice,
    costPrice: parsed.costPrice,
    defaultTaxRate: parsed.defaultTaxRate,
    note: parsed.note,
    pricingMode: parsed.pricingMode,
    ...toInventoryPayload(parsed),
  };
}

function toUpdatePayload(parsed: ProductFormParsed): UpdateProductRequest {
  return {
    name: parsed.name,
    productGroupId: parsed.productGroupId,
    unitId: parsed.unitId,
    specification: parsed.specification,
    length: parsed.length,
    width: parsed.width,
    thickness: parsed.thickness,
    density: parsed.density,
    defaultPrice: parsed.defaultPrice,
    costPrice: parsed.costPrice,
    defaultTaxRate: parsed.defaultTaxRate,
    note: parsed.note,
    status: parsed.status ?? 'Active',
    pricingMode: parsed.pricingMode,
    ...toInventoryPayload(parsed),
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
