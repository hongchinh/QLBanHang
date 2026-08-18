# Lưu filter trạng thái của danh sách báo giá

**Created:** 2026-08-13 11:36:22
**Source:** brainstorm session (hướng C — zustand `ui-store` + persist)

## Goal

Danh sách báo giá ([frontend/src/pages/quotations/quotation-list-page.tsx](../../../frontend/src/pages/quotations/quotation-list-page.tsx)) hiện chỉ giữ filter trạng thái trong URL query `?status=`. Khi user rời trang rồi vào lại từ menu (URL sạch), lựa chọn mất và rơi về `DEFAULT_ACTIVE_STATUSES`. Kế hoạch này thêm một tầng ghi nhớ lâu dài bằng `ui-store` (zustand + persist → localStorage): lựa chọn trạng thái gần nhất được lưu, và được dùng làm giá trị khôi phục khi URL không có `status`. URL vẫn là nguồn sự thật cho truy vấn đang chạy, nên deep-link `?status=` vẫn thắng.

## Scope

**In scope**

- Thêm `quotationStatusFilter: string[] | null` + setter vào [frontend/src/stores/ui-store.ts](../../../frontend/src/stores/ui-store.ts), đưa vào `partialize`.
- Sửa cách tính `statuses` trong `QuotationListPage` để đọc theo thứ tự ưu tiên: URL → store → mặc định.
- Ghi store khi user đổi lựa chọn ở MultiSelect "Trạng thái".
- Test: file mới `frontend/src/stores/ui-store.test.ts`; bổ sung ca kiểm thử vào `frontend/src/pages/quotations/quotation-list-page.test.tsx`.

**Out of scope**

- Các filter khác của danh sách: tìm kiếm (`q`), chủ sở hữu (`ownerUserIds`), 3 khoảng ngày (`from/to`, `revDateFrom/revDateTo`, `dlvDateFrom/dlvDateTo`), số dòng/trang (`size`), số trang (`page`).
- Backend, API, migration DB — không đổi gì.
- Thay đổi ngữ nghĩa nút "Xóa lọc" trong [multi-select.tsx](../../../frontend/src/components/ui/multi-select.tsx).
- Tách key localStorage theo `userId`, hoặc xóa filter khi logout.
- Feature flag / rollout theo giai đoạn.

## Assumptions

- Store lưu `string[]` chứ không phải `QuotationStatus[]`: giữ `ui-store` (tầng global UI) không phụ thuộc type của feature quotations. Việc validate do trang list làm, tái dùng `VALID_STATUSES` sẵn có.
- Không cần `migrate` khi thêm key mới vào `partialize`: zustand persist mặc định shallow-merge state đã lưu lên state khởi tạo, nên bản ghi cũ (chỉ có `sidebarCollapsed`) rehydrate xong sẽ để `quotationStatusFilter` ở giá trị khởi tạo `null`. Giữ `version: 1`.
- Giữ nguyên ngữ nghĩa hiện tại: lựa chọn rỗng (`[]`) được diễn giải thành `DEFAULT_ACTIVE_STATUSES`. Lưu `[]` vào store là hợp lệ và vẫn cho ra mặc định.
- Giá trị trong localStorage có thể là rác (user sửa tay, hoặc `STATUS_OPTIONS` đổi về sau). Sau khi lọc qua `VALID_STATUSES` mà rỗng thì coi như chưa lưu → dùng mặc định.
- Key `qldonhang-ui-store` không gắn `userId`; nhiều tài khoản trên cùng trình duyệt sẽ dùng chung filter. Đây là quyết định đã chốt — dữ liệu chỉ là danh sách trạng thái công khai, không nhạy cảm.
- Việc khôi phục nằm **trong `useMemo` tính `statuses`**, không phải trong `useEffect` ghi ngược vào URL. Đây là ràng buộc thiết kế: nhờ vậy request đầu tiên đã mang đúng filter (không có cú fetch thừa với giá trị mặc định) và URL không tự mọc thêm param.

## Risks

- **Fetch hai lần nếu làm sai chỗ khôi phục.** Nếu người thực thi chuyển logic sang `useEffect`, trang sẽ fetch một lần với mặc định rồi fetch lại. Phase 02 có test khóa hành vi này (`quotationsApi.list` được gọi đúng 1 lần).
- **Radix DropdownMenu trong jsdom.** MultiSelect dựng trên Radix; tương tác cần `userEvent` từ `@testing-library/user-event` (đã có sẵn trong devDependencies, dùng ở `payment-qr-page.test.tsx`, `customer-autocomplete.test.tsx`). `src/test/setup.ts` đã stub Pointer Capture API cho Radix.
- **Rò state giữa các test.** `ui-store` là singleton module-level. Mọi test đụng tới store phải reset state và `localStorage` trong `beforeEach`, nếu không thứ tự chạy sẽ làm test lung lay.
- **`useUiStore.persist.rehydrate()` merge lên state hiện tại, không phải state khởi tạo.** Test rehydrate phải `setState` về giá trị khởi tạo trước khi gọi.

## Phases

- [x] Phase 01 — Thêm `quotationStatusFilter` vào ui-store (S) — `phase-01-ui-store-persist.md`
- [x] Phase 02 — Nối store vào QuotationListPage (M) — `phase-02-list-page-wiring.md`

## Final Verification

Chạy từ thư mục `frontend/`:

```bash
npm run test
npm run typecheck
npm run lint
```

Cả ba phải PASS. Đặc biệt kiểm tra không có test nào đang PASS trước đó bị vỡ trong `src/pages/quotations/quotation-list-page.test.tsx` và `src/stores/`.

Nghiệm thu thủ công (chạy `npm run dev` ở `frontend/`, backend đang chạy):

1. Vào **Báo giá** → mở MultiSelect "Trạng thái" → chọn "Đã xác nhận" + "KT xác nhận" → sang **Khách hàng** → quay lại **Báo giá**: nút hiện "Trạng thái (2)", bảng lọc đúng 2 trạng thái đó.
2. Đóng hẳn trình duyệt, mở lại, đăng nhập, vào **Báo giá**: filter vẫn còn.
3. Bấm "Xóa lọc" → rời trang → quay lại: về 4 trạng thái mặc định (Nháp, Đã gửi, Đã xác nhận, KT xác nhận).
4. Dán URL `/quotations?status=Cancelled`: hiện đúng "Đã hủy", ghi đè giá trị đã lưu.

## Rollback / Recovery

Thuần frontend, không có migration DB và không đổi API — hoàn tác chỉ là revert commit:

```bash
git log --oneline -5
git revert <commit-sha>   # hoặc git revert <sha-phase-02> <sha-phase-01>
```

Nếu chỉ muốn tắt hành vi khôi phục mà vẫn giữ store: bỏ nhánh đọc `savedStatuses` trong `useMemo` tính `statuses` ở `quotation-list-page.tsx` — trang trở lại hành vi cũ ngay, dữ liệu đã lưu trong localStorage nằm yên vô hại.
