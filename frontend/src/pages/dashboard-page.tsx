import { useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { KpiCard } from '@/features/dashboard/components/kpi-card';
import { RangePicker } from '@/features/dashboard/components/range-picker';
import { RevenueAreaChart } from '@/features/dashboard/components/revenue-area-chart';
import { StatusFunnel } from '@/features/dashboard/components/status-funnel';
import { TopListCard } from '@/features/dashboard/components/top-list-card';
import { ActivityTimeline } from '@/features/dashboard/components/activity-timeline';
import {
  useDashboardSummary,
  useRecentActivity,
  useRevenueSeries,
  useTopCustomers,
  useTopProducts,
} from '@/features/dashboard/hooks';
import { useDashboardParams } from '@/features/dashboard/use-dashboard-params';
import type { Granularity } from '@/features/dashboard/types';
import { formatNumber } from '@/features/dashboard/format';

export function DashboardPage() {
  const { from, to, setRange, setPreset } = useDashboardParams();
  const [granularity, setGranularity] = useState<Granularity>('day');

  const summary = useDashboardSummary({ from, to });
  const revenue = useRevenueSeries({ from, to, granularity });
  const topCustomers = useTopCustomers({ from, to, limit: 5 });
  const topProducts = useTopProducts({ from, to, limit: 5 });
  const activity = useRecentActivity({ limit: 8 });

  return (
    <div className="space-y-6">
      <header className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Tổng quan của tôi</h1>
          <p className="text-sm text-muted-foreground">
            {summary.data ? `Từ ${summary.data.from} đến ${summary.data.to}` : 'Báo giá trong khoảng đang xem.'}
          </p>
        </div>
        <RangePicker from={from} to={to} onChange={setRange} onPreset={setPreset} />
      </header>

      <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-4">
        <KpiCard
          label="Doanh thu hôm nay"
          icon="kpiRevenueToday"
          format="currency"
          kpi={summary.data?.todayRevenue}
          loading={summary.isLoading}
          accentColor="hsl(var(--primary))"
        />
        <KpiCard
          label="Doanh thu khoảng"
          icon="kpiRevenueRange"
          format="currency"
          kpi={summary.data?.rangeRevenue}
          loading={summary.isLoading}
          accentColor="hsl(var(--primary))"
        />
        <KpiCard
          label="Tổng báo giá"
          icon="kpiQuotations"
          format="number"
          kpi={summary.data?.totalCount}
          loading={summary.isLoading}
          accentColor="hsl(var(--primary))"
        />
        <KpiCard
          label="Đã huỷ"
          icon="kpiCancelled"
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
            <CardTitle>Hoạt động gần đây</CardTitle>
          </CardHeader>
          <CardContent>
            <ActivityTimeline items={activity.data ?? []} loading={activity.isLoading} />
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
                secondary: `${formatNumber(c.quotationCount)} BG`,
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
