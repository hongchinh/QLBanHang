# Toast at the bottom center

## Goal
On desktop, toasts open at the top right (`sm:top-[72px] sm:right-4`, up to 420px wide), right on the sticky action bar of the forms, so "Cập nhật", "In phiếu", "Hủy phiếu" and "Xóa" cannot be clicked while a toast is shown. The whole viewport `<ol>` blocks clicks too, including its 16px padding and the gaps between toasts. Move the toast viewport to the bottom center on every screen size and let clicks pass through the viewport, so only a toast itself takes clicks.

Design approved in a brainstorm on 2026-10-15 (option A). Mockup: `.superpowers/brainstorm/2022-1792070911/content/toast-position.html` (not tracked).

## Scope
- In scope:
  - `frontend/src/components/ui/toaster.tsx`: viewport position, `pointer-events-none` on the viewport, slide-in direction.
  - `frontend/src/components/ui/toaster.test.tsx`: tests for the above.
  - Docs lines that call the toast viewport a phone-only element: `docs/code-standard/conventions.md`, `docs/architecture/pwa-mobile.md`.
  - A browser check on a scratch database (`BROWSER-CHECK.md` in this folder), then the user's own look.
- Out of scope:
  - `frontend/src/lib/use-toast.ts` (API unchanged) and the ~200 `toast(...)` call sites.
  - Duration (4 s), swipe direction (`right`), pause on hover: unchanged.
  - Inline "Đã lưu" status on the form action bar (option D, rejected), bottom right (B, rejected), top center over the header (C, rejected).

## Assumptions
- The viewport keeps `max-w-[420px]`; on a phone it stays full width at the bottom as today.
- `pb-[max(1rem,env(safe-area-inset-bottom))]` now applies on every size; on desktop the inset is 0, so the padding is 1rem, the same as the old `sm:pb-4`.
- Radix sets `pointer-events: none` on the viewport wrapper only while there are no toasts. With toasts, the `<ol>` gets `pointer-events-none` from us and each toast `<li>` keeps `pointer-events-auto` (already in the Root classes). Pointer events on a toast still bubble to the wrapper, so pause on hover keeps working.

## Risks
- Accepted: for 4 s a toast covers the middle of the totals strip at the bottom of the stock voucher form (text only, no buttons).
- Tall dialogs that stay open while a toast shows. Example: the partner quick add (`max-w-3xl max-h-[90vh]`) toasts "Không thể lưu" and stays open. At 1280×768 the toast spans x≈446–834 and the dialog's footer buttons sit near x≈840–1024, so they may touch, more so with two stacked toasts. The browser check (Task 3, S3) measures it; a hit is reported to the user, not fixed silently.
- A page with a button at the bottom center would now be covered instead. None known; the browser check and the user's own look cover the main forms.
- Other sessions share the main checkout; the phase runs in its own worktree.

## Phases
- [x] Phase 01 — Bottom-center toast viewport (S) — `phase-01-bottom-center-viewport.md`

## Final Verification
Run from the worktree root (`.claude/worktrees/toast-bottom-center`):
- `cd frontend; npx vitest run --config vitest.config.ts src/components/ui/toaster.test.tsx` — PASS
- `cd frontend; npm run typecheck; npm run lint` — no errors
- `cd frontend; npm run test -- --testTimeout=30000 --maxWorkers=3` — no failures beyond the known baseline (bank-accounts-tab "adds a new account", useNotificationHub "invalidates unread-count", payment-qr-page "generates and displays a QR")
- `BROWSER-CHECK.md` (Task 3): action bar buttons reachable while one or two toasts show at 1280×768, hover pause works, phone layout unchanged; the tall dialog result (S3) reported
- The user confirmed the change on their own screen

## Rollback / Recovery
Revert the phase commits on `fix/toast-bottom-center` (or drop the branch before merge); only the two frontend files, two doc sentences and the browser-check record change.
