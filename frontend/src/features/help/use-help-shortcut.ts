import { useEffect } from 'react';
import { DIALOG_SELECTOR, POPPER_WRAPPER_SELECTOR } from '@/lib/focus';
import { useUiStore } from '@/stores/ui-store';

/** F1 toggles the help panel, unless a modal dialog is open. */
export function useHelpShortcut(): void {
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key !== 'F1' || e.ctrlKey || e.altKey || e.shiftKey || e.metaKey || e.repeat) return;
      const modalOpen = [...document.querySelectorAll(DIALOG_SELECTOR)].some((el) => !el.closest(POPPER_WRAPPER_SELECTOR));
      if (modalOpen) return;
      e.preventDefault();
      useUiStore.getState().toggleHelp();
    };
    // Capture, so a grid handler that stops propagation cannot swallow F1.
    window.addEventListener('keydown', onKey, true);
    return () => window.removeEventListener('keydown', onKey, true);
  }, []);
}
