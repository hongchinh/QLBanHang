import { useEffect, useSyncExternalStore } from 'react';
import { buildHelpIndex, type HelpIndex } from './registry';

let cache: Promise<HelpIndex> | null = null;
let loaded: HelpIndex | null = null;
let failed = false;
const listeners = new Set<() => void>();
const emit = () => listeners.forEach((l) => l());
const subscribe = (l: () => void) => { listeners.add(l); return () => { listeners.delete(l); }; };

// One lazy chunk with every help file. A failed import (offline before the service worker cached it, or a chunk
// removed by a deploy) clears the cache so the next call tries again.
export function loadHelpIndex(): Promise<HelpIndex> {
  cache ??= import('./content-index').then(
    (m) => { loaded = buildHelpIndex(m.helpDocs); failed = false; emit(); return loaded; },
    (e: unknown) => { cache = null; failed = true; emit(); throw e; },
  );
  return cache;
}

export function useHelpIndex(): HelpIndex | null {
  useEffect(() => { loadHelpIndex().catch(() => {}); }, []);
  return useSyncExternalStore(subscribe, () => loaded, () => null);
}

export function useHelpLoadFailed(): boolean {
  return useSyncExternalStore(subscribe, () => failed, () => false);
}

export function resetHelpIndexCacheForTests(): void { cache = null; loaded = null; failed = false; }
