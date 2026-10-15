# Execution Report — AppIcon registry (step 1 of option D)

> Plan: [SUMMARY.md](SUMMARY.md) · Branch: `feat/app-icon-registry` (from `main` `9d3e407`, not pushed) · Executed 2026-10-15 · Brainstorm: [261014-2115-notch-icon-analysis](../../../brainstorms/261014-2115-notch-icon-analysis/SUMMARY.md)

## Outcome

All four phases are complete. Every frontend icon now goes through `AppIcon` (`frontend/src/components/icons/`) and the business-named map `icon-registry.ts`. Colours come from ten `--icon-*` tone tokens, stroke width follows the size, and the glyphs ▲ ▼ ▾ ✕ ← → that acted as icons are icons. ESLint (`error`) and two guard tests block direct `lucide-react` imports and glyph icons. The home KPI cards have label icons.

| Check | Baseline (`9d3e407`) | Final (`51baeaf`) | Result |
|---|---|---|---|
| `npm run typecheck` | exit 0 | exit 0 | ✅ |
| `npm run lint` | 3 errors, 13 warnings | 3 errors, 13 warnings, 0 `no-restricted-imports` | ✅ same pre-existing jsx-a11y errors (`ui/table.tsx`, `customer-catalog-list.tsx`, `product-catalog-list.tsx`) |
| `npm run test` | 3 failed / 2114 | 3 failed / 2163 (2160 passed) | ✅ the same 3 baseline tests |
| `npm run build` | passes | passes | ✅ |
| `lucide-react` imports outside `components/icons/` | 78 source + 2 test files | none | ✅ |
| `▲ ▼ ▾ ✕` in non-test source | present | none | ✅ |

The plan's "0 error" lint target was not reachable: the 3 errors exist on `main` and are outside this plan.

## Commits per phase

| Phase | Commits | Notes |
|---|---|---|
| Plan | `5758e47` | plan + brainstorm committed on the branch |
| 01 Foundation | `2aa994d`, `1ecc1af`, `ad07155`, `477fa49`, `658951f`, fix `80745ff` | `80745ff`: AppIcon drops aria/role/absoluteStrokeWidth props; guard catches subpath/dynamic imports |
| 02 Layout and shared | `89cfbd4`, `bab0c8a`, `fb24d3c`, `892f673`, fix `51ebf1c` | `voucher-print-button.tsx` (PR #15) added to task 4 |
| 03 Pages | `c20a394`, `e5f8bbc`, `c9d24a3`, `f0cb4ff`, `4c37d1a` | LEGACY empty after `4c37d1a` |
| 04 Glyphs, KPI, finalize | `ee992d0`, `001c3f9`, `7d2cb03`, `9a9e253`, `6f975b1`, `39d8190`, `95f9f3b`, fix `6d45091`, docs `19b5fbc` | |
| Visual check fixes | `51baeaf` | see "Visual check" |

Each task had a sonnet implementer (RED line required), then an opus reviewer that checked spec compliance and code quality in its own detached checkout. Minor review notes were collected and applied in the fix commits. A final whole-branch review found no Critical or Important issues ("ready to merge").

## Visual check

Headless Chrome with a mocked API compared `9d3e407` with `19b5fbc` on 9 screens (24 screenshot pairs at 1440×900, plus a 390×844 home page). All sidebar icons were pixel-identical; every other difference was on the intended list, except:

1. **Revenue report header wrapped at 1440px.** The Excel button's new icon made the title, date and "Xuất Excel" wrap. Fixed in `51baeaf` (user chose to keep the icon): the title block no longer shrinks and only the controls group wraps. At 1440 the header is one row again (first card back at y 211). At 1366 it now wraps more cleanly than before the branch.
2. **Coloured icons on solid blue buttons were nearly invisible** (contrast ~1.4:1): "KT xác nhận" (emerald), "Thêm user" and "Thêm role" (cyan). The user chose to keep these icons white (`tone="none"`, the filled-surface rule). These three items are dropped from the plan's intentional-change list.

Cosmetic, accepted: executive stat tiles are 1px taller; delta arrows sit 2px from the number (was a text space); the Lãi gộp margin hint lost its `title` tooltip (the hint is not truncated); home KPI cards are 13px shorter because a "▼ 8.3%" badge no longer wraps.

Not covered by screenshots: opening-stock (cyan add, red delete) and busy-state spinners.

## Deviations from the plan

- **Plan refreshed before execution** for PR #15/#16: one more lucide file (`voucher-print-button.tsx`, phase 02 task 4), counts 78/80, line references.
- **Three intentional colour changes withdrawn** (see visual check item 2).
- **KPI label icons are size 18**, not the plan snippet's default 16, to match the approved mockup ("Bước 1").
- **Explicit tones that keep the old colour where no name has it:** `warning` with `tone={blocked ? 'danger' : 'warn'}` (confirm-required-dialog, negative-stock-dialog); `more` with `tone="muted"` (two row menus).
- **Raw colour class kept:** `branches-page.tsx` CalendarDays keeps `text-slate-600` (no slate-600 tone; muted is slate-500).
- **Additions beyond the task text:** `sidebar-group.tsx` chevrons; `quotation-list-page` "Thao tác khác" loader → `ButtonLoader` (convention: ButtonLoader inside buttons); the margin hint wraps arrow and number in a `whitespace-nowrap` inline-flex span; `product-catalog-detail` empty state got `gap-1`.
- **Docs:** the convention says lucide may be imported only inside `src/components/icons/`, not "only `icon-registry.ts`" (`app-icon.tsx` imports `LucideProps`). The guidance "keep action-bar button backgrounds restrained" was kept. `docs/SUMMARY.md` mentions the icon registry.
- **Phases 03 lanes ran one after another**, as the user chose, with each review running in parallel with the next implementer.

## Residual risks / follow-ups

- **Coloured icons on solid primary buttons (pre-existing, not changed):** 18 "Thêm…/Tạo…" buttons render a cyan `add` icon on the blue default button, and "Tính lại giá vốn" a blue `recalcCost` icon on blue. All were already coloured on `main`. Candidates: `tone="none"` everywhere, or outline buttons. Files: cash-reason, cash-voucher, credit-limit, customer, customer-quotations-section, debt-offset, opening-debt, fund-transfer, fund, payment-method, product-group, product, quotation-list, branches, stock-reason, stock-voucher, supplier, warehouse list pages; settings/recalc-cost-page.
- **Unused registry name** `branch` (Building2): kept for the branch sample-data plan.
- **Dark mode:** `--icon-*` tokens equal the light values; `--icon-muted` is darker than `--muted-foreground` in `.dark`. The app has no theme toggle yet.
- **Step 2 (Notch icon set)** stays behind the brainstorm's five entry criteria; switching now means editing `icon-registry.ts` only.
