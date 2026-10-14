import { useState } from 'react';
import { defaultGranularity } from './period';
import type { CashPoint, SalesPoint, SeriesGranularity } from './types';

// A chart tile is empty when no bucket has a figure.
export const isSalesSeriesEmpty = (points: SalesPoint[]) =>
  points.every((p) => p.revenue === 0 && (p.cost ?? 0) === 0 && (p.grossProfit ?? 0) === 0);
export const isCashSeriesEmpty = (points: CashPoint[]) => points.every((p) => p.receipts === 0 && p.payments === 0);

// The granularity of the charts of a tab. It starts at the default of the period and goes back to it whenever
// the period changes, also when an earlier period comes back. It is worked out during render, so no request
// is sent for a new period with the old granularity.
export function usePeriodGranularity(
  from: string,
  to: string,
): [SeriesGranularity, (granularity: SeriesGranularity) => void] {
  const period = `${from}|${to}`;
  const [choice, setChoice] = useState<{ period: string; granularity: SeriesGranularity } | null>(null);
  const current = choice?.period === period ? choice : null;
  // A choice of another period is dropped (a render-phase update: React renders again at once).
  if (choice !== null && current === null) setChoice(null);
  const granularity = current?.granularity ?? defaultGranularity(from, to);
  return [granularity, (next) => setChoice({ period, granularity: next })];
}
