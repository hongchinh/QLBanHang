import type { AgingBucket } from './types';

export const DEFAULT_BUCKETS = '30,60,90';

const MAX_BOUNDARIES = 6;
// Mirrors DebtAging.MaxBoundaryDays (prevents int overflow in bucket ranges).
const MAX_BOUNDARY_DAYS = 36500;

/** Mirrors DebtAging.ParseBoundaries: "15, 45" -> "15,45"; null when invalid. Blank -> DEFAULT_BUCKETS. */
export function normalizeBuckets(input: string): string | null {
  if (input.trim() === '') return DEFAULT_BUCKETS;
  const parts = input.split(',').map((p) => p.trim());
  if (parts.length > MAX_BOUNDARIES) return null;
  const values: number[] = [];
  for (const part of parts) {
    if (!/^[0-9]+$/.test(part)) return null;
    const value = Number(part);
    if (!Number.isSafeInteger(value) || value < 1 || value > MAX_BOUNDARY_DAYS) return null;
    if (values.length > 0 && value <= values[values.length - 1]) return null;
    values.push(value);
  }
  return values.join(',');
}

/** "0–30", "31–60", "> 90" (en dash; open-ended bucket "> {from - 1}"). */
export function bucketLabel(bucket: AgingBucket): string {
  if (bucket.to == null) return `> ${bucket.from - 1}`;
  return `${bucket.from}–${bucket.to}`;
}
