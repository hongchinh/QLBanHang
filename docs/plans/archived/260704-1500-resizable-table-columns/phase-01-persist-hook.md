# Phase 01 — Column sizing persistence hook

**Status:** [x] done
**Complexity:** S

## Objective
Create a reusable hook `useColumnSizingPersist(storageKey, initialSizing?)` that manages TanStack Table's `ColumnSizingState` (`Record<string, number>`), backed by `localStorage`, so any table can persist user-resized column widths across page reloads.

## Files
- `frontend/src/lib/use-column-sizing-persist.ts` (new)
- `frontend/src/lib/use-column-sizing-persist.test.ts` (new)

## Tasks

### Task 1: Confirm TanStack Table version supports required APIs
1. Run `grep -n "@tanstack/react-table" d:/Projects/QLDonHang/frontend/package.json` (or open the file) to confirm the installed major version is v8+.
2. Expected: version string starting with `^8.` or `8.` — these APIs (`columnResizeMode`, `getCanResize`, `getResizeHandler`, `getIsResizing`, `getSize`, `getTotalSize`) are stable since v8.0. If the version is older, stop and flag to the user before continuing (out of scope to upgrade TanStack Table in this plan).

### Task 2: Write the failing test for `useColumnSizingPersist`
1. Write the failing test — create `frontend/src/lib/use-column-sizing-persist.test.ts`:

```ts
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { act, renderHook } from '@testing-library/react';
import { useColumnSizingPersist } from './use-column-sizing-persist';

const STORAGE_KEY = 'qldh:col-sizes:test-table';

describe('useColumnSizingPersist', () => {
  beforeEach(() => {
    window.localStorage.clear();
  });
  afterEach(() => {
    window.localStorage.clear();
  });

  it('returns the initial sizing when localStorage is empty', () => {
    const { result } = renderHook(() =>
      useColumnSizingPersist('test-table', { code: 100 }),
    );
    expect(result.current[0]).toEqual({ code: 100 });
  });

  it('returns an empty object when no initial sizing and localStorage is empty', () => {
    const { result } = renderHook(() => useColumnSizingPersist('test-table'));
    expect(result.current[0]).toEqual({});
  });

  it('persists updates to localStorage under the namespaced key', () => {
    const { result } = renderHook(() =>
      useColumnSizingPersist('test-table', { code: 100 }),
    );
    act(() => {
      result.current[1]({ code: 200 });
    });
    expect(result.current[0]).toEqual({ code: 200 });
    expect(JSON.parse(window.localStorage.getItem(STORAGE_KEY)!)).toEqual({
      code: 200,
    });
  });

  it('reads previously persisted sizing on mount, merged over initial sizing', () => {
    window.localStorage.setItem(STORAGE_KEY, JSON.stringify({ code: 250 }));
    const { result } = renderHook(() =>
      useColumnSizingPersist('test-table', { code: 100, name: 160 }),
    );
    expect(result.current[0]).toEqual({ code: 250, name: 160 });
  });

  it('ignores malformed JSON in localStorage and falls back to initial sizing', () => {
    window.localStorage.setItem(STORAGE_KEY, '{not valid json');
    const { result } = renderHook(() =>
      useColumnSizingPersist('test-table', { code: 100 }),
    );
    expect(result.current[0]).toEqual({ code: 100 });
  });

  it('accepts a functional updater like React setState', () => {
    const { result } = renderHook(() =>
      useColumnSizingPersist('test-table', { code: 100 }),
    );
    act(() => {
      result.current[1]((prev) => ({ ...prev, name: 160 }));
    });
    expect(result.current[0]).toEqual({ code: 100, name: 160 });
  });
});
```

2. Run test to verify it fails — `cd frontend && npx vitest run src/lib/use-column-sizing-persist.test.ts` / Expected: FAIL with `Cannot find module './use-column-sizing-persist'` (file doesn't exist yet).

### Task 3: Implement `useColumnSizingPersist`
1. Write minimal implementation — create `frontend/src/lib/use-column-sizing-persist.ts`:

```ts
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
```

2. Run tests to verify they pass — `cd frontend && npx vitest run src/lib/use-column-sizing-persist.test.ts` / Expected: PASS (6 tests).
3. Commit — `git commit -m "feat(table): add useColumnSizingPersist hook for persisting column widths"`

## Verification
- `cd frontend && npx vitest run src/lib/use-column-sizing-persist.test.ts` → all pass
- `cd frontend && npx tsc --noEmit` → no new type errors

## Exit Criteria
- `useColumnSizingPersist` hook exists, is fully unit-tested, and correctly merges/persists sizing state to `localStorage` under a namespaced key per table.
