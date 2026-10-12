import { useCallback, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { useDebtAging } from '@/features/cash-reports/hooks';
import { DEFAULT_BUCKETS, bucketLabel, normalizeBuckets } from '@/features/cash-reports/aging';
import type { DebtAgingRow } from '@/features/cash-reports/types';
import type { DebtPartnerSearchItem } from '@/features/debt/types';
import { DEBT_SIDE_LABELS } from '@/features/cash-common/labels';
import type { DebtSide } from '@/features/cash-common/types';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { getErrorMessage } from '@/lib/api-client';
import { formatCurrencyVnd } from '@/lib/utils';
import { todayYmd } from '@/lib/vn-datetime';
import { NUMERIC_CELL, formatSignedBalance, parseDebtSide } from './document-link';
import { ReportPartnerPicker } from './report-partner-picker';

const SIDES: DebtSide[] = ['Receivable', 'Payable'];
const BUCKETS_ERROR = 'Mốc không hợp lệ: các số nguyên dương tăng dần, cách nhau bởi dấu phẩy, tối đa 6 mốc';

// Tuổi nợ: open debt per partner by age bucket as at a date. Route permission: reports.debt.
// Filters live in the URL; the bucket boundaries are typed here and validated before they reach the API.
export function DebtAgingPage() {
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const side = parseDebtSide(searchParams.get('side'));
  const at = searchParams.get('at') || todayYmd();
  const partnerId = searchParams.get('partnerId') || undefined;
  const urlBuckets = normalizeBuckets(searchParams.get('buckets') ?? '');
  const [picked, setPicked] = useState<{ id: string; label: string } | null>(null);
  const [bucketsText, setBucketsText] = useState(searchParams.get('buckets') ?? DEFAULT_BUCKETS);
  const [typedInvalid, setTypedInvalid] = useState(false);
  // The page stays mounted when only the query string changes (e.g. the sidebar link), so re-derive the
  // typed text from the URL whenever its raw value changes.
  const rawBuckets = searchParams.get('buckets') ?? '';
  const [syncedBuckets, setSyncedBuckets] = useState(rawBuckets);
  if (syncedBuckets !== rawBuckets) {
    setSyncedBuckets(rawBuckets);
    setBucketsText(rawBuckets || DEFAULT_BUCKETS);
    setTypedInvalid(false);
  }

  const { data, isLoading, isError, error } = useDebtAging(
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

  const commitBuckets = () => {
    const normalized = normalizeBuckets(bucketsText);
    if (normalized === null) {
      setTypedInvalid(true);
      return;
    }
    setTypedInvalid(false);
    setBucketsText(normalized);
    updateParams({ buckets: normalized });
  };

  // A disabled query still returns cached data for its key; never show a report for buckets the URL rejected.
  const report = urlBuckets === null ? undefined : data;
  const rows = report?.rows ?? [];
  const matched = partnerId ? rows.find((r) => r.partnerId === partnerId) : undefined;
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

  const openDocuments = (row: DebtAgingRow) => {
    if (!row.partnerId) return;
    const params = new URLSearchParams({ side, at, partnerId: row.partnerId });
    navigate(`/reports/debt-documents?${params.toString()}`);
  };

  const amountCells = (row: DebtAgingRow) => (
    <>
      {row.amounts.map((amount, i) => (
        <td key={i} className={NUMERIC_CELL}>
          {formatCurrencyVnd(amount)}
        </td>
      ))}
      <td className={NUMERIC_CELL}>{formatCurrencyVnd(row.totalOpen)}</td>
      <td className={NUMERIC_CELL}>{formatCurrencyVnd(row.unallocated)}</td>
      <td className={NUMERIC_CELL}>{formatSignedBalance(side, row.balance)}</td>
    </>
  );

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">Tuổi nợ</h1>

      <Card>
        <CardContent className="grid gap-4 pt-6 sm:grid-cols-2 lg:grid-cols-4">
          <div className="space-y-1">
            <Label htmlFor="debt-aging-side">Phía</Label>
            {/* A partner belongs to one side's list, so switching the side clears it. */}
            <Select value={side} onValueChange={(v) => updateParams({ side: v, partnerId: null })}>
              <SelectTrigger id="debt-aging-side">
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
            <Label htmlFor="debt-aging-at">Tại ngày</Label>
            <Input id="debt-aging-at" type="date" value={at} onChange={(e) => updateParams({ at: e.target.value })} />
          </div>
          <div className="space-y-1">
            <Label htmlFor="debt-aging-partner">Đối tượng</Label>
            <ReportPartnerPicker
              id="debt-aging-partner"
              key={side}
              side={side}
              selectedLabel={selectedLabel}
              onSelect={selectPartner}
              onClear={() => updateParams({ partnerId: null })}
            />
          </div>
          <div className="space-y-1">
            <Label htmlFor="debt-aging-buckets">Mốc tuổi nợ</Label>
            <Input
              id="debt-aging-buckets"
              value={bucketsText}
              onChange={(e) => setBucketsText(e.target.value)}
              onBlur={commitBuckets}
              onKeyDown={(e) => {
                if (e.key === 'Enter') commitBuckets();
              }}
            />
          </div>
        </CardContent>
      </Card>

      {(typedInvalid || urlBuckets === null) && <p className="text-sm text-destructive">{BUCKETS_ERROR}</p>}

      {isError ? (
        <p className="text-sm text-destructive">{getErrorMessage(error)}</p>
      ) : report ? (
        rows.length === 0 ? (
          <p className="text-sm text-muted-foreground">Không có công nợ tại ngày này</p>
        ) : (
          <Card>
            <CardContent className="overflow-x-auto pt-6">
              <table className="w-full min-w-[900px] text-sm">
                <thead>
                  <tr className="border-b text-left text-muted-foreground">
                    <th className="px-2 py-2">Mã</th>
                    <th className="px-2 py-2">Tên</th>
                    {report.buckets.map((b, i) => (
                      <th key={i} className="px-2 py-2 text-right">
                        {bucketLabel(b)}
                      </th>
                    ))}
                    <th className="px-2 py-2 text-right">Tổng còn nợ</th>
                    <th className="px-2 py-2 text-right">Chưa đối trừ</th>
                    <th className="px-2 py-2 text-right">Số dư</th>
                  </tr>
                </thead>
                <tbody>
                  {rows.map((row) => (
                    <tr
                      key={row.partnerId ?? row.partnerCode}
                      className="cursor-pointer border-b hover:bg-muted/50"
                      onClick={() => openDocuments(row)}
                    >
                      <td className="px-2 py-2 font-medium">{row.partnerCode}</td>
                      <td className="px-2 py-2">{row.partnerName}</td>
                      {amountCells(row)}
                    </tr>
                  ))}
                </tbody>
                <tfoot>
                  <tr className="font-semibold">
                    <td colSpan={2} className="px-2 py-2">
                      Tổng cộng
                    </td>
                    {amountCells(report.totals)}
                  </tr>
                </tfoot>
              </table>
            </CardContent>
          </Card>
        )
      ) : (
        isLoading && <p className="text-sm text-muted-foreground">Đang tải…</p>
      )}
    </div>
  );
}
