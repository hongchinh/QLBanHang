# Review đối chiếu code — hướng dẫn sử dụng theo màn hình

> Thuộc brainstorm [Hướng dẫn sử dụng theo từng màn hình](SUMMARY.md) · Review: 2026-10-15, trên `main` ở `3ae38049` (đã có PR #19 dữ liệu mẫu) · Kết quả: 4 mức cao, 9 mức trung bình, 9 mức thấp. Tất cả đã được sửa trong SUMMARY.md và mockup bản 2.

## Cách review

- Đối chiếu từng khẳng định của SUMMARY.md với code: `App.tsx`, `nav-config.ts`, `nav-search.ts`, `app-layout.tsx`, `app-header.tsx`, `sheet.tsx`, `ui-store.ts`, `sw.ts`, `vite.config.ts`, `Caddyfile`, `stock-voucher-form-page.tsx`, và `package.json` ở thư mục gốc.
- Đọc báo cáo thực thi của [plan dữ liệu mẫu](../../plans/archived/261014-1616-branch-sample-data/EXECUTION-REPORT.md) và code backend liên quan (`BranchService`, `SampleDataRunner`, `BranchDtos`).
- Kiểm tra hai giả định bên ngoài: chất lượng `recordVideo` của Playwright và điều kiện để YouTube hiện chương.

## Mức cao

| # | Phát hiện | Bằng chứng | Đã xử lý |
|---|---|---|---|
| R-H1 | Tài liệu ghi tính năng dữ liệu mẫu "đã duyệt, chưa làm" và cho đợt video chờ nó. Thực tế tính năng đã merge cùng ngày. Link tới plan cũng hỏng. | `3ae38049 Merge pull request #19 from hongchinh/feat/branch-sample-data`. Plan đã chuyển sang `docs/plans/archived/261014-1616-branch-sample-data/` | Sửa mục Constraints, bảng Quyết định, bảng chia đợt (đợt 3 chỉ còn phụ thuộc đợt 1) và Next steps. Sửa link |
| R-H2 | Dải gợi ý lần đầu do layout vẽ phía trên `<Outlet>` sẽ làm vỡ các trang cao hết màn hình. | `main#main-content` là vùng cuộn, khung trang dùng `h-full` ([stock-voucher-form-page.tsx:681](../../../frontend/src/pages/stock-vouchers/stock-voucher-form-page.tsx#L681)). 8 trang dùng `flex h-full` (form và danh sách phiếu kho, danh sách phiếu thu chi, chuyển quỹ, bù trừ, báo giá, QR thanh toán, doanh thu chi tiết). Chèn một dải cao khoảng 44px thì `main` cuộn và thanh tổng tiền bị đẩy xuống dưới mép màn hình | Đổi thành ô gợi ý nổi neo dưới nút ?, không nằm trong luồng bố cục. Đã cập nhật mockup C (bản 2) |
| R-H3 | Dữ liệu để quay không như tài liệu giả định. | Báo cáo thực thi: "The company dashboard showed all figures at 0". Có huy hiệu Demo trên ô chọn chi nhánh. Báo giá mẫu là non-goal. Hạt giống ngẫu nhiên lấy từ FNV-1a của mã chi nhánh, còn kế hoạch dữ liệu phụ thuộc ngày chạy và phiên bản .NET | Thêm "Môi trường quay": nạp dữ liệu qua API, chờ `Ready`, rồi trên DB quay đặt `IsDemo = false` và đổi tên chi nhánh. Thêm `setup` cho từng video để tạo báo giá. Kịch bản không dựa vào mã phiếu hay số liệu. Câu hỏi mở số 4 cũ (mã phiếu có cố định không) đã có lời đáp và được thay |
| R-H4 | `recordVideo` của Playwright cho hình nhòe ở 1080p. Tài liệu cũng sai khi nói repo chưa có Playwright. | [microsoft/playwright#31424](https://github.com/microsoft/playwright/issues/31424): 1 Mbit/s, VP8, không chỉnh được. `package.json` ở thư mục gốc có `playwright ^1.60.0`; `%LOCALAPPDATA%\ms-playwright` có `chromium-1234` và `ffmpeg-1011` (bản ffmpeg rút gọn chỉ dùng để Playwright tự quay) | Chụp khung hình bằng CDP `Page.startScreencast` rồi mã hóa H.264 bằng ffmpeg bản đầy đủ. Đợt 3 bắt đầu bằng một spike chất lượng. Package khóa `playwright` 1.60 để dùng lại Chromium đã cài |

## Mức trung bình

| # | Phát hiện | Đã xử lý |
|---|---|---|
| R-M1 | Tài liệu tự mâu thuẫn. Nó nói mỗi màn hình là một chunk tải khi cần. Nhưng tìm khóa từ route, và để ô gợi ý biết màn có hướng dẫn không, thì phải đọc `routes` của mọi file ngay từ đầu. | Gom vào một chunk, khoảng 25 KB sau nén, tải ngay sau khi app hiện xong |
| R-M2 | Tài liệu nói khớp route "giống sidebar". Nhưng `findActiveItem` (`nav-search.ts`) chỉ so tiền tố chuỗi trên `to`, không hiểu `:id`, và với nó `/stock-out/new` khớp cả `new` lẫn `:id`. | Dùng `matchRoutes` của react-router, ưu tiên đoạn cố định. Thêm unit test cho trường hợp này |
| R-M3 | Quy tắc khóa cho route `/` ra chuỗi rỗng, và chưa nói cách đặt tên cho route chỉ có tham số (`/admin/user-settings/:userId`, `/admin/users/:userId/transfer-quotations`). | `/` → `home`. Bỏ đoạn tham số khi tạo khóa. Thêm ví dụ |
| R-M4 | Tài liệu để mở cách test đọc danh sách route. `App.tsx` khai báo 94 `<Route>` bằng JSX lồng nhau, không có danh sách nào để đọc. | Tách cây `<Route>` thành fragment được export, đọc bằng `createRoutesFromChildren`. Việc này đưa vào đợt 1 |
| R-M5 | YouTube chỉ hiện chương khi chương đầu ở 0:00, có ít nhất 3 chương và mỗi chương dài ít nhất 10 giây ([hướng dẫn](https://www.storyblocks.com/resources/tutorials/how-to-add-chapters-to-youtube-video)). Một bước ngắn là mất toàn bộ chương. | Mô tả bắt đầu bằng "0:00 Giới thiệu", gộp cảnh ngắn hơn 10 giây vào chương trước, không đủ 3 chương thì bỏ danh sách chương. Mốc giờ trong khung vẫn tách theo từng bước |
| R-M6 | Thêm nút ? trên điện thoại có thể làm tràn lại thanh đầu trang vừa được sửa (`d4f7b511 fix(frontend): mobile header no longer overflows`). | Plan đo ở 360px. Nếu không vừa thì nút ? chỉ hiện từ `md`, và trên điện thoại có mục trong menu người dùng |
| R-M7 | Test chỉ phát hiện video cũ khi một bước bị xóa. Thêm bước hoặc sửa câu thì không ai biết. | `videos.json` lưu hash lời đọc của từng bước. `help:stale` báo khi hash khác hoặc có bước mới |
| R-M8 | `sources` chỉ có đường dẫn frontend, nên bỏ sót các thay đổi ở backend làm đổi thông báo trong mục "Lưu ý". | Đường dẫn tính từ thư mục gốc repo, có ví dụ trỏ vào `backend/src/OrderMgmt.Application/Inventory/StockVouchers/**` |
| R-M9 | Ở 1280px khung che nút Lưu và In của form (mockup A), trong khi một bước lại bảo "Bấm Lưu". | Từ 1600px trở lên thì khung đẩy nội dung sang, dưới đó thì đè lên |

## Mức thấp

| # | Phát hiện | Đã xử lý |
|---|---|---|
| R-L1 | "Khoảng 45 mục menu", thực tế `nav-config.ts` có 41. | Sửa thành 41 |
| R-L2 | Mockup có "bản 1.8" và link "Mở trang hướng dẫn đầy đủ", không khớp với thiết kế. | Mockup bản 2 bỏ cả hai |
| R-L3 | `updated` sửa tay nên dễ quên. | `help:stale` báo khi `updated` cũ hơn ngày commit cuối của file |
| R-L4 | Video cũ chuyển sang không công khai nhưng vẫn hiện trong playlist công khai. | Thêm bước gỡ khỏi playlist |
| R-L5 | Kênh công khai nhưng thanh đầu trang hiện tên user và thông tin đơn vị. | Môi trường quay đặt hai thứ này thành dữ liệu mẫu. Thêm câu hỏi mở số 4 |
| R-L6 | Quy tắc lọc "Xem thêm" bằng `visibleNavGroups` chưa nói cách xử lý form và trang cấu hình (không có trong menu). | Khóa hiện khi route đầu tiên của nó nằm dưới một mục menu user thấy được (`findActiveItem`). `/` luôn hiện |
| R-L7 | Chưa nói gì về Esc và focus, nên dễ đụng với typeahead và ô đang sửa trong lưới. | Focus vào tiêu đề khung khi mở. Esc chỉ đóng khi focus trong khung. Đóng thì focus trở về chỗ cũ |
| R-L8 | Ảnh bìa video chưa chốt lấy từ đâu. | Lấy từ `i.ytimg.com/vi/<id>/mqdefault.jpg` (Caddyfile không có CSP), lỗi thì thay bằng thẻ chữ |
| R-L9 | `sheet.tsx` là Radix Dialog modal, không dùng thẳng cho khung không modal được. | Ghi rõ: dùng `modal={false}` hoặc làm riêng |

## Đã xác nhận đúng

- `ui-store` dùng zustand persist và lưu ghim menu theo user id (`pinnedNavPaths: Record<string, string[]>`). Có thể thêm `helpHintSeen` và `helpHintsOff` cùng kiểu.
- `visibleNavGroups(hasPermission, isInRole)` có trong `nav-config.ts`.
- Service worker dùng `injectManifest` và `precacheAndRoute(self.__WB_MANIFEST)`, nên các file JS của bản build được precache.
- `.github/workflows` trống, repo chưa có CI.
- `Caddyfile` không đặt `Content-Security-Policy`.
- F1 chưa bị dùng ở đâu trong frontend.
- Các phím tắt trong mockup khớp code ở `stock-voucher-form-page.tsx` và `stock-line-grid.tsx`: Ctrl S lưu, Alt 1–4 đổi tab, Insert thêm dòng, Ctrl Delete xóa dòng.
- Các route của 12 màn đợt 1 có trong `App.tsx`. Route của danh sách khách hàng, chuyển quỹ, phiếu thu, phiếu chi và phiếu kho đều có `new` và `:id`.
- API dữ liệu mẫu: `CreateBranchRequest.WithSampleData` (`BranchDtos.cs:24`). Trạng thái `SampleDataStatus.Running/Ready/Failed` do `BranchService` và `SampleDataRunner` ghi.

## Không áp dụng

- **Kiểm tra mọi chữ đậm trong hướng dẫn có xuất hiện trong `sources`** để bắt nhãn bị đổi tên. Không áp dụng vì nhiều nhãn được ghép động, như `Thêm ${labels.title.toLowerCase()}` trong form phiếu kho, nên báo nhầm sẽ nhiều. Có thể xem lại sau đợt 2, khi đã có đủ nội dung để đo tỉ lệ báo nhầm.
- **Đổi `main` thành flex column** để giữ dải gợi ý trong luồng bố cục. Không áp dụng vì phải đổi chuỗi chiều cao của mọi trang. Ô nổi giải quyết được mà không đụng vào layout.
