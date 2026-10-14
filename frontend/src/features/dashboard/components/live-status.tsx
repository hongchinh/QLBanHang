import { format } from 'date-fns';
import type { RealtimeStatus } from '@/lib/realtime-bus';
import { cn } from '@/lib/utils';

interface LiveStatusProps {
  status: RealtimeStatus;
  // Time of the newest figures on screen (epoch milliseconds); null before the first load.
  updatedAt: number | null;
}

const LOOK: Record<RealtimeStatus, { label: string; text: string; dot: string }> = {
  connected: { label: 'Trực tiếp', text: 'text-emerald-700', dot: 'bg-emerald-500' },
  connecting: { label: 'Đang kết nối…', text: 'text-muted-foreground', dot: 'bg-slate-400' },
  disconnected: { label: 'Mất kết nối', text: 'text-amber-700', dot: 'bg-amber-500' },
};

function timeText(status: RealtimeStatus, updatedAt: number | null): string | null {
  if (updatedAt === null) return null;
  if (status === 'connected') return `cập nhật ${format(updatedAt, 'HH:mm:ss')}`;
  // Without the connection the figures only follow the one-minute refetch: the minute is enough.
  if (status === 'disconnected') return `số liệu lúc ${format(updatedAt, 'HH:mm')}`;
  return null;
}

// "Trực tiếp · cập nhật 14:32:05", "Đang kết nối…" or "Mất kết nối · số liệu lúc 14:32".
export function LiveStatus({ status, updatedAt }: LiveStatusProps) {
  const look = LOOK[status];
  const time = timeText(status, updatedAt);
  return (
    <span role="status" className={cn('inline-flex items-center gap-1.5 text-xs', look.text)}>
      <span aria-hidden="true" className={cn('h-2 w-2 shrink-0 rounded-full', look.dot)} />
      <span>
        {look.label}
        {/* The time changes with every refresh: only a change of the connection is announced. */}
        {time && <span aria-live="off"> · {time}</span>}
      </span>
    </span>
  );
}
