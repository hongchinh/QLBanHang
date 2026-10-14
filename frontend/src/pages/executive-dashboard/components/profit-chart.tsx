import { Bar, CartesianGrid, ComposedChart, Legend, Line, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import { formatVnd, formatVndCompact } from '@/features/dashboard/format';
import { bucketLabel, seriesSpan } from '@/features/executive-dashboard/period';
import type { SalesPoint, SeriesGranularity } from '@/features/executive-dashboard/types';
import { GranularityToggle } from './granularity-toggle';

interface ProfitChartProps {
  points: SalesPoint[];
  // Without inventory.view_cost the server sends no cost: only the revenue is drawn.
  canViewCost: boolean;
  // The chosen granularity (the toggle).
  granularity: SeriesGranularity;
  // The granularity of the points on screen, which label the axis (default: granularity). While another
  // granularity loads, the previous points stay on screen and keep their own labels.
  labelGranularity?: SeriesGranularity;
  // The toggle is shown only with a handler.
  onGranularityChange?(granularity: SeriesGranularity): void;
  dayDisabled?: boolean;
  height?: number;
}

const total = (values: (number | null)[]) => values.reduce<number>((sum, value) => sum + (value ?? 0), 0);

// The text alternative of the chart: what it draws, by which bucket, over which buckets, and the totals.
function chartText(points: SalesPoint[], canViewCost: boolean, granularity: SeriesGranularity): string {
  const span = seriesSpan(points.map((p) => p.date), granularity);
  const revenue = `doanh thu ${formatVnd(total(points.map((p) => p.revenue)))}`;
  if (!canViewCost) return `Doanh thu ${span}: ${revenue}.`;
  const cost = formatVnd(total(points.map((p) => p.cost)));
  const grossProfit = formatVnd(total(points.map((p) => p.grossProfit)));
  return `Doanh thu, giá vốn và lãi gộp ${span}: ${revenue}, giá vốn ${cost}, lãi gộp ${grossProfit}.`;
}

// Revenue and cost bars with the gross profit line, per bucket. The first week or month bucket can start
// before the period, and a period of returns only can have a positive margin: both are shown as the server
// sends them.
export function ProfitChart({
  points,
  canViewCost,
  granularity,
  labelGranularity = granularity,
  onGranularityChange,
  dayDisabled,
  height = 240,
}: ProfitChartProps) {
  const label = (date: string) => bucketLabel(date, labelGranularity);
  return (
    <div className="space-y-3">
      {onGranularityChange && (
        <GranularityToggle value={granularity} onChange={onGranularityChange} dayDisabled={dayDisabled} />
      )}
      <div className="w-full" style={{ height }} role="img" aria-label={chartText(points, canViewCost, labelGranularity)}>
        <ResponsiveContainer width="100%" height="100%">
          <ComposedChart data={points} margin={{ top: 10, right: 16, bottom: 0, left: 0 }}>
            <CartesianGrid strokeDasharray="3 3" stroke="#F0F0F0" />
            <XAxis dataKey="date" tickFormatter={label} tick={{ fontSize: 11 }} stroke="#9CA3AF" />
            <YAxis
              tick={{ fontSize: 11 }}
              stroke="#9CA3AF"
              tickFormatter={(value: number) => formatVndCompact(value)}
              width={72}
            />
            <Tooltip
              formatter={(value: number, name: string) => [formatVnd(value), name]}
              labelFormatter={(date: string) => label(date)}
              contentStyle={{ fontSize: 12, borderRadius: 8 }}
              labelStyle={{ color: '#6B7280' }}
            />
            <Legend wrapperStyle={{ fontSize: 12 }} />
            <Bar dataKey="revenue" name="Doanh thu" fill="hsl(var(--primary))" isAnimationActive={false} />
            {canViewCost && <Bar dataKey="cost" name="Giá vốn" fill="#94a3b8" isAnimationActive={false} />}
            {canViewCost && (
              <Line
                type="monotone"
                dataKey="grossProfit"
                name="Lãi gộp"
                stroke="#16a34a"
                strokeWidth={2}
                dot={false}
                isAnimationActive={false}
              />
            )}
          </ComposedChart>
        </ResponsiveContainer>
      </div>
    </div>
  );
}
