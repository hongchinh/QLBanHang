import { useCallback, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useDebtDocuments } from '@/features/cash-reports/hooks';
import type { DebtPartnerSearchItem } from '@/features/debt/types';
import { DEBT_SIDE_LABELS } from '@/features/cash-common/labels';
import type { DebtSide } from '@/features/cash-common/types';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { getErrorMessage } from '@/lib/api-client';
import { useReplaceSearchParams } from '@/lib/use-search-param-state';
import { formatDateOnly } from '@/lib/date-only';
import { cn, formatCurrencyVnd } from '@/lib/utils';
import { todayYmd } from '@/lib/vn-datetime';
import { DEBT_SOURCE_TYPE_LABELS, NUMERIC_CELL, formatSignedBalance, parseDebtSide } from './document-link';
import { DocumentCodeLink } from './document-code-link';
import { ReportPartnerPicker } from './report-partner-picker';

const SIDES: DebtSide[] = ['Receivable', 'Payable'];

// Công nợ theo chứng từ: open documents per partner as at a date. Route permission: reports.debt.
// Filters live in the URL so other screens can open it for one partner.
export function DebtDocumentsPage() {
  const [searchParams] = useSearchParams();
  const setParams = useReplaceSearchParams();
  const side = parseDebtSide(searchParams.get('side'));
  const at = searchParams.get('at') || todayYmd();
  const partnerId = searchParams.get('partnerId') || undefined;
  const [picked, setPicked] = useState<{ id: string; label: string } | null>(null);

  const { data: report, isLoading, isError, error } = useDebtDocuments({ side, at, partnerId });

  const updateParams = useCallback(
    (patch: Record<string, string | null>) => {
      setParams((prev) => {
        const next = new URLSearchParams(prev);
        for (const [key, value] of Object.entries(patch)) {
          if (value) next.set(key, value);
          else next.delete(key);
        }
        return next;
      });
    },
    [setParams],
  );

  const matched = partnerId ? report?.groups.find((g) => g.partnerId === partnerId) : undefined;
  const selectedLabel = !partnerId
    ? ''
    : picked?.id === partnerId
      ? picked.label
      : matched
        ? `${matched.partnerCode} - ${matched.partnerName}`
        : 'Đối tượng đã chọn';

  const selectPartner = (p: DebtPartnerSearchItem) => {
    setPicked({ id: p.id, label: `${p.code} - ${p.name}` });
    updateParams({ partnerId: p.id });
  };

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">Công nợ theo chứng từ</h1>

      <Card>
        <CardContent className="grid gap-4 pt-6 sm:grid-cols-3">
          <div className="space-y-1">
            <Label htmlFor="debt-documents-side">Phía</Label>
            {/* A partner belongs to one side's list, so switching the side clears it. */}
            <Select value={side} onValueChange={(v) => updateParams({ side: v, partnerId: null })}>
              <SelectTrigger id="debt-documents-side">
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
            <Label htmlFor="debt-documents-at">Tại ngày</Label>
            <Input id="debt-documents-at" type="date" value={at} onChange={(e) => updateParams({ at: e.target.value })} />
          </div>
          <div className="space-y-1">
            <Label htmlFor="debt-documents-partner">Đối tượng</Label>
            <ReportPartnerPicker
              id="debt-documents-partner"
              key={side}
              side={side}
              selectedLabel={selectedLabel}
              onSelect={selectPartner}
              onClear={() => updateParams({ partnerId: null })}
            />
          </div>
        </CardContent>
      </Card>

      {isError ? (
        <p className="text-sm text-destructive">{getErrorMessage(error)}</p>
      ) : report ? (
        report.groups.length === 0 ? (
          <p className="text-sm text-muted-foreground">Không có chứng từ còn nợ tại ngày này</p>
        ) : (
          <>
            {report.groups.map((group) => (
              <Card key={group.partnerId}>
                <CardContent className="space-y-3 overflow-x-auto pt-6">
                  <h2 className="font-semibold">
                    {group.partnerCode} - {group.partnerName}
                  </h2>
                  <p className="text-sm font-medium">Chứng từ còn nợ</p>
                  <table className="w-full min-w-[1000px] text-sm">
                    <thead>
                      <tr className="border-b text-left text-muted-foreground">
                        <th className="px-2 py-2">Ngày CT</th>
                        <th className="px-2 py-2">Số CT</th>
                        <th className="px-2 py-2">Loại CT</th>
                        <th className="px-2 py-2">Diễn giải</th>
                        <th className="px-2 py-2">Hạn TT</th>
                        <th className="px-2 py-2 text-right">Số tiền</th>
                        <th className="px-2 py-2 text-right">Đã đối trừ</th>
                        <th className="px-2 py-2 text-right">Còn lại</th>
                        <th className="px-2 py-2 text-right">Số ngày nợ</th>
                        <th className="px-2 py-2 text-right">Quá hạn (ngày)</th>
                      </tr>
                    </thead>
                    <tbody>
                      {group.items.map((row) => (
                        <tr key={row.entryId} className="border-b">
                          <td className="whitespace-nowrap px-2 py-2">{formatDateOnly(row.docDate)}</td>
                          <td className="px-2 py-2">
                            <DocumentCodeLink sourceType={row.sourceType} sourceId={row.sourceId} code={row.docCode} />
                          </td>
                          <td className="px-2 py-2">{DEBT_SOURCE_TYPE_LABELS[row.sourceType]}</td>
                          <td className="px-2 py-2">{row.description}</td>
                          <td className="whitespace-nowrap px-2 py-2">{formatDateOnly(row.dueDate)}</td>
                          <td className={NUMERIC_CELL}>{formatCurrencyVnd(row.amount)}</td>
                          <td className={NUMERIC_CELL}>{formatCurrencyVnd(row.settled)}</td>
                          <td className={NUMERIC_CELL}>{formatCurrencyVnd(row.remaining)}</td>
                          <td className={NUMERIC_CELL}>{row.daysOutstanding}</td>
                          <td className={cn(NUMERIC_CELL, row.daysOverdue > 0 && 'text-destructive')}>
                            {row.daysOverdue > 0 ? row.daysOverdue : ''}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>

                  {group.unallocated.length > 0 && (
                    <>
                      <p className="text-sm font-medium">Chưa đối trừ</p>
                      <table className="w-full min-w-[800px] text-sm">
                        <thead>
                          <tr className="border-b text-left text-muted-foreground">
                            <th className="px-2 py-2">Ngày CT</th>
                            <th className="px-2 py-2">Số CT</th>
                            <th className="px-2 py-2">Loại CT</th>
                            <th className="px-2 py-2">Diễn giải</th>
                            <th className="px-2 py-2 text-right">Số tiền</th>
                            <th className="px-2 py-2 text-right">Đã đối trừ</th>
                            <th className="px-2 py-2 text-right">Còn lại</th>
                          </tr>
                        </thead>
                        <tbody>
                          {group.unallocated.map((row) => (
                            <tr key={row.entryId} className="border-b">
                              <td className="whitespace-nowrap px-2 py-2">{formatDateOnly(row.docDate)}</td>
                              <td className="px-2 py-2">
                                <DocumentCodeLink
                                  sourceType={row.sourceType}
                                  sourceId={row.sourceId}
                                  code={row.docCode}
                                />
                              </td>
                              <td className="px-2 py-2">{DEBT_SOURCE_TYPE_LABELS[row.sourceType]}</td>
                              <td className="px-2 py-2">{row.description}</td>
                              <td className={NUMERIC_CELL}>{formatCurrencyVnd(row.amount)}</td>
                              <td className={NUMERIC_CELL}>{formatCurrencyVnd(row.settled)}</td>
                              <td className={NUMERIC_CELL}>{formatCurrencyVnd(row.remaining)}</td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </>
                  )}

                  <p className="text-sm font-semibold">{`Số dư: ${formatSignedBalance(report.side, group.balance)}`}</p>
                </CardContent>
              </Card>
            ))}
            <Card>
              <CardContent className="grid gap-2 pt-6 text-sm font-semibold sm:grid-cols-3">
                <div>{`Còn lại: ${formatCurrencyVnd(report.totalRemaining)}`}</div>
                <div>{`Chưa đối trừ: ${formatCurrencyVnd(report.totalUnallocated)}`}</div>
                <div>{`Tổng số dư: ${formatSignedBalance(report.side, report.totalBalance)}`}</div>
              </CardContent>
            </Card>
          </>
        )
      ) : (
        isLoading && <p className="text-sm text-muted-foreground">Đang tải…</p>
      )}
    </div>
  );
}
