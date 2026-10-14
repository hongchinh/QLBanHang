# Section 04 — Ngân hàng thật (đợt 5)

Mockup: [mockup-03-events-notifications.html](mockup-03-events-notifications.html) và [mockup-02-screens.html](mockup-02-screens.html), màn hình 1 và 4.

Phạm vi: nhận giao dịch của tài khoản ngân hàng thật, lưu vào sổ giao dịch chỉ đọc và phát tin. Không tạo phiếu, không đụng sổ quỹ, không đối chiếu.

## Nhà cung cấp

| Lựa chọn | Tóm tắt | Nhận xét |
|---|---|---|
| **SePay (đề xuất)** | Liên kết tài khoản, bắn webhook từng giao dịch kèm số dư sau giao dịch | Trang API liệt kê 29 ngân hàng; có HMAC; gói miễn phí 50 giao dịch mỗi tháng, gói trả phí từ 120.000đ mỗi tháng (trang bảng giá không ghi ngày cập nhật) |
| Casso | Webhook V2 có chữ ký `X-Casso-Signature` | Bảng giá tìm được từ khoảng 379.000đ mỗi tháng và có thể đã cũ; danh sách ngân hàng không xác nhận được |
| API ngân hàng trực tiếp | Ký hợp đồng với từng ngân hàng | Không phí trung gian, nhưng thủ tục doanh nghiệp và tích hợp riêng từng ngân hàng |

Chỉ hiện thực SePay. Bảng giao dịch có cột nhà cung cấp, đủ để thêm nhà cung cấp khác sau này mà không cần lớp trừu tượng ngay bây giờ.

**Dùng tạm khi chưa có đợt 5.** SePay có sẵn tính năng chia sẻ biến động số dư sang Telegram, không cần code. Ban quản trị có thể bật trực tiếp trên SePay.

## Luồng webhook

`POST /api/webhooks/sepay`, không cần đăng nhập, có xác thực chữ ký.

1. Kiểm tra chữ ký. Sai thì trả 401 và không lưu gì.
2. Đọc payload. Thiếu trường bắt buộc thì trả 400 và ghi log.
3. Chống trùng theo cặp nhà cung cấp và mã giao dịch (chỉ mục duy nhất). Đã có thì trả 200 và bỏ qua.
4. Tìm tài khoản theo số tài khoản.
5. Trong một transaction: ghi giao dịch; nếu tài khoản đã khai báo và đang bật thì ghi thêm dòng biến động (nguồn Ngân hàng) và dòng hàng đợi tin `bank.transaction`.
6. Trả 200 theo dạng SePay yêu cầu. Request không gọi kênh ngoài nào.

Ánh xạ payload của SePay:

| Trường SePay | Lưu thành |
|---|---|
| `id` | mã giao dịch của nhà cung cấp (khóa chống trùng) |
| `gateway` | ngân hàng |
| `transactionDate` | thời điểm giao dịch (giờ Việt Nam, lưu UTC) |
| `accountNumber`, `subAccount` | số tài khoản, tài khoản phụ |
| `transferType` (`in` hoặc `out`) và `transferAmount` | số tiền có dấu |
| `accumulated` | số dư sau giao dịch |
| `content`, `description` | nội dung chuyển khoản, mô tả gốc |
| `referenceCode` | mã tham chiếu của ngân hàng |
| `code` | mã thanh toán SePay nhận diện được; lưu lại, chưa dùng |

## Bảo mật

- Ưu tiên HMAC-SHA256 qua header `X-SePay-Signature`; so sánh chữ ký trong thời gian hằng. Phương án dự phòng là `Authorization: Apikey …`. Cách ghép chuỗi ký và định dạng header kiểm tra lại theo tài liệu SePay khi lập plan.
- Secret nằm ở biến môi trường, không ghi vào log.
- Thêm một chính sách giới hạn tần suất cho webhook trong [Program.cs](../../../backend/src/OrderMgmt.WebApi/Program.cs), cạnh các chính sách đăng nhập hiện có; giới hạn kích thước body.
- Lưu payload gốc để tra soát.

## Tài khoản ngân hàng

Bảng `bank_accounts`: nhà cung cấp, ngân hàng, số tài khoản, tên hiển thị, chi nhánh (để trống nghĩa là toàn công ty), đang bật. Admin khai báo ở trang cấu hình, quyền `system.manage_settings`.

**Tài khoản chưa khai báo.** Giao dịch vẫn được lưu, nhưng không tạo biến động và không phát tin. Trang cấu hình liệt kê các số tài khoản lạ đã gửi giao dịch tới, để admin khai báo.

## Quyền xem

- Quyền mới `bank_feed.view`, mặc định cho ADMIN, MANAGER, ACCOUNTANT.
- Tài khoản gắn chi nhánh: người có chi nhánh mặc định là chi nhánh đó, hoặc có `branches.access_all`.
- Tài khoản toàn công ty: cần `branches.access_all`.
- Điều kiện nhận tin cá nhân: xem [section-02](section-02-events-and-notifications.md#người-nhận-cá-nhân).

## Hiển thị

- **Tab Biến động:** dòng có nhãn "NH", số tài khoản che bớt, nội dung chuyển khoản, số dư sau giao dịch.
- **Dải số dư trên tab Biến động và ô "Ngân hàng (thực tế)" trên Tổng quan:** số dư của giao dịch có thời điểm mới nhất của từng tài khoản, kèm giờ cập nhật.

Webhook có thể đến trễ hoặc sai thứ tự. Dòng thời gian sắp theo thời điểm giao dịch, không theo thời điểm nhận.

## Thay đổi tài liệu

[product-goals.md](../../project-pdr/product-goals.md) hiện ghi non-goal: không xác nhận thanh toán, không webhook ngân hàng, không đối chiếu cho mã VietQR. Đợt 5 thu hẹp thành: không xác nhận thanh toán và không đối chiếu cho mã VietQR; nguồn giao dịch ngân hàng là chỉ đọc và không được khớp với chứng từ.

## Câu hỏi cần trả lời trước khi lập plan

1. Công ty dùng ngân hàng nào, tài khoản cá nhân hay doanh nghiệp.
2. SePay có hỗ trợ loại tài khoản đó không, và có gửi `accumulated` cho ngân hàng đó không. Nếu không có số dư, dải số dư và ô "Ngân hàng (thực tế)" bị bỏ; dòng giao dịch vẫn dùng được.
3. Số giao dịch mỗi tháng, để chọn gói.

## Kiểm thử đợt 5

- **Unit:** kiểm tra chữ ký (đúng, sai, thiếu); ánh xạ payload, kể cả tiền vào và tiền ra; đổi giờ Việt Nam sang UTC.
- **Integration:** gửi trùng một giao dịch chỉ lưu một lần và phát một tin; tài khoản chưa khai báo không tạo biến động; tài khoản tắt không phát tin; lọc theo `bank_feed.view` và theo chi nhánh; hai giao dịch đến sai thứ tự vẫn cho đúng số dư mới nhất.
- **Thử tay:** dùng chế độ thử của SePay gửi giao dịch giả; sau đó một giao dịch thật giá trị nhỏ, nhận tin trên điện thoại và trong nhóm chat.
