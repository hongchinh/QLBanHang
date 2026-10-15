import { useCallback, useEffect, useLayoutEffect, useRef, useState } from 'react';
import { useLocation } from 'react-router-dom';
import { AppIcon } from '@/components/icons';
import { visibleNavGroups } from '@/components/layout/nav-config';
import { Button } from '@/components/ui/button';
import { loadHelpIndex, useHelpIndex, useHelpLoadFailed } from '@/features/help/load-help';
import { accessibleRelated, docsInSameGroup } from '@/features/help/related';
import { videoFor } from '@/features/help/videos';
import { useCanAccessRoute } from '@/hooks/useCanAccessRoute';
import { useMediaQuery } from '@/hooks/useMediaQuery';
import { DIALOG_SELECTOR, POPPER_WRAPPER_SELECTOR } from '@/lib/focus';
import { cn } from '@/lib/utils';
import { useAuthStore } from '@/stores/auth-store';
import { useUiStore } from '@/stores/ui-store';
import { HelpDocView } from './help-doc-view';

export const HELP_PANEL_CLASS =
  'fixed inset-x-0 bottom-0 z-50 flex max-h-[85dvh] flex-col rounded-t-2xl border-t bg-card pb-safe shadow-xl md:absolute md:inset-x-auto md:bottom-0 md:right-0 md:top-16 md:z-30 md:max-h-none md:w-[400px] md:rounded-none md:border-l md:border-t-0 md:pb-0 min-[1600px]:static min-[1600px]:col-start-3 min-[1600px]:row-start-2 min-[1600px]:z-auto min-[1600px]:shadow-none';

// Laptop and tablet widths, where the panel lies over the page; from 1600px it is a column of its own.
const OVERLAY_QUERY = '(min-width: 768px) and (max-width: 1599.98px)';
const HELP_BUTTON_SELECTOR = '[data-help-button]';
// Longer than the 200ms animation: a panel whose animationend never comes still goes away.
const LEAVE_FALLBACK_MS = 400;

// shrink: toward the help button (overlay widths); sheet: down off a phone screen; none: no animation.
type HelpMotion = 'none' | 'shrink' | 'sheet';

function helpMotion(): HelpMotion {
  if (typeof window === 'undefined' || typeof window.matchMedia !== 'function') return 'none';
  const matches = (query: string) => window.matchMedia(query).matches;
  if (matches('(prefers-reduced-motion: reduce)') || matches('(min-width: 1600px)')) return 'none';
  return matches('(min-width: 768px)') ? 'shrink' : 'sheet';
}

// The help button's centre, relative to the panel: it can lie outside the panel box.
function originAtHelpButton(panel: HTMLElement): string {
  const button = document.querySelector(HELP_BUTTON_SELECTOR);
  if (!button) return '';
  const p = panel.getBoundingClientRect();
  const b = button.getBoundingClientRect();
  return `${b.left + b.width / 2 - p.left}px ${b.top + b.height / 2 - p.top}px`;
}

function pingHelpButton() {
  const button = document.querySelector(HELP_BUTTON_SELECTOR);
  if (!(button instanceof HTMLElement)) return;
  button.classList.remove('help-ping');
  void button.offsetWidth; // restart the animation when it is still on
  button.classList.add('help-ping');
  button.addEventListener('animationend', () => button.classList.remove('help-ping'), { once: true });
}

// "2026-10-15" -> "15/10/2026" by splitting, so no time zone can shift the day.
function formatUpdated(updated: string): string {
  const [y, m, d] = updated.split('-');
  return `${d}/${m}/${y}`;
}

function HelpPanelOpen({ motion, leaving, onLeft }: { motion: HelpMotion; leaving: boolean; onLeft: () => void }) {
  const { pathname } = useLocation();
  const closeHelp = useUiStore((s) => s.closeHelp);
  const index = useHelpIndex();
  const loadFailed = useHelpLoadFailed();
  const canAccess = useCanAccessRoute();
  const hasPermission = useAuthStore((s) => s.hasPermission);
  const isInRole = useAuthStore((s) => s.isInRole);
  // The doc picked inside the panel only counts on the route it was picked on.
  const [picked, setPicked] = useState<{ key: string; pathname: string } | null>(null);
  if (picked && picked.pathname !== pathname) setPicked(null); // reset during render: no flash of the old pick
  const viewKey = picked?.pathname === pathname ? picked.key : null;
  const focusTitleAfterPick = useRef(false);
  const pick = (next: { key: string; pathname: string } | null) => {
    focusTitleAfterPick.current = true;
    setPicked(next);
  };
  const asideRef = useRef<HTMLElement>(null);
  const titleRef = useRef<HTMLHeadingElement>(null);
  const overlay = useMediaQuery(OVERLAY_QUERY);

  // Aim at the help button when opening and again when leaving: the page may have scrolled or resized.
  useLayoutEffect(() => {
    if (motion === 'shrink' && asideRef.current) asideRef.current.style.transformOrigin = originAtHelpButton(asideRef.current);
  }, [motion, leaving]);

  const finishLeaving = useCallback(() => {
    if (motion === 'shrink') pingHelpButton();
    onLeft();
  }, [motion, onLeft]);

  useEffect(() => {
    const aside = asideRef.current;
    if (!leaving || !aside) return;
    const onAnimationEnd = (e: Event) => {
      if (e.target === aside) finishLeaving();
    };
    const timer = window.setTimeout(finishLeaving, LEAVE_FALLBACK_MS);
    aside.addEventListener('animationend', onAnimationEnd);
    return () => {
      window.clearTimeout(timer);
      aside.removeEventListener('animationend', onAnimationEnd);
    };
  }, [leaving, finishLeaving]);

  // Over the page, a press anywhere else closes the panel. Capture phase, and the press still does its own job.
  useEffect(() => {
    if (!overlay || leaving) return;
    const onPointerDown = (e: PointerEvent) => {
      const target = e.target;
      if (!(target instanceof Element) || asideRef.current?.contains(target)) return;
      // The help button toggles by itself; a dialog or popup over the page is not "the page".
      if (target.closest(`${HELP_BUTTON_SELECTOR}, ${DIALOG_SELECTOR}, ${POPPER_WRAPPER_SELECTOR}`)) return;
      closeHelp();
    };
    document.addEventListener('pointerdown', onPointerDown, true);
    return () => document.removeEventListener('pointerdown', onPointerDown, true);
  }, [overlay, leaving, closeHelp]);

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

  // The clicked button unmounts when the shown doc changes: keep the focus in the panel, on the title.
  useEffect(() => {
    if (!focusTitleAfterPick.current) return;
    focusTitleAfterPick.current = false;
    titleRef.current?.focus();
  }, [picked]);

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
        onOpenRelated={(key) => pick({ key, pathname })}
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
                    onClick={() => pick({ key: d.key, pathname })}
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
        tabIndex={-1}
        className={cn(
          'fixed inset-0 z-40 bg-black/40 md:hidden',
          motion === 'sheet' && (leaving ? 'pointer-events-none animate-out fade-out-0 fill-mode-forwards' : 'animate-in fade-in-0'),
        )}
        onClick={closeHelp}
      />
      <aside
        ref={asideRef}
        data-testid="help-panel"
        aria-labelledby="help-panel-title"
        className={cn(
          HELP_PANEL_CLASS,
          motion !== 'none' && 'duration-200',
          motion === 'shrink' && (leaving ? 'animate-out fade-out-0 zoom-out-[0.04]' : 'animate-in fade-in-0 zoom-in-[0.04]'),
          motion === 'sheet' && (leaving ? 'animate-out slide-out-to-bottom' : 'animate-in slide-in-from-bottom'),
          leaving && 'pointer-events-none fill-mode-forwards',
        )}
      >
        <div className="flex shrink-0 items-center gap-2 border-b px-4 py-3">
          {viewKey && (
            <Button variant="ghost" size="sm" onClick={() => pick(null)}>
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
  // The panel stays mounted while it animates away; each opening is a fresh panel.
  const [shown, setShown] = useState({ open: helpOpen, session: 0, motion: 'none' as HelpMotion, leaving: false });
  if (shown.open !== helpOpen) {
    const motion = helpMotion();
    setShown(
      helpOpen
        ? { open: true, session: shown.session + 1, motion, leaving: false }
        : { ...shown, open: false, motion, leaving: motion !== 'none' },
    );
  }
  const onLeft = useCallback(() => setShown((s) => (s.leaving ? { ...s, leaving: false } : s)), []);
  if (!helpOpen && !shown.leaving) return null;
  return <HelpPanelOpen key={shown.session} motion={shown.motion} leaving={!helpOpen} onLeft={onLeft} />;
}
