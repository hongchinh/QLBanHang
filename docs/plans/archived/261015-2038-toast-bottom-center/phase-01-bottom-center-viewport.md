# Phase 01 — Bottom-center toast viewport

**Status:** [x] done (2026-10-15)
**Complexity:** S

## Objective
Toasts stack from the bottom center on every screen size, and only a toast itself blocks clicks.

## Files
- frontend/src/components/ui/toaster.tsx
- frontend/src/components/ui/toaster.test.tsx
- docs/code-standard/conventions.md
- docs/architecture/pwa-mobile.md

## Setup
Other sessions share the main checkout (on 2026-10-15 another session started plan `261015-2038-product-quick-add` in it). Work in a worktree of your own:

```powershell
git worktree add .claude/worktrees/toast-bottom-center -b fix/toast-bottom-center main
New-Item -ItemType Junction -Path .claude/worktrees/toast-bottom-center/frontend/node_modules -Target D:\Projects\QLBanHang\frontend\node_modules
```

Keep the drive letter uppercase (`D:\`) in paths. Run every command below from the worktree root. Before removing the worktree later, delete only the junction with `(Get-Item -Force <worktree>\frontend\node_modules).Delete()` and check that `D:\Projects\QLBanHang\frontend\node_modules` still holds its packages.

Every commit message ends with a blank line and:

```
Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
```

## Tasks

### Task 1 — Viewport at the bottom center, clicks pass through

1. Write the failing tests. Replace the body of `frontend/src/components/ui/toaster.test.tsx` with:

   ```tsx
   import { act, render, screen } from '@testing-library/react'
   import { afterEach, describe, expect, it } from 'vitest'
   import { dismissToast, toast } from '@/lib/use-toast'
   import { Toaster } from './toaster'

   const shown: string[] = []

   afterEach(() => {
     act(() => shown.splice(0).forEach(dismissToast))
   })

   describe('Toaster', () => {
     // Keep the toasts above the iPhone home indicator.
     it('pads the viewport by the bottom safe-area inset', () => {
       render(<Toaster />)
       const viewport = document.querySelector('ol')!
       expect(viewport).toHaveClass('pb-[max(1rem,env(safe-area-inset-bottom))]')
       expect(viewport).not.toHaveClass('sm:pb-4')
     })

     // At the top right the toasts covered the sticky action bar of the forms (Cập nhật, In phiếu, Hủy phiếu).
     it('stacks the toasts from the bottom center on every screen size', () => {
       render(<Toaster />)
       const viewport = document.querySelector('ol')!
       expect(viewport).toHaveClass('bottom-0', 'left-1/2', '-translate-x-1/2', 'flex-col-reverse', 'max-w-[420px]')
       expect(viewport.className).not.toMatch(/\bsm:(top|right|bottom|flex-col)/)
     })

     // The viewport's padding and the gaps between toasts must not swallow clicks; only a toast does.
     it('lets clicks pass through the viewport but not through a toast', () => {
       render(<Toaster />)
       act(() => {
         shown.push(toast({ title: 'Đã lưu phiếu' }))
       })
       expect(document.querySelector('ol')).toHaveClass('pointer-events-none')
       expect(screen.getByText('Đã lưu phiếu').closest('li')).toHaveClass('pointer-events-auto', 'data-[state=open]:slide-in-from-bottom-full')
     })
   })
   ```

2. Run the tests to verify they fail — `cd frontend; npx vitest run --config vitest.config.ts src/components/ui/toaster.test.tsx` / Expected: FAIL — the first test on `not.toHaveClass('sm:pb-4')`, the second on missing `left-1/2` (and the `sm:` overrides), the third on missing `pointer-events-none`.

3. Minimal implementation in `frontend/src/components/ui/toaster.tsx`:
   - In the Root classes, replace `data-[state=open]:slide-in-from-top-full` with `data-[state=open]:slide-in-from-bottom-full`.
   - Replace the Viewport `className` with:

     ```tsx
     className="pointer-events-none fixed bottom-0 left-1/2 z-[100] flex max-h-screen w-full max-w-[420px] -translate-x-1/2 flex-col-reverse gap-2 p-4 pb-[max(1rem,env(safe-area-inset-bottom))]"
     ```

   Keep `swipeDirection="right"`, the Root `pointer-events-auto`, durations and everything else as is.

4. Run the tests to verify they pass — `cd frontend; npx vitest run --config vitest.config.ts src/components/ui/toaster.test.tsx` / Expected: PASS (3 tests). Then `cd frontend; npm run typecheck; npm run lint` / Expected: no errors.

5. Commit —
   ```
   git add frontend/src/components/ui/toaster.tsx frontend/src/components/ui/toaster.test.tsx
   git commit -m "fix(toast): show toasts at the bottom center so they no longer cover the form action bar" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
   ```

### Task 2 — Docs

1. `docs/code-standard/conventions.md`: find the sentence ending in "uses `max(<value>, env(safe-area-inset-*))` instead (mobile search sheet, update popup, toast viewport)." Keep it, and add right after it: "The toast viewport sits at the bottom center on every screen size and has `pointer-events-none`; only a toast takes clicks, so it never blocks the sticky action bar at the top of a form."
2. `docs/architecture/pwa-mobile.md`: in the line "Elements with their own spacing keep it as a minimum with `max(<spacing>, env(safe-area-inset-*))`: the mobile search sheet, the "Có phiên bản mới" popup and the phone toast viewport.", change "the phone toast viewport" to "the toast viewport (bottom center on every screen size)".
3. Commit —
   ```
   git add docs/code-standard/conventions.md docs/architecture/pwa-mobile.md
   git commit -m "docs: toast viewport at the bottom center" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
   ```

### Task 3 — Browser check (agent), then the user's look

Follow the isolation rules of `docs/plans/archived/261015-0926-cash-voucher-form-redesign/BROWSER-CHECK.md`. Read-only run: no source edits or commits; scripts and screenshots go to `docs/plans/261015-2038-toast-bottom-center/browser-check/` (scripts may also live in the scratchpad).

1. **Backend on a scratch database.** Create `qldonhang_toast_browser` (`"/c/Program Files/PostgreSQL/18/bin/psql"` with `PGPASSWORD=1`). Never connect to `qldonhang_test`, `qldonhang` or any `qldonhang_integtest*` database. Start the API from the worktree's `backend/src/OrderMgmt.WebApi` with `ConnectionStrings__Default` and `ConnectionStrings__DefaultConnection` set to the scratch DB, `ASPNETCORE_URLS=http://localhost:5056`, `ASPNETCORE_ENVIRONMENT=Development`, `Database__AutoMigrateAndSeed=true`, `--no-launch-profile`. Do not use port 5050.
2. **Frontend.** From the worktree's `frontend`: `npx vite --port 5182 --strictPort`. Do not edit `vite.config.ts`.
3. **Browser.** `playwright-core` (not in `frontend/node_modules`; install it in a scratch folder, never in the repo) driving Edge (`chromium.launch({ channel: 'msedge' })`), context with `serviceWorkers: 'block'`, locale `vi-VN`, time zone `Asia/Ho_Chi_Minh`, viewport 1280×768. Rewrite `/api/*` to `http://localhost:5056`; abort `/hubs/*` and close the hub WebSocket through `context.routeWebSocket`. Log every request URL and assert none goes to `:5050`.
4. **Data.** As the seeded `admin`, create customer KH0001 through the real API.
5. **S1 — form action bar.** Open `/receipts/new`, pick KH0001, type an amount on line 1, click "Lưu tạm". While the success toast is visible (within 4 s):
   - Take a screenshot `s1-toast.png`.
   - For each visible button in the sticky action bar (the print button, the QR button, "Cập nhật"), take its bounding-box center and check `document.elementFromPoint(x, y)` is the button or inside it. Expected: all of them.
   - Check the toast's `<li>` box lies in the bottom half of the window and is horizontally centered (±2px).
   - Click "Cập nhật" while the toast still shows. Expected: the click reaches the button (a request goes out or the button reacts).
6. **S2 — two toasts.** Trigger a second toast while the first shows (for example: route `PUT /api/cash-vouchers/*` to a 400 response once and click "Cập nhật"). Repeat the `elementFromPoint` check for the action bar buttons and screenshot `s2-two-toasts.png`.
7. **S3 — tall dialog with a toast.** On a receipt form, open the partner quick add (the + button or F4 in the partner field). Route `POST /api/customers` to a 400 response, fill the required fields and save, so the "Không thể lưu" toast shows while the dialog stays open. Check `elementFromPoint` at the center of the dialog's save and cancel buttons. Expected: both are the buttons. If the toast covers either, record it as a defect with a screenshot `s3-dialog.png`; do not change code.
8. **S4 — hover.** Hover the toast for 6 s: it stays. Move the pointer away: it closes within about 5 s. The ✕ button closes it at once.
9. **S5 — phone.** Viewport 390×844 with `isMobile: true`, `hasTouch: true`: repeat S1 up to the toast; the toast spans the width at the bottom. Screenshot `s5-phone.png`.
10. **Cleanup.** Stop the API, `dotnet run` and Vite processes you started; check ports 5056 and 5182 are free; `drop database qldonhang_toast_browser`.
11. Write `docs/plans/261015-2038-toast-bottom-center/BROWSER-CHECK.md`: date, commit under test, a PASS/FAIL table for S1–S5, defects, and the isolation proof (ports, DB, request log has no `:5050`). Commit it with the screenshots:
    ```
    git add docs/plans/261015-2038-toast-bottom-center/BROWSER-CHECK.md docs/plans/261015-2038-toast-bottom-center/browser-check
    git commit -m "docs(plan): toast bottom center browser check" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
    ```
12. **User's look.** Stop and ask the user (AskUserQuestion, in Vietnamese) to look at the change on their own screen: save a voucher and click the action bar buttons while the toast shows. Record their answer in `BROWSER-CHECK.md`.

## Verification
- `cd frontend; npx vitest run --config vitest.config.ts src/components/ui/toaster.test.tsx`
- `cd frontend; npm run typecheck; npm run lint`
- `cd frontend; npm run test -- --testTimeout=30000 --maxWorkers=3` — known baseline failures under load: bank-accounts-tab "adds a new account", useNotificationHub "invalidates unread-count", payment-qr-page "generates and displays a QR". Any other failure counts only if it fails again in a rerun with the same flags.

## Exit Criteria
- The three toaster tests pass; the frontend suite has no failures beyond the known baseline; typecheck and lint are clean.
- `BROWSER-CHECK.md` shows S1, S2, S4 and S5 passing. A S3 defect is reported to the user, who decides whether to fix it here or later.
- The user confirmed the change on their own screen.
- Docs no longer describe the toast viewport as phone-only.
