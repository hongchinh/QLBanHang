# Phase 02 — Color tokens, collapsible groups, remembered state, temporary open

**Status:** [x] complete
**Complexity:** M

## Objective

Draw the style C group header (colored 26px icon block, bold name, chevron), make groups collapsible with several open at once, remember the user's choice in `ui-store`, start with every group open except Danh mục and Hệ thống, and temporarily open the group of the current page without writing to the store. Make the expanded nav a scrolling region (`flex-1 min-h-0 overflow-y-auto`) so Phase 03 can put the search box above it.

## Files

- `frontend/src/index.css` (modify)
- `frontend/tailwind.config.ts` (modify)
- `frontend/src/components/layout/sidebar/nav-tokens.test.ts` (new)
- `frontend/src/stores/ui-store.ts` (modify)
- `frontend/src/stores/ui-store.test.ts` (modify)
- `frontend/src/components/layout/sidebar/sidebar-group.tsx` (new)
- `frontend/src/components/layout/sidebar/sidebar-item.tsx` (modify)
- `frontend/src/components/layout/sidebar/sidebar.tsx` (modify)
- `frontend/src/components/layout/sidebar/sidebar.test.tsx` (modify)

## Tasks

### Task 2.1 — `--nav-*` tokens

Token values are bare HSL channels like the existing ones (`--primary: 221.2 83.2% 53.3%`), because Tailwind reads them through `hsl(var(--x))`. Alpha goes inside the channel value (`… / 0.12`).

1. **Write the failing test** `frontend/src/components/layout/sidebar/nav-tokens.test.ts` (reads the files as text; jsdom runs with `css: false`):
   ```ts
   import { readFileSync } from 'node:fs';
   import { resolve } from 'node:path';
   import { describe, expect, it } from 'vitest';

   // Vitest runs from frontend/.
   const css = readFileSync(resolve(process.cwd(), 'src/index.css'), 'utf8');
   const tailwind = readFileSync(resolve(process.cwd(), 'tailwind.config.ts'), 'utf8');
   const block = (selector: string) => css.slice(css.indexOf(`${selector} {`), css.indexOf('}', css.indexOf(`${selector} {`)));

   const GROUPS = ['sales', 'inventory', 'cash', 'debt', 'reports', 'catalogs', 'system'];
   const HELPERS = ['group-bg', 'pin-bg', 'pin-fg', 'pin-border', 'match'];
   const CHANNELS = /^\d+(\.\d+)? \d+(\.\d+)?% \d+(\.\d+)?%( \/ \d?\.\d+)?$/;

   describe('nav color tokens', () => {
     it('defines group colors in :root and helpers in :root and .dark', () => {
       for (const id of GROUPS) expect(block(':root')).toContain(`--nav-${id}:`);
       for (const id of HELPERS) {
         expect(block(':root')).toContain(`--nav-${id}:`);
         expect(block('.dark')).toContain(`--nav-${id}:`);
       }
     });

     it('writes every value as bare HSL channels', () => {
       const values = [...css.matchAll(/--nav-[\w-]+:\s*([^;]+);/g)].map((m) => m[1].trim());
       expect(values.length).toBe(GROUPS.length + HELPERS.length * 2);
       for (const v of values) expect(v).toMatch(CHANNELS);
     });

     it('maps every token in tailwind.config.ts', () => {
       for (const id of [...GROUPS, ...HELPERS]) expect(tailwind).toContain(`'hsl(var(--nav-${id}))'`);
     });
   });
   ```
2. **Run test to verify it fails:** `npx vitest run src/components/layout/sidebar/nav-tokens.test.ts`. Expected: FAIL — `:root` block does not contain `--nav-sales:`.
3. **Write minimal implementation.**
   - `index.css`, inside `:root` after `--header-danger`:
     ```css
     /* Sidebar group colors (same in dark mode) and helpers. */
     --nav-sales: 221.2 83.2% 53.3%;
     --nav-inventory: 32.1 94.6% 43.7%;
     --nav-cash: 161.4 93.5% 30.4%;
     --nav-debt: 0 72.2% 50.6%;
     --nav-reports: 262.1 83.3% 57.8%;
     --nav-catalogs: 191.6 91.4% 36.5%;
     --nav-system: 215.4 16.3% 34.9%;
     --nav-group-bg: 210 40% 97%;
     --nav-pin-bg: 45 100% 95%;
     --nav-pin-fg: 30 80% 30%;
     --nav-pin-border: 45 90% 80%;
     --nav-match: 48 100% 85%;
     ```
     inside `.dark` after `--header-danger`:
     ```css
     --nav-group-bg: 217 33% 11%;
     --nav-pin-bg: 37.7 92.1% 50.2% / 0.12;
     --nav-pin-fg: 43.3 96.4% 56.3%;
     --nav-pin-border: 37.7 92.1% 50.2% / 0.35;
     --nav-match: 47.9 95.8% 53.1% / 0.3;
     ```
   - `tailwind.config.ts`, in `theme.extend.colors` after `header`:
     ```ts
     nav: {
       sales: 'hsl(var(--nav-sales))',
       inventory: 'hsl(var(--nav-inventory))',
       cash: 'hsl(var(--nav-cash))',
       debt: 'hsl(var(--nav-debt))',
       reports: 'hsl(var(--nav-reports))',
       catalogs: 'hsl(var(--nav-catalogs))',
       system: 'hsl(var(--nav-system))',
       'group-bg': 'hsl(var(--nav-group-bg))',
       'pin-bg': 'hsl(var(--nav-pin-bg))',
       'pin-fg': 'hsl(var(--nav-pin-fg))',
       'pin-border': 'hsl(var(--nav-pin-border))',
       match: 'hsl(var(--nav-match))',
     },
     ```
     (Classes: `bg-nav-sales` … `bg-nav-system`, `bg-nav-group-bg`, `bg-nav-pin-bg`, `text-nav-pin-fg`, `border-nav-pin-border`, `bg-nav-match`.)
4. **Run tests to verify they pass:** `npx vitest run src/components/layout/sidebar/nav-tokens.test.ts`. Expected: PASS.
5. **Commit:**
   ```bash
   git add frontend/src/index.css frontend/tailwind.config.ts frontend/src/components/layout/sidebar/nav-tokens.test.ts
   git commit -m "feat(frontend): sidebar group color tokens"
   ```

### Task 2.2 — `sidebarGroupOpen` in `ui-store`

1. **Write the failing test** — append to `frontend/src/stores/ui-store.test.ts`:
   ```ts
   describe('useUiStore sidebarGroupOpen', () => {
     beforeEach(() => {
       localStorage.clear();
       useUiStore.setState({ sidebarCollapsed: false, sidebarGroupOpen: {} });
     });

     it('starts empty so groups use their defaults', () => {
       expect(useUiStore.getState().sidebarGroupOpen).toEqual({});
     });

     it('records and persists the user choice per group', () => {
       useUiStore.getState().setSidebarGroupOpen('catalogs', true);
       useUiStore.getState().setSidebarGroupOpen('sales', false);
       expect(useUiStore.getState().sidebarGroupOpen).toEqual({ catalogs: true, sales: false });
       expect(readPersisted()?.state.sidebarGroupOpen).toEqual({ catalogs: true, sales: false });
     });

     it('loads a version 1 payload that predates the field', async () => {
       localStorage.setItem(STORAGE_KEY, JSON.stringify({ state: { sidebarCollapsed: true }, version: 1 }));
       await useUiStore.persist.rehydrate();
       expect(useUiStore.getState().sidebarCollapsed).toBe(true);
       expect(useUiStore.getState().sidebarGroupOpen).toEqual({});
     });
   });
   ```
2. **Run test to verify it fails:** `npx vitest run src/stores/ui-store.test.ts`. Expected: FAIL — `setSidebarGroupOpen` is not a function (and a type error on the unknown field).
3. **Write minimal implementation** in `ui-store.ts`:
   - `UiState` gains
     ```ts
     // Only groups the user opened/closed by clicking; others use NavGroup.defaultOpen.
     // Shared by every user of this browser (a display preference).
     sidebarGroupOpen: Record<string, boolean>;
     setSidebarGroupOpen: (groupId: string, open: boolean) => void;
     ```
   - initial `sidebarGroupOpen: {}`; action `setSidebarGroupOpen: (groupId, open) => set((s) => ({ sidebarGroupOpen: { ...s.sidebarGroupOpen, [groupId]: open } }))`;
   - `partialize` adds `sidebarGroupOpen: state.sidebarGroupOpen`. `version` stays `1` (only a field is added; the default shallow merge keeps `{}` for old payloads).
4. **Run tests to verify they pass:** `npx vitest run src/stores`. Expected: PASS.
5. **Commit:**
   ```bash
   git add frontend/src/stores/ui-store.ts frontend/src/stores/ui-store.test.ts
   git commit -m "feat(frontend): remember sidebar group open state"
   ```

### Task 2.3 — `SidebarGroup` accordion with temporary open

1. **Write the failing test** — in `sidebar.test.tsx`, change `beforeEach` to `useUiStore.setState({ sidebarCollapsed: false, sidebarGroupOpen: {} })`, add `import userEvent from '@testing-library/user-event';` and:
   ```tsx
   const header = (name: string) => screen.getByRole('button', { name });

   describe('Sidebar groups', () => {
     it('opens every group except Danh mục and Hệ thống by default', () => {
       renderSidebar('/');
       for (const name of ['Bán hàng', 'Kho', 'Thu chi', 'Công nợ', 'Báo cáo']) {
         expect(header(name)).toHaveAttribute('aria-expanded', 'true');
       }
       expect(header('Danh mục')).toHaveAttribute('aria-expanded', 'false');
       expect(header('Hệ thống')).toHaveAttribute('aria-expanded', 'false');
       expect(screen.queryByRole('link', { name: 'Hàng hóa' })).not.toBeInTheDocument();
     });

     it('toggles a group and stores the choice', async () => {
       const user = userEvent.setup();
       renderSidebar('/');
       const sales = header('Bán hàng');
       expect(sales).toHaveAttribute('aria-controls', expect.any(String));
       await user.click(sales);
       expect(sales).toHaveAttribute('aria-expanded', 'false');
       expect(useUiStore.getState().sidebarGroupOpen).toEqual({ sales: false });
       await user.click(sales);
       expect(sales).toHaveAttribute('aria-expanded', 'true');
       expect(useUiStore.getState().sidebarGroupOpen).toEqual({ sales: true });
     });

     it('uses the stored choice', () => {
       useUiStore.setState({ sidebarGroupOpen: { inventory: false, catalogs: true } });
       renderSidebar('/');
       expect(header('Kho')).toHaveAttribute('aria-expanded', 'false');
       expect(header('Danh mục')).toHaveAttribute('aria-expanded', 'true');
     });

     it('opens the group of the current page temporarily, without touching the store', () => {
       const { navigate } = renderSidebar('/');
       navigate('/products');
       expect(header('Danh mục')).toHaveAttribute('aria-expanded', 'true');
       expect(useUiStore.getState().sidebarGroupOpen).toEqual({});
       navigate('/receipts');
       expect(header('Danh mục')).toHaveAttribute('aria-expanded', 'false');
     });

     it('closes a temporarily open group on click and stores it', async () => {
       const user = userEvent.setup();
       renderSidebar('/products');
       await user.click(header('Danh mục'));
       expect(header('Danh mục')).toHaveAttribute('aria-expanded', 'false');
       expect(useUiStore.getState().sidebarGroupOpen).toEqual({ catalogs: false });
     });

     it('keeps group state on a page without an item', () => {
       renderSidebar('/admin/user-settings/u1');
       expect(header('Danh mục')).toHaveAttribute('aria-expanded', 'false');
       expect(header('Bán hàng')).toHaveAttribute('aria-expanded', 'true');
     });
   });
   ```
2. **Run test to verify it fails:** `npx vitest run src/components/layout/sidebar/sidebar.test.tsx`. Expected: FAIL — no button named "Bán hàng".
3. **Write minimal implementation.**
   - `sidebar-item.tsx`: `SidebarItemList` gains an optional `hidden?: boolean` passed to the `<ul>`.
   - `frontend/src/components/layout/sidebar/sidebar-group.tsx`:
     ```tsx
     interface SidebarGroupProps {
       group: NavGroup;
       open: boolean;
       onToggle: () => void;
       activePath: string | null;
       listId: string;
     }
     ```
     Renders `<section className="mt-2">` with a `button type="button"` (`aria-expanded={open}`, `aria-controls={listId}`, classes `flex w-full items-center gap-2 rounded-md bg-nav-group-bg px-2 py-1.5 text-sm font-semibold text-foreground hover:bg-accent`), containing `<span aria-hidden className={cn('flex h-[26px] w-[26px] shrink-0 items-center justify-center rounded-md text-white', group.color)}><Icon className="h-4 w-4" /></span>`, `<span className="flex-1 truncate text-left">{group.label}</span>` and `ChevronDown` (open) / `ChevronRight` (closed) `h-4 w-4 text-muted-foreground`. Below: `<SidebarItemList id={listId} hidden={!open} label={group.label} items={group.items} activePath={activePath} />` (the list always exists so `aria-controls` resolves).
   - `sidebar.tsx`:
     ```tsx
     const idPrefix = useId();
     const sidebarGroupOpen = useUiStore((s) => s.sidebarGroupOpen);
     const setSidebarGroupOpen = useUiStore((s) => s.setSidebarGroupOpen);
     const activeGroupId = activeItem ? (groups.find((g) => g.items.includes(activeItem))?.id ?? null) : null;
     // The group of the current page opens temporarily on every route change (never stored).
     // A header click on it records the choice and drops the temporary state until the next route change.
     const [tempOpen, setTempOpen] = useState<{ pathname: string; groupId: string | null } | null>(null);
     const tempGroupId = tempOpen?.pathname === pathname ? tempOpen.groupId : activeGroupId;
     const isOpen = (g: NavGroup) => g.id === tempGroupId || (sidebarGroupOpen[g.id] ?? g.defaultOpen);
     const toggleGroup = (g: NavGroup) => {
       setSidebarGroupOpen(g.id, !isOpen(g));
       if (g.id === tempGroupId) setTempOpen({ pathname, groupId: null });
     };
     ```
     Expanded layout (keep the mobile close bar when `onClose`):
     ```tsx
     <nav aria-label="Chức năng" className="min-h-0 flex-1 overflow-y-auto overflow-x-hidden p-3">
       <ul>{/* dashboard SidebarItem */}</ul>
       {groups.map((g) => (
         <SidebarGroup key={g.id} group={g} open={isOpen(g)} onToggle={() => toggleGroup(g)}
           activePath={activePath} listId={`${idPrefix}-group-${g.id}`} />
       ))}
     </nav>
     ```
     Phase 03 puts `SidebarSearch` (`shrink-0`) above the `<nav>`. Collapsed layout is unchanged in this phase.
4. **Run tests to verify they pass:** `npx vitest run src/components/layout`. Expected: PASS (Phase 01 tests too: the system group is temporarily open on `/settings/my-quotation-settings`).
5. **Commit:**
   ```bash
   git add frontend/src/components/layout/sidebar/sidebar-group.tsx frontend/src/components/layout/sidebar/sidebar-item.tsx frontend/src/components/layout/sidebar/sidebar.tsx frontend/src/components/layout/sidebar/sidebar.test.tsx
   git commit -m "feat(frontend): collapsible sidebar groups that open for the current page"
   ```

### Task 2.4 — Phase checks

1. From `frontend/`: `npm run typecheck`, `npm run lint`, `npm run test` (compare with `BASELINE.md`), `npm run build`, then `git checkout -- tsconfig.app.tsbuildinfo tsconfig.node.tsbuildinfo`.
2. Manual (`npm run dev`): admin, ~700px window: headers show the 7 colors, Danh mục/Hệ thống start closed, the nav scrolls; toggle dark mode — header background uses `--nav-group-bg`, icon blocks keep their color.
3. Tick Phase 02 in `SUMMARY.md`, set this file's status, commit `chore(plan): sidebar redesign phase 02 complete` (stage `docs/plans/261012-2311-sidebar-navigation-redesign`).

## Verification

- `npx vitest run src/components/layout src/stores`
- `npm run typecheck && npm run lint && npm run test && npm run build`

## Exit Criteria

- Tokens defined (light/dark) and mapped; no color code in components.
- Groups toggle with `aria-expanded`/`aria-controls` (ids unique per sidebar instance); choice persisted; defaults applied; current page's group opens temporarily without a store write.
- Tests, typecheck, lint, build at baseline.
