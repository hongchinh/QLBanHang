export function formatVnd(value: number): string {
  return new Intl.NumberFormat('vi-VN', {
    style: 'currency',
    currency: 'VND',
    maximumFractionDigits: 0,
  }).format(value);
}

export function formatVndShort(value: number): string {
  const abs = Math.abs(value);
  if (abs >= 1_000_000_000) return `₫${(value / 1_000_000_000).toFixed(1)}B`;
  if (abs >= 1_000_000) return `₫${(value / 1_000_000).toFixed(1)}M`;
  if (abs >= 1_000) return `₫${(value / 1_000).toFixed(1)}K`;
  return `₫${value}`;
}

export function formatNumber(value: number): string {
  return new Intl.NumberFormat('vi-VN').format(value);
}

function formatDecimal(value: number, minDigits: number, maxDigits: number): string {
  return new Intl.NumberFormat('vi-VN', {
    minimumFractionDigits: minDigits,
    maximumFractionDigits: maxDigits,
  }).format(value);
}

// Money on KPI tiles: "2,84 tỷ", "412 tr", "45,5 tr"; the full number below one million.
// The unit is chosen after rounding to 0,1 tr, so 999.960.000 prints "1,00 tỷ", never "1.000 tr".
export function formatVndCompact(value: number): string {
  const abs = Math.abs(value);
  if (abs < 1_000_000) return formatNumber(value);
  const sign = value < 0 ? '-' : '';
  const millions = Math.round(abs / 100_000) / 10;
  if (millions < 1_000) return `${sign}${formatDecimal(millions, 0, 1)} tr`;
  return `${sign}${formatDecimal(abs / 1_000_000_000, 2, 2)} tỷ`;
}

// formatVndCompact with "+" in front of a positive value: "+186 tr", "-700.000", "0".
export function formatSignedCompact(value: number): string {
  return value > 0 ? `+${formatVndCompact(value)}` : formatVndCompact(value);
}

// A percentage with a decimal comma: 14.5 → "14,5%", 100 → "100%"; "—" without a value.
export function formatPct(value: number | null | undefined, digits = 1): string {
  if (value == null) return '—';
  return `${formatDecimal(value, 0, digits)}%`;
}

export function formatDelta(deltaPct: number | null | undefined): {
  text: string;
  tone: 'positive' | 'negative' | 'neutral';
  direction: 'up' | 'down' | null;
} {
  if (deltaPct == null || !Number.isFinite(deltaPct)) {
    return { text: '—', tone: 'neutral', direction: null };
  }
  return {
    text: `${Math.abs(deltaPct).toFixed(1)}%`,
    tone: deltaPct >= 0 ? 'positive' : 'negative',
    direction: deltaPct >= 0 ? 'up' : 'down',
  };
}

export function formatDateYmd(d: Date): string {
  const yyyy = d.getFullYear();
  const mm = String(d.getMonth() + 1).padStart(2, '0');
  const dd = String(d.getDate()).padStart(2, '0');
  return `${yyyy}-${mm}-${dd}`;
}
