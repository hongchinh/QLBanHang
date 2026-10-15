import { useCallback, useSyncExternalStore } from 'react';

const hasMatchMedia = () => typeof window !== 'undefined' && typeof window.matchMedia === 'function';

// fallback is what the hook reports where matchMedia does not exist (tests, old browsers).
export function useMediaQuery(query: string, fallback = false): boolean {
  const subscribe = useCallback(
    (onChange: () => void) => {
      if (!hasMatchMedia()) return () => {};
      const mql = window.matchMedia(query);
      mql.addEventListener('change', onChange);
      return () => mql.removeEventListener('change', onChange);
    },
    [query],
  );
  const getSnapshot = () => (hasMatchMedia() ? window.matchMedia(query).matches : fallback);
  return useSyncExternalStore(subscribe, getSnapshot, () => fallback);
}
