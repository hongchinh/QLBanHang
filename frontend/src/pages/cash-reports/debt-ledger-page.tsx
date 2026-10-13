import { useCallback, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useDebtLedger } from '@/features/cash-reports/hooks';
import type { DebtPartnerSearchItem } from '@/features/debt/types';
import { DEBT_SIDE_LABELS } from '@/features/cash-common/labels';
import type { DebtSide } from '@/features/cash-common/types';
import { ReportScopeSelect } from '@/components/report-scope-select/report-scope-select';
import { useReportScope } from '@/components/report-scope-select/use-report-scope';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { getErrorMessage } from '@/lib/api-client';
import { firstDayOfMonthYmd, todayYmd } from '@/lib/vn-datetime';
import { parseDebtSide } from './document-link';
import { DebtLedgerTable } from './debt-ledger-table';
import { ReportPartnerPicker } from './report-partner-picker';

const SIDES: DebtSide[] = ['Receivable', 'Payable'];

// Sổ chi tiết công nợ of one partner and side. Route permission: reports.debt. Filters live in the URL,
// so the balance report can open it for a partner.
export function DebtLedgerPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const side = parseDebtSide(searchParams.get('side'));
  const partnerId = searchParams.get('partnerId') ?? '';
  const from = searchParams.get('from') || firstDayOfMonthYmd();
  const to = searchParams.get('to') || todayYmd();
  const [scope, setScope] = useReportScope();
  const [picked, setPicked] = useState<{ id: string; label: string } | null>(null);

  const rangeError = from > to ? 'Từ ngày không được sau Đến ngày' : null;

  const { data: ledger, isLoading, isError, error } = useDebtLedger(
    { side, partnerId, from, to, scope },
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
      : ledger && ledger.partnerId === partnerId
        ? `${ledger.partnerCode} - ${ledger.partnerName}`
        : '';

  const selectPartner = (p: DebtPartnerSearchItem) => {
    setPicked({ id: p.id, label: `${p.code} - ${p.name}` });
    updateParams({ partnerId: p.id });
  };

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">Sổ chi tiết công nợ</h1>

      <Card>
        <CardContent className="grid gap-4 pt-6 sm:grid-cols-2 lg:grid-cols-4">
          <div className="space-y-1">
            <Label htmlFor="debt-ledger-side">Phía</Label>
            {/* A partner belongs to one side's list, so switching the side clears it. */}
            <Select value={side} onValueChange={(v) => updateParams({ side: v, partnerId: null })}>
              <SelectTrigger id="debt-ledger-side">
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
            <Label htmlFor="debt-ledger-partner">Đối tượng</Label>
            <ReportPartnerPicker id="debt-ledger-partner" key={side} side={side} selectedLabel={selectedLabel} onSelect={selectPartner} />
          </div>
          <div className="space-y-1">
            <Label htmlFor="debt-ledger-from">Từ ngày</Label>
            <Input
              id="debt-ledger-from"
              type="date"
              value={from}
              onChange={(e) => updateParams({ from: e.target.value })}
            />
          </div>
          <div className="space-y-1">
            <Label htmlFor="debt-ledger-to">Đến ngày</Label>
            <Input id="debt-ledger-to" type="date" value={to} onChange={(e) => updateParams({ to: e.target.value })} />
          </div>
          <ReportScopeSelect id="debt-ledger-scope" value={scope} onChange={setScope} />
        </CardContent>
      </Card>

      {!partnerId ? (
        <p className="text-sm text-muted-foreground">Chọn đối tượng để xem sổ chi tiết công nợ</p>
      ) : rangeError ? (
        <p className="text-sm text-destructive">{rangeError}</p>
      ) : isError ? (
        <p className="text-sm text-destructive">{getErrorMessage(error)}</p>
      ) : ledger ? (
        <Card>
          <CardContent className="overflow-x-auto pt-6">
            <DebtLedgerTable ledger={ledger} />
          </CardContent>
        </Card>
      ) : (
        isLoading && <p className="text-sm text-muted-foreground">Đang tải…</p>
      )}
    </div>
  );
}
