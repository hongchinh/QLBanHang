# Phase 04 — Frontend: `CustomerAutocomplete` component + tests

**Status:** [ ] pending
**Complexity:** L

## Objective

Component `CustomerAutocomplete` thực thi đầy đủ BD §4.2 — §18: typeahead debounce 250ms, dropdown table-style 5 cột, keyboard nav (Arrow/Tab cycle/Enter/Esc), exact-match Enter, icon "+" cạnh input. Có component test (vitest + RTL) cover hành vi keyboard quan trọng.

## Files

- (new) `frontend/src/components/customer-autocomplete/customer-autocomplete.tsx` (component chính)
- (new) `frontend/src/components/customer-autocomplete/customer-autocomplete.test.tsx`
- (new) `frontend/src/hooks/use-debounced-value.ts` (nếu chưa có — kiểm tra trước; nhiều khả năng chưa có)

## Component contract

```ts
interface CustomerAutocompleteProps {
  value: { id: string; name: string } | null;
  onSelect: (customer: CustomerSearchItem) => void;
  onClear: () => void;
  onAddNewClick: () => void;
  disabled?: boolean;
  inputId?: string; // for label htmlFor
  inputAriaLabel?: string;
  placeholder?: string;
  errorMessage?: string;
}
```

## Tasks

1. **Hook `useDebouncedValue<T>(value, delayMs)`** (nếu chưa có):
   ```ts
   export function useDebouncedValue<T>(value: T, delayMs: number): T {
     const [debounced, setDebounced] = useState(value);
     useEffect(() => {
       const id = setTimeout(() => setDebounced(value), delayMs);
       return () => clearTimeout(id);
     }, [value, delayMs]);
     return debounced;
   }
   ```

2. **Component structure** (`customer-autocomplete.tsx`):
   - State: `keyword`, `isOpen`, `highlightedIndex`.
   - Khi `value != null` (đã chọn): hiển thị `Input` chứa `value.name` + nút "X" để clear; gõ tiếp → reset, `isOpen=true`.
   - Khi `value == null`: input rỗng/keyword, mở dropdown khi `keyword.length > 0` (BD §2 — focus rỗng KHÔNG mở).
   - `debouncedKeyword = useDebouncedValue(keyword, 250)`.
   - `const { data, isLoading, error } = useCustomersSearch(debouncedKeyword, { activeOnly: true, limit: 20 });`
   - Reset `highlightedIndex = 0` khi `data` thay đổi.
   - Đóng dropdown khi click ngoài (use `useOnClickOutside` or refs).
   - Dropdown layout: relative wrapper, absolute card; table với 5 cột + 1 cột "Loại đối tượng" hiển thị badge "Khách hàng" (BD §6).
   - Empty/Loading/Error states (BD §15):
     - Loading: "Đang tìm kiếm..."
     - Empty: "Không tìm thấy khách hàng phù hợp"
     - Error: "Không thể tải danh sách khách hàng. Vui lòng thử lại." + giữ keyword.
   - Icon "+" cạnh input (bên phải, tách bằng gap): `<Button size="icon" variant="outline" onClick={onAddNewClick} aria-label="Thêm mới khách hàng">` (Lucide `Plus`).

3. **Keyboard handler** (BD §18, áp dụng khi `isOpen`):
   ```ts
   function handleKeyDown(e: KeyboardEvent<HTMLInputElement>) {
     if (!isOpen) {
       if (e.key === 'ArrowDown' && keyword.length > 0) {
         setIsOpen(true);
       }
       return;
     }
     const results = data ?? [];
     switch (e.key) {
       case 'ArrowDown':
         e.preventDefault();
         setHighlightedIndex(i => (i + 1) % Math.max(results.length, 1));
         break;
       case 'ArrowUp':
         e.preventDefault();
         setHighlightedIndex(i => (i - 1 + results.length) % Math.max(results.length, 1));
         break;
       case 'Tab':
         if (results.length === 0) return; // fall through to natural Tab
         e.preventDefault();
         setHighlightedIndex(i =>
           e.shiftKey
             ? (i - 1 + results.length) % results.length
             : (i + 1) % results.length
         );
         break;
       case 'Enter': {
         e.preventDefault();
         // Exact code match takes priority (BD §7.4)
         const upper = keyword.trim().toUpperCase();
         const exact = results.find(r => r.code.toUpperCase() === upper);
         const pick = exact ?? results[highlightedIndex];
         if (pick) selectCustomer(pick);
         break;
       }
       case 'Escape':
         e.preventDefault();
         setIsOpen(false); // giữ keyword (BD §7.5)
         break;
     }
   }
   ```

4. **selectCustomer(c)**:
   - Call `onSelect(c)`.
   - `setKeyword('')`.
   - `setIsOpen(false)`.
   - (Focus next field: handler ở phase 05.)

5. **A11y attrs**:
   - Input: `role="combobox"`, `aria-expanded={isOpen}`, `aria-controls="customer-listbox"`, `aria-autocomplete="list"`, `aria-activedescendant={isOpen && results[i] ? \`customer-option-${results[i].id}\` : undefined}`.
   - Dropdown: `id="customer-listbox"`, `role="listbox"`.
   - Mỗi row: `id="customer-option-${id}"`, `role="option"`, `aria-selected={i === highlightedIndex}`.

6. **Component tests** (`customer-autocomplete.test.tsx`):
   - Setup: mock `useCustomersSearch` (vitest module mock) hoặc mock API client với MSW (kiểm tra repo có MSW chưa; nếu không, mock hook trực tiếp).
   - `it('does not open dropdown when input is focused and empty')` (AC-OBJ-002, 003).
   - `it('opens dropdown and shows results after typing')` — fake timers cho debounce.
   - `it('Arrow Down moves highlight; Arrow Up moves up')` (AC-OBJ-012).
   - `it('Tab cycles highlight forward and wraps at end')` (AC-OBJ-013, 014).
   - `it('Shift+Tab cycles backward')`.
   - `it('Enter selects highlighted row')` (AC-OBJ-015).
   - `it('Enter with exact code match auto-selects even without highlight change')` (AC-OBJ-016).
   - `it('Escape closes dropdown but keeps keyword')` (AC-OBJ-017).
   - `it('Add-new button calls onAddNewClick')` (AC-OBJ-026).
   - `it('renders empty state when no results')` (BD §15.2).

## Verification

```powershell
cd d:\Projects\QLBanHang\frontend
npm run typecheck
npm test -- customer-autocomplete
```

- 10 tests pass.
- Typecheck pass.

## Exit Criteria

- All 10 component tests pass.
- Render check: import component vào storybook hoặc vào quotation form tạm thời, mở dev server `npm run dev`, gõ keyword → thấy dropdown 5 cột render đúng.
- A11y: kiểm tra DOM trên DevTools — input có `role="combobox"`, dropdown có `role="listbox"`, mỗi row có `role="option"`.
