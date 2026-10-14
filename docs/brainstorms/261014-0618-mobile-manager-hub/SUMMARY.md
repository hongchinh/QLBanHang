# Mobile cho nhà quản trị — xem báo cáo, soát xét phiếu, nhận biến động số dư

> Brainstorm: 2026-10-14 06:18:47 · Mockup: [mockup-01-approaches.html](mockup-01-approaches.html) (3 phương án giao diện) · [mockup-02-screens.html](mockup-02-screens.html) (6 màn hình đã duyệt) · [mockup-03-events-notifications.html](mockup-03-events-notifications.html) (luồng sự kiện, mẫu tin, cấu hình)
>
> Gộp [261007-2304-pwa-mobile-learn-from-crm](../261007-2304-pwa-mobile-learn-from-crm/SUMMARY.md) và thay lộ trình của nó. Thiết kế kỹ thuật của push handler, thẻ danh sách và iOS vẫn nằm ở các section của brainstorm đó.

## Problem framing

Nhà quản trị (giám đốc, quản lý) cần dùng phần mềm trên điện thoại cho ba việc: **xem báo cáo, soát xét phiếu, nhận tin khi số dư biến động**. Họ không nhập liệu trên điện thoại.

Hiện trạng:

- **Push chưa hiển thị.** PWA cài được và backend đã gửi Web Push, nhưng [sw.ts](../../../frontend/src/sw.ts) chỉ có listener `message`, chưa có `push` và `notificationclick`.
- **Giao diện điện thoại là desktop thu nhỏ.** Menu là drawer của sidebar ([app-layout.tsx](../../../frontend/src/components/layout/app-layout.tsx)) với 7 nhóm, 38 mục. Mọi danh sách là bảng cuộn ngang.
- **Chưa có bước duyệt nào cho phiếu kho và thu chi.** Phiếu ghi sổ ngay khi lưu; trạng thái chỉ có Hoạt động và Đã hủy. Cảnh báo vượt hạn mức nợ, âm quỹ, âm kho do người lập tự xác nhận.
- **Thông báo chỉ có cho báo giá.** [QuotationService.cs](../../../backend/src/OrderMgmt.Application/Sales/Quotations/Services/QuotationService.cs) phát tin khi báo giá sang Xác nhận, Kế toán xác nhận hoặc Hủy, tới chủ báo giá và Admin. Không có tin nào về quỹ hay công nợ.
- **Tổng quan chỉ có số liệu báo giá và doanh thu.** Chưa có tồn quỹ, công nợ, tồn kho.
- **Brainstorm 261007 đã duyệt nhưng chưa có plan.** Push handler, thẻ cho 4 trang danh sách, hỗ trợ iOS và tài liệu nền tảng đều chưa làm.

## Goals

- Trên điện thoại, quản trị tới ba việc chính bằng một chạm từ thanh tab ở đáy màn hình.
- Thông báo đẩy hiển thị đúng trên Android và iPhone (iOS 16.4 trở lên, app đã cài); bấm vào mở đúng trang.
- Trang Tổng quan điều hành có tồn quỹ, doanh thu tháng, phải thu và quá hạn, phải trả và đến hạn, giá trị tồn kho, số dư ngân hàng thực tế. Mỗi số khớp với báo cáo cùng tên.
- 5 báo cáo và 4 trang danh sách hiển thị dạng thẻ dưới 768px.
- Mọi biến động số dư quỹ và mọi giao dịch của tài khoản ngân hàng thật thành tin gửi tới từng người và tới nhóm chat (Telegram, Google Chat, Zalo).
- Quản trị soát xét phiếu đã ghi sổ: Duyệt hoặc Yêu cầu sửa, không đổi sổ sách.

## Non-goals

- Nhập phiếu trên điện thoại; thiết kế lại các form cho màn hình hẹp.
- Duyệt trước khi ghi sổ, duyệt nhiều cấp, bình luận qua lại giữa người duyệt và người lập.
- Tự tạo phiếu từ giao dịch ngân hàng, đối chiếu sổ quỹ với ngân hàng, tự khớp theo mã báo giá trong nội dung chuyển khoản.
- Cảnh báo công nợ và tồn kho; tin tổng kết cuối ngày.
- Offline mutations, Background Sync, mở rộng allow-list cache API (giữ nguyên non-goal của PWA gốc).
- Thẻ cho các trang admin và danh mục.
- Chuyển thông báo báo giá hiện có sang hàng đợi mới.
- App native. Đã ghi nhận làm sau, xem [Lộ trình](#lộ-trình).

## Constraints & assumptions

- Frontend: React 18 + Vite + Tailwind 3 + shadcn/ui. Responsive bằng class CSS, không có hook `useMediaQuery`. Service worker dùng `vite-plugin-pwa` kiểu `injectManifest`, `registerType: 'prompt'`.
- Backend: mọi thao tác ghi sổ chạy trong `ITransactionRunner` với khóa advisory. [EfTransactionRunner.cs](../../../backend/src/OrderMgmt.Infrastructure/Persistence/EfTransactionRunner.cs) chạy các `IBeforeCommitHook` ngay trước commit ngoài cùng. Dự án chưa có `BackgroundService` nào.
- Số liệu theo chi nhánh đang làm việc (`X-Branch-Id`), như mọi báo cáo hiện có.
- Service worker không cache dữ liệu theo chi nhánh hay người dùng ([sw-routes.ts](../../../frontend/src/lib/sw-routes.ts)).
- Production: frontend (Caddy) và API khác origin trên Railway. Backend nhận được webhook từ internet.
- Kho, thu chi và công nợ go-live sau Round 3. Đợt 3–5 chỉ phát huy sau go-live; đợt 1–2 dùng được ngay với báo giá.
- Giả định: VAPID key được cấu hình trên production; ngân hàng của công ty nằm trong danh sách SePay hỗ trợ; ban quản trị lập được nhóm Telegram, space Google Chat (cần Google Workspace) và Zalo OA.

## Decisions

| # | Câu hỏi | Quyết định |
|---|---|---|
| 1 | "Duyệt phiếu" nghĩa là gì | **Soát xét sau ghi sổ.** Phiếu vẫn ghi sổ ngay; quản trị đánh dấu Đã duyệt hoặc Yêu cầu sửa. |
| 2 | Số dư nào cần báo biến động | **Quỹ trong phần mềm** và **tài khoản ngân hàng thật**. |
| 3 | Giao dịch ngân hàng thật dùng để làm gì | **Chỉ thông báo**, kèm sổ giao dịch chỉ đọc. Không tạo phiếu, không đụng sổ quỹ. |
| 4 | Kênh nhận tin | **Thông báo đẩy của app, nhóm Zalo, Telegram, Google Chat.** |
| 5 | Phương án giao diện | **B — khu quản trị mobile** trong cùng app. Gộp toàn bộ brainstorm 261007. App native (C) làm sau. |
| 6 | Tin cá nhân mặc định | **Bật cho ADMIN và MANAGER**, tắt cho người khác. Ai cũng tự tắt hoặc đặt ngưỡng được. |
| 7 | Tự duyệt phiếu của mình | **Cho phép.** Không áp nguyên tắc bốn mắt. |

## Approaches considered

| Phương án | Tóm tắt | Ưu | Nhược |
|---|---|---|---|
| A — Responsive toàn bộ app | Giữ mọi trang và menu; bảng thành thẻ trên màn hình hẹp; tính năng quản trị là mục menu mới | Mọi chức năng dùng được trên điện thoại; mọi vai trò cùng hưởng | Phải sửa hơn 40 trang, kể cả form nhập liệu vốn chỉ hợp desktop; quản trị phải tìm trong menu dài |
| **B — Khu quản trị mobile (chọn)** | Cùng một app; trên điện thoại quản trị thấy 5 tab: Tổng quan, Soát xét, Biến động, Báo cáo, Thêm | Ba việc chính ở ngay màn hình đầu; ít màn hình mới; không đụng form nhập liệu | Vài báo cáo có hai cách trình bày (bảng và thẻ) |
| C — App native trên cửa hàng | Bọc giao diện B bằng Capacitor, cài từ CH Play và App Store | Push ổn định nhất, nhất là trên iPhone; mở khóa sinh trắc học | Tài khoản nhà phát triển, quy trình duyệt app, build riêng cho vài người dùng |
| D — Chỉ dùng nhóm chat | Không làm giao diện mobile; tin trong nhóm chat kèm liên kết | Rẻ nhất | Không xem báo cáo và soát xét được trên điện thoại |

Phần có giá trị nhất của A (thẻ cho các trang danh sách chính) được giữ lại qua việc gộp brainstorm 261007. C làm được sau trên nền B nếu push web không đủ tin cậy.

## Recommended approach

### Nguyên tắc kiến trúc

1. **Một app, một bộ route.** Không có khu `/m` riêng. Các màn hình quản trị là trang bình thường, thiết kế mobile-first, desktop cũng dùng được. Thanh tab ẩn/hiện bằng CSS.
2. **`ResponsiveTable` của 261007 là viên gạch chung.** Bảng trên desktop, thẻ trên điện thoại, dùng cho 4 trang danh sách cũ, 5 báo cáo và các màn hình mới.
3. **Một dòng sự kiện chứng từ.** Ghi trong cùng transaction với việc ghi sổ, qua `IBeforeCommitHook`. Nó nuôi tab Biến động, các thông báo và hộp Soát xét. Logic ghi sổ không đổi.
4. **Gửi tin sau commit, qua hàng đợi trong DB.** Không gọi kênh ngoài khi đang giữ khóa ghi sổ. Kênh lỗi thì thử lại; lưu phiếu không bao giờ hỏng vì thông báo.
5. **Ngân hàng thật đi chung dòng sự kiện** qua webhook, chỉ đọc.

### Lộ trình

Mỗi đợt một plan riêng.

| Đợt | Nội dung | Chi tiết |
|---|---|---|
| 1 | Nền tảng PWA: push hiển thị và bấm mở đúng trang; iOS (icon, safe-area, hướng dẫn cài); tài liệu `docs/architecture/pwa-mobile.md` | [section-01](section-01-mobile-shell-and-screens.md#đợt-1--nền-tảng-pwa), 261007 [§1](../261007-2304-pwa-mobile-learn-from-crm/section-01-push-handler.md) và [§3](../261007-2304-pwa-mobile-learn-from-crm/section-03-ios-and-platform-docs.md) |
| 2 | Vỏ quản trị mobile: thanh tab, Tổng quan điều hành, mục lục Báo cáo, 5 báo cáo dạng thẻ; `ResponsiveTable` và thẻ cho Báo giá, Khách hàng, Hàng hóa, Phiếu nhập/xuất | [section-01](section-01-mobile-shell-and-screens.md#đợt-2--vỏ-quản-trị-mobile), 261007 [§2](../261007-2304-pwa-mobile-learn-from-crm/section-02-mobile-card-view.md) |
| 3 | Dòng sự kiện chứng từ, biến động quỹ, tab Biến động, tùy chọn nhận tin, kênh nhóm Telegram và Google Chat | [section-02](section-02-events-and-notifications.md) |
| 4 | Soát xét sau ghi sổ | [section-03](section-03-review.md) |
| 5 | Ngân hàng thật: webhook SePay, sổ giao dịch chỉ đọc, thông báo | [section-04](section-04-bank-feed.md) |
| 6 | Kênh nhóm Zalo qua Zalo OA, gộp với [brainstorm gửi báo giá qua Zalo](../260522-2326-zalo-oa-send-quotation/SUMMARY.md) | [section-02](section-02-events-and-notifications.md#kênh-nhóm) |
| Sau | App native (Capacitor) bọc lại giao diện, thêm khóa app bằng PIN hoặc sinh trắc học | — |

Lý do xếp thứ tự:

- Đợt 1 nhỏ và mở khóa mọi thông báo.
- Đợt 2 cho giá trị thấy ngay và chỉ cần hai endpoint tóm tắt mới.
- Đợt 3 đi trước đợt 4 vì soát xét dùng lại dòng sự kiện.
- Đợt 5 độc lập với đợt 4; chỉ cần hàng đợi tin của đợt 3.
- Đợt 6 phụ thuộc việc lập Zalo OA và kiểm chứng điều kiện của Zalo.

### Quyền mới

| Quyền | Ý nghĩa | Mặc định |
|---|---|---|
| `documents.review` | Xem hộp soát xét và ra quyết định | ADMIN, MANAGER |
| `bank_feed.view` | Xem giao dịch ngân hàng thật và nhận tin về chúng | ADMIN, MANAGER, ACCOUNTANT |

Biến động quỹ dùng quyền sẵn có `reports.cash`. Khai báo đích nhận tin và tài khoản ngân hàng dùng `system.manage_settings`. Quyền mới được cấp một lần cho các vai trò hệ thống, theo cơ chế seeder hiện có.

### Xử lý lỗi, bảo mật, kiểm thử

- **Lỗi.** Tin tới mỗi đích có trạng thái riêng. Lỗi tạm thì thử lại giãn cách, tối đa 5 lần. Lỗi vĩnh viễn thì dừng và báo admin. Webhook sai chữ ký trả 401; trùng mã giao dịch trả 200 và bỏ qua. Soát xét cần mạng, không có hàng đợi offline.
- **Bảo mật.** Webhook xác thực HMAC-SHA256, có giới hạn tần suất và kích thước body. Bí mật của kênh không bao giờ được API trả lại đầy đủ. Tab Biến động và tin đẩy lọc theo quyền và chi nhánh ở server. Service worker chỉ mở URL cùng origin.
- **Riêng tư.** Số tiền và số dư hiện trên màn hình khóa và trong nhóm chat. Lớp kiểm soát là quyền và thành viên nhóm.
- **Mất điện thoại.** Admin khóa tài khoản hoặc đặt lại mật khẩu để thu hồi phiên.
- **Kiểm thử.** Unit và integration ở backend (PostgreSQL `qldonhang_integtest`), Vitest ở frontend, thử tay trên Android Chrome và iPhone iOS 16.4 trở lên. Danh sách kiểm của từng đợt nằm trong section tương ứng.

## Open questions

Không câu nào chặn đợt 1–2.

1. **Ngân hàng.** Công ty dùng ngân hàng nào; tài khoản doanh nghiệp có được SePay hỗ trợ không; SePay có gửi số dư sau giao dịch cho ngân hàng đó không; chọn gói nào. Cần trả lời trước đợt 5.
2. **Zalo OA.** Gói OA nào gửi được tin vào nhóm; thời hạn access token và refresh token. Tra cứu không tìm thấy tài liệu chính thức; cần kiểm tra trên developers.zalo.me trước đợt 6. Các con số về token trong brainstorm 260522 cũng chưa được kiểm chứng.
3. **Kế thừa từ 261007.** VAPID key đã cấu hình trên production chưa; `IconRenderer` có xuất PNG nền trong suốt không (iOS tô đen); favicon dùng đường dẫn tương đối `/api/...` có hỏng trên production không.
4. **Nhiều chi nhánh.** Tổng quan có cần số gộp mọi chi nhánh không. Thiết kế hiện tại theo chi nhánh đang làm việc.
5. **Màn hình khóa.** Có cần tùy chọn ẩn số tiền trong thông báo đẩy không.
6. **Phiếu ghi ngày tương lai.** Số dư quỹ trong tin và trên Tổng quan tính mọi phiếu đang hiệu lực hay chỉ đến hết hôm nay. Xem [section-02](section-02-events-and-notifications.md#trường-hợp-biên).

## Next steps

1. Lập plan đợt 1 bằng skill `write-plan`. Nguồn: 261007 §1 và §3, cùng [section-01](section-01-mobile-shell-and-screens.md).
2. Lập plan đợt 2.
3. Trả lời câu hỏi mở 1 trước khi lập plan đợt 5, câu hỏi mở 2 trước đợt 6.
4. Mỗi đợt cập nhật `docs/architecture/system-architecture.md`, `docs/code-standard/conventions.md` và `docs/project-pdr/product-goals.md`. Đợt 5 thu hẹp non-goal về webhook ngân hàng.

## Sources

- [SePay — tích hợp webhook](https://developer.sepay.vn/en/sepay-webhooks/tich-hop-webhook)
- [SePay — bảng giá](https://sepay.vn/bang-gia.html)
- [SePay — API ngân hàng](https://sepay.vn/api-ngan-hang.html)
- [Casso — thiết lập webhook](https://developer.casso.vn/webhook/thiet-lap-webhook-thu-cong)
