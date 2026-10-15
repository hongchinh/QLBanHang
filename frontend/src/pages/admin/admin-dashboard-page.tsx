import { useMemo, useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { KpiCard } from '@/features/dashboard/components/kpi-card';
import { RangePicker } from '@/features/dashboard/components/range-picker';
import { RevenueAreaChart } from '@/features/dashboard/components/revenue-area-chart';
import { StatusFunnel } from '@/features/dashboard/components/status-funnel';
import { TopListCard } from '@/features/dashboard/components/top-list-card';
import { SalesLeaderboard } from '@/features/dashboard/components/sales-leaderboard';
import {
  useDashboardSummary,
  useLeaderboard,
  useRevenueSeries,
  useTopCustomers,
  useTopProducts,
} from '@/features/dashboard/hooks';
import { useDashboardParams } from '@/features/dashboard/use-dashboard-params';
import { useAdminUsers } from '@/features/admin-users/hooks';
import type { Granularity } from '@/features/dashboard/types';
import { formatNumber } from '@/features/dashboard/format';

const ALL_SALES = '__all__';

export function AdminDashboardPage() {
  const { from, to, saleUserId, setRange, setSaleUserId, setPreset } = useDashboardParams();
  const [granularity, setGranularity] = useState<Granularity>('day');

  const summary = useDashboardSummary({ from, to, saleUserId });
  const revenue = useRevenueSeries({ from, to, granularity, saleUserId });
  const topCustomers = useTopCustomers({ from, to, limit: 5, saleUserId });
  const topProducts = useTopProducts({ from, to, limit: 5, saleUserId });
  const leaderboard = useLeaderboard({ from, to, limit: 5 });
  const usersQuery = useAdminUsers({ activeOnly: true });

  const salesOptions = useMemo(
    () => (usersQuery.data ?? []).filter((u) => u.isActive),
    [usersQuery.data],
  );

  return (
    <div className="space-y-6">
      <header className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Tổng quan hệ thống</h1>
          <p className="text-sm text-muted-foreground">
            {summary.data ? `Từ ${summary.data.from} đến ${summary.data.to}` : 'Báo giá trong khoảng đang xem.'}
          </p>
        </div>
        <div className="flex flex-wrap items-center gap-3">
          <Select
            value={saleUserId ?? ALL_SALES}
            onValueChange={(v) => setSaleUserId(v === ALL_SALES ? undefined : v)}
          >
            <SelectTrigger className="w-56">
              <SelectValue placeholder="Tất cả sale" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value={ALL_SALES}>Tất cả sale</SelectItem>
              {salesOptions.map((u) => (
                <SelectItem key={u.id} value={u.id}>
                  {u.fullName}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          <RangePicker from={from} to={to} onChange={setRange} onPreset={setPreset} />
        </div>
      </header>

      <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-4">
        <KpiCard
          label="Doanh thu hôm nay"
          format="currency"
          kpi={summary.data?.todayRevenue}
          loading={summary.isLoading}
          accentColor="hsl(var(--primary))"
        />
        <KpiCard
          label="Doanh thu khoảng"
          format="currency"
          kpi={summary.data?.rangeRevenue}
          loading={summary.isLoading}
          accentColor="hsl(var(--primary))"
        />
        <KpiCard
          label="Tổng báo giá"
          format="number"
          kpi={summary.data?.totalCount}
          loading={summary.isLoading}
          accentColor="hsl(var(--primary))"
        />
        <KpiCard
          label="Đã huỷ"
          format="number"
          kpi={summary.data?.cancelledCount}
          loading={summary.isLoading}
        />
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Doanh thu</CardTitle>
        </CardHeader>
        <CardContent>
          <RevenueAreaChart
            points={revenue.data?.points ?? []}
            granularity={granularity}
            onGranularityChange={setGranularity}
            loading={revenue.isLoading}
          />
        </CardContent>
      </Card>

      <div className="grid gap-4 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>Phễu trạng thái</CardTitle>
          </CardHeader>
          <CardContent>
            <StatusFunnel data={summary.data?.funnel} loading={summary.isLoading} />
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle>Bảng xếp hạng sale</CardTitle>
          </CardHeader>
          <CardContent>
            <SalesLeaderboard
              items={leaderboard.data ?? []}
              loading={leaderboard.isLoading}
              drillHref={(userId) => `/reports/revenue?saleUserId=${userId}&from=${from}&to=${to}`}
            />
          </CardContent>
        </Card>
      </div>

      <div className="grid gap-4 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>Top khách hàng</CardTitle>
          </CardHeader>
          <CardContent>
            <TopListCard
              format="currency"
              loading={topCustomers.isLoading}
              items={(topCustomers.data ?? []).map((c) => ({
                id: c.customerId,
                name: c.customerName,
                primary: c.revenue,
                secondary: `${formatNumber(c.voucherCount)} phiếu`,
              }))}
            />
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle>Top sản phẩm</CardTitle>
          </CardHeader>
          <CardContent>
            <TopListCard
              format="currency"
              loading={topProducts.isLoading}
              items={(topProducts.data ?? []).map((p, i) => ({
                id: p.productId ?? `idx-${i}`,
                name: p.productName,
                primary: p.revenue,
                secondary: `${formatNumber(p.quantity)} đơn vị`,
              }))}
            />
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
