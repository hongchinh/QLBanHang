import type { ReactNode } from 'react';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import type { ErrorTab, StockVoucherTab } from '@/pages/stock-vouchers/utils/voucher-tabs';

export interface StockVoucherTabsProps {
  value: StockVoucherTab;
  onValueChange: (tab: StockVoucherTab) => void;
  lineCount: number;
  errorTabs: ReadonlySet<ErrorTab>;
  lines: ReactNode;
  other: ReactNode;
  payment: ReactNode;
  // Edit only.
  history?: ReactNode;
}

const TRIGGER =
  'rounded-b-none border border-b-0 px-3 py-1.5 data-[state=active]:-mb-px data-[state=active]:font-bold data-[state=active]:text-primary';
// Panels stay mounted (form fields keep their refs); inactive ones are hidden with CSS (D5).
const PANEL = 'mt-0 data-[state=inactive]:hidden';

export function StockVoucherTabs(props: StockVoucherTabsProps) {
  const { value, onValueChange, lineCount, errorTabs, lines, other, payment, history } = props;
  return (
    <Tabs value={value} onValueChange={(v) => onValueChange(v as StockVoucherTab)} className="flex flex-1 flex-col">
      <TabsList className="h-auto shrink-0 justify-start gap-0.5 rounded-none border-b bg-transparent p-0">
        <Trigger value="lines" label="Hàng hóa" hotkey="Alt+1" error={errorTabs.has('lines')}>
          <span className="ml-1.5 rounded-full bg-primary/10 px-1.5 text-xs text-primary">{lineCount}</span>
        </Trigger>
        <Trigger value="other" label="Thông tin khác" hotkey="Alt+2" error={errorTabs.has('other')} />
        <Trigger value="payment" label="Thanh toán" hotkey="Alt+3" error={errorTabs.has('payment')} />
        {history && <Trigger value="history" label="Lịch sử" hotkey="Alt+4" error={false} />}
      </TabsList>
      <TabsContent value="lines" forceMount className={`${PANEL} min-h-[280px] flex flex-1 flex-col overflow-hidden rounded-b-md border border-t-0 bg-card`}>
        {lines}
      </TabsContent>
      <TabsContent value="other" forceMount className={`${PANEL} rounded-b-md border border-t-0 bg-card`}>
        {other}
      </TabsContent>
      <TabsContent value="payment" forceMount className={`${PANEL} rounded-b-md border border-t-0 bg-card`}>
        {payment}
      </TabsContent>
      {history && (
        <TabsContent value="history" forceMount className={`${PANEL} overflow-auto rounded-b-md border border-t-0 bg-card p-3`}>
          {history}
        </TabsContent>
      )}
    </Tabs>
  );
}

function Trigger({ value, label, hotkey, error, children }: { value: StockVoucherTab; label: string; hotkey: string; error: boolean; children?: ReactNode }) {
  return (
    <TabsTrigger value={value} className={TRIGGER} title={hotkey}>
      <span>{label}</span>
      {children}
      {error && <span className="ml-1.5 inline-block h-1.5 w-1.5 rounded-full bg-destructive" data-testid={`tab-error-${value}`} aria-label="có lỗi" />}
    </TabsTrigger>
  );
}
