const fmt = new Intl.NumberFormat('vi-VN');

export function parseMoneyInput(text: string): number | undefined {
  const trimmed = text.trim();
  if (trimmed === '') return undefined;
  const normalized = trimmed.includes(',')
    ? trimmed.replace(/\./g, '').replace(',', '.')
    : /^-?\d{1,3}(\.\d{3})+$/.test(trimmed)
    ? trimmed.replace(/\./g, '')
    : trimmed;
  const n = Number(normalized);
  return Number.isFinite(n) ? n : undefined;
}

export function formatMoneyForDisplay(value: unknown): string {
  if (value === undefined || value === null || value === '') return '';
  const n = typeof value === 'number' ? value : Number(value);
  return Number.isFinite(n) ? fmt.format(n) : '';
}
