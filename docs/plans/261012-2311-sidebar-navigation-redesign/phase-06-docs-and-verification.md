# Phase 06 — Documentation and final verification

**Status:** [x] complete
**Complexity:** S

## Objective

Record the new navigation conventions, update the directory map and the docs index, run the full verification and the manual checklist, and hand over the PR.

## Files

- `docs/code-standard/conventions.md` (modify)
- `docs/codebase/directory-structure.md` (modify)
- `docs/SUMMARY.md` (modify)
- `docs/brainstorms/261012-2135-sidebar-navigation-redesign/SUMMARY.md` (modify: status line and open question)
- `docs/plans/261012-2311-sidebar-navigation-redesign/SUMMARY.md`, this file (status)

No code changes in this phase, so no TDD cycle.

## Tasks

### Task 6.1 — Conventions and directory map

1. `docs/code-standard/conventions.md`, section **Frontend**, add after the "Use shadcn-style primitives…" bullet:
   ```markdown
   - Sidebar navigation is declared only in `components/layout/nav-config.ts`. Add an item to the group of its type of work (`sales`, `inventory`, `cash`, `debt`, `reports`, `catalogs`, `system`) with `to`, `label`, a lucide `icon` not used by any other item or group, and `permission` / `anyPermission` / `role` for visibility; report items also carry `section` (Doanh thu / Kho / Quỹ / Công nợ) and sit next to the items of the same section. `nav-config.test.ts` asserts the order per role and icon uniqueness; update it with the item. Group colors are the `--nav-*` tokens in `index.css` (bare HSL channels, light and dark), used through the `bg-nav-<id>` classes; components never write color codes.
   - The active sidebar item is the one whose `to` is the longest segment-boundary prefix of the path (`findActiveItem` in `components/layout/sidebar/nav-search.ts`); `/` matches only itself. A detail route needs no nav change as long as it lives under its list route.
   - Sidebar UI state lives in `ui-store`: `sidebarGroupOpen` (only groups the user clicked; others use `defaultOpen`; shared by every user of the browser) and `pinnedNavPaths` (per user id, in pin order; pins of items the user cannot see are hidden, not removed).
   ```
2. `docs/codebase/directory-structure.md`, under `components/layout/` in the frontend tree, list the sidebar folder:
   ```
   │   ├── layout/                  # app shell, header, sidebar, search, notifications
   │   │   ├── nav-config.ts        # sidebar groups/items, permissions, icons, sections
   │   │   └── sidebar/             # sidebar (wide), sidebar-rail + sidebar-flyout (collapsed), sidebar-search,
   │   │                            # sidebar-pin-chips, sidebar-group, sidebar-item, nav-search.ts (pure filter/highlight/active item)
   ```
   (keep the existing tree characters and indentation of the file).
3. **Commit:**
   ```bash
   git add docs/code-standard/conventions.md docs/codebase/directory-structure.md
   git commit -m "docs: sidebar navigation conventions and layout map"
   ```

### Task 6.2 — Docs index and brainstorm status

1. `docs/SUMMARY.md`, in **Other**, after the `plans/archived/261012-0433-round2a-review-refactor` row (or next to the other active plans), add:
   ```markdown
   | [plans/261012-2311-sidebar-navigation-redesign/SUMMARY.md](plans/261012-2311-sidebar-navigation-redesign/SUMMARY.md) | Implementation plan for the sidebar redesign — scroll hotfix, task-based groups with colored blocks, longest-prefix highlight, collapsible groups, accent-insensitive search, per-user pin chips, collapsed rail with flyouts |
   ```
   Make sure the two brainstorm rows of `261012-2135-sidebar-navigation-redesign` are present (they were added uncommitted by the brainstorm session; add them if the branch lacks them).
2. In the brainstorm `SUMMARY.md`: set **Status** to `Implemented (plan docs/plans/261012-2311-sidebar-navigation-redesign)` and strike the first open question with the order actually used.
3. `git diff docs/SUMMARY.md`: if the diff contains rows that belong to other uncommitted work (for example `261012-2237-tt133-report-forms`), **stop and ask the user** whether to commit them with this branch; otherwise:
   ```bash
   git add docs/SUMMARY.md docs/brainstorms/261012-2135-sidebar-navigation-redesign/SUMMARY.md
   git commit -m "docs: index the sidebar redesign plan"
   ```

### Task 6.3 — Final verification

1. From `frontend/`:
   ```bash
   npx vitest run src/components/layout src/stores
   npm run test        # FAIL lines must equal BASELINE.md
   npm run typecheck
   npm run lint        # error/warning counts <= BASELINE.md
   npm run build && git checkout -- tsconfig.app.tsbuildinfo tsconfig.node.tsbuildinfo
   ```
2. Manual checklist (`npm run dev` with the backend running; jsdom cannot check layout):
   - [ ] Admin, window ~700px tall, wide mode: search box fixed, nav scrolls to Phân quyền; 7 colored groups; Danh mục / Hệ thống start closed on a fresh profile (clear `qldonhang-ui-store`).
   - [ ] `/settings/my-quotation-settings` lights only "Cài đặt của tôi"; `/settings/branches` lights "Cấu hình hệ thống"; `/quotations/<id>` lights "Báo giá"; `/admin/user-settings/<id>` lights nothing.
   - [ ] Close Danh mục, open `/products`: Danh mục opens; go to `/receipts`: it closes again; reload: state remembered.
   - [ ] Search "doi tru", "bao cao", "cong no", "xyz"; Enter / ↓ / ↑ / Esc / ×; the box clears after navigation.
   - [ ] Pin 8 items: 6 chips + "+2", expand / "Thu gọn", × unpin, active chip; another user on the same browser sees no pins.
   - [ ] Collapsed rail: tooltips on hover, flyout on click and Enter/Space, Esc returns focus, one flyout at a time, Báo cáo flyout scrolls inside, active group ring + bar, ★ flyout, 🔍 expands and focuses.
   - [ ] Mobile drawer (devtools, touch emulation): same as wide mode, ☆ and × always visible, drawer scrolls and closes on navigation.
   - [ ] Dark mode: group header background, chip colors, search highlight readable; icon blocks keep their colors.
   - [ ] SALES account: Bán hàng, Danh mục (Hàng hóa, Nhóm hàng hóa), Hệ thống. WAREHOUSE account: Bán hàng (QR), Kho, Báo cáo › Kho, Danh mục (Nhà cung cấp), Hệ thống.
3. Tick Phase 06 in `SUMMARY.md`, set this file's status, commit `chore(plan): sidebar redesign complete` (stage `docs/plans/261012-2311-sidebar-navigation-redesign`).

### Task 6.4 — Push and hand over

1. `git push qlbanhang feat/sidebar-nav-redesign`.
2. No `gh` CLI: give the user `https://github.com/hongchinh/QLBanHang/compare/main...feat/sidebar-nav-redesign` and a PR body (Vietnamese summary of the 7 groups, search, pins, rail; test commands and results; the manual checklist; end with the PR attribution line from the session's system reminder).
3. Post to the team chat (`bash .claude/tools/gchat.sh "…"`) and give the user this deploy note to post when it ships:
   ```
   Menu trái đã được sắp xếp lại theo loại công việc: Bán hàng · Kho · Thu chi · Công nợ · Báo cáo · Danh mục · Hệ thống. Danh mục và Hệ thống mặc định đang gập, bấm tiêu đề nhóm để mở (hệ thống nhớ lựa chọn). Gõ vào ô "Tìm chức năng…" để tìm nhanh (không cần dấu), bấm ☆ cạnh một mục để ghim lên đầu menu.
   ```

## Verification

- Commands of Task 6.3 step 1; manual checklist of Task 6.3 step 2.

## Exit Criteria

- Conventions, directory map, docs index and brainstorm status updated and committed.
- Full frontend suite at baseline, typecheck/lint/build pass, manual checklist done.
- Branch pushed; compare URL, PR body and deploy note handed to the user.
