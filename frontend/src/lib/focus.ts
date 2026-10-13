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
