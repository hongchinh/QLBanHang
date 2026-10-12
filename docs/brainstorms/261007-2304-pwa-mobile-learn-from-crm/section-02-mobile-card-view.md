# Section 02 — Card view trên mobile (Approach A)

## Hiện trạng

- 12 trang dùng TanStack Table và tự render `<Table>` của shadcn (`components/ui/table.tsx`); không có DataTable dùng chung.
- Báo giá (`pages/quotations/quotation-list-page.tsx`) có `TableColGroup`, header dính `sticky top-0`, cột thao tác dính `sticky right-0`, footer tổng.
- Trên < 768px, bảng cuộn ngang (`overflow-x-auto` / `min-w-max`).

Tham chiếu CRM: `MudTable Breakpoint="Breakpoint.Sm"` tự xếp hàng thành thẻ (Index, Customers, CustomerCare, BaoCao).

## Phạm vi

Báo giá, Khách hàng, Hàng hóa, Phiếu nhập/xuất. Các trang admin/danh mục giữ bảng.

## Thiết kế

### `frontend/src/components/data/responsive-table.tsx` (mới)

```tsx
interface ResponsiveTableProps<T> {
  table: Table<T>                  // instance TanStack của trang
  renderCard: (row: Row<T>) => ReactNode
  isLoading?: boolean
  emptyText: string
  summary?: ReactNode              // thẻ tóm tắt trên đầu danh sách (vd. tổng báo giá)
  children: ReactNode              // <Table> hiện tại của trang, giữ nguyên
}
```

- `md+`: `<div className="hidden md:block">{children}</div>`.
- `< md`: `<div className="md:hidden space-y-2">` → `summary` → `table.getRowModel().rows.map(renderCard)`; trạng thái loading/empty dùng cùng câu chữ với bảng.
- Không đổi query, state, phân trang, bộ lọc của trang — tất cả đi qua cùng `table`.
- Ẩn/hiện bằng CSS (đúng quy ước hiện tại, không `useMediaQuery`).

### Quy ước thẻ

- `Card` của shadcn; toàn bộ thẻ là `<Link>` tới trang chi tiết/sửa.
- Thao tác (sửa, xóa, in, gửi Zalo…) gom vào `DropdownMenu` ⋮ góc phải, nút ≥ 44px, `stopPropagation` để không kích hoạt link.
- Số tiền căn phải, `tabular-nums` (theo `docs/code-standard/conventions.md`).
- Trạng thái dùng `Badge` hiện có (cùng màu với cột trạng thái trên bảng).

### Nội dung thẻ (đề xuất — chốt khi lập plan)

| Trang | Dòng 1 (tiêu đề) | Dòng 2 | Góc phải | Phụ |
| --- | --- | --- | --- | --- |
| Báo giá | Số báo giá + Badge trạng thái | Tên khách hàng | Tổng tiền | Ngày, NV phụ trách |
| Khách hàng | Tên khách hàng | SĐT · Mã | — | Địa chỉ (1 dòng, truncate) |
| Hàng hóa | Tên hàng | Mã · Nhóm hàng | Giá bán | ĐVT |
| Phiếu nhập/xuất | Số phiếu + loại (Nhập/Xuất) | Đối tượng (KH/NCC) · Kho | Tổng tiền | Ngày, trạng thái |

Báo giá: footer tổng hiện thành thẻ `summary` phía trên danh sách.

## Kiểm thử

- `responsive-table.test.tsx`: render đúng số thẻ theo `getRowModel()`; empty/loading; cả 2 khối có trong DOM với class `hidden md:block` / `md:hidden`.
- Test hiện có của các list page phải vẫn pass.
- Thủ công ở 375px và 768px: chuyển bảng ↔ thẻ; lọc/phân trang/tổng đúng; menu ⋮ chạm được, không mở nhầm link.
