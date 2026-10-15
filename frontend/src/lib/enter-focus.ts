import { canTakeFocus } from '@/lib/focus';   // connected and not :disabled (covers `fieldset disabled`)

export type FocusStep = 'moved' | 'end' | 'unknown';

/** Focuses the next (direction 1) or previous (-1) field of `ids` that exists and is not disabled. */
export function focusNextInOrder(ids: readonly string[], currentId: string, direction: 1 | -1): FocusStep {
  const at = ids.indexOf(currentId);
  if (at === -1) return 'unknown';
  for (let i = at + direction; i >= 0 && i < ids.length; i += direction) {
    const el = document.getElementById(ids[i]);
    if (canTakeFocus(el)) {
      el.focus();
      return 'moved';
    }
  }
  return 'end';
}

/** Focuses an element; `select` selects its text, `end` puts the caret after it. */
export function focusById(id: string, mode: 'keep' | 'select' | 'end' = 'keep'): boolean {
  const el = document.getElementById(id);
  if (!canTakeFocus(el)) return false;
  el.focus();
  if (el instanceof HTMLInputElement || el instanceof HTMLTextAreaElement) {
    try {
      if (mode === 'select') el.select();
      else if (mode === 'end') el.setSelectionRange(el.value.length, el.value.length);
    } catch {
      // Inputs such as datetime-local have no selection range.
    }
  }
  return true;
}
