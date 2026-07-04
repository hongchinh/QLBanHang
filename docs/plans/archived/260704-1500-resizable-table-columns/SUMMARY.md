# Resizable Table Columns (Quotation List + Shared Table)

## Goal
Let users drag-resize column widths in the shared `Table` component, and adopt it in the quotation list page (`frontend/src/pages/quotations/quotation-list-page.tsx`). Column widths persist per-browser via `localStorage`, keyed per table. The sticky "actions" column stays fixed width and is never resizable.

## Scope
- In scope:
  - New hook `useColumnSizingPersist` in `frontend/src/lib/` for reading/writing `columnSizing` state to `localStorage`.
  - Update shared `Table`/`TableHead` (`frontend/src/components/ui/table.tsx`) to support an optional resize handle, `colgroup`-based fixed layout.
  - Adopt resizing in `frontend/src/pages/quotations/quotation-list-page.tsx` only.
  - Manual visual verification that other pages using `Table` still render correctly after the layout change.
- Out of scope:
  - Adopting resizing in any other page (reports, customers, products, admin, etc.) — they get the layout change (colgroup/fixed) for free but do NOT opt into resizing in this plan.
  - Column reordering, column visibility toggles, or persisting column order.
  - Automated E2E/browser drag-simulation tests (jsdom cannot simulate real mouse-drag pointer capture reliably) — covered by manual verification instead.

## Assumptions
- `localStorage` is available (already used elsewhere in the app's environment; jsdom test setup supports it too).
- TanStack Table version in use (`@tanstack/react-table`) supports `columnResizeMode`, `enableColumnResizing`, `getCanResize()`, `getResizeHandler()`, `getIsResizing()`, `getSize()`, `getTotalSize()` — these are stable APIs present since v8, confirmed via existing `package.json` dependency (checked in Phase 1).
- The `actions` column in quotation-list-page is identified by `id: 'actions'` and is the only sticky column requiring `enableResizing: false`.
- No design-system token exists yet for a resize-handle color; use `bg-white/30` default / `bg-primary` while dragging, matching the header's white text on colored background (`qldh-table-head`).

## Risks
- Switching to `table-layout: fixed` + `<colgroup>` changes the rendering of **every** page using the shared `Table` component (11 files). Risk: a table whose columns don't sum to a sensible total width could look broken (columns of default TanStack size 150px each, overflowing awkwardly) if that page doesn't pass explicit sizes. Mitigated by keeping default TanStack column size (150) as fallback and verifying visually per Phase 3.
- Resize handle hit-area overlapping with sortable/clickable header content (none of the current tables have sortable headers, so low risk).
- `columnSizing` in localStorage referencing stale column ids (e.g., after conditional columns like `totalCost`/`grossProfit` are hidden for a user without `quotations.view_cost`) — mitigated by only ever spreading known-default keys and letting TanStack ignore extras.

## Phases
- [x] Phase 01 — Column sizing persistence hook (S) — `phase-01-persist-hook.md`
- [x] Phase 02 — Shared Table component resize support (M) — `phase-02-shared-table.md`
- [x] Phase 03 — Quotation list page adoption + manual verification (S) — `phase-03-quotation-list-adoption.md`

## Final Verification
- `cd frontend && npm test -- --run` (all unit tests pass, including new `useColumnSizingPersist` tests)
- `cd frontend && npm run build` (TypeScript compiles cleanly)
- Manual browser verification steps listed in Phase 03.

## Rollback / Recovery
- All changes are additive and client-side only (no API/backend changes, no migrations).
- To roll back: revert the three changed/added files (`frontend/src/lib/use-column-sizing-persist.ts` + test, `frontend/src/components/ui/table.tsx`, `frontend/src/pages/quotations/quotation-list-page.tsx`). No data cleanup needed — stale `localStorage` keys (`qldh:col-sizes:*`) are harmless if left behind since nothing reads them once the feature is removed.
