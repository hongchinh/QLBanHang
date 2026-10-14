import type { ReactNode } from 'react';

interface Props {
  /**
   * Sticky notice above the app (the push permission prompt); takes its own height. Pass null when there is
   * nothing to show: an element that renders nothing still gets a slot.
   */
  banner?: ReactNode;
  children: ReactNode;
}

// The window-high column every route lives in. The banner and the app share the viewport height, so the
// document never scrolls: the app (AppLayout, the login page...) gets what the banner leaves.
export function AppShell({ banner, children }: Props) {
  return (
    <div data-testid="app-shell" className="flex h-screen flex-col">
      {banner ? <div className="shrink-0">{banner}</div> : null}
      <div className="flex min-h-0 flex-1 flex-col overflow-y-auto">{children}</div>
    </div>
  );
}
