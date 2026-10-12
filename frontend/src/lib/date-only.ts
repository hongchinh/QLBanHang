// `yyyy-MM-dd` (DateOnly) → `dd/MM/yyyy`, without going through Date (no time-zone shift).
export function formatDateOnly(value?: string | null): string {
  if (!value) return '';
  const [y, m, d] = value.slice(0, 10).split('-');
  return `${d}/${m}/${y}`;
}
