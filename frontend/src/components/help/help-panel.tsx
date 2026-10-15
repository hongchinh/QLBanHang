import { useEffect, useRef, useState } from 'react';
import { useLocation } from 'react-router-dom';
import { AppIcon } from '@/components/icons';
import { visibleNavGroups } from '@/components/layout/nav-config';
import { Button } from '@/components/ui/button';
import { loadHelpIndex, useHelpIndex, useHelpLoadFailed } from '@/features/help/load-help';
import { accessibleRelated, docsInSameGroup } from '@/features/help/related';
import { videoFor } from '@/features/help/videos';
import { useCanAccessRoute } from '@/hooks/useCanAccessRoute';
import { useAuthStore } from '@/stores/auth-store';
import { useUiStore } from '@/stores/ui-store';
import { HelpDocView } from './help-doc-view';

export const HELP_PANEL_CLASS =
  'fixed inset-x-0 bottom-0 z-50 flex max-h-[85dvh] flex-col rounded-t-2xl border-t bg-card pb-safe shadow-xl md:absolute md:inset-x-auto md:bottom-0 md:right-0 md:top-16 md:z-30 md:max-h-none md:w-[400px] md:rounded-none md:border-l md:border-t-0 md:pb-0 min-[1600px]:static min-[1600px]:col-start-3 min-[1600px]:row-start-2 min-[1600px]:z-auto min-[1600px]:shadow-none';

// "2026-10-15" -> "15/10/2026" by splitting, so no time zone can shift the day.
function formatUpdated(updated: string): string {
  const [y, m, d] = updated.split('-');
  return `${d}/${m}/${y}`;
}

function HelpPanelOpen() {
  const { pathname } = useLocation();
  const closeHelp = useUiStore((s) => s.closeHelp);
  const index = useHelpIndex();
  const loadFailed = useHelpLoadFailed();
  const canAccess = useCanAccessRoute();
  const hasPermission = useAuthStore((s) => s.hasPermission);
  const isInRole = useAuthStore((s) => s.isInRole);
  // The doc picked inside the panel only counts on the route it was picked on.
  const [picked, setPicked] = useState<{ key: string; pathname: string } | null>(null);
  const viewKey = picked?.pathname === pathname ? picked.key : null;
  const asideRef = useRef<HTMLElement>(null);
  const titleRef = useRef<HTMLHeadingElement>(null);

  useEffect(() => {
    const previous = document.activeElement;
    const aside = asideRef.current;
    titleRef.current?.focus();
    return () => {
      // Not modal: only take the focus back when the user has not moved it to another field.
      const active = document.activeElement;
      const focusIsFree = active === document.body || (aside?.contains(active) ?? false);
      if (previous instanceof HTMLElement && previous.isConnected && focusIsFree) previous.focus();
    };
  }, []);

  useEffect(() => {
    const aside = asideRef.current;
    if (!aside) return;
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key !== 'Escape') return;
      e.stopPropagation();
      closeHelp();
    };
    aside.addEventListener('keydown', onKeyDown);
    return () => aside.removeEventListener('keydown', onKeyDown);
  }, [closeHelp]);

  const doc = index ? ((viewKey ? index.byKey.get(viewKey) : undefined) ?? index.resolve(pathname)) : null;
  const sameGroup =
    index && !doc ? docsInSameGroup(pathname, visibleNavGroups(hasPermission, isInRole), index, canAccess) : [];

  let body: React.ReactNode;
  if (!index) {
    body = loadFailed ? (
      <div className="space-y-3 text-sm">
        <p>Không tải được hướng dẫn. Kiểm tra kết nối mạng rồi thử lại.</p>
        <Button variant="outline" size="sm" onClick={() => { loadHelpIndex().catch(() => {}); }}>
          Thử lại
        </Button>
      </div>
    ) : (
      <p className="text-sm text-muted-foreground">Đang tải hướng dẫn…</p>
    );
  } else if (doc) {
    body = (
      <HelpDocView
        doc={doc}
        video={videoFor(doc.key)}
        related={accessibleRelated(doc, index, canAccess)}
        onOpenRelated={(key) => setPicked({ key, pathname })}
      />
    );
  } else {
    body = (
      <div className="space-y-3 text-sm">
        <p>Chưa có hướng dẫn cho màn hình này.</p>
        {sameGroup.length > 0 && (
          <div className="space-y-1.5">
            <p className="text-muted-foreground">Hướng dẫn cùng nhóm:</p>
            <ul className="space-y-1">
              {sameGroup.map((d) => (
                <li key={d.key}>
                  <button
                    type="button"
                    onClick={() => setPicked({ key: d.key, pathname })}
                    className="text-primary hover:underline"
                  >
                    {d.title}
                  </button>
                </li>
              ))}
            </ul>
          </div>
        )}
      </div>
    );
  }

  return (
    <>
      <button
        type="button"
        aria-label="Đóng hướng dẫn"
        className="fixed inset-0 z-40 bg-black/40 md:hidden"
        onClick={closeHelp}
      />
      <aside ref={asideRef} data-testid="help-panel" aria-labelledby="help-panel-title" className={HELP_PANEL_CLASS}>
        <div className="flex shrink-0 items-center gap-2 border-b px-4 py-3">
          {viewKey && (
            <Button variant="ghost" size="sm" onClick={() => setPicked(null)}>
              <AppIcon name="back" size={16} />
              Quay lại
            </Button>
          )}
          <h2 id="help-panel-title" ref={titleRef} tabIndex={-1} className="min-w-0 flex-1 truncate text-base font-semibold outline-none">
            {doc?.title ?? 'Hướng dẫn'}
          </h2>
          <Button variant="ghost" size="icon" aria-label="Đóng hướng dẫn" onClick={closeHelp}>
            <AppIcon name="close" size={16} />
          </Button>
        </div>
        <div data-testid="help-panel-body" className="min-h-0 flex-1 overflow-y-auto p-4">
          {body}
        </div>
        {doc && (
          <div className="shrink-0 border-t px-4 py-2 text-xs text-muted-foreground">
            Cập nhật {formatUpdated(doc.updated)}
          </div>
        )}
      </aside>
    </>
  );
}

export function HelpPanel() {
  const helpOpen = useUiStore((s) => s.helpOpen);
  return helpOpen ? <HelpPanelOpen /> : null;
}
