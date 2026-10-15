import { useState } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { toast } from '@/lib/use-toast';
import { getErrorMessage } from '@/lib/api-client';
import { useQuotationSystemSettings, useUpdateQuotationSystemSettings } from '@/features/quotation-settings/hooks';
import type { RevenueReportingDateField } from '@/features/quotation-settings/types';
import { BackLink } from '@/components/layout/back-link';

const DATE_FIELD_OPTIONS: { value: RevenueReportingDateField; label: string }[] = [
  { value: 'QuotationDate', label: 'Theo ngày báo giá (mặc định)' },
  { value: 'ConfirmedAt', label: 'Theo ngày khách xác nhận' },
  { value: 'AccountingConfirmedAt', label: 'Theo ngày kế toán xác nhận nhận tiền' },
];

export function QuotationSystemSettingsPage() {
  const { data: settings, isLoading } = useQuotationSystemSettings();
  const update = useUpdateQuotationSystemSettings();
  const [selected, setSelected] = useState<RevenueReportingDateField | undefined>(undefined);

  const currentValue = selected ?? settings?.revenueReportingDateField ?? 'QuotationDate';

  async function handleSave() {
    try {
      await update.mutateAsync({ revenueReportingDateField: currentValue });
      setSelected(undefined);
      toast({ variant: 'success', title: 'Đã lưu cấu hình' });
    } catch (err) {
      toast({ variant: 'destructive', title: 'Lưu thất bại', description: getErrorMessage(err) });
    }
  }

  if (isLoading) {
    return <div className="text-sm text-muted-foreground">Đang tải...</div>;
  }

  return (
    <div className="space-y-6">
      <div className="flex items-start gap-2">
        <BackLink to="/settings" label="Cấu hình" />
        <div>
          <h1 className="text-2xl font-bold">Cấu hình hệ thống báo giá</h1>
          <p className="text-sm text-muted-foreground">
            Thiết lập trường ngày dùng trong báo cáo doanh thu và dashboard.
          </p>
        </div>
      </div>

      <Card className="max-w-lg">
        <CardHeader>
          <CardTitle>Trường ngày doanh thu</CardTitle>
          <CardDescription>
            Chọn trường ngày được dùng để lọc và tổng hợp doanh thu trên dashboard. Thay đổi có hiệu lực ngay lập tức.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="dateField">Trường ngày</Label>
            <Select value={currentValue} onValueChange={(v) => setSelected(v as RevenueReportingDateField)}>
              <SelectTrigger id="dateField">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {DATE_FIELD_OPTIONS.map((opt) => (
                  <SelectItem key={opt.value} value={opt.value}>
                    {opt.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          {settings?.updatedAt && (
            <p className="text-xs text-muted-foreground">
              Cập nhật lần cuối: {new Date(settings.updatedAt).toLocaleString('vi-VN')}
              {settings.updatedByName && ` bởi ${settings.updatedByName}`}
            </p>
          )}

          <Button onClick={() => void handleSave()} disabled={update.isPending}>
            {update.isPending ? 'Đang lưu...' : 'Lưu'}
          </Button>
        </CardContent>
      </Card>
    </div>
  );
}
