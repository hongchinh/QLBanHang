import { useState } from 'react';
import { useInventorySettings, useUpdateInventorySettings } from '@/features/inventory-settings/hooks';
import type {
  InventorySettings,
  UpdateInventorySettingsRequest,
} from '@/features/inventory-settings/types';
import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { formatApiErrorDetails, getErrorMessage } from '@/lib/api-client';
import { toast } from '@/lib/use-toast';

const COSTING_METHODS = [
  { value: 'PeriodicAverage', label: 'Bình quân cuối kỳ' },
  { value: 'Fifo', label: 'FIFO (chưa hỗ trợ)', disabled: true },
] as const;

const COSTING_PERIODS = [
  { value: 'Month', label: 'Tháng' },
  { value: 'Quarter', label: 'Quý' },
  { value: 'Year', label: 'Năm' },
] as const;

const COSTING_SCOPES = [
  { value: 'Branch', label: 'Theo chi nhánh' },
  { value: 'Warehouse', label: 'Theo kho' },
] as const;

const NEGATIVE_STOCK_POLICIES = [
  { value: 'Allow', label: 'Cho phép' },
  { value: 'Warn', label: 'Cảnh báo' },
  { value: 'Block', label: 'Chặn' },
] as const;

const DEFAULT_DATE_MODES = [
  { value: 'Now', label: 'Hôm nay' },
  { value: 'PreviousVoucher', label: 'Theo phiếu trước' },
] as const;

export function InventorySettingsPage() {
  const { data, isLoading, isError, error } = useInventorySettings();

  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-2xl font-bold">Cấu hình kho</h1>
        <p className="text-sm text-muted-foreground">Phương pháp tính giá vốn và quy tắc nhập xuất kho</p>
      </div>

      {isError && (
        <div className="rounded-md border border-destructive/30 bg-destructive/10 p-3 text-sm text-destructive">
          {getErrorMessage(error)}
        </div>
      )}
      {isLoading && <div className="text-sm text-muted-foreground">Đang tải...</div>}
      {data && <InventorySettingsForm initial={data} />}
    </div>
  );
}

function toRequest(s: InventorySettings): UpdateInventorySettingsRequest {
  return {
    costingMethod: s.costingMethod,
    costingPeriod: s.costingPeriod,
    costingScope: s.costingScope,
    purchaseCostIncludesVat: s.purchaseCostIncludesVat,
    negativeStockPolicy: s.negativeStockPolicy,
    netExcludesVat: s.netExcludesVat,
    defaultDateMode: s.defaultDateMode,
  };
}

function InventorySettingsForm({ initial }: { initial: InventorySettings }) {
  const update = useUpdateInventorySettings();
  const [values, setValues] = useState<UpdateInventorySettingsRequest>(() => toRequest(initial));
  const [confirmOpen, setConfirmOpen] = useState(false);

  const set = <K extends keyof UpdateInventorySettingsRequest>(key: K, value: UpdateInventorySettingsRequest[K]) =>
    setValues((v) => ({ ...v, [key]: value }));

  // These three settings trigger a cost recalculation on the server (D11 / D33).
  const costingChanged =
    values.costingPeriod !== initial.costingPeriod ||
    values.costingScope !== initial.costingScope ||
    values.purchaseCostIncludesVat !== initial.purchaseCostIncludesVat;

  const save = async () => {
    try {
      await update.mutateAsync(values);
      toast({ variant: 'success', title: 'Đã lưu cấu hình kho' });
      setConfirmOpen(false);
    } catch (err) {
      toast({ variant: 'destructive', title: 'Không thể lưu', description: formatApiErrorDetails(err) });
    }
  };

  const onSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (costingChanged) setConfirmOpen(true);
    else void save();
  };

  return (
    <>
      <form onSubmit={onSubmit} className="space-y-4">
        <Card>
          <CardHeader>
            <CardTitle>Giá vốn</CardTitle>
          </CardHeader>
          <CardContent className="grid gap-4 md:grid-cols-3">
            <SelectField
              id="costingMethod"
              label="Phương pháp tính giá"
              value={values.costingMethod}
              options={COSTING_METHODS}
              onChange={(v) => set('costingMethod', v)}
            />
            <SelectField
              id="costingPeriod"
              label="Kỳ tính giá"
              value={values.costingPeriod}
              options={COSTING_PERIODS}
              onChange={(v) => set('costingPeriod', v)}
            />
            <SelectField
              id="costingScope"
              label="Phạm vi tính giá"
              value={values.costingScope}
              options={COSTING_SCOPES}
              onChange={(v) => set('costingScope', v)}
            />
            <CheckboxField
              id="purchaseCostIncludesVat"
              label="Giá nhập kho gồm VAT đầu vào"
              checked={values.purchaseCostIncludesVat}
              onChange={(v) => set('purchaseCostIncludesVat', v)}
            />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Phiếu nhập xuất</CardTitle>
          </CardHeader>
          <CardContent className="grid gap-4 md:grid-cols-3">
            <SelectField
              id="negativeStockPolicy"
              label="Chính sách xuất âm"
              value={values.negativeStockPolicy}
              options={NEGATIVE_STOCK_POLICIES}
              onChange={(v) => set('negativeStockPolicy', v)}
            />
            <SelectField
              id="defaultDateMode"
              label="Ngày mặc định phiếu mới"
              value={values.defaultDateMode}
              options={DEFAULT_DATE_MODES}
              onChange={(v) => set('defaultDateMode', v)}
            />
            <CheckboxField
              id="netExcludesVat"
              label="Số còn lại không cộng VAT"
              checked={values.netExcludesVat}
              onChange={(v) => set('netExcludesVat', v)}
            />
          </CardContent>
        </Card>

        <div className="flex justify-end">
          <Button type="submit" disabled={update.isPending}>
            {update.isPending ? 'Đang lưu...' : 'Lưu'}
          </Button>
        </div>
      </form>

      <ConfirmDialog
        open={confirmOpen}
        onOpenChange={setConfirmOpen}
        title="Tính lại giá vốn?"
        description="Thay đổi này sẽ tính lại giá vốn từ kỳ chưa khóa sổ. Tiếp tục?"
        confirmLabel="Tiếp tục"
        loading={update.isPending}
        onConfirm={() => void save()}
      />
    </>
  );
}

interface SelectFieldProps<T extends string> {
  id: string;
  label: string;
  value: T;
  options: readonly { value: T; label: string; disabled?: boolean }[];
  onChange: (value: T) => void;
}

function SelectField<T extends string>({ id, label, value, options, onChange }: SelectFieldProps<T>) {
  return (
    <div className="space-y-2">
      <Label htmlFor={id}>{label}</Label>
      <Select value={value} onValueChange={(v) => onChange(v as T)}>
        <SelectTrigger id={id} aria-label={label}>
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          {options.map((o) => (
            <SelectItem key={o.value} value={o.value} disabled={o.disabled}>
              {o.label}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
    </div>
  );
}

interface CheckboxFieldProps {
  id: string;
  label: string;
  checked: boolean;
  onChange: (checked: boolean) => void;
}

function CheckboxField({ id, label, checked, onChange }: CheckboxFieldProps) {
  return (
    <div className="flex items-center gap-2 md:col-span-3">
      <input
        id={id}
        type="checkbox"
        className="h-4 w-4"
        checked={checked}
        onChange={(e) => onChange(e.target.checked)}
      />
      <Label htmlFor={id}>{label}</Label>
    </div>
  );
}
