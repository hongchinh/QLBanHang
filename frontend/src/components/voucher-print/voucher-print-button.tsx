import { useEffect, useRef, useState } from 'react';
import { AppIcon } from '@/components/icons';
import { ButtonLoader } from '@/components/ui/button-loader';
import { Button } from '@/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { CompanyProfileHint } from '@/components/tt133-export/company-profile-hint';
import { StockPriceModeDialog } from './stock-price-mode-dialog';
import { canTakeFocus, giveFocusBack, isFocusLost } from '@/lib/focus';
import { useVoucherPrint } from '@/features/voucher-print/use-voucher-print';
import type { StockPrintPriceMode, VoucherPrintKind, VoucherPrintTarget } from '@/features/voucher-print/types';
import type { ExportFormat } from '@/features/tt133-export/types';
import type { CashVoucherType } from '@/features/cash-common/types';
import type { StockDirection } from '@/features/stock-reasons/types';

export interface VoucherPrintButtonProps {
  kind: VoucherPrintKind;
  type: CashVoucherType | StockDirection;
  vouchers: VoucherPrintTarget[];
  variant?: 'button' | 'icon'; // button: label + company hint; icon: row action, no label, no hint
  label?: string;
  disabled?: boolean;
  disabledReason?: string; // shown as the button title while disabled
}

// "In" menu: PDF or Excel of the given vouchers.
export function VoucherPrintButton({
  kind,
  type,
  vouchers,
  variant = 'button',
  label = 'In',
  disabled,
  disabledReason,
}: VoucherPrintButtonProps) {
  const { print, isPending } = useVoucherPrint();
  const isDisabled = disabled || vouchers.length === 0 || isPending;
  // Stock vouchers ask for the price mode first; the format waits here while the dialog is open.
  const [pendingFormat, setPendingFormat] = useState<ExportFormat | null>(null);
  const triggerRef = useRef<HTMLButtonElement>(null);
  const refocusWhenEnabled = useRef(false);
  // The dialog was opened from a menu item that is gone, so Radix has nowhere to return the focus: send it to the
  // trigger. The trigger is disabled while a print runs; then the focus follows once it is enabled again.
  const returnFocus = (event: Event) => {
    event.preventDefault();
    const trigger = triggerRef.current;
    if (!canTakeFocus(trigger)) refocusWhenEnabled.current = true;
    else if (isFocusLost()) giveFocusBack(trigger); // never take the focus from where the user went meanwhile
  };
  useEffect(() => {
    if (isPending || !refocusWhenEnabled.current) return;
    refocusWhenEnabled.current = false;
    const trigger = triggerRef.current;
    if (isFocusLost() && canTakeFocus(trigger)) giveFocusBack(trigger);
  }, [isPending]);
  const choose = (format: ExportFormat) => {
    refocusWhenEnabled.current = false; // a stale wish from an earlier dialog must not fire on this print
    if (kind === 'stock') setPendingFormat(format);
    else void print({ kind, type, vouchers, format });
  };
  // Runs inside the dialog's "In" click, so a PDF tab can still open in that user gesture.
  const confirmPriceMode = (priceMode: StockPrintPriceMode) => {
    if (pendingFormat) void print({ kind, type, vouchers, format: pendingFormat, priceMode });
  };
  const stop = (e: React.SyntheticEvent) => e.stopPropagation();
  const title = disabled ? disabledReason : undefined;
  const icon = isPending ? <ButtonLoader /> : <AppIcon name="print" tone="none" />;

  return (
    <div className="flex flex-wrap items-center gap-2">
      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          {variant === 'icon' ? (
            <Button
              variant="ghost"
              size="icon"
              aria-label={`In ${vouchers[0]?.code}`}
              disabled={isDisabled}
              title={title}
              onClick={stop}
              ref={triggerRef}
            >
              {icon}
            </Button>
          ) : (
            <Button variant="outline" size="sm" disabled={isDisabled} title={title} onClick={stop} ref={triggerRef}>
              {icon}
              {label}
              <AppIcon name="chevronDown" />
            </Button>
          )}
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end" onClick={stop}>
          <DropdownMenuItem onSelect={() => choose('pdf')}>
            <AppIcon name="exportPdf" className="mr-2" />
            In (PDF)
          </DropdownMenuItem>
          <DropdownMenuItem onSelect={() => choose('xlsx')}>
            <AppIcon name="exportExcel" className="mr-2" />
            Excel
          </DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>
      {kind === 'stock' && (
        <StockPriceModeDialog
          open={pendingFormat !== null}
          onOpenChange={(open) => !open && setPendingFormat(null)}
          onConfirm={confirmPriceMode}
          onCloseAutoFocus={returnFocus}
        />
      )}
      {variant === 'button' && <CompanyProfileHint />}
    </div>
  );
}
