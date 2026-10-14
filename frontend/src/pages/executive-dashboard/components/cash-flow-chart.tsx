import { Bar, BarChart, CartesianGrid, Legend, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import { formatVnd, formatVndCompact } from '@/features/dashboard/format';
import { bucketLabel, seriesSpan } from '@/features/executive-dashboard/period';
import type { CashPoint, SeriesGranularity } from '@/features/executive-dashboard/types';
import { GranularityToggle } from './granularity-toggle';

interface CashFlowChartProps {
  points: CashPoint[];
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

const total = (values: number[]) => values.reduce((sum, value) => sum + value, 0);

// The text alternative of the chart: by which bucket, over which buckets, and the totals.
function chartText(points: CashPoint[], granularity: SeriesGranularity): string {
  const receipts = formatVnd(total(points.map((p) => p.receipts)));
  const payments = formatVnd(total(points.map((p) => p.payments)));
  return `Thu và chi ${seriesSpan(points.map((p) => p.date), granularity)}: thu ${receipts}, chi ${payments}.`;
}

// Receipts and payments per bucket.
export function CashFlowChart({
  points,
  granularity,
  labelGranularity = granularity,
  onGranularityChange,
  dayDisabled,
  height = 160,
}: CashFlowChartProps) {
  const label = (date: string) => bucketLabel(date, labelGranularity);
  return (
    <div className="space-y-3">
      {onGranularityChange && (
        <GranularityToggle value={granularity} onChange={onGranularityChange} dayDisabled={dayDisabled} />
      )}
      <div className="w-full" style={{ height }} role="img" aria-label={chartText(points, labelGranularity)}>
        <ResponsiveContainer width="100%" height="100%">
          <BarChart data={points} margin={{ top: 10, right: 16, bottom: 0, left: 0 }}>
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
            <Bar dataKey="receipts" name="Thu" fill="#16a34a" isAnimationActive={false} />
            <Bar dataKey="payments" name="Chi" fill="#dc2626" isAnimationActive={false} />
          </BarChart>
        </ResponsiveContainer>
      </div>
    </div>
  );
}
