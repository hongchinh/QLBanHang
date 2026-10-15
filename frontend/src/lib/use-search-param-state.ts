import { useCallback } from 'react';
import { useLocation, useSearchParams } from 'react-router-dom';

/** setSearchParams that replaces the history entry and keeps its state (e.g. a drill-down's `state.from`). */
export function useReplaceSearchParams() {
  const [, setParams] = useSearchParams();
  const { state } = useLocation();
  return useCallback(
    (update: (prev: URLSearchParams) => URLSearchParams) => setParams(update, { replace: true, state }),
    [setParams, state],
  );
}

export function useSearchParamString(key: string, defaultValue = ''): [string, (next: string) => void] {
  const [params] = useSearchParams();
  const setParams = useReplaceSearchParams();
  const value = params.get(key) ?? defaultValue;
  const setValue = useCallback(
    (next: string) => {
      setParams((prev) => {
        const out = new URLSearchParams(prev);
        if (!next) out.delete(key);
        else out.set(key, next);
        return out;
      });
    },
    [key, setParams],
  );
  return [value, setValue];
}

export function useSearchParamNumber(key: string, defaultValue = 1): [number, (next: number) => void] {
  const [params] = useSearchParams();
  const setParams = useReplaceSearchParams();
  const raw = params.get(key);
  const parsed = raw ? Number(raw) : NaN;
  const value = Number.isFinite(parsed) && parsed > 0 ? parsed : defaultValue;
  const setValue = useCallback(
    (next: number) => {
      setParams((prev) => {
        const out = new URLSearchParams(prev);
        if (next === defaultValue) out.delete(key);
        else out.set(key, String(next));
        return out;
      });
    },
    [key, defaultValue, setParams],
  );
  return [value, setValue];
}
