# Hướng dẫn sử dụng theo từng màn hình và video YouTube làm bằng code

> Brainstorm: 2026-10-15 10:01:29
>
> - **Trạng thái:** thiết kế đã duyệt, chưa có plan. Đã review đối chiếu code ngày 2026-10-15 và sửa theo [section-02-review.md](section-02-review.md).
> - **Tham khảo** MISA, Fast, Bravo và công cụ: [section-01-research.md](section-01-research.md).
> - **Mockup:** [khung Hướng dẫn trên màn Phiếu xuất kho](mockup-01-help-panel.html), bản 2. Đã chọn phương án C. Sau review, gợi ý lần đầu đổi từ dải vàng trên tiêu đề sang một ô nổi dưới nút ? (R-H2). Chân khung bỏ link trang đầy đủ và số phiên bản.

## Problem framing

Phần mềm có 41 mục menu và khoảng 75 route. Nhiều mục có cả danh sách và form. Trong app chưa có hướng dẫn nào.

Người dùng mới (khách vừa mua, nhân viên mới) không biết một màn hình dùng để làm gì và bắt đầu từ đâu. Người dùng cũ hay quên các nghiệp vụ ít gặp như đối trừ, bù trừ, khóa sổ, tính lại giá vốn.

Tiêu chí:
1. Người dùng vào một màn hình là biết cần làm gì.
2. Khi cần thì có link sang video YouTube xem chi tiết.
3. Video làm bằng AI, tối ưu và dễ hiểu.
4. **Dễ bảo trì:** khi có chức năng mới hoặc giao diện đổi thì cập nhật hướng dẫn và video dễ dàng.

MISA, Fast và Bravo đều để tài liệu trên một trang web riêng và video trên YouTube, tách khỏi phần mềm. Nội dung vì thế dễ lệch với phần mềm: MISA còn link bài của bản 2023, nhiều video Fast quay từ 2022 với giao diện cũ. Chỉ Bravo mở đúng trang của màn hình khi bấm F1, nhờ mỗi màn hình có một khóa riêng (`?CommandKey=`). Chi tiết ở [section-01-research.md](section-01-research.md).

## Goals

- Mọi màn hình có nút **?** trên thanh đầu trang, phím **F1** cũng mở. Bấm vào thì khung **Hướng dẫn** mở ở bên phải, gồm: video, "Dùng để", các bước chính, lưu ý, phím tắt, xem thêm. Trên điện thoại khung là tấm trượt từ dưới lên.
- Mỗi bước có **mốc giờ**, bấm vào thì mở video YouTube ở đúng đoạn đó.
- Lần đầu một người mở một màn hình có hướng dẫn, một **ô gợi ý nổi** dưới nút ? mời xem.
- Nội dung là **file Markdown trong repo**, mỗi màn hình một file. Dev và Claude viết, sửa cùng PR với code. Test báo lỗi khi màn hình thiếu hướng dẫn hoặc video đã cũ.
- Video **làm bằng code**: kịch bản Playwright chạy trên dữ liệu mẫu, có giọng đọc AI tiếng Việt, ffmpeg ghép. Giao diện đổi thì chạy lại lệnh.
- Kênh YouTube **công khai**, playlist theo các nhóm của sidebar.

## Non-goals

- Thay đổi backend. Toàn bộ nằm ở frontend và một package công cụ riêng. DB quay video chỉ dùng một lần rồi bỏ.
- Ẩn bước theo quyền của user. Nội dung viết chung cho mọi vai trò.
- Nhúng trình phát YouTube trong khung. Video mở trong tab mới.
- Tìm hướng dẫn trong ô tìm kiếm Ctrl K.
- Hướng dẫn riêng cho từng tab hoặc từng hộp thoại con. Các phần này thuộc hướng dẫn của màn cha.
- Nhiều video cho một màn hình. Đợt này mỗi màn hình tối đa một video.
- Nút góp ý 👍/👎, nhiều ngôn ngữ, trợ lý AI trả lời câu hỏi.
- Đợt này chưa làm (để đợt 4): tour từng bước, trang hướng dẫn công khai, video quy trình xuyên nhiều màn, tải video lên qua API.

## Constraints & assumptions

- Người viết và cập nhật nội dung là **dev và Claude, trong repo**. Không có nhân viên hỗ trợ viết trên CMS.
- Ưu tiên **tra cứu trước**. Tour dẫn từng bước chỉ làm sau, cho vài màn khó.
- Video chỉ quay trên **dữ liệu mẫu**. Tính năng [chi nhánh demo có dữ liệu mẫu](../261014-1601-branch-sample-data/SUMMARY.md) đã merge ngày 2026-10-15, PR #19 ([plan và báo cáo thực thi](../../plans/archived/261014-1616-branch-sample-data/EXECUTION-REPORT.md)). Những điểm của tính năng này ảnh hưởng tới việc quay:
  - **Tạo chi nhánh:** `POST /api/branches` với `withSampleData: true`. Dữ liệu được nạp chạy nền. `SampleDataStatus` đi từ `Running` sang `Ready` hoặc `Failed`, và đọc được qua `GET /api/branches`.
  - **Báo cáo toàn công ty và bảng điều hành toàn công ty bỏ qua chi nhánh demo.** Trên một DB chỉ có chi nhánh demo, các báo cáo này toàn số 0.
  - Ô chọn chi nhánh hiện huy hiệu "Demo" và chữ "(demo)".
  - **Không có báo giá mẫu**, vì báo giá không có `BranchId`.
  - Dữ liệu chỉ cố định với cùng mã chi nhánh và **cùng ngày chạy**, và có thể đổi khi nâng cấp phiên bản .NET. Vì vậy mã phiếu và số liệu đổi theo ngày quay.
- Video tải lên qua YouTube Data API từ một project chưa được Google kiểm duyệt sẽ bị khóa ở chế độ riêng tư. Vì vậy đợt đầu tải tay trong YouTube Studio.
- YouTube không cho thay file của một video đã đăng. Quay lại thì phải đăng thành video mới.
- YouTube chỉ hiện chương khi chương đầu ở 0:00, có ít nhất 3 chương và mỗi chương dài ít nhất 10 giây.
- Repo chưa có CI (`.github/workflows` trống). Các kiểm tra chạy qua `npm test` và các lệnh chạy tay trước khi phát hành.
- **Playwright:** frontend chưa có, nhưng `package.json` ở thư mục gốc đã có `playwright` 1.60, Chromium đã cài trong `%LOCALAPPDATA%\ms-playwright`.
  - `recordVideo` của Playwright cố định 1 Mbit/s, mã hóa VP8 ([issue #31424](https://github.com/microsoft/playwright/issues/31424)). Ở 1080p hình bị nhòe, nên không dùng.
  - Bản ffmpeg đi kèm Playwright chỉ đủ cho việc quay của nó. Máy quay video cần ffmpeg bản đầy đủ.

## Approaches considered

### Tài liệu hướng dẫn

| Phương án | Mô tả | Ưu | Nhược | Bảo trì |
|---|---|---|---|---|
| **PA1 — Khung Hướng dẫn trong app, nội dung trong repo** | Nút ? hoặc F1 mở khung theo màn hình. Mỗi màn một file Markdown. Có test bảo vệ | Hướng dẫn nằm ngay cạnh chỗ làm việc. Sửa cùng PR. Có test báo thiếu. Cùng nguồn có thể build ra trang công khai | Phải viết nội dung cho khoảng 60 màn | ★★★ |
| PA2 — Tour tương tác là chính (driver.js) | Lần đầu vào thì tour lần lượt làm sáng từng ô. Có gợi ý khi màn trống và ⓘ cho ô khó | Tốt nhất cho người mới | Đổi bố cục là tour hỏng. Tốn công nhất. Vẫn cần tài liệu và video cho phần sâu | ★ |
| PA3 — Trang hướng dẫn riêng + YouTube, kiểu MISA/Fast | App chỉ có menu Trợ giúp và link sang bài | Đã được các hãng dùng. Tốt cho SEO | Người dùng rời app. Nội dung và phần mềm nằm hai nơi nên dễ lệch nhau | ★ |

**Đã chọn:** PA1 làm nền. Tour của PA2 làm sau cho 4–5 màn khó. Trang công khai của PA3 build từ chính bộ Markdown của PA1, cũng làm sau.

### Video hướng dẫn

| Phương án | Mô tả | Ưu | Nhược | Bảo trì |
|---|---|---|---|---|
| **V1 — Video làm bằng code** | Kịch bản Playwright + giọng đọc AI tiếng Việt + ffmpeg, chạy trên dữ liệu mẫu | Giao diện đổi thì chạy lại. Giọng và phong cách đồng nhất. Gần như chỉ tốn tiền giọng đọc. Kịch bản kiêm test E2E | Mất vài ngày dựng quy trình ban đầu. Giọng máy kém tự nhiên hơn người thật một chút | ★★★ |
| V2 — Công cụ SaaS ghi màn hình có AI (Guidde, Clueso, Trupeer) | Quay một lần, AI viết lời, lồng giọng và phóng to | Nhanh có video, không cần code | Phí hàng tháng. Chưa xác nhận có giọng tiếng Việt tốt. Đổi giao diện thì quay lại tay. Nội dung nằm ở nhà cung cấp | ★ |
| V3 — Người thật quay, AI hậu kỳ (OBS + CapCut/Descript) | Người vừa thao tác vừa nói. AI cắt đoạn lặng và tạo phụ đề | Giọng tự nhiên, gần gũi | Tốn công nhất. Chất lượng không đều. Khó cập nhật | ★ |

Loại: video có người dẫn ảo (Synthesia, HeyGen). Đắt mà không giúp hiểu thao tác hơn.

**Đã chọn:** V1.

## Recommended approach (đã chốt)

### Quyết định

| Câu hỏi | Quyết định |
|---|---|
| Người viết và cập nhật | Dev và Claude, trong repo |
| Nỗi đau chính | Cả người mới lẫn người hay quên, ưu tiên tra cứu |
| Phương án tài liệu | PA1 làm nền, tour PA2 làm sau |
| Cách hiển thị | Mockup C: nút ? trên thanh đầu trang + F1, khung bên phải, ô gợi ý nổi ở lần đầu |
| Video | V1, làm bằng code. Chụp khung hình qua CDP screencast, không dùng `recordVideo` |
| YouTube | Kênh công khai, playlist theo nhóm sidebar |
| Dữ liệu quay | Bộ nạp dữ liệu mẫu (PR #19) chạy trên DB quay riêng. Nạp xong thì chuyển chi nhánh thành chi nhánh thường. Dữ liệu còn thiếu thì tạo trong `setup` của từng video |
| Tải lên | Tải tay trong YouTube Studio. Dùng API khi đã qua kiểm duyệt (đợt 4) |

### 1. Các thành phần trong app

| Thành phần | Việc nó làm |
|---|---|
| `frontend/src/help/content/<khóa>.md` | Nội dung của một màn hình |
| `help-registry` | Gom toàn bộ nội dung vào **một chunk**, khoảng 60 file × 1,5 KB ≈ 90 KB, chừng 25 KB sau nén. Chunk tải ngay sau khi app hiện xong. Từ route hiện tại tìm ra khóa bằng `matchRoutes` của react-router trên danh sách route khai báo trong các file. Hàm này ưu tiên đoạn cố định hơn đoạn tham số, nên `/stock-out/new` không bị nhầm với `/stock-out/:id`. **Không dùng** `findActiveItem` của sidebar: hàm đó chỉ so tiền tố chuỗi |
| `frontend/src/help/videos.json` | Do lệnh `video:link` ghi ra, **không sửa tay**. Theo từng khóa: mã video, độ dài, mốc giờ và hash lời đọc của từng bước |
| `HelpButton` | Nút ? trên thanh đầu trang, xem quy tắc phím ở dưới |
| `HelpPanel` | Khung rộng khoảng 400px, không modal nên vẫn nhập liệu được. `sheet.tsx` hiện là Radix Dialog modal, nên khung dùng `modal={false}` hoặc làm riêng. Màn hình **từ 1600px** trở lên thì khung đẩy nội dung sang (vùng `main` chừa phần bên phải), dưới 1600px thì khung đè lên. Dưới 768px là tấm trượt từ dưới lên. Đổi màn hình khi khung đang mở thì nội dung đổi theo. Dùng token màu sẵn có, nên chạy được cả chế độ tối |
| `FirstVisitHint` | Ô gợi ý nổi, neo dưới nút ?. Ô này **không nằm trong luồng bố cục**: 8 trang cao hết màn hình (ví dụ form phiếu kho, xem chuỗi chiều cao ở [stock-voucher-form-page.tsx:681](../../../frontend/src/pages/stock-vouchers/stock-voucher-form-page.tsx#L681)) sẽ bị đẩy thanh tổng tiền xuống nếu chèn một dải vào trang. Chỉ hiện ở màn có hướng dẫn |

**Phím và focus:**
- F1 mở hoặc đóng khung, và chặn trang trợ giúp mặc định của trình duyệt.
- Đang mở một hộp thoại modal thì F1 không làm gì.
- Khi mở khung, focus chuyển vào tiêu đề khung. Esc chỉ đóng khung khi focus đang ở trong khung, để không đụng tới typeahead và ô đang sửa trong lưới. Đóng khung thì focus trở về chỗ cũ.

**Trên điện thoại:**
- Thanh đầu trang vừa được sửa lỗi tràn (`d4f7b511`). Plan phải đo ở chiều ngang 360px.
- Nếu thêm nút ? 44px mà vẫn vừa thì giữ nút trên thanh.
- Nếu không vừa thì nút ? chỉ hiện từ `md` trở lên, và trên điện thoại có mục "Hướng dẫn màn hình này" trong menu người dùng. Khi đó ô gợi ý neo vào avatar.

**Khóa màn hình:**
- Lấy route, bỏ `/` và **bỏ các đoạn tham số**, nối các đoạn còn lại bằng `-`. Route `/` có khóa `home`.
- Mục có route con `new` và `:id` thì thêm `.list` cho danh sách và `.form` cho form.
- Một file có thể nhận nhiều route, ví dụ trang xem chi tiết dùng chung hướng dẫn với trang cha.

Ví dụ:
- `/` → `home`;
- `/stock-out` → `stock-out.list`;
- `/stock-out/new` và `/stock-out/:id` → `stock-out.form`;
- `/reports/cash-book` → `reports-cash-book`;
- `/reports/sales-revenue` và `/reports/sales-revenue/:saleUserId` → `reports-sales-revenue`;
- `/admin/user-settings/:userId` → `admin-user-settings`;
- `/admin/users/:userId/transfer-quotations` → `admin-users-transfer-quotations`.

**Khung Hướng dẫn**, từ trên xuống:
1. Ảnh bìa video lấy từ `https://i.ytimg.com/vi/<id>/mqdefault.jpg`, kèm thời lượng và link "Xem trên YouTube". Caddyfile không đặt CSP nên tải được. Ảnh lỗi hoặc mất mạng thì hiện một thẻ chữ thay ảnh.
2. Dùng để.
3. Các bước, mỗi bước có mốc giờ. Mốc giờ mở `youtube.com/watch?v=<id>&t=<giây>` ở tab mới.
4. Lưu ý.
5. Phím tắt (ẩn trên điện thoại).
6. Xem thêm: một khóa liên quan chỉ hiện khi route đầu tiên của nó nằm dưới một mục menu mà user thấy được. Kiểm tra bằng `findActiveItem` trên kết quả của `visibleNavGroups`. Route `/` luôn hiện. Các trang cấu hình đi qua `/settings`.
7. Chân khung: "Cập nhật dd/mm/yyyy" lấy từ `updated`. Không có link trang đầy đủ cho tới khi có trang công khai (đợt 4).

**Trạng thái của khung:**
- Màn chưa có hướng dẫn: khung ghi "Chưa có hướng dẫn cho màn hình này" và liệt kê các hướng dẫn trong cùng nhóm menu.
- Màn chưa có video: ẩn khối video và các mốc giờ.
- Mất mạng: phần chữ vẫn đọc được, vì chunk nội dung nằm trong danh sách precache của service worker (`precacheAndRoute` trong `sw.ts`). Khối video báo "Cần kết nối mạng để xem video".

**Gợi ý lần đầu:**
- Ô gợi ý có hai nút "Xem hướng dẫn" và "Ẩn", cùng một link "Không hiện gợi ý nữa". Nút ? có thêm chấm vàng.
- Bất kỳ cách đóng nào (mở khung, bấm "Ẩn", Esc, bấm ra ngoài) đều coi như màn đó đã xem.
- Lưu trong `ui-store` (zustand persist, đang dùng cho ghim menu) theo user id: `helpHintSeen: Record<userId, string[]>` và `helpHintsOff: Record<userId, boolean>`. Đổi máy thì gợi ý hiện lại, chấp nhận được.

### 2. Định dạng nội dung

```md
---
key: stock-out.form
title: Phiếu xuất kho
routes: [/stock-out/new, /stock-out/:id]
sources: [frontend/src/pages/stock-vouchers/**, frontend/src/features/stock-vouchers/**, backend/src/OrderMgmt.Application/Inventory/StockVouchers/**]
related: [receipts.form, credit-limits]
updated: 2026-10-15
---
## Dùng để
Ghi nhận hàng xuất khỏi kho: bán cho khách, trả hàng nhà cung cấp, xuất dùng nội bộ. Phiếu xuất bán trừ tồn kho và tăng công nợ phải thu của khách.

## Các bước
1. {#reason} Chọn **Lý do xuất** (Xuất bán hàng) và **Kho**.
2. {#partner} Gõ mã hoặc tên khách ở **Mã đối tượng**, Enter để chọn.
3. {#lines} Nhập hàng ở lưới: mã hàng → số lượng → đơn giá. Enter sang ô kế tiếp, `Insert` thêm dòng.

## Lưu ý
- Vượt hạn mức nợ: hệ thống cảnh báo hoặc chặn, tùy cấu hình Hạn mức nợ.

## Phím tắt
- `Ctrl S` Lưu
- `Alt 1–4` Đổi tab
```

- **Các mục cố định:** `Dùng để` và `Các bước` là bắt buộc. `Lưu ý` và `Phím tắt` có thể bỏ.
- **`sources`:** đường dẫn tính từ thư mục gốc repo, được phép trỏ vào backend khi thông báo trong mục "Lưu ý" đến từ backend.
- **Định dạng trong dòng:** chỉ dùng `**đậm**` cho tên nút, tên ô, và `` `phím` `` cho phím tắt. Không dùng link trong nội dung, liên kết sang màn khác đi qua `related`.
- **Đọc file:** frontmatter chỉ có dạng `khóa: giá trị` và `khóa: [a, b]`, nên một bộ đọc nhỏ tự viết là đủ, không bắt buộc thêm thư viện. Plan chốt cách làm.
- **Mã bước** `{#id}`: duy nhất trong file. Kịch bản video gắn cảnh quay vào mã này, nên đổi thứ tự các bước không làm lệch mốc giờ.
- **Quy tắc viết:**
  - "Dùng để" tối đa 2 câu.
  - Có 3–7 bước, mỗi bước bắt đầu bằng một động từ.
  - Tên nút và tên ô viết đậm, đúng chữ trên màn hình.
  - "Lưu ý" chỉ ghi những thông báo mà người dùng thật sự sẽ gặp.
  - Tiếng Việt có dấu, không dùng tên kỹ thuật như entity hay endpoint.

### 3. Cơ chế bảo trì

1. **Cùng một PR:** đổi giao diện hay thao tác thì sửa file hướng dẫn. Nếu các bước đổi thì quay lại video.
2. **Test bảo vệ**, chạy trong `npm test`:
   - **Đọc route của router:** tách cây `<Route>` trong `App.tsx` (94 phần tử, có lồng nhau và route `index`) thành một fragment được export. Test đọc fragment đó bằng `createRoutesFromChildren` của react-router mà không cần render, rồi ghép ra đường dẫn đầy đủ. Bỏ qua `/login`, `/403`, `/404`, `*` và các route bố cục không có đường dẫn.
   - Mọi route đọc được ở trên có **đúng một** file nhận nó. Như vậy các trang cấu hình đi từ `/settings` và các trang xem chi tiết cũng được tính.
   - `HELP_PENDING` liệt kê **các route** chưa có hướng dẫn (lúc kết thúc đợt 1 là mọi route trừ các route của 12 màn đã viết), và **chỉ được ngắn đi**. Route mới không có hướng dẫn và không nằm trong danh sách thì test báo lỗi. Route đã có hướng dẫn mà vẫn còn trong danh sách cũng báo lỗi.
   - Cấu trúc file: đủ frontmatter, đủ các mục bắt buộc, 3–7 bước, mã bước không trùng, `related` trỏ tới khóa có thật.
   - `videos.json`: mọi khóa và mọi mã bước phải có trong nội dung. Video còn trỏ tới một bước đã bị xóa thì test báo **"video cũ, cần quay lại"**.
3. **`npm run help:stale`** chỉ cảnh báo, không chặn. Chạy trước mỗi lần phát hành. Lệnh liệt kê:
   - các màn có `sources` được commit sau lần commit cuối của file hướng dẫn hoặc của mục tương ứng trong `videos.json`;
   - các màn có hash lời đọc trong `videos.json` khác với câu hiện tại của bước, hoặc có bước mới chưa có trong video. Tức là bước đã được thêm hay sửa câu sau lần quay;
   - các file có `updated` cũ hơn ngày commit cuối của chính file đó.
4. **Mẫu PR** `.github/pull_request_template.md` có ô tick: "Đổi giao diện hay thao tác → đã cập nhật hướng dẫn (và video nếu các bước đổi)".
5. **Skill `help-content`** trong `.claude/skills`. Đầu vào là một khóa màn hình hoặc một diff. Skill đọc code màn hình, tài liệu BD và hướng dẫn hiện có, soạn hoặc cập nhật file theo quy tắc viết, rồi liệt kê các cảnh video cần quay lại.
6. **Mục "Hướng dẫn người dùng"** trong `docs/code-standard/conventions.md` ghi lại các quy tắc trên.

### 4. Quy trình làm video (V1)

Đặt trong package riêng `tools/help-videos/`, có `package.json` riêng. `playwright` khóa cùng phiên bản với thư mục gốc (1.60), nên dùng lại được Chromium đã cài.

**Kịch bản** (`tools/help-videos/scripts/stock-out.form.video.ts`):

```ts
export default defineVideo({
  help: 'stock-out.form',
  title: 'Lập phiếu xuất bán hàng',
  intro: 'Video này hướng dẫn lập một phiếu xuất bán hàng trong khoảng 2 phút.',
  start: '/stock-out/new',
  // setup?: (api) => Promise<void>, tạo dữ liệu còn thiếu qua API trước khi quay
  scenes: [
    { step: 'reason', do: (ui) => ui.pick('Lý do xuất', 'Xuất bán hàng') },
    { step: 'partner', say: 'Gõ tên khách, chọn khách hàng đầu tiên rồi nhấn Enter.',
      do: (ui) => ui.typeahead('Mã đối tượng', { pickFirst: true }) },
  ],
  outro: 'Tiếp theo, xem video Phiếu thu tiền khách hàng.',
});
```

- Lời thoại của một cảnh mặc định là câu của bước tương ứng trong file hướng dẫn, đã bỏ định dạng. Chỉ viết `say` khi muốn nói tự nhiên hơn. Mỗi bước chỉ viết một lần mà dùng ở bốn nơi: khung hướng dẫn, lời đọc, phụ đề, chương YouTube.
- `ui` là bộ hàm nhỏ tìm ô theo nhãn trên màn hình (`pick`, `type`, `typeahead`, `click`, `press`). Nó cũng vẽ con trỏ giả và khung sáng quanh ô.
- **Kịch bản không được dựa vào mã phiếu hay số liệu cụ thể**, vì dữ liệu mẫu đổi theo ngày quay. Video form tự tạo phiếu mới trong lúc quay. Video danh sách và báo cáo chọn dòng theo vai trò (ví dụ "phiếu xuất mới nhất"), không theo mã.

**Môi trường quay** (`npm run video:env`), dựng lại mỗi lần quay:
1. Tạo mới DB `qldonhang_video` (không dùng DB dev), chạy migration, chạy backend và frontend trỏ vào DB đó.
2. Đăng nhập admin. Đặt thông tin đơn vị và tên hiển thị của user thành **dữ liệu mẫu**, vì kênh công khai.
3. `POST /api/branches` với `withSampleData: true`, rồi gọi `GET /api/branches` lặp lại cho tới khi `SampleDataStatus = Ready`. Gặp `Failed` thì dừng và in `SampleDataError`.
4. **Chỉ trên DB quay này**, chạy SQL đặt `IsDemo = false` và đổi tên chi nhánh (ví dụ "Chi nhánh Hà Nội"). Mục đích: hết huy hiệu Demo, và báo cáo toàn công ty cùng bảng điều hành có số liệu. Rồi đặt chi nhánh này làm chi nhánh mặc định của admin.
5. `setup` của từng video tạo thêm dữ liệu qua API khi cần, ví dụ báo giá cho các video báo giá và trang Tổng quan.

**`npm run video <khóa>`:**
1. Tạo giọng đọc cho phần mở đầu, từng cảnh và phần kết. Lưu sẵn ở `.cache/tts/<hash>.wav` (gitignore), với hash tính từ câu và giọng. Câu không đổi thì không tạo lại.
2. Quay ở 1920×1080, phóng giao diện 125% để chữ đọc được trên điện thoại. Có con trỏ giả di chuyển mượt, hiệu ứng khi bấm, khung sáng quanh ô đang thao tác, gõ phím chậm.
   - **Cách chụp:** CDP `Page.startScreencast`, khung hình JPEG chất lượng khoảng 90 ở đúng độ phân giải thiết bị, kèm mốc thời gian. Screencast chỉ gửi khung hình khi màn hình thay đổi, nên ffmpeg dựng lại thành video 30 khung/giây đều.
   - Mỗi cảnh dài bằng `max(thời gian thao tác, lời đọc) + 0,5 giây`. Mốc giờ đầu mỗi cảnh được ghi lại.
3. ffmpeg bản đầy đủ mã hóa H.264 (CRF khoảng 18), ghép thẻ tiêu đề (2 giây), phần quay và thẻ kết thúc, rồi trộn giọng đọc vào đúng mốc giờ.
4. Kết quả ở `out/<khóa>/` (gitignore):
   - `video.mp4`;
   - `vi.srt`: phụ đề giữ chữ viết tắt;
   - `thumbnail.png`: tên màn hình trên nền màu của nhóm menu;
   - `description.txt`: mục đích, chương, gợi ý video tiếp theo. **Chương** bắt đầu bằng "0:00 Giới thiệu". Cảnh nào ngắn hơn 10 giây thì gộp vào chương trước. Nếu không đủ 3 chương thì bỏ danh sách chương. Mốc giờ trong khung Hướng dẫn vẫn tách theo từng bước;
   - `chapters.json`: mốc giờ và hash lời đọc của từng bước.

**`npm run video <khóa> -- --dry`:** chạy hết các cảnh mà không tạo giọng, không chụp, không ghép. Đây là kiểm tra E2E cho kịch bản. Một cảnh không tìm thấy ô nghĩa là video đã cũ: lệnh báo lỗi và không xuất video.

**Đăng lên YouTube:**
1. Tải tay trong YouTube Studio, khoảng 2 phút mỗi video: tải mp4, dán mô tả, tải phụ đề và ảnh bìa, chọn playlist.
2. Chạy `npm run video:link <khóa> <mã-video>` để ghi vào `frontend/src/help/videos.json`:
   ```json
   { "stock-out.form": { "youtubeId": "…", "title": "Lập phiếu xuất bán hàng", "duration": 155,
     "steps": { "reason": { "at": 8, "hash": "3f2a…" }, "partner": { "at": 21, "hash": "9c1e…" } } } }
   ```
3. Khi quay lại:
   - đăng thành video mới và chạy `video:link` với mã mới;
   - chuyển video cũ sang không công khai, **gỡ khỏi playlist** (video không công khai vẫn hiện trong playlist công khai), sửa mô tả thành "Đã có bản mới: <link>".

   App luôn trỏ tới bản mới.

**Giọng đọc:**
- Cho nghe thử cùng một đoạn có từ khó (PX00012, HTTT, VAT, Ctrl S, 02-VT, m²) với Azure (vi-VN HoaiMy, NamMinh), Google Cloud (vi-VN), FPT.AI hoặc Vbee, và ElevenLabs. Chọn **một giọng cho mọi video**.
- Bảng cách đọc `pronunciations.json`, ví dụ "HTTT" → "hình thức thanh toán", "m²" → "mét vuông", "Ctrl" → "Control". Chỉ áp dụng cho giọng đọc, phụ đề giữ nguyên chữ.
- Khoảng 60 video × 1.500 ký tự ≈ 90 nghìn ký tự, nằm trong hạn mức miễn phí của phần lớn nhà cung cấp.
- Không dùng endpoint không chính thức (như edge-tts) cho video phát hành.

**Quy tắc cho video dễ hiểu:**
- Mỗi video làm một việc, dài 1–3 phút.
- Câu mở đầu nói rõ xem xong sẽ biết làm gì.
- Dữ liệu mẫu trông như thật.
- Thao tác chậm, khung sáng ở ô đang nhập, nghỉ 0,5 giây sau mỗi bước.
- Có phụ đề, chương theo từng bước.
- Cuối video gợi ý xem video tiếp theo.

### 5. Kênh YouTube

- Kênh công khai. Playlist theo các nhóm của sidebar: Bán hàng, Kho, Thu chi, Công nợ, Báo cáo, Danh mục, Hệ thống, cộng thêm "Bắt đầu sử dụng".
- Tiêu đề theo mẫu `Lập phiếu xuất bán hàng | Kho | QLBanHang`. Mô tả lấy từ `description.txt`. Phụ đề tiếng Việt. Ảnh bìa dùng màu của nhóm menu.

### 6. Chia đợt

| Đợt | Nội dung | Phụ thuộc |
|---|---|---|
| 1. Khung hướng dẫn | Tách cây route của `App.tsx`, registry, `HelpButton`, `HelpPanel`, F1, `FirstVisitHint`, test bảo vệ có `HELP_PENDING`, `help:stale`, mẫu PR, mục conventions, skill `help-content`. Nội dung cho 12 màn: `quotations.list`, `quotations.form`, `customers.list`, `stock-in.form`, `stock-out.form`, `receipts.form`, `payments.form`, `fund-transfers.form`, `debt-matching`, `inventory-opening-stock`, `debt-opening`, `dashboard-executive` | Không |
| 2. Nội dung còn lại | Claude soạn theo từng lô bằng skill `help-content`, dev duyệt, đến khi `HELP_PENDING` trống | Đợt 1 |
| 3. Video | **Việc đầu tiên là spike chất lượng:** quay thử 20 giây bằng CDP screencast + ffmpeg, so với `recordVideo`, xem trên điện thoại. Sau đó: package `tools/help-videos`, môi trường quay, nghe thử và chọn giọng, kịch bản và video cho 12 màn của đợt 1, lập kênh và playlist | Đợt 1, vì cần mã bước. Dữ liệu mẫu đã có. Chạy song song với đợt 2 được |
| 4. Để sau | Tour driver.js (MIT) cho 4–5 màn khó, trang hướng dẫn công khai build từ cùng bộ Markdown, video quy trình "Bắt đầu sử dụng", tải lên qua API sau khi Google kiểm duyệt | Đợt 3 |

### 7. Test

- **Unit:**
  - khớp route với khóa: `/stock-out/new` ra form chứ không phải `:id`, danh sách và form, khóa `home`, đoạn tham số bị bỏ, màn chưa có hướng dẫn;
  - đọc frontmatter và các mục;
  - định dạng trong dòng: chữ đậm, phím;
  - mã bước;
  - ghép `videos.json`: thiếu video thì ẩn;
  - đọc route từ fragment của router (`createRoutesFromChildren`): route lồng nhau, `index`, route bố cục.
- **Component:**
  - khung hiện đủ các mục;
  - F1 và nút ? mở, đóng được; F1 không làm gì khi đang mở hộp thoại modal;
  - Esc chỉ đóng khi focus trong khung; focus trở về chỗ cũ;
  - đẩy nội dung ở 1600px trở lên, đè lên ở dưới 1600px;
  - đổi route thì nội dung đổi theo;
  - ô gợi ý chỉ hiện một lần cho mỗi user ở mỗi màn, đóng cách nào cũng tính; "Không hiện gợi ý nữa" tắt cho tất cả;
  - ảnh bìa lỗi thì thay bằng thẻ chữ;
  - tấm trượt trên điện thoại;
  - "Xem thêm" lọc theo menu user thấy được.
- **Test bảo vệ:** như mục 3.
- **Video:**
  - `--dry` cho từng kịch bản, chạy tay trước khi phát hành;
  - unit test cho phần tạo SRT, chương (0:00, ít nhất 3 chương, gộp cảnh ngắn hơn 10 giây), mô tả, hash lời đọc và bảng cách đọc.

## Open questions

1. Tên kênh YouTube, và tài khoản Google nào sở hữu kênh.
2. Danh sách 12 màn của đợt 1 là đề xuất. Chốt lại khi viết plan.
3. Giọng nam hay nữ, và chọn nhà cung cấp nào. Chốt sau khi nghe thử.
4. Tên đơn vị, chi nhánh và người dùng hiển thị trên video.
5. Đợt 4: tên miền và chỗ đặt trang hướng dẫn công khai (cùng Caddy dưới `/huong-dan` hay một trang riêng).

## Next steps

1. Viết plan cho đợt 1 bằng skill `write-plan`.
2. Viết plan riêng cho đợt 3 (video), bắt đầu bằng spike chất lượng quay. Dữ liệu mẫu đã có, nên chỉ cần đợt 1 xong phần mã bước.
3. Lập kênh YouTube và nghe thử giọng đọc. Hai việc này làm song song với đợt 1 được.
