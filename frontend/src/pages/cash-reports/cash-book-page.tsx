import { useId, useState } from 'react';
import { Link } from 'react-router-dom';
import { format } from 'date-fns';
import { useCashBook } from '@/features/cash-reports/hooks';
import type { CashBookGroup, CashBookRow, FundKindFilter } from '@/features/cash-reports/types';
import { useFunds } from '@/features/funds/hooks';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { getErrorMessage } from '@/lib/api-client';
import { formatCurrencyVnd } from '@/lib/utils';
import { firstDayOfMonthYmd, todayYmd } from '@/lib/vn-datetime';
import { NUMERIC_CELL, documentLink, formatAmountCell } from './document-link';

const ALL = 'all';

const KIND_OPTIONS: { value: FundKindFilter; label: string }[] = [
  { value: 'All', label: 'Tất cả' },
  { value: 'Cash', label: 'Tiền mặt' },
  { value: 'Bank', label: 'Ngân hàng' },
];

function CodeLink({ row, code }: { row: CashBookRow; code: string | null | undefined }) {
  if (!code) return null;
  return (
    <Link to={documentLink(row.sourceType, row.sourceId)} className="text-primary hover:underline">
      {code}
    </Link>
  );
}

function FundGroupSection({ group }: { group: CashBookGroup }) {
  const headingId = useId();
  const bankInfo = [group.bankName, group.accountNumber && `STK ${group.accountNumber}`].filter(Boolean).join(' · ');
  return (
    <section aria-labelledby={headingId}>
      <Card>
        <CardContent className="space-y-3 pt-6">
          <div>
            <h2 id={headingId} className="text-base font-semibold">
              {group.fundCode} – {group.fundName}
            </h2>
            {bankInfo && <p className="text-sm text-muted-foreground">{bankInfo}</p>}
          </div>
          <div className="overflow-x-auto">
            <table className="w-full min-w-[960px] text-sm">
              <thead>
                <tr className="border-b text-left text-muted-foreground">
                  <th className="px-2 py-2">Ngày</th>
                  <th className="px-2 py-2">Số phiếu thu</th>
                  <th className="px-2 py-2">Số phiếu chi</th>
                  <th className="px-2 py-2">Đối tượng/Người nộp-nhận</th>
                  <th className="px-2 py-2">Diễn giải</th>
                  <th className="px-2 py-2 text-right">Thu</th>
                  <th className="px-2 py-2 text-right">Chi</th>
                  <th className="px-2 py-2 text-right">Tồn</th>
                </tr>
              </thead>
              <tbody>
                <tr className="border-b font-medium">
                  <td colSpan={7} className="px-2 py-2">
                    Tồn đầu kỳ
                  </td>
                  <td className={NUMERIC_CELL}>{formatCurrencyVnd(group.opening)}</td>
                </tr>
                {group.rows.map((row) => (
                  <tr key={`${row.sourceType}-${row.sourceId}`} className="border-b">
                    <td className="whitespace-nowrap px-2 py-2">{format(new Date(row.at), 'dd/MM/yyyy')}</td>
                    <td className="px-2 py-2">
                      <CodeLink row={row} code={row.sourceType === 'TransferIn' ? row.transferCode : row.receiptCode} />
                    </td>
                    <td className="px-2 py-2">
                      <CodeLink row={row} code={row.sourceType === 'TransferOut' ? row.transferCode : row.paymentCode} />
                    </td>
                    <td className="px-2 py-2">{row.partnerOrPerson}</td>
                    <td className="px-2 py-2">{row.description}</td>
                    <td className={NUMERIC_CELL}>{formatAmountCell(row.in)}</td>
                    <td className={NUMERIC_CELL}>{formatAmountCell(row.out)}</td>
                    <td className={NUMERIC_CELL}>{formatCurrencyVnd(row.running)}</td>
                  </tr>
                ))}
                {group.rows.length === 0 && (
                  <tr className="border-b">
                    <td colSpan={8} className="px-2 py-4 text-center text-muted-foreground">
                      Không có phát sinh trong khoảng thời gian này
                    </td>
                  </tr>
                )}
              </tbody>
              <tfoot>
                <tr className="border-b font-semibold">
                  <td colSpan={5} className="px-2 py-2">
                    Cộng
                  </td>
                  <td className={NUMERIC_CELL}>{formatCurrencyVnd(group.totalIn)}</td>
                  <td className={NUMERIC_CELL}>{formatCurrencyVnd(group.totalOut)}</td>
                  <td />
                </tr>
                <tr className="font-semibold">
                  <td colSpan={7} className="px-2 py-2">
                    Tồn cuối kỳ
                  </td>
                  <td className={NUMERIC_CELL}>{formatCurrencyVnd(group.closing)}</td>
                </tr>
              </tfoot>
            </table>
          </div>
        </CardContent>
      </Card>
    </section>
  );
}

// Sổ quỹ. Route permission: reports.cash. One section per fund; grand totals come from the server (P25).
export function CashBookPage() {
  const [kind, setKind] = useState<FundKindFilter>('All');
  const [fundId, setFundId] = useState(ALL);
  const [from, setFrom] = useState(() => firstDayOfMonthYmd());
  const [to, setTo] = useState(() => todayYmd());
  const { data: funds = [] } = useFunds({ kind: kind === 'All' ? undefined : kind });

  const rangeError = !from || !to ? 'Chọn đủ Từ ngày và Đến ngày' : from > to ? 'Từ ngày không được sau Đến ngày' : null;

  const {
    data: report,
    isLoading,
    isError,
    error,
  } = useCashBook({ kind, fundId: fundId === ALL ? undefined : fundId, from, to }, { enabled: !rangeError });

  // A fund of the other kind would be rejected by the server, so a kind change goes back to all funds.
  const changeKind = (value: string) => {
    setKind(value as FundKindFilter);
    setFundId(ALL);
  };

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">Sổ quỹ</h1>

      <Card>
        <CardContent className="grid gap-4 pt-6 sm:grid-cols-2 lg:grid-cols-4">
          <div className="space-y-1">
            <Label htmlFor="cash-book-kind">Loại quỹ</Label>
            <Select value={kind} onValueChange={changeKind}>
              <SelectTrigger id="cash-book-kind">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {KIND_OPTIONS.map((o) => (
                  <SelectItem key={o.value} value={o.value}>
                    {o.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-1">
            <Label htmlFor="cash-book-fund">Quỹ</Label>
            <Select value={fundId} onValueChange={setFundId}>
              <SelectTrigger id="cash-book-fund">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={ALL}>Tất cả</SelectItem>
                {funds.map((f) => (
                  <SelectItem key={f.id} value={f.id}>
                    {f.code} – {f.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-1">
            <Label htmlFor="cash-book-from">Từ ngày</Label>
            <Input id="cash-book-from" type="date" value={from} onChange={(e) => setFrom(e.target.value)} />
          </div>
          <div className="space-y-1">
            <Label htmlFor="cash-book-to">Đến ngày</Label>
            <Input id="cash-book-to" type="date" value={to} onChange={(e) => setTo(e.target.value)} />
          </div>
        </CardContent>
      </Card>

      {rangeError ? (
        <p className="text-sm text-destructive">{rangeError}</p>
      ) : isError ? (
        <p className="text-sm text-destructive">{getErrorMessage(error)}</p>
      ) : report ? (
        <>
          {report.groups.map((g) => (
            <FundGroupSection key={g.fundId} group={g} />
          ))}
          {report.groups.length === 0 && <p className="text-sm text-muted-foreground">Không có quỹ phù hợp</p>}
          {report.groups.length >= 2 && (
            <Card data-testid="cash-book-grand-total">
              <CardContent className="space-y-3 pt-6">
                <h2 className="text-base font-semibold">Tổng cộng các quỹ</h2>
                <div className="grid gap-4 text-sm sm:grid-cols-4">
                  {[
                    { label: 'Tồn đầu kỳ', value: report.grandOpening },
                    { label: 'Tổng thu', value: report.grandIn },
                    { label: 'Tổng chi', value: report.grandOut },
                    { label: 'Tồn cuối kỳ', value: report.grandClosing },
                  ].map((s) => (
                    <div key={s.label}>
                      <div className="text-muted-foreground">{s.label}</div>
                      <div className="font-semibold tabular-nums">{formatCurrencyVnd(s.value)}</div>
                    </div>
                  ))}
                </div>
                <p className="text-xs text-muted-foreground">Không tính chuyển quỹ nội bộ giữa các quỹ đang xem</p>
              </CardContent>
            </Card>
          )}
        </>
      ) : (
        isLoading && <p className="text-sm text-muted-foreground">Đang tải…</p>
      )}
    </div>
  );
}
