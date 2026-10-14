import * as ToastPrimitive from '@radix-ui/react-toast';
import { AppIcon } from '@/components/icons';
import { cn } from '@/lib/utils';
import { dismissToast, useToasts, type ToastVariant } from '@/lib/use-toast';

const variantClasses: Record<ToastVariant, string> = {
  default: 'border bg-background text-foreground',
  success: 'border-emerald-500/30 bg-emerald-500/10 text-emerald-900 dark:text-emerald-100',
  destructive: 'border-destructive/30 bg-destructive/10 text-destructive',
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
            'data-[state=closed]:fade-out-80 data-[state=open]:slide-in-from-top-full',
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
      <ToastPrimitive.Viewport className="fixed bottom-0 right-0 z-[100] flex max-h-screen w-full flex-col-reverse gap-2 p-4 pb-[max(1rem,env(safe-area-inset-bottom))] sm:pb-4 sm:bottom-auto sm:right-4 sm:top-[72px] sm:flex-col sm:max-w-[420px]" />
    </ToastPrimitive.Provider>
  );
}
