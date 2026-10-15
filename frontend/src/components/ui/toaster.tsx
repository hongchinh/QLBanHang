import * as ToastPrimitive from '@radix-ui/react-toast';
import { AppIcon } from '@/components/icons';
import { cn } from '@/lib/utils';
import { dismissToast, useToasts, type ToastVariant } from '@/lib/use-toast';

const variantClasses: Record<ToastVariant, string> = {
  default: 'border bg-background text-foreground',
  success: 'border-emerald-500/30 bg-emerald-50 text-emerald-900 dark:bg-emerald-950 dark:text-emerald-100',
  destructive: 'border-destructive/30 bg-red-50 text-destructive dark:border-red-900 dark:bg-red-950 dark:text-red-200',
};

export function Toaster() {
  const items = useToasts();

  return (
    <ToastPrimitive.Provider swipeDirection="right">
      {items.map((t) => (
        <ToastPrimitive.Root
          key={t.id}
          duration={t.durationMs}
          onOpenChange={(open) => {
            if (!open) dismissToast(t.id);
          }}
          className={cn(
            'group pointer-events-auto relative flex w-full items-start gap-3 overflow-hidden rounded-md p-4 pr-8 shadow-lg',
            'data-[state=open]:animate-in data-[state=closed]:animate-out data-[swipe=end]:animate-out',
            'data-[state=closed]:fade-out-80 data-[state=open]:slide-in-from-bottom-full',
            variantClasses[t.variant ?? 'default'],
          )}
        >
          <div className="flex-1 space-y-1">
            {t.title && <ToastPrimitive.Title className="text-sm font-semibold">{t.title}</ToastPrimitive.Title>}
            {t.description && (
              <ToastPrimitive.Description className="text-sm opacity-90">
                {t.description}
              </ToastPrimitive.Description>
            )}
            {t.action && (
              <ToastPrimitive.Action
                altText={t.action.label}
                onClick={t.action.onClick}
                className="text-sm font-medium underline underline-offset-2"
              >
                {t.action.label}
              </ToastPrimitive.Action>
            )}
          </div>
          <ToastPrimitive.Close
            aria-label="Đóng"
            className="absolute right-2 top-2 rounded-md p-1 opacity-60 transition-opacity hover:opacity-100"
          >
            <AppIcon name="close" />
          </ToastPrimitive.Close>
        </ToastPrimitive.Root>
      ))}
      <ToastPrimitive.Viewport className="pointer-events-none fixed bottom-0 left-1/2 z-[100] flex max-h-screen w-full max-w-[420px] -translate-x-1/2 flex-col-reverse gap-2 p-4 pb-[max(1rem,env(safe-area-inset-bottom))]" />
    </ToastPrimitive.Provider>
  );
}
