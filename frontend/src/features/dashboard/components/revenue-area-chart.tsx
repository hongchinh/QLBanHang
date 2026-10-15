import { Area, AreaChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import { cn } from '@/lib/utils';
import type { Granularity, RevenuePoint } from '../types';
import { formatVnd, formatVndShort } from '../format';

interface RevenueAreaChartProps {
  points: Pick<RevenuePoint, 'date' | 'total'>[];
  granularity: Granularity;
  onGranularityChange: (g: Granularity) => void;
  loading?: boolean;
}

const GRAN_TABS: { key: Granularity; label: string }[] = [
  { key: 'day', label: 'Ngày' },
  { key: 'week', label: 'Tuần' },
  { key: 'month', label: 'Tháng' },
];

export function RevenueAreaChart({ points, granularity, onGranularityChange, loading }: RevenueAreaChartProps) {
  const empty = !loading && (points.length === 0 || points.every((p) => p.total === 0));
  const data = points.map((p) => ({ date: p.date, total: p.total }));

  return (
    <div className="space-y-3">
      <div className="flex items-center justify-end gap-1">
        {GRAN_TABS.map((t) => (
          <button
            key={t.key}
            type="button"
            onClick={() => onGranularityChange(t.key)}
            className={cn(
              'rounded-md border px-2 py-1 text-xs font-medium transition-colors',
              granularity === t.key
                ? 'border-foreground bg-foreground text-background'
                : 'border-border bg-card text-muted-foreground hover:bg-accent',
            )}
          >
            {t.label}
          </button>
        ))}
      </div>
      <div className="h-72 w-full">
        {loading ? (
          <div className="h-full w-full animate-pulse rounded bg-muted" />
        ) : empty ? (
          <div className="flex h-full w-full items-center justify-center text-sm text-muted-foreground">
            Chưa có dữ liệu
          </div>
        ) : (
          <ResponsiveContainer width="100%" height="100%">
            <AreaChart data={data} margin={{ top: 10, right: 16, bottom: 0, left: 0 }}>
              <defs>
                <linearGradient id="revenue-gradient" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="0%" stopColor="hsl(var(--primary))" stopOpacity={0.4} />
                  <stop offset="100%" stopColor="hsl(var(--primary))" stopOpacity={0} />
                </linearGradient>
              </defs>
              <CartesianGrid strokeDasharray="3 3" stroke="#F0F0F0" />
              <XAxis dataKey="date" tick={{ fontSize: 11 }} stroke="#9CA3AF" />
              <YAxis
                tick={{ fontSize: 11 }}
                stroke="#9CA3AF"
                tickFormatter={(v: number) => formatVndShort(v)}
                width={72}
              />
              <Tooltip
                formatter={(v: number) => [formatVnd(v), 'Doanh thu']}
                contentStyle={{ fontSize: 12, borderRadius: 8 }}
                labelStyle={{ color: '#6B7280' }}
              />
              <Area
                type="monotone"
                dataKey="total"
                stroke="hsl(var(--primary))"
                strokeWidth={2}
                fill="url(#revenue-gradient)"
                isAnimationActive={false}
              />
            </AreaChart>
          </ResponsiveContainer>
        )}
      </div>
    </div>
  );
}
