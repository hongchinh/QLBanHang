// `toNumberOrZero`: '' / null / undefined / non-numeric become 0 (totals, sums).
export function toNumberOrZero(v: unknown): number {
  const n = typeof v === 'number' ? v : Number(v);
  return Number.isFinite(n) ? n : 0;
}

// `toOptionalNumber`: '' / null / undefined / non-numeric mean "no value" (undefined), for optional inputs.
export function toOptionalNumber(v: unknown): number | undefined {
  if (v === undefined || v === null || v === '') return undefined;
  const n = typeof v === 'number' ? v : Number(v);
  return Number.isFinite(n) ? n : undefined;
}
