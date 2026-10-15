import * as React from 'react';
import * as DialogPrimitive from '@radix-ui/react-dialog';
import { AppIcon } from '@/components/icons';
import { canTakeFocus, currentFocus, giveFocusBack, isFocusLost } from '@/lib/focus';
import { cn } from '@/lib/utils';

const Dialog = DialogPrimitive.Root;
const DialogTrigger = DialogPrimitive.Trigger;
const DialogPortal = DialogPrimitive.Portal;
const DialogClose = DialogPrimitive.Close;

const DialogOverlay = React.forwardRef<
  React.ElementRef<typeof DialogPrimitive.Overlay>,
  React.ComponentPropsWithoutRef<typeof DialogPrimitive.Overlay>
>(({ className, ...props }, ref) => (
  <DialogPrimitive.Overlay
    ref={ref}
    className={cn(
      'fixed inset-0 z-50 bg-black/50 data-[state=open]:animate-in data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0',
      className,
    )}
    {...props}
  />
));
DialogOverlay.displayName = DialogPrimitive.Overlay.displayName;

/**
 * Mounts with the content of an open dialog. Its first render comes before that content is committed, so before a
 * child with autoFocus takes the focus: what has the focus then is what the dialog was opened from. Radix reports the
 * opening (onOpenAutoFocus) only when no child has taken the focus already.
 */
function OpenerRecorder({ openerRef }: { openerRef: React.MutableRefObject<HTMLElement | null> }) {
  const [opener] = React.useState(currentFocus);
  React.useLayoutEffect(() => {
    openerRef.current = opener;
  }, [opener, openerRef]);
  return null;
}

const DialogContent = React.forwardRef<
  React.ElementRef<typeof DialogPrimitive.Content>,
  React.ComponentPropsWithoutRef<typeof DialogPrimitive.Content> & { showClose?: boolean }
>(({ className, children, showClose = true, onCloseAutoFocus, ...props }, ref) => {
  // A dialog opened from state has no DialogTrigger, so Radix would drop the focus on <body> when it closes.
  // Remember what had the focus when it opened and give it back, unless it is gone or disabled.
  const openerRef = React.useRef<HTMLElement | null>(null);
  return (
  <DialogPortal>
    <DialogOverlay />
    <DialogPrimitive.Content
      ref={ref}
      onCloseAutoFocus={(event) => {
        // Radix asks once, after the content has gone. Released here: a dialog that stays rendered (a confirm dialog
        // of a page) would otherwise hold on to an opener the page may have removed until it opens again.
        const opener = openerRef.current;
        openerRef.current = null;
        onCloseAutoFocus?.(event);
        if (event.defaultPrevented) return;
        if (!canTakeFocus(opener)) return;
        event.preventDefault();
        // Radix fires this a tick after the unmount: never take the focus from where the user went meanwhile. A key
        // still held down (the Enter that opened the dialog) must not press the opener again.
        if (isFocusLost()) giveFocusBack(opener);
      }}
      className={cn(
        'fixed left-[50%] top-[50%] z-50 grid w-full max-w-lg translate-x-[-50%] translate-y-[-50%] gap-4 border bg-background p-6 shadow-lg rounded-lg',
        'data-[state=open]:animate-in data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0',
        'data-[state=closed]:zoom-out-95 data-[state=open]:zoom-in-95',
        className,
      )}
      {...props}
    >
      <OpenerRecorder openerRef={openerRef} />
      {children}
      {showClose && (
        <DialogPrimitive.Close className="absolute right-4 top-4 rounded-sm opacity-70 transition-opacity hover:opacity-100 focus:outline-none focus:ring-2 focus:ring-ring">
          <AppIcon name="close" />
          <span className="sr-only">Đóng</span>
        </DialogPrimitive.Close>
      )}
    </DialogPrimitive.Content>
  </DialogPortal>
  );
});
DialogContent.displayName = DialogPrimitive.Content.displayName;

const DialogHeader = ({ className, ...props }: React.HTMLAttributes<HTMLDivElement>) => (
  <div className={cn('flex flex-col space-y-1.5 text-left', className)} {...props} />
);
DialogHeader.displayName = 'DialogHeader';

const DialogFooter = ({ className, ...props }: React.HTMLAttributes<HTMLDivElement>) => (
  <div className={cn('flex flex-col-reverse gap-2 sm:flex-row sm:justify-end', className)} {...props} />
);
DialogFooter.displayName = 'DialogFooter';

const DialogTitle = React.forwardRef<
  React.ElementRef<typeof DialogPrimitive.Title>,
  React.ComponentPropsWithoutRef<typeof DialogPrimitive.Title>
>(({ className, ...props }, ref) => (
  <DialogPrimitive.Title
    ref={ref}
    className={cn('text-lg font-semibold leading-none', className)}
    {...props}
  />
));
DialogTitle.displayName = DialogPrimitive.Title.displayName;

const DialogDescription = React.forwardRef<
  React.ElementRef<typeof DialogPrimitive.Description>,
  React.ComponentPropsWithoutRef<typeof DialogPrimitive.Description>
>(({ className, ...props }, ref) => (
  <DialogPrimitive.Description
    ref={ref}
    className={cn('text-sm text-muted-foreground', className)}
    {...props}
  />
));
DialogDescription.displayName = DialogPrimitive.Description.displayName;

export {
  Dialog,
  DialogTrigger,
  DialogContent,
  DialogHeader,
  DialogFooter,
  DialogTitle,
  DialogDescription,
  DialogClose,
};
