# Phase 06 — Frontend: quotation integration

**Status:** [ ] pending
**Complexity:** S

## Objective

Add a "Tạo QR thanh toán" button to the quotation detail/form page that opens `/qr-thanh-toan`
with the quotation total and code pre-filled, so a sales user can generate a payment QR for a
specific quotation in one click.

## Files

- `frontend/src/pages/quotations/quotation-form-page.tsx` (edit)
- `frontend/src/pages/quotations/quotation-form-page.test.tsx` (edit or new — check whether this
  file already exists before creating it)

## Tasks

### 1. Add the button

1. Write the failing test — check whether
   `frontend/src/pages/quotations/quotation-form-page.test.tsx` already exists:
   - If it exists, add a new `it(...)` block to its existing `describe`.
   - If it does not exist, this codebase's `quotation-form-page.tsx` is complex enough (auth store,
     router, multiple hooks) that a full render test is disproportionate for one button; instead
     add a focused test only for the navigation helper described below, and skip writing a full
     page-render test — note this explicitly rather than silently doing nothing.

   Add this test (adjust the `describe`/imports to match the existing file's setup — reuse
   whatever router/query-client/auth-store mocking wrapper the file already has for its other
   tests):

```tsx
it('links to the payment QR page with the quotation total and code pre-filled', async () => {
  // Render the page with `initial` (the loaded quotation) set to a fixture with
  // total: 1250000 and code: 'BG-0001', using the same render/wrapper helper as this
  // file's other tests.
  // ...
  const link = screen.getByRole('link', { name: /tạo qr thanh toán/i });
  expect(link).toHaveAttribute(
    'href',
    expect.stringContaining('/qr-thanh-toan?amount=1250000&content=BG-0001'),
  );
});
```

   Run: `cd frontend && npm run test -- quotation-form-page` / Expected: FAIL (no such link yet).

2. In `frontend/src/pages/quotations/quotation-form-page.tsx`:
   - Add `QrCode` to the existing `lucide-react` import list (it already imports several icons —
     add `QrCode` alongside them).
   - Near the `Excel`/`Printer` dropdown buttons (around the block containing
     `<Printer className="mr-2 h-4 w-4 text-indigo-600" />` at line ~690), add a `Link` (already
     imported from `react-router-dom` at the top of the file) rendered as a button-styled anchor.
     Note: the surrounding region (opening at `{isEdit && (` around line 573) only guards on
     `isEdit`, not on `initial` being loaded — `initial` can still be `undefined` there while the
     quotation is fetching. Do not assume `initial` is non-null just because you're inside that
     block; the button must have its own `{initial && (...)}` guard (as shown below) so it never
     reads `.total`/`.code` off an undefined value:
     ```tsx
     {initial && (
       <Button variant="outline" size="sm" asChild>
         <Link
           to={`/qr-thanh-toan?amount=${Math.round(initial.total)}&content=${encodeURIComponent(initial.code)}`}
         >
           <QrCode className="mr-2 h-4 w-4 text-cyan-600" />
           Tạo QR thanh toán
         </Link>
       </Button>
     )}
     ```
     Use `Button`'s existing `asChild` support (already used elsewhere in this codebase's
     shadcn-style `Button` component — confirm by checking `frontend/src/components/ui/button.tsx`
     for `asChild` support before writing this; if `asChild` is not supported, render a plain
     `<Link className={buttonVariants({ variant: 'outline', size: 'sm' })}>` instead, matching
     whatever pattern this button component actually exposes).
   - Place it before the `Excel` dropdown so the action order reads: QR → Excel → In, consistent
     with "generate something to hand to someone" preceding the print/export actions.

3. Run test — `cd frontend && npm run test -- quotation-form-page` / Expected: PASS.
4. Commit — `git commit -m "feat(quotations): add payment QR shortcut from quotation detail"`.

## Verification

- `cd frontend && npm run typecheck`
- `cd frontend && npm run test -- quotation-form-page`
- `cd frontend && npm run build`
- Manual: open an existing quotation, click "Tạo QR thanh toán", confirm `/qr-thanh-toan` opens
  with `amount` equal to the quotation's total and `content` equal to its code, and the form is
  pre-filled accordingly (per Phase 04's query-param pre-fill logic).

## Exit Criteria

- The quotation detail page has a working "Tạo QR thanh toán" link that pre-fills amount and
  content on the payment-qr page for that quotation.
