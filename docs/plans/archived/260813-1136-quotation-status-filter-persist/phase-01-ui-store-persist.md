# Phase 01 — Thêm `quotationStatusFilter` vào ui-store

**Status:** [x] complete
**Complexity:** S

## Objective

`ui-store` có thêm state `quotationStatusFilter: string[] | null` cùng setter, được persist xuống localStorage dưới key `qldonhang-ui-store`, và bản ghi cũ (chỉ có `sidebarCollapsed`) vẫn rehydrate được mà không cần `migrate`.

## Files

- `frontend/src/stores/ui-store.test.ts` (tạo mới)
- `frontend/src/stores/ui-store.ts` (sửa)

## Tasks

Tất cả lệnh chạy từ thư mục `frontend/`.

### Task 1 — Setter cập nhật state và ghi xuống localStorage

1. **Viết test thất bại** — tạo `frontend/src/stores/ui-store.test.ts`:

   ```ts
   import { beforeEach, describe, expect, it } from 'vitest';
   import { useUiStore } from './ui-store';

   const STORAGE_KEY = 'qldonhang-ui-store';

   function readPersisted(): { state: Record<string, unknown>; version?: number } | null {
     const raw = localStorage.getItem(STORAGE_KEY);
     return raw ? JSON.parse(raw) : null;
   }

   describe('useUiStore quotationStatusFilter', () => {
     beforeEach(() => {
       localStorage.clear();
       useUiStore.setState({ sidebarCollapsed: false, quotationStatusFilter: null });
     });

     it('starts as null', () => {
       expect(useUiStore.getState().quotationStatusFilter).toBeNull();
     });

     it('setQuotationStatusFilter updates state', () => {
       useUiStore.getState().setQuotationStatusFilter(['Confirmed', 'AccountingConfirmed']);
       expect(useUiStore.getState().quotationStatusFilter).toEqual([
         'Confirmed',
         'AccountingConfirmed',
       ]);
     });

     it('persists the selection to localStorage', () => {
       useUiStore.getState().setQuotationStatusFilter(['Confirmed']);
       expect(readPersisted()?.state.quotationStatusFilter).toEqual(['Confirmed']);
     });

     it('persists an empty selection as an empty array, not null', () => {
       useUiStore.getState().setQuotationStatusFilter([]);
       expect(useUiStore.getState().quotationStatusFilter).toEqual([]);
       expect(readPersisted()?.state.quotationStatusFilter).toEqual([]);
     });
   });
   ```

2. **Chạy test để xác nhận FAIL** — `npm run test -- src/stores/ui-store.test.ts`
   Expected: FAIL. TypeScript/vitest báo `Property 'setQuotationStatusFilter' does not exist on type 'UiState'` (hoặc runtime `TypeError: ...setQuotationStatusFilter is not a function`).

3. **Viết implementation tối thiểu** — sửa `frontend/src/stores/ui-store.ts`:
   - Trong `interface UiState`, thêm hai dòng:
     ```ts
     quotationStatusFilter: string[] | null;
     setQuotationStatusFilter: (next: string[]) => void;
     ```
   - Trong initializer, thêm giá trị khởi tạo và setter:
     ```ts
     quotationStatusFilter: null,
     setQuotationStatusFilter: (next) => set({ quotationStatusFilter: next }),
     ```
   - Trong `partialize`, thêm khóa mới:
     ```ts
     partialize: (state) => ({
       sidebarCollapsed: state.sidebarCollapsed,
       quotationStatusFilter: state.quotationStatusFilter,
     }),
     ```
   - Giữ nguyên `name: 'qldonhang-ui-store'` và `version: 1`. Không thêm `migrate`.

4. **Chạy test để xác nhận PASS** — `npm run test -- src/stores/ui-store.test.ts`
   Expected: PASS, 4/4 test.

5. **Commit** — `git commit -m "feat(ui-store): persist quotation status filter selection"`

### Task 2 — Bản ghi persist cũ vẫn rehydrate được, không cần migrate

1. **Viết test thất bại** — thêm `describe` thứ hai vào cuối `frontend/src/stores/ui-store.test.ts`:

   ```ts
   describe('useUiStore rehydration from a pre-existing persisted state', () => {
     beforeEach(() => {
       localStorage.clear();
       useUiStore.setState({ sidebarCollapsed: false, quotationStatusFilter: null });
     });

     it('keeps quotationStatusFilter null when the stored payload predates it', async () => {
       localStorage.setItem(
         'qldonhang-ui-store',
         JSON.stringify({ state: { sidebarCollapsed: true }, version: 1 }),
       );

       await useUiStore.persist.rehydrate();

       expect(useUiStore.getState().sidebarCollapsed).toBe(true);
       expect(useUiStore.getState().quotationStatusFilter).toBeNull();
     });

     it('restores a stored quotationStatusFilter', async () => {
       localStorage.setItem(
         'qldonhang-ui-store',
         JSON.stringify({
           state: { sidebarCollapsed: false, quotationStatusFilter: ['Confirmed'] },
           version: 1,
         }),
       );

       await useUiStore.persist.rehydrate();

       expect(useUiStore.getState().quotationStatusFilter).toEqual(['Confirmed']);
     });
   });
   ```

   Lưu ý cho người thực thi: `rehydrate()` merge state đã lưu lên **state hiện tại**, không phải state khởi tạo — vì vậy `beforeEach` phải `setState` về giá trị khởi tạo trước, nếu không test sẽ ăn theo kết quả của test chạy trước.

2. **Chạy test để xác nhận FAIL** — `npm run test -- src/stores/ui-store.test.ts`
   Expected: nếu Task 1 đã xong đúng thì hai test này có thể PASS ngay — điều đó xác nhận giả định "không cần `migrate`". Nếu FAIL (ví dụ `quotationStatusFilter` thành `undefined` thay vì `null`), sang bước 3.

3. **Viết implementation tối thiểu** — chỉ làm nếu bước 2 FAIL: thêm `merge` tường minh vào options của `persist` để đảm bảo khóa thiếu lấy giá trị khởi tạo:
   ```ts
   merge: (persisted, current) => ({ ...current, ...(persisted as Partial<UiState>) }),
   ```
   Không đổi `version`, không thêm `migrate`.

4. **Chạy test để xác nhận PASS** — `npm run test -- src/stores/ui-store.test.ts`
   Expected: PASS, 6/6 test.

5. **Commit** — `git commit -m "test(ui-store): cover rehydration of legacy persisted payloads"`
   (Nếu bước 3 phải chạy, đổi message thành `fix(ui-store): merge persisted state over defaults`.)

## Verification

Chạy từ `frontend/`:

- `npm run test -- src/stores/ui-store.test.ts` → PASS, 6 test.
- `npm run typecheck` → không lỗi.
- `npm run lint` → không lỗi mới.

## Exit Criteria

- `frontend/src/stores/ui-store.ts` có `quotationStatusFilter: string[] | null`, setter `setQuotationStatusFilter`, và cả hai khóa trong `partialize`.
- `version` vẫn là `1`, không có hàm `migrate`.
- `frontend/src/stores/ui-store.test.ts` tồn tại và PASS toàn bộ.
- `sidebarCollapsed` cùng các hành vi sidebar cũ không đổi; các test hiện có vẫn PASS.
