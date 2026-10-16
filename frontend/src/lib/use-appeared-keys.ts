import { useEffect, useLayoutEffect, useRef, useState } from 'react';

// Keys that were added since the first commit, each kept for `durationMs` after it appeared (the flash of newly
// shown grid columns). The first commit marks nothing, so opening a saved document does not flash.
export function useAppearedKeys(keys: readonly string[], durationMs = 1200): ReadonlySet<string> {
  // Compared by value (keys never hold '|'): callers build a new array on every render.
  const signature = keys.join('|');
  // The signature of the last commit. Written only by the layout effect. A ref written during render would already
  // hold the new keys on StrictMode's second render, and nothing would ever be marked in dev.
  const previous = useRef<string | null>(null);
  const timers = useRef(new Map<string, ReturnType<typeof setTimeout>>());
  const [appeared, setAppeared] = useState<ReadonlySet<string>>(() => new Set());

  // No dependency list: the effect runs after every commit and compares with the last one itself. The grids compute
  // their keys from the form, a store outside React. A render can read the new keys and then be thrown away when no
  // state changed at its priority (a pick made outside an event, then a focus event). React keeps that render's deps,
  // so an effect keyed on [signature] would skip the commit that really shows the new keys.
  // eslint-disable-next-line react-hooks/exhaustive-deps -- every commit on purpose; a same signature returns first
  useLayoutEffect(() => {
    const before = previous.current;
    if (before === signature) return;
    previous.current = signature;
    if (before === null) return;
    const current = signature === '' ? [] : signature.split('|');
    const old = before === '' ? [] : before.split('|');
    const added = current.filter((key) => !old.includes(key));
    const removed = old.filter((key) => !current.includes(key));
    if (added.length === 0 && removed.length === 0) return;
    // A key that went away loses its mark and its timer; the other keys keep theirs.
    for (const key of removed) {
      clearTimeout(timers.current.get(key));
      timers.current.delete(key);
    }
    for (const key of added) {
      const id = setTimeout(() => {
        timers.current.delete(key);
        setAppeared((marks) => {
          const next = new Set(marks);
          next.delete(key);
          return next;
        });
      }, durationMs);
      timers.current.set(key, id);
    }
    setAppeared((marks) => {
      const next = new Set(marks);
      for (const key of removed) next.delete(key);
      for (const key of added) next.add(key);
      return next;
    });
  });

  useEffect(() => {
    const pending = timers.current;
    return () => {
      for (const id of pending.values()) clearTimeout(id);
      pending.clear();
    };
  }, []);

  return appeared;
}
