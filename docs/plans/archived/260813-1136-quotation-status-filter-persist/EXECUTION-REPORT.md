# Execution Report — Lưu filter trạng thái của danh sách báo giá

**Plan:** `docs/plans/260813-1136-quotation-status-filter-persist/SUMMARY.md`
**Executed:** 2026-08-13
**Mode:** Batch
**Result:** Cả hai phase hoàn tất, verification đạt baseline.

## Phases

| Phase | Trạng thái | Ghi chú |
| --- | --- | --- |
| Phase 01 — Thêm `quotationStatusFilter` vào ui-store | [x] complete | 6/6 test PASS. Không cần `merge` tường minh (xem Deviations). |
| Phase 02 — Nối store vào QuotationListPage | [x] complete | 8/8 test PASS trong file list page. |

## Files changed

| File | Loại | Nội dung |
| --- | --- | --- |
| `frontend/src/stores/ui-store.ts` | sửa | Thêm `quotationStatusFilter: string[] \| null`, setter `setQuotationStatusFilter`, đưa khóa mới vào `partialize`. Giữ `version: 1`, không thêm `migrate`. |
| `frontend/src/stores/ui-store.test.ts` | tạo mới | 6 test: giá trị khởi tạo, setter, persist xuống localStorage, persist `[]`, rehydrate payload cũ, rehydrate payload có filter. |
| `frontend/src/pages/quotations/quotation-list-page.tsx` | sửa | Import `useUiStore`; helper cấp module `parseStatuses`; `useMemo` tính `statuses` theo thứ tự URL → store → `DEFAULT_ACTIVE_STATUSES`; `onChange` của MultiSelect "Trạng thái" ghi thêm `setSavedStatuses(next)`. |
| `frontend/src/pages/quotations/quotation-list-page.test.tsx` | sửa | `renderPage(initialEntries)` nhận tham số router; thêm 6 test cho persistence (5 test khôi phục + 1 test ghi store). |
| `frontend/src/pages/admin/roles-matrix-page.test.tsx` | sửa (ngoài scope, đã được user chấp thuận) | `waitForMatrix()` nhận `{ timeout: 5000 }`. Xem Deviations. |

## Verification

Chạy từ `frontend/`:

| Lệnh | Kết quả |
| --- | --- |
| `npm run test -- src/stores/ui-store.test.ts` | PASS — 6/6 |
| `npm run test -- src/pages/quotations/quotation-list-page.test.tsx` | PASS — 8/8 (2 test cũ + 6 mới) |
| `npm run test` (toàn suite) | 34/37 file PASS, 229/232 test PASS — 3 file FAIL đều là lỗi có trước (xem dưới) |
| `npm run typecheck` | PASS — không lỗi |
| `npm run lint` | 3 error + 14 warning — **giống hệt baseline**, toàn bộ nằm ở file không bị đụng tới (`components/ui/table.tsx`, `features/customers/components/customer-catalog-list.tsx`, `features/products/components/product-catalog-list.tsx`) |

### Vòng TDD đã tuân thủ

Mỗi task production đều xác nhận FAIL trước khi implement:

- Phase 01 Task 1: FAIL với `TypeError: ...setQuotationStatusFilter is not a function` → PASS 4/4.
- Phase 02 Task 2: FAIL đúng như plan dự đoán — `expected ['Draft','Sent','Confirmed','AccountingConfirmed'] to deeply equal ['Confirmed']`, 1 fail / 6 pass → PASS 7/7.
- Phase 02 Task 3: FAIL với `quotationStatusFilter` nhận `null` → PASS 8/8.

### Lỗi test có trước khi thực thi (không liên quan)

Baseline đo bằng `git stash` rồi chạy full suite trên cây sạch — **3 test FAIL sẵn**, không thay đổi sau khi thực thi:

- `src/features/bank-accounts/bank-accounts-tab.test.tsx > adds a new account via the form`
- `src/hooks/useNotificationHub.test.ts > invalidates unread-count query on NewNotification`
- `src/pages/payment-qr/payment-qr-page.test.tsx > generates and displays a QR after submitting valid data`

## Deviations from plan

1. **Phase 01 Task 2 bước 3 (`merge` tường minh) không cần thực hiện.** Plan đã lường trước tình huống này: hai test rehydrate PASS ngay sau khi Task 1 xong, xác nhận giả định "zustand persist shallow-merge nên không cần `migrate`". `version` giữ ở `1`, không có `migrate`, không có `merge`.

2. **Sửa `src/pages/admin/roles-matrix-page.test.tsx` — ngoài Files của plan, đã hỏi và được user chấp thuận.** Thêm test file thứ 37 làm đổi cách vitest xếp lịch chạy song song, khiến `waitFor` mặc định 1000ms trong `waitForMatrix()` (test đầu tiên của file, chịu chi phí cold start) bị timeout khi chạy full suite. Đã chứng minh không liên quan về mặt chức năng:
   - `roles-matrix-page.tsx` và cả thư mục `src/pages/admin/` không tham chiếu `ui-store`/`useUiStore`.
   - File PASS 7/7 khi chạy riêng, và PASS khi chạy cùng `quotation-list-page.test.tsx` + `ui-store.test.ts`.
   - Chạy full suite với toàn bộ code production mới nhưng tạm bỏ `ui-store.test.ts` ra → `roles-matrix` PASS, chỉ còn 3 lỗi baseline. Tức nguyên nhân là số lượng file (tải song song), không phải logic.

   Sửa: bọc `{ timeout: 5000 }` cho `waitFor` sẵn có. Full suite trở về đúng baseline 3 lỗi. Phase 02 Task 4 bước 5 đã dự trù các "sửa nhỏ phát sinh từ bước 1-4".

3. **Chưa commit.** Plan đặt lệnh `git commit` ở cuối mỗi task (5 commit). Việc commit được dồn về cổng xác nhận cuối của skill (`Confirm and Auto commit git`) để user quyết định, thay vì tạo commit rải rác trong lúc thực thi.

4. **Nghiệm thu thủ công (Phase 02 Task 4 bước 4) chưa chạy.** Cần `npm run dev` với backend đang chạy và tương tác trình duyệt thật — nằm ngoài khả năng của phiên thực thi này. 4 kịch bản trong `SUMMARY.md` mục **Final Verification** đều đã có test tự động tương ứng:

   | Kịch bản thủ công | Test tự động khóa hành vi |
   | --- | --- |
   | 1. Chọn 2 trạng thái → rời trang → quay lại | `restores the stored selection on the very first request` + `saves the new selection to the store when the user changes it` |
   | 2. Đóng/mở lại trình duyệt | `persists the selection to localStorage` + `restores a stored quotationStatusFilter` |
   | 3. "Xóa lọc" → rời trang → quay lại | `treats a stored empty selection as the defaults` |
   | 4. Deep-link `?status=Cancelled` | `lets an explicit URL status win over the stored selection` |

## Exit criteria

**Phase 01**

- [x] `ui-store.ts` có `quotationStatusFilter: string[] \| null`, setter, và cả hai khóa trong `partialize`.
- [x] `version` vẫn là `1`, không có `migrate`.
- [x] `ui-store.test.ts` tồn tại, PASS toàn bộ.
- [x] `sidebarCollapsed` và hành vi sidebar không đổi; test cũ vẫn PASS.

**Phase 02**

- [x] `statuses` tính theo URL → store → `DEFAULT_ACTIVE_STATUSES`, toàn bộ trong `useMemo`; không thêm `useEffect` nào.
- [x] `quotationsApi.list` chỉ gọi 1 lần ở render đầu khi khôi phục từ store — có test khóa (`toHaveBeenCalledTimes(1)`).
- [x] MultiSelect "Trạng thái" ghi cả URL param lẫn `ui-store`; MultiSelect "Chủ sở hữu" và các filter khác không bị đụng.
- [x] Giá trị lưu không hợp lệ (`['Foo']`) hoặc rỗng (`[]`) đều rơi về mặc định.
- [ ] Bốn kịch bản nghiệm thu thủ công — chưa chạy, xem Deviations #4.

## Residual risks / follow-ups

1. **Nghiệm thu thủ công còn nợ.** Chạy 4 kịch bản trong `SUMMARY.md` trước khi phát hành.
2. **Ba test FAIL có sẵn** (`bank-accounts-tab`, `useNotificationHub`, `payment-qr-page`) vẫn đỏ — có trước kế hoạch này, không nằm trong scope. Nên mở việc riêng để xử lý.
3. **Test nhạy timing.** `roles-matrix-page` đã được nới timeout, nhưng `bank-accounts-tab` và `payment-qr-page` cũng dùng cùng mẫu `waitFor`/`findByRole` với timeout mặc định — nhóm này có thể còn lung lay khi số file test tiếp tục tăng.
4. **Key localStorage không gắn `userId`** — nhiều tài khoản trên cùng trình duyệt dùng chung filter. Đây là quyết định đã chốt trong plan (dữ liệu không nhạy cảm), ghi lại để tra cứu về sau.
5. **`ui-store` là singleton module-level.** Test nào đụng tới store phải reset `useUiStore.setState(...)` và `localStorage` trong `beforeEach`, nếu không thứ tự chạy sẽ làm test lung lay.
