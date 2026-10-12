# Phase 03 — Accent-insensitive search

**Status:** [ ] pending
**Complexity:** M

## Objective

Add the sticky "Tìm chức năng…" box above the scrolling nav. Typing filters items by item label, group label or report section, ignoring Vietnamese accents; matching groups are forced open with a result count; the matched part of an item label is highlighted; pins (Phase 04) and Tổng quan are hidden while searching. Keys: Enter opens the first result, ↓ moves real focus into the results, ↑/↓ move between result links (↑ on the first returns to the box), Esc or × clears and refocuses the box. The box clears on navigation and when the sidebar collapses.

## Files

- `frontend/src/components/layout/sidebar/nav-search.ts` (modify)
- `frontend/src/components/layout/sidebar/nav-search.test.ts` (modify)
- `frontend/src/components/layout/sidebar/sidebar-search.tsx` (new)
- `frontend/src/components/layout/sidebar/sidebar-item.tsx` (modify)
- `frontend/src/components/layout/sidebar/sidebar-group.tsx` (modify)
- `frontend/src/components/layout/sidebar/sidebar.tsx` (modify)
- `frontend/src/components/layout/sidebar/sidebar.test.tsx` (modify)

## Tasks

### Task 3.1 — `normalizeVi` and `filterNavGroups`

1. **Write the failing test** — append to `nav-search.test.ts`:
   ```ts
   import { allNavGroups } from './sidebar-test-utils';
   import { filterNavGroups, normalizeVi } from './nav-search';

   describe('normalizeVi', () => {
     it('strips accents, maps đ to d and lowercases', () => {
       expect(normalizeVi('Đối trừ chứng từ')).toBe('doi tru chung tu');
       expect(normalizeVi('ĐƯỜNG Hóa')).toBe('duong hoa');
     });

     it('keeps one character per input character', () => {
       for (const label of ['Đối trừ chứng từ', 'Số dư công nợ', 'Lý do thu/chi']) {
         expect(normalizeVi(label)).toHaveLength(label.normalize('NFC').length);
       }
       expect(normalizeVi('Đối'.normalize('NFD'))).toBe('doi');
     });
   });

   describe('filterNavGroups', () => {
     const groups = allNavGroups();
     const ids = (r: ReturnType<typeof filterNavGroups>) => r.groups.map((g) => g.id);
     const tos = (r: ReturnType<typeof filterNavGroups>, id: string) => r.groups.find((g) => g.id === id)?.items.map((i) => i.to);

     it('matches item labels without accents and returns the highlight range', () => {
       const r = filterNavGroups(groups, 'doi tru');
       expect(ids(r)).toEqual(['debt']);
       expect(tos(r, 'debt')).toEqual(['/debt/matching']);
       expect(r.matches.get('/debt/matching')).toEqual({ start: 0, end: 7 });
       expect(r.count).toBe(1);
     });

     it('a group label match returns every item of the group', () => {
       const r = filterNavGroups(groups, 'bao cao');
       expect(ids(r)).toEqual(['reports']);
       expect(r.count).toBe(14);
       expect(r.matches.size).toBe(0);
     });

     it('a section match returns every item of the section', () => {
       const r = filterNavGroups(groups, 'cong no');
       expect(ids(r)).toEqual(['debt', 'reports']);
       expect(tos(r, 'debt')).toEqual(['/debt/matching', '/debt/offsets', '/debt/opening', '/credit-limits']);
       expect(tos(r, 'reports')).toContain('/reports/debt-aging');
       expect(r.matches.has('/reports/debt-aging')).toBe(false);
       expect(r.matches.get('/reports/debt-balance')).toEqual({ start: 6, end: 13 });
       expect(r.count).toBe(11);
     });

     it('trims the query; an empty query keeps every group', () => {
       const r = filterNavGroups(groups, '   ');
       expect(ids(r)).toEqual(groups.map((g) => g.id));
       expect(r.count).toBe(groups.reduce((n, g) => n + g.items.length, 0));
       expect(filterNavGroups(groups, '  Phieu thu ').matches.get('/receipts')).toEqual({ start: 0, end: 9 });
     });

     it('returns nothing for an unknown word', () => {
       expect(filterNavGroups(groups, 'xyz')).toEqual({ groups: [], matches: new Map(), count: 0 });
     });
   });
   ```
2. **Run test to verify it fails:** `npx vitest run src/components/layout/sidebar/nav-search.test.ts`. Expected: FAIL — `normalizeVi` / `filterNavGroups` are not exported.
3. **Write minimal implementation** in `nav-search.ts`:
   ```ts
   import type { NavGroup, NavItem } from '../nav-config';

   export interface MatchRange {
     start: number;
     end: number;
   }

   export interface NavFilterResult {
     groups: NavGroup[];
     // Keyed by item `to`; absent when the item matched through its group or section.
     matches: Map<string, MatchRange>;
     count: number;
   }

   // Each character of the NFC input maps to one output character, so an index in the
   // result is an index in the (NFC) label and can be used to highlight it.
   export function normalizeVi(text: string): string {
     return Array.from(text.normalize('NFC'), (ch) =>
       ch.normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/[đĐ]/g, 'd').toLowerCase(),
     ).join('');
   }

   const countItems = (groups: readonly NavGroup[]) => groups.reduce((n, g) => n + g.items.length, 0);

   export function filterNavGroups(groups: readonly NavGroup[], query: string): NavFilterResult {
     const q = normalizeVi(query.trim());
     const matches = new Map<string, MatchRange>();
     if (!q) return { groups: [...groups], matches, count: countItems(groups) };

     const result: NavGroup[] = [];
     for (const group of groups) {
       const groupHit = normalizeVi(group.label).includes(q);
       const items = group.items.filter((item: NavItem) => {
         const start = normalizeVi(item.label).indexOf(q);
         if (start >= 0) {
           matches.set(item.to, { start, end: start + q.length });
           return true;
         }
         return groupHit || (item.section !== undefined && normalizeVi(item.section).includes(q));
       });
       if (items.length > 0) result.push({ ...group, items });
     }
     return { groups: result, matches, count: countItems(result) };
   }
   ```
4. **Run tests to verify they pass:** `npx vitest run src/components/layout/sidebar/nav-search.test.ts`. Expected: PASS.
5. **Commit:**
   ```bash
   git add frontend/src/components/layout/sidebar/nav-search.ts frontend/src/components/layout/sidebar/nav-search.test.ts
   git commit -m "feat(frontend): accent-insensitive nav filter"
   ```

### Task 3.2 — Search box, filtered view, Enter, clear on navigation

1. **Write the failing test** — in `sidebar.test.tsx`:
   ```tsx
   const searchBox = () => screen.getByRole('searchbox', { name: 'Tìm chức năng' });

   describe('Sidebar search', () => {
     it('filters items, hides Tổng quan and highlights the match', async () => {
       const user = userEvent.setup();
       renderSidebar('/');
       await user.type(searchBox(), 'doi tru');
       expect(screen.getByRole('link', { name: 'Đối trừ chứng từ' })).toBeInTheDocument();
       expect(screen.queryByRole('link', { name: 'Phiếu thu' })).not.toBeInTheDocument();
       expect(screen.queryByRole('link', { name: 'Tổng quan' })).not.toBeInTheDocument();
       expect(screen.getByText('Đối trừ', { selector: 'mark' })).toBeInTheDocument();
       expect(screen.getByText('1 kết quả')).toBeInTheDocument();
     });

     it('opens collapsed groups that have results and shows their count', async () => {
       const user = userEvent.setup();
       renderSidebar('/');
       await user.type(searchBox(), 'hang hoa');
       expect(screen.getByRole('link', { name: 'Hàng hóa' })).toBeInTheDocument();
       expect(screen.getByRole('link', { name: 'Nhóm hàng hóa' })).toBeInTheDocument();
       expect(screen.queryByRole('button', { name: 'Danh mục' })).not.toBeInTheDocument();
       expect(screen.getByText('Danh mục').parentElement).toHaveTextContent('Danh mục2');
       expect(useUiStore.getState().sidebarGroupOpen).toEqual({});
     });

     it('shows a section match', async () => {
       const user = userEvent.setup();
       renderSidebar('/');
       await user.type(searchBox(), 'cong no');
       expect(screen.getByRole('link', { name: 'Tuổi nợ' })).toBeInTheDocument();
     });

     it('shows an empty state', async () => {
       const user = userEvent.setup();
       renderSidebar('/');
       await user.type(searchBox(), 'xyz');
       expect(screen.getByText('Không tìm thấy chức năng')).toBeInTheDocument();
       expect(screen.getByText('Thử từ khóa ngắn hơn')).toBeInTheDocument();
     });

     it('treats a blank query as no search', async () => {
       const user = userEvent.setup();
       renderSidebar('/');
       await user.type(searchBox(), '   ');
       expect(screen.getByRole('link', { name: 'Tổng quan' })).toBeInTheDocument();
     });

     it('Enter opens the first result and the box clears after navigation', async () => {
       const user = userEvent.setup();
       const { pathname } = renderSidebar('/');
       await user.type(searchBox(), 'phieu{Enter}');
       expect(pathname()).toBe('/stock-in');
       expect(searchBox()).toHaveValue('');
     });

     it('clears when another route is opened', async () => {
       const user = userEvent.setup();
       const { navigate } = renderSidebar('/');
       await user.type(searchBox(), 'thu');
       navigate('/receipts');
       expect(searchBox()).toHaveValue('');
     });
   });
   ```
   ("phieu" → first result is Kho › Phiếu nhập kho: Bán hàng has no "phieu" item.)
2. **Run test to verify it fails:** `npx vitest run src/components/layout/sidebar/sidebar.test.tsx`. Expected: FAIL — no searchbox named "Tìm chức năng".
3. **Write minimal implementation.**
   - `frontend/src/components/layout/sidebar/sidebar-search.tsx`:
     ```tsx
     interface SidebarSearchProps {
       value: string;
       onChange: (value: string) => void;
       onEnter: () => void;
       onArrowDown: () => void;
       onClear: () => void;
       // null when not searching.
       resultCount: number | null;
       inputRef: React.RefObject<HTMLInputElement>;
       controlsId: string;
     }
     ```
     Renders `<div className="shrink-0 border-b p-3">` with a relative wrapper: `Search` icon (left, `aria-hidden`), `<input ref={inputRef} type="search" aria-label="Tìm chức năng" placeholder="Tìm chức năng…" aria-controls={controlsId} value onChange className="h-9 w-full rounded-md border bg-background pl-8 pr-8 text-sm [&::-webkit-search-cancel-button]:hidden …">`; `onKeyDown`: `Enter` → `preventDefault(); onEnter()`, `ArrowDown` → `preventDefault(); onArrowDown()`, `Escape` → `preventDefault(); onClear()`. When `value` is not empty, a `button type="button" aria-label="Xóa tìm kiếm"` with `X` icon calls `onClear`. Below: `<div aria-live="polite" className="sr-only">{resultCount === null ? '' : `${resultCount} kết quả`}</div>`.
   - `sidebar-item.tsx`:
     - `SidebarItem` gains `match?: MatchRange`; the label renders `<Highlight text={item.label} match={match} />`:
       ```tsx
       function Highlight({ text, match }: { text: string; match?: MatchRange }) {
         if (!match) return <>{text}</>;
         const nfc = text.normalize('NFC');
         return (
           <>
             {nfc.slice(0, match.start)}
             <mark className="rounded-sm bg-nav-match text-inherit">{nfc.slice(match.start, match.end)}</mark>
             {nfc.slice(match.end)}
           </>
         );
       }
       ```
     - `SidebarItemList` gains `matches?: ReadonlyMap<string, MatchRange>` and passes `matches?.get(item.to)`.
   - `sidebar-group.tsx`: new optional props `resultCount?: number` and `matches?: ReadonlyMap<string, MatchRange>`. When `resultCount !== undefined` (search mode) the header is a non-interactive `div` with the same icon block and name, and `<span className="text-xs tabular-nums text-muted-foreground">{resultCount}</span>` in place of the chevron; the list is always shown. `matches` is passed to `SidebarItemList`.
   - `sidebar.tsx`:
     ```tsx
     const navigate = useNavigate();
     const navId = `${idPrefix}-nav`;
     const inputRef = useRef<HTMLInputElement>(null);
     const [query, setQuery] = useState('');
     const searching = query.trim() !== '';
     const filtered = useMemo(() => (searching ? filterNavGroups(groups, query) : null), [searching, groups, query]);

     // The box clears on navigation and when the sidebar collapses.
     useEffect(() => { setQuery(''); }, [pathname, collapsed]);

     const clearSearch = useCallback(() => {
       setQuery('');
       inputRef.current?.focus();
     }, []);
     const openFirstResult = () => {
       const first = filtered?.groups[0]?.items[0];
       if (first) navigate(first.to);
     };
     const focusFirstResult = () => {
       document.getElementById(navId)?.querySelector<HTMLAnchorElement>('a[href]')?.focus();
     };
     ```
     Render `<SidebarSearch value={query} onChange={setQuery} onEnter={openFirstResult} onArrowDown={focusFirstResult} onClear={clearSearch} resultCount={filtered?.count ?? null} inputRef={inputRef} controlsId={navId} />` above `<nav id={navId} …>`. Inside the nav:
     - searching and `filtered.count === 0`: `<div className="flex flex-col items-center gap-1 py-8 text-center text-sm text-muted-foreground"><SearchX className="h-6 w-6" aria-hidden /><p>Không tìm thấy chức năng</p><p className="text-xs">Thử từ khóa ngắn hơn</p></div>`;
     - searching with results: `filtered.groups.map((g) => <SidebarGroup key={g.id} group={g} open onToggle={() => {}} resultCount={g.items.length} matches={filtered.matches} activePath={activePath} listId={`${idPrefix}-group-${g.id}`} />)`;
     - not searching: the dashboard and the Phase 02 groups as before.
4. **Run tests to verify they pass:** `npx vitest run src/components/layout`. Expected: PASS.
5. **Commit:**
   ```bash
   git add frontend/src/components/layout/sidebar/sidebar-search.tsx frontend/src/components/layout/sidebar/sidebar-item.tsx frontend/src/components/layout/sidebar/sidebar-group.tsx frontend/src/components/layout/sidebar/sidebar.tsx frontend/src/components/layout/sidebar/sidebar.test.tsx
   git commit -m "feat(frontend): search box that filters the sidebar"
   ```

### Task 3.3 — Keyboard navigation in results and Esc/× clear

1. **Write the failing test** — in `sidebar.test.tsx`:
   ```tsx
   describe('Sidebar search keyboard', () => {
     it('↓ moves focus into the results, ↑ on the first returns to the box', async () => {
       const user = userEvent.setup();
       renderSidebar('/');
       await user.type(searchBox(), 'phieu');
       await user.keyboard('{ArrowDown}');
       expect(screen.getByRole('link', { name: 'Phiếu nhập kho' })).toHaveFocus();
       await user.keyboard('{ArrowDown}');
       expect(screen.getByRole('link', { name: 'Phiếu xuất kho' })).toHaveFocus();
       await user.keyboard('{ArrowUp}{ArrowUp}');
       expect(searchBox()).toHaveFocus();
     });

     it('Esc in the results clears the box and focuses it', async () => {
       const user = userEvent.setup();
       renderSidebar('/');
       await user.type(searchBox(), 'phieu');
       await user.keyboard('{ArrowDown}{Escape}');
       expect(searchBox()).toHaveValue('');
       expect(searchBox()).toHaveFocus();
     });

     it('Esc in the box and the × button clear it', async () => {
       const user = userEvent.setup();
       renderSidebar('/');
       await user.type(searchBox(), 'thu{Escape}');
       expect(searchBox()).toHaveValue('');
       await user.type(searchBox(), 'thu');
       await user.click(screen.getByRole('button', { name: 'Xóa tìm kiếm' }));
       expect(searchBox()).toHaveValue('');
       expect(searchBox()).toHaveFocus();
     });
   });
   ```
2. **Run test to verify it fails:** `npx vitest run src/components/layout/sidebar/sidebar.test.tsx`. Expected: FAIL — the second ↓ leaves focus on "Phiếu nhập kho" (no handler in the results) and Esc in the results does not clear. (The Esc-in-box / × test may already pass from Task 3.2; that is fine.)
3. **Write minimal implementation** in `sidebar.tsx` — a native listener on the nav while searching (a React `onKeyDown` on `<nav>` trips `jsx-a11y/no-noninteractive-element-interactions`):
   ```tsx
   const navRef = useRef<HTMLElement>(null);
   useEffect(() => {
     const nav = navRef.current;
     if (!nav || !searching) return;
     const onKeyDown = (e: KeyboardEvent) => {
       if (e.key === 'Escape') {
         e.preventDefault();
         clearSearch();
         return;
       }
       const links = Array.from(nav.querySelectorAll<HTMLAnchorElement>('a[href]'));
       const index = links.indexOf(document.activeElement as HTMLAnchorElement);
       if (index < 0) return;
       if (e.key === 'ArrowDown') {
         e.preventDefault();
         links[index + 1]?.focus();
       } else if (e.key === 'ArrowUp') {
         e.preventDefault();
         (index === 0 ? inputRef.current : links[index - 1])?.focus();
       }
     };
     nav.addEventListener('keydown', onKeyDown);
     return () => nav.removeEventListener('keydown', onKeyDown);
   }, [searching, clearSearch]);
   ```
   Attach `ref={navRef}` to the `<nav>`; `focusFirstResult` can use `navRef.current` instead of `getElementById`. Result links keep the visible `focus-visible:ring-2 focus-visible:ring-ring` ring (add it to the `SidebarItem` link classes if missing).
4. **Run tests to verify they pass:** `npx vitest run src/components/layout`. Expected: PASS.
5. **Commit:**
   ```bash
   git add frontend/src/components/layout/sidebar/sidebar.tsx frontend/src/components/layout/sidebar/sidebar-item.tsx frontend/src/components/layout/sidebar/sidebar.test.tsx
   git commit -m "feat(frontend): keyboard navigation in sidebar search results"
   ```

### Task 3.4 — Phase checks

1. From `frontend/`: `npm run typecheck`, `npm run lint`, `npm run test` (compare with `BASELINE.md`), `npm run build`, then `git checkout -- tsconfig.app.tsbuildinfo tsconfig.node.tsbuildinfo`.
2. Manual: the box stays fixed while the nav scrolls; "bao cao" lists every report; the highlight is visible in light and dark mode; Ctrl+K still opens the header search only.
3. Tick Phase 03 in `SUMMARY.md`, set this file's status, commit `chore(plan): sidebar redesign phase 03 complete`.

## Verification

- `npx vitest run src/components/layout`
- `npm run typecheck && npm run lint && npm run test && npm run build`

## Exit Criteria

- Accent-insensitive filter over item, group and section labels; highlight on item labels; counts in headers; empty state; `aria-live` count.
- Enter / ↓ / ↑ / Esc / × behave as in section-01 §3.3; box clears on navigation and collapse; Tổng quan hidden while searching.
- Tests, typecheck, lint, build at baseline.
