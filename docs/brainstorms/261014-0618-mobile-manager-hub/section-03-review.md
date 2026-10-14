# Section 03 — Soát xét sau ghi sổ (đợt 4)

Mockup: [mockup-02-screens.html](mockup-02-screens.html), màn hình 2 và 3.

Soát xét là một lớp phủ lên chứng từ đã ghi sổ. Nó không thêm trạng thái vào phiếu, không chặn thao tác nào và không đụng sổ kho, quỹ, công nợ.

## Phạm vi

- Sáu loại: phiếu nhập kho, phiếu xuất kho, phiếu thu, phiếu chi, chuyển quỹ, bù trừ công nợ.
- Admin bật/tắt từng loại trong cấu hình; mặc định bật cả sáu.
- Phiếu thu và phiếu chi tự động của phiếu kho không có dòng riêng. Chúng được soát xét qua phiếu kho gốc; màn hình xem phiếu kho hiện số đã thu hoặc đã chi và quỹ.
- Không gồm báo giá (đã có luồng trạng thái riêng), tồn đầu kỳ, nợ đầu kỳ, số dư đầu kỳ quỹ.
- Chỉ tính phiếu có sự kiện từ ngày bật tính năng. Phiếu cũ không có dòng sự kiện thì không vào hộp.

## Trạng thái

| Sự kiện | Trước | Sau |
|---|---|---|
| Tạo phiếu | — | Chờ soát xét |
| Sửa hoặc khôi phục | bất kỳ | Chờ soát xét; thêm nhãn "Sửa sau duyệt" nếu phiếu từng được duyệt |
| Hủy hoặc xóa | chưa từng được duyệt | rời hộp |
| Hủy hoặc xóa | từng được duyệt | Chờ soát xét, nhãn "Hủy sau duyệt" |
| Duyệt | Chờ soát xét hoặc Yêu cầu sửa | Đã duyệt |
| Yêu cầu sửa (bắt buộc ghi chú) | Chờ soát xét hoặc Đã duyệt | Yêu cầu sửa |

Yêu cầu sửa trên phiếu đã duyệt là cách rút lại một lần duyệt nhầm, nhất là sau khi duyệt nhiều phiếu một lúc.

Phiếu đã duyệt rồi bị xóa vẫn hiện trong hộp với thông tin chụp lại lúc xóa (mã, số tiền, đối tượng, người xóa), để quản trị xác nhận đã biết. Duyệt dòng này là xác nhận.

## Quy tắc

- **Tự duyệt được.** Người có quyền soát xét duyệt được cả phiếu do chính mình lập hoặc sửa.
- **Không duyệt nhầm bản cũ.** Lệnh quyết định mang theo mã sự kiện mới nhất mà người duyệt đã thấy. Nếu phiếu vừa có sự kiện mới, lệnh bị từ chối (409) và màn hình tải lại.
- **Duyệt nhiều.** Chọn nhiều phiếu rồi duyệt một lần. Phiếu nào vừa đổi thì bị bỏ qua; kết quả trả về theo từng phiếu. Yêu cầu sửa chỉ làm trên từng phiếu, vì ghi chú là của riêng phiếu đó.
- **Khóa sổ.** Quyết định soát xét không đổi sổ sách nên không bị khóa sổ chặn.
- **Quyền.** `documents.review`, mặc định cho ADMIN và MANAGER. Người duyệt chỉ thấy loại phiếu mình có quyền xem, trong chi nhánh đang làm việc.

## Dữ liệu

Dùng `document_events` của đợt 3. Thêm:

| Bảng | Mỗi dòng là | Cột chính |
|---|---|---|
| `document_reviews` | Trạng thái soát xét hiện tại của một chứng từ | loại và id chứng từ, chi nhánh, trạng thái, sự kiện mới nhất, sự kiện đã quyết định, người quyết định, thời điểm, ghi chú, đã từng được duyệt |
| `document_review_decisions` | Một quyết định (lịch sử) | chứng từ, quyết định, ghi chú, người, thời điểm, sự kiện tại lúc quyết định |
| cấu hình soát xét | Các loại chứng từ đang bật | một dòng cấu hình |

`DocumentEventHook` cập nhật `document_reviews` trong cùng transaction với sự kiện: tạo dòng mới hoặc đưa về Chờ soát xét theo bảng trạng thái ở trên.

## API

| Endpoint | Quyền |
|---|---|
| `GET /api/reviews` (lọc trạng thái, loại phiếu, người lập; phân trang) | `documents.review` |
| `GET /api/reviews/summary` (số phiếu chờ, vài phiếu mới nhất) | `documents.review` |
| `POST /api/reviews/decisions` (danh sách phiếu kèm mã sự kiện đã thấy, quyết định, ghi chú) | `documents.review` |
| `GET /api/reviews/by-document` (trạng thái của một phiếu, cho dải thông báo trên form) | quyền xem loại phiếu đó |
| `GET`, `PUT /api/settings/review` | `system.manage_settings` |

Màn hình xem phiếu dùng các endpoint chi tiết hiện có của từng loại phiếu. Với phiếu đã xóa, các endpoint đó trả 404; màn hình hiện ảnh chụp từ sự kiện cuối.

## Giao diện

- **Soát xét (`/review`).** Ba ngăn: Chờ soát xét, Yêu cầu sửa, Đã duyệt. Lọc theo loại phiếu và người lập. Mỗi thẻ có mã, loại, nhãn lý do (Tạo mới, Sửa sau duyệt, Hủy sau duyệt), đối tượng, người và giờ thay đổi, số tiền. Chọn nhiều để duyệt. Trên desktop hiện dạng bảng qua `ResponsiveTable`.
- **Xem phiếu (`/review/:loại/:id`).** Chỉ đọc, gọn cho điện thoại: thông tin chung, các số chính, danh sách dòng hàng rút gọn, lịch sử sự kiện và quyết định. Phiếu sửa sau duyệt hiện tổng tiền trước và sau. Thanh dưới cùng có hai nút Yêu cầu sửa và Duyệt. Có lối "Mở bản đầy đủ" tới form hiện có.
- **Form phiếu trên desktop.** Một dải thông báo nhỏ cho biết trạng thái soát xét. Khi bị yêu cầu sửa, dải ghi "Yêu cầu sửa: ghi chú — người yêu cầu, giờ". Các trang danh sách chưa có cột soát xét.
- **Tổng quan.** Khối "Chờ soát xét" hiện số phiếu chờ và hai phiếu mới nhất.

## Thông báo

- Khi bị yêu cầu sửa: gửi cho người thay đổi phiếu gần nhất và người tạo phiếu (nếu khác nhau), kèm ghi chú và liên kết tới phiếu. Chủ đề `review.changes_requested`; chỉ gửi cá nhân, không gửi nhóm.
- Không gửi tin đẩy cho từng phiếu chờ soát xét. Người duyệt xem huy hiệu trên tab và khối trên Tổng quan; biến động quỹ của phiếu thu chi đã có tin riêng từ đợt 3.

## Giới hạn

- So sánh trước và sau chỉ có ở mức tổng tiền, đối tượng và ngày. Không so sánh từng dòng hàng.
- Không chặn in hay xuất phiếu chưa duyệt.
- Trang khóa sổ chưa cảnh báo về phiếu còn chờ soát xét trong kỳ.

## Kiểm thử đợt 4

- **Unit:** bảng chuyển trạng thái, kể cả "từng được duyệt"; từ chối khi mã sự kiện đã cũ; kết quả theo từng phiếu khi duyệt nhiều.
- **Integration:** tạo, sửa, hủy, khôi phục, xóa trên cả sáu loại phiếu cho đúng trạng thái; phiếu tự động không sinh dòng soát xét; người thiếu quyền xem một loại phiếu không thấy loại đó; lọc theo chi nhánh; tắt một loại trong cấu hình thì loại đó không vào hộp.
- **Vitest:** danh sách (ngăn, lọc, chọn nhiều), màn hình xem phiếu, luồng 409, dải thông báo trên form.
- **Thử tay:** duyệt và yêu cầu sửa trên điện thoại; người lập nhận tin và thấy ghi chú trên form desktop.
