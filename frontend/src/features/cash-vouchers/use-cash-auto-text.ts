import { useCallback, useMemo, useRef } from 'react';
import type { UseFormReturn } from 'react-hook-form';
import { buildAutoDescription, followAuto, followHeaderInLines } from './auto-text';
import type { CashLineFormValues, CashVoucherFormParsed, CashVoucherFormValues } from './schema';

// Event-driven "reason - partner" text of Lý do nộp/chi and the line descriptions (section-01 §3).
// No effect watches the form: the handlers of the user's actions call these methods, so a reset or a
// background refetch never writes. The page sets the baselines explicitly.
export function useCashAutoText(form: UseFormReturn<CashVoucherFormValues, unknown, CashVoucherFormParsed>) {
  const prevAuto = useRef(''); // what the system last wrote (or would have written) in Lý do nộp
  const prevDescription = useRef(''); // the last known Lý do nộp, for the lines that follow it
  const settled = useRef(false); // initNew runs once, never after a baseline or a user action

  const followLines = useCallback(
    (next: string, dirty: boolean) => {
      const lines = form.getValues('lines') as CashLineFormValues[];
      const { lines: followed, changed } = followHeaderInLines(lines, prevDescription.current, next);
      if (changed) {
        followed.forEach((line, i) => {
          if (line !== lines[i]) form.setValue(`lines.${i}.description`, line.description ?? '', { shouldDirty: dirty });
        });
      }
      prevDescription.current = next;
    },
    [form],
  );

  const writeDescription = useCallback(
    (next: string, dirty: boolean) => {
      form.setValue('description', next, { shouldDirty: dirty });
      followLines(next, dirty);
    },
    [form, followLines],
  );

  return useMemo(
    () => ({
      /** One-time fill of a new voucher once the default reason is known; never dirties the form. */
      initNew(reasonName: string) {
        if (settled.current || !reasonName.trim()) return;
        settled.current = true;
        const auto = buildAutoDescription(reasonName, form.getValues('partnerName'));
        const next = followAuto(form.getValues('description'), prevAuto.current, auto);
        prevAuto.current = auto;
        if (next !== (form.getValues('description') ?? '')) writeDescription(next, false);
      },
      /** A saved voucher (or a form after reset): remember what the auto text would be, write nothing. */
      baseline(v: { reasonName?: string; partnerName?: string; description?: string } | undefined) {
        settled.current = true;
        prevAuto.current = buildAutoDescription(v?.reasonName, v?.partnerName);
        prevDescription.current = v?.description ?? '';
      },
      /** The reason or the partner changed by the user: one call, one write. */
      onInputsChanged({ reasonName, partnerName }: { reasonName: string; partnerName: string }) {
        settled.current = true;
        const auto = buildAutoDescription(reasonName, partnerName);
        const current = form.getValues('description') ?? '';
        const next = followAuto(current, prevAuto.current, auto);
        prevAuto.current = auto;
        if (next !== current) writeDescription(next, true);
      },
      /** Lý do nộp/chi typed by hand: the lines that followed it follow the new text. */
      onDescriptionEdited(value: string) {
        followLines(value, true);
      },
    }),
    [form, followLines, writeDescription],
  );
}
