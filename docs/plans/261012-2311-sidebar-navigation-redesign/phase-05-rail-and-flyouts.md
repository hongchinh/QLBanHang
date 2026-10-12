# Phase 05 — Collapsed rail and flyouts

**Status:** [x] complete
**Complexity:** M

## Objective

Replace the collapsed mode (one icon per item) with a 64px rail, top to bottom: 🔍 (expands the sidebar, persisted like the header toggle, and focuses the search box), ★ (flyout of pins; only when there are pins), a divider, Tổng quan, then one 34px colored icon block per group. Clicking a group block, or Enter/Space on it, opens a flyout to the right (Radix `Popover`) with a colored title and the group's items (sections and ☆ as in the wide mode); hover only shows a tooltip. One flyout at a time; it closes on outside click, Esc (focus returns to the icon), choosing an item, or a route change. The group of the current page has a 2px `primary` ring and a 3px `primary` bar on the left edge. A long flyout scrolls inside (`max-h` from the available height, `collisionPadding`).

## Files

- `frontend/src/components/layout/sidebar/sidebar-flyout.tsx` (new)
- `frontend/src/components/layout/sidebar/sidebar-rail.tsx` (new)
- `frontend/src/components/layout/sidebar/sidebar-rail.test.tsx` (new)
- `frontend/src/components/layout/sidebar/sidebar-item.tsx` (modify)
- `frontend/src/components/layout/sidebar/sidebar.tsx` (modify)

## Tasks

### Task 5.1 — Rail with group flyouts

1. **Write the failing test** `frontend/src/components/layout/sidebar/sidebar-rail.test.tsx`:
   ```tsx
   import { screen, within } from '@testing-library/react';
   import userEvent from '@testing-library/user-event';
   import { beforeEach, describe, expect, it } from 'vitest';
   import { useAuthStore } from '@/stores/auth-store';
   import { useUiStore } from '@/stores/ui-store';
   import { renderSidebar } from './sidebar-test-utils';

   const TEST_USER = { id: 'u1', username: 'u1', email: 'u1@x.vn', fullName: 'User 1', roles: [], permissions: [] };

   beforeEach(() => {
     localStorage.clear();
     useUiStore.setState({ sidebarCollapsed: true, sidebarGroupOpen: {}, pinnedNavPaths: {} });
     useAuthStore.setState({ user: TEST_USER });
   });

   const groupButton = (name: string) => screen.getByRole('button', { name });

   describe('SidebarRail', () => {
     it('shows one block per group and no search box', () => {
       renderSidebar('/');
       for (const name of ['Bán hàng', 'Kho', 'Thu chi', 'Công nợ', 'Báo cáo', 'Danh mục', 'Hệ thống']) {
         expect(groupButton(name)).toBeInTheDocument();
       }
       expect(screen.getByRole('link', { name: 'Tổng quan' })).toBeInTheDocument();
       expect(screen.queryByRole('searchbox')).not.toBeInTheDocument();
       expect(screen.queryByRole('link', { name: 'Phiếu thu' })).not.toBeInTheDocument();
     });

     it('click opens the group flyout with its items and sections', async () => {
       const user = userEvent.setup();
       renderSidebar('/');
       await user.click(groupButton('Báo cáo'));
       const flyout = screen.getByRole('dialog');
       expect(groupButton('Báo cáo')).toHaveAttribute('aria-expanded', 'true');
       expect(within(flyout).getByRole('link', { name: 'Tuổi nợ' })).toBeInTheDocument();
       expect(within(flyout).getAllByRole('heading', { level: 4 }).map((h) => h.textContent))
         .toEqual(['Doanh thu', 'Kho', 'Quỹ', 'Công nợ']);
       expect(within(flyout).getByRole('button', { name: 'Ghim Tuổi nợ' })).toBeInTheDocument();
     });

     it('Esc closes the flyout and returns focus to the icon', async () => {
       const user = userEvent.setup();
       renderSidebar('/');
       await user.click(groupButton('Kho'));
       await user.keyboard('{Escape}');
       expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
       expect(groupButton('Kho')).toHaveFocus();
     });

     it('opens from the keyboard', async () => {
       const user = userEvent.setup();
       renderSidebar('/');
       groupButton('Thu chi').focus();
       await user.keyboard('{Enter}');
       expect(within(screen.getByRole('dialog')).getByRole('link', { name: 'Phiếu thu' })).toBeInTheDocument();
     });

     it('keeps one flyout open at a time', async () => {
       const user = userEvent.setup();
       renderSidebar('/');
       await user.click(groupButton('Kho'));
       await user.click(groupButton('Thu chi'));
       expect(screen.getAllByRole('dialog')).toHaveLength(1);
       expect(within(screen.getByRole('dialog')).getByRole('link', { name: 'Phiếu thu' })).toBeInTheDocument();
     });

     it('choosing an item navigates and closes the flyout', async () => {
       const user = userEvent.setup();
       const { pathname } = renderSidebar('/');
       await user.click(groupButton('Danh mục'));
       await user.click(within(screen.getByRole('dialog')).getByRole('link', { name: 'Hàng hóa' }));
       expect(pathname()).toBe('/products');
       expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
     });

     it('closes on a route change', async () => {
       const user = userEvent.setup();
       const { navigate } = renderSidebar('/');
       await user.click(groupButton('Kho'));
       navigate('/receipts');
       expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
     });

     it('marks the group of the current page', () => {
       renderSidebar('/products');
       expect(groupButton('Danh mục')).toHaveAttribute('data-active', 'true');
       expect(groupButton('Kho')).not.toHaveAttribute('data-active');
     });
   });
   ```
2. **Run test to verify it fails:** `npx vitest run src/components/layout/sidebar/sidebar-rail.test.tsx`. Expected: FAIL — no button named "Bán hàng" (the old collapsed mode lists items).
3. **Write minimal implementation.**
   - `sidebar-item.tsx`: `SidebarItem` gains `onNavigate?: () => void` (the link's `onClick`); `SidebarItemList` gains `onNavigate?: () => void` and `showSections = true` and passes `onNavigate` to every item.
   - `frontend/src/components/layout/sidebar/sidebar-flyout.tsx`:
     ```tsx
     interface SidebarFlyoutProps {
       title: string;
       icon: React.ComponentType<{ className?: string }>;
       // Background + text classes of the title icon block.
       iconClassName: string;
       open: boolean;
       onOpenChange: (open: boolean) => void;
       trigger: React.ReactElement;
       children: React.ReactNode;
     }

     export function SidebarFlyout({ title, icon: Icon, iconClassName, open, onOpenChange, trigger, children }: SidebarFlyoutProps) {
       return (
         <Popover open={open} onOpenChange={onOpenChange}>
           <Tooltip>
             <TooltipTrigger asChild>
               <PopoverTrigger asChild>{trigger}</PopoverTrigger>
             </TooltipTrigger>
             <TooltipContent side="right">{title}</TooltipContent>
           </Tooltip>
           <PopoverContent
             side="right"
             align="start"
             collisionPadding={8}
             className="max-h-[var(--radix-popover-content-available-height)] w-60 overflow-y-auto p-2"
           >
             <div className="mb-1 flex items-center gap-2 px-1 py-1 text-sm font-semibold">
               <span aria-hidden className={cn('flex h-[26px] w-[26px] items-center justify-center rounded-md', iconClassName)}>
                 <Icon className="h-4 w-4" />
               </span>
               {title}
             </div>
             {children}
           </PopoverContent>
         </Popover>
       );
     }
     ```
     (Radix renders the content with `role="dialog"`; the trigger gets `aria-haspopup="dialog"` and `aria-expanded`. Esc and outside click call `onOpenChange(false)`; Radix returns focus to the trigger. No `role="menu"`: the flyout holds links.)
   - `frontend/src/components/layout/sidebar/sidebar-rail.tsx`:
     ```tsx
     interface SidebarRailProps {
       dashboardItem: NavItem;
       groups: NavGroup[];
       activePath: string | null;
       activeGroupId: string | null;
       pins?: PinControls;
     }
     ```
     State `const [openId, setOpenId] = useState<string | null>(null);` (one flyout at a time); `useEffect(() => setOpenId(null), [pathname]);` with `const { pathname } = useLocation();`; `const close = () => setOpenId(null);`.
     Markup: `<nav aria-label="Chức năng" className="flex min-h-0 flex-1 flex-col items-center gap-1 overflow-y-auto overflow-x-hidden py-3">`, then a divider `<div className="my-1 h-px w-8 bg-border" />`, `<SidebarItem item={dashboardItem} active={dashboardItem.to === activePath} collapsed />`, then per group a row `<div className="relative flex w-full justify-center py-0.5">` holding, when `group.id === activeGroupId`, `<span aria-hidden className="absolute bottom-1 left-0 top-1 w-[3px] rounded-r bg-primary" />`, and
     ```tsx
     <SidebarFlyout
       title={group.label}
       icon={group.icon}
       iconClassName={cn(group.color, 'text-white')}
       open={openId === group.id}
       onOpenChange={(open) => setOpenId(open ? group.id : null)}
       trigger={
         <button
           type="button"
           aria-label={group.label}
           data-active={group.id === activeGroupId ? 'true' : undefined}
           className={cn(
             'flex h-[34px] w-[34px] items-center justify-center rounded-md text-white focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
             group.color,
             group.id === activeGroupId && 'ring-2 ring-primary ring-offset-2 ring-offset-card',
           )}
         >
           <group.icon className="h-4 w-4" />
         </button>
       }
     >
       <SidebarItemList label={group.label} items={group.items} activePath={activePath} pins={pins} onNavigate={close} />
     </SidebarFlyout>
     ```
   - `sidebar.tsx`: when `collapsed`, render `<SidebarRail dashboardItem={dashboardItem} groups={groups} activePath={activePath} activeGroupId={activeGroupId} pins={pins} />` instead of the old per-item icon list (delete that branch).
4. **Run tests to verify they pass:** `npx vitest run src/components/layout`. Expected: PASS.
5. **Commit:**
   ```bash
   git add frontend/src/components/layout/sidebar/sidebar-flyout.tsx frontend/src/components/layout/sidebar/sidebar-rail.tsx frontend/src/components/layout/sidebar/sidebar-rail.test.tsx frontend/src/components/layout/sidebar/sidebar-item.tsx frontend/src/components/layout/sidebar/sidebar.tsx
   git commit -m "feat(frontend): collapsed sidebar rail with group flyouts"
   ```

### Task 5.2 — 🔍 and ★ on the rail

1. **Write the failing test** — append to `sidebar-rail.test.tsx`:
   ```tsx
   describe('SidebarRail search and pins', () => {
     it('🔍 expands the sidebar, persists it and focuses the search box', async () => {
       const user = userEvent.setup();
       renderSidebar('/');
       await user.click(screen.getByRole('button', { name: 'Tìm chức năng' }));
       expect(useUiStore.getState().sidebarCollapsed).toBe(false);
       expect(screen.getByRole('searchbox', { name: 'Tìm chức năng' })).toHaveFocus();
     });

     it('★ is hidden without pins', () => {
       renderSidebar('/');
       expect(screen.queryByRole('button', { name: 'Mục đã ghim' })).not.toBeInTheDocument();
     });

     it('★ opens the pinned items flyout', async () => {
       const user = userEvent.setup();
       useUiStore.setState({ pinnedNavPaths: { u1: ['/reports/debt-aging', '/receipts'] } });
       renderSidebar('/');
       await user.click(screen.getByRole('button', { name: 'Mục đã ghim' }));
       const flyout = screen.getByRole('dialog');
       expect(within(flyout).getAllByRole('link').map((a) => a.textContent)).toEqual(['Tuổi nợ', 'Phiếu thu']);
       expect(within(flyout).queryByRole('heading', { level: 4 })).not.toBeInTheDocument();
     });
   });
   ```
2. **Run test to verify it fails:** `npx vitest run src/components/layout/sidebar/sidebar-rail.test.tsx`. Expected: FAIL — no button "Tìm chức năng" / "Mục đã ghim".
3. **Write minimal implementation.**
   - `sidebar-rail.tsx`: new props `pinnedItems: NavItem[]` and `onSearch: () => void`. Before the divider:
     - `<Tooltip><TooltipTrigger asChild><button type="button" aria-label="Tìm chức năng" onClick={onSearch} className="flex h-10 w-10 items-center justify-center rounded-md text-muted-foreground hover:bg-accent hover:text-accent-foreground"><Search className="h-4 w-4" /></button></TooltipTrigger><TooltipContent side="right">Tìm chức năng</TooltipContent></Tooltip>`;
     - when `pins && pinnedItems.length > 0`: a `SidebarFlyout` with `title="Mục đã ghim"`, `icon={Star}`, `iconClassName="bg-nav-pin-bg text-nav-pin-fg"`, `open={openId === 'pins'}`, `onOpenChange={(open) => setOpenId(open ? 'pins' : null)}`, trigger `<button type="button" aria-label="Mục đã ghim" className="… h-10 w-10 … text-nav-pin-fg"><Star className="h-4 w-4 fill-current" /></button>`, content `<SidebarItemList label="Mục đã ghim" items={pinnedItems} activePath={activePath} pins={pins} onNavigate={close} showSections={false} />`.
   - `sidebar.tsx` (focus once after expanding; `jsx-a11y/no-autofocus` forbids `autoFocus`):
     ```tsx
     const setSidebarCollapsed = useUiStore((s) => s.setSidebarCollapsed);
     const focusSearchOnExpand = useRef(false);
     const expandToSearch = () => {
       focusSearchOnExpand.current = true;
       setSidebarCollapsed(false);
     };
     useEffect(() => {
       if (collapsed || !focusSearchOnExpand.current) return;
       focusSearchOnExpand.current = false;
       inputRef.current?.focus();
     }, [collapsed]);
     ```
     Pass `pinnedItems={pinnedItems}` and `onSearch={expandToSearch}` to `SidebarRail`.
4. **Run tests to verify they pass:** `npx vitest run src/components/layout`. Expected: PASS.
5. **Commit:**
   ```bash
   git add frontend/src/components/layout/sidebar/sidebar-rail.tsx frontend/src/components/layout/sidebar/sidebar-rail.test.tsx frontend/src/components/layout/sidebar/sidebar.tsx
   git commit -m "feat(frontend): search and pins on the collapsed sidebar rail"
   ```

### Task 5.3 — Phase checks

1. From `frontend/`: `npm run typecheck`, `npm run lint`, `npm run test` (compare with `BASELINE.md`), `npm run build`, then `git checkout -- tsconfig.app.tsbuildinfo tsconfig.node.tsbuildinfo`.
2. Manual (`npm run dev`), collapsed mode, ~700px window: hover a block → tooltip only; click → flyout to the right; Báo cáo flyout scrolls inside and stays on screen; the current page's block has the ring and the left bar; ★ flyout lists pins; 🔍 expands and focuses the box and the sidebar stays expanded after choosing a result; dark mode.
3. Tick Phase 05 in `SUMMARY.md`, set this file's status, commit `chore(plan): sidebar redesign phase 05 complete`.

## Verification

- `npx vitest run src/components/layout`
- `npm run typecheck && npm run lint && npm run test && npm run build`

## Exit Criteria

- Rail: 🔍, ★ (with pins), divider, Tổng quan, 7 colored group blocks; active group ring + bar.
- Flyouts open by click/Enter/Space only, one at a time, close on Esc (focus back), outside click, item choice and route change; long content scrolls inside.
- Tests, typecheck, lint, build at baseline.
