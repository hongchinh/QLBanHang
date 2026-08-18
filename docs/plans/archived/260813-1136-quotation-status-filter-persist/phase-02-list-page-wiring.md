# Phase 02 — Nối store vào QuotationListPage

**Status:** [x] complete
**Complexity:** M

## Objective

`QuotationListPage` khôi phục filter trạng thái từ `ui-store` khi URL không có `?status=`, và ghi lựa chọn mới vào store mỗi khi user đổi. Thứ tự ưu tiên: URL → store → `DEFAULT_ACTIVE_STATUSES`. Việc khôi phục nằm trong `useMemo` tính `statuses` nên request đầu tiên đã đúng filter và chỉ phát sinh một lần.

## Files

- `frontend/src/pages/quotations/quotation-list-page.test.tsx` (sửa — thêm ca kiểm thử)
- `frontend/src/pages/quotations/quotation-list-page.tsx` (sửa)

## Tasks

Tất cả lệnh chạy từ thư mục `frontend/`. Phase 01 phải hoàn tất trước.

### Task 1 — Chuẩn bị test harness cho file list page

Đây là task dọn đường, chưa sinh code production nên không theo vòng TDD.

1. Mở `frontend/src/pages/quotations/quotation-list-page.test.tsx`. Bổ sung import ở đầu file (giữ nguyên các import cũ):

   ```ts
   import userEvent from '@testing-library/user-event';
   import { waitFor } from '@testing-library/react';
   import { quotationsApi } from '@/features/quotations/api';
   import { useUiStore } from '@/stores/ui-store';
   ```

   `waitFor` thêm vào dòng import sẵn có từ `@testing-library/react` (`render, screen, within`).

2. Sửa `renderPage` để nhận entry của router:

   ```ts
   function renderPage(initialEntries: string[] = ['/quotations']) {
     const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
     return render(
       <QueryClientProvider client={qc}>
         <MemoryRouter initialEntries={initialEntries}>
           <QuotationListPage />
         </MemoryRouter>
       </QueryClientProvider>,
     );
   }
   ```

   Hai test cũ gọi `renderPage()` không tham số nên vẫn chạy như trước.

3. Trong `beforeEach` sẵn có của `describe('QuotationListPage column resizing')`, giữ nguyên phần `useAuthStore.setState(...)`. Không đụng tới nó.

4. Chạy `npm run test -- src/pages/quotations/quotation-list-page.test.tsx`
   Expected: PASS, 2 test cũ vẫn xanh.

5. **Commit** — `git commit -m "test(quotations): parameterize list page render harness"`

### Task 2 — Khôi phục filter đã lưu khi URL không có `status`

1. **Viết test thất bại** — thêm `describe` mới vào cuối `frontend/src/pages/quotations/quotation-list-page.test.tsx`:

   ```ts
   const ADMIN_USER = {
     id: 'u1',
     username: 'tester',
     email: 't@example.com',
     fullName: 'Tester',
     roles: ['ADMIN'],
     permissions: [
       'quotations.create',
       'quotations.update',
       'quotations.print',
       'quotations.view_all',
       'quotations.view_cost',
       'quotations.accounting_confirm',
     ],
   };

   function lastListStatuses() {
     const calls = vi.mocked(quotationsApi.list).mock.calls;
     return calls[calls.length - 1][0].statuses;
   }

   describe('QuotationListPage status filter persistence', () => {
     beforeEach(() => {
       localStorage.clear();
       useUiStore.setState({ sidebarCollapsed: false, quotationStatusFilter: null });
       vi.mocked(quotationsApi.list).mockClear();
       useAuthStore.setState({
         accessToken: 'test-token',
         expiresAt: new Date(Date.now() + 3_600_000).toISOString(),
         user: ADMIN_USER,
       });
     });

     it('uses the default active statuses when nothing is stored', async () => {
       renderPage();
       await screen.findByRole('columnheader', { name: 'Số báo giá' });
       expect(lastListStatuses()).toEqual(['Draft', 'Sent', 'Confirmed', 'AccountingConfirmed']);
     });

     it('restores the stored selection on the very first request', async () => {
       useUiStore.setState({ quotationStatusFilter: ['Confirmed'] });

       renderPage();
       await screen.findByRole('columnheader', { name: 'Số báo giá' });

       expect(vi.mocked(quotationsApi.list)).toHaveBeenCalledTimes(1);
       expect(lastListStatuses()).toEqual(['Confirmed']);
     });

     it('lets an explicit URL status win over the stored selection', async () => {
       useUiStore.setState({ quotationStatusFilter: ['Confirmed'] });

       renderPage(['/quotations?status=Draft']);
       await screen.findByRole('columnheader', { name: 'Số báo giá' });

       expect(lastListStatuses()).toEqual(['Draft']);
     });

     it('falls back to the defaults when the stored selection is not a known status', async () => {
       useUiStore.setState({ quotationStatusFilter: ['Foo'] });

       renderPage();
       await screen.findByRole('columnheader', { name: 'Số báo giá' });

       expect(lastListStatuses()).toEqual(['Draft', 'Sent', 'Confirmed', 'AccountingConfirmed']);
     });

     it('treats a stored empty selection as the defaults', async () => {
       useUiStore.setState({ quotationStatusFilter: [] });

       renderPage();
       await screen.findByRole('columnheader', { name: 'Số báo giá' });

       expect(lastListStatuses()).toEqual(['Draft', 'Sent', 'Confirmed', 'AccountingConfirmed']);
     });
   });
   ```

2. **Chạy test để xác nhận FAIL** — `npm run test -- src/pages/quotations/quotation-list-page.test.tsx`
   Expected: FAIL. Ca `restores the stored selection on the very first request` báo
   `expected [ 'Draft', 'Sent', 'Confirmed', 'AccountingConfirmed' ] to deeply equal [ 'Confirmed' ]`.
   Ba ca còn lại (`defaults`, `URL win`, `garbage`, `empty`) PASS sẵn vì hành vi hiện tại đã đúng — chúng là lưới an toàn chống hồi quy.

3. **Viết implementation tối thiểu** — sửa `frontend/src/pages/quotations/quotation-list-page.tsx`:

   - Thêm import store cạnh import `useAuthStore` sẵn có:
     ```ts
     import { useUiStore } from '@/stores/ui-store';
     ```

   - Thêm helper cấp module, đặt ngay sau khai báo `DEFAULT_ACTIVE_STATUSES` (dòng ~65):
     ```ts
     function parseStatuses(raw: string | readonly string[] | null | undefined): QuotationStatus[] {
       if (!raw) return [];
       const parts = typeof raw === 'string' ? raw.split(',') : raw;
       return parts.filter((s): s is QuotationStatus => VALID_STATUSES.has(s as QuotationStatus));
     }
     ```

   - Trong component, ngay sau các dòng `useAuthStore(...)` (dòng ~307-310), đọc store:
     ```ts
     const savedStatuses = useUiStore((s) => s.quotationStatusFilter);
     const setSavedStatuses = useUiStore((s) => s.setQuotationStatusFilter);
     ```

   - Thay toàn bộ `useMemo` tính `statuses` (hiện ở dòng ~323-331) bằng:
     ```ts
     const statuses = useMemo<QuotationStatus[]>(() => {
       const fromUrl = parseStatuses(statusParam);
       if (fromUrl.length > 0) return fromUrl;
       const fromStore = parseStatuses(savedStatuses);
       if (fromStore.length > 0) return fromStore;
       return [...DEFAULT_ACTIVE_STATUSES];
     }, [statusParam, savedStatuses]);
     ```

   Không thêm `useEffect` nào. Không ghi ngược `?status=` vào URL.

4. **Chạy test để xác nhận PASS** — `npm run test -- src/pages/quotations/quotation-list-page.test.tsx`
   Expected: PASS, 7 test (2 cũ + 5 mới).

5. **Commit** — `git commit -m "feat(quotations): restore saved status filter on list mount"`

### Task 3 — Ghi lựa chọn mới vào store khi user đổi filter

1. **Viết test thất bại** — thêm ca sau vào cuối `describe('QuotationListPage status filter persistence')`:

   ```ts
   it('saves the new selection to the store when the user changes it', async () => {
     const user = userEvent.setup();
     renderPage();
     await screen.findByRole('columnheader', { name: 'Số báo giá' });

     await user.click(screen.getByRole('button', { name: 'Trạng thái' }));
     await user.click(await screen.findByRole('menuitemcheckbox', { name: 'Nháp' }));

     await waitFor(() => {
       expect(useUiStore.getState().quotationStatusFilter).toEqual([
         'Sent',
         'Confirmed',
         'AccountingConfirmed',
       ]);
     });
     await waitFor(() => {
       expect(lastListStatuses()).toEqual(['Sent', 'Confirmed', 'AccountingConfirmed']);
     });
   });
   ```

   Giải thích cho người thực thi: giá trị ban đầu là 4 trạng thái mặc định, nên click "Nháp" là thao tác **bỏ tick** — `MultiSelect.toggle` lọc `Draft` ra và giữ nguyên thứ tự còn lại (`multi-select.tsx:40-45`).

2. **Chạy test để xác nhận FAIL** — `npm run test -- src/pages/quotations/quotation-list-page.test.tsx`
   Expected: FAIL ở `expect(useUiStore.getState().quotationStatusFilter).toEqual([...])` — nhận `null` vì `onChange` chưa ghi store. Assert về `lastListStatuses()` sẽ PASS (URL param vẫn hoạt động như cũ).

3. **Viết implementation tối thiểu** — trong `frontend/src/pages/quotations/quotation-list-page.tsx`, sửa `onChange` của `MultiSelect<QuotationStatus>` "Trạng thái" (hiện ở dòng ~623-626):

   ```tsx
   onChange={(next) => {
     setStatusParam(next.join(','));
     setSavedStatuses(next);
     if (page !== 1) setPage(1);
   }}
   ```

   Chỉ sửa MultiSelect "Trạng thái". MultiSelect "Chủ sở hữu" ngay bên dưới giữ nguyên.

4. **Chạy test để xác nhận PASS** — `npm run test -- src/pages/quotations/quotation-list-page.test.tsx`
   Expected: PASS, 8 test.

5. **Commit** — `git commit -m "feat(quotations): save status filter selection to ui-store"`

### Task 4 — Kiểm tra toàn bộ và nghiệm thu thủ công

1. Chạy `npm run test` → toàn bộ suite PASS, không có test cũ nào vỡ.
2. Chạy `npm run typecheck` → không lỗi.
3. Chạy `npm run lint` → không lỗi mới.
4. Chạy `npm run dev` (backend đang chạy) và thực hiện 4 kịch bản nghiệm thu thủ công trong `SUMMARY.md` mục **Final Verification**. Ghi lại kết quả từng bước.
5. **Commit** nếu có sửa nhỏ phát sinh từ bước 1-4 — `git commit -m "fix(quotations): <mô tả cụ thể>"`. Nếu không có gì phải sửa thì bỏ qua bước commit này.

## Verification

Chạy từ `frontend/`:

- `npm run test -- src/pages/quotations/quotation-list-page.test.tsx` → PASS, 8 test.
- `npm run test` → toàn bộ suite PASS.
- `npm run typecheck` → không lỗi.
- `npm run lint` → không lỗi mới.

## Exit Criteria

- `statuses` được tính theo thứ tự URL → store → `DEFAULT_ACTIVE_STATUSES`, toàn bộ trong `useMemo`; file không có `useEffect` mới nào cho việc này.
- `quotationsApi.list` chỉ được gọi một lần ở lần render đầu khi khôi phục từ store (có test khóa).
- MultiSelect "Trạng thái" ghi cả URL param lẫn `ui-store`; các filter khác không bị đụng tới.
- Giá trị lưu không hợp lệ hoặc rỗng đều rơi về mặc định.
- Bốn kịch bản nghiệm thu thủ công đều đạt.
