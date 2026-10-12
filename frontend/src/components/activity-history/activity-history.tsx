import { Ban, CirclePlus, Link2Off, Loader2, ReceiptText, RotateCcw, Save, Trash2, type LucideIcon } from 'lucide-react';
import { Button } from '@/components/ui/button';
import type { StockVoucherActivityAction } from '@/features/stock-vouchers/types';

// The stock action union is a superset of the cash one, so cash, fund transfer and debt offset activities fit too.
export type DocumentActivityAction = StockVoucherActivityAction;

export interface DocumentActivity {
  id: string;
  action: DocumentActivityAction;
  occurredAt: string;
  description: string;
  actorName?: string | null;
}

interface Props {
  activities: DocumentActivity[];
  isLoading: boolean;
  isError: boolean;
  errorMessage: string;
  onRetry: () => void;
}

// QuotationActivityHistory (pages/quotations/quotation-form-page.tsx) duplicates this layout with its own action union.
export function ActivityHistory({ activities, isLoading, isError, errorMessage, onRetry }: Props) {
  if (isLoading) {
    return (
      <div role="status" className="flex items-center gap-2 py-6 text-sm text-muted-foreground">
        <Loader2 aria-hidden="true" className="h-4 w-4 animate-spin text-blue-600" />
        Đang tải lịch sử...
      </div>
    );
  }

  if (isError) {
    return (
      <div role="alert" className="rounded-md border border-destructive/30 bg-destructive/10 p-3 text-sm text-destructive">
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
          <div className="mt-0.5"><ActivityIcon action={activity.action} /></div>
          <div className="min-w-0 flex-1">
            <div className="flex flex-wrap items-center gap-x-2 gap-y-1">
              <span className="font-medium">{ACTIVITY_LABELS[activity.action] ?? activity.action}</span>
              <time dateTime={activity.occurredAt} className="text-xs text-muted-foreground">
                {formatActivityTime(activity.occurredAt)}
              </time>
            </div>
            <p className="mt-1 text-sm text-foreground">{activity.description}</p>
            <p className="mt-1 text-xs text-muted-foreground">{activity.actorName ?? 'Người dùng không xác định'}</p>
          </div>
        </li>
      ))}
    </ul>
  );
}

const ACTIVITY_LABELS: Record<DocumentActivityAction, string> = {
  Created: 'Tạo phiếu',
  Updated: 'Cập nhật',
  Cancelled: 'Hủy phiếu',
  Restored: 'Khôi phục',
  Deleted: 'Xóa',
  SettlementRemoved: 'Bỏ đối trừ',
  AutoVoucherChanged: 'Phiếu thu/chi tự động',
};

const ACTIVITY_ICONS: Record<DocumentActivityAction, { icon: LucideIcon; className: string }> = {
  Created: { icon: CirclePlus, className: 'text-cyan-600' },
  Updated: { icon: Save, className: 'text-blue-600' },
  Cancelled: { icon: Ban, className: 'text-red-600' },
  Restored: { icon: RotateCcw, className: 'text-emerald-600' },
  Deleted: { icon: Trash2, className: 'text-red-600' },
  SettlementRemoved: { icon: Link2Off, className: 'text-amber-600' },
  AutoVoucherChanged: { icon: ReceiptText, className: 'text-indigo-600' },
};

// An action the map does not know (a newer server) shows no icon.
function ActivityIcon({ action }: { action: DocumentActivityAction }) {
  const entry = ACTIVITY_ICONS[action];
  if (!entry) return null;
  const Icon = entry.icon;
  return <Icon aria-hidden="true" className={`h-4 w-4 ${entry.className}`} />;
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
