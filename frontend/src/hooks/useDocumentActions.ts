import { useState } from 'react';
import { formatApiErrorDetails, getApiError } from '@/lib/api-client';
import {
  toConfirmationRequest,
  withConfirmed,
  type ConfirmationRequest,
  type ConfirmFlags,
} from '@/lib/confirm-required';
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
 * The action flow shared by the document forms: busy state, the 422 confirmation chain
 * (a confirmed resend keeps every earlier flag; a newer 422 replaces the pending request),
 * 409 reload and the error toasts.
 *
 * Callers own the submit guard (run has no re-entry guard), `perform` (mutations, navigation, success
 * toasts; it should reject with the API error) and the field-error mapping; the hook owns busy, the 422
 * resend with accumulated flags (token-aware via withConfirmed), the 409 reload and the error toasts.
 */
export function useDocumentActions<A extends DocumentAction>(options: Options<A>) {
  const [busy, setBusy] = useState(false);
  const [confirmation, setConfirmation] = useState<{ action: A; request: ConfirmationRequest } | null>(null);

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
