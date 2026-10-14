/** Any open dialog: Radix Dialog content has role="dialog", an AlertDialog role="alertdialog". */
export const DIALOG_SELECTOR = '[role="dialog"], [role="alertdialog"]';

/** The page area of the app layout, where the routed page lives. The app header and the sidebar are outside it. */
export const PAGE_AREA_SELECTOR = 'main#main-content';

/** True when nothing has the focus (it fell back on <body>), e.g. after the focused element was disabled or removed. */
export function isFocusLost(): boolean {
  const active = document.activeElement;
  return !active || active === document.body;
}

/** The element that has the focus, or null when it is lost. */
export function currentFocus(): HTMLElement | null {
  const active = document.activeElement;
  return active instanceof HTMLElement && active !== document.body ? active : null;
}

/** Still on the page and able to take the focus. */
export function canTakeFocus(el: HTMLElement | null | undefined): el is HTMLElement {
  return !!el && el.isConnected && !el.matches(':disabled');
}

/**
 * onKeyDown for the buttons of a dialog that a key press may open: the auto-repeats of a held Enter or Space do not
 * press them, a single press does. Enter held on the save button opens the 422 dialog, whose first button takes the
 * focus: its repeats would close the dialog before it could be read, give the focus back to the save button and save
 * again. Other keys repeat as usual (a held Tab still walks through the buttons).
 */
export function ignoreKeyRepeat(event: { key: string; repeat: boolean; preventDefault(): void }): void {
  if (event.repeat && (event.key === 'Enter' || event.key === ' ')) event.preventDefault();
}

/** Ends the watch of the last `giveFocusBack`, while it is still on. */
let endHeldKeyWatch: (() => void) | null = null;

/**
 * Gives the focus back to `el` after a dialog or a running action took it away.
 *
 * A key that is still held down auto-repeats on whatever has the focus. It was pressed somewhere else (on the button
 * of the dialog that has just closed, on the save button before it was disabled), so its repeats must not act on
 * `el`: a held Enter would press the save button again after every answer, or reopen the dialog it has just closed.
 * They are swallowed until the key is released or another key is pressed.
 */
export function giveFocusBack(el: HTMLElement): void {
  endHeldKeyWatch?.();
  const onKeyDown = (event: KeyboardEvent) => {
    // A new press is the user's own, and so are its repeats.
    if (!event.repeat) return end();
    if (event.target !== el) return;
    event.preventDefault();
    event.stopImmediatePropagation();
  };
  const end = () => {
    document.removeEventListener('keydown', onKeyDown, true);
    document.removeEventListener('keyup', end, true);
    endHeldKeyWatch = null;
  };
  // Capture phase on the document: before any key handler of the page, which may act without waiting for a click.
  document.addEventListener('keydown', onKeyDown, true);
  document.addEventListener('keyup', end, true);
  endHeldKeyWatch = end;
  el.focus();
}
