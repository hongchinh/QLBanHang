# Phase 03 — Quotation list page adoption + manual verification

**Status:** [x] done (automated tasks; manual browser verification pending user confirmation)
**Complexity:** S

## Objective
Wire column resizing (with persistence) into `frontend/src/pages/quotations/quotation-list-page.tsx` using the hook from Phase 01 and the `TableHead`/`TableColGroup` support from Phase 02. Verify manually in the browser, including that the shared-component layout change doesn't break other pages.

## Files
- `frontend/src/pages/quotations/column-sizing-defaults.ts` (new)
- `frontend/src/pages/quotations/column-sizing-defaults.test.ts` (new)
- `frontend/src/pages/quotations/quotation-list-page.tsx` (modify)
- `frontend/src/pages/quotations/quotation-list-page.test.tsx` (new)

## Tasks

### Task 1: Write failing test for column sizing defaults module
1. Write the failing test — create `frontend/src/pages/quotations/column-sizing-defaults.test.ts`:

```ts
import { describe, expect, it } from 'vitest';
import {
  QUOTATION_LIST_STORAGE_KEY,
  QUOTATION_LIST_COLUMN_SIZING_DEFAULTS,
} from './column-sizing-defaults';

describe('quotation list column sizing defaults', () => {
  it('exposes a stable storage key', () => {
    expect(QUOTATION_LIST_STORAGE_KEY).toBe('quotations-list');
  });

  it('defines a positive default width for every known column id', () => {
    const expectedIds = [
      'code',
      'quotationDate',
      'revenueDate',
      'deliveryDate',
      'customerName',
      'subtotal',
      'discount',
      'freight',
      'taxRate',
      'taxAmount',
      'total',
      'advancePayment',
      'totalCost',
      'grossProfit',
      'status',
      'owner',
      'createdByName',
      'contactPhone',
      'actions',
    ];
    for (const id of expectedIds) {
      expect(QUOTATION_LIST_COLUMN_SIZING_DEFAULTS[id]).toBeGreaterThan(0);
    }
  });

  it('gives the actions column a fixed compact width', () => {
    expect(QUOTATION_LIST_COLUMN_SIZING_DEFAULTS.actions).toBe(90);
  });
});
```

2. Run test to verify it fails — `cd frontend && npx vitest run src/pages/quotations/column-sizing-defaults.test.ts` / Expected: FAIL with `Cannot find module './column-sizing-defaults'`.

### Task 2: Implement column sizing defaults module
1. Write minimal implementation — create `frontend/src/pages/quotations/column-sizing-defaults.ts`:

```ts
export const QUOTATION_LIST_STORAGE_KEY = 'quotations-list';

export const QUOTATION_LIST_COLUMN_SIZING_DEFAULTS: Record<string, number> = {
  code: 100,
  quotationDate: 100,
  revenueDate: 100,
  deliveryDate: 100,
  customerName: 160,
  subtotal: 120,
  discount: 110,
  freight: 110,
  taxRate: 80,
  taxAmount: 120,
  total: 120,
  advancePayment: 110,
  totalCost: 120,
  grossProfit: 120,
  status: 120,
  owner: 140,
  createdByName: 120,
  contactPhone: 100,
  actions: 90,
};

export const COLUMN_MIN_SIZE = 60;
export const ACTIONS_COLUMN_SIZE = QUOTATION_LIST_COLUMN_SIZING_DEFAULTS.actions;
```

2. Run tests to verify they pass — `cd frontend && npx vitest run src/pages/quotations/column-sizing-defaults.test.ts` / Expected: PASS (3 tests).
3. Commit — `git commit -m "feat(quotations): add column sizing defaults for quotation list table"`

### Task 3: Write failing test for resize handles rendering in the quotation list page
1. Write the failing test — create `frontend/src/pages/quotations/quotation-list-page.test.tsx`:

```tsx
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { QuotationListPage } from './quotation-list-page';
import { useAuthStore } from '@/stores/auth-store';

vi.mock('@/features/quotations/api', () => ({
  quotationsApi: {
    list: vi.fn().mockResolvedValue({
      items: [],
      totalItems: 0,
      totalPages: 0,
      hasPreviousPage: false,
      hasNextPage: false,
      aggregates: {},
    }),
    listOwners: vi.fn().mockResolvedValue([]),
    downloadPdf: vi.fn(),
    downloadExcel: vi.fn(),
    downloadHandoverWithPricePdf: vi.fn(),
    downloadHandoverNoPricePdf: vi.fn(),
    downloadHandoverWithPriceExcel: vi.fn(),
    downloadHandoverNoPriceExcel: vi.fn(),
  },
}));

function renderPage() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <MemoryRouter>
        <QuotationListPage />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('QuotationListPage column resizing', () => {
  beforeEach(() => {
    useAuthStore.setState({
      accessToken: 'test-token',
      expiresAt: new Date(Date.now() + 3_600_000).toISOString(),
      user: {
        id: 'u1',
        username: 'tester',
        email: 't@example.com',
        fullName: 'Tester',
        roles: ['ADMIN'],
        permissions: [
          'quotations.create',
          'quotations.update',
          'quotations.print',
          'quotations.view_all',
          'quotations.view_cost',
          'quotations.accounting_confirm',
        ],
      },
    });
  });

  it('renders a resize handle on regular column headers but not on the actions header', async () => {
    renderPage();
    const codeHeader = await screen.findByRole('columnheader', { name: 'Số báo giá' });
    expect(within(codeHeader).getByTestId('column-resize-handle')).toBeInTheDocument();

    const headers = screen.getAllByRole('columnheader');
    const actionsHeader = headers[headers.length - 1];
    expect(within(actionsHeader).queryByTestId('column-resize-handle')).not.toBeInTheDocument();
  });

  it('renders a colgroup with one col per header column', async () => {
    renderPage();
    await screen.findByRole('columnheader', { name: 'Số báo giá' });
    const headers = screen.getAllByRole('columnheader');
    const cols = document.querySelectorAll('col');
    expect(cols.length).toBe(headers.length);
  });
});
```

2. Run test to verify it fails — `cd frontend && npx vitest run src/pages/quotations/quotation-list-page.test.tsx` / Expected: FAIL — no `column-resize-handle` testid present yet (page not wired), and/or no `<col>` elements found (colgroup not rendered).

### Task 4: Wire resizing into `quotation-list-page.tsx`
1. Write minimal implementation. Edit `frontend/src/pages/quotations/quotation-list-page.tsx`:

Add imports:
```tsx
import { useColumnSizingPersist } from '@/lib/use-column-sizing-persist';
import { TableColGroup } from '@/components/ui/table';
import {
  QUOTATION_LIST_STORAGE_KEY,
  QUOTATION_LIST_COLUMN_SIZING_DEFAULTS,
  COLUMN_MIN_SIZE,
} from './column-sizing-defaults';
```

Inside `QuotationListPage`, after the existing `pendingTransition` state declaration, add:
```tsx
const [columnSizing, setColumnSizing] = useColumnSizingPersist(
  QUOTATION_LIST_STORAGE_KEY,
  QUOTATION_LIST_COLUMN_SIZING_DEFAULTS,
);
```

Update each column definition in the `columns` `useMemo` array to include `size`/`minSize` matching `QUOTATION_LIST_COLUMN_SIZING_DEFAULTS`, e.g.:
```tsx
{ header: 'Số báo giá', accessorKey: 'code', size: QUOTATION_LIST_COLUMN_SIZING_DEFAULTS.code, minSize: COLUMN_MIN_SIZE },
```
Apply the same `size`/`minSize` pattern to every column definition (`quotationDate`, `revenueDate`, `deliveryDate`, `customerName`, `subtotal`, `discount`, `freight`, `taxRate`, `taxAmount`, `total`, `advancePayment`, `totalCost`, `grossProfit`, `status`, `owner`, `createdByName`, `contactPhone`), reusing the same key name as the column's `accessorKey`/`id` to look up `QUOTATION_LIST_COLUMN_SIZING_DEFAULTS[...]`.

For the `actions` column definition, add:
```tsx
{
  id: 'actions',
  header: '',
  size: QUOTATION_LIST_COLUMN_SIZING_DEFAULTS.actions,
  minSize: QUOTATION_LIST_COLUMN_SIZING_DEFAULTS.actions,
  enableResizing: false,
  cell: ({ row }) => ( /* unchanged */ ),
},
```

Update the `useReactTable` call:
```tsx
const table = useReactTable({
  data: data?.items ?? [],
  columns,
  state: { columnSizing },
  onColumnSizingChange: setColumnSizing,
  columnResizeMode: 'onChange',
  enableColumnResizing: true,
  getCoreRowModel: getCoreRowModel(),
});
```

Update the table JSX render block. Replace:
```tsx
<Table containerClassName="h-full" className="min-w-max">
  <TableHeader className="sticky top-0 z-10">
    {table.getHeaderGroups().map((hg) => (
      <TableRow key={hg.id}>
        {hg.headers.map((h) => (
          <TableHead
            key={h.id}
            className={h.column.id === 'actions' ? 'sticky right-0 z-20 bg-background shadow-[-2px_0_4px_-1px_rgba(0,0,0,0.1)]' : ''}
          >
            {flexRender(h.column.columnDef.header, h.getContext())}
          </TableHead>
        ))}
      </TableRow>
    ))}
  </TableHeader>
```
with:
```tsx
<Table
  containerClassName="h-full"
  className="min-w-max table-fixed"
  style={{ width: table.getTotalSize() }}
>
  <TableColGroup widths={table.getHeaderGroups()[0].headers.map((h) => h.getSize())} />
  <TableHeader className="sticky top-0 z-10">
    {table.getHeaderGroups().map((hg) => (
      <TableRow key={hg.id}>
        {hg.headers.map((h) => (
          <TableHead
            key={h.id}
            className={h.column.id === 'actions' ? 'sticky right-0 z-20 bg-background shadow-[-2px_0_4px_-1px_rgba(0,0,0,0.1)]' : ''}
            resizable={
              h.column.getCanResize()
                ? { onResizeStart: h.getResizeHandler(), isResizing: h.column.getIsResizing() }
                : undefined
            }
          >
            {flexRender(h.column.columnDef.header, h.getContext())}
          </TableHead>
        ))}
      </TableRow>
    ))}
  </TableHeader>
```
(`style` needs to be threaded through the `Table` component's props — it already spreads `...props` onto the `<table>` element per Phase 02's unchanged `Table` implementation, so `style` passes through natively; no further change to `Table` needed.)

2. Run tests to verify they pass — `cd frontend && npx vitest run src/pages/quotations/quotation-list-page.test.tsx` / Expected: PASS (2 tests).
3. Run full test suite — `cd frontend && npx vitest run` / Expected: PASS (no regressions).
4. Run typecheck — `cd frontend && npx tsc --noEmit` / Expected: no errors.
5. Commit — `git commit -m "feat(quotations): make list table columns resizable with persisted widths"`

### Task 5: Manual browser verification
1. Start the dev server: `cd frontend && npm run dev`.
2. Open the quotation list page in a browser.
3. Drag the right edge of the "Khách hàng" header — confirm the column visibly grows/shrinks live, cursor shows `col-resize`, and a highlighted bar appears while dragging.
4. Confirm the rightmost "actions" column (icon buttons) shows no drag handle and does not change width when dragging adjacent columns.
5. Resize 2-3 columns, then reload the page — confirm the resized widths are restored (persisted via `localStorage`, key `qldh:col-sizes:quotations-list` visible in DevTools Application tab).
6. Log in as (or simulate via role/permission toggle) a user without `quotations.view_cost` — confirm the page loads without console errors even though `totalCost`/`grossProfit` sizing keys may be present in `localStorage` from a previous admin session.
7. Widen several columns until the table exceeds the viewport width — confirm the table container scrolls horizontally (existing `overflow-auto` behavior preserved) and the sticky "actions" column remains pinned to the right edge while scrolling.
8. Visit at least 2 other pages using the shared `Table` component (e.g. `frontend/src/pages/customers/customer-list-page.tsx` and `frontend/src/pages/products/product-list-page.tsx`) — confirm their column layout/proportions look unchanged (they do not pass `table-fixed` or `TableColGroup`, so they keep the browser's automatic table layout per Phase 02 Task 4's decision).

## Verification
- `cd frontend && npx vitest run` → full suite passes
- `cd frontend && npx tsc --noEmit` → no type errors
- `cd frontend && npm run build` → build succeeds
- Manual steps in Task 5 all confirmed

## Exit Criteria
- Quotation list table columns are resizable by dragging header edges, except the `actions` column which stays fixed and sticky.
- Resized widths persist across page reloads via `localStorage`.
- No regressions observed on other pages using the shared `Table` component.
