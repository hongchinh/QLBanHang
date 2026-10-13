import { useCallback, useMemo } from 'react';
import { useSearchParams } from 'react-router-dom';
import { computePreset } from '@/lib/date-range-presets';
import type { RangePreset } from '@/lib/date-range-presets';
import { formatDateYmd } from './format';

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

  const defaults = useMemo(() => {
    const today = new Date();
    const first = new Date(today.getFullYear(), today.getMonth(), 1);
    return { from: formatDateYmd(first), to: formatDateYmd(today) };
  }, []);

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
