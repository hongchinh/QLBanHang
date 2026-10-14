import { useState } from 'react';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { readPriceMode, writePriceMode } from '@/features/voucher-print/price-mode-storage';
import type { StockPrintPriceMode } from '@/features/voucher-print/types';
import { useAuthStore } from '@/stores/auth-store';

interface Props {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onConfirm: (mode: StockPrintPriceMode) => void;
  onCloseAutoFocus?: (event: Event) => void; // the opener is a menu item that is gone by now: say where the focus goes
}

function Body({ onConfirm, onClose }: { onConfirm: (mode: StockPrintPriceMode) => void; onClose: () => void }) {
  const userId = useAuthStore((s) => s.user?.id);
  const canViewCost = useAuthStore((s) => s.hasPermission('inventory.view_cost'));
  const [mode, setMode] = useState<StockPrintPriceMode>(() => readPriceMode(userId, canViewCost));

  const options: { value: StockPrintPriceMode; label: string }[] = [
    { value: 'Voucher', label: 'Giá trên phiếu' },
    ...(canViewCost ? [{ value: 'Cost' as const, label: 'Giá vốn' }] : []),
    { value: 'None', label: 'Không in tiền' },
  ];

  const confirm = () => {
    writePriceMode(userId, mode);
    onConfirm(mode);
    onClose();
  };

  return (
    <>
      <div className="space-y-3" role="radiogroup" aria-label="Chọn giá in">
        {options.map((o) => (
          <label key={o.value} className="flex items-center gap-2 text-sm">
            <input
              type="radio"
              name="stock-print-price-mode"
              checked={mode === o.value}
              onChange={() => setMode(o.value)}
            />
            {o.label}
          </label>
        ))}
      </div>
      <DialogFooter>
        <Button variant="outline" onClick={onClose}>
          Hủy
        </Button>
        <Button onClick={confirm}>In</Button>
      </DialogFooter>
    </>
  );
}

// Which price a stock voucher prints with. The body mounts only while open, so each opening re-reads the remembered mode.
export function StockPriceModeDialog({ open, onOpenChange, onConfirm, onCloseAutoFocus }: Props) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent
        className="max-w-sm"
        aria-describedby={undefined}
        onCloseAutoFocus={onCloseAutoFocus}
        onClick={(e) => e.stopPropagation()}>
        <DialogHeader>
          <DialogTitle>Chọn giá in</DialogTitle>
        </DialogHeader>
        <Body onConfirm={onConfirm} onClose={() => onOpenChange(false)} />
      </DialogContent>
    </Dialog>
  );
}
