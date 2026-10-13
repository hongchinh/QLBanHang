# Phase 01 — Visual Scaffold (FE only)

**Status:** [x] completed
**Complexity:** M

## Objective
Tạo cấu trúc visual header xanh GMO + brand block + sidebar collapse. **Không** đụng backend, **không** wire search/notification logic thật (chỉ slot UI placeholder để các phase sau replace). Sau phase này, layout đã match style guide về visual; logo là placeholder text + icon (chưa upload).

## Files

**Tạo mới**
- `frontend/src/stores/ui-store.ts`
- `frontend/src/components/ui/tooltip.tsx` (shadcn — nếu chưa có)
- `frontend/src/components/ui/avatar.tsx` (shadcn — nếu chưa có)
- `frontend/src/components/layout/header/app-header.tsx`
- `frontend/src/components/layout/header/brand-block.tsx` (gộp hamburger toggle bên trong)
- `frontend/src/components/layout/header/header-search-placeholder.tsx` (desktop ≥768)
- `frontend/src/components/layout/header/header-search-mobile-button.tsx` (icon button mobile, click TODO phase 03 mở overlay)
- `frontend/src/components/layout/header/header-notifications-placeholder.tsx`
- `frontend/src/components/layout/header/header-user-menu.tsx`
- `frontend/src/components/layout/sidebar/sidebar.tsx`
- `frontend/src/components/layout/skip-to-content.tsx`
- `frontend/src/components/layout/header/__tests__/app-header.test.tsx`

**Sửa**
- `frontend/src/index.css` — thêm `--header-*` tokens
- `frontend/tailwind.config.ts` — thêm color aliases `header.bg`, `header.fg`, `header.active`, `header.danger`
- `frontend/src/components/layout/app-layout.tsx` — refactor grid 2-row, import components mới, thêm `<SkipToContent />`
- `frontend/package.json` — thêm `@radix-ui/react-tooltip` + `@radix-ui/react-avatar` nếu shadcn add chưa cài

## Pre-tasks (chốt design trước khi code)

**A. Pill hover color** — GMO style guide dùng pill **đen** trên header xanh, nhưng QLBanHang là B2B VN, có thể quá "harsh". Phase 01 task #1 prototype 2 variant cùng lúc trong CSS (comment 1 out), screenshot show stakeholder chọn trước khi merge:
   - Variant A (default plan): `--header-active: 0 0% 10%` (đen GMO-style).
   - Variant B (mềm hơn): `--header-active: 212 78% 22%` (xanh đậm hơn header bg).

**B. "Đổi mật khẩu" trong user dropdown** — task #0 verify trước khi build menu:
```
grep -rE "(change.?password|reset.?password|password.{0,10}modal)" frontend/src
```
   - Nếu CÓ route/modal → wire vào user-menu.
   - Nếu KHÔNG → **bỏ hẳn** menu item, mở ticket riêng. Không để TODO comment trong code.

## Tasks

0. **Verify "Đổi mật khẩu"** (xem Pre-tasks B) — ghi kết quả vào doc này trước khi tiếp tục.

1. **Cài shadcn components còn thiếu**:
   ```
   cd frontend && npx shadcn@latest add tooltip avatar
   ```
   Verify `frontend/src/components/ui/tooltip.tsx` + `avatar.tsx` tồn tại + dependencies trong `package.json`.

2. Thêm tokens vào `frontend/src/index.css` `:root`:
   - `--header-bg: 212 78% 36%;` (~#1352A1 xanh GMO)
   - `--header-fg: 0 0% 100%;`
   - `--header-active: 0 0% 10%;` (variant A — pill đen; xem Pre-tasks A)
   - `--header-brand-bg: 0 0% 100%;`
   - `--header-danger: 0 75% 55%;` (badge đỏ + logout text)
   - Thêm `.dark` overrides tương ứng (giữ token nhưng có thể đổi `--header-bg` đậm hơn).

3. Thêm color aliases vào `frontend/tailwind.config.ts` trong `theme.extend.colors`:
   ```ts
   header: {
     bg: 'hsl(var(--header-bg))',
     fg: 'hsl(var(--header-fg))',
     active: 'hsl(var(--header-active))',
     'brand-bg': 'hsl(var(--header-brand-bg))',
     danger: 'hsl(var(--header-danger))',
   },
   ```

4. Tạo `frontend/src/stores/ui-store.ts`:
   - Zustand store với state:
     - `sidebarCollapsed: boolean` (default `false`) — **persist**.
     - `mobileDrawerOpen: boolean` (default `false`) — **không persist** (session-only, tránh F5 mở drawer).
   - Actions: `toggleSidebar()`, `setSidebarCollapsed(v)`, `openMobileDrawer()`, `closeMobileDrawer()`.
   - Wrap bằng `persist` middleware từ `zustand/middleware`, key `qldonhang-ui-store`, `partialize: (s) => ({ sidebarCollapsed: s.sidebarCollapsed })` (loại `mobileDrawerOpen`), version `1`.

5. Tạo `frontend/src/components/layout/sidebar/sidebar.tsx`:
   - Export component `Sidebar` nhận props: `groups`, `dashboardItem`, `collapsed: boolean`, `onClose?: () => void`.
   - Logic NavLink (active state + permission gating) **giữ y nguyên** từ `SidebarContent` hiện tại.
   - **Khác biệt khi `collapsed=true`**: width 64px; mỗi NavLink chỉ render icon center; ẩn `group.label` heading; bỏ padding ngang lớn.
   - **Tooltip khi collapsed (MUST-HAVE — không skip)**: wrap mỗi NavLink trong `<Tooltip>` từ `@/components/ui/tooltip` (đã add ở task 1), `<TooltipContent side="right">` show `label`. Provider mount 1 lần ở `AppLayout` root, `delayDuration={200}`. Khi `collapsed=false` → render link bình thường, không wrap tooltip.
   - Bỏ phần "brand link" + "logout button" (đã chuyển lên header).
   - Bỏ phần "user info" block ở dưới (chuyển vào user dropdown).
   - Transition width: `transition-[width] duration-200 ease-in-out`.

6. Tạo `frontend/src/components/layout/header/brand-block.tsx`:
   - Props: `collapsed: boolean`, `onToggleCollapse: () => void`, `showToggle: boolean` (false trên mobile).
   - Container: `bg-header-brand-bg` width `240px` (`64px` khi collapsed) trên desktop; **mobile <768px** giữ `160px` (đủ hiện logo + text, không co 64px — preserve brand recognition). Height `64px`, `border-r`, `transition-[width] duration-200 ease-in-out`.
   - Layout 2 phần (desktop):
     - **Logo zone** (flex-1 click `<Link to="/">`): icon `FileText` lucide + text "QLBanHang" font-bold (ẩn text khi `collapsed`, chỉ icon center).
     - **Toggle button** (chỉ render khi `showToggle`): icon `PanelLeftClose`/`PanelLeftOpen`, `aria-label="Thu gọn menu"`, click `onToggleCollapse`. Đặt trong brand block (góc phải) để gần sidebar — không tách ra header chính.
   - Mobile: ẩn toggle, logo zone chiếm full.
   - Comment TODO: "Phase 02 replace icon+text bằng `<img>` từ `useBrandingMeta()` — variant `mark` khi collapsed, `full` khi expanded".

7. Tạo `frontend/src/components/layout/header/header-search-placeholder.tsx`:
   - Render input `disabled` nền trắng (`bg-white`), height `40px`, max-width `320px`, rounded `rounded-md`.
   - Icon `Search` lucide bên trái input (absolute positioning).
   - Placeholder text: "Tìm khách hàng, mã báo giá…".
   - `aria-label="Tìm kiếm toàn cục"`.
   - Responsive: max-w `240px` ở `md` (768-1279), `hidden` dưới `md`.

8. Tạo `frontend/src/components/layout/header/header-search-mobile-button.tsx`:
   - Render icon button `Search` text-white, visible `md:hidden`.
   - `aria-label="Mở tìm kiếm"`.
   - Click: no-op trong phase 01 (TODO comment "Phase 03 mở fullscreen search overlay").
   - **Lý do**: tránh mất feature search trên mobile như review note #6.

9. Tạo `frontend/src/components/layout/header/header-notifications-placeholder.tsx`:
   - Render button vuông ~44×44 (đủ touch target) với **icon `Bell`-only** + `Tooltip` "Thông báo" (Radix tooltip từ task 1).
   - **Bỏ** label `text-[10px]` (vi phạm readability, tiếng Việt có dấu càng khó đọc).
   - `aria-label="Thông báo"`.
   - Hover: `bg-header-active` pill bao quanh.
   - Click: no-op (TODO phase 04).

10. Tạo `frontend/src/components/layout/header/header-user-menu.tsx`:
    - Dùng `DropdownMenu` từ `@/components/ui/dropdown-menu` (đã có) + `Avatar` từ `@/components/ui/avatar` (task 1).
    - Trigger: button text-white chứa:
      - `<Avatar className="h-8 w-8">` — `AvatarImage` (TODO: phase sau, hiện chưa có `user.avatarUrl`) + `AvatarFallback` = initials lấy từ `user.fullName` (vd "Nguyễn Văn A" → "NA"; helper `getInitials(fullName)`).
      - Text `user.fullName` `max-w-[160px] truncate hidden lg:inline` (ẩn dưới `lg`, chỉ avatar).
      - `ChevronDown` icon `hidden lg:inline`.
    - DropdownMenuContent (right-aligned):
      - Header (read-only): `user.fullName` + `user.username` text muted.
      - Separator.
      - DropdownMenuItem "Cài đặt của tôi" → `navigate('/settings/my-quotation-settings')`.
      - DropdownMenuItem "Đổi mật khẩu" — **chỉ render nếu task #0 verify được route/modal**; nếu không, bỏ hẳn dòng này (không comment-out).
      - Separator.
      - DropdownMenuItem "Đăng xuất" với className `text-[hsl(var(--header-danger))]` → `onLogout()`.

11. Tạo `frontend/src/components/layout/skip-to-content.tsx`:
    - Link `<a href="#main-content">Bỏ qua tới nội dung chính</a>` với class `sr-only focus:not-sr-only focus:absolute focus:top-2 focus:left-2 focus:z-[100] focus:px-4 focus:py-2 focus:bg-header-bg focus:text-header-fg focus:rounded`.
    - A11y: cho keyboard user nhảy thẳng vào content, bỏ qua header + sidebar.

12. Tạo `frontend/src/components/layout/header/app-header.tsx`:
    - Container: `<header role="banner" className="flex h-16 bg-header-bg text-header-fg items-center">`.
    - Children theo thứ tự:
      - **Mobile hamburger** (visible `md:hidden`): button `Menu` icon, `aria-label="Mở menu"`, click `openMobileDrawer()` từ `useUiStore`.
      - `<BrandBlock collapsed={sidebarCollapsed} onToggleCollapse={toggleSidebar} showToggle={isDesktop} />` — toggle nằm **trong** brand block (gần sidebar), không tách ra header chính.
      - `<HeaderSearchPlaceholder />` (desktop ≥ md, phase 03 replace).
      - `<HeaderSearchMobileButton />` (mobile < md).
      - `<div className="flex-1" />` spacer.
      - `<HeaderNotificationsPlaceholder />` (phase 04 replace).
      - `<HeaderUserMenu user={user} onLogout={handleLogout} />`.
    - Đọc `sidebarCollapsed`, `toggleSidebar`, `openMobileDrawer` từ `useUiStore`.
    - Đọc `user` từ `useAuthStore`.
    - `isDesktop` check qua `window.matchMedia('(min-width: 768px)')` hoặc đơn giản hơn: pass class `hidden md:flex` cho toggle button bên trong BrandBlock thay vì check JS.

13. Refactor `frontend/src/components/layout/app-layout.tsx`:
    - Đổi root từ `flex h-screen` sang `grid h-screen` với `grid-template-rows: 4rem 1fr` (header 64px, content 1fr).
    - Wrap root trong `<TooltipProvider delayDuration={200}>` (Radix) để mọi tooltip trong sidebar/header dùng chung provider.
    - Render `<SkipToContent />` ngay đầu (trước header).
    - Row 1 (span 2 columns): `<AppHeader />`.
    - Row 2:
      - Col 1 (desktop sidebar `<aside>`): `hidden md:flex`, width `collapsed ? w-16 : w-64`, `transition-[width] duration-200 ease-in-out`.
      - Col 2 (`<main id="main-content">`): `flex-1 overflow-y-auto p-4 md:p-3` + `<Outlet />`. `id="main-content"` để match `<SkipToContent>` target.
    - Mobile drawer: state lấy từ `useUiStore.mobileDrawerOpen` (không persist) thay vì local `useState`. `<aside>` fixed inset-y-0 left-0 z-50 width 240, slide-in/out theo state. Click overlay → `closeMobileDrawer()`.
    - Xoá phần `<header className="flex h-16 ...">` cũ (đã chuyển vào `AppHeader`).
    - Truyền `collapsed={sidebarCollapsed}` vào `<Sidebar>` desktop variant. Mobile drawer luôn render `collapsed=false`.
    - Effect close drawer on route change: `useEffect(() => closeMobileDrawer(), [location.pathname])` (giữ logic hiện có).

14. Tạo test `frontend/src/components/layout/header/__tests__/app-header.test.tsx`:
    - Pattern theo các RTL test hiện có trong repo (xem `frontend/src/**/__tests__/*.test.tsx` cho reference).
    - Test 1: render `<AppHeader />` với mock `useAuthStore` (user "Nguyễn Văn A") + `useUiStore` → expect text "Nguyễn Văn A" hoặc avatar fallback "NA" trong document.
    - Test 2: click hamburger mobile → expect `openMobileDrawer` mock được gọi.
    - Test 3: click toggle desktop → expect `toggleSidebar` mock được gọi.
    - Test 4: render với `sidebarCollapsed=true` → brand text "QLBanHang" không visible (chỉ icon).

15. `npm run typecheck` từ thư mục `frontend`.

16. `npm run lint` từ thư mục `frontend`.

17. `npm run test` — đảm bảo test mới + existing test suite vẫn pass.

18. **Manual visual verification** với dev server (`npm run dev`):
    - 1920px: header full-width xanh GMO, brand 240px trắng, sidebar 240px trắng dưới brand.
    - 1280px: layout đầy đủ.
    - 375px: brand **160px** (không co xuống 64), text "QLBanHang" vẫn đọc được; hamburger mở drawer; icon search visible.
    - Click hamburger desktop (trong brand block) → sidebar co 64px + brand co 64px, F5 vẫn còn.
    - Collapsed: hover từng icon sidebar → tooltip hiện label (Radix, không phải `title` HTML).
    - Tab key từ đầu page → focus skip-link → Enter → focus vào main content.
    - Navigate qua /, /customers, /products, /quotations, /reports/revenue, /settings/my-quotation-settings — không 404, không vỡ visual.
    - Logout từ user dropdown → quay về login page.
    - Verify tab key qua user dropdown trigger → avatar/initials có visible focus ring.

## Verification

```
cd frontend
npm run typecheck
npm run lint
npm run test
npm run dev   # manual check 3 viewport + navigation + tooltip + skip-link
```

## Exit Criteria
- [ ] Task #0 "Đổi mật khẩu" verify đã thực hiện; quyết định (có/không) ghi trong commit message hoặc inline comment.
- [ ] `npm run typecheck` pass (0 errors).
- [ ] `npm run lint` pass (không thêm error mới so với baseline).
- [ ] `npm run test` pass — bao gồm test mới `app-header.test.tsx` (≥3 case).
- [ ] Header xanh GMO render đúng ở 1920px; brand block trắng width = sidebar width.
- [ ] Click hamburger **bên trong brand block** (desktop) toggle sidebar 240↔64px; F5 vẫn giữ state collapse (localStorage `qldonhang-ui-store`).
- [ ] Sidebar collapsed: hover icon → Radix tooltip hiện label (không phải `title` HTML, có style + delay 200ms).
- [ ] Skip-link "Bỏ qua tới nội dung chính" focusable bằng Tab; Enter → nhảy vào main content.
- [ ] Notification button icon-only + Radix tooltip "Thông báo" (không có label `text-[10px]`).
- [ ] User dropdown trigger: avatar (initials) luôn visible; tên + chevron ẩn dưới `lg` (1024px); tên truncate nếu dài.
- [ ] User dropdown chỉ có "Đổi mật khẩu" khi task #0 verify thành công, không có dòng comment-out hoặc TODO trong code.
- [ ] Mobile <768px: brand giữ **160px** (text "QLBanHang" đọc được); icon search button visible; collapse toggle desktop ẩn.
- [ ] `mobileDrawerOpen` state nằm trong `ui-store` (không persist) — verify F5 sau khi mở drawer thì drawer đóng lại.
- [ ] Transition sidebar/brand `duration-200` smooth, không jank.
- [ ] Tab navigation qua header có focus ring rõ trên nền xanh (kiểm tra contrast).
- [ ] Navigation qua 5-6 trang chính vẫn hoạt động; permission filter NavLink vẫn đúng.
- [ ] Logout từ user dropdown chạy logout flow như cũ.
- [ ] Không phá vỡ visual các page con (spot-check quotation form, customer table, reports).
