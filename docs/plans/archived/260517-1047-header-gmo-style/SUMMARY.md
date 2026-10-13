# Plan: GMO-Style Header for QLBanHang

## Goal
Áp dụng visual language của style guide **ヘルステックONE byGMO** vào main layout của QLBanHang dưới dạng **hybrid**: header xanh GMO full-width chứa brand block (logo admin upload), nút collapse sidebar, search global, notification, user dropdown — sidebar giữ trắng nhưng support collapse 240↔64px. Không đụng `--primary` để không ảnh hưởng các button/link trong app.

## Scope

**In scope**
- Restructure `app-layout.tsx` sang grid 2-row (header full-width row 1, sidebar | content row 2).
- Thêm CSS tokens `--header-*` trong `index.css` + Tailwind aliases.
- Component header mới: `app-header.tsx`, `brand-block.tsx`, `header-search.tsx`, `header-notifications.tsx`, `header-user-menu.tsx`.
- Refactor sidebar thành component riêng `sidebar.tsx` (extract từ `SidebarContent` hiện tại) + variant collapsed.
- Store `ui-store.ts` (zustand + persist) cho `sidebarCollapsed`.
- Backend endpoint mới:
  - `GET /api/settings/branding` — metadata branding (có/không có logo, updatedAt) — authenticated.
  - `PUT /api/settings/branding` — upload 2 file (`logoFull` + `logoMark`) qua multipart — admin (`user_settings.manage`).
  - `GET /api/settings/branding/logo?variant=full|mark` — stream image từ DB BLOB + ETag + `Cache-Control: private, max-age=300` — authenticated.
  - `GET /api/search/global?q=...` (search KH + báo giá)
  - `GET/POST /api/notifications*` + bảng `notifications`
- FE: thêm tab "Logo công ty" trong page `/settings/my-quotation-settings` hiện có (chỉ admin thấy via permission gate). Form 2 ô upload: "Logo ngang" (expanded 240px) + "Logo vuông" (collapsed 64px / mobile / favicon).

**Out of scope**
- Domain event hook auto-tạo notification (PR4 chỉ có endpoint + manual seed).
- SignalR / realtime notifications (polling 60s là đủ).
- Đổi `--primary` hoặc theme tổng thể (chỉ thêm `--header-*`).
- Featured pill buttons (`クリニックマップ`, `AIアシスト`) — không có feature tương đương trong QLBanHang.
- 9-dot menu, Message icon, Settings icon shortcut — bỏ.
- Chat nội bộ.
- Mobile redesign sâu (chỉ responsive cơ bản: brand co còn 64px, search ẩn dưới 768px).
- Đổi password modal (giả định đã có hoặc out-of-scope, sẽ xác minh trong phase 01).

## Assumptions
- `Permissions.UserSettings.Manage` (`user_settings.manage`) đủ để gate branding upload — đã có sẵn trong `OrderMgmt.Domain/Constants/Permissions.cs`. Fit ngữ cảnh vì branding nằm trong page "Cài đặt của tôi".
- Logo lưu DB BLOB (`varbinary(max)`) — 2 cột `LogoFull` + `LogoMark` kèm 2 cột `LogoFullContentType` + `LogoMarkContentType`. Singleton row `Id=1`. Không dùng filesystem (multi-instance safe).
- Search endpoint orchestrate qua service mới `SearchService.GlobalAsync`, tận dụng pattern từ `CustomerService.SearchAsync` hiện có; quotation cần query mới với cùng scoping logic (ViewAll vs ownership) như `QuotationService`.
- Notification user-scoped (`UserId` FK), không cần permission đặc biệt — chỉ cần `[Authorize]`.
- Frontend chưa cài `@radix-ui/react-popover` và `@radix-ui/react-tabs`; sẽ thêm trong phase liên quan (popover phase 03, tabs phase 02).
- "Đổi mật khẩu" trong user dropdown: phase 01 task đầu tiên là grep `password.*change|reset` quyết một lần — nếu chưa có route/modal thì **bỏ hẳn** menu item, mở ticket riêng. Không để TODO trong code.

## Risks
- **Refactor `app-layout.tsx` phá NavLink/route hiện tại** → Phase 01 giữ logic NavLink/permission filtering y nguyên, chỉ đổi vỏ visual. Manual test 3 viewport + 4-5 trang chính.
- **DB BLOB size lớn nếu admin upload ảnh không tối ưu** (vd PNG 2MB × 2 variant = 4MB row) → giới hạn `2MB/file` ở BE + client-side validate; recommend SVG cho logo mark. Browser cache 5 phút giảm load lặp.
- **Brand block flash khi load branding** → React Query với `placeholderData` (icon + text fallback) cho tới khi resolve; tránh layout shift bằng cách fix height `h-8` cho `<img>`.
- **Search query chậm trên DB lớn** → Limit 5 mỗi loại + tận dụng index sẵn có trên `Customer.Name`, `Quotation.Code` + **min keyword length 3 ký tự** ở BE (2 ký tự match quá nhiều) + 2 query KH/báo giá chạy `Task.WhenAll` parallel (cần `IDbContextFactory` để không share context).
- **Polling notification 60s × nhiều user** gây load → React Query auto-pause khi tab inactive (default behavior); chấp nhận trade-off cho phase này.
- **Sidebar collapse phá CSS hardcode width trong page con** (vd quotation form) → Spot-check 3-5 trang chính sau phase 01.
- **localStorage state stale** sau khi đổi schema store → Đặt version key trong persist config để invalidate khi cần.

## Phases
- [x] Phase 01 — Visual scaffold (FE only) (M) — `phase-01-visual-scaffold.md`
- [x] Phase 02 — Branding upload (BE + FE) (M) — `phase-02-branding-upload.md`
- [x] Phase 03 — Global search (BE + FE) (M) — `phase-03-global-search.md`
- [x] Phase 04 — Notifications (BE + FE) (L) — `phase-04-notifications.md`

Mỗi phase tương ứng 1 PR độc lập, có thể merge riêng vào main. **Lưu ý ship strategy**:
- **PR phase 01-03 merge vào main + deploy staging** để team test, nhưng **không deploy prod cho đến hết phase 04** — lý do: phase 01 alone ship prod = user thấy search disabled + bell click không gì + brand placeholder, gây confused.
- **Hoặc** (alternative): merge cả 4 PR rồi mới deploy prod 1 lần. Quyết định cuối tùy team.
- Phase 04 (notification) là phase cuối, sau khi merge sẽ tag release + deploy prod.

## Final Verification

Sau khi tất cả phase pass:

```
cd frontend && npm run typecheck && npm run lint && npm run test
dotnet build backend/src/OrderMgmt.Application backend/src/OrderMgmt.Infrastructure backend/src/OrderMgmt.WebApi
# Integration tests cho controllers mới (set TEST_DB_CONNECTION khác dev DB trước khi chạy)
dotnet test backend/tests/OrderMgmt.IntegrationTests
```

Khởi động backend + frontend, login admin, verify checklist:

- [ ] Header xanh GMO render đúng ở 1920px; brand block trắng width = sidebar width (240px).
- [ ] Click hamburger toggle sidebar 240↔64px; F5 vẫn giữ state collapse.
- [ ] Navigate qua Tổng quan / Khách hàng / Hàng hóa / Báo giá / Cài đặt — không 404 / không vỡ layout.
- [ ] Vào `/settings/my-quotation-settings` với admin → thấy tab "Logo công ty"; user thường không thấy tab này.
- [ ] Tab "Logo công ty" → upload 2 ảnh (full + mark) → brand block đổi ngay; F5 vẫn còn.
- [ ] Sidebar collapse → brand block swap sang logo mark (variant vuông); expand → quay lại logo full.
- [ ] Gõ tên KH vào search → popover hiện 5 KH; click navigate `/customers/:id`. Gõ mã báo giá tương tự.
- [ ] Ctrl+K focus search; Esc close popover.
- [ ] User chỉ có `quotations.view` (không có `customers.view`) → search ẩn nhóm KH.
- [ ] Seed 1 notification cho admin → badge "1" hiện; click bell → popover hiện item; click item → navigate + badge biến mất.
- [ ] Logout từ user dropdown chạy logout flow như cũ.
- [ ] Mobile 375px: brand 64px, sidebar drawer mở/đóng như cũ.

## Rollback / Recovery

Mỗi phase là một commit/PR riêng → `git revert <commit>` được:

- **Phase 04**: revert FE + revert migration (`dotnet ef migrations remove` rồi commit); drop bảng `notifications` thủ công nếu đã apply prod.
- **Phase 03**: revert FE + BE; không có schema change.
- **Phase 02**: revert FE + BE + revert migration (`dotnet ef migrations remove`); nếu đã apply prod, drop bảng `system_branding` thủ công (logo BLOB nằm trong table, không có file rời rạc cần dọn).
- **Phase 01**: revert FE; nếu state collapse trong localStorage gây vấn đề, clear key `qldonhang-ui-store` từ browser DevTools.

## Reference

- Brainstorm gốc: conversation context (style guide image của ヘルステックONE byGMO).
- Backend pattern: [backend/src/OrderMgmt.WebApi/Controllers/CustomersController.cs](../../../backend/src/OrderMgmt.WebApi/Controllers/CustomersController.cs), [AdminUsersController.cs](../../../backend/src/OrderMgmt.WebApi/Controllers/AdminUsersController.cs).
- Permission constants: [backend/src/OrderMgmt.Domain/Constants/Permissions.cs](../../../backend/src/OrderMgmt.Domain/Constants/Permissions.cs).
- Frontend layout hiện tại: [frontend/src/components/layout/app-layout.tsx](../../../frontend/src/components/layout/app-layout.tsx).
- CSS tokens: [frontend/src/index.css](../../../frontend/src/index.css).
- Memory: `feedback_build_skip_when_app_running.md`, `feedback_test_db_separation_check.md`, `project_quotation_only_pivot.md`.
