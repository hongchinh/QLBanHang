import { useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { format } from 'date-fns';
import { Undo2 } from 'lucide-react';
import type { DebtSide, DebtSourceType, SettlementOrigin } from '@/features/cash-common/types';
import { useDebtSettlements, useDeleteSettlement, useDeleteSettlementBatch } from '@/features/debt/hooks';
import type { DebtSettlement } from '@/features/debt/types';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { getErrorMessage } from '@/lib/api-client';
import { roundAwayFromZero } from '@/lib/round';
import { toast } from '@/lib/use-toast';

const SETTLEMENT_ORIGIN_LABELS: Record<SettlementOrigin, string> = {
  AtVoucher: 'Lúc lập phiếu',
  Manual: 'Thủ công',
  Offset: 'Bù trừ',
  Auto: 'Tự động',
};

const fmt = new Intl.NumberFormat('vi-VN');
const NUMERIC_CELL = 'px-2 py-2 text-right tabular-nums';

// Auto settlements belong to the stock voucher and Offset ones to the offset voucher (P22).
const isUnmatchable = (origin: SettlementOrigin) => origin === 'Manual' || origin === 'AtVoucher';

function formatYmd(ymd: string): string {
  const [y, m, d] = ymd.slice(0, 10).split('-');
  return `${d}/${m}/${y}`;
}

function formatInstant(iso: string): string {
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? iso : format(d, 'dd/MM/yyyy HH:mm');
}

interface Batch {
  batchId: string;
  origin: SettlementOrigin;
  effectiveAt: string;
  createdByName?: string | null;
  total: number;
  settlements: DebtSettlement[];
}

// The server lists newest first; a batch keeps the position of its first settlement.
function groupByBatch(settlements: DebtSettlement[]): Batch[] {
  const batches = new Map<string, Batch>();
  for (const s of settlements) {
    let batch = batches.get(s.batchId);
    if (!batch) {
      batch = {
        batchId: s.batchId,
        origin: s.origin,
        effectiveAt: s.effectiveAt,
        createdByName: s.createdByName,
        total: 0,
        settlements: [],
      };
      batches.set(s.batchId, batch);
    }
    batch.settlements.push(s);
    batch.total = roundAwayFromZero(batch.total + s.amount, 2);
  }
  return [...batches.values()];
}

type PendingUnmatch = { kind: 'one'; settlement: DebtSettlement } | { kind: 'batch'; batch: Batch };

export interface SettlementListProps {
  partnerId: string;
  side: DebtSide;
  // Screen of a document; without it the codes are plain text.
  docHref?: (sourceType: DebtSourceType, sourceId: string) => string;
}

// Tab "Đã đối trừ": settlements of the partner and side, grouped by batch. Only Manual and AtVoucher ones can be
// removed here; the server refuses the others (409 SETTLEMENT_MANAGED) and a locked period (400 PERIOD_LOCKED).
export function SettlementList({ partnerId, side, docHref }: SettlementListProps) {
  const { data, isLoading, isError, error } = useDebtSettlements({ partnerId, side }, { enabled: !!partnerId });
  const deleteOne = useDeleteSettlement();
  const deleteBatch = useDeleteSettlementBatch();
  const [pending, setPending] = useState<PendingUnmatch | null>(null);
  const batches = useMemo(() => groupByBatch(data ?? []), [data]);

  const callbacks = {
    onSuccess: () => {
      setPending(null);
      toast({ variant: 'success', title: 'Đã bỏ đối trừ' });
    },
    onError: (err: unknown) => {
      setPending(null);
      toast({ variant: 'destructive', title: 'Không bỏ được đối trừ', description: getErrorMessage(err) });
    },
  };

  const confirmUnmatch = () => {
    if (!pending) return;
    if (pending.kind === 'one') deleteOne.mutate(pending.settlement.id, callbacks);
    else deleteBatch.mutate(pending.batch.batchId, callbacks);
  };

  const docCell = (code: string, ymd: string, sourceType: DebtSourceType, sourceId: string) => (
    <td className="px-2 py-2">
      {docHref ? (
        <Link to={docHref(sourceType, sourceId)} className="font-medium text-blue-700 hover:underline">
          {code}
        </Link>
      ) : (
        <span className="font-medium">{code}</span>
      )}
      <span className="ml-2 text-muted-foreground tabular-nums">{formatYmd(ymd)}</span>
    </td>
  );

  if (isError) {
    return (
      <div className="rounded-md border border-destructive/30 bg-destructive/10 p-3 text-sm text-destructive">
        {getErrorMessage(error)}
      </div>
    );
  }

  return (
    <>
      <div className="overflow-x-auto rounded-md border">
        <table className="w-full min-w-[640px] text-sm">
          <thead className="bg-muted/50 text-left">
            <tr>
              <th className="px-2 py-2 font-medium">Chứng từ ghi nợ</th>
              <th className="px-2 py-2 font-medium">Chứng từ giảm nợ</th>
              <th className="px-2 py-2 text-right font-medium">Số tiền</th>
              <th className="w-48 px-2 py-2" aria-label="Thao tác" />
            </tr>
          </thead>
          {batches.map((batch) => {
            const unmatchable = isUnmatchable(batch.origin);
            return (
              <tbody key={batch.batchId} data-testid={`settlement-batch-${batch.batchId}`} className="border-t">
                <tr className="bg-muted/30">
                  <td colSpan={2} className="px-2 py-2">
                    <span className="flex flex-wrap items-center gap-2">
                      <Badge variant={unmatchable ? 'secondary' : 'outline'}>
                        {SETTLEMENT_ORIGIN_LABELS[batch.origin]}
                      </Badge>
                      <span className="tabular-nums">{formatInstant(batch.effectiveAt)}</span>
                      {batch.createdByName && <span className="text-muted-foreground">{batch.createdByName}</span>}
                    </span>
                  </td>
                  <td className={`${NUMERIC_CELL} font-semibold`} data-testid="batch-total">
                    {fmt.format(batch.total)}
                  </td>
                  <td className="px-2 py-2 text-right">
                    {unmatchable && (
                      <Button variant="outline" size="sm" onClick={() => setPending({ kind: 'batch', batch })}>
                        <Undo2 className="mr-1 h-4 w-4 text-red-600" />
                        Bỏ cả lô
                      </Button>
                    )}
                  </td>
                </tr>
                {batch.settlements.map((s) => (
                  <tr key={s.id} className="border-t">
                    {docCell(s.increaseDocCode, s.increaseDocDate, s.increaseSourceType, s.increaseSourceId)}
                    {docCell(s.decreaseDocCode, s.decreaseDocDate, s.decreaseSourceType, s.decreaseSourceId)}
                    <td className={NUMERIC_CELL}>{fmt.format(s.amount)}</td>
                    <td className="px-2 py-2 text-right">
                      {unmatchable ? (
                        <Button variant="ghost" size="sm" onClick={() => setPending({ kind: 'one', settlement: s })}>
                          Bỏ
                        </Button>
                      ) : (
                        <span className="text-xs italic text-muted-foreground">Do chứng từ quản lý</span>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            );
          })}
          {batches.length === 0 && (
            <tbody>
              <tr>
                <td colSpan={4} className="h-20 text-center text-muted-foreground">
                  {isLoading ? 'Đang tải...' : 'Chưa có đối trừ nào.'}
                </td>
              </tr>
            </tbody>
          )}
        </table>
      </div>

      <ConfirmDialog
        open={pending !== null}
        onOpenChange={(open) => {
          if (!open) setPending(null);
        }}
        title={pending?.kind === 'batch' ? 'Bỏ cả lô đối trừ?' : 'Bỏ đối trừ?'}
        description={
          pending?.kind === 'batch'
            ? `Bỏ ${pending.batch.settlements.length} dòng đối trừ của lô, tổng ${fmt.format(pending.batch.total)}.`
            : pending
              ? `Bỏ đối trừ ${pending.settlement.increaseDocCode} – ${pending.settlement.decreaseDocCode}, số tiền ${fmt.format(pending.settlement.amount)}.`
              : undefined
        }
        confirmLabel="Bỏ đối trừ"
        destructive
        loading={deleteOne.isPending || deleteBatch.isPending}
        onConfirm={confirmUnmatch}
      />
    </>
  );
}
