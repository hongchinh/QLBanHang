import { useCallback, useMemo } from 'react';
import { useSearchParams } from 'react-router-dom';
import { parseISO } from 'date-fns';
import { computePreset } from '@/lib/date-range-presets';
import type { RangePreset } from '@/lib/date-range-presets';
import { useReplaceSearchParams } from '@/lib/use-search-param-state';
import { useVnToday } from '@/lib/use-vn-today';

// The presets live in lib/date-range-presets.ts; the type is re-exported for the modules that import it from here.
export type { RangePreset };

export interface UseDashboardParams {
  from: string;
  to: string;
  saleUserId: string | undefined;
  setRange: (from: string, to: string) => void;
  setSaleUserId: (id: string | undefined) => void;
  setPreset: (preset: RangePreset) => void;
}

export interface UseDashboardParamsOptions {
  // Whether the screen can ask for this period; a URL period that fails gives way to the default period.
  isValid?: (from: string, to: string) => boolean;
}

export function useDashboardParams({ isValid }: UseDashboardParamsOptions = {}): UseDashboardParams {
  const [sp] = useSearchParams();
  // Replaces the entry and keeps its state, so a drill-down's state.from survives a period or sale change.
  const setSp = useReplaceSearchParams();

  // Without from/to in the URL: this month up to today. It follows the VN date, so a page left open past
  // midnight asks for the new day (or month) instead of the day it was opened.
  const today = useVnToday();
  // parseISO reads a date-only string as local midnight (VN time, lib/vn-datetime.ts).
  const defaults = useMemo(() => computePreset('this-month', parseISO(today)), [today]);

  const urlFrom = sp.get('from') ?? defaults.from;
  const urlTo = sp.get('to') ?? defaults.to;
  // A hand-edited or old link with a period the screen refuses opens the default period instead.
  const valid = isValid?.(urlFrom, urlTo) ?? true;
  const from = valid ? urlFrom : defaults.from;
  const to = valid ? urlTo : defaults.to;
  const saleUserId = sp.get('saleUserId') ?? undefined;

  const setRange = useCallback(
    (f: string, t: string) => {
      setSp((prev) => {
        const out = new URLSearchParams(prev);
        out.set('from', f);
        out.set('to', t);
        return out;
      });
    },
    [setSp],
  );

  const setSaleUserId = useCallback(
    (id: string | undefined) => {
      setSp((prev) => {
        const out = new URLSearchParams(prev);
        if (id) out.set('saleUserId', id);
        else out.delete('saleUserId');
        return out;
      });
    },
    [setSp],
  );

  const setPreset = useCallback(
    (preset: RangePreset) => {
      const range = computePreset(preset);
      setRange(range.from, range.to);
    },
    [setRange],
  );

  return { from, to, saleUserId, setRange, setSaleUserId, setPreset };
}
