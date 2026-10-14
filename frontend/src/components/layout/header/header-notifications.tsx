import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { AppIcon } from '@/components/icons';
import { formatDistanceToNow } from 'date-fns';
import { vi } from 'date-fns/locale';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { cn } from '@/lib/utils';
import { useMarkAllRead, useMarkRead, useNotifications, useUnreadCount } from '@/features/notifications/hooks';
import type { NotificationItem } from '@/features/notifications/api';

function formatBadgeCount(n: number): string {
  if (n > 9) return '9+';
  return String(n);
}

export function HeaderNotifications() {
  const [open, setOpen] = useState(false);
  const navigate = useNavigate();
  const unreadCountQuery = useUnreadCount();
  const listQuery = useNotifications(false, open);
  const markRead = useMarkRead();
  const markAllRead = useMarkAllRead();

  const unreadCount = unreadCountQuery.data ?? 0;
  const items = listQuery.data ?? [];

  const handleSelect = async (n: NotificationItem) => {
    if (!n.isRead) {
      try {
        await markRead.mutateAsync(n.id);
      } catch {
        // swallow — UI already navigates
      }
    }
    if (n.link) {
      navigate(n.link);
      setOpen(false);
    }
  };

  const handleMarkAll = async () => {
    if (unreadCount === 0) return;
    try {
      await markAllRead.mutateAsync();
    } catch {
      // swallow
    }
  };

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <button
          type="button"
          aria-label={`Thông báo, ${unreadCount} chưa đọc`}
          className="relative flex h-11 w-11 items-center justify-center rounded-md text-header-fg hover:bg-header-active"
        >
          <AppIcon name="notifications" size={20} />
          {unreadCount > 0 && (
            <span
              aria-hidden
              className="absolute right-1 top-1 flex h-[18px] min-w-[18px] items-center justify-center rounded-full bg-header-danger px-1 text-[10px] font-bold text-white"
            >
              {formatBadgeCount(unreadCount)}
            </span>
          )}
        </button>
      </PopoverTrigger>
      <PopoverContent
        align="end"
        sideOffset={4}
        className="w-[min(360px,calc(100vw-2rem))] p-0"
      >
        <div className="flex items-center justify-between border-b px-3 py-2">
          <span className="text-sm font-semibold">Thông báo</span>
          <button
            type="button"
            onClick={handleMarkAll}
            disabled={unreadCount === 0 || markAllRead.isPending}
            className="text-xs text-muted-foreground hover:text-foreground disabled:cursor-not-allowed disabled:opacity-50"
          >
            Đánh dấu tất cả đã đọc
          </button>
        </div>
        <div className="max-h-[60vh] overflow-y-auto">
          {listQuery.isLoading ? (
            <div className="flex items-center gap-2 px-3 py-4 text-sm text-muted-foreground">
              <AppIcon name="loading" />
              Đang tải…
            </div>
          ) : items.length === 0 ? (
            <div className="px-3 py-4 text-sm text-muted-foreground">Chưa có thông báo.</div>
          ) : (
            items.map((n) => (
              <button
                type="button"
                key={n.id}
                onClick={() => handleSelect(n)}
                className={cn(
                  'flex w-full items-start gap-2 border-b border-l-[3px] px-3 py-2.5 text-left text-sm last:border-b-0 transition-colors hover:bg-accent/60',
                  n.isRead
                    ? 'border-l-transparent bg-background'
                    : 'border-l-blue-500 bg-blue-50',
                )}
              >
                <span
                  className={cn(
                    'mt-[5px] h-2 w-2 shrink-0 rounded-full',
                    n.isRead ? 'bg-transparent' : 'bg-blue-500',
                  )}
                />
                <div className="flex min-w-0 flex-col gap-0.5">
                  <span
                    className={cn(
                      'leading-snug',
                      n.isRead ? 'font-normal text-muted-foreground' : 'font-semibold text-foreground',
                    )}
                  >
                    {n.title}
                  </span>
                  {n.body && (
                    <span className="line-clamp-2 text-xs text-muted-foreground">{n.body}</span>
                  )}
                  <span className="text-[10px] text-muted-foreground">
                    {formatDistanceToNow(new Date(n.createdAt), { addSuffix: true, locale: vi })}
                  </span>
                </div>
              </button>
            ))
          )}
        </div>
      </PopoverContent>
    </Popover>
  );
}
