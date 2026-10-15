import { useEffect, useMemo, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  Table,
  TableBody,
  TableCell,
  TableFooter,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { useFromHere } from '@/components/layout/back-link';
import { Can } from '@/components/auth/can';
import { useAuthStore } from '@/stores/auth-store';
import { useSalesRevenue } from '@/features/reports/sales-revenue/hooks';
import { useQuotationOwners } from '@/features/quotations/hooks';

const ALL_SALES = '__all__';

function startOfMonthIso(): string {
  const d = new Date();
  return new Date(d.getFullYear(), d.getMonth(), 1).toISOString().slice(0, 10);
}

function todayIso(): string {
  return new Date().toISOString().slice(0, 10);
}

const moneyFmt = new Intl.NumberFormat('vi-VN');

export function SalesRevenuePage() {
  const navigate = useNavigate();
  const fromHere = useFromHere();
  const isAdmin = useAuthStore((s) => s.hasPermission('quotations.view_all'));
  const currentUser = useAuthStore((s) => s.user);
  const [from, setFrom] = useState(startOfMonthIso());
  const [to, setTo] = useState(todayIso());
  const [saleUserId, setSaleUserId] = useState<string | undefined>(undefined);

  const didInit = useRef(false);
  useEffect(() => {
    if (!didInit.current && currentUser?.id) {
      didInit.current = true;
      setSaleUserId(currentUser.id);
    }
  }, [currentUser?.id]);

  const params = useMemo(() => ({ from, to, saleUserId }), [from, to, saleUserId]);
  const query = useSalesRevenue(params, Boolean(from && to));
  const ownersQuery = useQuotationOwners({ enabled: isAdmin });

  return (
    <Can permission="reports.revenue" fallback={<div className="p-4">Bạn không có quyền xem báo cáo này.</div>}>
      <div className="space-y-4">
        <div>
          <h1 className="text-2xl font-bold">Doanh thu theo sale</h1>
          <p className="text-sm text-muted-foreground">
            Tổng hợp báo giá đã xác nhận theo thời điểm xác nhận.
          </p>
        </div>

        <Card>
          <CardHeader>
            <CardTitle>Bộ lọc</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="flex flex-wrap items-end gap-3">
              <div>
                <Label htmlFor="from">Từ ngày</Label>
                <Input id="from" type="date" value={from} onChange={(e) => setFrom(e.target.value)} />
              </div>
              <div>
                <Label htmlFor="to">Đến ngày</Label>
                <Input id="to" type="date" value={to} onChange={(e) => setTo(e.target.value)} />
              </div>
              {isAdmin && (
                <div className="min-w-[220px]">
                  <Label>Nhân viên kinh doanh</Label>
                  <Select
                    value={saleUserId ?? ALL_SALES}
                    onValueChange={(v) => setSaleUserId(v === ALL_SALES ? undefined : v)}
                  >
                    <SelectTrigger>
                      <SelectValue placeholder="Tất cả" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value={ALL_SALES}>Tất cả</SelectItem>
                      {(ownersQuery.data ?? []).map((u) => (
                        <SelectItem key={u.id} value={u.id}>
                          {u.isDeleted ? `${u.fullName} (đã nghỉ)` : u.fullName}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
              )}
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Kết quả</CardTitle>
          </CardHeader>
          <CardContent>
            {query.isLoading ? (
              <div className="text-sm text-muted-foreground">Đang tải…</div>
            ) : query.isError ? (
              <div className="text-sm text-destructive">Không tải được báo cáo.</div>
            ) : !query.data || query.data.items.length === 0 ? (
              <div className="text-sm text-muted-foreground">Không có báo giá đã xác nhận trong khoảng này.</div>
            ) : (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Sale</TableHead>
                    <TableHead className="text-right">Số báo giá</TableHead>
                    <TableHead className="text-right">Doanh thu (gồm thuế)</TableHead>
                    <TableHead className="text-right">Doanh thu thuần</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {query.data.items.map((it) => (
                    <TableRow
                      key={it.saleUserId}
                      className="cursor-pointer hover:bg-muted/50"
                      onClick={() =>
                        navigate(
                          `/reports/sales-revenue/${it.saleUserId}?${new URLSearchParams({ from, to }).toString()}`,
                          { state: { saleName: it.saleName, ...fromHere } },
                        )
                      }
                    >
                      <TableCell>
                        {it.saleName}
                        {it.isSaleDeleted && (
                          <span className="ml-2 text-xs text-muted-foreground">(đã ngừng)</span>
                        )}
                      </TableCell>
                      <TableCell className="text-right">{it.quotationCount}</TableCell>
                      <TableCell className="text-right">{moneyFmt.format(it.totalRevenueGross)}</TableCell>
                      <TableCell className="text-right">{moneyFmt.format(it.totalRevenueNet)}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
                <TableFooter>
                  <TableRow>
                    <TableCell>Tổng cộng</TableCell>
                    <TableCell className="text-right">{query.data.totalQuotationCount}</TableCell>
                    <TableCell className="text-right">{moneyFmt.format(query.data.grandTotalGross)}</TableCell>
                    <TableCell className="text-right">{moneyFmt.format(query.data.grandTotalNet)}</TableCell>
                  </TableRow>
                </TableFooter>
              </Table>
            )}
          </CardContent>
        </Card>
      </div>
    </Can>
  );
}
