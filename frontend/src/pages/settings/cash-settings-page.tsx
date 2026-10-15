import { useState } from 'react';
import { LIMIT_POLICY_LABELS } from '@/features/cash-common/labels';
import type { LimitPolicy } from '@/features/cash-common/types';
import { useCashSettings, useUpdateCashSettings } from '@/features/cash-settings/hooks';
import type { CashSettings, UpdateCashSettingsRequest } from '@/features/cash-settings/types';
import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { formatApiErrorDetails, getErrorMessage } from '@/lib/api-client';
import { toast } from '@/lib/use-toast';
import { BackLink } from '@/components/layout/back-link';

const LIMIT_POLICIES: LimitPolicy[] = ['Allow', 'Warn', 'Block'];

// Route permission: cash.settings.
export function CashSettingsPage() {
  const { data, isLoading, isError, error } = useCashSettings();

  return (
    <div className="space-y-4">
      <div className="flex items-start gap-2">
        <BackLink to="/settings" label="Cấu hình" />
        <div>
          <h1 className="text-2xl font-bold">Cấu hình tiền & công nợ</h1>
          <p className="text-sm text-muted-foreground">Chính sách khi vượt hạn mức nợ và khi chi âm quỹ</p>
        </div>
      </div>

      {isError && (
        <div className="rounded-md border border-destructive/30 bg-destructive/10 p-3 text-sm text-destructive">
          {getErrorMessage(error)}
        </div>
      )}
      {isLoading && <div className="text-sm text-muted-foreground">Đang tải...</div>}
      {data && <CashSettingsForm initial={data} />}
    </div>
  );
}

function CashSettingsForm({ initial }: { initial: CashSettings }) {
  const update = useUpdateCashSettings();
  const [values, setValues] = useState<UpdateCashSettingsRequest>({
    creditLimitPolicy: initial.creditLimitPolicy,
    negativeFundPolicy: initial.negativeFundPolicy,
  });

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      await update.mutateAsync(values);
      toast({ variant: 'success', title: 'Đã lưu cấu hình tiền & công nợ' });
    } catch (err) {
      toast({ variant: 'destructive', title: 'Không thể lưu', description: formatApiErrorDetails(err) });
    }
  };

  return (
    <form onSubmit={onSubmit} className="space-y-4">
      <Card>
        <CardHeader>
          <CardTitle>Tiền & Công nợ</CardTitle>
        </CardHeader>
        <CardContent className="grid gap-4 md:grid-cols-3">
          <PolicyField
            id="creditLimitPolicy"
            label="Vượt hạn mức nợ"
            value={values.creditLimitPolicy}
            onChange={(v) => setValues((s) => ({ ...s, creditLimitPolicy: v }))}
          />
          <PolicyField
            id="negativeFundPolicy"
            label="Chi âm quỹ"
            value={values.negativeFundPolicy}
            onChange={(v) => setValues((s) => ({ ...s, negativeFundPolicy: v }))}
          />
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

interface PolicyFieldProps {
  id: string;
  label: string;
  value: LimitPolicy;
  onChange: (value: LimitPolicy) => void;
}

function PolicyField({ id, label, value, onChange }: PolicyFieldProps) {
  return (
    <div className="space-y-2">
      <Label htmlFor={id}>{label}</Label>
      <Select value={value} onValueChange={(v) => onChange(v as LimitPolicy)}>
        <SelectTrigger id={id} aria-label={label}>
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          {LIMIT_POLICIES.map((p) => (
            <SelectItem key={p} value={p}>
              {LIMIT_POLICY_LABELS[p]}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
    </div>
  );
}
