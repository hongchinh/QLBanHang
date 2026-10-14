import { useCallback, useEffect, useRef, useState } from 'react';
import { formatApiErrorDetails, getApiError } from '@/lib/api-client';
import {
  toConfirmationRequest,
  withConfirmed,
  type ConfirmationRequest,
  type ConfirmFlags,
} from '@/lib/confirm-required';
import { canTakeFocus, DIALOG_SELECTOR, giveFocusBack, isFocusLost, PAGE_AREA_SELECTOR } from '@/lib/focus';
import { toast } from '@/lib/use-toast';

/** `flags` are the confirmations (P10) already given for the action; a resend keeps them all. */
export type DocumentAction = { kind: string; flags: ConfirmFlags };

interface Options<A extends DocumentAction> {
  /** Runs the mutations, navigation and success toasts; should reject with the API error. */
  perform: (action: A, flags: ConfirmFlags) => Promise<void>;
  /** Refetches the document; resolves undefined when that failed. */
  reload: () => Promise<unknown | undefined>;
  /** Resets the form to the reloaded document. */
  onReloaded: (fresh: unknown) => void;
  /** Maps server validation keys onto the form; true when every key was mapped. */
  applyValidationErrors?: (details: Record<string, string[]>, action: A) => boolean;
  /** Toast title per action kind for a failed action. */
  errorTitles: Record<A['kind'], string>;
  /** Label of the confirm button of the 422 dialog, per action kind. */
  confirmLabels: Record<A['kind'], string>;
  /** Toast after a 409 once the document was reloaded. */
  concurrencyMessage: string;
  /** Toast after a 409 when the reload failed (resolved undefined or rejected); local edits are kept. */
  concurrencyReloadFailedMessage: string;
}

/**
 * Whether a focus on `el` tells where the user is in the page. A dialog comes and goes, and so do the focus guards
 * Radix puts at the edges of <body> while a dialog, menu or popover is open. The app header and the sidebar lie outside
 * the page area: the header search, left with Escape, must not get the focus when an action started from <body> ends
 * (Ctrl+S saves there). A form rendered without the app layout has no page area to keep to.
 */
function isInPage(el: HTMLElement): boolean {
  if (el.closest(DIALOG_SELECTOR) || el.hasAttribute('data-radix-focus-guard')) return false;
  const pageArea = document.querySelector(PAGE_AREA_SELECTOR);
  return !pageArea || pageArea.contains(el);
}

/**
 * Where the focus may go back to after an action or its 422 dialog: the element the user was last on in the page
 * (see isInPage) since the form opened, while it is on the page and enabled. Never another control: a blind Enter there
 * would run an action the user did not ask for (a save button comes first in a form, "Xóa nháp" on a new stock
 * voucher).
 *
 * A dialog comes and goes, so nothing inside one can take the focus back later. A cancel / delete / restore starts
 * from the button of a confirm dialog that is closing, and the 422 dialog is opened from state: neither leaves Radix a
 * trigger to return the focus to.
 */
function useFocusReturnTarget(): () => HTMLElement | null {
  const lastFocusRef = useRef<HTMLElement | null>(null);
  const mountedRef = useRef(false);
  useEffect(() => {
    mountedRef.current = true;
    // What had the focus when the form mounted is not remembered: that can be the sidebar link the form was opened
    // with, a control outside the form.
    const onFocusIn = (event: FocusEvent) => {
      const target = event.target;
      if (target instanceof HTMLElement && isInPage(target)) lastFocusRef.current = target;
    };
    // Capture phase: a control that stops its own focus event must not hide where the user is.
    document.addEventListener('focusin', onFocusIn, true);
    return () => {
      // The page is gone (browser Back with the 422 dialog open). The closed dialog still asks for a target a tick
      // later, and nothing may get the focus then, not even an element that outlives the page.
      mountedRef.current = false;
      document.removeEventListener('focusin', onFocusIn, true);
    };
  }, []);
  return useCallback(() => {
    const last = lastFocusRef.current;
    return mountedRef.current && canTakeFocus(last) ? last : null;
  }, []);
}

/**
 * The action flow shared by the document forms: busy state, the 422 confirmation chain
 * (a confirmed resend keeps every earlier flag; a newer 422 replaces the pending request),
 * 409 reload and the error toasts.
 *
 * Callers own the submit guard (run has no re-entry guard), `perform` (mutations, navigation, success
 * toasts; it should reject with the API error) and the field-error mapping; the hook owns busy, the 422
 * resend with accumulated flags (token-aware via withConfirmed), the 409 reload and the error toasts.
 * It also gives the focus back to where the user was once an action has ended or its 422 dialog has closed.
 */
export function useDocumentActions<A extends DocumentAction>(options: Options<A>) {
  const [busy, setBusy] = useState(false);
  const [confirmation, setConfirmation] = useState<{ action: A; request: ConfirmationRequest } | null>(null);

  // Where the focus goes back to after an action or its 422 dialog. The action disables its own buttons while it runs,
  // which makes the browser drop the focus, so "what has the focus now" is not enough.
  const focusReturnTarget = useFocusReturnTarget();
  // `busy` as of the last render, to tell when an action has just ended.
  const wasBusyRef = useRef(false);

  // Once an action has ended, however it ended (a save that stays on the page, a 409, a 500, a confirmed resend), and
  // its buttons are enabled again.
  useEffect(() => {
    const ended = wasBusyRef.current && !busy;
    wasBusyRef.current = busy;
    if (!ended) return;
    // The 422 dialog takes the focus itself and gives it back when it closes.
    if (confirmation) return;
    // Never take the focus from something the user (or the page) already moved it to.
    const target = isFocusLost() ? focusReturnTarget() : null;
    // The Enter that pressed the save button may still be down: it must not save again now that the button is back.
    if (target) giveFocusBack(target);
  }, [busy, confirmation, focusReturnTarget]);

  async function run(action: A): Promise<void> {
    setBusy(true);
    try {
      await options.perform(action, action.flags);
    } catch (err) {
      await handleError(err, action);
    } finally {
      setBusy(false);
    }
  }

  async function handleError(err: unknown, action: A) {
    const request = toConfirmationRequest(err);
    if (request) {
      setConfirmation({ action, request });
      return;
    }
    const apiError = getApiError(err);
    switch (apiError?.code) {
      case 'CONCURRENCY': {
        // D29: reload and drop local edits; never resend stale values with the new version.
        const fresh = await options.reload().catch(() => undefined);
        if (!fresh) {
          // Nothing reloaded: keep the user's edits rather than resetting to the stale cache.
          toast({ variant: 'destructive', title: options.concurrencyReloadFailedMessage });
          return;
        }
        options.onReloaded(fresh);
        toast({ variant: 'destructive', title: options.concurrencyMessage });
        return;
      }
      case 'VALIDATION':
        if (apiError.details && options.applyValidationErrors?.(apiError.details, action)) {
          toast({ variant: 'destructive', title: options.errorTitles[action.kind as A['kind']], description: apiError.message });
          return;
        }
        break;
    }
    toast({
      variant: 'destructive',
      title: options.errorTitles[action.kind as A['kind']],
      description: formatApiErrorDetails(err),
    });
  }

  const confirmDialogProps = {
    request: confirmation?.request ?? null,
    // Back to where the user was, whether the dialog was dismissed or confirmed. Null when that element is gone or
    // disabled, or the page is: the dialog then only has what had the focus when it opened. After a confirmation the
    // buttons are still disabled here, and the effect above gives the focus back once the resend has ended.
    restoreFocusTo: focusReturnTarget,
    confirmLabel: confirmation ? options.confirmLabels[confirmation.action.kind as A['kind']] : undefined,
    onConfirm: () => {
      if (!confirmation) return;
      const { action, request } = confirmation;
      setConfirmation(null);
      // A blocked 422 has nothing to confirm.
      if (request.blocked) return;
      void run({ ...action, flags: withConfirmed(action.flags, request) });
    },
    onClose: () => setConfirmation(null),
  };

  return { busy, run, confirmDialogProps };
}
