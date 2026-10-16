import { useEffect, useLayoutEffect, useRef, useState } from 'react';

// Keys that were added since the first commit, each kept for `durationMs` after it appeared (the flash of newly
// shown grid columns). The first commit marks nothing, so opening a saved document does not flash.
export function useAppearedKeys(keys: readonly string[], durationMs = 1200): ReadonlySet<string> {
  // Compared by value (keys never hold '|'): callers build a new array on every render.
  const signature = keys.join('|');
  // Written only by the layout effect. A ref written during render would already hold the new keys on StrictMode's
  // second render, and nothing would ever be marked in dev.
  const previous = useRef<string[] | null>(null);
  const timers = useRef(new Map<string, ReturnType<typeof setTimeout>>());
  const [appeared, setAppeared] = useState<ReadonlySet<string>>(() => new Set());

  useLayoutEffect(() => {
    const current = signature === '' ? [] : signature.split('|');
    const before = previous.current;
    previous.current = current;
    if (before === null) return;
    const added = current.filter((key) => !before.includes(key));
    const removed = before.filter((key) => !current.includes(key));
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
  }, [signature, durationMs]);

  useEffect(() => {
    const pending = timers.current;
    return () => {
      for (const id of pending.values()) clearTimeout(id);
      pending.clear();
    };
  }, []);

  return appeared;
}
