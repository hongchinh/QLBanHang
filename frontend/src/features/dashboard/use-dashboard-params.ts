import { useCallback, useMemo } from 'react';
import { useSearchParams } from 'react-router-dom';
import { parseISO } from 'date-fns';
import { computePreset } from '@/lib/date-range-presets';
import type { RangePreset } from '@/lib/date-range-presets';
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

export function useDashboardParams(): UseDashboardParams {
  const [sp, setSp] = useSearchParams();

  // Without from/to in the URL: this month up to today. It follows the VN date, so a page left open past
  // midnight asks for the new day (or month) instead of the day it was opened.
  const today = useVnToday();
  // parseISO reads a date-only string as local midnight (VN time, lib/vn-datetime.ts).
  const defaults = useMemo(() => computePreset('this-month', parseISO(today)), [today]);

  const from = sp.get('from') ?? defaults.from;
  const to = sp.get('to') ?? defaults.to;
  const saleUserId = sp.get('saleUserId') ?? undefined;

  const setRange = useCallback(
    (f: string, t: string) => {
      setSp(
        (prev) => {
          const out = new URLSearchParams(prev);
          out.set('from', f);
          out.set('to', t);
          return out;
        },
        { replace: true },
      );
    },
    [setSp],
  );

  const setSaleUserId = useCallback(
    (id: string | undefined) => {
      setSp(
        (prev) => {
          const out = new URLSearchParams(prev);
          if (id) out.set('saleUserId', id);
          else out.delete('saleUserId');
          return out;
        },
        { replace: true },
      );
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
