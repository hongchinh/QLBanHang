import { useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Bar,
  BarChart,
  CartesianGrid,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Button } from '@/components/ui/button';
import { AppIcon } from '@/components/icons';
import { cn } from '@/lib/utils';
import { RangePicker } from '@/features/dashboard/components/range-picker';
import { useLeaderboard } from '@/features/dashboard/hooks';
import { useDashboardParams } from '@/features/dashboard/use-dashboard-params';
import type { SalesLeaderboardItem } from '@/features/dashboard/types';
import { DeltaArrow } from '@/features/dashboard/components/delta-arrow';
import { formatDelta, formatNumber, formatVnd, formatVndShort } from '@/features/dashboard/format';

type SortKey = 'rank' | 'revenue' | 'confirmedCount' | 'conversionRate' | 'deltaPct';

export function SalesPerformancePage() {
  const { from, to, setRange, setPreset } = useDashboardParams();
  const [sortKey, setSortKey] = useState<SortKey>('revenue');
  const [sortDesc, setSortDesc] = useState(true);
  const navigate = useNavigate();

  const leaderboard = useLeaderboard({ from, to, limit: 50 });

  const sorted = useMemo(() => {
    const items = [...(leaderboard.data ?? [])];
    items.sort((a, b) => compareLeaderboard(a, b, sortKey));
    if (!sortDesc) items.reverse();
    return items;
  }, [leaderboard.data, sortKey, sortDesc]);

  const top10 = useMemo(() => [...(leaderboard.data ?? [])].sort((a, b) => b.revenue - a.revenue).slice(0, 10), [leaderboard.data]);

  const toggleSort = (key: SortKey) => {
    if (sortKey === key) setSortDesc((v) => !v);
    else {
      setSortKey(key);
      setSortDesc(true);
    }
  };

  const sortIcon = (key: SortKey) =>
    sortKey === key ? (sortDesc ? <AppIcon name="sortDesc" size={12} className="ml-1 inline" /> : <AppIcon name="sortAsc" size={12} className="ml-1 inline" />) : null;

  return (
    <div className="space-y-6">
      <header className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Hiệu suất sale</h1>
          <p className="text-sm text-muted-foreground">Từ {from} đến {to}</p>
        </div>
        <RangePicker from={from} to={to} onChange={setRange} onPreset={setPreset} />
      </header>

      <Card>
        <CardHeader>
          <CardTitle>Bảng xếp hạng</CardTitle>
        </CardHeader>
        <CardContent>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead className="w-12">#</TableHead>
                <TableHead>Sale</TableHead>
                <TableHead className="cursor-pointer text-right" onClick={() => toggleSort('confirmedCount')}>
                  Số BG xác nhận{sortIcon('confirmedCount')}
                </TableHead>
                <TableHead className="cursor-pointer text-right" onClick={() => toggleSort('revenue')}>
                  Doanh thu{sortIcon('revenue')}
                </TableHead>
                <TableHead className="cursor-pointer text-right" onClick={() => toggleSort('conversionRate')}>
                  Conversion{sortIcon('conversionRate')}
                </TableHead>
                <TableHead className="cursor-pointer text-right" onClick={() => toggleSort('deltaPct')}>
                  Δ kỳ trước{sortIcon('deltaPct')}
                </TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {leaderboard.isLoading && (
                <TableRow>
                  <TableCell colSpan={6} className="text-center text-sm text-muted-foreground">
                    Đang tải…
                  </TableCell>
                </TableRow>
              )}
              {!leaderboard.isLoading && sorted.length === 0 && (
                <TableRow>
                  <TableCell colSpan={6} className="text-center text-sm text-muted-foreground">
                    Chưa có dữ liệu.
                  </TableCell>
                </TableRow>
              )}
              {sorted.map((it, idx) => {
                const delta = formatDelta(it.deltaPct);
                return (
                  <TableRow
                    key={it.userId}
                    className="cursor-pointer"
                    onClick={() => navigate(`/reports/revenue?saleUserId=${it.userId}&from=${from}&to=${to}`)}
                  >
                    <TableCell className="font-semibold text-muted-foreground">{idx + 1}</TableCell>
                    <TableCell className="font-medium">{it.fullName}</TableCell>
                    <TableCell className="text-right tabular-nums">{formatNumber(it.confirmedCount)}</TableCell>
                    <TableCell className="text-right tabular-nums">{formatVnd(it.revenue)}</TableCell>
                    <TableCell className="text-right tabular-nums">
                      {it.conversionRate != null ? `${it.conversionRate.toFixed(1)}%` : '—'}
                    </TableCell>
                    <TableCell
                      className={cn(
                        'text-right tabular-nums',
                        delta.tone === 'positive' && 'text-emerald-600',
                        delta.tone === 'negative' && 'text-rose-600',
                      )}
                    >
                      <span className="inline-flex items-center gap-0.5">
                        {delta.direction && <DeltaArrow direction={delta.direction} />}
                        {delta.text}
                      </span>
                    </TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      <div className="grid gap-4 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>Top 10 doanh thu</CardTitle>
          </CardHeader>
          <CardContent>
            <SalesBarChart data={top10} dataKey="revenue" formatter={formatVndShort} tooltipFormatter={formatVnd} />
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle>Top 10 tỉ lệ chuyển đổi</CardTitle>
          </CardHeader>
          <CardContent>
            <SalesBarChart
              data={[...top10].sort((a, b) => (b.conversionRate ?? 0) - (a.conversionRate ?? 0))}
              dataKey="conversionRate"
              formatter={(v: number) => `${v.toFixed(1)}%`}
              tooltipFormatter={(v: number) => `${v.toFixed(1)}%`}
            />
          </CardContent>
        </Card>
      </div>
      <div className="flex justify-end">
        <Button variant="outline" size="sm" onClick={() => navigate('/reports/revenue')}>
          Xem chi tiết doanh thu
        </Button>
      </div>
    </div>
  );
}

function SalesBarChart({
  data,
  dataKey,
  formatter,
  tooltipFormatter,
}: {
  data: SalesLeaderboardItem[];
  dataKey: 'revenue' | 'conversionRate';
  formatter: (v: number) => string;
  tooltipFormatter: (v: number) => string;
}) {
  const rows = data.map((d) => ({
    name: d.fullName,
    value: dataKey === 'conversionRate' ? (d.conversionRate ?? 0) : d.revenue,
  }));
  return (
    <div className="h-72 w-full">
      <ResponsiveContainer width="100%" height="100%">
        <BarChart data={rows} layout="vertical" margin={{ top: 4, right: 12, bottom: 4, left: 12 }}>
          <CartesianGrid strokeDasharray="3 3" stroke="#F0F0F0" horizontal={false} />
          <XAxis type="number" tick={{ fontSize: 11 }} tickFormatter={formatter} stroke="#9CA3AF" />
          <YAxis type="category" dataKey="name" width={120} tick={{ fontSize: 11 }} stroke="#9CA3AF" />
          <Tooltip formatter={(v: number) => tooltipFormatter(v)} contentStyle={{ fontSize: 12, borderRadius: 8 }} />
          <Bar dataKey="value" fill="hsl(var(--primary))" radius={[0, 4, 4, 0]} isAnimationActive={false} />
        </BarChart>
      </ResponsiveContainer>
    </div>
  );
}

function compareLeaderboard(a: SalesLeaderboardItem, b: SalesLeaderboardItem, key: SortKey): number {
  switch (key) {
    case 'revenue':
      return b.revenue - a.revenue;
    case 'confirmedCount':
      return b.confirmedCount - a.confirmedCount;
    case 'conversionRate':
      return (b.conversionRate ?? -Infinity) - (a.conversionRate ?? -Infinity);
    case 'deltaPct':
      return (b.deltaPct ?? -Infinity) - (a.deltaPct ?? -Infinity);
    case 'rank':
    default:
      return b.revenue - a.revenue;
  }
}
