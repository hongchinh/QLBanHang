# Phase 02 — Shared Table component resize support

**Status:** [x] done
**Complexity:** M

## Objective
Extend the shared `Table`/`TableHead` components (`frontend/src/components/ui/table.tsx`) to optionally render a drag-resize handle, and add a `TableColGroup` helper for `table-layout: fixed` + explicit column widths, without breaking any of the 11 existing pages that use `Table` but don't opt into resizing.

## Files
- `frontend/src/components/ui/table.tsx` (modify)
- `frontend/src/components/ui/table.test.tsx` (new)

## Context (current state of table.tsx, for reference)
`TableHead` currently:
```tsx
const TableHead = React.forwardRef<HTMLTableCellElement, React.ThHTMLAttributes<HTMLTableCellElement>>(
  ({ className, ...props }, ref) => (
    <th
      ref={ref}
      className={cn(
        'qldh-table-head text-left align-middle font-semibold text-white [&:has([role=checkbox])]:pr-0',
        className,
      )}
      {...props}
    />
  ),
);
```
`Table` currently wraps a `<table className="qldh-table w-full caption-bottom">` in a `<div className="relative w-full overflow-auto">`.

## Tasks

### Task 1: Write failing tests for `TableHead` resize handle
1. Write the failing test — create `frontend/src/components/ui/table.test.tsx`:

```tsx
import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { TableHead, TableColGroup } from './table';

describe('TableHead resize handle', () => {
  it('does not render a resize handle by default', () => {
    render(
      <table>
        <thead>
          <tr>
            <TableHead>Col A</TableHead>
          </tr>
        </thead>
      </table>,
    );
    expect(screen.queryByTestId('column-resize-handle')).not.toBeInTheDocument();
  });

  it('renders a resize handle when resizable props are provided', () => {
    const onResizeStart = vi.fn();
    render(
      <table>
        <thead>
          <tr>
            <TableHead
              resizable={{
                onResizeStart,
                isResizing: false,
              }}
            >
              Col A
            </TableHead>
          </tr>
        </thead>
      </table>,
    );
    expect(screen.getByTestId('column-resize-handle')).toBeInTheDocument();
  });

  it('calls onResizeStart on mousedown of the handle', () => {
    const onResizeStart = vi.fn();
    render(
      <table>
        <thead>
          <tr>
            <TableHead resizable={{ onResizeStart, isResizing: false }}>Col A</TableHead>
          </tr>
        </thead>
      </table>,
    );
    screen.getByTestId('column-resize-handle').dispatchEvent(
      new MouseEvent('mousedown', { bubbles: true }),
    );
    expect(onResizeStart).toHaveBeenCalledTimes(1);
  });

  it('applies an active-resize class when isResizing is true', () => {
    render(
      <table>
        <thead>
          <tr>
            <TableHead resizable={{ onResizeStart: vi.fn(), isResizing: true }}>Col A</TableHead>
          </tr>
        </thead>
      </table>,
    );
    expect(screen.getByTestId('column-resize-handle').className).toContain('bg-primary');
  });
});

describe('TableColGroup', () => {
  it('renders one col element per width entry', () => {
    const { container } = render(
      <table>
        <TableColGroup widths={[100, 200, 50]} />
      </table>,
    );
    const cols = container.querySelectorAll('col');
    expect(cols).toHaveLength(3);
    expect((cols[0] as HTMLElement).style.width).toBe('100px');
    expect((cols[1] as HTMLElement).style.width).toBe('200px');
    expect((cols[2] as HTMLElement).style.width).toBe('50px');
  });
});
```

2. Run test to verify it fails — `cd frontend && npx vitest run src/components/ui/table.test.tsx` / Expected: FAIL — `resizable` prop not recognized / `TableColGroup` not exported (TS/module error).

### Task 2: Implement `resizable` prop on `TableHead` and add `TableColGroup`
1. Write minimal implementation. Edit `frontend/src/components/ui/table.tsx`:

Add near the top, after imports:
```tsx
export interface TableHeadResizableProps {
  onResizeStart: (e: React.MouseEvent | React.TouchEvent) => void;
  isResizing: boolean;
}
```

Update `TableHead`:
```tsx
interface TableHeadProps extends React.ThHTMLAttributes<HTMLTableCellElement> {
  resizable?: TableHeadResizableProps;
}

const TableHead = React.forwardRef<HTMLTableCellElement, TableHeadProps>(
  ({ className, resizable, children, ...props }, ref) => (
    <th
      ref={ref}
      className={cn(
        'qldh-table-head relative text-left align-middle font-semibold text-white [&:has([role=checkbox])]:pr-0',
        className,
      )}
      {...props}
    >
      {children}
      {resizable && (
        <div
          data-testid="column-resize-handle"
          onMouseDown={resizable.onResizeStart}
          onTouchStart={resizable.onResizeStart}
          className={cn(
            'absolute right-0 top-0 h-full w-2 cursor-col-resize touch-none select-none',
            'hover:bg-white/30',
            resizable.isResizing && 'bg-primary',
          )}
        />
      )}
    </th>
  ),
);
```

Add a new exported component `TableColGroup` (place after `Table`):
```tsx
function TableColGroup({ widths }: { widths: number[] }) {
  return (
    <colgroup>
      {widths.map((w, i) => (
        <col key={i} style={{ width: `${w}px` }} />
      ))}
    </colgroup>
  );
}
```

Update the final export statement to include the new items:
```tsx
export { Table, TableHeader, TableBody, TableFooter, TableHead, TableRow, TableCell, TableColGroup };
```

Note: `resizable` is optional and defaults to `undefined`, so all 11 existing call sites of `TableHead` continue to work unchanged (no resize handle rendered, `relative` positioning class added is visually inert without the handle).

2. Run tests to verify they pass — `cd frontend && npx vitest run src/components/ui/table.test.tsx` / Expected: PASS (5 tests).
3. Commit — `git commit -m "feat(table): add optional resize handle to TableHead and TableColGroup helper"`

### Task 3: Manually verify `table-layout: fixed` does not break existing pages (deferred to Phase 03)
This is a cross-cutting visual check, not a unit test. It is performed once in Phase 03 Task 4 after the quotation list page adopts the colgroup, since that is when `table-layout: fixed` actually gets applied to a real `<table>` element (the shared `Table` component itself does not force `table-layout: fixed` globally — see Task 4 below for why).

### Task 4: Decide where `table-layout: fixed` is applied
1. Re-examine scope: applying `table-layout: fixed` unconditionally inside the shared `<table>` element (in `Table`) would affect all 11 existing pages immediately, none of which pass a `<colgroup>` today — this would break their column proportions (browsers default to equal-width columns under `fixed` layout without explicit widths).
2. Decision: do NOT set `table-layout: fixed` as a hardcoded class inside `Table`. Instead, allow callers to opt in via the existing `className` prop on `Table` (e.g. `<Table className="table-fixed">`), only passed by pages that also render a `<TableColGroup>`. This keeps the shared component non-breaking for the other 10 pages by default, while satisfying the quotation list page's need in Phase 03.
3. No code change needed for this task — `Table`'s `className` prop already merges via `cn(...)` (see existing implementation), so `table-fixed` (Tailwind's utility for `table-layout: fixed`) can be passed directly from the quotation list page in Phase 03. Confirm this utility class is available by checking Tailwind config includes core plugins (default Tailwind ships `table-layout` utilities under `tableLayout` core plugin, enabled by default — no config change expected).
4. Run `cd frontend && grep -n "corePlugins" tailwind.config.*` (or open the file) to confirm `tableLayout` is not disabled. Expected: no `corePlugins` block disabling it, or it's absent entirely (default enabled).

## Verification
- `cd frontend && npx vitest run src/components/ui/table.test.tsx` → all pass
- `cd frontend && npx tsc --noEmit` → no new type errors
- `cd frontend && npx vitest run` → full suite still passes (no regressions in other component tests)

## Exit Criteria
- `TableHead` accepts an optional `resizable` prop rendering a drag handle with hover/active styling, tested.
- `TableColGroup` helper exists and renders `<col>` elements matching given pixel widths, tested.
- Confirmed `table-fixed` Tailwind utility is available for opt-in use per table instance (not hardcoded globally), so the 10 other pages using `Table` are unaffected.
