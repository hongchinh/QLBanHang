import { useMemo } from 'react';
import { useNavigate, useParams, useSearchParams, useLocation } from 'react-router-dom';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Table,
  TableBody,
  TableCell,
  TableFooter,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { BackLink, useBackTarget } from '@/components/layout/back-link';
import { Can } from '@/components/auth/can';
import { useSalesRevenueDetail } from '@/features/reports/sales-revenue-detail/hooks';
import type { SalesRevenueLineItemDto } from '@/features/reports/sales-revenue-detail/types';

const moneyFmt = new Intl.NumberFormat('vi-VN');

interface RevenueTotals {
  quantity: number;
  lineTotal: number;
  freight: number;
  unitCost: number;
  lineCost: number;
  lineProfit: number;
}

function calculateRevenueTotals(items: SalesRevenueLineItemDto[]): RevenueTotals {
  return {
    quantity: items.reduce((sum, item) => sum + item.quantity, 0),
    lineTotal: items.reduce((sum, item) => sum + item.lineTotal, 0),
    freight: items.reduce((sum, item) => sum + item.freight, 0),
    unitCost: items.reduce((sum, item) => sum + (item.unitCost ?? 0), 0),
    lineCost: items.reduce((sum, item) => sum + (item.lineCost ?? 0), 0),
    lineProfit: items.reduce((sum, item) => sum + (item.lineProfit ?? 0), 0),
  };
}

function useRevenueTotals(items: SalesRevenueLineItemDto[]): RevenueTotals {
  return useMemo(() => calculateRevenueTotals(items), [items]);
}

interface TotalsRowProps {
  totals: RevenueTotals;
  hasCost: boolean;
}

function TotalsRow({ totals, hasCost }: TotalsRowProps) {
  return (
    <TableFooter className="sticky bottom-0 z-10 bg-muted font-semibold">
      <TableRow className="hover:bg-muted">
        <TableCell className="text-sm">Tổng cộng</TableCell>
        <TableCell colSpan={3} />
        <TableCell />
        <TableCell />
        <TableCell />
        <TableCell className="text-right tabular-nums">
          {moneyFmt.format(totals.quantity)}
        </TableCell>
        <TableCell />
        <TableCell className="text-right tabular-nums">
          {moneyFmt.format(totals.lineTotal)}
        </TableCell>
        <TableCell className="text-right tabular-nums">
          {moneyFmt.format(totals.freight)}
        </TableCell>
        {hasCost && (
          <>
            <TableCell className="text-right tabular-nums">
              {moneyFmt.format(totals.unitCost)}
            </TableCell>
            <TableCell className="text-right tabular-nums">
              {moneyFmt.format(totals.lineCost)}
            </TableCell>
            <TableCell className="text-right tabular-nums">
              {moneyFmt.format(totals.lineProfit)}
            </TableCell>
          </>
        )}
        <TableCell colSpan={2} />
      </TableRow>
    </TableFooter>
  );
}

function formatDate(value: string | null | undefined): string {
  if (!value) return '';
  return value.slice(0, 10).split('-').reverse().join('/');
}

export function SalesRevenueDetailPage() {
  const { saleUserId } = useParams<{ saleUserId: string }>();
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const location = useLocation();
  const back = useBackTarget();
  const saleName = (location.state as { saleName?: string } | null)?.saleName;

  const from = searchParams.get('from') ?? '';
  const to = searchParams.get('to') ?? '';
  const hasParams = Boolean(from && to && saleUserId);

  const query = useSalesRevenueDetail(saleUserId, { from, to }, hasParams);

  const items: SalesRevenueLineItemDto[] = query.data ?? [];
  const hasCost = items.some((i) => i.unitCost !== null);
  const totals = useRevenueTotals(items);

  return (
    <Can
      permission="reports.revenue"
      fallback={<div className="p-4">Bạn không có quyền xem báo cáo này.</div>}
    >
      <div className="flex h-full min-h-0 flex-col gap-4">
        <div className="flex items-center gap-3">
          <BackLink to={back ?? '/reports/sales-revenue'} label={back ? 'báo cáo trước' : 'Doanh thu theo sale'} />
          <div>
            <h1 className="text-2xl font-bold">
              Chi tiết doanh thu{saleName ? ` — ${saleName}` : ''}
            </h1>
            <p className="text-sm text-muted-foreground">
              {from && to ? `${formatDate(from)} – ${formatDate(to)}` : ''}
            </p>
          </div>
        </div>

        {!hasParams && (
          <div className="rounded-md border border-muted bg-muted/30 p-4 text-sm text-muted-foreground">
            Vui lòng truy cập trang này từ báo cáo doanh thu theo sale.
          </div>
        )}

        <Card className="flex flex-1 min-h-0 flex-col">
          <CardHeader>
            <CardTitle>Dòng hàng hóa</CardTitle>
          </CardHeader>
          <CardContent className="flex flex-1 min-h-0 flex-col p-0">
            {query.isLoading ? (
              <div className="p-6 text-sm text-muted-foreground">Đang tải…</div>
            ) : query.isError ? (
              <div className="p-6 text-sm text-destructive">Không tải được dữ liệu.</div>
            ) : items.length === 0 ? (
              <div className="p-6 text-sm text-muted-foreground">
                Không có dòng hàng nào trong khoảng thời gian này.
              </div>
            ) : (
              <div
                data-testid="table-scroll-container"
                className="relative flex-1 min-h-0 overflow-hidden"
              >
                <Table containerClassName="h-full">
                  <TableHeader className="sticky top-0 z-10">
                    <TableRow>
                      <TableHead>Số BG</TableHead>
                      <TableHead>Ngày BG</TableHead>
                      <TableHead>Ngày xác nhận</TableHead>
                      <TableHead>Khách hàng</TableHead>
                      <TableHead>Hàng hóa</TableHead>
                      <TableHead>Quy cách</TableHead>
                      <TableHead>Đơn vị tính</TableHead>
                      <TableHead className="text-right">Số lượng</TableHead>
                      <TableHead className="text-right">Đơn giá</TableHead>
                      <TableHead className="text-right">Số tiền</TableHead>
                      <TableHead className="text-right">Vận chuyển</TableHead>
                      {hasCost && (
                        <>
                          <TableHead className="text-right">Đơn giá nhập</TableHead>
                          <TableHead className="text-right">Thành tiền nhập</TableHead>
                          <TableHead className="text-right">Lợi nhuận</TableHead>
                        </>
                      )}
                      <TableHead>Địa chỉ</TableHead>
                      <TableHead>Điện thoại</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {items.map((item, idx) => (
                      <TableRow
                        key={`${item.quotationId}-${item.productName}-${idx}`}
                        className={`cursor-pointer hover:bg-muted/50${item.isFirstLineOfQuotation && idx > 0 ? ' border-t-2 border-border' : ''}`}
                        onClick={() => navigate(`/quotations/${item.quotationId}`)}
                      >
                        <TableCell>
                          {item.isFirstLineOfQuotation ? item.quotationCode : ''}
                        </TableCell>
                        <TableCell>
                          {item.isFirstLineOfQuotation ? formatDate(item.quotationDate) : ''}
                        </TableCell>
                        <TableCell>
                          {item.isFirstLineOfQuotation ? formatDate(item.confirmedAt) : ''}
                        </TableCell>
                        <TableCell>
                          {item.isFirstLineOfQuotation ? item.customerName : ''}
                        </TableCell>
                        <TableCell>{item.productName}</TableCell>
                        <TableCell>{item.specification ?? ''}</TableCell>
                        <TableCell>{item.unitName}</TableCell>
                        <TableCell className="text-right tabular-nums">{moneyFmt.format(item.quantity)}</TableCell>
                        <TableCell className="text-right tabular-nums">{moneyFmt.format(item.unitPrice)}</TableCell>
                        <TableCell className="text-right tabular-nums">{moneyFmt.format(item.lineTotal)}</TableCell>
                        <TableCell className="text-right tabular-nums">
                          {item.isFirstLineOfQuotation ? moneyFmt.format(item.freight) : ''}
                        </TableCell>
                        {hasCost && (
                          <>
                            <TableCell className="text-right tabular-nums">
                              {item.unitCost !== null ? moneyFmt.format(item.unitCost) : ''}
                            </TableCell>
                            <TableCell className="text-right tabular-nums">
                              {item.lineCost !== null ? moneyFmt.format(item.lineCost) : ''}
                            </TableCell>
                            <TableCell className="text-right tabular-nums">
                              {item.lineProfit !== null ? moneyFmt.format(item.lineProfit) : ''}
                            </TableCell>
                          </>
                        )}
                        <TableCell>
                          {item.isFirstLineOfQuotation ? (item.customerAddress ?? '') : ''}
                        </TableCell>
                        <TableCell>
                          {item.isFirstLineOfQuotation ? (item.contactPhone ?? '') : ''}
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                  <TotalsRow totals={totals} hasCost={hasCost} />
                </Table>
              </div>
            )}
          </CardContent>
        </Card>
      </div>
    </Can>
  );
}

export { calculateRevenueTotals, useRevenueTotals, TotalsRow };
