import { AlertTriangle } from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { ignoreKeyRepeat, isFocusLost } from '@/lib/focus';
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
  return (
    <Dialog open={request !== null} onOpenChange={(next) => !next && onClose()}>
      <DialogContent
        showClose={false}
        className="max-w-lg"
        onCloseAutoFocus={(event) => {
          const target = restoreFocusTo?.();
          if (!target) return;
          event.preventDefault();
          if (isFocusLost()) target.focus();
        }}
      >
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <AlertTriangle className={blocked ? 'h-5 w-5 text-red-600' : 'h-5 w-5 text-amber-600'} />
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
