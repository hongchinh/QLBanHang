# Phase 00 — Scroll hotfix PR to `main`

**Status:** [ ] pending
**Complexity:** S

## Objective

Make the desktop sidebar scroll today, independent of the redesign. The layout grid uses `grid-rows-[4rem_1fr]`; `1fr` is `minmax(auto, 1fr)`, so the `<aside>` grid item (min-height `auto`) stretches the row to the full menu height and `overflow-hidden` cuts it: the `<nav>` never overflows and never scrolls. Changing the track to `minmax(0,1fr)` caps the row at the viewport.

This phase can run before Round 2B is merged. It uses its own worktree so the Round 2B working tree is not touched.

## Files

- `frontend/src/components/layout/app-layout.tsx` (modify)
- `frontend/src/components/layout/__tests__/app-layout-scroll.test.tsx` (new)

## Tasks

### Task 0.1 — Worktree from `qlbanhang/main`

1. From `D:/Projects/QLBanHang`:
   ```bash
   git fetch qlbanhang
   git worktree add D:/Projects/QLBanHang-scrollfix -b fix/sidebar-scroll qlbanhang/main
   cd D:/Projects/QLBanHang-scrollfix/frontend && npm ci
   ```
2. Expected: the worktree exists on branch `fix/sidebar-scroll`, `npm ci` succeeds.

### Task 0.2 — Grid row track `minmax(0,1fr)`

jsdom does not compute layout, so the test guards the class that fixes it.

1. **Write the failing test** `frontend/src/components/layout/__tests__/app-layout-scroll.test.tsx`:
   ```tsx
   import { MemoryRouter, Route, Routes } from 'react-router-dom';
   import { render, screen } from '@testing-library/react';
   import { describe, expect, it, vi } from 'vitest';
   import { AppLayout } from '../app-layout';
   import { useBranchContext } from '@/features/branches/use-branch-context';

   vi.mock('@/features/branches/use-branch-context', () => ({ useBranchContext: vi.fn() }));
   vi.mock('@/hooks/useNotificationHub', () => ({ useNotificationHub: vi.fn() }));
   vi.mock('../header/app-header', () => ({ AppHeader: () => <header>header</header> }));

   describe('AppLayout grid', () => {
     it('caps the content row at the viewport so the sidebar can scroll', () => {
       vi.mocked(useBranchContext).mockReturnValue({ myBranches: undefined, workingBranch: null, ready: true });
       render(
         <MemoryRouter initialEntries={['/']}>
           <Routes>
             <Route element={<AppLayout />}>
               <Route path="/" element={<div>Page content</div>} />
             </Route>
           </Routes>
         </MemoryRouter>,
       );

       const grid = screen.getByText('header').parentElement!;
       expect(grid).toHaveClass('grid-rows-[4rem_minmax(0,1fr)]');
       expect(grid).not.toHaveClass('grid-rows-[4rem_1fr]');
     });
   });
   ```
2. **Run test to verify it fails:** `npx vitest run src/components/layout/__tests__/app-layout-scroll.test.tsx` (from `frontend/`). Expected: FAIL — element does not have class `grid-rows-[4rem_minmax(0,1fr)]`.
3. **Write minimal implementation:** in `app-layout.tsx` change `grid-rows-[4rem_1fr]` to `grid-rows-[4rem_minmax(0,1fr)]` on the root grid `div`. Nothing else.
4. **Run tests to verify they pass:** `npx vitest run src/components/layout`. Expected: PASS (including `app-layout-branch-gate.test.tsx`).
5. **Commit:**
   ```bash
   git add frontend/src/components/layout/app-layout.tsx frontend/src/components/layout/__tests__/app-layout-scroll.test.tsx
   git commit -m "fix(frontend): let the desktop sidebar scroll"
   ```

### Task 0.3 — Manual check, push, PR

1. `npm run typecheck`, `npm run lint`, `npm run build`, then `git checkout -- tsconfig.app.tsbuildinfo tsconfig.node.tsbuildinfo`.
2. Manual: `npm run dev` (backend running), admin account, browser window about 700px tall: the sidebar scrolls to "Phân quyền" in the wide mode and the collapsed mode; the mobile drawer still scrolls.
3. `git push qlbanhang fix/sidebar-scroll`. No `gh` CLI: give the user the compare URL `https://github.com/hongchinh/QLBanHang/compare/main...fix/sidebar-scroll` and this PR body:
   ```
   Sidebar không cuộn được trên desktop: hàng `1fr` của grid layout (= minmax(auto, 1fr)) bị `<aside>` kéo cao bằng cả menu, phần thừa bị `overflow-hidden` cắt. Đổi track thành `minmax(0,1fr)`.

   Kiểm tra: cửa sổ ~700px, admin, chế độ rộng / thu gọn / mobile drawer.
   ```
4. After the user confirms the PR is open, remove the worktree from `D:/Projects/QLBanHang`: `git worktree remove D:/Projects/QLBanHang-scrollfix` (keep the branch until merged).

## Verification

- `npx vitest run src/components/layout`
- `npm run typecheck && npm run lint && npm run build`
- Manual scroll check above.

## Exit Criteria

- `fix/sidebar-scroll` pushed to `qlbanhang` with one commit; PR URL and body handed to the user.
- Targeted tests, typecheck, lint and build pass; manual scroll check done.
