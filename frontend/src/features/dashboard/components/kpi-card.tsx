import { Area, AreaChart, ResponsiveContainer, Tooltip } from 'recharts';
import { cn } from '@/lib/utils';
import { AppIcon, type IconName } from '@/components/icons';
import type { Kpi } from '../types';
import { DeltaArrow } from './delta-arrow';
import { formatDelta, formatNumber, formatVnd } from '../format';

interface KpiCardProps {
  label: string;
  format?: 'currency' | 'number';
  kpi?: Kpi;
  loading?: boolean;
  accentColor?: string;
  icon?: IconName;
}

export function KpiCard({ label, format = 'number', kpi, loading, accentColor, icon }: KpiCardProps) {
  const value = kpi?.value ?? 0;
  const display = format === 'currency' ? formatVnd(value) : formatNumber(value);
  const delta = formatDelta(kpi?.deltaPct);
  const sparkData = (kpi?.spark ?? []).map((v, i) => ({ i, v }));
  const gradientId = `kpi-spark-${label.replace(/[^a-zA-Z0-9]/g, '')}`;

  return (
    <div
      className="rounded-xl border border-border bg-card p-5 shadow-sm"
      style={accentColor ? { color: accentColor } : undefined}
    >
      <div className="flex items-center justify-between gap-2">
        <p className="text-xs uppercase tracking-wide text-muted-foreground">{label}</p>
        {icon && <AppIcon name={icon} size={18} className="text-muted-foreground" tone="none" />}
      </div>
      <div className="mt-2 flex items-baseline justify-between gap-3">
        <div className="text-3xl font-semibold tracking-tight tabular-nums text-foreground">
          {loading ? <span className="inline-block h-7 w-24 animate-pulse rounded bg-muted" /> : display}
        </div>
        <DeltaBadge tone={delta.tone} text={delta.text} direction={delta.direction} />
      </div>
      {sparkData.length > 0 && (
        <div className="mt-3 h-16">
          <ResponsiveContainer width="100%" height="100%">
            <AreaChart data={sparkData} margin={{ top: 0, right: 0, bottom: 0, left: 0 }}>
              <defs>
                <linearGradient id={gradientId} x1="0" y1="0" x2="0" y2="1">
                  <stop offset="0%" stopColor="currentColor" stopOpacity={0.3} />
                  <stop offset="100%" stopColor="currentColor" stopOpacity={0} />
                </linearGradient>
              </defs>
              <Area
                type="monotone"
                dataKey="v"
                stroke="currentColor"
                strokeWidth={1.5}
                fill={`url(#${gradientId})`}
                isAnimationActive={false}
              />
              <Tooltip content={() => null} cursor={false} />
            </AreaChart>
          </ResponsiveContainer>
        </div>
      )}
    </div>
  );
}

function DeltaBadge({
  tone,
  text,
  direction,
}: {
  tone: 'positive' | 'negative' | 'neutral';
  text: string;
  direction: 'up' | 'down' | null;
}) {
  return (
    <span
      className={cn(
        'inline-flex items-center gap-0.5 rounded-full px-2 py-0.5 text-xs font-medium',
        tone === 'positive' && 'bg-emerald-50 text-emerald-700',
        tone === 'negative' && 'bg-rose-50 text-rose-700',
        tone === 'neutral' && 'bg-muted text-muted-foreground',
      )}
    >
      {direction && <DeltaArrow direction={direction} />}
      {text}
    </span>
  );
}
