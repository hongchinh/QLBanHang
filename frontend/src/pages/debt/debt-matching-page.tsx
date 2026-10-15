import { useCallback, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { AppIcon } from '@/components/icons';
import { DEBT_SIDE_LABELS } from '@/features/cash-common/labels';
import type { DebtSide } from '@/features/cash-common/types';
import { useCreateSettlementBatch, useOpenItems } from '@/features/debt/hooks';
import { pairAllocations, suggestMatching, type MatchRow } from '@/features/debt/matching';
import type { OpenItem } from '@/features/debt/types';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Label } from '@/components/ui/label';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { formatApiErrorDetails, getErrorMessage } from '@/lib/api-client';
import { roundAwayFromZero } from '@/lib/round';
import { toast } from '@/lib/use-toast';
import { DebtPartnerPicker } from './components/debt-partner-picker';
import { OpenItemGrid } from './components/open-item-grid';
import { SettlementList } from './components/settlement-list';

const SIDES: DebtSide[] = ['Receivable', 'Payable'];
const fmt = new Intl.NumberFormat('vi-VN');

const sumOf = (rows: MatchRow[]) =>
  roundAwayFromZero(
    rows.reduce((sum, row) => sum + row.amount, 0),
    2,
  );

// Rows of entries still listed: an entry gone after a refresh (settled elsewhere) drops out of the selection.
function listedRows(rows: MatchRow[], items: OpenItem[]): MatchRow[] {
  const ids = new Set(items.map((item) => item.entryId));
  return rows.filter((row) => ids.has(row.entryId));
}

function exceedsOpen(rows: MatchRow[], items: OpenItem[]): boolean {
  const open = new Map(items.map((item) => [item.entryId, item.open]));
  return rows.some((row) => row.amount > (open.get(row.entryId) ?? 0));
}

const toAllocatable = (items: OpenItem[]) => items.map((item) => ({ ...item, dueDate: item.dueDate ?? null }));

// The two grids of one partner and side. Remounted per partner and side, so the selection never carries over.
function MatchingPanel({ partnerId, side }: { partnerId: string; side: DebtSide }) {
  const decrease = useOpenItems({ partnerId, side, direction: 'Decrease' });
  const increase = useOpenItems({ partnerId, side, direction: 'Increase' });
  const create = useCreateSettlementBatch();
  const [leftRows, setLeftRows] = useState<MatchRow[]>([]);
  const [rightRows, setRightRows] = useState<MatchRow[]>([]);

  const decreaseItems = decrease.data ?? [];
  const increaseItems = increase.data ?? [];
  const left = listedRows(leftRows, decreaseItems);
  const right = listedRows(rightRows, increaseItems);
  const leftSum = sumOf(left);
  const rightSum = sumOf(right);
  const overOpen = exceedsOpen(left, decreaseItems) || exceedsOpen(right, increaseItems);
  const canSave = leftSum > 0 && leftSum === rightSum && !overOpen && !create.isPending;
  const loadError = decrease.error ?? increase.error;

  const clear = () => {
    setLeftRows([]);
    setRightRows([]);
  };

  const suggest = () => {
    const suggestion = suggestMatching(toAllocatable(decreaseItems), toAllocatable(increaseItems));
    setLeftRows(suggestion.decreases);
    setRightRows(suggestion.increases);
  };

  const save = () => {
    let items;
    try {
      items = pairAllocations(left, right);
    } catch (err) {
      toast({ variant: 'destructive', title: 'Không đối trừ được', description: getErrorMessage(err) });
      return;
    }
    create.mutate(
      { partnerId, side, items },
      {
        onSuccess: () => {
          clear();
          toast({ variant: 'success', title: 'Đã đối trừ chứng từ' });
        },
        // 400: the `items[i].*` details name the rejected pairs.
        onError: (err) =>
          toast({ variant: 'destructive', title: 'Không đối trừ được', description: formatApiErrorDetails(err) }),
      },
    );
  };

  return (
    <div className="space-y-3">
      {loadError && (
        <div className="rounded-md border border-destructive/30 bg-destructive/10 p-3 text-sm text-destructive">
          {getErrorMessage(loadError)}
        </div>
      )}
      <div className="grid gap-4 lg:grid-cols-2">
        <OpenItemGrid
          title="Chứng từ giảm nợ chưa đối trừ"
          items={decreaseItems}
          value={left}
          onChange={setLeftRows}
          isLoading={decrease.isLoading}
        />
        <OpenItemGrid
          title="Chứng từ ghi nợ còn nợ"
          items={increaseItems}
          value={right}
          onChange={setRightRows}
          isLoading={increase.isLoading}
        />
      </div>
      <div className="flex flex-wrap items-center justify-between gap-3 border-t pt-3 text-sm">
        <div className="flex flex-wrap items-baseline gap-x-4 gap-y-1">
          <span>
            Σ giảm nợ:{' '}
            <span data-testid="sum-decrease" className="font-semibold tabular-nums">
              {fmt.format(leftSum)}
            </span>
          </span>
          <span>
            Σ ghi nợ:{' '}
            <span data-testid="sum-increase" className="font-semibold tabular-nums">
              {fmt.format(rightSum)}
            </span>
          </span>
          {leftSum !== rightSum && (
            <span role="status" className="text-destructive">
              Hai bên chưa bằng nhau
            </span>
          )}
        </div>
        <div className="flex flex-wrap gap-2">
          <Button
            type="button"
            variant="outline"
            onClick={suggest}
            disabled={decreaseItems.length === 0 || increaseItems.length === 0}
          >
            <AppIcon name="autoMatch" className="mr-1" />
            Gợi ý
          </Button>
          <Button type="button" variant="outline" onClick={clear} disabled={left.length === 0 && right.length === 0}>
            <AppIcon name="clearMatches" className="mr-1" />
            Xóa
          </Button>
          <Button type="button" onClick={save} disabled={!canSave}>
            <AppIcon name="applyMatching" className="mr-1" />
            {create.isPending ? 'Đang lưu...' : 'Đối trừ'}
          </Button>
        </div>
      </div>
    </div>
  );
}

// Đối trừ chứng từ (route debt/matching, permission debt.matching). The URL keeps the side and the partner.
export function DebtMatchingPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const side: DebtSide = searchParams.get('side') === 'Payable' ? 'Payable' : 'Receivable';
  const partnerId = searchParams.get('partnerId') ?? '';
  const partnerName = searchParams.get('partnerName') ?? '';

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

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">Đối trừ chứng từ</h1>

      <Card>
        <CardContent className="grid gap-4 pt-6 sm:grid-cols-2 lg:grid-cols-3">
          <div className="space-y-1">
            <span className="text-sm font-medium">Phía</span>
            {/* A partner belongs to one side's list, so switching the side clears it. */}
            <div role="group" aria-label="Phía" className="flex gap-2">
              {SIDES.map((s) => (
                <Button
                  key={s}
                  type="button"
                  variant={s === side ? 'default' : 'outline'}
                  aria-pressed={s === side}
                  onClick={() => {
                    if (s !== side) updateParams({ side: s, partnerId: null, partnerName: null });
                  }}
                >
                  {DEBT_SIDE_LABELS[s]}
                </Button>
              ))}
            </div>
          </div>
          <div className="space-y-1 lg:col-span-2">
            <Label htmlFor="debt-matching-partner">Đối tượng</Label>
            <DebtPartnerPicker
              key={side}
              id="debt-matching-partner"
              side={side}
              selectedLabel={partnerId ? partnerName : ''}
              onSelect={(p) => updateParams({ side, partnerId: p.id, partnerName: `${p.code} - ${p.name}` })}
            />
          </div>
        </CardContent>
      </Card>

      {partnerId ? (
        <Tabs defaultValue="open">
          <TabsList>
            <TabsTrigger value="open">Chưa đối trừ</TabsTrigger>
            <TabsTrigger value="settled">Đã đối trừ</TabsTrigger>
          </TabsList>
          {/* Kept mounted, so the amounts typed survive a look at the other tab. */}
          <TabsContent value="open" forceMount className="data-[state=inactive]:hidden">
            <MatchingPanel key={`${side}:${partnerId}`} partnerId={partnerId} side={side} />
          </TabsContent>
          <TabsContent value="settled">
            <SettlementList partnerId={partnerId} side={side} />
          </TabsContent>
        </Tabs>
      ) : (
        <p className="text-sm text-muted-foreground">Chọn đối tượng để đối trừ chứng từ.</p>
      )}
    </div>
  );
}
