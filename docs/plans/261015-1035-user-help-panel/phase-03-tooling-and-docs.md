# Phase 03 — Maintenance tooling and docs

**Status:** [ ] pending
**Complexity:** M

## Objective

Give the team the tools that keep help in step with the code: `npm run help:stale` (warning only), a PR checkbox, the `help-content` project skill that drafts and updates help files, and the conventions that tell every session the rules.

## Files

- `frontend/src/features/help/stale.ts` + `stale.test.ts` (new)
- `frontend/scripts/help-stale.ts` + `help-stale.test.ts` (new) · `frontend/package.json` (modify — one script)
- `.github/pull_request_template.md` (new)
- `.claude/skills/help-content/SKILL.md` (new)
- `docs/code-standard/conventions.md`, `docs/codebase/directory-structure.md`, `docs/architecture/system-architecture.md`, `docs/SUMMARY.md` (modify)

## Tasks

### Task 3.1 — Stale rules (pure)

Rules (brainstorm §3 point 3, P6). Commits are `CommitStamp`s (SUMMARY contract): ordering uses `at` (epoch seconds, so commits made in different time zones compare correctly), comparisons with `updated` and `recordedAt` use `day` (the committer's local day).

| Kind | Fires when | `detail` (Vietnamese) |
|---|---|---|
| `sources-no-match` | once per glob in `unmatchedSources` | `Nguồn "<glob>" không khớp file nào, sửa lại sources` |
| `sources-newer-than-help` | `sources` and `helpFile` set and `sources.at > helpFile.at` | `Code đổi ngày <sources.day>, hướng dẫn sửa lần cuối ngày <helpFile.day>` |
| `updated-older-than-commit` | `helpFile` set and `updated < helpFile.day` | `updated (<updated>) cũ hơn lần commit cuối (<helpFile.day>)` |
| `sources-newer-than-video` | a video, `sources` set and `sources.day > video.recordedAt` | `Code đổi ngày <sources.day> sau khi quay video ngày <recordedAt>` |
| `narration-changed` | a video step exists and its `hash` ≠ the step's current hash | `Bước <id> đã sửa câu sau khi quay` |
| `step-not-in-video` | a video and a current step id missing from `video.steps` | `Bước <id> chưa có trong video` |

Warnings come out in the table's order. `formatStaleReport([])` → `Không có cảnh báo.`; otherwise `<n> cảnh báo:` followed by one line `- <key>: <detail>` per warning, in input order.

1. Write the failing test `src/features/help/stale.test.ts`: one case per kind that fires and one that does not (equal `at`, `null` stamps, no video, empty `unmatchedSources`); `sources.at > helpFile.at` while `sources.day < helpFile.day` (a commit from a later time zone) still fires `sources-newer-than-help`; a case with two kinds at once; both `formatStaleReport` outputs.
2. Run `npm run test -- --testTimeout=30000 --maxWorkers=3 src/features/help/stale.test.ts` → FAIL: `Failed to resolve import "./stale"`.
3. Implement `src/features/help/stale.ts` per the SUMMARY contract, with only `import type` lines (P7).
4. Run → PASS.
5. Commit: `feat(frontend): help stale rules`.

### Task 3.2 — `npm run help:stale`

1. Write the failing smoke test `frontend/scripts/help-stale.test.ts`. Keep the default jsdom environment: `src/test/setup.ts` touches `Element.prototype` and throws under `// @vitest-environment node`, and `child_process` works under jsdom. The script makes about three `git` calls per help file, so the test gets its own 60 s timeout (a plain `npm test` uses the 5 s default):
   ```ts
   import { execFileSync } from 'node:child_process';
   import { fileURLToPath } from 'node:url';
   import { dirname, join } from 'node:path';
   import { describe, expect, it } from 'vitest';

   const frontend = join(dirname(fileURLToPath(import.meta.url)), '..');

   describe('help:stale', () => {
     it('runs, reads every help file and exits 0', () => {
       const out = execFileSync(process.execPath, ['scripts/help-stale.ts'], { cwd: frontend, encoding: 'utf8' });
       expect(out).toMatch(/Không có cảnh báo\.|\d+ cảnh báo:/);
     }, 60_000);
   });
   ```
2. Run `npm run test -- --testTimeout=30000 --maxWorkers=3 scripts/help-stale.test.ts` → FAIL: `Cannot find module …/scripts/help-stale.ts`.
3. Implement `frontend/scripts/help-stale.ts` (runs under Node type stripping; imports with `.ts` extensions; no other dependency):
   ```ts
   import { execFileSync } from 'node:child_process';
   import { readFileSync, readdirSync } from 'node:fs';
   import { dirname, join } from 'node:path';
   import { fileURLToPath } from 'node:url';
   import { parseHelpDoc, stepHash } from '../src/features/help/parse-help.ts';
   import { formatStaleReport, staleWarnings, type CommitStamp, type StaleWarning } from '../src/features/help/stale.ts';
   import type { HelpVideo } from '../src/features/help/types.ts';

   // Lists help that may be out of date (warnings only; exit 1 only for an unreadable help file).
   const here = dirname(fileURLToPath(import.meta.url));
   const helpDir = join(here, '../src/features/help');
   const repoRoot = execFileSync('git', ['rev-parse', '--show-toplevel'], { cwd: here, encoding: 'utf8' }).trim();
   const git = (args: string[]) => execFileSync('git', args, { cwd: repoRoot, encoding: 'utf8' }).trim();
   // %ct orders commits across time zones; %cs is the committer's local day, compared with `updated`.
   const lastCommit = (pathspecs: string[]): CommitStamp | null => {
     const out = git(['log', '-1', '--format=%ct %cs', '--', ...pathspecs]);
     if (!out) return null;
     const [at, day] = out.split(' ');
     return { at: Number(at), day };
   };
   const videos = JSON.parse(readFileSync(join(helpDir, 'videos.json'), 'utf8')) as Record<string, HelpVideo>;

   const warnings: StaleWarning[] = [];
   for (const file of readdirSync(join(helpDir, 'content')).filter((f) => f.endsWith('.md')).sort()) {
     const key = file.slice(0, -3);
     const { doc, errors } = parseHelpDoc(readFileSync(join(helpDir, 'content', file), 'utf8'), key);
     if (!doc) { console.error(errors.join('\n')); process.exitCode = 1; continue; }
     warnings.push(...staleWarnings({
       key,
       updated: doc.updated,
       helpFile: lastCommit([`frontend/src/features/help/content/${file}`]),
       sources: lastCommit(doc.sources.map((s) => `:(glob)${s}`)),
       unmatchedSources: doc.sources.filter((s) => git(['ls-files', '--', `:(glob)${s}`]) === ''),
       steps: doc.steps.map((s) => ({ id: s.id, hash: stepHash(s.text) })),
       video: videos[key],
     }));
   }
   console.log(formatStaleReport(warnings));
   ```
   Add `"help:stale": "node scripts/help-stale.ts"` to `frontend/package.json` `scripts` (after `test:ui`).
4. Run the test → PASS; run `npm run help:stale` by hand and paste its output into the commit body; `npm run lint` clean.
5. Commit: `feat(frontend): npm run help:stale`.

### Task 3.3 — PR template

1. Create `.github/pull_request_template.md`:
   ```md
   ## Thay đổi

   -

   ## Kiểm tra

   - [ ] `npm run test` / `dotnet test` đã chạy
   - [ ] Đổi giao diện hay thao tác của một màn hình → đã cập nhật file hướng dẫn trong `frontend/src/features/help/content/` (và quay lại video nếu các bước đổi)
   - [ ] Thêm màn hình mới → đã có file hướng dẫn, hoặc route nằm trong `HELP_PENDING`
   ```
2. Commit: `docs: pull request template with the help checklist`.

### Task 3.4 — Project skill `help-content`

1. Create `.claude/skills/help-content/SKILL.md` with this frontmatter and body (English, the house style of `.claude/skills/*`):
   ```md
   ---
   name: help-content
   description: Write or update the in-app help file of a screen (frontend/src/features/help/content/<key>.md). Use when a screen gets help for the first time, when a change touches a screen that has help, or when `npm run help:stale` lists it.
   ---
   ```
   Body sections, each with concrete commands:
   - **Inputs**: a help key, a route, or a diff (`git diff <base>..HEAD --name-only`). From a diff, find the affected help files by matching the changed paths against every file's `sources` globs, and the routes added or removed in `frontend/src/app-routes.tsx`.
   - **Read first**: the route in `app-routes.tsx` and its page; the page's components, `labels.ts` files and toolbar; the backend validator and service messages under the doc's backend `sources`; the business rules in `docs/project-pdr/product-goals.md`; the brainstorm of the feature (`docs/SUMMARY.md` lists them).
   - **Write** (rules copied from `docs/code-standard/conventions.md` "User Help"): file name = key; frontmatter fields, with every `sources` glob matching tracked files (`git ls-files -- ':(glob)<glob>'` prints something; `help:stale` reports `sources-no-match` otherwise); "Dùng để" at most 2 sentences; 3–7 steps, each starts with a verb, one line, `{#id}` kept stable when the meaning stays (change the id only when the step is new); bold labels exactly as on screen; "Lưu ý" only for messages the user really sees; shortcuts only those the screen handles; `related` only existing keys, added in both directions when it helps; `updated` = today.
   - **Register**: remove the file's routes from `frontend/src/features/help/help-pending.ts`.
   - **Check**: `cd frontend && npm run test -- --testTimeout=30000 --maxWorkers=3 src/features/help` passes; every bold label is found by `git grep -nF "<label>" -- <sources>` or is explained (dynamic label) in the commit body; `npm run help:stale` after committing.
   - **Videos**: when `videos.json` has the key and a step was added, removed or reworded, list those step ids as "cần quay lại" in the final report (round 3 tooling re-records them).
2. Commit: `docs: help-content skill`.

### Task 3.5 — Conventions and docs

1. `docs/code-standard/conventions.md`: add a section `## User Help` after `## Demo Branches And Background Jobs` with these bullets:
   - Every app route (`frontend/src/app-routes.tsx`, leaf routes except `/login`, `/403`, `/404`, redirects and `*`) has exactly one help file in `frontend/src/features/help/content/<key>.md`, or is listed in `HELP_PENDING` (`features/help/help-pending.ts`), which only shrinks. `help-guard.test.ts` enforces it, plus valid files, existing `related` keys and `videos.json` steps. A new route goes into `app-routes.tsx`, never back into `App.tsx`.
   - Key: the route without `/` and without `:param` segments, joined with `-`; `/` is `home`; a list/form pair gets `.list` / `.form`. One file may own several routes (a detail page with its parent).
   - File format and writing rules (the brainstorm's §2, restated): frontmatter `key`, `title`, `routes`, `sources` (repo-relative globs, backend allowed), `related`, `updated`; sections `Dùng để` (≤ 2 sentences), `Các bước` (3–7 one-line steps `N. {#id} text`, verbs first, stable ids), `Lưu ý`, `Phím tắt`; inline only `**label**` and `` `key` ``; no links.
   - A change to a screen's UI or behaviour updates its help file in the same PR (the PR template asks). Run `npm run help:stale` before a release. Use the `help-content` skill to draft.
   - UI: `HelpPanel` (rendered by `AppLayout`), `HelpButton` and `FirstVisitHint` (rendered by `AppHeader`), F1 through `useHelpShortcut`; state in `ui-store` (`helpOpen`, `helpHintSeen`, `helpHintsOff`). The content is one lazy chunk (`content-index.ts`); import it only through `load-help.ts`. F1 does nothing while a modal dialog is open (`DIALOG_SELECTOR` outside `POPPER_WRAPPER_SELECTOR`, `lib/focus.ts`); popovers never block it.
   - `videos.json` is written by the round-3 video tool, never by hand; a step's hash is `stepHash` from `parse-help.ts`.
2. `docs/codebase/directory-structure.md`, section "Frontend Structure": add `src/app-routes.tsx` (the route tree), `src/features/help/` (parser, registry, content, `videos.json`, `HELP_PENDING`, guard tests) and `src/components/help/` (panel, button, hint), and `frontend/scripts/help-stale.ts`; in "Route Entry Points" change the "Frontend routes" row to `../../frontend/src/app-routes.tsx` (rendered by `App.tsx`).
3. `docs/architecture/system-architecture.md`, section "Frontend Layering": add one paragraph: the help panel is route-keyed (`matchRoutes` over the help files' routes), lives at layout level, loads its content as one lazy chunk precached by the service worker, has no backend, and links to YouTube for videos.
4. `docs/SUMMARY.md`: append to the description of the `code-standard/conventions.md` row `, and the user help rules (help file per route, `HELP_PENDING`, `help:stale`)`; append to the `codebase/directory-structure.md` row `, the route tree file and the help module`.
5. Commit: `docs: user help conventions and structure`.

## Verification

```bash
cd "$WT/frontend"
npm run test -- --testTimeout=30000 --maxWorkers=3 src/features/help scripts
npm run help:stale
npm run typecheck && npm run lint
```

## Exit Criteria

- `npm run help:stale` runs from `frontend/`, exits 0 with the current content and prints `Không có cảnh báo.` or the warnings; for the committed files it prints no `không khớp file nào` line (`sources-no-match`).
- The PR template, the `help-content` skill and the conventions exist and name the same paths as the code.
