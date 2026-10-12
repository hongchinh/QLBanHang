import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useSetFundOpeningBalance } from '@/features/funds/hooks';
import type { Fund } from '@/features/funds/types';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { ConfirmRequiredDialog } from '@/components/confirm-required-dialog/confirm-required-dialog';
import { formatApiErrorDetails, getApiError } from '@/lib/api-client';
import {
  toConfirmationRequest,
  withConfirmed,
  type ConfirmationRequest,
  type ConfirmFlags,
} from '@/lib/confirm-required';
import { formatMoneyForDisplay, parseMoneyInput } from '@/lib/money-input';
import { toast } from '@/lib/use-toast';

interface Props {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  fund?: Fund;
}

// A 422 waiting for the user: the amount and the flags already confirmed are resent on "continue" (P10).
interface PendingConfirmation {
  request: ConfirmationRequest;
  amount: number;
  flags: ConfirmFlags;
}

// Sets the fund's opening amount, dated at the branch opening date by the server (P8).
export function FundOpeningBalanceDialog({ open, onOpenChange, fund }: Props) {
  const navigate = useNavigate();
  const save = useSetFundOpeningBalance();
  const [text, setText] = useState('');
  const [error, setError] = useState<string | undefined>();
  const [pending, setPending] = useState<PendingConfirmation | null>(null);

  useEffect(() => {
    if (open) {
      setText(formatMoneyForDisplay(fund?.openingAmount ?? 0));
      setError(undefined);
      setPending(null);
    }
  }, [open, fund]);

  const submit = async (amount: number, flags: ConfirmFlags) => {
    if (!fund) return;
    try {
      await save.mutateAsync({
        id: fund.id,
        data: { amount, acknowledgeNegativeFund: flags.acknowledgeNegativeFund ?? false },
      });
      toast({ variant: 'success', title: 'Đã lưu số dư đầu kỳ', description: `${fund.code} — ${fund.name}` });
      onOpenChange(false);
    } catch (err) {
      const request = toConfirmationRequest(err);
      if (request) {
        setPending({ request, amount, flags });
        return;
      }
      const apiError = getApiError(err);
      if (apiError?.code === 'OPENING_DATE_NOT_SET') {
        toast({
          variant: 'destructive',
          title: 'Chưa đặt ngày đầu kỳ',
          description: apiError.message,
          action: { label: 'Mở Cài đặt → Chi nhánh', onClick: () => navigate('/settings/branches') },
        });
        return;
      }
      setError(apiError?.details?.amount?.[0]);
      toast({ variant: 'destructive', title: 'Không thể lưu', description: formatApiErrorDetails(err) });
    }
  };

  const onSave = () => {
    const amount = parseMoneyInput(text);
    if (amount === undefined) {
      setError('Nhập số tiền hợp lệ');
      return;
    }
    if (amount < 0) {
      setError('Số dư đầu kỳ không được âm');
      return;
    }
    setError(undefined);
    void submit(amount, {});
  };

  const onConfirm = () => {
    if (!pending) return;
    const { request, amount, flags } = pending;
    setPending(null);
    void submit(amount, withConfirmed(flags, request));
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>Số dư đầu kỳ</DialogTitle>
          <DialogDescription>
            {fund ? `${fund.code} — ${fund.name}. ` : ''}Số dư tại ngày đầu kỳ của chi nhánh.
          </DialogDescription>
        </DialogHeader>

        <form
          className="space-y-4 pt-2"
          onSubmit={(e) => {
            e.preventDefault();
            onSave();
          }}
        >
          <div className="space-y-2">
            <Label htmlFor="openingAmount">Số dư đầu kỳ *</Label>
            <Input
              id="openingAmount"
              inputMode="decimal"
              className="text-right tabular-nums"
              value={text}
              onChange={(e) => setText(e.target.value)}
              onBlur={() => {
                const amount = parseMoneyInput(text);
                if (amount !== undefined) setText(formatMoneyForDisplay(amount));
              }}
            />
            {error && <p className="text-sm text-destructive">{error}</p>}
          </div>

          <div className="flex justify-end gap-2 pt-2">
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)} disabled={save.isPending}>
              Hủy
            </Button>
            <Button type="submit" disabled={save.isPending}>
              {save.isPending ? 'Đang lưu...' : 'Lưu'}
            </Button>
          </div>
        </form>

        <ConfirmRequiredDialog
          request={pending?.request ?? null}
          onConfirm={onConfirm}
          onClose={() => setPending(null)}
        />
      </DialogContent>
    </Dialog>
  );
}
