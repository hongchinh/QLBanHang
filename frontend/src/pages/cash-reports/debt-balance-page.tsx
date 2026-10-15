import { useNavigate, useSearchParams } from 'react-router-dom';
import { useDebtBalance } from '@/features/cash-reports/hooks';
import type { DebtBalanceRow } from '@/features/cash-reports/types';
import { DEBT_SIDE_LABELS } from '@/features/cash-common/labels';
import type { DebtSide } from '@/features/cash-common/types';
import { useFromHere } from '@/components/layout/back-link';
import { PeriodPresetSelect } from '@/components/period-preset-select/period-preset-select';
import { ReportScopeSelect } from '@/components/report-scope-select/report-scope-select';
import { useReportScope } from '@/components/report-scope-select/use-report-scope';
import { Tt133ExportButton } from '@/components/tt133-export/tt133-export-button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { tt133FileBase, type Tt133ExportForm } from '@/features/tt133-export/types';
import { getErrorMessage } from '@/lib/api-client';
import { useDebouncedValue } from '@/lib/use-debounced-value';
import { useSearchParamString } from '@/lib/use-search-param-state';
import { firstDayOfMonthYmd, todayYmd } from '@/lib/vn-datetime';
import { NUMERIC_CELL, formatAmountCell, parseDebtSide } from './document-link';

const SIDES: DebtSide[] = ['Receivable', 'Payable'];

function AmountCells({ row }: { row: DebtBalanceRow }) {
  return (
    <>
      <td className={NUMERIC_CELL}>{formatAmountCell(row.openingDebit)}</td>
      <td className={NUMERIC_CELL}>{formatAmountCell(row.openingCredit)}</td>
      <td className={NUMERIC_CELL}>{formatAmountCell(row.increase)}</td>
      <td className={NUMERIC_CELL}>{formatAmountCell(row.decrease)}</td>
      <td className={NUMERIC_CELL}>{formatAmountCell(row.closingDebit)}</td>
      <td className={NUMERIC_CELL}>{formatAmountCell(row.closingCredit)}</td>
    </>
  );
}

// Số dư công nợ. Route permission: reports.debt. Filters live in the URL; a row opens the partner's ledger.
export function DebtBalancePage() {
  const navigate = useNavigate();
  const fromHere = useFromHere();
  const [, setSearchParams] = useSearchParams();
  const [sideParam, setSide] = useSearchParamString('side');
  const [from, setFrom] = useSearchParamString('from', firstDayOfMonthYmd());
  const [to, setTo] = useSearchParamString('to', todayYmd());
  const [search, setSearch] = useSearchParamString('search');
  const [scope, setScope] = useReportScope();
  const debouncedSearch = useDebouncedValue(search, 300);
  const side = parseDebtSide(sideParam);

  // yyyy-MM-dd strings compare chronologically.
  const rangeError = from > to ? 'Từ ngày không được sau Đến ngày' : null;

  const { data: report, isLoading, isError, error } = useDebtBalance(
    { side, from, to, search: debouncedSearch.trim() || undefined, scope },
    { enabled: !rangeError },
  );
  const rows = report?.rows ?? [];

  // The summary is exported with the screen's filters, so the file equals the screen.
  const exportForm: Tt133ExportForm = {
    code: 'TongHopCongNo',
    report: 'debt-balance',
    params: { side, from, to, search: debouncedSearch.trim() || undefined, scope },
    fileBase: tt133FileBase('TongHopCongNo', from, to),
  };

  const openLedger = (row: DebtBalanceRow) => {
    if (!row.partnerId) return;
    const params = new URLSearchParams({ side, partnerId: row.partnerId, from, to });
    if (scope === 'Company') params.set('scope', scope);
    navigate(`/reports/debt-ledger?${params.toString()}`, { state: fromHere });
  };

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h1 className="text-xl font-semibold">Số dư công nợ</h1>
        <Tt133ExportButton forms={[exportForm]} disabled={!!rangeError} />
      </div>

      <Card>
        <CardContent className="grid gap-4 pt-6 sm:grid-cols-2 lg:grid-cols-4">
          <div className="space-y-1">
            <Label htmlFor="debt-balance-side">Phía</Label>
            <Select value={side} onValueChange={setSide}>
              <SelectTrigger id="debt-balance-side">
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
          <PeriodPresetSelect
            id="debt-balance-period"
            onChange={(range) =>
              // One update for both keys: two setSearchParams calls in a row would keep only the last.
              setSearchParams(
                (prev) => {
                  const next = new URLSearchParams(prev);
                  next.set('from', range.from);
                  next.set('to', range.to);
                  return next;
                },
                { replace: true },
              )
            }
          />
          <div className="space-y-1">
            <Label htmlFor="debt-balance-from">Từ ngày</Label>
            <Input id="debt-balance-from" type="date" value={from} onChange={(e) => setFrom(e.target.value)} />
          </div>
          <div className="space-y-1">
            <Label htmlFor="debt-balance-to">Đến ngày</Label>
            <Input id="debt-balance-to" type="date" value={to} onChange={(e) => setTo(e.target.value)} />
          </div>
          <div className="space-y-1">
            <Label htmlFor="debt-balance-search">Tìm kiếm</Label>
            <Input
              id="debt-balance-search"
              placeholder="Mã hoặc tên đối tượng"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
          </div>
          <ReportScopeSelect id="debt-balance-scope" value={scope} onChange={setScope} />
        </CardContent>
      </Card>

      {rangeError ? (
        <p className="text-sm text-destructive">{rangeError}</p>
      ) : (
        <Card>
          <CardContent className="overflow-x-auto pt-6">
            <table className="w-full min-w-[960px] text-sm">
              <thead>
                <tr className="border-b text-left text-muted-foreground">
                  <th className="px-2 py-2">Mã</th>
                  <th className="px-2 py-2">Tên</th>
                  <th className="px-2 py-2 text-right">Đầu kỳ Nợ</th>
                  <th className="px-2 py-2 text-right">Đầu kỳ Có</th>
                  <th className="px-2 py-2 text-right">Phát sinh tăng</th>
                  <th className="px-2 py-2 text-right">Phát sinh giảm</th>
                  <th className="px-2 py-2 text-right">Cuối kỳ Nợ</th>
                  <th className="px-2 py-2 text-right">Cuối kỳ Có</th>
                </tr>
              </thead>
              <tbody>
                {rows.map((row) => (
                  <tr
                    key={row.partnerId ?? row.partnerCode}
                    className="cursor-pointer border-b hover:bg-muted/50"
                    onClick={() => openLedger(row)}
                  >
                    <td className="px-2 py-2 font-medium">{row.partnerCode}</td>
                    <td className="px-2 py-2">{row.partnerName}</td>
                    <AmountCells row={row} />
                  </tr>
                ))}
                {isError && (
                  <tr>
                    <td colSpan={8} className="px-2 py-6 text-center text-destructive">
                      {getErrorMessage(error)}
                    </td>
                  </tr>
                )}
                {isLoading && (
                  <tr>
                    <td colSpan={8} className="px-2 py-6 text-center text-muted-foreground">
                      Đang tải…
                    </td>
                  </tr>
                )}
                {report && !isError && rows.length === 0 && (
                  <tr>
                    <td colSpan={8} className="px-2 py-6 text-center text-muted-foreground">
                      Không có số dư công nợ trong khoảng thời gian này
                    </td>
                  </tr>
                )}
              </tbody>
              {report && !isError && (
                <tfoot>
                  <tr className="font-semibold">
                    <td colSpan={2} className="px-2 py-2">
                      Tổng cộng
                    </td>
                    <AmountCells row={report.totals} />
                  </tr>
                </tfoot>
              )}
            </table>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
