# Phân tích hành vi người dùng — 4.2. Mã đối tượng

> **Cập nhật 2026-10-15.** Brainstorm [thiết kế lại form phiếu thu/chi và ô đối tượng dùng chung](../brainstorms/261015-0644-cash-voucher-form-redesign/SUMMARY.md) (quyết định D2, D4, D9, D11, D13) thay các mục sau. Phần còn lại vẫn giữ nguyên.
>
> - **§1, §3, AC-OBJ-001, AC-OBJ-006**: ô mã tìm **cả khách hàng và nhà cung cấp** đang hoạt động, rồi lý do tự khớp với đối tượng được chọn. Đối tượng bắt buộc hay không theo quy tắc lý do của server. Áp dụng cho phiếu thu/chi và phiếu kho. Báo giá chỉ tìm khách hàng.
> - **§4, §15.2**: nút + vẫn nằm cạnh ô và Tab tới được. Ngoài ra có thêm **dòng "＋ Thêm mới …" cuối danh sách** và **phím F4**. Khi đã tìm xong mà không có kết quả, Enter mở hộp thêm nhanh.
> - **§10, §13**: Lý do nộp có dạng **"Tên lý do - Tên đối tượng"**, không còn "Thu tiền của [Tên]". Bỏ chọn đối tượng thì Lý do nộp (nếu còn là chữ tự sinh) về tên lý do.
> - **§11, §12**: Diễn giải của các dòng **chưa sửa tay** đổi theo Lý do nộp. Dòng người dùng đã nhập không bao giờ bị ghi đè.
> - Vẫn giữ: focus sang Tên đối tượng sau khi chọn hoặc thêm nhanh (§5, §8, AC-OBJ-021); gõ chữ mà không chọn thì không cho lưu (§14, AC-OBJ-023/024).

## 1. Quy tắc chính

Trường **Mã đối tượng** là field bắt buộc trong màn hình **Thêm mới Phiếu thu**.

Với nghiệp vụ mặc định **“5. Thu khác”**, danh sách đối tượng chỉ lấy:

```text
Khách hàng
```

Không hiển thị đối tượng ngừng sử dụng/inactive.

---

## 2. Hành vi khi focus vào Mã đối tượng

Khi người dùng focus vào ô **Mã đối tượng** nhưng chưa gõ từ khóa:

- Không mở dropdown.
- Không hiển thị danh sách gần đây.
- Không hiển thị danh sách thường dùng.
- Chờ người dùng nhập từ khóa.

```text
Focus Mã đối tượng
    ↓
Keyword rỗng
    ↓
Không mở dropdown
```

---

## 3. Hành vi khi nhập từ khóa

Khi người dùng gõ từ khóa:

- Dropdown mở.
- Hệ thống gọi API tìm kiếm.
- Chỉ tìm trong nhóm **Khách hàng**.
- Chỉ lấy khách hàng đang hoạt động.
- Hỗ trợ tìm kiếm có dấu/không dấu.

Tìm kiếm theo:

| Tiêu chí |
|---|
| Mã đối tượng |
| Tên đối tượng |
| Mã số thuế |
| Địa chỉ |
| Điện thoại |

API đề xuất:

```http
GET /api/objects/search?keyword={keyword}&objectTypes=Customer&activeOnly=true
```

---

## 4. Icon Thêm mới đối tượng

Icon **Thêm mới** luôn nằm **cạnh ô input Mã đối tượng**.

Không đặt icon thêm mới trong danh sách kết quả.

UI đề xuất:

```text
[Mã đối tượng input________________] [+]
```

Trong đó:

- `[+]` là icon thêm mới đối tượng.
- Icon có thể focus được bằng bàn phím.
- Icon có tooltip/aria-label:

```text
Thêm mới khách hàng
```

Khi người dùng click hoặc dùng bàn phím kích hoạt icon:

- Mở form thêm mới đối tượng trong danh mục đối tượng.
- Vì loại đối tượng được lọc là **Khách hàng**, form thêm mới nên mặc định loại đối tượng là **Khách hàng**.

---

## 5. Hành vi thêm nhanh đối tượng

Luồng thêm nhanh:

```text
Người dùng nhập từ khóa
    ↓
Không tìm thấy hoặc muốn thêm mới
    ↓
Người dùng click / focus icon Thêm mới cạnh input
    ↓
Hệ thống mở form thêm mới đối tượng trong danh mục đối tượng
    ↓
Người dùng lưu khách hàng mới
    ↓
Hệ thống tự động chọn khách hàng vừa tạo vào Phiếu thu
    ↓
Fill Tên đối tượng, Địa chỉ, Lý do nộp
    ↓
Focus sang Tên đối tượng
```

Sau khi thêm nhanh thành công, hệ thống **tự động chọn đối tượng vừa tạo vào phiếu thu**.

---

## 6. Dropdown kết quả tìm kiếm

Dropdown hiển thị dạng bảng mini.

Các cột:

| Cột | Mô tả |
|---|---|
| Mã đối tượng | Mã khách hàng |
| Tên đối tượng | Tên khách hàng |
| Mã số thuế | Mã số thuế |
| Địa chỉ | Địa chỉ |
| Điện thoại | Số điện thoại |
| Loại đối tượng | Khách hàng |

Vì nghiệp vụ này chỉ lấy khách hàng, cột **Loại đối tượng** có thể vẫn hiển thị để nhất quán UI hoặc có thể ẩn nếu muốn gọn.

---

## 7. Hành vi bàn phím trong dropdown

### 7.1. Arrow Up / Arrow Down

| Phím | Hành vi |
|---|---|
| Arrow Down | Di chuyển highlight xuống dòng tiếp theo |
| Arrow Up | Di chuyển highlight lên dòng trước |

---

### 7.2. Tab

Khi dropdown đang mở:

```text
Tab = chuyển highlight sang dòng tiếp theo
```

Khi Tab đến dòng cuối, nhấn Tab tiếp:

```text
Vòng lại dòng đầu
```

Luồng:

```text
Highlight dòng 1
    ↓ Tab
Highlight dòng 2
    ↓ Tab
Highlight dòng 3
    ↓ Tab
Highlight dòng cuối
    ↓ Tab
Highlight dòng 1
```

Như vậy, khi dropdown đang mở, Tab không thoát khỏi field mà dùng để duyệt kết quả.

---

### 7.3. Shift + Tab

Đề xuất hành vi tương ứng:

```text
Shift + Tab = chuyển highlight lên dòng trước
```

Nếu đang ở dòng đầu, Shift + Tab có thể vòng lại dòng cuối.

---

### 7.4. Enter

Khi dropdown đang mở:

| Trạng thái | Hành vi |
|---|---|
| Đang highlight một dòng | Chọn dòng đó |
| Chỉ có một kết quả đúng mã tuyệt đối | Tự động chọn kết quả đó |
| Không có dòng nào | Không chọn |

Ví dụ:

```text
Người dùng nhập KH001
API trả về đúng 1 khách hàng KH001
Người dùng nhấn Enter
Hệ thống chọn KH001
```

---

### 7.5. Esc

Khi dropdown đang mở:

```text
Esc = đóng dropdown, không chọn dữ liệu
```

Nếu input đang có text nhưng chưa chọn khách hàng hợp lệ, field vẫn chưa hợp lệ.

---

## 8. Hành vi chọn khách hàng

Người dùng có thể chọn khách hàng bằng:

- Click chuột vào một dòng.
- Arrow Up/Down + Enter.
- Tab để duyệt dòng + Enter.
- Nhập đúng mã tuyệt đối và Enter nếu chỉ có một kết quả.

Sau khi chọn khách hàng, hệ thống:

1. Set `objectId`.
2. Set `objectCode`.
3. Set `objectName`.
4. Fill **Tên đối tượng**.
5. Fill **Địa chỉ**.
6. Tự sinh **Lý do nộp**.
7. Đóng dropdown.
8. Focus sang **Tên đối tượng**.

---

## 9. Trường Tên đối tượng sau khi chọn

Sau khi chọn khách hàng, focus chuyển sang **Tên đối tượng**.

Trường **Tên đối tượng**:

- Được tự động fill theo khách hàng đã chọn.
- Cho phép người dùng sửa tay.
- Dữ liệu sửa tay là tên hiển thị trên chứng từ.
- Không làm thay đổi tên khách hàng trong danh mục gốc.

Rule đề xuất:

```text
objectNameFromMaster = tên trong danh mục khách hàng
displayObjectName = tên người dùng sửa trên chứng từ
```

Khi lưu chứng từ, nên lưu cả:

- `objectId`
- `objectCode`
- `objectName` tại thời điểm lập chứng từ, có thể là tên đã sửa tay.

---

## 10. Hành vi tự động fill

Sau khi chọn khách hàng, hệ thống tự động fill:

| Field | Hành vi |
|---|---|
| Mã đối tượng | Gán mã khách hàng |
| Tên đối tượng | Gán tên khách hàng, cho phép sửa |
| Địa chỉ | Gán địa chỉ khách hàng |
| Lý do nộp | Tự sinh theo tên khách hàng |

Mẫu lý do nộp:

```text
Thu tiền của [Tên đối tượng]
```

Ví dụ:

```text
Thu tiền của Công ty ABC
```

---

## 11. Hành vi khi đổi Mã đối tượng

Khi người dùng đã chọn khách hàng A, sau đó đổi sang khách hàng B:

- Cập nhật Mã đối tượng.
- Cập nhật Tên đối tượng.
- Cập nhật Địa chỉ.
- Tự sinh lại Lý do nộp nếu lý do nộp chưa bị người dùng sửa tay.
- Nếu người dùng đã sửa tay Lý do nộp thì không ghi đè.

Không cập nhật lại đối tượng/tên đối tượng trên toàn bộ bảng hạch toán nếu bảng đã có nhiều dòng.

---

## 12. Hành vi với bảng hạch toán

Khi chọn Mã đối tượng lần đầu:

- Có thể cập nhật đối tượng/tên đối tượng xuống dòng hạch toán đang khởi tạo.

Khi đổi Mã đối tượng sau khi bảng đã có nhiều dòng:

- Không cập nhật lại toàn bộ bảng hạch toán.
- Không ghi đè dữ liệu người dùng đã nhập trong grid.

Rule:

```text
Không tự động update đối tượng/tên đối tượng trên tất cả dòng hạch toán khi đổi Mã đối tượng.
```

---

## 13. Hành vi khi clear Mã đối tượng

Khi người dùng xóa Mã đối tượng:

Frontend cần clear:

| Field | Hành vi |
|---|---|
| objectId | Clear |
| Mã đối tượng | Clear |
| Tên đối tượng | Clear |
| Địa chỉ | Clear |
| Lý do nộp | Clear nếu đang là giá trị tự sinh |
| Bảng hạch toán | Không tự cập nhật nếu đã có dữ liệu |

Sau đó nếu người dùng lưu, báo lỗi:

```text
Vui lòng chọn đối tượng.
```

---

## 14. Hành vi nhập text nhưng không chọn khách hàng

Nếu người dùng gõ text nhưng chưa chọn khách hàng hợp lệ từ dropdown:

- Không có `objectId`.
- Field chưa hợp lệ.
- Không cho lưu.

Lỗi đề xuất:

```text
Vui lòng chọn đối tượng từ danh sách.
```

Khi bấm **Cất** hoặc **Cất và Thêm**:

- Focus về ô **Mã đối tượng**.
- Hiển thị lỗi tại field.

---

## 15. Loading / Empty / Error state

### 15.1. Loading

```text
Đang tìm kiếm...
```

### 15.2. Không có kết quả

```text
Không tìm thấy khách hàng phù hợp
```

Do icon thêm mới nằm cạnh input, không cần hiển thị nút thêm trong dropdown.

### 15.3. Lỗi API

```text
Không thể tải danh sách khách hàng. Vui lòng thử lại.
```

Không làm mất keyword người dùng đang nhập.

---

## 16. Acceptance Criteria cuối cùng

| Mã AC | Nội dung |
|---|---|
| AC-OBJ-001 | Mã đối tượng là bắt buộc với nghiệp vụ `5. Thu khác` |
| AC-OBJ-002 | Khi focus vào Mã đối tượng và chưa gõ từ khóa, dropdown không mở |
| AC-OBJ-003 | Không hiển thị danh sách gần đây/thường dùng khi field rỗng |
| AC-OBJ-004 | Khi người dùng gõ từ khóa, dropdown mở và tìm kiếm khách hàng |
| AC-OBJ-005 | Dropdown chỉ hiển thị khách hàng active |
| AC-OBJ-006 | Dropdown chỉ lọc loại đối tượng là Khách hàng |
| AC-OBJ-007 | Dropdown hiển thị mã, tên, MST, địa chỉ, điện thoại, loại đối tượng |
| AC-OBJ-008 | Hỗ trợ tìm kiếm theo mã, tên, MST, địa chỉ, điện thoại |
| AC-OBJ-009 | Hỗ trợ tìm kiếm tiếng Việt có dấu và không dấu |
| AC-OBJ-010 | Người dùng chọn được khách hàng bằng chuột |
| AC-OBJ-011 | Người dùng chọn được khách hàng bằng bàn phím |
| AC-OBJ-012 | Arrow Up/Down di chuyển highlight trong danh sách |
| AC-OBJ-013 | Tab khi dropdown đang mở chuyển highlight sang dòng tiếp theo |
| AC-OBJ-014 | Tab ở dòng cuối sẽ vòng lại dòng đầu |
| AC-OBJ-015 | Enter chọn dòng đang highlight |
| AC-OBJ-016 | Nếu nhập đúng mã tuyệt đối và chỉ có một kết quả, Enter tự chọn khách hàng đó |
| AC-OBJ-017 | Esc đóng dropdown và không chọn dữ liệu |
| AC-OBJ-018 | Sau khi chọn khách hàng, hệ thống fill tên đối tượng |
| AC-OBJ-019 | Sau khi chọn khách hàng, hệ thống fill địa chỉ |
| AC-OBJ-020 | Sau khi chọn khách hàng, hệ thống tự sinh lý do nộp |
| AC-OBJ-021 | Sau khi chọn khách hàng, focus chuyển sang Tên đối tượng |
| AC-OBJ-022 | Tên đối tượng cho phép sửa tay sau khi tự fill |
| AC-OBJ-023 | Nếu người dùng nhập text nhưng không chọn khách hàng hợp lệ, không cho lưu |
| AC-OBJ-024 | Khi submit thiếu đối tượng, báo lỗi và focus về Mã đối tượng |
| AC-OBJ-025 | Icon Thêm mới nằm cạnh ô input Mã đối tượng |
| AC-OBJ-026 | Người dùng có thể click hoặc focus icon Thêm mới để mở form thêm mới khách hàng |
| AC-OBJ-027 | Form thêm mới đối tượng mặc định loại đối tượng là Khách hàng |
| AC-OBJ-028 | Sau khi thêm nhanh khách hàng thành công, tự động chọn khách hàng vừa tạo vào phiếu thu |
| AC-OBJ-029 | Khi đổi mã đối tượng, không tự cập nhật lại đối tượng/tên đối tượng trên toàn bộ bảng hạch toán nếu đã có nhiều dòng |
| AC-OBJ-030 | Nếu lý do nộp đã được sửa tay, khi đổi khách hàng không tự ghi đè lý do nộp |
| AC-OBJ-031 | Không hiển thị khách hàng inactive trong dropdown |
| AC-OBJ-032 | Nếu API search lỗi, hiển thị lỗi trong dropdown và không làm mất keyword |
| AC-OBJ-033 | Dropdown phản hồi nhanh, phục vụ nhập liệu bàn phím tốc độ cao |

---

## 17. Gợi ý state frontend sau khi chốt

```ts
type ObjectType = "Customer";

type SelectedCustomer = {
  id: string;
  code: string;
  name: string;
  taxCode?: string;
  address?: string;
  phone?: string;
  objectType: "Customer";
  isActive: boolean;
};

type ObjectCodeFieldState = {
  keyword: string;
  selectedCustomer: SelectedCustomer | null;
  results: SelectedCustomer[];
  isOpen: boolean;
  isLoading: boolean;
  highlightedIndex: number;
  error?: string;
  isDirty: boolean;
};
```

---

## 18. Gợi ý xử lý keyboard cuối cùng

```ts
function handleObjectCodeKeyDown(event: KeyboardEvent) {
  if (!dropdownOpen) return;

  switch (event.key) {
    case "Tab":
      event.preventDefault();
      moveHighlightToNext();
      // Nếu đang ở dòng cuối thì vòng lại dòng đầu
      break;

    case "ArrowDown":
      event.preventDefault();
      moveHighlightToNext();
      break;

    case "ArrowUp":
      event.preventDefault();
      moveHighlightToPrevious();
      break;

    case "Enter":
      event.preventDefault();
      selectHighlightedCustomerOrExactMatch();
      break;

    case "Escape":
      event.preventDefault();
      closeDropdown();
      break;
  }
}
```

---

## 19. Kết luận

Phần **4.2. Mã đối tượng** là một autocomplete nghiệp vụ quan trọng trong màn hình Thêm mới Phiếu thu. Component này cần tối ưu mạnh cho nhập liệu bằng bàn phím, tìm kiếm nhanh, lọc đúng khách hàng active, hỗ trợ thêm nhanh khách hàng và đảm bảo không ghi đè dữ liệu người dùng đã nhập trong bảng hạch toán.
