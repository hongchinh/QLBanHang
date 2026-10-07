import { Ban, CirclePlus, Loader2, RotateCcw, Save, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import type { StockVoucherActivity, StockVoucherActivityAction } from '@/features/stock-vouchers/types';

interface Props {
  activities: StockVoucherActivity[];
  isLoading: boolean;
  isError: boolean;
  errorMessage: string;
  onRetry: () => void;
}

// Same layout as QuotationActivityHistory.
export function StockVoucherActivityHistory({ activities, isLoading, isError, errorMessage, onRetry }: Props) {
  if (isLoading) {
    return (
      <div className="flex items-center gap-2 py-6 text-sm text-muted-foreground">
        <Loader2 className="h-4 w-4 animate-spin text-blue-600" />
        Đang tải lịch sử...
      </div>
    );
  }

  if (isError) {
    return (
      <div className="rounded-md border border-destructive/30 bg-destructive/10 p-3 text-sm text-destructive">
        <div>{errorMessage}</div>
        <Button type="button" variant="outline" size="sm" className="mt-2" onClick={onRetry}>
          Thử lại
        </Button>
      </div>
    );
  }

  if (activities.length === 0) {
    return (
      <div className="rounded-md border border-dashed p-6 text-center text-sm text-muted-foreground">
        Chưa có lịch sử.
      </div>
    );
  }

  return (
    <ul className="divide-y rounded-md border">
      {activities.map((activity) => (
        <li key={activity.id} className="flex gap-3 p-3">
          <div className="mt-0.5">{activityIcon(activity.action)}</div>
          <div className="min-w-0 flex-1">
            <div className="flex flex-wrap items-center gap-x-2 gap-y-1">
              <span className="font-medium">{ACTIVITY_LABELS[activity.action] ?? activity.action}</span>
              <span className="text-xs text-muted-foreground">{formatActivityTime(activity.occurredAt)}</span>
            </div>
            <p className="mt-1 text-sm text-foreground">{activity.description}</p>
            <p className="mt-1 text-xs text-muted-foreground">{activity.actorName ?? 'Người dùng không xác định'}</p>
          </div>
        </li>
      ))}
    </ul>
  );
}

const ACTIVITY_LABELS: Record<StockVoucherActivityAction, string> = {
  Created: 'Tạo phiếu',
  Updated: 'Cập nhật',
  Cancelled: 'Hủy phiếu',
  Restored: 'Khôi phục',
  Deleted: 'Xóa',
};

function activityIcon(action: StockVoucherActivityAction) {
  switch (action) {
    case 'Created':
      return <CirclePlus className="h-4 w-4 text-cyan-600" />;
    case 'Updated':
      return <Save className="h-4 w-4 text-blue-600" />;
    case 'Cancelled':
      return <Ban className="h-4 w-4 text-red-600" />;
    case 'Restored':
      return <RotateCcw className="h-4 w-4 text-emerald-600" />;
    case 'Deleted':
      return <Trash2 className="h-4 w-4 text-red-600" />;
  }
}

function formatActivityTime(value: string) {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  return date.toLocaleString('vi-VN', {
    hour: '2-digit',
    minute: '2-digit',
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
  });
}
