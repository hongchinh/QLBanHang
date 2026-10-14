import { useEffect, useMemo, useRef, useState } from 'react';
import { AppIcon } from '@/components/icons';
import { ButtonLoader } from '@/components/ui/button-loader';
import { useSearchParams } from 'react-router-dom';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Table, TableBody, TableCell, TableFooter, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { toast } from '@/lib/use-toast';
import { useAuthStore } from '@/stores/auth-store';
import { KpiCard } from '@/features/dashboard/components/kpi-card';
import { RangePicker } from '@/features/dashboard/components/range-picker';
import { RevenueAreaChart } from '@/features/dashboard/components/revenue-area-chart';
import { TopListCard } from '@/features/dashboard/components/top-list-card';
import {
  useDashboardSummary,
  useRevenueSeries,
  useTopCustomers,
} from '@/features/dashboard/hooks';
import { useDashboardParams } from '@/features/dashboard/use-dashboard-params';
import { useQuotationOwners } from '@/features/quotations/hooks';
import { salesRevenueDetailApi } from '@/features/reports/sales-revenue-detail/api';
import { useRevenueLineItems } from '@/features/reports/sales-revenue-detail/hooks';
import type { SalesRevenueLineItemDto } from '@/features/reports/sales-revenue-detail/types';
import type { Granularity, Kpi } from '@/features/dashboard/types';
import { formatDateYmd, formatNumber, formatVnd } from '@/features/dashboard/format';

const ALL_SALES = '__all__';
const moneyNumber = new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 0 });
const decimalNumber = new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 2 });

function formatDate(value: string | null | undefined): string {
  if (!value) return '';
  const datePart = value.slice(0, 10);
  const [year, month, day] = datePart.split('-');
  return year && month && day ? `${day}/${month}` : datePart;
}

function formatNullableNumber(value: number | null | undefined): string {
  if (value === null || value === undefined) return '';
  return decimalNumber.format(value);
}

function formatMoneyNumber(value: number | null | undefined): string {
  if (value === null || value === undefined) return '';
  return moneyNumber.format(value);
}

function formatProductSize(item: SalesRevenueLineItemDto): string {
  const dimensions = [item.length, item.width, item.thickness]
    .filter((value): value is number => value !== null && value !== undefined)
    .map((value) => decimalNumber.format(value));

  if (dimensions.length > 0) return dimensions.join(' x ');
  return item.specification || item.productName;
}

export function RevenuePage() {
  const { from, to, saleUserId, setRange, setSaleUserId, setPreset } = useDashboardParams();
  const [granularity, setGranularity] = useState<Granularity>('day');
  const [isExporting, setIsExporting] = useState(false);
  const isAdmin = useAuthStore((s) => s.hasPermission('quotations.view_all'));
  const currentUser = useAuthStore((s) => s.user);
  const [searchParams] = useSearchParams();

  const didInitDate = useRef(false);
  useEffect(() => {
    if (!didInitDate.current && !searchParams.has('from')) {
      didInitDate.current = true;
      const today = formatDateYmd(new Date());
      setRange(today, today);
    }
  }, []); // eslint-disable-line react-hooks/exhaustive-deps

  const didInit = useRef(false);
  useEffect(() => {
    if (!didInit.current && currentUser?.id && !saleUserId) {
      didInit.current = true;
      setSaleUserId(currentUser.id);
    }
  }, [currentUser?.id, saleUserId, setSaleUserId]);

  async function handleExportExcel() {
    if (!from || !to) return;
    setIsExporting(true);
    try {
      await salesRevenueDetailApi.downloadRevenueExcel({ from, to, saleUserId });
    } catch {
      toast({ title: 'Lỗi', description: 'Không thể xuất Excel. Vui lòng thử lại.', variant: 'destructive' });
    } finally {
      setIsExporting(false);
    }
  }

  const summary = useDashboardSummary({ from, to, saleUserId });
  const revenue = useRevenueSeries({ from, to, granularity, saleUserId });
  const topCustomers = useTopCustomers({ from, to, limit: 5, saleUserId });
  const detailLines = useRevenueLineItems({ from, to, saleUserId });
  const ownersQuery = useQuotationOwners({ enabled: isAdmin });

  const rangeRevenue: Kpi | undefined = summary.data?.rangeRevenue;
  const totalCount = summary.data?.totalCount.value ?? 0;
  const cancelledCount = summary.data?.cancelledCount.value ?? 0;
  const confirmedCount = Math.max(0, Math.round(totalCount - cancelledCount));
  const avgPerQuote = confirmedCount > 0 && rangeRevenue ? rangeRevenue.value / confirmedCount : 0;
  const detailItems = useMemo(() => detailLines.data ?? [], [detailLines.data]);
  const hasCostColumns = detailItems.some((item) => item.unitCost !== null || item.lineCost !== null || item.lineProfit !== null);
  const detailTotals = useMemo(() => {
    return detailItems.reduce(
      (acc, item) => {
        acc.quantity += item.quantity;
        acc.sheetCount += item.sheetCount ?? 0;
        acc.lineTotal += item.lineTotal;
        if (item.isFirstLineOfQuotation) {
          acc.freight += item.freight;
          acc.taxAmount += item.taxAmount;
          acc.total += item.total;
        }
        acc.lineCost += item.lineCost ?? 0;
        acc.lineProfit += item.lineProfit ?? 0;
        return acc;
      },
      { quantity: 0, sheetCount: 0, lineTotal: 0, freight: 0, taxAmount: 0, total: 0, lineCost: 0, lineProfit: 0 },
    );
  }, [detailItems]);

  return (
    <div className="space-y-6">
      <header className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">
            {!isAdmin && currentUser ? `Báo cáo doanh thu của ${currentUser.fullName}` : 'Báo cáo doanh thu'}
          </h1>
          <p className="text-sm text-muted-foreground">
            {summary.data ? `Từ ${summary.data.from} đến ${summary.data.to}` : 'Báo giá xác nhận trong khoảng đang xem.'}
          </p>
        </div>
        <div className="flex flex-wrap items-center gap-3">
          {isAdmin && (
            <Select
              value={saleUserId ?? ALL_SALES}
              onValueChange={(v) => setSaleUserId(v === ALL_SALES ? undefined : v)}
            >
              <SelectTrigger className="w-56">
                <SelectValue placeholder="Tất cả sale" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={ALL_SALES}>Tất cả sale</SelectItem>
                {(ownersQuery.data ?? []).map((u) => (
                  <SelectItem key={u.id} value={u.id}>
                    {u.isDeleted ? `${u.fullName} (đã nghỉ)` : u.fullName}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          )}
          <RangePicker from={from} to={to} onChange={setRange} onPreset={setPreset} />
          <Button
            variant="outline"
            size="sm"
            onClick={handleExportExcel}
            disabled={isExporting || !from || !to}
          >
            {isExporting ? <ButtonLoader className="mr-2" /> : <AppIcon name="exportExcel" className="mr-2" />}
            Xuất Excel
          </Button>
        </div>
      </header>

      <Card>
        <CardHeader>
          <CardTitle>Chi tiết doanh thu</CardTitle>
        </CardHeader>
        <CardContent>
          {detailLines.isLoading ? (
            <div className="h-80 w-full animate-pulse rounded bg-muted" />
          ) : detailLines.isError ? (
            <div className="text-sm text-destructive">Không tải được chi tiết doanh thu.</div>
          ) : detailItems.length === 0 ? (
            <div className="rounded-md border border-dashed p-6 text-center text-sm text-muted-foreground">
              Không có dòng hàng nào trong khoảng thời gian này.
            </div>
          ) : (
            <div className="max-h-[calc(100dvh-16rem)] min-h-[300px] overflow-auto rounded-md border">
              <Table
                containerClassName="overflow-visible"
                className="min-w-[1280px] [&_td:not(:last-child)]:border-r [&_td:not(:last-child)]:border-border/40 [&_th:not(:last-child)]:border-r [&_th:not(:last-child)]:border-white/20"
              >
                <TableHeader className="sticky top-0 z-10">
                  <TableRow>
                    <TableHead>Ngày lập BG</TableHead>
                    <TableHead>Số BG</TableHead>
                    <TableHead>Ngày tính DT</TableHead>
                    <TableHead>Tên đơn vị</TableHead>
                    <TableHead>Địa chỉ giao hàng</TableHead>
                    <TableHead>Hàng hóa / kích thước</TableHead>
                    <TableHead className="text-right">Tỷ trọng</TableHead>
                    <TableHead className="text-right">SL m²</TableHead>
                    <TableHead className="text-right">SL tấm</TableHead>
                    <TableHead className="text-right">Đơn giá</TableHead>
                    <TableHead className="text-right">Thành tiền</TableHead>
                    <TableHead className="text-right">Cước vận chuyển</TableHead>
                    <TableHead className="text-right">VAT</TableHead>
                    <TableHead className="text-right">Tổng cộng</TableHead>
                    {hasCostColumns && (
                      <>
                        <TableHead className="text-right">Giá nhập</TableHead>
                        <TableHead className="text-right">Thành tiền nhập</TableHead>
                        <TableHead className="text-right">Chênh lệch</TableHead>
                        <TableHead className="text-right">Chênh + cước</TableHead>
                      </>
                    )}
                    <TableHead>Liên hệ</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {detailItems.map((item, idx) => {
                    const isFirst = item.isFirstLineOfQuotation;
                    const freight = isFirst ? item.freight : 0;
                    const profitPlusFreight = item.lineProfit !== null ? item.lineProfit + freight : null;
                    return (
                      <TableRow
                        key={`${item.quotationId}-${item.productName}-${idx}`}
                        className={`even:bg-muted/50${isFirst && idx > 0 ? ' border-t-2 border-border' : ''}`}
                      >
                        <TableCell className="whitespace-nowrap">
                          {isFirst ? formatDate(item.quotationDate) : ''}
                        </TableCell>
                        <TableCell className="whitespace-nowrap">
                          {isFirst ? item.quotationCode : ''}
                        </TableCell>
                        <TableCell className="whitespace-nowrap">
                          {isFirst ? formatDate(item.revenueDate) : ''}
                        </TableCell>
                        <TableCell className="min-w-[10rem]">
                          {isFirst ? item.customerName : ''}
                        </TableCell>
                        <TableCell className="min-w-[12rem]">
                          {isFirst ? (item.deliveryAddress ?? item.customerAddress ?? '') : ''}
                        </TableCell>
                        <TableCell className="min-w-[14rem]">
                          <div className="font-medium">{item.productName}</div>
                          <div className="text-xs text-muted-foreground">{formatProductSize(item)}</div>
                        </TableCell>
                        <TableCell className="text-right tabular-nums">{formatNullableNumber(item.density)}</TableCell>
                        <TableCell className="text-right tabular-nums">{formatNullableNumber(item.quantity)}</TableCell>
                        <TableCell className="text-right tabular-nums">{formatNullableNumber(item.sheetCount)}</TableCell>
                        <TableCell className="text-right tabular-nums">{formatMoneyNumber(item.unitPrice)}</TableCell>
                        <TableCell className="text-right tabular-nums">{formatMoneyNumber(item.lineTotal)}</TableCell>
                        <TableCell className="text-right tabular-nums">{isFirst ? formatMoneyNumber(item.freight) : ''}</TableCell>
                        <TableCell className="text-right tabular-nums">{isFirst ? formatMoneyNumber(item.taxAmount) : ''}</TableCell>
                        <TableCell className="text-right tabular-nums font-medium">{isFirst ? formatMoneyNumber(item.total) : ''}</TableCell>
                        {hasCostColumns && (
                          <>
                            <TableCell className="text-right tabular-nums">{formatMoneyNumber(item.unitCost)}</TableCell>
                            <TableCell className="text-right tabular-nums">{formatMoneyNumber(item.lineCost)}</TableCell>
                            <TableCell className="text-right tabular-nums">{formatMoneyNumber(item.lineProfit)}</TableCell>
                            <TableCell className="text-right tabular-nums">{formatMoneyNumber(profitPlusFreight)}</TableCell>
                          </>
                        )}
                        <TableCell className="whitespace-nowrap">
                          {isFirst ? (item.deliveryPhone ?? item.contactPhone ?? '') : ''}
                        </TableCell>
                      </TableRow>
                    );
                  })}
                </TableBody>
                <TableFooter className="sticky bottom-0 z-10 bg-muted">
                  <TableRow>
                    <TableCell colSpan={7}>Tổng cộng</TableCell>
                    <TableCell className="text-right tabular-nums">{formatNullableNumber(detailTotals.quantity)}</TableCell>
                    <TableCell className="text-right tabular-nums">{formatNullableNumber(detailTotals.sheetCount)}</TableCell>
                    <TableCell />
                    <TableCell className="text-right tabular-nums">{formatMoneyNumber(detailTotals.lineTotal)}</TableCell>
                    <TableCell className="text-right tabular-nums">{formatMoneyNumber(detailTotals.freight)}</TableCell>
                    <TableCell className="text-right tabular-nums">{formatMoneyNumber(detailTotals.taxAmount)}</TableCell>
                    <TableCell className="text-right tabular-nums font-semibold">{formatMoneyNumber(detailTotals.total)}</TableCell>
                    {hasCostColumns && (
                      <>
                        <TableCell />
                        <TableCell className="text-right tabular-nums">{formatMoneyNumber(detailTotals.lineCost)}</TableCell>
                        <TableCell className="text-right tabular-nums">{formatMoneyNumber(detailTotals.lineProfit)}</TableCell>
                        <TableCell className="text-right tabular-nums">
                          {formatMoneyNumber(detailTotals.lineProfit + detailTotals.freight)}
                        </TableCell>
                      </>
                    )}
                    <TableCell />
                  </TableRow>
                </TableFooter>
              </Table>
            </div>
          )}
        </CardContent>
      </Card>

      <div className="grid gap-4 md:grid-cols-3">
        <KpiCard
          label="Tổng doanh thu"
          format="currency"
          kpi={rangeRevenue}
          loading={summary.isLoading}
          accentColor="hsl(var(--primary))"
        />
        <KpiCard
          label="Số báo giá xác nhận"
          format="number"
          kpi={{ value: confirmedCount, deltaPct: null, spark: [] }}
          loading={summary.isLoading}
        />
        <KpiCard
          label="Trung bình / BG"
          format="currency"
          kpi={{ value: avgPerQuote, deltaPct: null, spark: [] }}
          loading={summary.isLoading}
        />
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Doanh thu theo {granularity === 'day' ? 'ngày' : granularity === 'week' ? 'tuần' : 'tháng'}</CardTitle>
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

      <div className="grid gap-4 lg:grid-cols-3">
        <Card className="lg:col-span-2">
          <CardHeader>
            <CardTitle>Bảng doanh thu theo ngày</CardTitle>
          </CardHeader>
          <CardContent>
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Ngày</TableHead>
                  <TableHead className="text-right">Số BG</TableHead>
                  <TableHead className="text-right">Doanh thu</TableHead>
                  <TableHead className="text-right">TB / BG</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {(revenue.data?.points ?? []).filter((p) => p.total > 0 || p.confirmedCount > 0).map((p) => (
                  <TableRow key={p.date}>
                    <TableCell>{p.date}</TableCell>
                    <TableCell className="text-right tabular-nums">{formatNumber(p.confirmedCount)}</TableCell>
                    <TableCell className="text-right tabular-nums">{formatVnd(p.total)}</TableCell>
                    <TableCell className="text-right tabular-nums">
                      {p.confirmedCount > 0 ? formatVnd(p.total / p.confirmedCount) : '—'}
                    </TableCell>
                  </TableRow>
                ))}
                {(revenue.data?.points ?? []).every((p) => p.total === 0 && p.confirmedCount === 0) && !revenue.isLoading && (
                  <TableRow>
                    <TableCell colSpan={4} className="text-center text-sm text-muted-foreground">
                      Chưa có dữ liệu.
                    </TableCell>
                  </TableRow>
                )}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
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
      </div>
    </div>
  );
}
