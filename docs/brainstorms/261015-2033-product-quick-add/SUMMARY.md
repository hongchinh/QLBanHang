# Product quick add from the product code cell

- **Created:** 2026-10-15 20:33:44
- **Status:** design approved by the user; plan [261015-2038-product-quick-add](../../plans/archived/261015-2038-product-quick-add/SUMMARY.md)
- **Mockup:** [mockup-01-quick-add-product.html](mockup-01-quick-add-product.html). Part 1 shows the entry points. Part 2 compares a short form (A) with the full form (B); the user chose **B**.

## Problem

On a stock voucher, the user types a code into the **Mã hàng** cell. If the product is not in the catalog, the only way out is to leave the voucher, create the product under Danh mục → Hàng hóa, come back and type the code again. The partner field already has a quick add (+, F4, "Thêm mới" row; brainstorm 261015-0644). The product cell has none.

## Goals

- Add a product without leaving the screen, from the product code cell of the stock-in, stock-out, quotation and opening stock screens.
- Once saved, the new product fills the line exactly as a product picked from the list would.
- Make product creation possible for the Warehouse, Accountant and Sales seed roles.

## Non-goals

- Quick add on the stock card and cost recalculation screens. They only look products up.
- Editing a product from the voucher screen.
- A "Thêm mới" button inside `ProductCatalogDialog`. Plan 260528-1833, never executed, covered that. It is not needed once the cell itself has quick add.
- A separate permission for creating product groups (see D5).

## Decisions

| # | Decision | Choice |
|---|---|---|
| D1 | Screens | Stock in, stock out, quotation, opening stock (`allowQuickAdd`). Stock card and cost recalculation stay off. |
| D2 | Permission | Seed grants `products.create` to Warehouse, Accountant and Sales. |
| D3 | Typed text | Guessed from its shape. No space and no Vietnamese diacritics (`TL-50`) → **Mã**. Otherwise (`Tôn lạnh 0.4`) → **Tên**, and the code is left blank so the server generates it. |
| D4 | Dialog content | Full form (option B): every field of the "Thêm hàng hóa" page except Trạng thái. A new product is always Active. |
| D5 | Side effect of D2 | Accepted. `products.create` also opens `/products/new` and `POST /product-groups` for the three roles. |
| D6 | Approach | Extract the shared `ProductFormFields` out of the product form page and use it in both the page and the dialog. This follows `CustomerFormFields`. |

## Approaches considered

1. **Extract a shared form (chosen).** One set of fields for the catalog page and the dialog, so a later field change lands in both. It costs a refactor of `product-form-page.tsx` (530 lines), and the page's tests must keep passing.
2. **Separate dialog component.** Leaves the catalog page untouched, but the two forms drift apart over time.
3. **Open `/products/new` in a new tab.** Needs no new UI, but the user must switch tabs and retype the code, and the line is not filled.

Form content: **A (short form; essential fields, dimensions only for area/volume pricing, rest folded)** was recommended. The user chose **B (full form)**.

## Behaviour

**Entry points** (only with `products.create` and `allowQuickAdd`):

- No results: the list shows **＋ Thêm mới hàng hóa "…"**, highlighted, so Enter opens the dialog.
- With results: the "Thêm mới" row is the last option. Arrow keys and Tab reach it.
- **F4** in the code cell opens the dialog directly, even when the list is closed.
- Empty text, or results still loading: Enter does not open the dialog. Enter waits for the results, like the partner field.

**Dialog "Thêm nhanh hàng hóa":**

- The typed text is prefilled per D3. Focus goes to the first empty box of Mã/Tên.
- Enter moves to the next field, Shift+Enter back. Enter on the last field saves. Ctrl+Enter saves, Esc cancels.
- Key events do not reach the voucher form underneath (`StopFormEvents`, as in the partner quick add).
- A unit name typed by hand goes through get-or-create, as on the catalog page.

**After saving:**

- Toast "Đã tạo hàng hóa {code}".
- The product goes through the same `commit` path as a pick from the list. The line gets name, unit, specification, dimensions, price, VAT and discounts. Focus moves on per the screen's own rule: the "Cột & phím Enter" settings on stock vouchers, `nextFocusId` on quotations.
- The voucher itself is **not** saved.

**Cancel:** focus returns to the code cell, and the typed text stays.

**Errors:**

- Duplicate code, validation error, network failure or unit creation failure: the dialog stays open, the error shows at the top of the form plus a toast, and the input is kept.
- If loading the groups or units fails, the dialog shows "Không tải được danh mục" with a retry button.

## Components (frontend)

| Unit | Responsibility |
|---|---|
| `features/products/components/product-form-fields.tsx` (new) | Fields + Lưu/Hủy, extracted from `ProductFormInner` in `pages/products/product-form-page.tsx`. Props: `initial`, `groups`, `units`, `initialCode`, `initialName`, `showStatusField`, `enterNavigation`, `onSubmit`, `onCancel`, `submitting`, `submitError`, `hasSubmitError`. The catalog page keeps its current look and behaviour; `enterNavigation` is off there. |
| `features/products/use-save-product.ts` (new) | Unit get-or-create, then create or update the product, and refresh the unit list. Moved out of the page's `onSubmit`. Used by the page and the dialog. |
| `features/products/split-typed-text.ts` (new, pure) | `splitTypedProductText(text) → { code?: string; name?: string }` per D3. |
| `features/products/components/product-quick-add-dialog.tsx` (new) | Dialog; the body mounts only while open and loads groups and units then. On save it calls `onCreated(toProductSuggestion(created))`; the existing `toProductSuggestion` already accepts `Product`. The caller wraps it in `StopFormEvents`. |
| `pages/quotations/components/product-typeahead-cell.tsx` (changed) | New prop `allowQuickAdd` (default `false`). The cell checks `products.create` itself. It adds the "Thêm mới" row as the last option, F4, the pending-Enter rule and the dialog. The created product goes through `commit`. |
| Callers | `stock-line-grid.tsx`, `quotations/components/line-items-grid.tsx`, `inventory/opening-stock-page.tsx` pass `allowQuickAdd`. |

## Backend

- `DbSeeder`: add `Permissions.Products.Create` to the Warehouse, Accountant and Sales role definitions, with a comment pointing to this brainstorm.
- The seeder only grants new permission codes to existing roles. On existing databases an admin must enable `products.create` for these roles in the permission matrix. This is a go-live checklist item.
- No API change. `POST /products` already generates a code when it is blank, and `POST /lookups/units` needs only authentication.

## Testing

- `split-typed-text.test.ts`: code, name, blank, surrounding spaces, diacritics, mixed case.
- `product-form-fields.test.tsx`: Enter/Shift+Enter walk, Ctrl+Enter submit, Esc cancel, Trạng thái hidden when `showStatusField` is false.
- `product-form-page.test.tsx`: existing tests keep passing after the extraction.
- `product-quick-add-dialog.test.tsx`: prefill per D3; save calls `onCreated` with the mapped suggestion; on error the dialog stays open and keeps the input; unit get-or-create path.
- `product-typeahead-cell.test.tsx`: the "Thêm mới" row shows only with the permission and `allowQuickAdd`; it is highlighted when there are no results; F4 opens the dialog; Enter waits for pending results; created → `onSelect`; Esc returns focus to the cell.
- `stock-line-grid.test.tsx`: a created product fills the line and focus follows the Enter path.
- Backend integration test: the seeded Warehouse, Accountant and Sales roles hold `products.create`.
- Real-browser check on stock out, quotation and opening stock.
- Help files via the `help-content` skill: `stock-in.form.md`, `stock-out.form.md`, `quotations.form.md`, `inventory-opening-stock.md`.

## Open questions

None. All decisions above were confirmed by the user on 2026-10-15.

## Next steps

1. Write the implementation plan (`write-plan`) from this brainstorm.
2. Add this brainstorm to `docs/SUMMARY.md`.
