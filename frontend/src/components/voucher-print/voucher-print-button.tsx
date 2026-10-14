import { ChevronDown, FileSpreadsheet, FileText, Loader2, Printer } from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { CompanyProfileHint } from '@/components/tt133-export/company-profile-hint';
import { useVoucherPrint } from '@/features/voucher-print/use-voucher-print';
import type { VoucherPrintKind, VoucherPrintTarget } from '@/features/voucher-print/types';
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
  const choose = (format: ExportFormat) => void print({ kind, type, vouchers, format });
  const stop = (e: React.SyntheticEvent) => e.stopPropagation();
  const title = disabled ? disabledReason : undefined;
  const icon = isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : <Printer className="h-4 w-4" />;

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
            >
              {icon}
            </Button>
          ) : (
            <Button variant="outline" size="sm" disabled={isDisabled} title={title} onClick={stop}>
              {icon}
              {label}
              <ChevronDown className="h-4 w-4" />
            </Button>
          )}
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end" onClick={stop}>
          <DropdownMenuItem onSelect={() => choose('pdf')}>
            <FileText className="mr-2 h-4 w-4 text-indigo-600" />
            In (PDF)
          </DropdownMenuItem>
          <DropdownMenuItem onSelect={() => choose('xlsx')}>
            <FileSpreadsheet className="mr-2 h-4 w-4 text-emerald-700" />
            Excel
          </DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>
      {variant === 'button' && <CompanyProfileHint />}
    </div>
  );
}
