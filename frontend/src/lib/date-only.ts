// Input must be a DateOnly string (`yyyy-MM-dd`, a time part is ignored); an instant (e.g. ISO with `Z`) must go through `lib/vn-datetime`.
// `yyyy-MM-dd` → `dd/MM/yyyy`, without going through Date (no time-zone shift).
export function formatDateOnly(value?: string | null): string {
  if (!value) return '';
  const [y, m, d] = value.slice(0, 10).split('-');
  return `${d}/${m}/${y}`;
}
