# Review đối chiếu code — plan Khung Hướng dẫn theo màn hình, đợt 1

> Thuộc plan [Khung Hướng dẫn theo màn hình, đợt 1](SUMMARY.md) · Review: 2026-10-15 11:31, trên `qlbanhang/main` ở `1a877638` (frontend và backend giống `3ae38049` của main checkout) · Kết quả: 3 mức cao, 6 mức trung bình, 10 mức thấp. Tất cả đã được sửa trong SUMMARY.md và các file phase.

## Cách review

- Đối chiếu từng khẳng định, đường dẫn, lệnh và mẫu code của 7 file plan với code ở base: `App.tsx`, `app-layout.tsx`, `app-header.tsx`, `header-user-menu.tsx`, `ui-store.ts` và test của nó, `icon-registry.ts`, `lib/focus.ts`, `useCanAccessRoute.ts`, `route-permissions.ts`, `nav-config.ts`, `nav-search.ts`, `popover.tsx`, `sw.ts`, `vite.config.ts`, `vitest.config.ts`, `src/test/setup.ts`, `eslint.config.js`, `tsconfig.app.json`, `package.json`, các file của form phiếu kho, `DbSeeder.cs`, `StockVoucherService.cs`, `appsettings*.json`, `DependencyInjection.cs` và `product-goals.md`.
- Đọc mã nguồn Radix đã cài (`@radix-ui/react-popover`, `react-dismissable-layer`, `react-popper`) để kiểm hành vi đóng popover và `virtualRef`.
- Kiểm các nhánh đang chạy song song: `git log qlbanhang/main..<nhánh>` và `git diff --name-only qlbanhang/main...<nhánh>`.
- Chạy thử những lệnh mà plan dựa vào: `git log --format='%ct %cs'`, `git ls-files -- ':(glob)…'` (git 2.56), thời gian của `git log` với pathspec glob, `require('lucide-react')` cho 4 icon, chuỗi tiếng Việt trong `dist` cũ và `dist/sw.js`.

## Mức cao

| # | Phát hiện | Bằng chứng | Đã xử lý |
|---|---|---|---|
| P-H1 | Test smoke của `help:stale` khai báo `// @vitest-environment node`. Ở môi trường node, file setup chung ném `ReferenceError: Element is not defined`, nên test không bao giờ xanh và bước RED báo sai lý do. | [setup.ts:19](../../../frontend/src/test/setup.ts#L19) gọi `Element.prototype.hasPointerCapture`. `vitest.config.ts` áp `setupFiles` cho mọi test. Repo chưa có test nào chạy môi trường node. | Phase 03 Task 3.2: bỏ dòng khai báo, giữ jsdom (`child_process` vẫn chạy), có ghi lý do. |
| P-H2 | Kiểm tra trình duyệt chặn `/api` bằng `page.route`, nhưng service worker ở chế độ dev tự gọi API. Các request đó đi qua proxy của Vite sang cổng 5050, tức backend dev với DB `qldonhang_test`. Bộ chặn `:5050` không thấy được vì URL vẫn là `:5182`. | `vite.config.ts` có `devOptions.enabled: true`. [sw.ts](../../../frontend/src/sw.ts) đăng ký `NetworkFirst` cho các đường dẫn trong [sw-routes.ts](../../../frontend/src/lib/sw-routes.ts): `/api/lookups/`, `/api/banks`, `/api/settings/branding`. Playwright không định tuyến request của service worker. | Phase 05 Task 5.1: mọi context tạo qua hàm `newIsolatedContext` với `serviceWorkers: 'block'`, dùng `context.route` và `context.on('request')`, và kiểm `navigator.serviceWorker.controller === null`. SUMMARY thêm rủi ro "Dev service worker". |
| P-H3 | Hai plan chồng màn đang chạy, nên plan này gần như chắc chắn merge sau. Theo P12, bên merge sau phải cập nhật hướng dẫn. Nhưng plan viết nội dung theo base cũ, và Task 5.2 chỉ xử lý route khi merge main, không cập nhật nội dung. | `feat/voucher-payment-qr-fe`: 17 commit, sửa `cash-voucher-form-page.tsx` và `stock-voucher-form-page.tsx`. `feat/cash-voucher-form-redesign`: 14 commit, 29 file frontend. Chưa nhánh nào sửa `App.tsx`. | Thêm Phase 04 Task 4.0: merge `qlbanhang/main` trước khi viết, và cập nhật `stock-out.form` nếu form đổi. Task 5.2: sau khi merge, sửa mọi hướng dẫn mà `help:stale` báo `Code đổi ngày …` và chạy lại kịch bản 1–3, 6 nếu form đổi. P12, phần rủi ro, thứ tự phase và Exit Criteria của Phase 05 sửa theo. Phase 04 không chạy song song với Phase 02 nữa. |

## Mức trung bình

| # | Phát hiện | Bằng chứng | Đã xử lý |
|---|---|---|---|
| P-M1 | Ô gợi ý lần đầu tự đóng và ghi "đã xem" ngay khi focus chuyển ô (Tab, Enter sang ô kế tiếp, autofocus đến muộn). Brainstorm chỉ cho đóng bằng: mở khung, Ẩn, Esc, bấm ra ngoài. | `react-dismissable-layer` (`useFocusOutside`) nghe `focusin` trên document và gọi `onDismiss` khi focus nằm ngoài lớp. `PopoverContentNonModal` chỉ chặn trường hợp có pointerdown trước đó. Brainstorm §1 "Gợi ý lần đầu". | P10 và Task 2.9: thêm `onFocusOutside={(e) => e.preventDefault()}`. Test mới: Tab sang ô khác thì gợi ý vẫn còn và chưa ghi "đã xem"; bấm ra ngoài thì đóng và ghi "đã xem". Kịch bản 1 của Phase 05 bấm Tab 2 lần. |
| P-M2 | Đóng khung luôn trả focus về phần tử đã nhớ. Khung không modal, nên người dùng có thể đã bấm sang ô khác rồi nhấn F1 để đóng; khi đó focus bị kéo về ô cũ. | Task 2.5 cũ: "give the focus back to the remembered element when it is still connected". Brainstorm cho phép vừa mở khung vừa nhập liệu. | Task 2.5: chỉ trả focus khi focus đang trong khung hoặc ở `body`. Có test và kịch bản 2 của Phase 05. |
| P-M3 | Luật F1 (P9) chặn cả khi popover đang mở. Brainstorm chỉ nói hộp thoại modal, và code hiện có đã phân biệt hai loại. | Brainstorm §1 "Phím và focus", §7 ("F1 không làm gì khi đang mở hộp thoại modal"). [stock-voucher-form-page.tsx:126-133](../../../frontend/src/pages/stock-vouchers/stock-voucher-form-page.tsx#L126-L133) (`inModalDialog` bỏ qua dialog nằm trong `[data-radix-popper-content-wrapper]`). | P9, Task 2.6: thêm `POPPER_WRAPPER_SELECTOR` vào `lib/focus.ts`; F1 chỉ bị chặn khi có `DIALOG_SELECTOR` nằm ngoài wrapper. Bỏ ngoại lệ `data-help-hint`. Không sửa `stock-voucher-form-page.tsx` vì các nhánh P12 đang sửa file đó. Mục conventions (Task 3.5) và kịch bản 2 sửa theo. |
| P-M4 | `help:stale` không báo khi một glob trong `sources` không khớp file nào (gõ sai, thư mục đổi tên). Hướng dẫn đó sẽ không bao giờ bị báo cũ, trái với tiêu chí dễ bảo trì. | `git log -1 -- <glob sai>` trả rỗng, nên `sourcesDate = null` và không có cảnh báo. Đã thử: `git ls-files -- ':(glob)frontend/src/pages/stock-voucherz/**'` cho 0 dòng. | Contract thêm `unmatchedSources` và loại `sources-no-match` ("Nguồn "…" không khớp file nào"). Task 3.1 có test, Task 3.2 kiểm bằng `git ls-files`. Skill `help-content`, Exit Criteria của Phase 03 và Task 4.5 kiểm điều này. |
| P-M5 | Câu Lưu ý mẫu của `stock-out.form` ghi "Khách đã ngừng theo dõi". Cụm này không có trong code. | Trạng thái khách là `Ngừng sử dụng` ([customer-form-fields.tsx:37](../../../frontend/src/pages/customers/customer-form-fields.tsx#L37)). Lỗi backend là `Đối tượng đã ngừng hoạt động.`, chỉ áp cho khách mới chọn ([StockVoucherService.cs:666-668](../../../backend/src/OrderMgmt.Application/Inventory/StockVouchers/Services/StockVoucherService.cs#L666-L668)). | Task 1.6: sửa thành "Khách đang Ngừng sử dụng thì không chọn được (báo Đối tượng đã ngừng hoạt động); phiếu cũ của khách đó vẫn sửa được." Bước 2 ghi kèm bằng chứng để người thực thi kiểm lại. |
| P-M6 | Plan trỏ tới tiêu đề không có trong `product-goals.md`: "Cash & Debt" (Task 4.3, 4.4) và "Credit limit and negative fund policies" (Task 1.6). | Các mục thật: "Tiền & Công nợ" trong "Core Business Rules"; hạn mức nợ là một gạch đầu dòng Round 2A ở "Current Scope"; bảng điều hành là các gạch đầu dòng "Executive dashboard — Bảng điều hành" ở "Current Scope". | Sửa tên mục ở Task 1.6, 4.3, 4.4. |

## Mức thấp

| # | Phát hiện | Đã xử lý |
|---|---|---|
| P-L1 | Thân khung chưa ghi là vùng cuộn, nên hướng dẫn dài tràn khỏi tấm trượt hoặc khung cao hết màn hình. | Task 2.5: phần thân là `min-h-0 flex-1 overflow-y-auto`, có `data-testid="help-panel-body"`; đầu và chân khung `shrink-0`. Có test class, và kịch bản 2, 4 của Phase 05 kiểm. |
| P-L2 | Test smoke gọi git khoảng 3 lần cho mỗi file hướng dẫn (đo được khoảng 0,13 giây mỗi lần), cộng thời gian khởi động node. `npm test` thường có timeout mặc định 5 giây nên dễ hết giờ. | Task 3.2: đặt timeout riêng 60 giây cho test. |
| P-L3 | Harness của Task 2.9 dùng `autoFocus`; `jsx-a11y/no-autofocus` báo lỗi lint cả trong file test. | Thêm `eslint-disable-next-line` kèm lý do, theo cách của [dialog.test.tsx:191](../../../frontend/src/components/ui/dialog.test.tsx#L191). |
| P-L4 | So chuỗi `%cI` sai khi hai commit khác múi giờ, ví dụ commit merge trên GitHub. | Contract dùng `CommitStamp { at: %ct, day: %cs }`: thứ tự so bằng `at`, ngày so bằng `day`. Task 3.1 có test cho trường hợp khác múi giờ. |
| P-L5 | Nếu tải chunk nội dung lỗi (mất mạng trước khi service worker kịp cache, hoặc chunk đã bị xóa sau deploy), promise lỗi bị giữ lại và khung hiện "Đang tải…" mãi. | Task 1.7: `load-help.ts` thành một store nhỏ đọc bằng `useSyncExternalStore`. Lỗi thì xóa cache và bật `useHelpLoadFailed()`. Có file `load-help-failure.test.ts`. Task 2.5 hiện "Không tải được hướng dẫn…" kèm nút `Thử lại`. Các mock của Phase 02 cung cấp đủ 3 tên. |
| P-L6 | Thiếu test "đổi route khi khung đang mở thì nội dung đổi theo" (brainstorm §7). | Task 2.5: test bấm link sang `/products` sau khi đã mở một hướng dẫn liên quan. |
| P-L7 | Màn quản trị hiển thị role WAREHOUSE là "Kho / giao hàng", không phải "Warehouse". | Kịch bản 6: ghi đúng tên, quyền theo [DbSeeder.cs:226-233](../../../backend/src/OrderMgmt.Infrastructure/Persistence/Seed/DbSeeder.cs#L226-L233), và kiểm thêm `Đối trừ chứng từ` cũng bị ẩn. |
| P-L8 | Chân lưới phiếu hiển thị `Ctrl`+`Del`; file hướng dẫn ghi `Ctrl Delete`. | Task 1.6: đổi thành `Ctrl Del`. |
| P-L9 | Tài liệu kiến trúc nói chunk nội dung được precache, nhưng bước kiểm tra cuối không kiểm điều này. | Final Verification: `grep -c "assets/content-index-" dist/sw.js` ≥ 1. Đã xác nhận `dist/sw.js` cũ có chứa `assets/index-`. |
| P-L10 | Test NFC dùng chữ tách dấu dán thẳng vào chuỗi. Khi chép tay, editor có thể gộp dấu và test mất tác dụng. | Task 1.3: viết `'Lúu'` và `'Lúu'`. |

## Khẳng định đã kiểm và đúng

- Base `1a877638` tồn tại (PR #20, chỉ đổi tài liệu, nằm trên PR #19).
- `App.tsx`:
  - cây route nằm ở dòng 144–759;
  - `Navigate` chỉ dùng trong cây route;
  - `RouteErrorBoundary` ở lại `App.tsx`;
  - có `/settings`, `/admin/users/:userId/transfer-quotations`, và index của `/reports` là một redirect.
- Router và hàm làm phẳng route:
  - react-router 6.27: `createRoutesFromChildren` đọc được fragment;
  - kết quả của `flattenRoutePaths` cho cây mẫu đúng thứ tự sắp xếp.
- Các test App hiện có:
  - `App.routes.test.tsx` mock theo đường dẫn module, nên vẫn áp dụng sau khi chuyển route sang `app-routes.tsx`;
  - `App.shell.test.tsx` không kiểm `console.error`.
- Cấu hình build và chạy:
  - `"type": "module"`, Node 24.18;
  - `allowImportingTsExtensions`, `resolveJsonModule`;
  - vitest không giới hạn `include`, nên nhận `scripts/*.test.ts`;
  - ESLint không bật kiểm kiểu, nên lint được thư mục `scripts/`;
  - build hiện ra một chunk `index`, giữ nguyên chữ UTF-8, nên lệnh `grep` ở bước kiểm tra cuối dùng được.
- Layout và header:
  - header có `col-span-full` và `h-16`, nên `md:top-16` và cột thứ ba ở 1600 px đều đúng;
  - `SkipToContent` là `sr-only`, không chiếm ô lưới;
  - có `pb-safe`, và `--icon-warn` là kênh HSL trần.
- Store, icon, focus:
  - `ui-store` có `version: 1` và `partialize`; test có sẵn hàm `readPersisted`;
  - icon `close`, `back`, `warning` đã có; `CircleHelp`, `Lightbulb`, `Play`, `ExternalLink` có trong lucide-react 0.453;
  - `DIALOG_SELECTOR` có trong `lib/focus.ts`.
- Hàm hỗ trợ:
  - chữ ký của `useCanAccessRoute()`, `visibleNavGroups(hasPermission, isInRole)` và `findActiveItem(pathname, items)` khớp plan;
  - `PopoverAnchor` hỗ trợ `virtualRef` trong typings đã cài.
- Quyền và menu:
  - `/receipts/new` cần `receipts.create`; `/product-groups` cần `products.view`;
  - Khách hàng thuộc nhóm "Bán hàng"; nhóm "Kho" có nhập, xuất, tồn đầu kỳ; nhóm "Danh mục" chưa có hướng dẫn nào. Vì vậy kỳ vọng ở kịch bản 5 đúng.
- Backend và công cụ:
  - ưu tiên `DefaultConnection` rồi mới tới `Default`;
  - có `Database:AutoMigrateAndSeed` và `Seed:AdminPassword = Admin@123`;
  - Playwright 1.60 ở thư mục gốc; `.claude/worktrees/` nằm trong `.git/info/exclude`; `.claude/skills/` được commit;
  - `.github/` chưa có mẫu PR (thư mục `workflows` trống).
- Mọi đường dẫn `sources` ở Phase 04 đều tồn tại.
- Nhãn của phiếu xuất kho (trừ câu ở P-M5):
  - `Kho`, `Mã đối tượng`, `HTTT`;
  - tab `Hàng hóa`/`Alt+1`, `Thanh toán`/`Alt+3`, `Lịch sử`/`Alt+4` (chỉ khi sửa phiếu);
  - `Số tiền thanh toán`, `Lưu tạm`, `Lưu và thoát`, `Cập nhật`, `Insert`;
  - `stock-totals-total`, `Lưu thay đổi trước khi in`, `Vượt hạn mức nợ`;
  - lý do `Xuất bán hàng` (có trong dữ liệu seed).
- FNV-1a 32-bit của `"a"` là `e40c292c`.
