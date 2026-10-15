import { useEffect } from 'react';
import { useLocation } from 'react-router-dom';
import { useAuthStore } from '@/stores/auth-store';
import { useUiStore } from '@/stores/ui-store';
import { shouldShowHint } from './hint';
import { useHelpIndex } from './load-help';

export function useFirstVisitHint() {
  const userId = useAuthStore((s) => s.user?.id);
  const index = useHelpIndex();
  const { pathname } = useLocation();
  const doc = index?.resolve(pathname) ?? null;
  const helpOpen = useUiStore((s) => s.helpOpen);
  const seen = useUiStore((s) => s.helpHintSeen);
  const off = useUiStore((s) => s.helpHintsOff);
  const { markHelpHintSeen, turnOffHelpHints, openHelp } = useUiStore.getState();
  const key = doc?.key;

  // The panel shows this screen's help, so its hint counts as seen.
  useEffect(() => {
    if (helpOpen && userId && key) markHelpHintSeen(userId, key);
  }, [helpOpen, userId, key, markHelpHintSeen]);

  const dismiss = () => {
    if (userId && key) markHelpHintSeen(userId, key);
  };

  return {
    visible: shouldShowHint({ userId, doc, helpOpen, seen, off }),
    doc,
    dismiss,
    openFromHint: () => {
      dismiss();
      openHelp();
    },
    turnOff: () => {
      if (userId) turnOffHelpHints(userId);
    },
  };
}
