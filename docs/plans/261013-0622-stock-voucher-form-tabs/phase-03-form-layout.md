# Phase 03 — Form layout T2: main fields, tabs, totals bar, debt strip, full height

**Status:** [ ] pending
**Complexity:** L

## Objective

Rebuild the stock voucher form page around the T2 layout (brainstorm §1):

- a sticky title bar (unchanged, but the "Số phiếu" input is removed);
- a **2-row main area** that is always visible:
  - Ngày\*, Lý do\*, Kho\*, HTTT
  - Mã đối tượng, Tên đối tượng, Địa chỉ
- **4 tabs**:
  - **Hàng hóa (n)** (default)
  - **Thông tin khác**: MST, Người nhận/giao, Ghi chú
  - **Thanh toán**: Phí VC, CK cả đơn, Số tiền thanh toán, Quỹ, Hạn TT, % VAT cho mọi dòng, link phiếu tự động
  - **Lịch sử** (edit only)
- a **bottom totals bar** plus the **partner debt strip** (receivable/payable → sau phiếu, hạn mức, còn được nợ sau phiếu, quá hạn, số ngày nợ);
- a page that fills the viewport height.

Behavior changes:

- Enter moves through the main fields and then into the grid. Enter never submits the form (D7).
- Alt+1…4 switch tabs.
- A tab with an error shows a red dot. Saving with errors switches to the first tab with an error and focuses the field.
- Clicking Phí VC / CK đơn / Đã thanh toán on the bar opens the payment tab and focuses that input.

The grid itself does not change in this phase (Phase 04).

## Files

- `frontend/src/pages/stock-vouchers/components/stock-totals-panel.tsx` → `stock-voucher-payment-tab.tsx` (git mv + modify)
- `frontend/src/pages/stock-vouchers/components/stock-totals-panel.test.tsx` → `stock-voucher-payment-tab.test.tsx` (git mv + modify)
- `frontend/src/pages/stock-vouchers/components/customer-debt-panel.tsx` → `partner-debt-strip.tsx` (git mv + rewrite)
- `frontend/src/pages/stock-vouchers/components/customer-debt-panel.test.tsx` → `partner-debt-strip.test.tsx` (git mv + rewrite)
- `frontend/src/pages/stock-vouchers/components/stock-totals-bar.tsx` (new)
- `frontend/src/pages/stock-vouchers/components/stock-totals-bar.test.tsx` (new)
- `frontend/src/pages/stock-vouchers/components/stock-voucher-tabs.tsx` (new)
- `frontend/src/pages/stock-vouchers/components/stock-voucher-tabs.test.tsx` (new)
- `frontend/src/pages/stock-vouchers/components/stock-voucher-main-fields.tsx` (new)
- `frontend/src/pages/stock-vouchers/stock-voucher-form-page.tsx` (modify)
- `frontend/src/pages/stock-vouchers/stock-voucher-form-page.test.tsx` (modify)
- `frontend/src/styles/form-inline.css` (modify)

## Tasks

### Task 3.1 — Payment tab (from `StockTotalsPanel`)

1. **Move the files first:**
   ```bash
   git mv frontend/src/pages/stock-vouchers/components/stock-totals-panel.tsx frontend/src/pages/stock-vouchers/components/stock-voucher-payment-tab.tsx
   git mv frontend/src/pages/stock-vouchers/components/stock-totals-panel.test.tsx frontend/src/pages/stock-vouchers/components/stock-voucher-payment-tab.test.tsx
   ```
   Then **write the failing test.** Rewrite the test to import `StockVoucherPaymentTab` from `./stock-voucher-payment-tab`, and change `renderPanel` to render `<StockVoucherPaymentTab {...props}><label htmlFor="x">Quỹ</label><select id="x" /></StockVoucherPaymentTab>` with props without `totals`:
   - Keep the handler assertions for "Phí vận chuyển", "CK cả đơn" and "Số tiền thanh toán", and keep the VAT-to-all test.
   - Remove the `stock-totals-*` assertions, which move to the bar.
   - Add:
     ```ts
     it('renders the fund / due date slot and no totals', () => {
       renderPanel('Out');
       expect(screen.getByLabelText('Quỹ')).toBeInTheDocument();
       expect(screen.queryByTestId('stock-totals-total')).not.toBeInTheDocument();
       expect(screen.queryByText('Tổng cộng')).not.toBeInTheDocument();
     });
     ```
2. **Run:** `npx vitest run src/pages/stock-vouchers/components/stock-voucher-payment-tab.test.tsx` → FAIL, `StockVoucherPaymentTab` is not exported.
3. **Minimal implementation** in `stock-voucher-payment-tab.tsx`:
   - Rename the component to `StockVoucherPaymentTab` and the props interface to `StockVoucherPaymentTabProps`. Remove `totals` from the props and add `children?: React.ReactNode` (the fund and due-date fields the page renders).
   - Drop the `Card`/`CardHeader` wrapper, `TotalRow` and the "Tổng thanh toán" block.
   - Render a two-column grid: `<div className="grid gap-x-8 gap-y-1.5 p-3 md:grid-cols-2">`. The left column holds the three `EditableRow`s, the paid-amount error, the auto voucher link and the VAT-to-all row. The right column is `<div className="space-y-1.5">{children}</div>`.
   - Keep `EditableRow` unchanged. It keeps the ids `stock-freight`, `stock-order-discount` and `stock-paid-amount`.
4. **Run:** same command → PASS.
5. **Commit:** `git commit -m "refactor(frontend): stock totals panel becomes the payment tab"` (stage both moved files).

The page still imports `StockTotalsPanel` at this point, so `npm run typecheck` fails until Task 3.6. Run only the targeted test here, and do not push between 3.1 and 3.6.

### Task 3.2 — Totals bar

1. **Write the failing test** `stock-totals-bar.test.tsx`:
   ```ts
   import { describe, expect, it, vi } from 'vitest';
   import { fireEvent, render, screen } from '@testing-library/react';
   import { StockTotalsBar } from './stock-totals-bar';

   const totals = { goodsAmount: 1_000_000, lineDiscountTotal: 50_000, discountTotal: 70_000, vatTotal: 93_000, total: 1_053_000 };

   describe('StockTotalsBar', () => {
     it('shows the totals and jumps to the editable amounts', () => {
       const onJump = vi.fn();
       render(<StockTotalsBar totals={totals} freight={30_000} orderDiscount={20_000} paidAmount={500_000} lineCount={15} onJump={onJump} />);

       expect(screen.getByTestId('stock-totals-lines')).toHaveTextContent('15 dòng');
       expect(screen.getByTestId('stock-totals-goods')).toHaveTextContent('1.000.000');
       expect(screen.getByTestId('stock-totals-discount')).toHaveTextContent('70.000');
       expect(screen.getByTestId('stock-totals-vat')).toHaveTextContent('93.000');
       expect(screen.getByTestId('stock-totals-total')).toHaveTextContent('1.053.000');

       fireEvent.click(screen.getByRole('button', { name: /Phí VC/ }));
       fireEvent.click(screen.getByRole('button', { name: /CK đơn/ }));
       fireEvent.click(screen.getByRole('button', { name: /Đã thanh toán/ }));
       expect(onJump.mock.calls.map((c) => c[0])).toEqual(['freight', 'orderDiscount', 'paidAmount']);
       expect(screen.getByRole('button', { name: /Phí VC/ })).toHaveTextContent('30.000');
     });
   });
   ```
2. **Run:** `npx vitest run src/pages/stock-vouchers/components/stock-totals-bar.test.tsx` → FAIL, module not found.
3. **Minimal implementation** `stock-totals-bar.tsx`:
   ```tsx
   import type { StockTotals } from '@/pages/stock-vouchers/utils/compute-stock-line';

   const fmt = new Intl.NumberFormat('vi-VN');

   export type TotalsJumpField = 'freight' | 'orderDiscount' | 'paidAmount';

   export interface StockTotalsBarProps {
     totals: StockTotals;
     freight: number;
     orderDiscount: number;
     paidAmount: number;
     lineCount: number;
     // Opens the payment tab on the matching input.
     onJump: (field: TotalsJumpField) => void;
   }

   // Read-only totals under the grid; the amounts are edited in the payment tab.
   export function StockTotalsBar({ totals, freight, orderDiscount, paidAmount, lineCount, onJump }: StockTotalsBarProps) {
     return (
       <div className="flex flex-wrap items-center justify-between gap-x-6 gap-y-1 px-3 py-2 text-sm">
         <div className="flex flex-wrap items-center gap-x-5 gap-y-1 text-muted-foreground">
           <span data-testid="stock-totals-lines">{lineCount} dòng</span>
           <Amount label="Tiền hàng" value={totals.goodsAmount} testId="stock-totals-goods" />
           <Amount label="CK" value={totals.discountTotal} testId="stock-totals-discount" />
           <Amount label="VAT" value={totals.vatTotal} testId="stock-totals-vat" />
           <JumpAmount label="Phí VC" value={freight} onClick={() => onJump('freight')} />
           <JumpAmount label="CK đơn" value={orderDiscount} onClick={() => onJump('orderDiscount')} />
           <JumpAmount label="Đã thanh toán" value={paidAmount} onClick={() => onJump('paidAmount')} />
         </div>
         <div className="text-base font-extrabold text-primary">
           Tổng thanh toán: <span className="tabular-nums" data-testid="stock-totals-total">{fmt.format(totals.total)}</span>
         </div>
       </div>
     );
   }

   function Amount({ label, value, testId }: { label: string; value: number; testId: string }) {
     return (
       <span>
         {label} <b className="font-semibold tabular-nums text-foreground" data-testid={testId}>{fmt.format(value)}</b>
       </span>
     );
   }

   function JumpAmount({ label, value, onClick }: { label: string; value: number; onClick: () => void }) {
     return (
       <button type="button" className="hover:underline" onClick={onClick}>
         {label} <b className="font-semibold tabular-nums text-foreground">{fmt.format(value)}</b>
       </button>
     );
   }
   ```
4. **Run:** same command → PASS.
5. **Commit:** `git commit -m "feat(frontend): stock voucher totals bar"`.

### Task 3.3 — Partner debt strip (from `CustomerDebtPanel`)

1. **Move the files:**
   ```bash
   git mv frontend/src/pages/stock-vouchers/components/customer-debt-panel.tsx frontend/src/pages/stock-vouchers/components/partner-debt-strip.tsx
   git mv frontend/src/pages/stock-vouchers/components/customer-debt-panel.test.tsx frontend/src/pages/stock-vouchers/components/partner-debt-strip.test.tsx
   ```
   **Write the failing test.** Rewrite `partner-debt-strip.test.tsx`. Keep the mocks, `render`, `setPermissions` and `result` helpers. Add `payableBalance: 500_000` to `summary()`. Then add these cases:
   ```ts
   const base = { partnerId: P1, partnerLabel: 'KH01 — An Phát', cancelled: false };

   it('shows balances, after-voucher values and a negative available amount in red', () => {
     setPermissions(['reports.debt']);
     summaryMock.mockReturnValue(result(summary({ receivableBalance: 7_000_000, creditLimit: 10_000_000 })));
     render(<PartnerDebtStrip {...base} current={{ effect: 'ReceivableIncrease', total: 4_000_000, paid: 0 }} />);

     expect(screen.getByText('KH01 — An Phát')).toBeInTheDocument();
     expect(screen.getByTestId('partner-debt-receivable')).toHaveTextContent('7.000.000');
     expect(screen.getByTestId('partner-debt-receivable-after')).toHaveTextContent('11.000.000');
     expect(screen.getByTestId('partner-debt-payable')).toHaveTextContent('500.000');
     expect(screen.queryByTestId('partner-debt-payable-after')).not.toBeInTheDocument();
     expect(screen.getByTestId('partner-debt-limit')).toHaveTextContent('10.000.000');
     expect(screen.getByTestId('partner-debt-available')).toHaveTextContent('-1.000.000');
     expect(screen.getByTestId('partner-debt-available')).toHaveClass('text-destructive');
     expect(screen.getByTestId('partner-debt-overdue')).toHaveTextContent('1.500.000');
     expect(screen.getByRole('link', { name: 'Xem' })).toHaveAttribute(
       'href', `/reports/debt-overdue?side=Receivable&partnerId=${P1}`,
     );
     expect(screen.getByTestId('partner-debt-credit-days')).toHaveTextContent('30');
   });

   it('an edited voucher subtracts its saved impact', () => {
     summaryMock.mockReturnValue(result(summary({ receivableBalance: 7_000_000 })));
     render(
       <PartnerDebtStrip
         {...base}
         current={{ effect: 'ReceivableIncrease', total: 5_000_000, paid: 0 }}
         saved={{ effect: 'ReceivableIncrease', total: 4_000_000, paid: 0 }}
       />,
     );
     expect(screen.getByTestId('partner-debt-receivable-after')).toHaveTextContent('8.000.000');
   });

   it('a cancelled voucher shows only the current balances; no limit reads "Không giới hạn"', () => {
     summaryMock.mockReturnValue(result(summary({ creditLimit: null, available: null })));
     render(<PartnerDebtStrip {...base} cancelled current={{ effect: 'ReceivableIncrease', total: 1, paid: 0 }} />);
     expect(screen.queryByTestId('partner-debt-receivable-after')).not.toBeInTheDocument();
     expect(screen.getByTestId('partner-debt-limit')).toHaveTextContent('Không giới hạn');
     expect(screen.getByTestId('partner-debt-available')).toHaveTextContent('Không giới hạn');
   });

   it('renders nothing on error and a skeleton while loading', () => {
     summaryMock.mockReturnValue(result(undefined, { isError: true }));
     const { container, unmount } = render(<PartnerDebtStrip {...base} current={{ effect: 'None', total: 0, paid: 0 }} />);
     expect(container).toBeEmptyDOMElement();
     unmount();
     summaryMock.mockReturnValue(result(undefined, { isLoading: true }));
     render(<PartnerDebtStrip {...base} current={{ effect: 'None', total: 0, paid: 0 }} />);
     expect(screen.getByTestId('partner-debt-skeleton')).toBeInTheDocument();
   });
   ```
   `render` wraps the strip in `MemoryRouter`. `toBeEmptyDOMElement` on the router's container still works, because `MemoryRouter` renders no DOM of its own.
2. **Run:** `npx vitest run src/pages/stock-vouchers/components/partner-debt-strip.test.tsx` → FAIL, `PartnerDebtStrip` is not exported.
3. **Minimal implementation.** Rewrite `partner-debt-strip.tsx`:
   ```tsx
   import { Link } from 'react-router-dom';
   import { usePartnerDebtSummary } from '@/features/debt/hooks';
   import { cn } from '@/lib/utils';
   import { useAuthStore } from '@/stores/auth-store';
   import { debtAfterVoucher, type VoucherDebtImpact } from '@/pages/stock-vouchers/utils/debt-after-voucher';

   const fmt = new Intl.NumberFormat('vi-VN');
   const UNLIMITED = 'Không giới hạn';

   export interface PartnerDebtStripProps {
     partnerId: string;
     partnerLabel: string;
     current: VoucherDebtImpact;
     // The stored impact of the voucher being edited (Active, same partner).
     saved?: VoucherDebtImpact;
     cancelled: boolean;
   }

   // Debt line under the totals bar (P6): the partner's position in the working branch and after this voucher.
   // Display only; the form decides when it is shown (partner picked, permission).
   export function PartnerDebtStrip({ partnerId, partnerLabel, current, saved, cancelled }: PartnerDebtStripProps) {
     const { data, isLoading, isError } = usePartnerDebtSummary(partnerId, { enabled: true });
     const canViewOverdueReport = useAuthStore((s) => s.hasPermission('reports.debt'));

     if (isError || (!data && !isLoading)) return null;
     if (!data) {
       return <div className="h-7 animate-pulse rounded bg-muted" data-testid="partner-debt-skeleton" aria-busy="true" />;
     }

     const after = cancelled ? null : debtAfterVoucher(data, current, saved);
     const available = after?.receivableChanged ? after.availableAfter : data.available ?? null;
     return (
       <div className="flex flex-wrap items-center gap-x-5 gap-y-1 border-t border-dashed bg-muted/40 px-3 py-1.5 text-xs text-muted-foreground">
         <span className="font-semibold text-foreground">{partnerLabel}</span>
         <span>
           Phải thu <Value testId="partner-debt-receivable" value={data.receivableBalance} />
           {after?.receivableChanged && <> → sau phiếu <Value testId="partner-debt-receivable-after" value={after.receivableAfter} /></>}
         </span>
         <span>
           Phải trả <Value testId="partner-debt-payable" value={data.payableBalance} />
           {after?.payableChanged && <> → sau phiếu <Value testId="partner-debt-payable-after" value={after.payableAfter} /></>}
         </span>
         <span>
           Hạn mức <b className="text-foreground" data-testid="partner-debt-limit">{data.creditLimit == null ? UNLIMITED : fmt.format(data.creditLimit)}</b>
         </span>
         <span>
           {after?.receivableChanged ? 'Còn được nợ sau phiếu' : 'Còn được nợ'}{' '}
           <b
             className={cn('tabular-nums text-foreground', available != null && available < 0 && 'text-destructive')}
             data-testid="partner-debt-available"
           >
             {available == null ? UNLIMITED : fmt.format(available)}
           </b>
         </span>
         <span>
           Quá hạn{' '}
           <b className={cn('tabular-nums text-foreground', data.overdueAmount > 0 && 'text-destructive')} data-testid="partner-debt-overdue">
             {fmt.format(data.overdueAmount)}
           </b>
           {data.overdueAmount > 0 && canViewOverdueReport && (
             <>
               {' '}
               <Link
                 to={`/reports/debt-overdue?side=Receivable&partnerId=${partnerId}`}
                 target="_blank"
                 rel="noopener noreferrer"
                 className="text-primary underline"
               >
                 Xem
               </Link>
             </>
           )}
         </span>
         <span>
           Số ngày nợ <b className="text-foreground" data-testid="partner-debt-credit-days">{data.creditDays == null ? '—' : String(data.creditDays)}</b>
         </span>
       </div>
     );
   }

   function Value({ value, testId }: { value: number; testId: string }) {
     return <b className="tabular-nums text-foreground" data-testid={testId}>{fmt.format(value)}</b>;
   }
   ```
   Check the selector name for permissions against the deleted panel: it used `useAuthStore((s) => s.hasPermission('reports.debt'))`. Keep the same call.
4. **Run:** same command → PASS.
5. **Commit:** `git commit -m "feat(frontend): partner debt strip with after-voucher balances"`.

### Task 3.4 — Tabs shell

1. **Write the failing test** `stock-voucher-tabs.test.tsx`:
   ```ts
   import type { ComponentProps } from 'react';
   import { describe, expect, it, vi } from 'vitest';
   import { fireEvent, render, screen } from '@testing-library/react';
   import { StockVoucherTabs } from './stock-voucher-tabs';

   function renderTabs(props: Partial<ComponentProps<typeof StockVoucherTabs>> = {}) {
     const onValueChange = vi.fn();
     render(
       <StockVoucherTabs
         value="lines"
         onValueChange={onValueChange}
         lineCount={15}
         errorTabs={new Set()}
         lines={<input id="grid-cell" />}
         other={<input id="other-cell" />}
         payment={<input id="payment-cell" />}
         {...props}
       />,
     );
     return onValueChange;
   }

   describe('StockVoucherTabs', () => {
     it('keeps every panel mounted, shows the line count and switches on click', () => {
       const onValueChange = renderTabs();
       expect(screen.getByRole('tab', { name: /Hàng hóa/ })).toHaveAttribute('data-state', 'active');
       expect(screen.getByRole('tab', { name: /Hàng hóa/ })).toHaveTextContent('15');
       expect(document.getElementById('payment-cell')).toBeInTheDocument();
       expect(screen.queryByRole('tab', { name: /Lịch sử/ })).not.toBeInTheDocument();

       fireEvent.mouseDown(screen.getByRole('tab', { name: /Thanh toán/ }));
       expect(onValueChange).toHaveBeenCalledWith('payment');
     });

     it('marks tabs with errors and shows history only when given', () => {
       renderTabs({ errorTabs: new Set(['payment']), history: <p>log</p> });
       expect(screen.getByTestId('tab-error-payment')).toBeInTheDocument();
       expect(screen.queryByTestId('tab-error-other')).not.toBeInTheDocument();
       expect(screen.getByRole('tab', { name: /Lịch sử/ })).toBeInTheDocument();
     });
   });
   ```
   Radix tab triggers activate on `mousedown` (`onMouseDown` with the left button). If `fireEvent.mouseDown` does not trigger it in jsdom, use `fireEvent.mouseDown(el, { button: 0 })`.
2. **Run:** `npx vitest run src/pages/stock-vouchers/components/stock-voucher-tabs.test.tsx` → FAIL, module not found.
3. **Minimal implementation** `stock-voucher-tabs.tsx`:
   ```tsx
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
       <Tabs value={value} onValueChange={(v) => onValueChange(v as StockVoucherTab)} className="flex min-h-0 flex-1 flex-col">
         <TabsList className="h-auto shrink-0 justify-start gap-0.5 rounded-none border-b bg-transparent p-0">
           <Trigger value="lines" label="Hàng hóa" hotkey="Alt+1" error={errorTabs.has('lines')}>
             <span className="ml-1.5 rounded-full bg-primary/10 px-1.5 text-xs text-primary">{lineCount}</span>
           </Trigger>
           <Trigger value="other" label="Thông tin khác" hotkey="Alt+2" error={errorTabs.has('other')} />
           <Trigger value="payment" label="Thanh toán" hotkey="Alt+3" error={errorTabs.has('payment')} />
           {history && <Trigger value="history" label="Lịch sử" hotkey="Alt+4" error={false} />}
         </TabsList>
         <TabsContent value="lines" forceMount className={`${PANEL} min-h-[280px] flex-1 overflow-auto rounded-b-md border border-t-0 bg-card`}>
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
   ```
4. **Run:** same command → PASS.
5. **Commit:** `git commit -m "feat(frontend): stock voucher tabs shell"`.

### Task 3.5 — Main fields

1. **Write the failing test.** Add to `stock-voucher-form-page.test.tsx`. Main fields are only meaningful wired to the page, so test through the page:
   ```ts
   it('Enter moves through the main fields, never saves, and lands on the first empty line', async () => {
     renderPage('In');
     byId('stock-voucher-at').focus();
     fireEvent.keyDown(byId('stock-voucher-at'), { key: 'Enter' });
     expect(document.activeElement).toBe(byId('stock-reason'));
     fireEvent.keyDown(byId('stock-reason'), { key: 'Enter' });
     expect(document.activeElement).toBe(byId('stock-warehouse'));
     fireEvent.keyDown(byId('stock-warehouse'), { key: 'Enter' });
     expect(document.activeElement).toBe(byId('stock-payment-method'));
     fireEvent.keyDown(byId('stock-payment-method'), { key: 'Enter' });
     expect(document.activeElement).toBe(byId('stock-partner'));
     fireEvent.keyDown(byId('stock-partner'), { key: 'Enter' });
     expect(document.activeElement).toBe(byId('stock-partner-name'));
     fireEvent.keyDown(byId('stock-partner-name'), { key: 'Enter' });
     expect(document.activeElement).toBe(byId('stock-partner-address'));
     fireEvent.keyDown(byId('stock-partner-address'), { key: 'Enter' });
     await waitFor(() => expect(document.activeElement).toBe(byId('stock-line-product-code-0')));
     expect(screen.getByRole('tab', { name: /Hàng hóa/ })).toHaveAttribute('data-state', 'active');
     expect(createMock).not.toHaveBeenCalled();
   });

   // Implicit submission is blocked by preventing the Enter keydown (D7).
   it('Enter in a tab field is prevented so it cannot submit the form', () => {
     renderPage('In');
     const event = createEvent.keyDown(byId('stock-voucher-note'), { key: 'Enter' });
     fireEvent(byId('stock-voucher-note'), event);
     expect(event.defaultPrevented).toBe(true);
   });
   ```
   Add `createEvent` to the `@testing-library/react` import.
2. **Run:** `npx vitest run src/pages/stock-vouchers/stock-voucher-form-page.test.tsx -t "Enter"` → FAIL (focus stays on `stock-voucher-at`; `defaultPrevented` is false).
3. **Minimal implementation.** Create `stock-voucher-main-fields.tsx`. Move the JSX of rows 1–2 and the address row out of the page into `StockVoucherMainFields`:
   ```tsx
   export interface StockVoucherMainFieldsProps {
     form: UseFormReturn<StockVoucherFormValues, unknown, StockVoucherFormParsed>;
     type: StockDirection;
     labels: StockVoucherLabels;
     readOnly: boolean;
     warehouses: Warehouse[];        // header list (active + current)
     reasons: StockReason[];
     paymentMethods: PaymentMethod[];
     warehouseId: string;
     reasonId: string;
     paymentMethodId?: string;
     selectedReason?: StockReason;
     selectedPartner: StockVoucherDraftPartner | null;
     onWarehouseChange: (id: string) => void;
     onReasonChange: (id: string) => void;
     onSelectPartner: (p: PartnerSearchItem | null) => void;
     // Enter on the last field.
     onEnterGrid: () => void;
   }

   // Enter order (brainstorm §1); disabled fields are skipped.
   const MAIN_FIELD_IDS = [
     'stock-voucher-at', 'stock-reason', 'stock-warehouse', 'stock-payment-method',
     'stock-partner', 'stock-partner-name', 'stock-partner-address',
   ];
   ```
   Layout: `<fieldset disabled={readOnly} className="m-0 min-w-0 space-y-[6px] rounded-md border bg-card px-3 py-2" onKeyDown={handleKeyDown}>` with two rows:
   - `<div className="form-inline-grid form-cols-stock-main-1">`: Ngày (`stock-voucher-at`, `type="datetime-local"`, registered), Lý do (`stock-reason`), Kho (`stock-warehouse`), HTTT (`stock-payment-method`). Then `FieldError` for `voucherAt ?? warehouseId ?? reasonId ?? paymentMethodId`.
   - `<div className="form-inline-grid form-cols-stock-main-2">`: "Mã đối tượng" (`PartnerAutocomplete inputId="stock-partner"`), "Tên đối tượng" (`stock-partner-name`), "Địa chỉ" (`stock-partner-address`).

   The select markup, `SELECT_CLASS` and `FieldError` are moved unchanged; export `FieldError` and `SELECT_CLASS` from this file so the page reuses them. The "Số phiếu" input is removed.

   Key handler:
   ```ts
   function handleKeyDown(e: React.KeyboardEvent<HTMLElement>) {
     if (e.defaultPrevented || e.key !== 'Enter' || e.shiftKey || e.ctrlKey || e.altKey || e.metaKey) return;
     const at = MAIN_FIELD_IDS.indexOf((e.target as HTMLElement).id);
     if (at === -1) return;
     e.preventDefault();
     const next = MAIN_FIELD_IDS.slice(at + 1)
       .map((id) => document.getElementById(id) as HTMLInputElement | HTMLSelectElement | null)
       .find((el) => el && !el.disabled);
     if (next) next.focus();
     else onEnterGrid();
   }
   ```
   `PartnerAutocomplete` already calls `preventDefault()` on Enter while its list is open, so a pick never moves focus.

   In `frontend/src/styles/form-inline.css`, add after `.form-cols-3`:
   ```css
   .form-inline-grid.form-cols-stock-main-1 {
     --qldh-form-grid-columns: 90px minmax(170px, 1fr) 90px minmax(180px, 1.2fr) 50px minmax(140px, 1fr) 50px minmax(150px, 1fr);
   }
   .form-inline-grid.form-cols-stock-main-2 {
     --qldh-form-grid-columns: 90px minmax(170px, 1fr) 90px minmax(220px, 1.6fr) 50px minmax(260px, 2.6fr);
   }
   @media (max-width: 1279px) {
     .form-inline-grid.form-cols-stock-main-1,
     .form-inline-grid.form-cols-stock-main-2 {
       --qldh-form-grid-columns: 90px minmax(160px, 1fr) 90px minmax(160px, 1fr);
     }
   }
   ```
   Wire it in the page (Task 3.6 completes the page). For this task, render `<StockVoucherMainFields … onEnterGrid={enterGrid} />` in place of the old card rows, and add to the page:
   ```ts
   // Enter from the main area: the first line without a product, or a new one.
   function enterGrid() {
     setActiveTab('lines');
     const lines = form.getValues('lines');
     let idx = lines.findIndex((l) => !l.productId);
     if (idx === -1) {
       idx = lines.length;
       form.setValue('lines', [...lines, createEmptyStockLine(idx, form.getValues('warehouseId'))], { shouldDirty: true });
     }
     setTimeout(() => document.getElementById(`stock-line-product-code-${idx}`)?.focus(), 0);
   }
   ```
   Import `createEmptyStockLine` from `@/features/stock-vouchers/payload`. The `activeTab` state is introduced in Task 3.6; for this task add `const [activeTab, setActiveTab] = useState<StockVoucherTab>('lines');` now.

   In `handleFormKeyDown`, after the Ctrl+S branch, add (D7):
   ```ts
   const tag = (e.target as HTMLElement).tagName;
   if (e.key === 'Enter' && (tag === 'INPUT' || tag === 'SELECT')) e.preventDefault();
   ```
4. **Run:** `npx vitest run src/pages/stock-vouchers/stock-voucher-form-page.test.tsx -t "Enter"` → PASS.
5. **Commit** together with Task 3.6, because the page does not compile until 3.6 removes the old panel imports.

### Task 3.6 — Page wiring: tabs, bar, strip, error tab, Alt+n, full height

1. **Write the failing tests.** Add to `stock-voucher-form-page.test.tsx`:
   ```ts
   function tab(name: RegExp) {
     return screen.getByRole('tab', { name });
   }

   it('Alt+3 opens the payment tab and Alt+1 goes back to the lines', () => {
     renderPage('In');
     fireEvent.keyDown(byId('stock-voucher-at'), { key: '3', altKey: true });
     expect(tab(/Thanh toán/)).toHaveAttribute('data-state', 'active');
     fireEvent.keyDown(byId('stock-voucher-at'), { key: '1', altKey: true });
     expect(tab(/Hàng hóa/)).toHaveAttribute('data-state', 'active');
   });

   it('clicking Phí VC on the totals bar focuses the freight input in the payment tab', async () => {
     renderPage('In');
     fireEvent.click(screen.getByRole('button', { name: /Phí VC/ }));
     expect(tab(/Thanh toán/)).toHaveAttribute('data-state', 'active');
     await waitFor(() => expect(document.activeElement).toBe(byId('stock-freight')));
   });

   it('a server error on the fund switches to the payment tab and marks it', async () => {
     createMock.mockRejectedValueOnce(apiError(400, 'VALIDATION_ERROR', { fundId: ['Quỹ không hợp lệ'] }));
     renderPage('In');
     await fillPerUnitLine(3);
     fireEvent.click(screen.getByRole('button', { name: 'Lưu tạm' }));
     await waitFor(() => expect(tab(/Thanh toán/)).toHaveAttribute('data-state', 'active'));
     expect(screen.getByTestId('tab-error-payment')).toBeInTheDocument();
   });

   it('saving without lines switches back to the lines tab', async () => {
     renderPage('In');
     fireEvent.keyDown(byId('stock-voucher-at'), { key: '2', altKey: true });
     fireEvent.click(screen.getByRole('button', { name: 'Lưu tạm' }));
     await waitFor(() => expect(tab(/Hàng hóa/)).toHaveAttribute('data-state', 'active'));
     expect(screen.getByTestId('tab-error-lines')).toBeInTheDocument();
     expect(createMock).not.toHaveBeenCalled();
   });
   ```
   Check the existing tests `'server field errors show under their fields'` and `'server due date error shows under the due date'`. They show which `apiError` code and status the page maps to field errors. Use the same code in the fund test above.

   **Replace** `'debt panel only for stock-out sale with permission'` with:
   ```ts
   it('debt strip for a picked partner with permission, on both directions', async () => {
     partnerResults.data = [customer];
     partnerSummaryMock.mockReturnValue({
       data: { partnerId: customer.id, receivableBalance: 7_000_000, payableBalance: 0, creditLimit: 5_000_000, available: -2_000_000, overdueAmount: 0, creditDays: 15 },
       isLoading: false,
       isError: false,
     });
     const stripShown = () => screen.queryByTestId('partner-debt-receivable') !== null;

     const { unmount } = renderPage('Out');
     expect(stripShown()).toBe(false);
     fireEvent.change(byId('stock-partner'), { target: { value: 'KH' } });
     fireEvent.mouseDown(within(await screen.findByRole('listbox')).getByText('KH01'));
     await waitFor(() => expect(stripShown()).toBe(true));
     expect(partnerSummaryMock).toHaveBeenCalledWith(customer.id, { enabled: true });
     // XBH raises the receivable: "after" shows, and is 7.000.000 + unpaid total (0 lines → 0).
     expect(screen.getByTestId('partner-debt-receivable-after')).toHaveTextContent('7.000.000');
     // A reason without debt keeps the strip but drops the after-voucher value.
     fireEvent.change(byId('stock-reason'), { target: { value: R_OUT_GIFT } });
     await waitFor(() => expect(screen.queryByTestId('partner-debt-receivable-after')).toBeNull());
     expect(stripShown()).toBe(true);
     unmount();

     // Stock-in shows it too (customer return).
     routeParams.id = V1;
     voucherState.data = voucher({
       reasonId: R_IN_CUSTOMER,
       reasonDebtEffect: 'ReceivableDecrease',
       partnerId: customer.id,
       partnerCode: customer.code,
       partnerName: customer.name,
     });
     const second = renderPage('In');
     expect(stripShown()).toBe(true);
     second.unmount();

     // Without debt.partner_summary and reports.debt: hidden, and never requested.
     permissionState.granted = ['stock_in.view', 'stock_in.edit'];
     partnerSummaryMock.mockClear();
     renderPage('In');
     expect(stripShown()).toBe(false);
     expect(partnerSummaryMock).not.toHaveBeenCalled();
   });
   ```
   Keep the default `partnerSummaryMock.mockReturnValue` in `beforeEach`, adding `payableBalance: 0`.
2. **Run:** `npx vitest run src/pages/stock-vouchers/stock-voucher-form-page.test.tsx` → FAIL on the new tests (no tabs/bar/strip), plus a compile error for the removed `StockTotalsPanel` / `CustomerDebtPanel` imports.
3. **Minimal implementation** in `stock-voucher-form-page.tsx`:
   - **Imports:** drop `StockTotalsPanel`, `CustomerDebtPanel`, `Card*` (if unused) and `Label`/`Input` (if unused). Add `StockVoucherMainFields`, `FieldError`, `SELECT_CLASS`, `StockVoucherTabs`, `StockVoucherPaymentTab`, `StockTotalsBar` / `TotalsJumpField`, `PartnerDebtStrip`, `firstErrorTab`, `errorTabs` and `StockVoucherTab`.
   - **State:** `const [activeTab, setActiveTab] = useState<StockVoucherTab>('lines');` (from Task 3.5).
   - **Debt strip gate** (D9): replace `debtPanelPartnerId` with
     ```ts
     const debtStripPartnerId = canSeePartnerDebt ? selectedPartner?.id : undefined;
     const savedImpact =
       voucher && status === 'Active' && voucher.partnerId === selectedPartner?.id
         ? { effect: voucher.reasonDebtEffect, total: voucher.total, paid: voucher.paidAmount }
         : undefined;
     ```
   - **Server errors:** at the end of `applyServerErrors`, before `return allMapped;`:
     ```ts
     const tab = firstErrorTab(Object.keys(details));
     if (tab) setActiveTab(tab);
     ```
   - **Client errors:** in `submitWithIntent`, pass an invalid handler to `form.handleSubmit(onValid, onInvalid)`:
     ```ts
     (errors) => {
       const tab = firstErrorTab(Object.keys(errors));
       if (!tab) return;
       setActiveTab(tab);
       const key = Object.keys(errors).find((k) => k !== 'lines' && tabOfField(k) === tab);
       if (key) setTimeout(() => form.setFocus(key as FieldPath<StockVoucherFormValues>), 0);
     },
     ```
     Line cells are not registered inputs, so `setFocus` does not apply to them; their `CellError` marks them. Import `tabOfField`.
   - **Alt+n** in `handleFormKeyDown`, before the Enter guard:
     ```ts
     const ALT_TABS: Record<string, StockVoucherTab> = { '1': 'lines', '2': 'other', '3': 'payment', '4': 'history' };
     const altTab = e.altKey && !e.ctrlKey && !e.shiftKey && !e.metaKey ? ALT_TABS[e.key] : undefined;
     if (altTab && (altTab !== 'history' || isEdit)) {
       e.preventDefault();
       setActiveTab(altTab);
       return;
     }
     ```
     (`ALT_TABS` is a module constant.)
   - **Jump from the bar:**
     ```ts
     const JUMP_IDS: Record<TotalsJumpField, string> = { freight: 'stock-freight', orderDiscount: 'stock-order-discount', paidAmount: 'stock-paid-amount' };
     function jumpTo(field: TotalsJumpField) {
       setActiveTab('payment');
       setTimeout(() => document.getElementById(JUMP_IDS[field])?.focus(), 0);
     }
     ```
   - **Layout.** The page root becomes `<div className="flex h-full min-h-0 flex-col gap-2">`. The sticky title bar keeps its markup plus `shrink-0`, and the save buttons get `title="Ctrl+S"`. The read-only banner gets `shrink-0`. The form becomes `className="flex min-h-0 flex-1 flex-col gap-2"` and contains, in order:
     1. `<StockVoucherMainFields … />` (`shrink-0`).
     2. `<StockVoucherTabs value={activeTab} onValueChange={setActiveTab} lineCount={watchedLines.filter((l) => !!l.productId).length} errorTabs={errorTabs(Object.keys(errors))} lines={…} other={…} payment={…} history={isEdit ? <ActivityHistory … /> : undefined} />`:
        - `lines` = the existing `<StockLineGrid …/>` plus the `linesRootMessage` paragraph.
        - `other` = a `fieldset disabled={readOnly}` with a `form-inline-grid form-cols-2 p-3` holding MST (`stock-partner-tax-code`) and `labels.handlerLabel` (`stock-handler-name`), and a `form-inline-grid px-3 pb-3` with Ghi chú (`stock-voucher-note`). The markup is moved from the old card.
        - `payment` = `<StockVoucherPaymentTab …same props as the old panel minus totals…>` with the moved Quỹ select (`stock-fund`, only when `fundShown`), Hạn thanh toán (`stock-due-date`, only when `dueDateShown`, label followed by `({creditDays} ngày)` when `creditDays != null`) and the fund/due-date `FieldError`. Wrap them in `<div className="form-inline-grid">` rows.
     3. `<div className="sticky bottom-0 z-20 shrink-0 rounded-md border bg-card shadow-[0_-2px_6px_rgba(0,0,0,0.06)]">` containing:
        - `<StockTotalsBar totals={computed.totals} freight={freight} orderDiscount={orderDiscount} paidAmount={effectivePaid} lineCount={…same count…} onJump={jumpTo} />`
        - `{debtStripPartnerId && selectedPartner && <PartnerDebtStrip partnerId={debtStripPartnerId} partnerLabel={`${selectedPartner.code} — ${selectedPartner.name}`} current={{ effect: selectedReason?.debtEffect ?? 'None', total, paid: effectivePaid }} saved={savedImpact} cancelled={status === 'Cancelled'} />}`
   - Remove the separate "Lịch sử" card below the form, and the "Thông tin chung" card and its grid wrapper. The dialogs stay outside the form.
   - **Existing tests.** Update any that relied on the removed card titles or the old panel testids (`customer-debt-*` → `partner-debt-*`). Assertions on values and payloads must not change. `'edit shows the activity history'` should still pass, because the history panel is force-mounted. If `getByText('Lịch sử')` now matches both the tab label and a heading, use `getByRole('tab', { name: /Lịch sử/ })`.
4. **Run:**
   ```bash
   npx vitest run src/pages/stock-vouchers
   npm run typecheck
   npm run lint
   ```
   Expected: PASS; lint counts ≤ baseline.
5. **Commit** (Tasks 3.5 + 3.6):
   ```bash
   git add frontend/src/pages/stock-vouchers/stock-voucher-form-page.tsx \
     frontend/src/pages/stock-vouchers/stock-voucher-form-page.test.tsx \
     frontend/src/pages/stock-vouchers/components/stock-voucher-main-fields.tsx \
     frontend/src/styles/form-inline.css
   git commit -m "feat(frontend): stock voucher form T2 layout with tabs, totals bar and debt strip"
   ```

## Verification

From `frontend/`:

- `npx vitest run src/pages/stock-vouchers` → PASS
- `npm run test` → FAIL lines == `BASELINE.md`
- `npm run typecheck` → PASS
- `npm run lint` → counts ≤ `BASELINE.md`
- `npm run build && git checkout -- tsconfig.app.tsbuildinfo tsconfig.node.tsbuildinfo` → PASS
- Quick look (`npm run dev`):
  - the new stock-out page fills the window;
  - Enter walks the main fields into the grid;
  - the tabs switch;
  - the bar and the strip show.

## Exit Criteria

- `StockTotalsPanel` and `CustomerDebtPanel` no longer exist; `grep -r "StockTotalsPanel\|CustomerDebtPanel" frontend/src` is empty.
- The page shows the main fields, 4 tabs (Lịch sử only when editing), the totals bar and the debt strip, as in mockup-02.
- Enter never submits; Alt+1…4, the error dots, auto-switching to the tab with an error, and the bar jumps all work and are tested.
- All element ids used by the existing tests are unchanged, except the removed `stock-voucher-code`.
- A team chat summary is posted.
