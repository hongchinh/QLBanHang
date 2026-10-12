import { useCallback, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useDebtBySalesperson } from '@/features/cash-reports/hooks';
import type { DebtPartnerSearchItem } from '@/features/debt/types';
import { DEBT_SIDE_LABELS } from '@/features/cash-common/labels';
import type { DebtSide } from '@/features/cash-common/types';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { getErrorMessage } from '@/lib/api-client';
import { formatCurrencyVnd } from '@/lib/utils';
import { firstDayOfMonthYmd, todayYmd } from '@/lib/vn-datetime';
import { NUMERIC_CELL, formatSignedBalance, parseDebtSide } from './document-link';
import { ReportPartnerPicker } from './report-partner-picker';

const SIDES: DebtSide[] = ['Receivable', 'Payable'];

// Công nợ theo nhân viên: open debt per owner (the user who created the document). Route permission: reports.debt.
export function DebtBySalespersonPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const side = parseDebtSide(searchParams.get('side'));
  const from = searchParams.get('from') || firstDayOfMonthYmd();
  const to = searchParams.get('to') || todayYmd();
  const partnerId = searchParams.get('partnerId') || undefined;
  const [picked, setPicked] = useState<{ id: string; label: string } | null>(null);

  const rangeError = from > to ? 'Từ ngày không được sau Đến ngày' : null;

  const { data: report, isLoading, isError, error } = useDebtBySalesperson(
    { side, from, to, partnerId },
    { enabled: !rangeError },
  );

  const updateParams = useCallback(
    (patch: Record<string, string | null>) => {
      setSearchParams(
        (prev) => {
          const next = new URLSearchParams(prev);
          for (const [key, value] of Object.entries(patch)) {
            if (value) next.set(key, value);
            else next.delete(key);
          }
          return next;
        },
        { replace: true },
      );
    },
    [setSearchParams],
  );

  const selectedLabel = !partnerId ? '' : picked?.id === partnerId ? picked.label : 'Đối tượng đã chọn';

  const selectPartner = (p: DebtPartnerSearchItem) => {
    setPicked({ id: p.id, label: `${p.code} - ${p.name}` });
    updateParams({ partnerId: p.id });
  };

  const settledLabel = side === 'Receivable' ? 'Đã thu / đối trừ' : 'Đã trả / đối trừ';
  const unallocatedLabel =
    side === 'Receivable' ? 'Chưa đối trừ (khách trả trước)' : 'Chưa đối trừ (ứng trước NCC)';

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">Công nợ theo nhân viên</h1>

      <Card>
        <CardContent className="grid gap-4 pt-6 sm:grid-cols-2 lg:grid-cols-4">
          <div className="space-y-1">
            <Label htmlFor="debt-salesperson-side">Phía</Label>
            {/* A partner belongs to one side's list, so switching the side clears it. */}
            <Select value={side} onValueChange={(v) => updateParams({ side: v, partnerId: null })}>
              <SelectTrigger id="debt-salesperson-side">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {SIDES.map((s) => (
                  <SelectItem key={s} value={s}>
                    {DEBT_SIDE_LABELS[s]}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-1">
            <Label htmlFor="debt-salesperson-from">Từ ngày</Label>
            <Input
              id="debt-salesperson-from"
              type="date"
              value={from}
              onChange={(e) => updateParams({ from: e.target.value })}
            />
          </div>
          <div className="space-y-1">
            <Label htmlFor="debt-salesperson-to">Đến ngày</Label>
            <Input
              id="debt-salesperson-to"
              type="date"
              value={to}
              onChange={(e) => updateParams({ to: e.target.value })}
            />
          </div>
          <div className="space-y-1">
            <Label htmlFor="debt-salesperson-partner">Đối tượng</Label>
            <ReportPartnerPicker
              id="debt-salesperson-partner"
              key={side}
              side={side}
              selectedLabel={selectedLabel}
              onSelect={selectPartner}
              onClear={() => updateParams({ partnerId: null })}
            />
          </div>
        </CardContent>
      </Card>

      {rangeError ? (
        <p className="text-sm text-destructive">{rangeError}</p>
      ) : isError ? (
        <p className="text-sm text-destructive">{getErrorMessage(error)}</p>
      ) : report ? (
        report.rows.length === 0 &&
        report.unallocated.opening === 0 &&
        report.unallocated.closing === 0 &&
        report.openingBalance === 0 &&
        report.closingBalance === 0 ? (
          <p className="text-sm text-muted-foreground">Không có công nợ trong kỳ này</p>
        ) : (
          <Card>
            <CardContent className="space-y-3 overflow-x-auto pt-6">
              <table className="w-full min-w-[800px] text-sm">
                <thead>
                  <tr className="border-b text-left text-muted-foreground">
                    <th className="px-2 py-2">Nhân viên</th>
                    <th className="px-2 py-2 text-right">Còn nợ đầu kỳ</th>
                    <th className="px-2 py-2 text-right">Phát sinh nợ</th>
                    <th className="px-2 py-2 text-right">{settledLabel}</th>
                    <th className="px-2 py-2 text-right">Còn nợ cuối kỳ</th>
                    <th className="px-2 py-2 text-right">Trong đó quá hạn</th>
                  </tr>
                </thead>
                <tbody>
                  {report.rows.map((row) => (
                    <tr key={row.ownerUserId ?? 'unknown'} className="border-b">
                      <td className="px-2 py-2">{row.ownerName ?? 'Không xác định'}</td>
                      <td className={NUMERIC_CELL}>{formatCurrencyVnd(row.openingOpen)}</td>
                      <td className={NUMERIC_CELL}>{formatCurrencyVnd(row.increase)}</td>
                      <td className={NUMERIC_CELL}>{formatCurrencyVnd(row.settled)}</td>
                      <td className={NUMERIC_CELL}>{formatCurrencyVnd(row.closingOpen)}</td>
                      <td className={`${NUMERIC_CELL}${row.closingOverdue > 0 ? ' text-destructive' : ''}`}>
                        {formatCurrencyVnd(row.closingOverdue)}
                      </td>
                    </tr>
                  ))}
                </tbody>
                <tfoot>
                  <tr className="border-b font-semibold">
                    <td className="px-2 py-2">Cộng</td>
                    <td className={NUMERIC_CELL}>{formatCurrencyVnd(report.totals.openingOpen)}</td>
                    <td className={NUMERIC_CELL}>{formatCurrencyVnd(report.totals.increase)}</td>
                    <td className={NUMERIC_CELL}>{formatCurrencyVnd(report.totals.settled)}</td>
                    <td className={NUMERIC_CELL}>{formatCurrencyVnd(report.totals.closingOpen)}</td>
                    <td className={NUMERIC_CELL}>{formatCurrencyVnd(report.totals.closingOverdue)}</td>
                  </tr>
                  <tr className="border-b text-muted-foreground">
                    <td className="px-2 py-2">{unallocatedLabel}</td>
                    <td className={NUMERIC_CELL}>{formatCurrencyVnd(0 - report.unallocated.opening)}</td>
                    <td colSpan={2} />
                    <td className={NUMERIC_CELL}>{formatCurrencyVnd(0 - report.unallocated.closing)}</td>
                    <td />
                  </tr>
                  <tr className="font-semibold">
                    <td className="px-2 py-2">Số dư công nợ</td>
                    <td className={NUMERIC_CELL}>{formatSignedBalance(side, report.openingBalance)}</td>
                    <td colSpan={2} />
                    <td className={NUMERIC_CELL}>{formatSignedBalance(side, report.closingBalance)}</td>
                    <td />
                  </tr>
                </tfoot>
              </table>
              <p className="text-sm text-muted-foreground">Theo người lập chứng từ.</p>
            </CardContent>
          </Card>
        )
      ) : (
        isLoading && <p className="text-sm text-muted-foreground">Đang tải…</p>
      )}
    </div>
  );
}
