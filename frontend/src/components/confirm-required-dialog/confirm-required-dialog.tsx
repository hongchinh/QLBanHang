import { useEffect, useRef } from 'react';
import { AppIcon } from '@/components/icons';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { giveFocusBack, ignoreKeyRepeat, isFocusLost } from '@/lib/focus';
import type { ConfirmationRequest } from '@/lib/confirm-required';

interface Props {
  request: ConfirmationRequest | null;
  confirmLabel?: string;
  onConfirm: () => void;
  onClose: () => void;
  /** Element that gets the focus when the dialog closes; without it the element focused at open is used. */
  restoreFocusTo?: () => HTMLElement | null;
}

// Generic dialog for the 422 confirmation codes (P10); open while a request is set.
export function ConfirmRequiredDialog({ request, confirmLabel = 'Vẫn tiếp tục', onConfirm, onClose, restoreFocusTo }: Props) {
  const blocked = request?.blocked ?? false;
  // The dialog stays mounted and closes when the request is cleared. Unmounted, it went away with its page (browser
  // Back while it was open), and Radix still asks where the focus goes a tick later.
  const mountedRef = useRef(false);
  useEffect(() => {
    mountedRef.current = true;
    return () => {
      mountedRef.current = false;
    };
  }, []);
  return (
    <Dialog open={request !== null} onOpenChange={(next) => !next && onClose()}>
      <DialogContent
        showClose={false}
        className="max-w-lg"
        onCloseAutoFocus={(event) => {
          // Nothing to give the focus back to for a page that is gone, not even what had it when the dialog opened.
          if (!mountedRef.current) {
            event.preventDefault();
            return;
          }
          const target = restoreFocusTo?.();
          if (!target) return;
          event.preventDefault();
          // A key still held down (the Escape or Enter that closed the dialog) must not act on that element.
          if (isFocusLost()) giveFocusBack(target);
        }}
      >
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <AppIcon name="warning" size={20} tone={blocked ? 'danger' : 'warn'} />
            {request?.title}
          </DialogTitle>
          <DialogDescription>{request?.message}</DialogDescription>
        </DialogHeader>
        {request && request.items.length > 0 && (
          <ul className="max-h-72 divide-y overflow-auto rounded-md border text-sm">
            {request.items.map((item) => (
              <li key={item.key} className="px-3 py-2">
                <div className="font-medium">{item.key}</div>
                {item.messages.map((m) => (
                  <div key={m} className="text-muted-foreground">
                    {m}
                  </div>
                ))}
              </li>
            ))}
          </ul>
        )}
        {/* The press that opened the dialog may still be down: its repeats must not press a button here. */}
        <DialogFooter onKeyDown={ignoreKeyRepeat}>
          {blocked ? (
            <Button variant="outline" onClick={onClose}>
              Đóng
            </Button>
          ) : (
            <>
              <Button variant="outline" onClick={onClose}>
                Quay lại
              </Button>
              <Button variant="destructive" onClick={onConfirm}>
                {confirmLabel}
              </Button>
            </>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
