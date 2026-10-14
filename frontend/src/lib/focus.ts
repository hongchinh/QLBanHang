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
