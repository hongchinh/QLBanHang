import { useState } from 'react';
import type { Updater } from '@tanstack/react-table';

export type ColumnSizing = Record<string, number>;

function storageKeyFor(tableKey: string): string {
  return `qldh:col-sizes:${tableKey}`;
}

function readPersisted(tableKey: string): ColumnSizing | null {
  try {
    const raw = window.localStorage.getItem(storageKeyFor(tableKey));
    if (!raw) return null;
    const parsed = JSON.parse(raw);
    if (parsed && typeof parsed === 'object') return parsed as ColumnSizing;
    return null;
  } catch {
    return null;
  }
}

export function useColumnSizingPersist(
  tableKey: string,
  initialSizing: ColumnSizing = {},
): [ColumnSizing, (updater: Updater<ColumnSizing>) => void] {
  const [sizing, setSizingState] = useState<ColumnSizing>(() => {
    const persisted = readPersisted(tableKey);
    return persisted ? { ...initialSizing, ...persisted } : { ...initialSizing };
  });

  const setSizing = (updater: Updater<ColumnSizing>) => {
    setSizingState((prev) => {
      const next = typeof updater === 'function' ? updater(prev) : updater;
      try {
        window.localStorage.setItem(storageKeyFor(tableKey), JSON.stringify(next));
      } catch {
        // localStorage unavailable (e.g. private browsing quota) — ignore, in-memory state still updates
      }
      return next;
    });
  };

  return [sizing, setSizing];
}
