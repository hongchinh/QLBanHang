import type { ReactNode } from 'react';

interface Props {
  /**
   * Sticky notice above the app (the push permission prompt). It takes its own height, up to 30% of the
   * window: it cannot be scrolled away, so a notice that wraps to more lines than that (a phone) scrolls
   * inside its slot. Pass null when there is nothing to show: an element that renders nothing still gets a slot.
   */
  banner?: ReactNode;
  children: ReactNode;
}

// The window-high column every route lives in. The banner and the app share the viewport height, so the
// document never scrolls: the app (AppLayout, the login page...) gets what the banner leaves.
// Its safe-area padding keeps the app clear of the iPhone home indicator and, in landscape, the notch.
export function AppShell({ banner, children }: Props) {
  return (
    <div data-testid="app-shell" className="flex h-screen flex-col pb-safe pl-safe pr-safe">
      {banner ? <div className="max-h-[30vh] shrink-0 overflow-y-auto">{banner}</div> : null}
      <div className="flex min-h-0 flex-1 flex-col overflow-y-auto">{children}</div>
    </div>
  );
}
