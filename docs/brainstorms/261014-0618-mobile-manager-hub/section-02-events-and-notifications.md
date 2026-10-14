# Section 02 — Dòng sự kiện, biến động quỹ và thông báo (đợt 3, kênh Zalo ở đợt 6)

Mockup: [mockup-03-events-notifications.html](mockup-03-events-notifications.html) (luồng, mẫu tin, cấu hình) và [mockup-02-screens.html](mockup-02-screens.html), màn hình 4.

## Luồng

```
Ghi sổ (phiếu thu/chi, chuyển quỹ, phiếu kho, bù trừ, số dư đầu kỳ quỹ)
   │  trong cùng transaction
   ▼
Sự kiện chứng từ  +  Biến động số dư  +  Hàng đợi tin
   │  sau commit
   ▼
Bộ phát tin (BackgroundService)
   ├─ Tới từng người: chuông trong app, SignalR, Web Push
   └─ Tới nhóm: Telegram, Google Chat, Zalo OA
```

## Ghi sự kiện

**Sự kiện chứng từ.** Mỗi thay đổi của phiếu nhập kho, phiếu xuất kho, phiếu thu, phiếu chi, chuyển quỹ, bù trừ công nợ ghi một dòng. Nơi ghi là đúng các chỗ service đang gọi `AddActivity` (`StockVoucherService`, `CashVoucherService`, `FundTransferService`, `DebtOffsetService`): tạo, sửa, hủy, khôi phục, xóa. Đợt 3 ghi cả sáu loại để đợt 4 chỉ phải thêm phần soát xét.

Hai trường hợp riêng:

- Phiếu thu và phiếu chi tự động (`AutoCashVoucherService`) không ghi sự kiện riêng. Biến động quỹ của chúng gắn với sự kiện của phiếu kho gốc.
- Sửa số dư đầu kỳ quỹ (`FundService`) ghi một sự kiện loại riêng, để dòng biến động có người thao tác và nội dung. Loại này không thuộc phạm vi soát xét.

**Biến động quỹ.** Mọi đường ghi quỹ đã đi qua cặp `CaptureAsync` và `CheckAsync` của [FundBalanceGuard.cs](../../../backend/src/OrderMgmt.Application/CashDebt/Guards/FundBalanceGuard.cs): phiếu thu và phiếu chi, phiếu tự động của phiếu kho, chuyển quỹ, sửa số dư đầu kỳ. Kết quả của mỗi lần đánh giá có số dư cuối (`RunningBalanceResult.Closing` trong [RunningBalance.cs](../../../backend/src/OrderMgmt.Application/CashDebt/Common/RunningBalance.cs)). Biến động của một quỹ là số dư cuối sau trừ số dư cuối trước. Cách này không cần logic riêng cho từng loại phiếu và luôn khớp Sổ quỹ, vì cả hai dùng chung `FundMovementQuery`.

**Cơ chế.**

- `DocumentEventCollector` có vòng đời theo request, cùng mẫu với `SettlementChangeCollector`. Service khai báo thay đổi của chứng từ; bộ kiểm tra quỹ khai báo số dư trước và sau của từng quỹ. Một quỹ được khai báo nhiều lần trong một transaction thì giữ số "trước" đầu tiên và số "sau" cuối cùng.
- `DocumentEventHook` là một `IBeforeCommitHook`, cùng mẫu với [AutoRecomputeHook.cs](../../../backend/src/OrderMgmt.Application/CashDebt/AutoVouchers/AutoRecomputeHook.cs). Nó ghi các dòng sự kiện, biến động và hàng đợi ngay trước commit ngoài cùng.
- Transaction rollback (kể cả khi người dùng cần xác nhận cảnh báo 422) thì không có dòng nào được ghi.
- Biến động bằng 0 (sửa nội dung mà không đổi tiền) không tạo dòng biến động và không tạo tin.

## Dữ liệu

Tên bảng và cột là đề xuất, chốt khi lập plan. Các bảng này là dữ liệu dẫn xuất hoặc chỉ ghi thêm, không kế thừa `BaseEntity` và không soft-delete, giống các bảng sổ cái.

| Bảng | Mỗi dòng là | Cột chính |
|---|---|---|
| `document_events` | Một thay đổi của một chứng từ | thời điểm, chi nhánh, loại và id và mã chứng từ, hành động, người thao tác, đối tượng, tổng tiền trước và sau, ảnh chụp tóm tắt (JSON) |
| `balance_movements` | Một lần số dư của một quỹ hoặc một tài khoản ngân hàng thay đổi | thời điểm, chi nhánh, nguồn (Sổ hoặc Ngân hàng), quỹ hoặc tài khoản, số tăng giảm, số dư sau, sự kiện chứng từ hoặc giao dịch ngân hàng liên quan |
| `notification_outbox` | Một tin cần phát | chủ đề, chi nhánh, số tiền, nội dung (JSON), thời điểm tạo, thời điểm đã xử lý |
| `notification_destinations` | Một đích nhận tin của nhóm | kênh, tên, cấu hình (JSON), chủ đề, ngưỡng tiền, chi nhánh, đang bật, lỗi gần nhất |
| `notification_deliveries` | Một tin tới một đích | tin, đích, trạng thái, số lần thử, lần thử kế, lỗi gần nhất |
| `notification_preferences` | Tùy chọn của một người | bật biến động quỹ, bật giao dịch ngân hàng, ngưỡng tiền; để trống nghĩa là theo mặc định của vai trò |

Chủ đề của đợt 3 là `fund.movement`. Đợt 4 thêm `review.changes_requested`, đợt 5 thêm `bank.transaction`.

## Phát tin

`NotificationDispatcher` là `BackgroundService` đầu tiên của dự án.

1. Được đánh thức ngay sau commit bằng tín hiệu trong tiến trình; ngoài ra quét định kỳ để nhặt tin sót sau khi khởi động lại.
2. Lấy các dòng hàng đợi chưa xử lý bằng `FOR UPDATE SKIP LOCKED`, nên nhiều instance không xử lý trùng.
3. Với mỗi tin: xác định người nhận cá nhân và gọi `INotificationService.SendAsync` cho từng người (chuông, SignalR, Web Push như hiện có); tạo một dòng giao nhận cho mỗi đích nhóm khớp điều kiện; đánh dấu tin đã xử lý.
4. Gửi các dòng giao nhận. Lỗi tạm (mạng, 429, 5xx) thì thử lại giãn cách, tối đa 5 lần. Lỗi vĩnh viễn (400, 401, 403, 404) thì dừng và ghi lỗi lên đích.

### Người nhận cá nhân

Một người nhận tin khi thỏa cả ba điều kiện:

| Điều kiện | Biến động quỹ | Giao dịch ngân hàng (đợt 5) |
|---|---|---|
| Quyền | `reports.cash` | `bank_feed.view` |
| Chi nhánh | chi nhánh của quỹ là chi nhánh mặc định của người đó, hoặc có `branches.access_all` | tài khoản gắn chi nhánh: như bên trái; tài khoản toàn công ty: cần `branches.access_all` |
| Tùy chọn | đang bật, và số tiền không nhỏ hơn ngưỡng của người đó | như bên trái |

**Mặc định:** bật cho người có vai trò ADMIN hoặc MANAGER, tắt cho người khác. Ngưỡng mặc định là 0. Mỗi người tự đổi ở nút ⚙ của tab Biến động.

Tin "phiếu của tôi bị yêu cầu sửa" (đợt 4) luôn gửi, không có tùy chọn tắt.

### Kênh nhóm

Admin (quyền `system.manage_settings`) khai báo từng đích: kênh, tên, chủ đề, ngưỡng tiền, chi nhánh (hoặc tất cả). Mỗi đích có nút "Gửi thử" và hiện lỗi gần nhất.

| Kênh | Cách gửi | Cấu hình | Đợt |
|---|---|---|---|
| Telegram | Bot API `sendMessage` | Token bot ở biến môi trường (một bot cho cả hệ thống); chat id của nhóm lưu trong đích. Bot phải được thêm vào nhóm. | 3 |
| Google Chat | Incoming webhook của space | URL webhook lưu trong đích; URL này là bí mật, API chỉ trả dạng che. Chỉ có ở tài khoản Google Workspace. | 3 |
| Zalo | Tin nhắn nhóm qua Zalo OA | Dùng `IZaloOaService` và bộ quản lý token của [brainstorm 260522](../260522-2326-zalo-oa-send-quotation/SUMMARY.md); id nhóm lưu trong đích. Điều kiện gói OA và vòng đời token chưa được kiểm chứng. | 6 |

Lớp trừu tượng: một interface kênh nhóm với ba hiện thực. Kênh cá nhân vẫn là `NotificationService` hiện có.

## Nội dung tin

Một bộ định dạng dùng cho mọi kênh, sinh tiêu đề, nội dung và liên kết.

| Trường hợp | Tiêu đề | Nội dung |
|---|---|---|
| Tạo phiếu thu | Quỹ VCB +50.000.000 ₫ | PT00123 · Cty Hòa Phát · Lan lập 10:50 — Số dư quỹ: 1.500.000.000 ₫ |
| Sửa phiếu chi | Quỹ tiền mặt −5.000.000 ₫ | Sửa PC00041: 12.000.000 → 17.000.000 · Tuấn 09:20 — Số dư quỹ: 320.450.000 ₫ |
| Hủy phiếu chi | Quỹ tiền mặt +8.000.000 ₫ | Hủy PC00039 · Lan 08:55 — Số dư quỹ: 325.450.000 ₫ |
| Chuyển quỹ | Chuyển quỹ 200.000.000 ₫ | CQ00007 · Tiền mặt → VCB · Lan 15:02 — Số dư: Tiền mặt 120.450.000 ₫, VCB 1.700.000.000 ₫ |
| Phiếu kho có thanh toán | Quỹ tiền mặt +10.000.000 ₫ | PX00212 (thu PT00130) · Cty Hòa Phát · Minh 09:40 — Số dư quỹ: 330.450.000 ₫ |
| Giao dịch ngân hàng (đợt 5) | TK VCB ··6789 +50.000.000 ₫ | CTY HOA PHAT CK BG00123 · 10:42 — Số dư: 1.530.200.000 ₫ |

- Web Push dùng payload `{ title, body, url, tag }`; `tag` là mã chứng từ để các lần sửa liên tiếp của cùng một phiếu thay nhau thay vì chồng lên. `IPushSender` nhận thêm tham số `tag`; service worker đã đọc trường này từ đợt 1.
- Liên kết mở tab Biến động tại dòng tương ứng. Tin trong nhóm chat kèm liên kết tuyệt đối tới app.
- Số tiền theo định dạng Việt Nam; giờ là giờ Việt Nam, dữ liệu lưu UTC.

## Giao diện

- **Tab Biến động (`/balance-feed`).** Dải số dư từng quỹ và từng tài khoản; dòng thời gian gộp theo ngày; lọc theo nguồn (Sổ quỹ, Ngân hàng) và theo ngưỡng tiền; nhãn riêng cho sửa và hủy. Trên desktop hiện dạng bảng qua `ResponsiveTable`. Tải thêm theo con trỏ thời gian.
- **Tùy chọn nhận tin.** Mở từ nút ⚙: bật/tắt từng loại, ngưỡng tiền. Lần đầu mở tab, app mời cấp quyền thông báo đẩy.
- **Trang cấu hình "Thông báo".** Danh sách đích nhận tin, thêm/sửa/tắt, "Gửi thử", lỗi gần nhất.

## API

| Endpoint | Quyền |
|---|---|
| `GET /api/balance-feed` (lọc nguồn, ngưỡng, con trỏ) | `reports.cash` hoặc `bank_feed.view`; mỗi nguồn lọc theo quyền riêng |
| `GET`, `PUT /api/me/notification-preferences` | đã đăng nhập |
| `GET`, `POST`, `PUT`, `DELETE /api/settings/notification-destinations` | `system.manage_settings` |
| `POST /api/settings/notification-destinations/{id}/test` | `system.manage_settings` |

## Trường hợp biên

- **Chuyển quỹ:** hai dòng biến động (quỹ đi và quỹ đến), một tin.
- **Sửa phiếu đổi quỹ:** hai dòng biến động (quỹ cũ tăng lại, quỹ mới giảm), một tin.
- **Phiếu kho có thanh toán:** biến động gắn với phiếu thu hoặc phiếu chi tự động; tin ghi mã phiếu kho gốc.
- **Phiếu lùi ngày:** tin ghi thêm ngày chứng từ khi khác hôm nay. Số dư trong tin là số dư hiện tại của quỹ, không phải số dư tại ngày chứng từ.
- **Phiếu ghi ngày tương lai:** số dư cuối của bộ kiểm tra quỹ tính mọi phiếu đang hiệu lực, kể cả phiếu ngày tương lai, nên có thể khác Sổ quỹ tính đến hết hôm nay. Đề xuất: tin, tab Biến động và ô Tồn quỹ cùng dùng "mọi phiếu đang hiệu lực" để ba nơi khớp nhau. Đây là câu hỏi mở 6 trong [SUMMARY](SUMMARY.md#open-questions).
- **Thông báo báo giá hiện có** vẫn gọi thẳng `NotificationService` như cũ.

## Kiểm thử đợt 3

- **Unit:** gộp số dư trước và sau trong collector; chọn người nhận (quyền × chi nhánh × tùy chọn × ngưỡng × mặc định theo vai trò); bộ định dạng cho từng trường hợp ở bảng trên; phân loại lỗi tạm và lỗi vĩnh viễn.
- **Integration:** mỗi đường ghi quỹ sinh đúng biến động; tổng biến động của một quỹ bằng số dư cuối của Sổ quỹ; rollback và 422 không để lại dòng nào; hai bộ phát tin chạy song song không gửi trùng; kênh giả lỗi tạm thì thử lại rồi thành công; kênh giả lỗi vĩnh viễn thì dừng và ghi lỗi lên đích.
- **Vitest:** tab Biến động (lọc, nhãn, tải thêm), tùy chọn nhận tin, trang đích nhận tin.
- **Thử tay:** tạo, sửa, hủy một phiếu chi và nhận tin trên điện thoại, trong nhóm Telegram và trong space Google Chat.
