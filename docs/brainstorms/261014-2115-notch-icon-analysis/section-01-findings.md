# Phát hiện và số đo

> Thuộc [SUMMARY.md](SUMMARY.md). Số liệu đo ngày 2026-10-14 trên canvas bản 1791966097-e8e5 và mã nguồn `main` tại `8135477`.

## Phương pháp

- **Icon Notch:** trích 40 icon từ `project/Library.dc.html`. Thêm `stockin`/`stockout` lấy từ `project/RedesignDashboard.dc.html`, vì hai icon này chỉ có trong artboard đề xuất.
- **Khe nét–khối:** Edge headless lấy mẫu đường tâm của path bằng `getPointAtLength`, bước 0,005 đơn vị lưới.
  - Khe tính từ tâm = khoảng cách nhỏ nhất từ đường tâm tới khối 4×4 bo góc 1.
  - Khe tính từ mép ở cỡ N = (khe từ tâm − nửa độ dày nét) × N/24.
  - Độ dày nét theo bảng của canvas: 16px nét 2 · 20px nét 1,85 · 24px nét 1,75.
  - Vì đầu nét và góc nối đều bo tròn, cách tính này đúng cả ở đầu nét.
- **Ảnh render:** Edge headless, `--force-device-scale-factor=1`, phóng to bằng nearest-neighbour để không làm mịn.
- **Màu:** tỷ lệ tương phản theo WCAG 2.x. Mù màu đỏ (protan) và mù màu lục (deutan) mô phỏng bằng ma trận Machado 2009, mức 1,0, trên RGB tuyến tính.
- **Icon lucide đang dùng:** quét mọi `import {…} from 'lucide-react'` trong `frontend/src`, kể cả import nhiều dòng, bỏ file test.
  - Lượt dùng = số `<Icon` trong JSX cộng số `icon: Icon` trong file cấu hình, tối thiểu 1 cho mỗi file có import.
  - Phân loại bằng tay theo nghĩa nghiệp vụ. Bảng phân loại nằm trong [tools/coverage.py](tools/coverage.py).

## 1. Độ phủ

| Nhóm | Số icon | Lượt dùng | % |
|---|---:|---:|---:|
| Khớp nghĩa với một icon Notch | 26 | 130 | 45% |
| Dùng tạm: icon Notch lệch nghĩa, hoặc nhiều icon lucide dồn về một icon Notch | 18 | 22 | 8% |
| Tiện ích (chevron, spinner, mũi tên, menu…): nên giữ trơn, không có khối | 13 | 49 | 17% |
| Thiếu hẳn | 40 | 90 | 31% |
| **Tổng** | **97** | **291** | |

**Sidebar** (`nav-config.ts`, 48 icon nhóm và mục):

| | Số icon | Icon (nghĩa trong sidebar) |
|---|---:|---|
| Khớp | 13 | ShoppingCart (Bán hàng), Warehouse (Kho), Wallet (Thu chi), BarChart3 (Báo cáo), SlidersHorizontal (Cấu hình), FileText (Báo giá), Users (Khách hàng), ArrowUpFromLine (Xuất kho), TrendingUp (Doanh thu), UserRound (CN theo NV), CreditCard (HTTT), ShieldCheck (Phân quyền), Users2 (Người dùng) |
| Dùng tạm | 15 | Scale (Công nợ), Library (Danh mục), ArrowDownToLine (Nhập kho), ClipboardList (Tồn đầu kỳ), HandCoins (Phiếu thu), Link2 (Đối trừ), History (Nợ đầu kỳ), Presentation (Điều hành), Boxes (Tồn kho), AlarmClock (Nợ quá hạn), Package (Hàng hóa), Container (Kho hàng), ListChecks (Lý do NX), Settings (Hệ thống), UserCog (Cài đặt của tôi) |
| Thiếu | 20 | QrCode (QR thanh toán), Banknote (Phiếu chi), ArrowLeftRight (Chuyển quỹ), Combine (Bù trừ), Gauge (Hạn mức), BadgeDollarSign (DT sale), CarFront (DT xe), Target (Hiệu suất), ScrollText (Thẻ kho), Sigma (NXT), PiggyBank (Sổ quỹ), BookOpen (Số dư CN), BookText (Sổ chi tiết CN), Files (CN theo CT), Hourglass (Tuổi nợ), FileCheck (Biên bản), Tag (Nhóm hàng), Truck (NCC), Landmark (Quỹ), ListOrdered (Lý do TC) |

**Ngoài sidebar:**
- 8 thao tác dùng nhiều đang thiếu: Save (8 file), Ban (8), FileSpreadsheet (3), Printer (3), RotateCcw (5), AlertTriangle (4), Copy (3), Send (3).
- 12 icon thiếu khác: LayoutList, Star, ArrowRightLeft, Eraser, Sparkles, Calculator, Building2, Link2Off, SearchX, Undo2, Hash, Keyboard.

**Icon Notch chưa có chỗ dùng (10):** home, help, pie, database, kanban, hr, mail, message, filter, sort.

### Bảng phân loại đầy đủ

| Icon lucide | Số file | Lượt dùng | Phân loại | Icon Notch | Ghi chú |
|---|---:|---:|---|---|---|
| `Plus` | 24 | 24 | Khớp | `plus` |  |
| `Trash2` | 21 | 22 | Khớp | `trash` |  |
| `Search` | 19 | 21 | Khớp | `search` |  |
| `X` | 15 | 15 | Khớp | `close` |  |
| `Pencil` | 14 | 14 | Khớp | `edit` |  |
| `CheckCircle2` | 3 | 5 | Khớp | `approve` | Xác nhận báo giá |
| `FileText` | 3 | 4 | Khớp | `document` | Báo giá, PDF, logo |
| `LayoutDashboard` | 2 | 3 | Khớp | `dashboard` |  |
| `CirclePlus` | 2 | 2 | Khớp | `plus` |  |
| `SlidersHorizontal` | 2 | 2 | Khớp | `settings` | Cấu hình; Notch `settings` vẽ thanh trượt |
| `Users2` | 2 | 2 | Khớp | `team` | Người dùng |
| `Wallet` | 2 | 2 | Khớp | `wallet` | Thu chi |
| `ArrowUpFromLine` | 1 | 1 | Khớp | `upload` | Xuất kho |
| `BarChart3` | 1 | 1 | Khớp | `reports` |  |
| `Bell` | 1 | 1 | Khớp | `bell` |  |
| `CalendarDays` | 1 | 1 | Khớp | `calendar` |  |
| `Clock` | 1 | 1 | Khớp | `clock` |  |
| `CreditCard` | 1 | 1 | Khớp | `card` | HTTT |
| `Lock` | 1 | 1 | Khớp | `lock` |  |
| `ReceiptText` | 1 | 1 | Khớp | `invoice` |  |
| `ShieldCheck` | 1 | 1 | Khớp | `key` | Phân quyền |
| `ShoppingCart` | 1 | 1 | Khớp | `cart` | Bán hàng |
| `TrendingUp` | 1 | 1 | Khớp | `trend` | Doanh thu |
| `UserRound` | 1 | 1 | Khớp | `user` |  |
| `Users` | 1 | 1 | Khớp | `customer` | Khách hàng |
| `Warehouse` | 1 | 1 | Khớp | `warehouse` | Kho |
| `BadgeCheck` | 2 | 2 | Dùng tạm | `approve` | trùng với CheckCircle2 |
| `HandCoins` | 2 | 2 | Dùng tạm | `invoice` | Phiếu thu, trùng với ReceiptText |
| `Settings` | 2 | 2 | Dùng tạm | `settings` | Hệ thống; bánh răng, Notch chỉ có thanh trượt |
| `UserCog` | 2 | 2 | Dùng tạm | `user` | trùng với UserRound |
| `AlarmClock` | 1 | 1 | Dùng tạm | `clock` | Nợ quá hạn, trùng với Clock |
| `ArrowDownToLine` | 1 | 1 | Dùng tạm | `stockin` | chỉ có trong artboard đề xuất |
| `Boxes` | 1 | 1 | Dùng tạm | `warehouse` | Tồn kho, trùng |
| `ClipboardList` | 1 | 1 | Dùng tạm | `table` | Tồn đầu kỳ |
| `Container` | 1 | 1 | Dùng tạm | `warehouse` | Kho hàng, trùng |
| `History` | 1 | 1 | Dùng tạm | `clock` | Nợ đầu kỳ, trùng với Clock |
| `Library` | 1 | 1 | Dùng tạm | `folder` | Danh mục; Notch gọi là Dự án |
| `Link2` | 1 | 1 | Dùng tạm | `link` | Đối trừ; Notch gọi là Tích hợp |
| `ListChecks` | 1 | 1 | Dùng tạm | `tasks` | Lý do NX |
| `Package` | 1 | 1 | Dùng tạm | `warehouse` | Hàng hóa; glyph warehouse của Notch là hình hộp |
| `Presentation` | 1 | 1 | Dùng tạm | `dashboard` | Điều hành, trùng |
| `Scale` | 1 | 1 | Dùng tạm | `contract` | Công nợ; Notch gọi là Hợp đồng |
| `Settings2` | 1 | 1 | Dùng tạm | `settings` | trùng với SlidersHorizontal |
| `UserPlus` | 1 | 1 | Dùng tạm | `team` | trùng với Users2 |
| `Loader2` | 12 | 19 | Tiện ích | — | spinner |
| `ArrowLeft` | 8 | 9 | Tiện ích | — | quay lại |
| `ChevronDown` | 5 | 6 | Tiện ích | — |  |
| `Check` | 3 | 3 | Tiện ích | — | dấu tick trong select |
| `MoreHorizontal` | 3 | 3 | Tiện ích | — |  |
| `ChevronRight` | 2 | 2 | Tiện ích | — |  |
| `ArrowDown` | 1 | 1 | Tiện ích | — | sort |
| `ArrowUp` | 1 | 1 | Tiện ích | — | sort |
| `Circle` | 1 | 1 | Tiện ích | — | radio |
| `Menu` | 1 | 1 | Tiện ích | — |  |
| `MoreVertical` | 1 | 1 | Tiện ích | — |  |
| `PanelLeftClose` | 1 | 1 | Tiện ích | — |  |
| `PanelLeftOpen` | 1 | 1 | Tiện ích | — |  |
| `Save` | 8 | 11 | Thiếu | — |  |
| `Ban` | 8 | 10 | Thiếu | — |  |
| `FileSpreadsheet` | 3 | 6 | Thiếu | — |  |
| `Printer` | 3 | 6 | Thiếu | — |  |
| `RotateCcw` | 5 | 5 | Thiếu | — |  |
| `AlertTriangle` | 4 | 4 | Thiếu | — |  |
| `Copy` | 3 | 4 | Thiếu | — |  |
| `Send` | 3 | 4 | Thiếu | — |  |
| `ArrowRightLeft` | 2 | 2 | Thiếu | — |  |
| `Building2` | 1 | 2 | Thiếu | — |  |
| `Calculator` | 2 | 2 | Thiếu | — |  |
| `Eraser` | 2 | 2 | Thiếu | — |  |
| `LayoutList` | 2 | 2 | Thiếu | — |  |
| `QrCode` | 2 | 2 | Thiếu | — |  |
| `Sparkles` | 2 | 2 | Thiếu | — |  |
| `Star` | 2 | 2 | Thiếu | — |  |
| `ArrowLeftRight` | 1 | 1 | Thiếu | — |  |
| `BadgeDollarSign` | 1 | 1 | Thiếu | — |  |
| `Banknote` | 1 | 1 | Thiếu | — |  |
| `BookOpen` | 1 | 1 | Thiếu | — |  |
| `BookText` | 1 | 1 | Thiếu | — |  |
| `CarFront` | 1 | 1 | Thiếu | — |  |
| `Combine` | 1 | 1 | Thiếu | — |  |
| `FileCheck` | 1 | 1 | Thiếu | — |  |
| `Files` | 1 | 1 | Thiếu | — |  |
| `Gauge` | 1 | 1 | Thiếu | — |  |
| `Hash` | 1 | 1 | Thiếu | — |  |
| `Hourglass` | 1 | 1 | Thiếu | — |  |
| `Keyboard` | 1 | 1 | Thiếu | — |  |
| `Landmark` | 1 | 1 | Thiếu | — |  |
| `Link2Off` | 1 | 1 | Thiếu | — |  |
| `ListOrdered` | 1 | 1 | Thiếu | — |  |
| `PiggyBank` | 1 | 1 | Thiếu | — |  |
| `ScrollText` | 1 | 1 | Thiếu | — |  |
| `SearchX` | 1 | 1 | Thiếu | — |  |
| `Sigma` | 1 | 1 | Thiếu | — |  |
| `Tag` | 1 | 1 | Thiếu | — |  |
| `Target` | 1 | 1 | Thiếu | — |  |
| `Truck` | 1 | 1 | Thiếu | — |  |
| `Undo2` | 1 | 1 | Thiếu | — |  |

## 2. Xung đột với quy ước hiện tại

1. **Màu theo hành động.** Code hiện tại khớp phương án 1 trong mockup [260519](../260519-2239-quotation-form-icon-color-options.html); brainstorm đó không ghi lựa chọn, nên đây là quy ước thực tế chứ chưa phải quyết định đã ghi.
   - Icon tô màu theo hành động: thêm = cyan-600, sửa = blue-600, xóa/hủy = red-600, khôi phục = emerald-600, in = indigo-600, Excel = emerald-700, nhân bản = violet-600, quay lại = slate-500.
   - Quy tắc Notch nói "nét luôn là Ink hoặc Paper, màu nhấn chỉ ở khối", và ghi rõ "không nên tô cả nét bằng màu Signal".
   - Nếu theo Notch, ý nghĩa nguy hiểm của Hủy phiếu và Xóa phải chuyển sang chữ nút đỏ (xem [mockup-02](mockup-02-stock-out.html), chế độ Bước 2).
2. **Sidebar style C** ([261012](../261012-2135-sidebar-navigation-redesign/section-01-ui-design.md)): ô màu theo nhóm, icon trắng. Khối Signal đặt trên các ô này gần như biến mất:

   | Nhóm | Màu ô | Signal trên ô | Chữ trắng trên ô |
   |---|---|---:|---:|
   | Bán hàng | `#2563EB` | 1,66:1 | 5,17:1 |
   | Kho | `#D97706` | 1,02:1 | 3,19:1 |
   | Thu chi | `#059669` | 1,21:1 | 3,77:1 |
   | Công nợ | `#DC2626` | 1,55:1 | 4,83:1 |
   | Báo cáo | `#7C3AED` | 1,83:1 | 5,70:1 |
   | Danh mục | `#0891B2` | 1,18:1 | 3,68:1 |
   | Hệ thống | `#4A5668` | 2,39:1 | 7,44:1 |

3. **Các nền xanh đặc khác:**
   - Header `hsl(212 78% 36%)`: Signal chỉ đạt khoảng 2,3:1.
   - Mục sidebar đang mở (`bg-primary`, `#2563EB`): 1,66:1.
   - Vì vậy ở bước 2, header phải hiện icon không có khối, còn mục đang mở phải đổi sang nền trắng có vạch trái.
4. **Tính duy nhất của icon sidebar:** `nav-config.test.ts` kiểm tra mỗi icon mục là duy nhất và không dùng lại icon nhóm. Nếu dồn về 40 icon Notch thì test này fail: Hàng hóa, Tồn kho, Kho hàng và nhóm Kho đều thành `warehouse`; Nợ quá hạn và Nợ đầu kỳ đều thành `clock`.
5. **Màu chủ đạo:** app dùng xanh (`--primary` `#2563EB`, theme PWA `#1e40af`), còn Signal mặc định là cam. Canvas có sẵn lựa chọn xanh `#2F5BFF`, nhưng màu này lại trùng với nền xanh nêu ở mục 3.
6. **Dark mode:** đã có token `.dark` nhưng chưa có nút chuyển. Notch có luật cho nền tối (nét Paper, khối Signal). Luật này tương thích nếu nét dùng `currentColor` và khối dùng biến CSS.

## 3. Kiểm tra kỹ thuật

### 3.1 Khe nét–khối

Quy tắc 02 của canvas là "nét không chạm khối, luôn chừa khe 1px". Khi đo từ **mép** nét:
- **18/40 icon** có khe dưới 0,5px ở 16px;
- 10 icon dính hẳn hoặc đè lên khối;
- link và wallet có khối đè lên nét ở mọi cỡ.

Nguyên nhân chung: khe 1 đơn vị được đo từ đường tâm, trong khi riêng nửa độ dày nét đã là 0,875–1 đơn vị. Số âm nghĩa là nét đè lên khối.

| Icon | Nhóm | Khe từ tâm nét (đv lưới) | Khe từ mép @16px | @20px | @24px | Ghi chú |
|---|---|---:|---:|---:|---:|---|
| `link` | system | -2,00 | -2,00 | -2,44 | -2,88 | khối đè lên nét |
| `wallet` | trade | -1,00 | -1,33 | -1,60 | -1,88 | khối đè lên nét |
| `hr` | people | 0,00 | -0,67 | -0,77 | -0,88 | tâm nét chạm khối |
| `clock` | system | 0,00 | -0,67 | -0,77 | -0,87 | tâm nét chạm khối |
| `upload` | action | 0,50 | -0,33 | -0,35 | -0,38 | dính ở 16px |
| `stockin` | redesign | 0,50 | -0,33 | -0,35 | -0,38 | dính ở 16px; chỉ có trong artboard đề xuất |
| `stockout` | redesign | 0,50 | -0,33 | -0,35 | -0,38 | dính ở 16px; chỉ có trong artboard đề xuất |
| `database` | data | 0,70 | -0,20 | -0,19 | -0,18 | dính ở 16px |
| `table` | data | 0,75 | -0,17 | -0,15 | -0,12 | dính ở 16px |
| `plus` | action | 1,00 | 0,00 | 0,06 | 0,12 | dính ở 16px |
| `settings` | nav | 1,00 | 0,00 | 0,06 | 0,13 | dính ở 16px |
| `trend` | data | 1,00 | 0,00 | 0,06 | 0,13 | dính ở 16px |
| `close` | action | 1,12 | 0,08 | 0,16 | 0,25 | < 0,5px ở 16px |
| `document` | data | 1,24 | 0,16 | 0,26 | 0,36 | < 0,5px ở 16px |
| `contract` | trade | 1,24 | 0,16 | 0,26 | 0,36 | < 0,5px ở 16px |
| `message` | people | 1,50 | 0,33 | 0,48 | 0,62 | < 0,5px ở 16px |
| `mail` | people | 1,50 | 0,33 | 0,48 | 0,62 | < 0,5px ở 16px |
| `trash` | action | 1,50 | 0,33 | 0,48 | 0,62 | < 0,5px ở 16px; khối ngoài vùng an toàn |
| `help` | nav | 1,68 | 0,45 | 0,62 | 0,80 | < 0,5px ở 16px |
| `customer` | people | 1,69 | 0,46 | 0,64 | 0,82 | < 0,5px ở 16px |
| `kanban` | data | 2,00 | 0,67 | 0,90 | 1,12 |  |
| `cart` | trade | 2,00 | 0,67 | 0,90 | 1,12 |  |
| `warehouse` | trade | 2,00 | 0,67 | 0,90 | 1,12 |  |
| `card` | trade | 2,00 | 0,67 | 0,90 | 1,12 |  |
| `invoice` | trade | 2,00 | 0,67 | 0,90 | 1,13 |  |
| `tasks` | action | 2,16 | 0,78 | 1,03 | 1,29 |  |
| `bell` | nav | 2,22 | 0,81 | 1,08 | 1,34 | khối ngoài vùng an toàn |
| `user` | people | 2,28 | 0,85 | 1,13 | 1,40 |  |
| `folder` | system | 2,50 | 1,00 | 1,31 | 1,62 |  |
| `key` | system | 2,59 | 1,06 | 1,38 | 1,71 |  |
| `home` | nav | 3,00 | 1,33 | 1,73 | 2,12 |  |
| `calendar` | nav | 3,00 | 1,33 | 1,73 | 2,12 |  |
| `filter` | action | 3,00 | 1,33 | 1,73 | 2,12 |  |
| `lock` | system | 3,00 | 1,33 | 1,73 | 2,12 |  |
| `sort` | action | 3,12 | 1,42 | 1,83 | 2,25 |  |
| `approve` | action | 3,24 | 1,50 | 1,93 | 2,37 |  |
| `pie` | data | 4,00 | 2,00 | 2,56 | 3,12 |  |
| `team` | people | 4,00 | 2,00 | 2,56 | 3,12 |  |
| `search` | nav | 4,59 | 2,39 | 3,05 | 3,71 |  |
| `edit` | action | 4,66 | 2,44 | 3,11 | 3,78 |  |
| `dashboard` | nav | 5,00 | 2,67 | 3,40 | 4,12 |  |
| `reports` | data | 5,08 | 2,72 | 3,46 | 4,21 |  |

![Render thật ở 16px, phóng to 5 lần](notch-16px-zoom.png)

### 3.2 Vùng an toàn

Style guide quy định vùng an toàn 2px, tức nội dung nằm trong khung 20×20. Hai icon có khối nằm ngoài vùng này:
- `bell`: khối ở y = 1–5;
- `trash`: khối ở y = 1,5–5,5.

Mọi path đều nằm trong khung.

### 3.3 Màu và trạng thái

| Cặp màu | Tỷ lệ | Ghi chú |
|---|---:|---|
| Signal trên trắng | 3,12:1 | Vừa đạt mức 3:1 cho thành phần đồ họa (WCAG 1.4.11) |
| Signal trên Paper `#F3F2EE` | 2,78:1 | Không đạt |
| Mute (khối mặc định) trên trắng | 2,69:1 | Không đạt; chỉ chấp nhận được nếu khối mặc định coi là trang trí |
| Cảnh báo `#D92D20` trên trắng | 4,83:1 | Đạt |
| Khối tắt `#4A4E56` trên sidebar tối `#15171C` | 2,15:1 | Gần như vô hình (có chủ ý) |
| Signal và Mute (đang chọn so với mặc định) | 1,16:1 | Hai trạng thái chỉ khác sắc màu |
| Signal và cảnh báo | 1,55:1 | Gần như cùng màu |

Khi mô phỏng mù màu:

| | Mặc định (Mute) | Đang chọn (Signal) | Cảnh báo | Signal so với Mute | Signal so với cảnh báo |
|---|---|---|---|---:|---:|
| Mù màu đỏ (protan) | `#9C9EA6` | `#8B7A11` | `#655A1B` | 1,61:1 | 1,61:1 |
| Mù màu lục (deutan) | `#9B9DA6` | `#B3A012` | `#8F7F14` | 1,02:1 | 1,53:1 |

Hệ quả:
- **"Cảnh báo" không được báo chỉ bằng màu khối.**
- "Đang chọn" phải có thêm tín hiệu khác ngoài màu khối, ví dụ nền, vạch hoặc chữ đậm như sidebar trong mockup.

Ở 16px, khối xám của các mục chưa chọn gần như hòa vào nét có màu `muted-foreground`. Người dùng chỉ thực sự thấy khối ở mục đang mở.

### 3.4 Mâu thuẫn trong canvas

- **Nghĩa của khối:**
  - Artboard "Ứng dụng" ghi "khối chỉ sáng màu Signal ở mục đang mở; các mục khác giữ khối xám".
  - Hai màn đề xuất (Tổng quan, Thêm phiếu nhập kho) lại tô Signal cho mọi icon KPI, thao tác nhanh, ô tìm kiếm và nút chính. Màn Tổng quan có hơn 10 khối Signal cùng lúc.
- **Góc khung:** quy tắc "khung chữ nhật không bo", nhưng `wallet` có góc bo r = 2.
- **Icon vẽ rời, chưa có trong thư viện và chưa có luật về khối:** `stockin`, chi nhánh (hình nhà), thu gọn menu, chevron, mũi tên quay lại, nút đóng tab.
- **Cỡ ngoài bảng:** màn đề xuất dùng 18px và 22px với nét 1,85, trong khi bảng cỡ chỉ có 16, 20, 24, 32 và 48px.
- **Tên icon không khớp hình vẽ hoặc bị dùng sai nghĩa:**
  - `settings` vẽ thanh trượt;
  - `warehouse` vẽ hình hộp hàng;
  - `upload` bị dùng lại cho "Xuất kho";
  - `contract` được dùng cho "Công nợ", `invoice` cho "Phiếu thu".

### 3.5 Bất nhất hiện có trong QLBanHang

Phần này không phụ thuộc Notch. Bước 1 của phương án D sửa các điểm sau:

- **Thêm:** `Plus` thường có màu cyan, nhưng không màu ở `roles-matrix-page`, `customer-autocomplete`, `opening-stock-page`. Lịch sử hoạt động lại dùng `CirclePlus`.
- **`RotateCcw` mang hai nghĩa:** "Khôi phục" (màu emerald) ở các form phiếu, nhưng "Hủy" (không màu) ở `roles-matrix-page.tsx:206`.
- **Đóng (`X`):** màu slate-500 ở `cash-voucher-form`, đỏ ở `recalc-cost`, không màu ở nơi khác. Có 3 cỡ: h-3, h-4, h-5.
- **Màu không thống nhất:**
  - `Save` không màu trong form, nhưng xanh ở lịch sử hoạt động và form báo giá.
  - `Search` dùng `slate-500` ở 11 chỗ và `muted-foreground` ở 7 chỗ.
  - `Trash2` không màu ở `opening-stock-page`.
- **Spinner:** `Loader2` có lúc xanh, lúc slate-400, lúc không màu. Form lại dùng `ButtonLoader` (8 file).
- **Excel:** `revenue-page` có nút xuất Excel nhưng không có icon, trong khi nút TT133 dùng `FileSpreadsheet`.
- **Một icon mang nhiều nghĩa:**
  - `FileText` là logo dự phòng, Báo giá và xuất PDF.
  - `HandCoins` là "Phiếu thu" và "Cấu hình tiền & công nợ".
  - `SlidersHorizontal` là "Cấu hình hệ thống" và "Cấu hình kho".
  - `Building2` dùng cho 2 thẻ cài đặt.
- **Ký tự làm icon:**
  - `✕` ở `App.tsx:774`;
  - `▲▼` ở `stat-tile.tsx`, `format.ts`, `kpi-row.tsx`;
  - `▾` ở `range-picker.tsx`, `quotation-date-filter.tsx`;
  - `← Trước / Sau →` ở danh sách khách hàng và hàng hóa;
  - `→` ở các tab dashboard điều hành;
  - `←` ở `quotation-form-page.tsx`.
- **Không có registry chung:** 3 map icon cục bộ (`nav-config.ts`, `ACTIVITY_ICONS`, `settings-hub-page.tsx`) và màu Tailwind thô trên từng icon (red-600: 34, cyan-600: 26, blue-600: 24, slate-500: 22…).
- **Icon PWA:** `public/icons/icon-192.png` và `icon-512.png` không được tham chiếu ở đâu. Manifest lấy icon từ `/api/settings/branding/icon/{192|512}`. Đây là việc nhỏ ngoài phạm vi, ghi lại để dọn sau.
