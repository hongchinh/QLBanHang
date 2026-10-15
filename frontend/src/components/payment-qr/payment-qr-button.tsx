import { useState } from 'react';
import { AppIcon } from '@/components/icons';
import { Button } from '@/components/ui/button';
import type { PaymentQrKind } from '@/features/voucher-payment-qr/types';
import { PaymentQrDialog } from './payment-qr-dialog';

export interface PaymentQrButtonProps {
  kind: PaymentQrKind;
  id: string;
  code: string;
  disabled?: boolean;
  disabledReason?: string; // shown as the button title while disabled
}

// Opens the transfer QR of a voucher. The label hides on a phone; the aria-label keeps the name.
export function PaymentQrButton({ kind, id, code, disabled, disabledReason }: PaymentQrButtonProps) {
  const [open, setOpen] = useState(false);

  return (
    <>
      <Button
        type="button"
        variant="outline"
        size="sm"
        aria-label="QR chuyển khoản"
        disabled={disabled}
        title={disabled ? disabledReason : undefined}
        onClick={() => setOpen(true)}
      >
        <AppIcon name="paymentQr" size={16} />
        <span className="hidden sm:inline">QR chuyển khoản</span>
      </Button>
      <PaymentQrDialog kind={kind} id={id} voucherCode={code} open={open} onOpenChange={setOpen} />
    </>
  );
}
