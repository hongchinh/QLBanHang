// Stock quantities are stored with 6 decimals (numeric(18,6)); 0.003659 m³ must not show as 0.00.
// Same comma-group / dot-decimal style as the quotation quantity column.
const stockQuantityFmt = new Intl.NumberFormat('en-US', { maximumFractionDigits: 6 });

export function formatStockQuantity(value: number | undefined | null): string {
  if (value == null || !Number.isFinite(value)) return '';
  return stockQuantityFmt.format(value);
}
