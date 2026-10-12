# Phase 01 — Branch, baseline, regroup, icons, longest-prefix highlight

**Status:** [ ] pending
**Complexity:** M

## Objective

Start the redesign branch from `main` after Round 2B, record the test baseline, regroup the menu into the 7 task-based groups with `id` / `icon` / `color` / `defaultOpen` / `section`, give every item its own icon, and highlight the active item by the longest segment-boundary prefix of `pathname`. Groups are still always expanded in this phase (accordion comes in Phase 02); the collapsed mode still shows one icon per item (rail comes in Phase 05).

## Files

- `docs/plans/261012-2311-sidebar-navigation-redesign/BASELINE.md` (new)
- `frontend/src/components/layout/nav-config.ts` (rewrite)
- `frontend/src/components/layout/nav-config.test.ts` (rewrite)
- `frontend/src/components/layout/sidebar/nav-search.ts` (new)
- `frontend/src/components/layout/sidebar/nav-search.test.ts` (new)
- `frontend/src/components/layout/sidebar/sidebar-item.tsx` (new)
- `frontend/src/components/layout/sidebar/sidebar.tsx` (modify)
- `frontend/src/components/layout/sidebar/sidebar-test-utils.tsx` (new)
- `frontend/src/components/layout/sidebar/sidebar.test.tsx` (new)
- `frontend/src/components/layout/app-layout.tsx` (modify: type import only)

## Tasks

### Task 1.1 — Preconditions, branch, baseline

1. From `D:/Projects/QLBanHang`: `git fetch qlbanhang`.
2. Check Round 2B is merged: `git cat-file -e qlbanhang/main:frontend/src/pages/cash-reports/debt-reconciliation-page.tsx && echo merged`. Expected: `merged`. Otherwise **stop** and tell the user Phase 01 waits for the Round 2B merge.
3. `git status --short`: tracked modifications must be limited to docs (`docs/SUMMARY.md`). Then `git switch -c feat/sidebar-nav-redesign qlbanhang/main`. If git refuses because of local changes, stop and ask the user.
4. Check the reconciliation item is in the merged `nav-config.ts`: `grep -n "debt-reconciliation" frontend/src/components/layout/nav-config.ts`. Note the merged order of the debt report items (needed in Task 1.3).
5. Baseline from `frontend/`: `npm ci` (if `node_modules` is stale), then
   ```bash
   npm run test 2>&1 | tee "$TEMP/sidebar-baseline-test.txt" | grep -E "^ *(FAIL|×)" ; tail -5 "$TEMP/sidebar-baseline-test.txt"
   npm run lint 2>&1 | tail -3
   npm run typecheck
   ```
   Write `docs/plans/261012-2311-sidebar-navigation-redesign/BASELINE.md`: date, commit (`git rev-parse --short HEAD`), the list of failing test files/names, the totals line, lint `N errors, M warnings`, typecheck result.
6. **Commit** the design and plan docs plus the baseline:
   ```bash
   git add docs/brainstorms/261012-2135-sidebar-navigation-redesign docs/plans/261012-2311-sidebar-navigation-redesign
   git commit -m "docs: sidebar navigation redesign brainstorm and plan"
   ```
   (`docs/SUMMARY.md` is updated in Phase 06.)

### Task 1.2 — `findActiveItem` (longest segment-boundary prefix)

1. **Write the failing test** `frontend/src/components/layout/sidebar/nav-search.test.ts`:
   ```ts
   import { describe, expect, it } from 'vitest';
   import { LayoutDashboard } from 'lucide-react';
   import type { NavItem } from '../nav-config';
   import { findActiveItem } from './nav-search';

   const item = (to: string): NavItem => ({ to, label: to, icon: LayoutDashboard });
   const ITEMS = [
     item('/admin/dashboard'), item('/quotations'), item('/settings'),
     item('/settings/my-quotation-settings'), item('/reports/sales-revenue'), item('/admin/users'),
   ];

   describe('findActiveItem', () => {
     it.each([
       ['/settings/my-quotation-settings', '/settings/my-quotation-settings'],
       ['/settings', '/settings'],
       ['/settings/branches', '/settings'],
       ['/quotations/123', '/quotations'],
       ['/quotations/', '/quotations'],
       ['/reports/sales-revenue/0b6c', '/reports/sales-revenue'],
       ['/admin/dashboard', '/admin/dashboard'],
     ])('%s → %s', (pathname, expected) => {
       expect(findActiveItem(pathname, ITEMS)?.to).toBe(expected);
     });

     it('matches on segment boundaries only', () => {
       expect(findActiveItem('/settings-x', ITEMS)).toBeNull();
       expect(findActiveItem('/quotationsx/1', ITEMS)).toBeNull();
     });

     it('returns null for a page without an item', () => {
       expect(findActiveItem('/admin/user-settings/u1', ITEMS)).toBeNull();
     });

     it('matches "/" exactly', () => {
       const withRoot = [item('/'), ...ITEMS];
       expect(findActiveItem('/', withRoot)?.to).toBe('/');
       expect(findActiveItem('/unknown', withRoot)).toBeNull();
       expect(findActiveItem('/quotations/1', withRoot)?.to).toBe('/quotations');
     });
   });
   ```
2. **Run test to verify it fails:** `npx vitest run src/components/layout/sidebar/nav-search.test.ts`. Expected: FAIL — cannot resolve `./nav-search` (and `NavItem` is not exported from `../nav-config` yet; that is part of this RED).
3. **Write minimal implementation:**
   - In `nav-config.ts` add (and export) the render type, keeping everything else for now:
     ```ts
     export interface NavItem {
       to: string;
       label: string;
       icon: React.ComponentType<{ className?: string }>;
       // Sub-heading inside a group (Báo cáo only).
       section?: string;
     }
     ```
   - `frontend/src/components/layout/sidebar/nav-search.ts`:
     ```ts
     import type { NavItem } from '../nav-config';

     function matchesPath(pathname: string, to: string): boolean {
       // '/' is a prefix of every path, so it only matches itself.
       if (to === '/') return pathname === '/';
       return pathname === to || pathname.startsWith(`${to}/`);
     }

     // The item whose `to` is the longest segment-boundary prefix of the path,
     // so /settings/branches lights "Cấu hình hệ thống" and /settings/my-quotation-settings
     // lights only "Cài đặt của tôi".
     export function findActiveItem(pathname: string, items: readonly NavItem[]): NavItem | null {
       let best: NavItem | null = null;
       for (const item of items) {
         if (matchesPath(pathname, item.to) && (!best || item.to.length > best.to.length)) best = item;
       }
       return best;
     }
     ```
4. **Run tests to verify they pass:** `npx vitest run src/components/layout`. Expected: PASS.
5. **Commit:**
   ```bash
   git add frontend/src/components/layout/nav-config.ts frontend/src/components/layout/sidebar/nav-search.ts frontend/src/components/layout/sidebar/nav-search.test.ts
   git commit -m "feat(frontend): pick the active nav item by longest path prefix"
   ```

### Task 1.3 — Regroup `nav-config.ts`

Target tree (after permission filtering) and icons. Group icons are reserved for groups; no item uses one.

| Group `id` | Label | Group icon | `color` | `defaultOpen` |
|---|---|---|---|---|
| `sales` | Bán hàng | `ShoppingCart` | `bg-nav-sales` | true |
| `inventory` | Kho | `Warehouse` | `bg-nav-inventory` | true |
| `cash` | Thu chi | `Wallet` | `bg-nav-cash` | true |
| `debt` | Công nợ | `Scale` | `bg-nav-debt` | true |
| `reports` | Báo cáo | `BarChart3` | `bg-nav-reports` | true |
| `catalogs` | Danh mục | `Library` | `bg-nav-catalogs` | false |
| `system` | Hệ thống | `Settings` | `bg-nav-system` | false |

| Group | Section | `to` | Label | Icon | Permission (unchanged) |
|---|---|---|---|---|---|
| sales | | `/quotations` | Báo giá | `FileText` | `quotations.view` |
| sales | | `/customers` | Khách hàng | `Users` | `customers.view` |
| sales | | `/qr-thanh-toan` | Tạo mã QR thanh toán | `QrCode` | — |
| inventory | | `/stock-in` | Phiếu nhập kho | `ArrowDownToLine` | `stock_in.view` |
| inventory | | `/stock-out` | Phiếu xuất kho | `ArrowUpFromLine` | `stock_out.view` |
| inventory | | `/inventory/opening-stock` | Tồn đầu kỳ | `ClipboardList` | `inventory.opening_stock` |
| cash | | `/receipts` | Phiếu thu | `HandCoins` | `receipts.view` |
| cash | | `/payments` | Phiếu chi | `Banknote` | `payments.view` |
| cash | | `/fund-transfers` | Chuyển quỹ | `ArrowLeftRight` | `fund_transfers.view` |
| debt | | `/debt/matching` | Đối trừ chứng từ | `Link2` | `debt.matching` |
| debt | | `/debt/offsets` | Bù trừ công nợ | `Combine` (was `Scale`) | `debt_offset.view` |
| debt | | `/debt/opening` | Nợ đầu kỳ | `History` | `debt.opening_balance` |
| debt | | `/credit-limits` | Hạn mức nợ | `Gauge` | `credit_limit.manage` |
| reports | Doanh thu | `/reports/revenue` | Doanh thu | `TrendingUp` (was `BarChart3`) | `reports.revenue` |
| reports | Doanh thu | `/reports/sales-revenue` | Doanh thu sale | `BadgeDollarSign` | `reports.revenue` |
| reports | Doanh thu | `/reports/vehicle-revenue` | Doanh thu xe | `CarFront` | `reports.revenue` |
| reports | Doanh thu | `/reports/sales-performance` | Hiệu suất sale | `Target` | `quotations.view_all` |
| reports | Kho | `/inventory/stock-on-hand` | Tồn kho | `Boxes` | `reports.inventory` |
| reports | Kho | `/inventory/stock-card` | Thẻ kho | `ScrollText` | `reports.inventory` |
| reports | Quỹ | `/reports/cash-book` | Sổ quỹ | `PiggyBank` (was `Wallet`) | `reports.cash` |
| reports | Công nợ | `/reports/debt-balance` | Số dư công nợ | `BookOpen` | `reports.debt` |
| reports | Công nợ | `/reports/debt-ledger` | Sổ chi tiết công nợ | `BookText` | `reports.debt` |
| reports | Công nợ | `/reports/debt-documents` | Công nợ theo chứng từ | `Files` (was `FileText`) | `reports.debt` |
| reports | Công nợ | `/reports/debt-aging` | Tuổi nợ | `Hourglass` | `reports.debt` |
| reports | Công nợ | `/reports/debt-overdue` | Nợ quá hạn | `AlarmClock` | `reports.debt` |
| reports | Công nợ | `/reports/debt-by-salesperson` | Công nợ theo nhân viên | `UserRound` | `reports.debt` |
| reports | Công nợ | `/reports/debt-reconciliation` | Biên bản đối chiếu | `FileCheck` | `reports.debt` |
| catalogs | | `/products` | Hàng hóa | `Package` | `products.view` |
| catalogs | | `/product-groups` | Nhóm hàng hóa | `Tag` | `products.view` |
| catalogs | | `/suppliers` | Nhà cung cấp | `Truck` | `suppliers.view` |
| catalogs | | `/warehouses` | Kho hàng (was "Kho") | `Container` (was `Warehouse`) | `inventory.catalogs.manage` |
| catalogs | | `/stock-reasons` | Lý do nhập xuất | `ListChecks` | `inventory.catalogs.manage` |
| catalogs | | `/cash-reasons` | Lý do thu/chi | `ListOrdered` (was `ListChecks`) | `cash.catalogs.manage` |
| catalogs | | `/payment-methods` | Hình thức thanh toán | `CreditCard` (was `Wallet`) | `inventory.catalogs.manage` |
| catalogs | | `/funds` | Quỹ | `Landmark` | `anyPermission: ['cash.catalogs.manage', 'cash.opening_balance']` |
| system | | `/settings/my-quotation-settings` | Cài đặt của tôi | `UserCog` | — |
| system | | `/settings` | Cấu hình hệ thống | `SlidersHorizontal` (was `Settings`) | `anyPermission` (same 6 as today) |
| system | | `/admin/users` | Quản lý người dùng | `Users2` | `user_settings.manage` |
| system | | `/admin/roles` | Phân quyền | `ShieldCheck` | `roles.view` |

If Task 1.1 step 4 showed a different merged order of the debt reports, use the merged order in both the config and the test.

1. **Write the failing test:** replace `frontend/src/components/layout/nav-config.test.ts` entirely (keep the `WAREHOUSE_PERMS`, `SALES_PERMS`, `ACCOUNTANT_CASH_PERMS` arrays and `groupsFor` exactly as they are today):
   ```ts
   const all = () => visibleNavGroups(() => true, () => true);

   function paths(perms: Permission[], groupId: string): string[] {
     return groupsFor(perms).find((g) => g.id === groupId)?.items.map((i) => i.to) ?? [];
   }

   const DEBT_REPORTS = [
     '/reports/debt-balance', '/reports/debt-ledger', '/reports/debt-documents', '/reports/debt-aging',
     '/reports/debt-overdue', '/reports/debt-by-salesperson', '/reports/debt-reconciliation',
   ];

   describe('visibleNavGroups', () => {
     it('admin sees seven task groups in order', () => {
       expect(all().map((g) => g.id)).toEqual(['sales', 'inventory', 'cash', 'debt', 'reports', 'catalogs', 'system']);
       expect(all().map((g) => g.label)).toEqual(['Bán hàng', 'Kho', 'Thu chi', 'Công nợ', 'Báo cáo', 'Danh mục', 'Hệ thống']);
     });

     it('only Danh mục and Hệ thống start collapsed', () => {
       expect(all().filter((g) => !g.defaultOpen).map((g) => g.id)).toEqual(['catalogs', 'system']);
     });

     it('each group carries its color class', () => {
       for (const g of all()) expect(g.color).toBe(`bg-nav-${g.id}`);
     });

     it('sales user', () => {
       expect(groupsFor(SALES_PERMS, ['SALES']).map((g) => g.id)).toEqual(['sales', 'catalogs', 'system']);
       expect(paths(SALES_PERMS, 'sales')).toEqual(['/quotations', '/customers', '/qr-thanh-toan']);
       expect(paths(SALES_PERMS, 'catalogs')).toEqual(['/products', '/product-groups']);
       expect(paths(SALES_PERMS, 'system')).toEqual(['/settings/my-quotation-settings']);
     });

     it('warehouse user', () => {
       const groups = groupsFor(WAREHOUSE_PERMS, ['WAREHOUSE']);
       expect(groups.map((g) => g.id)).toEqual(['sales', 'inventory', 'reports', 'catalogs', 'system']);
       expect(paths(WAREHOUSE_PERMS, 'inventory')).toEqual(['/stock-in', '/stock-out', '/inventory/opening-stock']);
       expect(groups.find((g) => g.id === 'reports')?.items.map((i) => [i.section, i.to])).toEqual([
         ['Kho', '/inventory/stock-on-hand'],
         ['Kho', '/inventory/stock-card'],
       ]);
       expect(paths(WAREHOUSE_PERMS, 'catalogs')).toEqual(['/suppliers']);
     });

     it('accountant sees cash, debt, report and catalog items', () => {
       expect(paths(ACCOUNTANT_CASH_PERMS, 'cash')).toEqual(['/receipts', '/payments', '/fund-transfers']);
       expect(paths(ACCOUNTANT_CASH_PERMS, 'debt')).toEqual(['/debt/matching', '/debt/offsets', '/debt/opening', '/credit-limits']);
       expect(paths(ACCOUNTANT_CASH_PERMS, 'reports')).toEqual(['/reports/cash-book', ...DEBT_REPORTS]);
       expect(paths(ACCOUNTANT_CASH_PERMS, 'catalogs')).toEqual(['/cash-reasons', '/funds']);
       expect(paths(ACCOUNTANT_CASH_PERMS, 'system')).toContain('/settings');
       expect(groupsFor(SALES_PERMS).find((g) => g.id === 'cash')).toBeUndefined();
       expect(groupsFor(WAREHOUSE_PERMS).find((g) => g.id === 'debt')).toBeUndefined();
     });

     it('report sections for admin', () => {
       const reports = all().find((g) => g.id === 'reports')!;
       expect(reports.items.map((i) => [i.section, i.label])).toEqual([
         ['Doanh thu', 'Doanh thu'], ['Doanh thu', 'Doanh thu sale'], ['Doanh thu', 'Doanh thu xe'],
         ['Doanh thu', 'Hiệu suất sale'], ['Kho', 'Tồn kho'], ['Kho', 'Thẻ kho'], ['Quỹ', 'Sổ quỹ'],
         ['Công nợ', 'Số dư công nợ'], ['Công nợ', 'Sổ chi tiết công nợ'], ['Công nợ', 'Công nợ theo chứng từ'],
         ['Công nợ', 'Tuổi nợ'], ['Công nợ', 'Nợ quá hạn'], ['Công nợ', 'Công nợ theo nhân viên'],
         ['Công nợ', 'Biên bản đối chiếu'],
       ]);
     });

     it('catalog items follow their permissions', () => {
       expect(paths(['inventory.catalogs.manage'], 'catalogs')).toEqual(['/warehouses', '/stock-reasons', '/payment-methods']);
       expect(paths(['cash.catalogs.manage'], 'catalogs')).toEqual(['/cash-reasons', '/funds']);
       expect(paths(['cash.opening_balance'], 'catalogs')).toEqual(['/funds']);
       expect(paths(['credit_limit.manage'], 'debt')).toEqual(['/credit-limits']);
       expect(all().find((g) => g.id === 'catalogs')?.items.find((i) => i.to === '/warehouses')?.label).toBe('Kho hàng');
     });

     it('Cấu hình hệ thống shows with inventory.settings or cash.settings only', () => {
       expect(paths(['inventory.settings'], 'system')).toContain('/settings');
       expect(paths(['cash.settings'], 'system')).toContain('/settings');
       expect(paths(SALES_PERMS, 'system')).not.toContain('/settings');
     });

     it('every item icon is unique and differs from every group icon', () => {
       const groups = all();
       const itemIcons = groups.flatMap((g) => g.items.map((i) => i.icon));
       expect(new Set(itemIcons).size).toBe(itemIcons.length);
       const groupIcons = new Set(groups.map((g) => g.icon));
       expect(groupIcons.size).toBe(groups.length);
       expect(itemIcons.filter((icon) => groupIcons.has(icon))).toEqual([]);
     });
   });
   ```
2. **Run test to verify it fails:** `npx vitest run src/components/layout/nav-config.test.ts`. Expected: FAIL — groups have no `id` (first test receives `[undefined, …]`).
3. **Write minimal implementation** in `nav-config.ts`:
   - Export `NavGroupId = 'sales' | 'inventory' | 'cash' | 'debt' | 'reports' | 'catalogs' | 'system'` and
     ```ts
     export interface NavGroup {
       id: NavGroupId;
       label: string;
       icon: React.ComponentType<{ className?: string }>;
       // Tailwind background class of the group token (--nav-<id>), see index.css.
       color: string;
       defaultOpen: boolean;
       items: NavItem[];
     }
     interface NavItemConfig extends NavItem {
       permission?: Permission;
       // Visible when the user has at least one of these.
       anyPermission?: Permission[];
       role?: Role;
     }
     interface NavGroupConfig extends Omit<NavGroup, 'items'> {
       items: NavItemConfig[];
     }
     ```
   - Replace `navGroups` with the two tables above (`navGroups: NavGroupConfig[]`), imports updated (drop unused, add `ShoppingCart`, `Library`, `Combine`, `TrendingUp`, `BadgeDollarSign`, `CarFront`, `Target`, `PiggyBank`, `Files`, `FileCheck`, `Container`, `ListOrdered`, `CreditCard`, `SlidersHorizontal`).
   - `visibleNavGroups(hasPermission, isInRole): NavGroup[]` keeps its signature and filter; it maps each group to `{ id, label, icon, color, defaultOpen, items }` and strips the permission fields from items (`({ to, label, icon, section }) => ({ to, label, icon, section })`). An empty section disappears on its own because no remaining item carries it; a group with one item stays a group.
   - Remove the `import type { SidebarNavGroup, SidebarNavItem } from './sidebar/sidebar'`.
   - `sidebar.tsx`: delete `SidebarNavItem` / `SidebarNavGroup`, import `type { NavGroup, NavItem } from '../nav-config'`, use them in `SidebarProps` and `SidebarLink`, and key groups by `group.id`.
   - `app-layout.tsx`: `import type { NavItem } from './nav-config'` and type `dashboardItem: NavItem`.
4. **Run tests to verify they pass:** `npx vitest run src/components/layout` and `npm run typecheck`. Expected: PASS.
5. **Commit:**
   ```bash
   git add frontend/src/components/layout/nav-config.ts frontend/src/components/layout/nav-config.test.ts frontend/src/components/layout/sidebar/sidebar.tsx frontend/src/components/layout/app-layout.tsx
   git commit -m "feat(frontend): regroup the sidebar by type of work with distinct icons"
   ```

### Task 1.4 — `SidebarItem`, section headings and longest-prefix highlight in the sidebar

1. **Write the failing test.**
   - `frontend/src/components/layout/sidebar/sidebar-test-utils.tsx` (exports functions only):
     ```tsx
     import { act, render } from '@testing-library/react';
     import { MemoryRouter, useLocation, useNavigate, type NavigateFunction } from 'react-router-dom';
     import { LayoutDashboard } from 'lucide-react';
     import { TooltipProvider } from '@/components/ui/tooltip';
     import { useUiStore } from '@/stores/ui-store';
     import { visibleNavGroups, type NavGroup, type NavItem } from '../nav-config';
     import { Sidebar } from './sidebar';

     const DASHBOARD: NavItem = { to: '/admin/dashboard', label: 'Tổng quan', icon: LayoutDashboard };

     export function allNavGroups(): NavGroup[] {
       return visibleNavGroups(() => true, () => true);
     }

     interface Options { groups?: NavGroup[]; onClose?: () => void }

     // Renders the sidebar the way AppLayout does: `collapsed` comes from the ui store.
     export function renderSidebar(path: string, { groups = allNavGroups(), onClose }: Options = {}) {
       const probe: { navigate?: NavigateFunction; pathname?: string } = {};
       function Probe() {
         probe.navigate = useNavigate();
         probe.pathname = useLocation().pathname;
         return null;
       }
       function Harness() {
         const collapsed = useUiStore((s) => s.sidebarCollapsed);
         return <Sidebar dashboardItem={DASHBOARD} groups={groups} collapsed={collapsed} onClose={onClose} />;
       }
       const utils = render(
         <MemoryRouter initialEntries={[path]}>
           <TooltipProvider>
             <Harness />
             <Probe />
           </TooltipProvider>
         </MemoryRouter>,
       );
       return {
         ...utils,
         navigate: (to: string) => act(() => { probe.navigate!(to); }),
         pathname: () => probe.pathname,
       };
     }
     ```
   - `frontend/src/components/layout/sidebar/sidebar.test.tsx`:
     ```tsx
     import { screen, within } from '@testing-library/react';
     import { beforeEach, describe, expect, it } from 'vitest';
     import { useUiStore } from '@/stores/ui-store';
     import { renderSidebar } from './sidebar-test-utils';

     beforeEach(() => {
       localStorage.clear();
       useUiStore.setState({ sidebarCollapsed: false });
     });

     const link = (name: string) => screen.getByRole('link', { name });

     describe('Sidebar highlight', () => {
       it('lights only the longest matching item', () => {
         renderSidebar('/settings/my-quotation-settings');
         expect(link('Cài đặt của tôi')).toHaveAttribute('aria-current', 'page');
         expect(link('Cấu hình hệ thống')).not.toHaveAttribute('aria-current');
       });

       it('lights the list item on a detail page', () => {
         renderSidebar('/quotations/123');
         expect(link('Báo giá')).toHaveAttribute('aria-current', 'page');
       });

       it('lights nothing on a page without an item', () => {
         renderSidebar('/admin/user-settings/u1');
         expect(screen.queryAllByRole('link').filter((a) => a.getAttribute('aria-current'))).toEqual([]);
       });
     });

     describe('Sidebar sections', () => {
       it('shows report sub-headings in order', () => {
         renderSidebar('/');
         const reports = screen.getByRole('list', { name: 'Báo cáo' });
         expect(within(reports).getAllByRole('heading', { level: 4 }).map((h) => h.textContent))
           .toEqual(['Doanh thu', 'Kho', 'Quỹ', 'Công nợ']);
       });
     });
     ```
2. **Run test to verify it fails:** `npx vitest run src/components/layout/sidebar/sidebar.test.tsx`. Expected: FAIL — `/settings/my-quotation-settings` also lights "Cấu hình hệ thống" (NavLink prefix), and there is no list named "Báo cáo".
3. **Write minimal implementation.**
   - `frontend/src/components/layout/sidebar/sidebar-item.tsx`:
     - `SidebarItem({ item, active, collapsed = false })`: a `Link` (react-router) with `aria-current={active ? 'page' : undefined}`, the classes of today's `SidebarLink` (`bg-primary text-primary-foreground` when active), icon + truncated label; when `collapsed`, icon only with `aria-label` and the existing right-side `Tooltip`. Wrap the expanded link in `<li>`; the row is `<div className="flex items-center">` holding the link (`flex-1 min-w-0`) so Phase 04 can add the ☆ button next to it.
     - `SidebarItemList({ label, items, activePath, id? })`: `<ul aria-label={label} id={id} className="space-y-0.5 pl-2">`; before an item whose `section` differs from the previous item's, render `<li role="presentation"><h4 className="px-3 pb-0.5 pt-2 text-[11px] font-semibold text-muted-foreground">{section}</h4></li>`. Each item: `<SidebarItem item={item} active={item.to === activePath} />`.
   - `sidebar.tsx`:
     - `const { pathname } = useLocation();`
     - `const activeItem = useMemo(() => findActiveItem(pathname, [dashboardItem, ...groups.flatMap((g) => g.items)]), [pathname, dashboardItem, groups]);` and `const activePath = activeItem?.to ?? null;`
     - Expanded: dashboard via `SidebarItem` inside a `<ul>`, then per group the existing label `div` and `<SidebarItemList label={group.label} items={group.items} activePath={activePath} />`.
     - Collapsed: unchanged layout, but every link is `<SidebarItem collapsed active={…} />` (delete `SidebarLink` and the `NavLink` import).
4. **Run tests to verify they pass:** `npx vitest run src/components/layout`. Expected: PASS.
5. **Commit:**
   ```bash
   git add frontend/src/components/layout/sidebar/sidebar-item.tsx frontend/src/components/layout/sidebar/sidebar.tsx frontend/src/components/layout/sidebar/sidebar-test-utils.tsx frontend/src/components/layout/sidebar/sidebar.test.tsx
   git commit -m "feat(frontend): sidebar items with report sections and exact highlight"
   ```

### Task 1.5 — Phase checks

1. From `frontend/`: `npm run typecheck`, `npm run lint` (counts ≤ `BASELINE.md`), `npm run test` (FAIL lines == `BASELINE.md`), `npm run build`, then `git checkout -- tsconfig.app.tsbuildinfo tsconfig.node.tsbuildinfo`.
2. Tick Phase 01 in `SUMMARY.md`, set this file's status to complete, commit: `git add docs/plans/261012-2311-sidebar-navigation-redesign && git commit -m "chore(plan): sidebar redesign phase 01 complete"`.

## Verification

- `npx vitest run src/components/layout`
- `npm run typecheck && npm run lint && npm run test && npm run build`

## Exit Criteria

- Branch `feat/sidebar-nav-redesign` from `qlbanhang/main` (Round 2B merged); `BASELINE.md` committed.
- 7 groups with ids, colors, `defaultOpen`; report sections; icons unique and not equal to group icons; "Hệ thống" and "Kho hàng" labels.
- Only the longest-prefix item has `aria-current="page"`; `/` matches exactly.
- Targeted tests, typecheck, lint, test suite and build at baseline.
