import { useCallback, useEffect, useId, useRef, useState, type KeyboardEvent } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useDebtLedger } from '@/features/cash-reports/hooks';
import { useDebtPartnerSearch } from '@/features/debt/hooks';
import type { DebtPartnerSearchItem } from '@/features/debt/types';
import { DEBT_SIDE_LABELS } from '@/features/cash-common/labels';
import type { DebtSide } from '@/features/cash-common/types';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { getErrorMessage } from '@/lib/api-client';
import { useDebouncedValue } from '@/lib/use-debounced-value';
import { cn, formatCurrencyVnd } from '@/lib/utils';
import { firstDayOfMonthYmd, todayYmd } from '@/lib/vn-datetime';
import {
  DEBT_SOURCE_TYPE_LABELS,
  NUMERIC_CELL,
  formatAmountCell,
  formatDebitCredit,
  formatYmd,
  parseDebtSide,
} from './document-link';
import { DocumentCodeLink } from './document-code-link';

const SIDES: DebtSide[] = ['Receivable', 'Payable'];

interface PartnerPickerProps {
  side: DebtSide;
  selectedLabel: string;
  onSelect: (partner: DebtPartnerSearchItem) => void;
}

// Partners of the side (GET /debt/partners); an empty keyword lists the first ones on focus.
function PartnerPicker({ side, selectedLabel, onSelect }: PartnerPickerProps) {
  // null: the input shows the selected partner.
  const [keyword, setKeyword] = useState<string | null>(null);
  const [isOpen, setIsOpen] = useState(false);
  const [highlighted, setHighlighted] = useState(0);
  const debouncedKeyword = useDebouncedValue(keyword ?? '', 250);
  const { data, isLoading, isError } = useDebtPartnerSearch(side, debouncedKeyword, { includeInactive: true });
  const results = data ?? [];
  const containerRef = useRef<HTMLDivElement>(null);
  const listboxId = `debt-ledger-partners-${useId()}`;

  useEffect(() => setHighlighted(0), [data]);

  useEffect(() => {
    function onDocMouseDown(e: MouseEvent) {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) {
        setIsOpen(false);
        setKeyword(null);
      }
    }
    document.addEventListener('mousedown', onDocMouseDown);
    return () => document.removeEventListener('mousedown', onDocMouseDown);
  }, []);

  function pick(partner: DebtPartnerSearchItem) {
    onSelect(partner);
    setKeyword(null);
    setIsOpen(false);
  }

  function onKeyDown(e: KeyboardEvent<HTMLInputElement>) {
    if (e.key === 'Escape') {
      setIsOpen(false);
      setKeyword(null);
      return;
    }
    if (!isOpen || results.length === 0) return;
    if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
      e.preventDefault();
      const step = e.key === 'ArrowDown' ? 1 : -1;
      setHighlighted((i) => (i + step + results.length) % results.length);
    } else if (e.key === 'Enter') {
      e.preventDefault();
      const choice = results[highlighted];
      if (choice) pick(choice);
    }
  }

  return (
    <div className="relative" ref={containerRef}>
      <Input
        id="debt-ledger-partner"
        role="combobox"
        aria-label="Đối tượng"
        aria-expanded={isOpen}
        aria-controls={listboxId}
        aria-autocomplete="list"
        autoComplete="off"
        placeholder="Mã hoặc tên đối tượng"
        value={keyword ?? selectedLabel}
        onFocus={() => setIsOpen(true)}
        onKeyDown={onKeyDown}
        onChange={(e) => {
          setKeyword(e.target.value);
          setIsOpen(true);
        }}
      />
      {isOpen && (
        <ul
          id={listboxId}
          role="listbox"
          className="absolute left-0 z-50 mt-1 max-h-80 min-w-full overflow-auto rounded-md border bg-popover py-1 text-sm text-popover-foreground shadow-md sm:w-96"
        >
          {isLoading && <li className="px-2 py-2 text-center text-muted-foreground">Đang tìm kiếm...</li>}
          {!isLoading && isError && (
            <li className="px-2 py-2 text-center text-destructive">Không thể tải danh sách đối tượng.</li>
          )}
          {!isLoading && !isError && results.length === 0 && (
            <li className="px-2 py-2 text-center text-muted-foreground">Không tìm thấy đối tượng phù hợp</li>
          )}
          {!isLoading &&
            !isError &&
            results.map((p, i) => (
              <li
                key={p.id}
                role="option"
                aria-selected={i === highlighted}
                onMouseDown={(e) => {
                  // Avoid the blur before the selection runs.
                  e.preventDefault();
                  pick(p);
                }}
                onMouseEnter={() => setHighlighted(i)}
                className={cn('flex cursor-pointer gap-2 px-2 py-1', i === highlighted && 'qldh-lookup-row-highlight')}
              >
                <span className="font-medium">{p.code}</span>
                <span className="truncate text-muted-foreground">{p.name}</span>
              </li>
            ))}
        </ul>
      )}
    </div>
  );
}

// Sổ chi tiết công nợ of one partner and side. Route permission: reports.debt. Filters live in the URL,
// so the balance report can open it for a partner.
export function DebtLedgerPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const side = parseDebtSide(searchParams.get('side'));
  const partnerId = searchParams.get('partnerId') ?? '';
  const from = searchParams.get('from') || firstDayOfMonthYmd();
  const to = searchParams.get('to') || todayYmd();
  const [picked, setPicked] = useState<{ id: string; label: string } | null>(null);

  const rangeError = from > to ? 'Từ ngày không được sau Đến ngày' : null;

  const { data: ledger, isLoading, isError, error } = useDebtLedger(
    { side, partnerId, from, to },
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
            <PartnerPicker key={side} side={side} selectedLabel={selectedLabel} onSelect={selectPartner} />
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
            <table className="w-full min-w-[900px] text-sm">
              <thead>
                <tr className="border-b text-left text-muted-foreground">
                  <th className="px-2 py-2">Ngày</th>
                  <th className="px-2 py-2">Số CT</th>
                  <th className="px-2 py-2">Loại CT</th>
                  <th className="px-2 py-2">Diễn giải</th>
                  <th className="px-2 py-2 text-right">Tăng</th>
                  <th className="px-2 py-2 text-right">Giảm</th>
                  <th className="px-2 py-2 text-right">Số dư</th>
                </tr>
              </thead>
              <tbody>
                <tr className="border-b font-medium">
                  <td colSpan={6} className="px-2 py-2">
                    Số dư đầu kỳ
                  </td>
                  <td className={NUMERIC_CELL}>{formatDebitCredit(ledger.openingDebit, ledger.openingCredit)}</td>
                </tr>
                {ledger.rows.map((row) => (
                  <tr key={row.entryId} className="border-b">
                    <td className="whitespace-nowrap px-2 py-2">{formatYmd(row.docDate)}</td>
                    <td className="px-2 py-2">
                      <DocumentCodeLink sourceType={row.sourceType} sourceId={row.sourceId} code={row.docCode} />
                    </td>
                    <td className="px-2 py-2">{DEBT_SOURCE_TYPE_LABELS[row.sourceType]}</td>
                    <td className="px-2 py-2">{row.description}</td>
                    <td className={NUMERIC_CELL}>{formatAmountCell(row.increase)}</td>
                    <td className={NUMERIC_CELL}>{formatAmountCell(row.decrease)}</td>
                    <td className={NUMERIC_CELL}>{formatCurrencyVnd(row.runningBalance)}</td>
                  </tr>
                ))}
                {ledger.rows.length === 0 && (
                  <tr className="border-b">
                    <td colSpan={7} className="px-2 py-6 text-center text-muted-foreground">
                      Không có phát sinh trong khoảng thời gian này
                    </td>
                  </tr>
                )}
              </tbody>
              <tfoot>
                <tr className="border-b font-semibold">
                  <td colSpan={4} className="px-2 py-2">
                    Cộng phát sinh
                  </td>
                  <td className={NUMERIC_CELL}>{formatCurrencyVnd(ledger.totalIncrease)}</td>
                  <td className={NUMERIC_CELL}>{formatCurrencyVnd(ledger.totalDecrease)}</td>
                  <td />
                </tr>
                <tr className="font-semibold">
                  <td colSpan={6} className="px-2 py-2">
                    Số dư cuối kỳ
                  </td>
                  <td className={NUMERIC_CELL}>{formatDebitCredit(ledger.closingDebit, ledger.closingCredit)}</td>
                </tr>
              </tfoot>
            </table>
          </CardContent>
        </Card>
      ) : (
        isLoading && <p className="text-sm text-muted-foreground">Đang tải…</p>
      )}
    </div>
  );
}
