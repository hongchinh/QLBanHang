# Plan review — 2026-10-15

An independent agent reviewed this plan against the code (read-only), and every finding below was applied to the plan files. The high findings were re-checked by hand. Several facts were confirmed correct, among them:

- the product form element ids match `ENTER_ORDER`;
- `product-form-page.test.tsx` survives the extraction;
- `toProductSuggestion` accepts `Product`;
- `mutateAsync` resolves to `Product`;
- Radix fires `onCloseAutoFocus` in jsdom;
- `RoleCodes` members are const;
- no backend test pins the role permission sets;
- there are no id collisions on the host screens.

## High

| # | Finding | Fix in the plan |
|---|---|---|
| H1 | `line-items-grid.test.tsx` mocks the product cell with a plain input, so the planned quotation test could never pass. | Task 4.2: the cell mock records its props and the test asserts `allowQuickAdd` and `nextFocusId`. Commit and focus are covered by cell tests 3.4(f) and (f2). |
| H2 | `useCreateProduct` does not invalidate searches, and searches are cached for 30 s, so a product just created stays "not found" and gets created twice. | Task 2.2: add `productKeys.searches()`; `useSaveProduct.create` invalidates it; new test (e). |
| H3 | The waiting Enter was not gated on `canAdd`, so it would swallow Enter on all five screens, and the order of the key steps was not stated. | Task 3.4: handler order spelled out (F4, waiting Enter gated on `canAdd`, early return on `rows`, switch); invariant stated; new test (e2). |

## Medium

| # | Finding | Fix in the plan |
|---|---|---|
| M1 | `allowQuickAdd && useAuthStore(...)` is a conditional hook call, which the `rules-of-hooks` lint rule rejects. | The hook is called unconditionally, then combined with `allowQuickAdd`. |
| M2 | On opening stock (no `nextFocusId`), the focus fell to `<body>` after a save. | Focus goes back to the cell input after `commit`; new test (f2). |
| M3 | Test 2.4(j) would see react-hook-form focus `#name` after an implicit submit. | Test starts from `#name` with `initialName` and asserts `#productGroupId` is not focused. |
| M4 | Task 4.1 cited a test that does not exist, and an auth mock with `user` would change the grid's preferences key. | Expected focus is named (`stock-line-quantity-0`); the mock has no `user`; opening stock uses `useAuthStore.setState`. |
| M5 | The help files' `sources` did not cover the cell or the dialog, there was no Phím tắt line, there was no partner bullet in opening stock, and the plan wrongly claimed the skill updates hashes. | Task 4.3 rewritten. |
| M6 | The diacritics check missed text that arrives already decomposed (NFD). | Uses `\p{M}` after NFD; two new test cases. |
| M7 | Errors showed at the bottom, below the fold of the scrolling dialog, and a failure of the unit get-or-create step had no inline message. | With `showHeader={false}` the error box sits above the cards; the dialog keeps a local error state; test 3.2(f) checks the inline text. |

## Low (applied)

- Phase 01: `using` lines and namespace added; the `Catalog/` folder already exists.
- Task 2.3: `showHeader={false}` or a `MemoryRouter`; assertions use `onSubmit.mock.calls[0][0]`.
- Task 3.4 tests: `rerender` after a state change; the hoisted mock factory imports the dialog itself; the add row takes part in `activeOptionId` and scrolling.
- 3.4(h): submit spy added (conventions require the "voucher not saved" check).
- Accepted and noted: Hủy on `/products/new` becomes a button, so there is no middle-click; the held-key `giveFocusBack` guard is skipped.
- Browser check: the free-text quotation line (Esc, then Enter, reaches Tên).
- SUMMARY: the "id uniqueness test" claim removed; risks updated.
