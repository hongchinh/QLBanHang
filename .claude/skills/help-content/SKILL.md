---
name: help-content
description: Write or update the in-app help file of a screen (frontend/src/features/help/content/<key>.md). Use when a screen gets help for the first time, when a change touches a screen that has help, or when `npm run help:stale` lists it.
---

# Help Content

## Purpose

Keep the in-app help panel accurate: one Markdown file per app route in `frontend/src/features/help/content/<key>.md`. The rules below restate `docs/code-standard/conventions.md` ("User Help"); the conventions win if they differ.

## Inputs

One of:

- A help key (for example `stock-out.form`): work on `frontend/src/features/help/content/<key>.md`.
- A route (for example `/stock-out/new`): find the file whose `routes` lists it, or create one when the route is in `HELP_PENDING`.
- A diff: list the changed paths with `git diff <base>..HEAD --name-only`, then
  - match every changed path against the `sources` globs of every help file (`git grep -n "^sources:" -- frontend/src/features/help/content`); each file with a match needs a review;
  - check the routes added or removed in `frontend/src/app-routes.tsx` (`git diff <base>..HEAD -- frontend/src/app-routes.tsx`).

## Read First

- The route in `frontend/src/app-routes.tsx` and its page component.
- The page's components, its `labels.ts` files and its toolbar: bold labels must match the screen text exactly.
- The backend validators and service messages under the doc's backend `sources` (for "Lưu ý").
- The business rules in `docs/project-pdr/product-goals.md`.
- The brainstorm of the feature (`docs/SUMMARY.md` lists them).

## Write

- File name = key. Frontmatter: `key`, `title`, `routes`, `sources`, `related`, `updated`.
- Every `sources` glob must match tracked files: `git ls-files -- ':(glob)<glob>'` prints something. Otherwise `npm run help:stale` reports `sources-no-match`.
- Section "Dùng để": at most 2 sentences.
- Section "Các bước": 3-7 steps `N. {#id} text`. Each starts with a verb and fits on one line. Keep `{#id}` stable while the meaning stays; use a new id only for a new step.
- Bold labels exactly as on screen (`**Lưu tạm**`). Inline Markdown is only `**label**` and `` `key` ``; no links.
- Section "Lưu ý": only messages the user really sees.
- Section "Phím tắt": only shortcuts the screen handles.
- `related`: only existing keys; add it in both directions when it helps.
- `updated` = today (`date +%Y-%m-%d`).

## Register

Remove the file's routes from `frontend/src/features/help/help-pending.ts` (the list only shrinks).

## Check

```bash
cd frontend
npm run test -- --testTimeout=30000 --maxWorkers=3 src/features/help
git grep -nF "<label>" -- <sources>      # every bold label is found, or explain the dynamic label in the commit body
npm run help:stale                        # run again after committing
```

## Videos

When `frontend/src/features/help/videos.json` has the key and a step was added, removed or reworded, list those step ids as "cần quay lại" in the final report (the round 3 video tooling re-records them). Never edit `videos.json` by hand.
