import { useCallback, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useDebtOverdue } from '@/features/cash-reports/hooks';
import { DEFAULT_BUCKETS, bucketLabel, normalizeBuckets } from '@/features/cash-reports/aging';
import type { DebtPartnerSearchItem } from '@/features/debt/types';
import { DEBT_SIDE_LABELS } from '@/features/cash-common/labels';
import type { DebtSide } from '@/features/cash-common/types';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { getErrorMessage } from '@/lib/api-client';
import { formatDateOnly } from '@/lib/date-only';
import { formatCurrencyVnd } from '@/lib/utils';
import { todayYmd } from '@/lib/vn-datetime';
import { NUMERIC_CELL, parseDebtSide } from './document-link';
import { BucketBoundariesInput } from './bucket-boundaries-input';
import { DocumentCodeLink } from './document-code-link';
import { ReportPartnerPicker } from './report-partner-picker';

const SIDES: DebtSide[] = ['Receivable', 'Payable'];

// Nợ quá hạn: overdue open documents per partner, grouped by overdue bucket. Route permission: reports.debt.
export function DebtOverduePage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const side = parseDebtSide(searchParams.get('side'));
  const at = searchParams.get('at') || todayYmd();
  const partnerId = searchParams.get('partnerId') || undefined;
  const urlBuckets = normalizeBuckets(searchParams.get('buckets') ?? '');
  const [picked, setPicked] = useState<{ id: string; label: string } | null>(null);

  const { data, isLoading, isError, error } = useDebtOverdue(
    { side, at, partnerId, buckets: urlBuckets ?? DEFAULT_BUCKETS },
    { enabled: urlBuckets !== null },
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

  // A disabled query still returns cached data for its key; never show a report for buckets the URL rejected.
  const report = urlBuckets === null ? undefined : data;
  const groups = report?.groups ?? [];
  const matched = partnerId ? groups.find((g) => g.partnerId === partnerId) : undefined;
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
      <h1 className="text-xl font-semibold">Nợ quá hạn</h1>

      <Card>
        <CardContent className="grid gap-4 pt-6 sm:grid-cols-2 lg:grid-cols-4">
          <div className="space-y-1">
            <Label htmlFor="debt-overdue-side">Phía</Label>
            {/* A partner belongs to one side's list, so switching the side clears it. */}
            <Select value={side} onValueChange={(v) => updateParams({ side: v, partnerId: null })}>
              <SelectTrigger id="debt-overdue-side">
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
            <Label htmlFor="debt-overdue-at">Tại ngày</Label>
            <Input id="debt-overdue-at" type="date" value={at} onChange={(e) => updateParams({ at: e.target.value })} />
          </div>
          <div className="space-y-1">
            <Label htmlFor="debt-overdue-partner">Đối tượng</Label>
            <ReportPartnerPicker
              id="debt-overdue-partner"
              key={side}
              side={side}
              selectedLabel={selectedLabel}
              onSelect={selectPartner}
              onClear={() => updateParams({ partnerId: null })}
            />
          </div>
          <BucketBoundariesInput
            id="debt-overdue-buckets"
            label="Mốc quá hạn"
            rawValue={searchParams.get('buckets') ?? ''}
            onCommit={(buckets) => updateParams({ buckets })}
          />
        </CardContent>
      </Card>

      {isError ? (
        <p className="text-sm text-destructive">{getErrorMessage(error)}</p>
      ) : report ? (
        groups.length === 0 ? (
          <p className="text-sm text-muted-foreground">Không có khoản nợ quá hạn tại ngày này</p>
        ) : (
          <>
            {groups.map((group) => (
              <Card key={group.partnerId}>
                <CardContent className="space-y-3 overflow-x-auto pt-6">
                  <h2 className="font-semibold">
                    {group.partnerCode} - {group.partnerName}
                  </h2>
                  <table className="w-full min-w-[800px] text-sm">
                    <thead>
                      <tr className="border-b text-left text-muted-foreground">
                        <th className="px-2 py-2">Số CT</th>
                        <th className="px-2 py-2">Ngày CT</th>
                        <th className="px-2 py-2">Hạn TT</th>
                        <th className="px-2 py-2">Diễn giải</th>
                        <th className="px-2 py-2 text-right">Còn lại</th>
                        <th className="px-2 py-2 text-right">Số ngày quá hạn</th>
                        <th className="px-2 py-2">Nhóm</th>
                      </tr>
                    </thead>
                    <tbody>
                      {group.items.map((row) => (
                        <tr key={row.entryId} className="border-b">
                          <td className="whitespace-nowrap px-2 py-2">
                            <DocumentCodeLink sourceType={row.sourceType} sourceId={row.sourceId} code={row.docCode} />
                          </td>
                          <td className="whitespace-nowrap px-2 py-2">{formatDateOnly(row.docDate)}</td>
                          <td className="whitespace-nowrap px-2 py-2">{formatDateOnly(row.dueDate)}</td>
                          <td className="px-2 py-2">{row.description}</td>
                          <td className={NUMERIC_CELL}>{formatCurrencyVnd(row.remaining)}</td>
                          <td className={NUMERIC_CELL}>{row.daysOverdue}</td>
                          <td className="whitespace-nowrap px-2 py-2">
                            {bucketLabel(report.buckets[row.bucketIndex])}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                    <tfoot>
                      {report.buckets.map((b, i) => (
                        <tr key={i} className="text-muted-foreground">
                          <td colSpan={4} className="px-2 py-1">
                            Cộng nhóm {bucketLabel(b)}
                          </td>
                          <td className={NUMERIC_CELL}>{formatCurrencyVnd(group.amounts[i] ?? 0)}</td>
                          <td colSpan={2} />
                        </tr>
                      ))}
                      <tr className="font-semibold">
                        <td colSpan={4} className="px-2 py-2">
                          Tổng quá hạn
                        </td>
                        <td className={NUMERIC_CELL}>{formatCurrencyVnd(group.totalOverdue)}</td>
                        <td colSpan={2} />
                      </tr>
                    </tfoot>
                  </table>
                  {group.unallocated > 0 && (
                    <p className="text-sm text-muted-foreground">
                      Chưa đối trừ (tham khảo): {formatCurrencyVnd(group.unallocated)}
                    </p>
                  )}
                </CardContent>
              </Card>
            ))}
            <Card>
              <CardContent className="space-y-1 pt-6 text-sm">
                {report.buckets.map((b, i) => (
                  <div key={i} className="flex justify-between">
                    <span>Tổng nhóm {bucketLabel(b)}</span>
                    <span className="tabular-nums">{formatCurrencyVnd(report.totalAmounts[i] ?? 0)}</span>
                  </div>
                ))}
                <div className="flex justify-between font-semibold">
                  <span>Tổng quá hạn</span>
                  <span className="tabular-nums">{formatCurrencyVnd(report.totalOverdue)}</span>
                </div>
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
