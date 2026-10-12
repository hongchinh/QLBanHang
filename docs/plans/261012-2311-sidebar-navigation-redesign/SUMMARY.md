# Sidebar navigation redesign (Thiết kế lại sidebar trái)

> Created: 2026-10-12 23:11:18 · Source design: [brainstorm SUMMARY](../../brainstorms/261012-2135-sidebar-navigation-redesign/SUMMARY.md), [section-01 UI design](../../brainstorms/261012-2135-sidebar-navigation-redesign/section-01-ui-design.md) (source of truth for UI, components, edge cases and tests) · Mockups: [mockup-02-option-c-states.html](../../brainstorms/261012-2135-sidebar-navigation-redesign/mockup-02-option-c-states.html) · Depends on: [Round 2B plan](../261012-1709-cash-debt-round2b/SUMMARY.md) (must be merged before Phase 01)

## Goal

Make the left sidebar usable with about 40 items: fix the desktop scroll bug, regroup the menu by type of work (Bán hàng · Kho · Thu chi · Công nợ · Báo cáo · Danh mục · Hệ thống, style C with a colored icon block per group), make groups collapsible with a remembered state, add an accent-insensitive "Tìm chức năng…" filter, per-user pins shown as chips, and one flyout per group in the 64px collapsed rail. Frontend only: no route, permission, API or backend change.

## Scope

**In scope**

- Scroll hotfix in `app-layout.tsx` (`grid-rows-[4rem_minmax(0,1fr)]`), shipped first as its own PR to `main` (Phase 00).
- `nav-config.ts`: 7 groups with `id`, `icon`, `color`, `defaultOpen`; items with `section` (Báo cáo only); one distinct icon per item, none equal to a group icon; "Setting" → "Hệ thống"; catalog "Kho" → "Kho hàng"; the 7 debt reports of Round 2B under *Báo cáo › Công nợ*.
- Active item = longest segment-boundary prefix of `pathname` (fixes `/settings` lighting up together with "Cài đặt của tôi").
- `ui-store.ts`: `sidebarGroupOpen`, `pinnedNavPaths` (persisted, no migration).
- Color tokens `--nav-*` in `index.css` (light + dark) mapped in `tailwind.config.ts`.
- Components in `components/layout/sidebar/`: `sidebar`, `sidebar-search`, `sidebar-pin-chips`, `sidebar-group`, `sidebar-item`, `sidebar-rail`, `sidebar-flyout`, pure `nav-search.ts`.
- Docs: `code-standard/conventions.md`, `codebase/directory-structure.md`, `docs/SUMMARY.md`.

**Out of scope** (brainstorm non-goals)

- Server-side pins or cross-device sync.
- Hub pages `/reports`, `/catalogs`.
- Putting menu items into the header Ctrl+K search (it stays data-only); no keyboard shortcut for the sidebar search.
- Any route, permission or backend change.

## Decisions made while planning (in addition to the brainstorm)

| # | Topic | Decision |
|---|---|---|
| S1 | Types | `NavItem` (`to`, `label`, `icon`, `section?`) and `NavGroup` (`id`, `label`, `icon`, `color`, `defaultOpen`, `items`) are exported from `components/layout/nav-config.ts`. `SidebarNavItem` / `SidebarNavGroup` in `sidebar.tsx` are removed; sidebar files import the types from `../nav-config` (no import from `nav-config` back into `sidebar/`, so no cycle). The permission fields stay on an internal `NavItemConfig`. |
| S2 | `color` | `NavGroup.color` holds the Tailwind background class of the group token (`'bg-nav-sales'` …), written as a full literal in `nav-config.ts` so Tailwind's content scan keeps it. Components never write color codes. |
| S3 | Store action name | The brainstorm's `toggleGroup(id)` cannot know the *displayed* state (temporary open, `defaultOpen`). The store exposes `setSidebarGroupOpen(id, open)`; `Sidebar` calls it with `!displayedOpen`. `togglePin(userId, path)` is as designed. |
| S4 | Temporary open | `Sidebar` keeps `tempOpen: { pathname, groupId }`. The effective temporary group is `tempOpen.pathname === pathname ? tempOpen.groupId : activeGroupId`, so a route change re-opens the active group without an effect and clicking a header sets `{ pathname, groupId: null }`. Nothing is written to the store. |
| S5 | Search result shape | `filterNavGroups(groups, query)` returns `{ groups: NavGroup[]; matches: Map<string, MatchRange>; count: number }`: filtered groups keep the `NavGroup` shape (so `SidebarGroup` renders them unchanged), `matches` is keyed by item `to` and holds the highlight range in the item label (absent when the item matched through its group label or section). Only item labels are highlighted. |
| S6 | Index-preserving normalize | `normalizeVi` normalizes the input to NFC and then maps each character on its own (NFD, strip `\u0300-\u036f`, `đ/Đ` → `d`, lowercase), so index *i* of the result is index *i* of the NFC input and a match range can be applied to the label directly. |
| S7 | Pins helper | `resolvePinnedItems(paths, groups)` in `nav-search.ts` returns the visible items for the stored paths in pin order, skipping paths without a visible item (lost permission or removed route); the store keeps them. Tested as a pure function instead of in `sidebar-pin-chips.test.tsx`. |
| S8 | Chip tooltip | The full name on a truncated chip uses the native `title` attribute (no Radix tooltip per chip). |
| S9 | Touch visibility | ☆ and × visibility on touch devices uses the CSS variant `[@media(hover:none)]:` (no JS device detection): hidden-until-hover on desktop (`opacity-0 group-hover:opacity-100 focus-visible:opacity-100`), always visible (☆ at `opacity-60`) when `hover: none`. |
| S10 | Unique DOM ids | Desktop sidebar and mobile drawer render two `Sidebar` instances at once, so `aria-controls` targets use a `useId()` prefix. |
| S11 | Search focus on expand | `jsx-a11y/no-autofocus` is on, so the rail's 🔍 sets a ref flag and calls `setSidebarCollapsed(false)`; an effect on `collapsed` focuses the search input once and clears the flag. |
| S12 | Search-mode headers | While searching, a group header is a non-interactive `div` showing the group's result count instead of the chevron (groups are force-open then); in browse mode it is the `button` with `aria-expanded` / `aria-controls`. |
| S13 | Item ↔ star markup | An item row is a `div` holding the `Link` and a sibling ☆ `button` (a button inside an anchor is invalid). Active item: `aria-current="page"`. |

## Assumptions

- The brainstorm SUMMARY and section-01 are approved (status "Design approved … reviewed"); section-01 wins on UI details.
- Phase 00 can run now. Phases 01–06 start only after Round 2B is merged into `qlbanhang/main` (brainstorm "Xếp thứ tự với Round 2B"); Phase 01 Task 1.1 checks it.
- Round 2B ships the sidebar item `{ to: '/reports/debt-reconciliation', label: 'Biên bản đối chiếu', icon: FileCheck, permission: 'reports.debt' }` and the order Số dư công nợ, Sổ chi tiết công nợ, Công nợ theo chứng từ, Tuổi nợ, Nợ quá hạn, Công nợ theo nhân viên, Biên bản đối chiếu (Round 2B executor conventions). This plan uses that order for *Báo cáo › Công nợ* (brainstorm open question 1); if 2B merged a different order, use the merged order and note it in the Phase 01 commit message.
- `lucide-react` 0.453.0 is installed; every icon in the Phase 01 icon table was checked to exist in it.
- Remotes: PRs go to `qlbanhang`; local `main` tracks `origin` and is behind; no `gh` CLI (memory `git-remotes`).
- No frontend dependency is added (accordion is hand-made, flyout uses `components/ui/popover.tsx`).

## Risks

- **Users lose their bearings** after the regroup. Mitigation: pins and search; a short note in the team chat at deploy (Phase 06).
- **SALES / WAREHOUSE** have frequent items in the collapsed *Danh mục* group (accepted trade-off): opening it once is remembered, the group opens while on its page, and items can be pinned.
- **Layout regressions are invisible to jsdom** (scroll, rail width, flyout collision, dark mode). Mitigation: the manual checklist in Phase 05 and Phase 06 (700px-tall window, admin/SALES/WAREHOUSE, dark mode, mobile drawer).
- **Merge conflict with Round 2B** in `nav-config.ts` / `nav-config.test.ts`: avoided by starting Phase 01 from `main` after 2B is merged.
- **Baseline failures:** unrelated frontend test/lint failures may exist; Phase 01 records them in `BASELINE.md` and every later phase compares against it.

## Phases

- [ ] Phase 00 — Scroll hotfix PR to `main` (S) — `phase-00-scroll-hotfix.md`
- [x] Phase 01 — Branch, baseline, regroup, icons, longest-prefix highlight (M) — `phase-01-regroup-and-active-item.md`
- [x] Phase 02 — Color tokens, collapsible groups, remembered state, temporary open (M) — `phase-02-accordion.md`
- [x] Phase 03 — Accent-insensitive search (M) — `phase-03-search.md`
- [x] Phase 04 — Per-user pins and chips (M) — `phase-04-pins.md`
- [x] Phase 05 — Collapsed rail and flyouts (M) — `phase-05-rail-and-flyouts.md`
- [ ] Phase 06 — Documentation and final verification (S) — `phase-06-docs-and-verification.md`

Phases run in order (Phase 00 is independent and may already be merged when Phase 01 starts). Each phase ends with green targeted tests, `npm run typecheck`, `npm run lint` and `npm run build`.

## Executor conventions

- **Branches:** Phase 00 on `fix/sidebar-scroll` (worktree from `qlbanhang/main`). Phases 01–06 on `feat/sidebar-nav-redesign` created from `qlbanhang/main` after Round 2B is merged.
- **Commits:** conventional commits, scope `frontend` or `docs` (e.g. `feat(frontend): collapsible sidebar groups`). End every commit message with the attribution line from the session's system reminder.
- **Staging:** stage files explicitly from the task's file list (`git add <paths>`). Never `git add -A` / `git add .`: `source/` (legacy VB.NET) is untracked on purpose and `.claude/tools/` holds a token.
- **Shell:** commands are written for Git Bash (the Bash tool); Windows PowerShell 5.1 has no `&&`. Keep the cwd on the uppercase `D:\` drive while running workflows or worktree agents (memory `workflow-worktree-drive-case`).
- **Frontend commands** (from `frontend/`): `npx vitest run <path>`, `npm run typecheck`, `npm run lint`, `npm run test`, `npm run build`. After `npm run build` run `git checkout -- tsconfig.app.tsbuildinfo tsconfig.node.tsbuildinfo` from `frontend/` (the build rewrites these tracked files; skip a path that is not tracked).
- **Tests** sit next to the file under test. Sidebar component tests share `components/layout/sidebar/sidebar-test-utils.tsx` (created in Phase 01; it exports functions only, so `react-refresh/only-export-components` stays quiet). Reset stores in `beforeEach`: `localStorage.clear()`, `useUiStore.setState({ sidebarCollapsed: false, sidebarGroupOpen: {}, pinnedNavPaths: {} })` (only the fields that exist in that phase), `useAuthStore.setState({ user: TEST_USER })`.
- **RED can be a type/module error**: a test importing a function or prop that does not exist yet fails to compile under Vitest; that counts as RED ("FAIL — `<name>` is not exported").
- **Baseline:** compare the `FAIL` lines of `npm run test` and the lint error/warning counts with `BASELINE.md` (Phase 01).
- **Team chat:** after each phase, post a short Vietnamese summary with `bash .claude/tools/gchat.sh "…"` from the repo root (memory `feedback-gchat-updates`).

## Final Verification

From `frontend/`:

```bash
npx vitest run src/components/layout src/stores
npm run test        # FAIL lines == BASELINE.md
npm run typecheck
npm run lint        # error/warning counts <= BASELINE.md
npm run build && git checkout -- tsconfig.app.tsbuildinfo tsconfig.node.tsbuildinfo
```

Manual (`npm run dev`, backend running): the checklist in Phase 06 Task 6.3 — admin in a ~700px-tall window (wide, collapsed, mobile drawer), SALES and WAREHOUSE accounts, dark mode.

## Rollback / Recovery

- Frontend only, no API or migration. Revert the merge commit of `feat/sidebar-nav-redesign` (or individual phase commits, newest first) and redeploy the frontend.
- Persisted state only gains `sidebarGroupOpen` and `pinnedNavPaths` under `qldonhang-ui-store` (version 1). An older build ignores unknown persisted keys, so a rollback needs no storage cleanup.
- The Phase 00 hotfix is one class change and is kept even if the redesign is reverted.
