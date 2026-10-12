import { useCallback, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useDebtReconciliation } from '@/features/cash-reports/hooks';
import type { DebtPartnerSearchItem } from '@/features/debt/types';
import { DEBT_SIDE_LABELS } from '@/features/cash-common/labels';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { getErrorMessage } from '@/lib/api-client';
import { formatDateOnly } from '@/lib/date-only';
import { formatCurrencyVnd } from '@/lib/utils';
import { firstDayOfMonthYmd, todayYmd } from '@/lib/vn-datetime';
import { DebtLedgerTable } from './debt-ledger-table';
import { ReportPartnerPicker } from './report-partner-picker';

function netText(net: number): string {
  if (net > 0) return `Đối tượng còn nợ ${formatCurrencyVnd(net)}`;
  if (net < 0) return `Công ty còn nợ ${formatCurrencyVnd(Math.abs(net))}`;
  return 'Hai bên không còn nợ';
}

// Biên bản đối chiếu công nợ of one partner: a ledger per side and the net amount. On screen only; the print
// and Excel template is Round 3. Route permission: reports.debt.
export function DebtReconciliationPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const partnerId = searchParams.get('partnerId') ?? '';
  const from = searchParams.get('from') || firstDayOfMonthYmd();
  const to = searchParams.get('to') || todayYmd();
  const [picked, setPicked] = useState<{ id: string; label: string } | null>(null);

  const rangeError = from > to ? 'Từ ngày không được sau Đến ngày' : null;

  const { data: report, isLoading, isError, error } = useDebtReconciliation(
    { partnerId, from, to },
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

  const selectedLabel =
    picked?.id === partnerId
      ? picked.label
      : report && report.partnerId === partnerId
        ? `${report.partnerCode} - ${report.partnerName}`
        : '';

  const selectPartner = (p: DebtPartnerSearchItem) => {
    setPicked({ id: p.id, label: `${p.code} - ${p.name}` });
    updateParams({ partnerId: p.id });
  };

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">Biên bản đối chiếu công nợ</h1>

      <Card>
        <CardContent className="grid gap-4 pt-6 sm:grid-cols-3">
          <div className="space-y-1">
            <Label htmlFor="debt-reconciliation-partner">Đối tượng</Label>
            <ReportPartnerPicker
              id="debt-reconciliation-partner"
              selectedLabel={selectedLabel}
              onSelect={selectPartner}
            />
          </div>
          <div className="space-y-1">
            <Label htmlFor="debt-reconciliation-from">Từ ngày</Label>
            <Input
              id="debt-reconciliation-from"
              type="date"
              value={from}
              onChange={(e) => updateParams({ from: e.target.value })}
            />
          </div>
          <div className="space-y-1">
            <Label htmlFor="debt-reconciliation-to">Đến ngày</Label>
            <Input
              id="debt-reconciliation-to"
              type="date"
              value={to}
              onChange={(e) => updateParams({ to: e.target.value })}
            />
          </div>
        </CardContent>
      </Card>

      {!partnerId ? (
        <p className="text-sm text-muted-foreground">Chọn đối tượng để xem biên bản đối chiếu</p>
      ) : rangeError ? (
        <p className="text-sm text-destructive">{rangeError}</p>
      ) : isError ? (
        <p className="text-sm text-destructive">{getErrorMessage(error)}</p>
      ) : report ? (
        <div className="space-y-4">
          <Card>
            <CardContent className="space-y-1 pt-6 text-center">
              <p className="text-lg font-bold">BIÊN BẢN ĐỐI CHIẾU CÔNG NỢ</p>
              <p className="text-sm">{report.branchName}</p>
              <p className="text-sm">
                Từ ngày {formatDateOnly(report.from)} đến ngày {formatDateOnly(report.to)}
              </p>
              <p className="text-sm font-medium">
                {report.partnerCode} - {report.partnerName}
              </p>
              {report.taxCode && <p className="text-sm">MST: {report.taxCode}</p>}
              {report.address && <p className="text-sm">Địa chỉ: {report.address}</p>}
            </CardContent>
          </Card>

          {report.sides.map((ledger) => (
            <Card key={ledger.side}>
              <CardContent className="space-y-3 overflow-x-auto pt-6">
                <h2 className="text-base font-semibold">{DEBT_SIDE_LABELS[ledger.side]}</h2>
                <DebtLedgerTable ledger={ledger} />
              </CardContent>
            </Card>
          ))}

          {report.netBalance != null && (
            <p className="text-sm font-semibold">Bù trừ hai bên: {netText(report.netBalance)}</p>
          )}
          <p className="text-sm text-muted-foreground">In biên bản: Đợt 3.</p>
        </div>
      ) : (
        isLoading && <p className="text-sm text-muted-foreground">Đang tải…</p>
      )}
    </div>
  );
}
