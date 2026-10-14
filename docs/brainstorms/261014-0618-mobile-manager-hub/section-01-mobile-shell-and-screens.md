# Section 01 — Nền tảng PWA và vỏ quản trị mobile (đợt 1–2)

Mockup: [mockup-02-screens.html](mockup-02-screens.html), màn hình 1, 5 và 6.

## Đợt 1 — Nền tảng PWA

Lấy nguyên thiết kế của brainstorm 261007, không đổi:

| Hạng mục | Nguồn |
|---|---|
| Listener `push` và `notificationclick` trong service worker; `lib/sw-push.ts`; điều hướng SPA qua thông điệp `NAVIGATE` | [261007 §1](../261007-2304-pwa-mobile-learn-from-crm/section-01-push-handler.md) |
| Meta iOS, `apple-touch-icon` 180px, tiện ích safe-area (`pt-safe`, `pb-safe`, …), trạng thái `ios-needs-install` và banner hướng dẫn cài | [261007 §3](../261007-2304-pwa-mobile-learn-from-crm/section-03-ios-and-platform-docs.md) |
| Tài liệu `docs/architecture/pwa-mobile.md`; `Cache-Control: no-cache` cho `/sw.js` và `/manifest.webmanifest` trong `frontend/Caddyfile` | [261007 §3](../261007-2304-pwa-mobile-learn-from-crm/section-03-ios-and-platform-docs.md) |

Kiểm thử theo 261007: Vitest cho `sw-push` và `usePushNotification`; xUnit cho icon 180; thử tay trên Chrome Android và iPhone iOS 16.4 trở lên.

Đợt 1 không cần migration và không đổi backend, trừ việc endpoint icon nhận thêm kích thước 180.

Một bổ sung nhỏ so với 261007, để đợt 3 không phải sửa lại service worker: `parsePushPayload` nhận thêm trường tùy chọn `tag`. Thông báo dùng `tag` khi có, và dùng `url` như thiết kế gốc khi không có.

## Đợt 2 — Vỏ quản trị mobile

### Thanh tab

Component mới `components/layout/mobile-tab-bar.tsx`, đặt trong [app-layout.tsx](../../../frontend/src/components/layout/app-layout.tsx).

- Cố định ở đáy, `md:hidden`, có đệm `pb-safe`. Khi thanh tab hiện, vùng nội dung thêm đệm đáy bằng chiều cao thanh.
- **Ai thấy:** người có ít nhất một quyền báo cáo (`reports.*`), hoặc `documents.review`, hoặc `bank_feed.view`. Người khác giữ nút ☰ như hiện nay.
- Nút ☰ trên header vẫn còn; nó và tab "Thêm" mở cùng một drawer.

| Tab | Route | Hiện khi | Có từ |
|---|---|---|---|
| Tổng quan | `/overview` | luôn hiện | đợt 2 |
| Soát xét | `/review` | có `documents.review`; huy hiệu là số phiếu chờ | đợt 4 |
| Biến động | `/balance-feed` | có `reports.cash` hoặc `bank_feed.view` | đợt 3 |
| Báo cáo | `/reports` | có ít nhất một quyền báo cáo | đợt 2 |
| Thêm | mở drawer hiện có | luôn hiện | đợt 2 |

Thanh tab chỉ hiện các tab đã có và người dùng được phép; đợt 2 có ba tab.

**Trang mở đầu trên điện thoại.** Route `/` chuyển sang `/overview` khi thanh tab áp dụng cho người dùng và màn hình hẹp hơn 768px. Việc này dùng một lần kiểm tra `matchMedia` tại thời điểm điều hướng. Đây là ngoại lệ duy nhất so với quy ước chỉ dùng CSS; không có bố cục nào phụ thuộc vào nó.

**Desktop.** Các trang hiện có không đổi. Sidebar thêm nhóm "Quản trị" trong [nav-config.ts](../../../frontend/src/components/layout/nav-config.ts) gồm Tổng quan điều hành, Soát xét, Biến động số dư, theo đợt giao. Mục lục Báo cáo không cần mục sidebar riêng, vì sidebar đã có nhóm Báo cáo.

### Tổng quan (`/overview`)

Trang mới, mobile-first. Mỗi ô tự tải, có khung chờ và trạng thái lỗi riêng. Ô chỉ hiện khi người dùng có quyền.

| Ô | Nguồn | Quyền | Chạm mở |
|---|---|---|---|
| Tồn quỹ (sổ sách), tách tiền mặt và ngân hàng | endpoint mới `GET /api/funds/balances` | `reports.cash` | Sổ quỹ |
| Doanh thu tháng, so với tháng trước | `GET /api/dashboard/summary` hiện có | `quotations.view` | Doanh thu |
| Phải thu, kèm quá hạn | endpoint mới `GET /api/reports/debt-summary` | `reports.debt` | Nợ quá hạn |
| Phải trả, kèm đến hạn trong 7 ngày | cùng endpoint | `reports.debt` | Số dư công nợ |
| Giá trị tồn kho | `GET /api/reports/stock-summary` hiện có; nếu thiếu tổng thì bổ sung | `reports.inventory` và `inventory.view_cost` | Tồn kho |
| Ngân hàng (thực tế) | số dư sau giao dịch mới nhất của từng tài khoản | `bank_feed.view` | Biến động — có từ đợt 5 |

Hai khối bên dưới: "Chờ soát xét" (đợt 4) và "Biến động gần đây" (đợt 3).

Quy tắc số liệu:

- Theo chi nhánh đang làm việc.
- `funds/balances` dùng cùng nguồn với Sổ quỹ ([FundMovementQuery.cs](../../../backend/src/OrderMgmt.Application/CashDebt/Guards/FundMovementQuery.cs)).
- `debt-summary` dựng trên `DebtPositionQuery`, nên tổng phải thu và phải trả bằng số dư cuối kỳ của Số dư công nợ, và số quá hạn bằng tổng của báo cáo Nợ quá hạn.
- Giá trị tồn kho của kỳ chưa kết thúc ghi rõ "tạm tính", như báo cáo.

### Báo cáo (`/reports`)

Route `/reports` hiện chuyển thẳng sang `/reports/revenue` ([App.tsx](../../../frontend/src/App.tsx)). Thay bằng trang mục lục: các báo cáo theo nhóm Quỹ, Công nợ, Doanh thu, Kho. Danh sách lấy từ nhóm `reports` của `nav-config.ts`, nên chỉ có một nơi khai báo và quyền được lọc sẵn.

Năm báo cáo có giao diện thẻ trong đợt 2:

| Báo cáo | Thẻ tổng | Thẻ dòng |
|---|---|---|
| Sổ quỹ | Mỗi quỹ: đầu kỳ, thu, chi, cuối kỳ | Mã phiếu, ngày, đối tượng hoặc diễn giải, số thu hoặc chi, số dư |
| Số dư công nợ | Đầu kỳ, tăng, giảm, cuối kỳ | Đối tượng, dư Nợ hoặc dư Có cuối kỳ, tăng và giảm trong kỳ |
| Nợ quá hạn | Tổng quá hạn và bốn nhóm tuổi | Đối tượng, số quá hạn, bốn nhóm tuổi, tổng nợ, chưa phân bổ |
| Doanh thu sale | Tổng doanh thu | Nhân viên, doanh thu, số báo giá; chạm mở chi tiết |
| Tồn kho | Tổng giá trị, nếu có quyền xem giá vốn | Mã và tên hàng, kho, số lượng tồn, giá trị |

Các báo cáo còn lại vẫn mở được và giữ bảng cuộn ngang. Thanh lọc của năm báo cáo trên xuống dòng trên màn hình hẹp; cách thu gọn từng thanh lọc chốt khi lập plan.

### `ResponsiveTable` và bốn trang danh sách

Theo [261007 §2](../261007-2304-pwa-mobile-learn-from-crm/section-02-mobile-card-view.md): component `components/data/responsive-table.tsx` nhận instance TanStack, hiện bảng từ `md` trở lên và thẻ bên dưới, ẩn/hiện bằng CSS. Bốn trang: Báo giá, Khách hàng, Hàng hóa, Phiếu nhập/xuất. Năm báo cáo ở trên dùng cùng component.

### Kiểm thử đợt 2

- **Vitest:** thanh tab theo quyền và theo đợt; `responsive-table` (số thẻ, trạng thái rỗng và đang tải, class ẩn/hiện); ô Tổng quan ẩn khi thiếu quyền; mục lục báo cáo chỉ liệt kê báo cáo được phép; chuyển hướng `/` sang `/overview`.
- **Integration:** `funds/balances` bằng số dư cuối của Sổ quỹ; `debt-summary` bằng tổng của Số dư công nợ và Nợ quá hạn.
- **Thử tay:** 375px và 768px; thanh tab không che nội dung và không bị home indicator của iPhone che; các test hiện có của trang danh sách vẫn đạt.
