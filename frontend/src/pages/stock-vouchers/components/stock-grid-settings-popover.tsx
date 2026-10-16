import { AppIcon } from '@/components/icons';
import { Button } from '@/components/ui/button';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import type { PricingMode } from '@/features/products/types';
import { PRICING_MODES } from '@/lib/dimension-columns';
import { ENTER_STOP_FIELDS, isFieldDisabled, type EnterStopField } from '@/pages/stock-vouchers/utils/line-navigation';
import { OPTIONAL_COLUMNS, type OptionalColumn, type StockGridPrefs } from '@/pages/stock-vouchers/utils/use-stock-grid-prefs';

const COLUMN_LABELS: Record<OptionalColumn, string> = {
  warehouse: 'Kho', 'stock-at': 'SL tồn', amount: 'Số tiền', 'discount-rate': '%CK',
  'discount-amount': 'Tiền CK', 'vat-rate': '%VAT', 'vat-amount': 'Tiền VAT', note: 'Ghi chú',
};
const FIELD_LABELS: Record<EnterStopField, string> = {
  warehouse: 'Kho', 'sheet-count': 'Tấm', length: 'Dài', width: 'Rộng', thickness: 'Dày', quantity: 'SL',
  'unit-price': 'Đơn giá', 'discount-rate': '%CK', 'discount-amount': 'Tiền CK', 'vat-rate': '%VAT', note: 'Ghi chú',
};
const MODE_LABELS: Record<PricingMode, string> = { PerUnit: 'Cái', PerLinearMeter: 'md', PerSquareMeter: 'm²', PerCubicMeter: 'm³' };

export interface StockGridSettingsPopoverProps {
  prefs: StockGridPrefs;
  onToggleColumn: (column: OptionalColumn) => void;
  onToggleEnterStop: (mode: PricingMode, field: EnterStopField) => void;
  onReset: () => void;
}

// Per-user grid settings: which optional columns show and where Enter stops for each pricing mode.
export function StockGridSettingsPopover({ prefs, onToggleColumn, onToggleEnterStop, onReset }: StockGridSettingsPopoverProps) {
  return (
    <Popover>
      <PopoverTrigger asChild>
        <Button type="button" variant="outline" size="sm" className="h-7">
          <AppIcon name="gridSettings" className="mr-1.5" />
          Cột &amp; phím Enter
        </Button>
      </PopoverTrigger>
      <PopoverContent align="start" className="w-[560px] max-w-[calc(100vw-32px)] space-y-3 text-sm">
        <section>
          <h3 className="mb-1.5 font-semibold">Hiện cột</h3>
          <div className="grid grid-cols-2 gap-x-4 gap-y-1">
            {OPTIONAL_COLUMNS.map((c) => (
              <label key={c} className="flex items-center gap-2">
                <input type="checkbox" aria-label={COLUMN_LABELS[c]} checked={!prefs.hiddenColumns.includes(c)} onChange={() => onToggleColumn(c)} />
                {COLUMN_LABELS[c]}
                {c === 'warehouse' && <span className="text-xs text-muted-foreground">(tự hiện khi có dòng khác kho phiếu)</span>}
              </label>
            ))}
          </div>
        </section>
        <section>
          <h3 className="mb-1.5 font-semibold">Phím Enter dừng ở (Mã hàng luôn là ô đầu)</h3>
          <div className="overflow-x-auto">
            <table className="w-full border-collapse text-center text-xs">
              <thead>
                <tr>
                  <th className="border p-1" />
                  {ENTER_STOP_FIELDS.map((f) => <th key={f} className="border bg-muted p-1 font-semibold">{FIELD_LABELS[f]}</th>)}
                </tr>
              </thead>
              <tbody>
                {PRICING_MODES.map((mode) => (
                  <tr key={mode}>
                    <th className="border p-1 text-left font-semibold">{MODE_LABELS[mode]}</th>
                    {ENTER_STOP_FIELDS.map((f) => (
                      <td key={f} className="border p-1">
                        {isFieldDisabled(mode, f) ? (
                          <span className="text-muted-foreground">–</span>
                        ) : (
                          <input
                            type="checkbox"
                            aria-label={`Enter dừng ở ${FIELD_LABELS[f]} — ${MODE_LABELS[mode]}`}
                            checked={prefs.enterStops[mode].includes(f)}
                            onChange={() => onToggleEnterStop(mode, f)}
                          />
                        )}
                      </td>
                    ))}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
        <div className="text-right">
          <Button type="button" variant="outline" size="sm" onClick={onReset}>Khôi phục mặc định</Button>
        </div>
      </PopoverContent>
    </Popover>
  );
}
