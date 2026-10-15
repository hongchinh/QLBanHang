# In-app help panel per screen, round 1 (Khung Hướng dẫn theo màn hình, đợt 1)

> Created: 2026-10-15 10:35 · Based on `qlbanhang/main` at `1a87763` (PR #20, docs only, on top of PR #19 branch sample data) · Source design: [brainstorm SUMMARY](../../../brainstorms/261015-1001-user-help-guide/SUMMARY.md) (approved, reviewed against code 2026-10-15), [section-02 review](../../../brainstorms/261015-1001-user-help-guide/section-02-review.md), [mockup bản 2](../../../brainstorms/261015-1001-user-help-guide/mockup-01-help-panel.html) (option C) · Round 3 (video) is a separate plan. · Reviewed against the code 2026-10-15: [REVIEW.md](REVIEW.md) (3 high, 6 medium, 10 low, all applied).

## Goal

Every screen gets a **?** button and **F1** that open a help panel for the screen in view: what the screen is for, the main steps, notes, shortcuts and related help. A first-time visitor sees a small floating hint under the **?** button. The help text is one Markdown file per screen in the repo, guarded by tests so a new screen without help, a broken link or a stale video fails `npm test`, and `npm run help:stale` lists help that may be out of date. Round 1 writes the help for 12 screens; the other routes stay listed in `HELP_PENDING`.

## Scope

**In scope**

- `App.tsx`: move the `<Route>` tree into `src/app-routes.tsx` (`appRoutes`), so tests can read the routes with `createRoutesFromChildren`.
- `src/features/help/`: types, Markdown parser, route flattening, registry (`matchRoutes`), `videos.json` (empty) and helpers, content loader (one lazy chunk, retry after a failed load), `HELP_PENDING`, related and hint logic, F1 hook, `stale.ts`, the guard tests and the content files. `lib/focus.ts` gets `POPPER_WRAPPER_SELECTOR`.
- `src/components/help/`: `HelpInline`, `HelpDocView`, `HelpPanel`, `HelpButton`, `FirstVisitHint`.
- `ui-store`: `helpOpen`, `helpHintSeen`, `helpHintsOff` and their actions. `hooks/useMediaQuery.ts`. Four registry icons.
- Layout: `AppLayout` renders the panel (pushes the content from 1600 px, overlays below, bottom sheet below 768 px) and the F1 hook; `AppHeader` renders the button and the hint; `HeaderUserMenu` gets a mobile-only "Hướng dẫn màn hình này" item.
- `frontend/scripts/help-stale.ts` and `npm run help:stale`; `.github/pull_request_template.md`; the `help-content` project skill; docs (`conventions.md`, `directory-structure.md`, `system-architecture.md`, `docs/SUMMARY.md`).
- Content for 12 screens: `quotations.list`, `quotations.form`, `customers.list`, `stock-in.form`, `stock-out.form`, `receipts.form`, `payments.form`, `fund-transfers.form`, `debt-matching`, `inventory-opening-stock`, `debt-opening`, `dashboard-executive`.
- A real-browser check (real backend on a scratch database) at 1280, 1700, 390 and 360 px.

**Out of scope** (brainstorm non-goals and later rounds)

- Videos, `tools/help-videos`, the YouTube channel (round 3). `videos.json` stays `{}`; the panel hides the video block and the timestamps.
- Help for the other routes (round 2), tours, the public help site, process videos, API upload (round 4).
- Backend changes, per-permission steps, an embedded player, Ctrl K help search, per-tab or per-dialog help, feedback buttons.

## Decisions made while planning (in addition to the brainstorm)

| # | Topic | Decision |
|---|---|---|
| P1 | Workspace | The main checkout `D:\Projects\QLBanHang` is shared with other sessions. All work happens in the worktree `.claude/worktrees/user-help` on `feat/user-help-panel`, created from `qlbanhang/main` in Phase 00. Never switch branches or commit in the main checkout. No backend tests run (frontend only); Phase 05 uses its own scratch database `qldonhang_help_browser`. |
| P2 | Paths | The brainstorm's `src/help/content/` and `src/help/videos.json` become `src/features/help/content/` and `src/features/help/videos.json`, next to the module's code (feature-folder convention). UI components live in `src/components/help/`. |
| P3 | Mobile header (R-M6) | Measured from the classes at 360 px: menu 52 + logo 56 + padding 16 + search 44 + bell 44 + user 48 + gaps 12 = 272 px, so the branch switcher has about 88 px today and about 40 px with one more 44 px button. Decision: the **?** button is `hidden md:flex`; below `md` the user menu has "Hướng dẫn màn hình này" (`md:hidden`) and the first-visit hint anchors to the avatar. Phase 05 verifies at 360 and 390 px. |
| P4 | Related help (R-L6) | A related doc is listed when `useCanAccessRoute()` allows its `firstStaticPath` (its first route with every `:param` replaced by `_`). This mirrors the `ProtectedRoute` permissions, which `findActiveItem` would not (a form route needs the create permission). Clicking a related entry shows that doc in the panel, with "Quay lại"; it does not navigate. |
| P5 | Step hash | `stepHash` = FNV-1a 32-bit over `narrationText(step.text)`, 8 lowercase hex chars: sync, no dependency, same in Node and the browser. The round-3 video tool must use `stepHash` from `parse-help.ts`. |
| P6 | Video recording date | A `videos.json` entry carries `recordedAt` (ISO date). `help:stale` compares the last commit of the doc's `sources` with it, instead of reading per-entry git history. |
| P7 | Node runs the parser | `parse-help.ts` has only `import type` lines, so `node scripts/help-stale.ts` (Node type stripping, Node ≥ 22.18; the machine has 24.18) can import it with the `.ts` extension (`allowImportingTsExtensions` is on). The same holds for `stale.ts`. |
| P8 | Loading | One dynamic chunk (`content-index.ts`, all files parsed) cached in a module promise; `useHelpIndex()` returns `null` until it resolves. No React Query, so the header and layout tests need no new provider. |
| P9 | F1 rule | Plain F1 (no modifier, not a repeat) calls `preventDefault` and toggles the panel, unless a **modal** dialog is open: an element matching `DIALOG_SELECTOR` (`lib/focus.ts`) that is not inside a Radix popper wrapper (`POPPER_WRAPPER_SELECTOR` = `[data-radix-popper-content-wrapper]`, added to `lib/focus.ts`). Popovers (grid popovers, typeahead lists, the first-visit hint) do not block F1. This is the brainstorm rule (§1 "Phím và focus", §7) and the rule `inModalDialog` of `stock-voucher-form-page.tsx:126-133` already applies. |
| P10 | Hint | Radix `Popover` (`components/ui/popover.tsx`) with `PopoverAnchor virtualRef` (supported by the installed `@radix-ui/react-popper`), `onOpenAutoFocus` prevented so the form keeps its focus, `onFocusOutside` prevented so moving the focus between fields (Tab, Enter, a late autofocus) does not close it, `data-help-hint` on the content. The dismissals of the brainstorm (Ẩn, Xem hướng dẫn, Esc, a click outside, opening the panel) close it and mark the screen seen; a focus change alone does neither. |
| P11 | Related keys in round 1 | `related` may name only keys that have a file. The sample of the brainstorm (`credit-limits`) is replaced by existing keys. Round 2 adds links as files land. |
| P12 | Overlapping plans | The cash voucher form redesign (`docs/plans/261015-0926-…`) and the payment QR plan (`docs/plans/261015-0919-…`) change the receipt, payment, stock and quotation forms. Both were already running when this plan was reviewed (`feat/voucher-payment-qr-fe`: 17 commits, edits `cash-voucher-form-page.tsx` and `stock-voucher-form-page.tsx`; `feat/cash-voucher-form-redesign`: 14 commits, 29 frontend files), so this plan will most likely merge second and must bring the help in line. Phase 04 Task 4.0 merges `qlbanhang/main` before writing content; Phase 05 Task 5.2 merges it again and rewrites every round-1 help file that `help:stale` reports as `sources-newer-than-help`. Neither branch touches `App.tsx` so far; a route added upstream moves into `app-routes.tsx`. |

## Assumptions

- The brainstorm SUMMARY (after its review) is the approved spec. Where this plan restates a rule, the brainstorm section is named; if the two differ, ask before changing either.
- Node ≥ 22.18 runs `.ts` files without flags (type stripping). PostgreSQL on `localhost:5432` (`postgres`/`1`); `psql` at `/c/Program Files/PostgreSQL/18/bin/psql`. HTTP clients (`curl`, `wget`) are denied; the browser check uses Playwright from the main checkout's root `node_modules` (`playwright` 1.60 with Chromium in `%LOCALAPPDATA%\ms-playwright`) through `createRequire`, or Edge with `channel: 'msedge'`.
- `qlbanhang/main` may move while this runs; Phase 00 and Phase 05 check `git ls-remote qlbanhang main`. `git fetch`/`git push` fail inside the sandbox: retry the one command with the sandbox disabled.
- Content describes the screens as they are on the base commit.

## Risks

- **Route tree move.** `App.tsx` loses about 600 lines to `app-routes.tsx`. Mitigated: the move is mechanical (cut and paste, no JSX change), `App.routes.test.tsx` and `App.shell.test.tsx` rerun, and the guard test reads the same tree the app renders.
- **Full-height pages.** The hint is a portal popover outside the layout flow (R-H2); the panel overlay is absolute inside the layout grid, and the push mode adds a grid column only from 1600 px. Phase 05 measures the stock-out form's totals bar with the hint open.
- **Focus theft.** A first-visit hint that takes the focus would break keyboard-first forms (the cash form focuses the partner code). Mitigated by P10 and a test that asserts the focused input keeps the focus.
- **F1 conflicts.** Browsers open their help page on F1; `preventDefault` stops it in Chromium and Firefox. The app has no other F1 binding (`git grep "'F1'" -- frontend/src` is empty on the base).
- **Content drift.** P12 plans change four screens of round 1 and are already in flight. Phase 04 Task 4.0 and Phase 05 Task 5.2 merge `qlbanhang/main`, rewrite the help that `help:stale` flags and rerun the browser scenarios of a screen the merge changed.
- **Dev service worker.** `vite.config.ts` enables the PWA service worker in dev (`devOptions.enabled`), and `sw.ts` fetches `/api/lookups/`, `/api/banks` and `/api/settings/branding` itself. Those fetches bypass Playwright routing and would reach the developer backend on 5050 through the Vite proxy. Phase 05 blocks service workers in every browser context.
- **Lint on the panel.** `jsx-a11y` rejects key handlers on non-interactive elements; the panel registers Esc with `addEventListener` in an effect instead of `onKeyDown`.
- **Machine load.** Other sessions run test suites; run at most two test-running tracks and use the "under load" vitest flags.

## Phases

- [x] Phase 00 — Worktree, branch, baseline (S) — `phase-00-baseline.md`
- [x] Phase 01 — Help data layer, guard tests and the reference doc (L) — `phase-01-data-layer.md`
- [x] Phase 02 — Help UI: panel, button, F1, first-visit hint (L) — `phase-02-help-ui.md`
- [x] Phase 03 — Maintenance tooling and docs (M) — `phase-03-tooling-and-docs.md`
- [x] Phase 04 — Content for the other 11 screens (M) — `phase-04-content.md`
- [x] Phase 05 — Real-browser check and final verification (M) — `phase-05-browser-check-and-verification.md`

Order: 00 → 01 → 02 → 03 → 04 → 05. After Phase 01, Phase 03 Tasks 3.1–3.2 (`stale.ts`, the CLI) touch no file of Phase 02 and may run in a second worktree on `feat/user-help-stale`, merged `--no-ff` before Phase 03's remaining tasks. Phase 04 starts by merging `qlbanhang/main` (P12), so it runs alone after Phase 03. Run one phase per request (session limits) unless the user asks for several.

## Shared contracts (frozen in the phase named; later phases only use them)

### Data layer (Phase 01)

```ts
// frontend/src/features/help/types.ts
export interface HelpStep { id: string; text: string }            // text keeps the inline markers (**…**, `…`)
export interface HelpShortcut { keys: string; label: string }      // "- `Ctrl S` Lưu" → { keys: 'Ctrl S', label: 'Lưu' }
export interface HelpDoc {
  key: string; title: string; routes: string[]; sources: string[]; related: string[];
  updated: string;                                                 // YYYY-MM-DD
  purpose: string; steps: HelpStep[]; notes: string[]; shortcuts: HelpShortcut[];
}
export type InlineNode = { kind: 'text' | 'strong' | 'kbd'; text: string };
export interface HelpVideoStep { at: number; hash: string }        // seconds from the start; stepHash at recording
export interface HelpVideo {
  youtubeId: string; title: string; duration: number;              // seconds
  recordedAt: string;                                              // YYYY-MM-DD
  steps: Record<string, HelpVideoStep>;                            // by step id
}

// frontend/src/features/help/parse-help.ts   (only `import type` lines: Node runs it, P7)
export function parseHelpDoc(raw: string, fileKey: string): { doc: HelpDoc | null; errors: string[] };
export function parseInline(text: string): InlineNode[];
export function narrationText(text: string): string;  // markers and {#id} removed, whitespace collapsed, trimmed, NFC
export function stepHash(text: string): string;       // FNV-1a 32-bit of narrationText(text), 8 lowercase hex chars

// frontend/src/features/help/route-paths.ts
export const ROUTES_WITHOUT_HELP: readonly string[];  // ['/login', '/403', '/404']
export function flattenRoutePaths(routes: RouteObject[]): string[];  // sorted, unique; see Task 1.1

// frontend/src/app-routes.tsx
export const appRoutes: JSX.Element;                   // the fragment App.tsx renders inside <Routes>

// frontend/src/features/help/registry.ts
export interface HelpIndex {
  docs: readonly HelpDoc[];                            // sorted by key
  byKey: ReadonlyMap<string, HelpDoc>;
  resolve(pathname: string): HelpDoc | null;           // matchRoutes over every doc route; static beats :param
}
export function buildHelpIndex(docs: readonly HelpDoc[]): HelpIndex;
export function firstStaticPath(doc: HelpDoc): string; // routes[0] with each ':param' segment replaced by '_'

// frontend/src/features/help/videos.ts
export const helpVideos: Readonly<Record<string, HelpVideo>>;      // from videos.json ({} in round 1)
export function videoFor(key: string, videos?: Readonly<Record<string, HelpVideo>>): HelpVideo | undefined;
export function youtubeUrl(youtubeId: string, atSeconds?: number): string; // https://www.youtube.com/watch?v=<id>[&t=<n>s]
export function youtubeThumb(youtubeId: string): string;                   // https://i.ytimg.com/vi/<id>/mqdefault.jpg
export function formatDuration(seconds: number): string;                   // 155 → '2:35', 8 → '0:08'

// frontend/src/features/help/load-help.ts   (a tiny module-level store read with useSyncExternalStore)
export function loadHelpIndex(): Promise<HelpIndex>;   // import('./content-index'), cached module promise; a failed
                                                       // import clears the cache (the next call retries) and sets failed
export function useHelpIndex(): HelpIndex | null;      // null until loaded; starts loadHelpIndex() on mount
export function useHelpLoadFailed(): boolean;          // true after a failed load, false again once a retry succeeds
export function resetHelpIndexCacheForTests(): void;   // clears the promise, the loaded index and the failed flag

// frontend/src/features/help/help-pending.ts
export const HELP_PENDING: readonly string[];          // app routes without a help file; only shrinks

// ui-store additions (frontend/src/stores/ui-store.ts)
helpOpen: boolean;                                     // not persisted
helpHintSeen: Record<string, string[]>;                // per user id, help keys whose hint was dismissed (persisted)
helpHintsOff: Record<string, boolean>;                 // per user id (persisted)
openHelp(): void; closeHelp(): void; toggleHelp(): void;
markHelpHintSeen(userId: string, key: string): void;   // no duplicates
turnOffHelpHints(userId: string): void;
```

### UI (Phase 02)

```ts
// frontend/src/hooks/useMediaQuery.ts
export function useMediaQuery(query: string, fallback?: boolean): boolean;  // fallback (default false) without matchMedia

// frontend/src/features/help/related.ts
export function accessibleRelated(doc: HelpDoc, index: HelpIndex, canAccess: (path: string) => boolean): HelpDoc[];
export function docsInSameGroup(pathname: string, groups: readonly NavGroup[], index: HelpIndex,
  canAccess: (path: string) => boolean): HelpDoc[];

// frontend/src/features/help/hint.ts
export function shouldShowHint(o: { userId: string | undefined; doc: HelpDoc | null; helpOpen: boolean;
  seen: Record<string, string[]>; off: Record<string, boolean> }): boolean;

// frontend/src/features/help/use-first-visit-hint.ts
export function useFirstVisitHint(): { visible: boolean; doc: HelpDoc | null;
  dismiss(): void; openFromHint(): void; turnOff(): void };

// frontend/src/features/help/use-help-shortcut.ts
export function useHelpShortcut(): void;

// frontend/src/components/help/*
export function HelpInline(props: { text: string }): JSX.Element;
export function HelpDocView(props: { doc: HelpDoc; video?: HelpVideo; related: HelpDoc[];
  onOpenRelated(key: string): void }): JSX.Element;
export function HelpPanel(): JSX.Element | null;       // rendered by AppLayout
export const HelpButton: React.ForwardRefExoticComponent<{ showDot: boolean } & React.RefAttributes<HTMLButtonElement>>;
export function FirstVisitHint(props: { hint: ReturnType<typeof useFirstVisitHint>;
  desktopAnchorRef: React.RefObject<HTMLElement>; mobileAnchorRef: React.RefObject<HTMLElement> }): JSX.Element | null;
// AppHeader calls useFirstVisitHint() once and passes it to HelpButton (showDot) and FirstVisitHint.
// HeaderUserMenu gets `triggerRef?: React.Ref<HTMLButtonElement>` and `onOpenHelp?: () => void`.
```

### Tooling (Phase 03)

```ts
// frontend/src/features/help/stale.ts   (only `import type` lines, P7)
export interface CommitStamp {
  at: number;                      // committer time, seconds since the epoch (git %ct): orders commits across time zones
  day: string;                     // committer's local day, YYYY-MM-DD (git %cs): compared with `updated` / `recordedAt`
}
export interface StaleInput {
  key: string; updated: string;
  helpFile: CommitStamp | null;    // last commit of the doc file (null = not committed)
  sources: CommitStamp | null;     // last commit touching any `sources` glob (null = none)
  unmatchedSources: string[];      // `sources` globs that match no tracked file
  steps: { id: string; hash: string }[];
  video?: HelpVideo;
}
export type StaleKind = 'sources-newer-than-help' | 'sources-newer-than-video' | 'narration-changed'
  | 'step-not-in-video' | 'updated-older-than-commit' | 'sources-no-match';
export interface StaleWarning { key: string; kind: StaleKind; detail: string }
export function staleWarnings(input: StaleInput): StaleWarning[];
export function formatStaleReport(warnings: readonly StaleWarning[]): string;  // 'Không có cảnh báo.' when empty
```

## Final Verification

Run in the worktree after Phase 05:

```bash
cd "$WT/frontend"
npm run typecheck && npm run lint
npm run test -- --testTimeout=30000 --maxWorkers=3
npm run build && ls dist/assets | grep -E '^content-index-.*\.js$'      # the help chunk is separate
grep -l "Ghi nhận hàng xuất khỏi kho" dist/assets/*.js                  # only the content-index chunk
grep -c "assets/content-index-" dist/sw.js                              # ≥ 1: the chunk is precached (offline help)
npm run help:stale                                                      # exits 0; lists warnings or 'Không có cảnh báo.'
```

Then the browser check of Phase 05 Task 5.1 and the hygiene checks of "Executor conventions". The test suite must show no failure that is not listed in `BASELINE.md`.

## Rollback / Recovery

- All work is on `feat/user-help-panel`; `git revert -m 1 <merge>` of the PR removes the panel. No backend change, no migration, no data change.
- The only stored state is three new `ui-store` fields in `localStorage` (`qldonhang-ui-store`); an older frontend ignores them.
- If the route-tree move causes trouble on its own, revert its commit (Task 1.1): the help code then fails its guard test, so revert the whole branch instead of that commit alone once later tasks exist.

## Executor conventions

- `$WT` is `/D/Projects/QLBanHang/.claude/worktrees/user-help` (uppercase `D:`). Every path in the phases is relative to `$WT`. Commands are Git Bash.
- Implementer agents: sonnet. Each reports the RED failure line of its first test run. Reviewers run in their own detached checkout (`git worktree add "$TEMP/rev-<sha>" <sha> --detach`, with a `frontend/node_modules` junction to `$WT/frontend/node_modules`, deleted with `(Get-Item <path>).Delete()` before removing the worktree).
- Commit messages: Conventional Commits (`feat(frontend): …`, `test(frontend): …`, `docs: …`), ending with the `Co-Authored-By` trailer the session's attribution reminder gives.
- After each phase check for leaks: `git diff <base> --numstat` against `git diff <base> --numstat --ignore-cr-at-eol` (line endings: the index is LF, the checkout CRLF for most files), and `git log <base>.. -- frontend/tsconfig.app.tsbuildinfo` (must be empty; if an implementer committed it, `git checkout <base> -- frontend/tsconfig.app.tsbuildinfo` in a fix commit).
- Git Bash `sed -i` rewrites CRLF files as LF: use the Edit tool for source edits, or restore with `perl -pi -e 's/\r?\n/\r\n/' <file>`.
- Frontend tests under load: `npm run test -- --testTimeout=30000 --maxWorkers=3 <path>`; a 5 s timeout counts as a failure only after a rerun with the longer timeout.
- UI strings are Vietnamese with diacritics, exactly as written in the tasks.
- Mirror each phase result to the team Google Chat (`bash .claude/tools/gchat.sh "…"`, Vietnamese, from the main checkout); never commit `.claude/tools/`.
