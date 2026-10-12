# Phase 04 — Per-user pins and chips

**Status:** [x] complete
**Complexity:** M

## Objective

Let each signed-in user pin menu items (stored per `userId` in `ui-store`, persisted in localStorage). Pinned items show as chips above Tổng quan: at most 6 chips, the rest behind a dashed "+N" chip that expands in place (last chip becomes "Thu gọn", not persisted). Chips keep pin order, the chip of the current page is `primary`, × unpins. Each list item has a ☆ toggle (`aria-pressed`): a pinned item shows a yellow ★ always; an unpinned one shows ☆ on hover/focus, and always (dimmed) on touch devices. Pins of items the user can no longer see are hidden but kept. Tổng quan cannot be pinned. Chips are hidden while searching.

## Files

- `frontend/src/stores/ui-store.ts` (modify)
- `frontend/src/stores/ui-store.test.ts` (modify)
- `frontend/src/components/layout/sidebar/nav-search.ts` (modify)
- `frontend/src/components/layout/sidebar/nav-search.test.ts` (modify)
- `frontend/src/components/layout/sidebar/sidebar-item.tsx` (modify)
- `frontend/src/components/layout/sidebar/sidebar-group.tsx` (modify)
- `frontend/src/components/layout/sidebar/sidebar-pin-chips.tsx` (new)
- `frontend/src/components/layout/sidebar/sidebar-pin-chips.test.tsx` (new)
- `frontend/src/components/layout/sidebar/sidebar.tsx` (modify)
- `frontend/src/components/layout/sidebar/sidebar.test.tsx` (modify)

## Tasks

### Task 4.1 — `pinnedNavPaths` and `togglePin` in `ui-store`

1. **Write the failing test** — append to `ui-store.test.ts`:
   ```ts
   describe('useUiStore pinnedNavPaths', () => {
     beforeEach(() => {
       localStorage.clear();
       useUiStore.setState({ pinnedNavPaths: {} });
     });

     it('keeps pins per user in the order they were added', () => {
       const { togglePin } = useUiStore.getState();
       togglePin('u1', '/receipts');
       togglePin('u1', '/quotations');
       togglePin('u2', '/stock-in');
       expect(useUiStore.getState().pinnedNavPaths).toEqual({
         u1: ['/receipts', '/quotations'],
         u2: ['/stock-in'],
       });
     });

     it('toggling a pinned path removes it', () => {
       const { togglePin } = useUiStore.getState();
       togglePin('u1', '/receipts');
       togglePin('u1', '/quotations');
       togglePin('u1', '/receipts');
       expect(useUiStore.getState().pinnedNavPaths).toEqual({ u1: ['/quotations'] });
     });

     it('persists the pins', () => {
       useUiStore.getState().togglePin('u1', '/receipts');
       expect(readPersisted()?.state.pinnedNavPaths).toEqual({ u1: ['/receipts'] });
     });

     it('loads a version 1 payload that predates the field', async () => {
       localStorage.setItem(STORAGE_KEY, JSON.stringify({ state: { sidebarCollapsed: false }, version: 1 }));
       await useUiStore.persist.rehydrate();
       expect(useUiStore.getState().pinnedNavPaths).toEqual({});
     });
   });
   ```
2. **Run test to verify it fails:** `npx vitest run src/stores/ui-store.test.ts`. Expected: FAIL — `togglePin` is not a function.
3. **Write minimal implementation** in `ui-store.ts`:
   ```ts
   // Per user id, in pin order. Kept even when an item becomes invisible (hidden at render).
   pinnedNavPaths: Record<string, string[]>;
   togglePin: (userId: string, path: string) => void;
   ```
   initial `pinnedNavPaths: {}`;
   ```ts
   togglePin: (userId, path) =>
     set((s) => {
       const current = s.pinnedNavPaths[userId] ?? [];
       const next = current.includes(path) ? current.filter((p) => p !== path) : [...current, path];
       return { pinnedNavPaths: { ...s.pinnedNavPaths, [userId]: next } };
     }),
   ```
   `partialize` adds `pinnedNavPaths: state.pinnedNavPaths`; `version` stays `1`.
4. **Run tests to verify they pass:** `npx vitest run src/stores`. Expected: PASS.
5. **Commit:**
   ```bash
   git add frontend/src/stores/ui-store.ts frontend/src/stores/ui-store.test.ts
   git commit -m "feat(frontend): per-user sidebar pins in the ui store"
   ```

### Task 4.2 — `resolvePinnedItems`

1. **Write the failing test** — append to `nav-search.test.ts`:
   ```ts
   import { resolvePinnedItems } from './nav-search';

   describe('resolvePinnedItems', () => {
     const groups = allNavGroups();

     it('returns visible items in pin order', () => {
       expect(resolvePinnedItems(['/receipts', '/quotations'], groups).map((i) => i.label)).toEqual(['Phiếu thu', 'Báo giá']);
     });

     it('skips paths without a visible item', () => {
       const salesOnly = groups.filter((g) => g.id === 'sales');
       expect(resolvePinnedItems(['/receipts', '/gone', '/quotations'], salesOnly).map((i) => i.to)).toEqual(['/quotations']);
     });
   });
   ```
2. **Run test to verify it fails:** `npx vitest run src/components/layout/sidebar/nav-search.test.ts`. Expected: FAIL — `resolvePinnedItems` is not exported.
3. **Write minimal implementation** in `nav-search.ts`:
   ```ts
   // Items for the stored pin paths, in pin order. Paths the user can no longer see
   // (lost permission, removed route) are skipped here but stay in the store.
   export function resolvePinnedItems(paths: readonly string[], groups: readonly NavGroup[]): NavItem[] {
     const byPath = new Map(groups.flatMap((g) => g.items.map((i) => [i.to, i] as const)));
     return paths.flatMap((p) => byPath.get(p) ?? []);
   }
   ```
4. **Run tests to verify they pass:** `npx vitest run src/components/layout/sidebar/nav-search.test.ts`. Expected: PASS.
5. **Commit:**
   ```bash
   git add frontend/src/components/layout/sidebar/nav-search.ts frontend/src/components/layout/sidebar/nav-search.test.ts
   git commit -m "feat(frontend): resolve visible pinned nav items"
   ```

### Task 4.3 — ☆ toggle on list items

1. **Write the failing test** — in `sidebar.test.tsx`: add `import { useAuthStore } from '@/stores/auth-store';`, a fixture
   ```ts
   const TEST_USER = { id: 'u1', username: 'u1', email: 'u1@x.vn', fullName: 'User 1', roles: [], permissions: [] };
   ```
   extend `beforeEach` with `useUiStore.setState({ …, pinnedNavPaths: {} })` and `useAuthStore.setState({ user: TEST_USER })`, then:
   ```tsx
   describe('Sidebar pin toggle', () => {
     it('pins and unpins an item for the signed-in user', async () => {
       const user = userEvent.setup();
       renderSidebar('/');
       const star = screen.getByRole('button', { name: 'Ghim Phiếu thu' });
       expect(star).toHaveAttribute('aria-pressed', 'false');
       await user.click(star);
       expect(star).toHaveAttribute('aria-pressed', 'true');
       expect(useUiStore.getState().pinnedNavPaths).toEqual({ u1: ['/receipts'] });
       await user.click(star);
       expect(useUiStore.getState().pinnedNavPaths).toEqual({ u1: [] });
     });

     it('Tổng quan has no pin button', () => {
       renderSidebar('/');
       expect(screen.queryByRole('button', { name: 'Ghim Tổng quan' })).not.toBeInTheDocument();
     });

     it('shows no pin buttons without a user', () => {
       useAuthStore.setState({ user: null });
       renderSidebar('/');
       expect(screen.queryByRole('button', { name: /^Ghim / })).not.toBeInTheDocument();
     });
   });
   ```
2. **Run test to verify it fails:** `npx vitest run src/components/layout/sidebar/sidebar.test.tsx`. Expected: FAIL — no button "Ghim Phiếu thu".
3. **Write minimal implementation.**
   - `sidebar-item.tsx`:
     ```ts
     export interface PinControls {
       pinned: ReadonlySet<string>;
       toggle: (path: string) => void;
     }
     ```
     `SidebarItem` gains `pins?: PinControls` and computes `const pinned = pins?.pinned.has(item.to) ?? false;`. The expanded row becomes `<div className="group/item flex items-center">` with the link and, when `pins` is set:
     ```tsx
     <button
       type="button"
       aria-label={`Ghim ${item.label}`}
       aria-pressed={pinned}
       onClick={() => pins.toggle(item.to)}
       className={cn(
         'ml-0.5 shrink-0 rounded p-1 text-muted-foreground hover:text-foreground focus-visible:opacity-100 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
         pinned
           ? 'text-amber-500 opacity-100'
           : 'opacity-0 group-hover/item:opacity-100 [@media(hover:none)]:opacity-60',
       )}
     >
       <Star className={cn('h-3.5 w-3.5', pinned && 'fill-current')} aria-hidden />
     </button>
     ```
     (`text-amber-500` is a Tailwind palette class for the yellow ★, not a color code.) `SidebarItemList` gains `pins?: PinControls` and passes it to every item.
   - `sidebar-group.tsx`: new optional prop `pins?: PinControls` passed to `SidebarItemList`.
   - `sidebar.tsx`:
     ```tsx
     const EMPTY_PINS: string[] = []; // module scope: a stable fallback for the selector
     …
     const userId = useAuthStore((s) => s.user?.id ?? null);
     const pinnedPaths = useUiStore((s) => (userId ? s.pinnedNavPaths[userId] : undefined)) ?? EMPTY_PINS;
     const togglePin = useUiStore((s) => s.togglePin);
     const pins = useMemo<PinControls | undefined>(
       () => (userId ? { pinned: new Set(pinnedPaths), toggle: (path) => togglePin(userId, path) } : undefined),
       [userId, pinnedPaths, togglePin],
     );
     ```
     Pass `pins` to every `SidebarGroup` (browse and search mode). The dashboard `SidebarItem` gets no `pins`.
4. **Run tests to verify they pass:** `npx vitest run src/components/layout`. Expected: PASS.
5. **Commit:**
   ```bash
   git add frontend/src/components/layout/sidebar/sidebar-item.tsx frontend/src/components/layout/sidebar/sidebar-group.tsx frontend/src/components/layout/sidebar/sidebar.tsx frontend/src/components/layout/sidebar/sidebar.test.tsx
   git commit -m "feat(frontend): pin toggle on sidebar items"
   ```

### Task 4.4 — `SidebarPinChips`

1. **Write the failing test** `frontend/src/components/layout/sidebar/sidebar-pin-chips.test.tsx`:
   ```tsx
   import { render, screen, within } from '@testing-library/react';
   import userEvent from '@testing-library/user-event';
   import { MemoryRouter } from 'react-router-dom';
   import { describe, expect, it, vi } from 'vitest';
   import { allNavGroups } from './sidebar-test-utils';
   import { SidebarPinChips } from './sidebar-pin-chips';

   const ITEMS = allNavGroups().flatMap((g) => g.items).slice(0, 7);

   function renderChips(count: number, activePath: string | null = null, onUnpin = vi.fn()) {
     render(
       <MemoryRouter>
         <SidebarPinChips items={ITEMS.slice(0, count)} activePath={activePath} onUnpin={onUnpin} />
       </MemoryRouter>,
     );
     return { onUnpin, list: () => screen.getByRole('list', { name: 'Mục đã ghim' }) };
   }

   describe('SidebarPinChips', () => {
     it('shows 6 chips and +N for the rest, in pin order', () => {
       const { list } = renderChips(7);
       const links = within(list()).getAllByRole('link');
       expect(links.map((a) => a.textContent)).toEqual(ITEMS.slice(0, 6).map((i) => i.label));
       expect(within(list()).getByRole('button', { name: '+1' })).toHaveAttribute('aria-expanded', 'false');
     });

     it('+N expands in place and Thu gọn collapses', async () => {
       const user = userEvent.setup();
       const { list } = renderChips(7);
       await user.click(screen.getByRole('button', { name: '+1' }));
       expect(within(list()).getAllByRole('link')).toHaveLength(7);
       await user.click(screen.getByRole('button', { name: 'Thu gọn' }));
       expect(within(list()).getAllByRole('link')).toHaveLength(6);
     });

     it('shows no +N chip with 6 pins or fewer', () => {
       renderChips(6);
       expect(screen.queryByRole('button', { name: /^\+/ })).not.toBeInTheDocument();
     });

     it('× unpins an item', async () => {
       const user = userEvent.setup();
       const { onUnpin } = renderChips(2);
       await user.click(screen.getByRole('button', { name: `Bỏ ghim ${ITEMS[1].label}` }));
       expect(onUnpin).toHaveBeenCalledWith(ITEMS[1].to);
     });

     it('marks the chip of the current page and keeps the full name in a tooltip', () => {
       renderChips(3, ITEMS[2].to);
       const chip = screen.getByRole('link', { name: ITEMS[2].label });
       expect(chip).toHaveAttribute('aria-current', 'page');
       expect(chip).toHaveAttribute('title', ITEMS[2].label);
     });
   });
   ```
2. **Run test to verify it fails:** `npx vitest run src/components/layout/sidebar/sidebar-pin-chips.test.tsx`. Expected: FAIL — cannot resolve `./sidebar-pin-chips`.
3. **Write minimal implementation** `frontend/src/components/layout/sidebar/sidebar-pin-chips.tsx`:
   ```tsx
   const MAX_CHIPS = 6;

   interface SidebarPinChipsProps {
     items: NavItem[];
     activePath: string | null;
     onUnpin: (path: string) => void;
   }

   export function SidebarPinChips({ items, activePath, onUnpin }: SidebarPinChipsProps) {
     // Not persisted: every page load starts collapsed.
     const [expanded, setExpanded] = useState(false);
     const hiddenCount = items.length - MAX_CHIPS;
     const shown = expanded ? items : items.slice(0, MAX_CHIPS);
     …
   }
   ```
   Markup: `<div className="mb-2 border-b border-dashed pb-2">` › `<ul aria-label="Mục đã ghim" className="flex flex-wrap gap-1">`. Each chip `<li className="group/chip flex max-w-[120px] items-center rounded-full border text-xs">` with classes `border-nav-pin-border bg-nav-pin-bg text-nav-pin-fg`, or `border-primary bg-primary text-primary-foreground` when `item.to === activePath`; inside: `<Link to={item.to} title={item.label} aria-current={active ? 'page' : undefined} className="truncate py-0.5 pl-2 pr-1 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring rounded-full">{item.label}</Link>` and `<button type="button" aria-label={`Bỏ ghim ${item.label}`} onClick={() => onUnpin(item.to)} className="mr-1 shrink-0 rounded-full opacity-0 group-hover/chip:opacity-100 group-focus-within/chip:opacity-100 focus-visible:opacity-100 [@media(hover:none)]:opacity-100"><X className="h-3 w-3" aria-hidden /></button>`. When `hiddenCount > 0`: a last `<li>` with `<button type="button" aria-expanded={expanded} onClick={() => setExpanded((e) => !e)} className="rounded-full border border-dashed px-2 py-0.5 text-xs text-muted-foreground">{expanded ? 'Thu gọn' : `+${hiddenCount}`}</button>`.
4. **Run tests to verify they pass:** `npx vitest run src/components/layout/sidebar/sidebar-pin-chips.test.tsx`. Expected: PASS.
5. **Commit:**
   ```bash
   git add frontend/src/components/layout/sidebar/sidebar-pin-chips.tsx frontend/src/components/layout/sidebar/sidebar-pin-chips.test.tsx
   git commit -m "feat(frontend): pinned item chips"
   ```

### Task 4.5 — Chips in the sidebar

1. **Write the failing test** — in `sidebar.test.tsx`:
   ```tsx
   describe('Sidebar pin chips', () => {
     const chips = () => screen.queryByRole('list', { name: 'Mục đã ghim' });

     it('shows no chip area without pins', () => {
       renderSidebar('/');
       expect(chips()).not.toBeInTheDocument();
     });

     it('shows the user pins as chips above Tổng quan', async () => {
       const user = userEvent.setup();
       renderSidebar('/');
       await user.click(screen.getByRole('button', { name: 'Ghim Phiếu thu' }));
       expect(within(chips()!).getByRole('link', { name: 'Phiếu thu' })).toBeInTheDocument();
       await user.click(within(chips()!).getByRole('button', { name: 'Bỏ ghim Phiếu thu' }));
       expect(chips()).not.toBeInTheDocument();
     });

     it('hides pins of another user and of invisible items', () => {
       useUiStore.setState({ pinnedNavPaths: { u2: ['/receipts'], u1: ['/gone', '/quotations'] } });
       renderSidebar('/');
       expect(within(chips()!).getAllByRole('link').map((a) => a.textContent)).toEqual(['Báo giá']);
       expect(useUiStore.getState().pinnedNavPaths.u1).toEqual(['/gone', '/quotations']);
     });

     it('hides the chips while searching', async () => {
       const user = userEvent.setup();
       useUiStore.setState({ pinnedNavPaths: { u1: ['/quotations'] } });
       renderSidebar('/');
       await user.type(searchBox(), 'thu');
       expect(chips()).not.toBeInTheDocument();
     });
   });
   ```
2. **Run test to verify it fails:** `npx vitest run src/components/layout/sidebar/sidebar.test.tsx`. Expected: FAIL — no list "Mục đã ghim".
3. **Write minimal implementation** in `sidebar.tsx`:
   ```tsx
   const pinnedItems = useMemo(() => resolvePinnedItems(pinnedPaths, groups), [pinnedPaths, groups]);
   ```
   In browse mode, first inside the nav: `{pins && pinnedItems.length > 0 && <SidebarPinChips items={pinnedItems} activePath={activePath} onUnpin={pins.toggle} />}`, then Tổng quan and the groups. Not rendered while searching.
4. **Run tests to verify they pass:** `npx vitest run src/components/layout`. Expected: PASS.
5. **Commit:**
   ```bash
   git add frontend/src/components/layout/sidebar/sidebar.tsx frontend/src/components/layout/sidebar/sidebar.test.tsx
   git commit -m "feat(frontend): show pinned chips in the sidebar"
   ```

### Task 4.6 — Phase checks

1. From `frontend/`: `npm run typecheck`, `npm run lint`, `npm run test` (compare with `BASELINE.md`), `npm run build`, then `git checkout -- tsconfig.app.tsbuildinfo tsconfig.node.tsbuildinfo`.
2. Manual: pin 8 items → 6 chips + "+2"; a long name ("Tạo mã QR thanh toán") truncates with a native tooltip; ☆ appears on hover, ★ stays; log in as another user on the same browser → no pins shown; chip colors readable in dark mode; in the mobile drawer (devtools touch emulation) ☆ and × are always visible.
3. Tick Phase 04 in `SUMMARY.md`, set this file's status, commit `chore(plan): sidebar redesign phase 04 complete`.

## Verification

- `npx vitest run src/components/layout src/stores`
- `npm run typecheck && npm run lint && npm run test && npm run build`

## Exit Criteria

- Pins per user, in pin order, persisted; invisible pins hidden but kept; Tổng quan not pinnable.
- Chips: max 6 + "+N"/"Thu gọn", × unpins, active chip `primary`, hidden while searching.
- Tests, typecheck, lint, build at baseline.
