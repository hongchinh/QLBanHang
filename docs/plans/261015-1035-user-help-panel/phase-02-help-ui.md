# Phase 02 — Help UI: panel, button, F1, first-visit hint

**Status:** [ ] pending
**Complexity:** L

## Objective

Show the help: the panel in the layout (push from 1600 px, overlay from 768 px, bottom sheet below), the **?** button in the header (from `md`), the user-menu item (below `md`), F1, and the floating first-visit hint that never takes the focus. Mockup reference: `docs/brainstorms/261015-1001-user-help-guide/mockup-01-help-panel.html`, option C and "Trên điện thoại".

## Files

- `frontend/src/components/icons/icon-registry.ts` + `icon-registry.test.ts` (modify)
- `frontend/src/hooks/useMediaQuery.ts` + `useMediaQuery.test.ts` (new)
- `frontend/src/features/help/related.ts` + `related.test.ts` (new)
- `frontend/src/features/help/hint.ts` + `hint.test.ts` (new)
- `frontend/src/lib/focus.ts` (modify — `POPPER_WRAPPER_SELECTOR`)
- `frontend/src/features/help/use-help-shortcut.ts` + `use-help-shortcut.test.ts` (new)
- `frontend/src/features/help/use-first-visit-hint.ts` (new; tested through `first-visit-hint.test.tsx`)
- `frontend/src/components/help/test-fixtures.ts` (new, test-only data)
- `frontend/src/components/help/help-inline.tsx`, `help-doc-view.tsx` + `help-doc-view.test.tsx` (new)
- `frontend/src/components/help/help-panel.tsx` + `help-panel.test.tsx` (new)
- `frontend/src/components/help/help-button.tsx` + `help-button.test.tsx` (new)
- `frontend/src/components/help/first-visit-hint.tsx` + `first-visit-hint.test.tsx` (new)
- `frontend/src/components/layout/app-layout.tsx` (modify) + `components/layout/__tests__/app-layout-help.test.tsx` (new)
- `frontend/src/components/layout/header/app-header.tsx`, `header-user-menu.tsx` (modify) + `header/__tests__/app-header.test.tsx` (modify)

## Tasks

All commands run from `$WT/frontend`. "Run" means `npm run test -- --testTimeout=30000 --maxWorkers=3 <path>`. Component tests that need help content mock the loader:

```ts
vi.mock('@/features/help/load-help', () => ({
  useHelpIndex: vi.fn(),
  useHelpLoadFailed: vi.fn(() => false),
  loadHelpIndex: vi.fn(() => new Promise(() => {})),
}));
// then: vi.mocked(useHelpIndex).mockReturnValue(buildHelpIndex([...fixtureDocs]));
```

Every mock of `@/features/help/load-help` in this phase provides these three names (a test that renders `HelpPanel` calls all of them).

Fixtures for this phase live in `src/components/help/test-fixtures.ts` (created in Task 2.3, reused by every later task):

```ts
import type { HelpDoc, HelpVideo } from '@/features/help/types';

const base = { sources: ['x/**'], related: [] as string[], updated: '2026-10-15', notes: [] as string[], shortcuts: [] };
export const stockOutDoc: HelpDoc = {
  ...base, key: 'stock-out.form', title: 'Phiếu xuất kho', routes: ['/stock-out/new', '/stock-out/:id'],
  purpose: 'Ghi nhận hàng xuất khỏi kho.',
  steps: [
    { id: 'reason', text: 'Chọn **Lý do xuất**.' },
    { id: 'partner', text: 'Gõ **Mã đối tượng**.' },
    { id: 'save', text: 'Bấm **Lưu tạm** (`Ctrl S`).' },
  ],
  notes: ['Phải lưu trước khi in.'], shortcuts: [{ keys: 'Ctrl S', label: 'Lưu' }], related: ['receipts.form'],
};
export const receiptsDoc: HelpDoc = { ...base, key: 'receipts.form', title: 'Phiếu thu',
  routes: ['/receipts/new', '/receipts/:id'], purpose: 'Thu tiền.', steps: stockOutDoc.steps };
export const productGroupsDoc: HelpDoc = { ...base, key: 'product-groups', title: 'Nhóm hàng hóa',
  routes: ['/product-groups'], purpose: 'Nhóm hàng.', steps: stockOutDoc.steps };
export const fixtureDocs = [stockOutDoc, receiptsDoc, productGroupsDoc];
export const fixtureVideo: HelpVideo = { youtubeId: 'vid123', title: 'Lập phiếu xuất bán hàng', duration: 155,
  recordedAt: '2026-10-20', steps: { reason: { at: 8, hash: '00000000' }, partner: { at: 21, hash: '00000000' } } };
```

### Task 2.1 — Registry icons

1. Add failing rows to the `it.each` of `src/components/icons/icon-registry.test.ts` (import the drawings from `lucide-react` there, as the file already does): `['help', CircleHelp, undefined]`, `['helpHint', Lightbulb, 'warn']`, `['video', Play, undefined]`, `['externalLink', ExternalLink, undefined]`.
2. Run `src/components/icons` → FAIL (TypeScript/compile: `"help"` is not an `IconName`, or `iconDef(...)` undefined).
3. In `icon-registry.ts` import `CircleHelp, ExternalLink, Lightbulb, Play` (check first: `node -e "const l=require('lucide-react');console.log(!!l.CircleHelp,!!l.Lightbulb,!!l.Play,!!l.ExternalLink)"` prints four `true`) and add, in the Utility block, `help: { icon: CircleHelp }`, `video: { icon: Play }`, `externalLink: { icon: ExternalLink }`, and after `warning`, `helpHint: { icon: Lightbulb, tone: 'warn' }`.
4. Run `src/components/icons` → PASS (the guard tests there must stay green).
5. Commit: `feat(frontend): help icons in the registry`.

### Task 2.2 — `useMediaQuery`

1. Write the failing test `src/hooks/useMediaQuery.test.ts`:
   - without `window.matchMedia` → returns the fallback (`true` and `false` cases);
   - with a stub `window.matchMedia = vi.fn().mockReturnValue({ matches: true, addEventListener, removeEventListener })` → `true`; firing the stored `change` listener after flipping `matches` to `false` → `false` (`act`); the listener is removed on unmount. Restore `window.matchMedia` in `afterEach`.
2. Run → FAIL: `Failed to resolve import "./useMediaQuery"`.
3. Implement with `useSyncExternalStore`: subscribe adds a `change` listener when `matchMedia` exists; the snapshot is `window.matchMedia(query).matches`, or `fallback` without `matchMedia`; the server snapshot is `fallback`.
4. Run → PASS.
5. Commit: `feat(frontend): useMediaQuery hook`.

### Task 2.3 — Related help and same-group help (pure)

1. Create `src/components/help/test-fixtures.ts` (above). Write the failing test `src/features/help/related.test.ts` with `buildHelpIndex(fixtureDocs)` and `groups` built by `visibleNavGroups(() => true, () => true)`:
   - `accessibleRelated(stockOutDoc, index, () => true)` → `[receiptsDoc]`; with `canAccess = (p) => p !== '/receipts/new'` → `[]`; an unknown related key is skipped;
   - `docsInSameGroup('/products', groups, index, () => true)` → `[productGroupsDoc]` (both under "Danh mục"); with `canAccess` refusing `/product-groups` → `[]`; `docsInSameGroup('/', groups, index, () => true)` → `[]` (no group).
2. Run → FAIL: `Failed to resolve import "./related"`.
3. Implement `src/features/help/related.ts`: `accessibleRelated` maps `doc.related` through `index.byKey`, drops missing docs and docs whose `firstStaticPath` `canAccess` refuses. `docsInSameGroup` finds the group whose items contain `findActiveItem(pathname, group.items)` (import from `@/components/layout/sidebar/nav-search`), then returns the index docs whose `firstStaticPath` also has an active item in that group and passes `canAccess`, sorted by title (`localeCompare(…, 'vi')`).
4. Run → PASS.
5. Commit: `feat(frontend): related and same-group help`.

### Task 2.4 — `HelpInline` and `HelpDocView`

Layout follows the mockup's panel body, top to bottom: video block (only with a video), "Màn hình này dùng để", "Các bước chính", "Lưu ý" (amber box with the `warning` icon), "Phím tắt" (`hidden md:block`), "Xem thêm".

1. Write the failing test `src/components/help/help-doc-view.test.tsx` (fixtures from `./test-fixtures`):
   - without a video: the purpose text; three numbered steps; `Lý do xuất` inside a `<strong>`; `Ctrl S` inside a `<kbd>`; no link named `Xem trên YouTube`; no element named `/Xem bước/`;
   - with `fixtureVideo`: a link `Xem trên YouTube` with `href` `https://www.youtube.com/watch?v=vid123`, `target="_blank"`, `rel` containing `noopener`; an image with `src` `https://i.ytimg.com/vi/vid123/mqdefault.jpg`; the duration `2:35`; a link named `Xem bước 1 trong video (0:08)` with `href` ending `&t=8s` and one for step 2 (`&t=21s`), none for step 3;
   - `fireEvent.error(img)` → the image is gone and the text card shows `Lập phiếu xuất bán hàng`;
   - with `vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(false)` and a video → `Cần kết nối mạng để xem video` and no YouTube link;
   - "Lưu ý" and "Phím tắt" sections render only when the lists are non-empty; the shortcut section has class `hidden` and `md:block`;
   - "Xem thêm": a button `Phiếu thu`; clicking it calls `onOpenRelated('receipts.form')`; no "Xem thêm" heading with `related: []`.
2. Run → FAIL: `Failed to resolve import "./help-doc-view"`.
3. Implement:
   - `help-inline.tsx`: `HelpInline({ text })` renders `parseInline(text)`: `strong` → `<strong className="font-semibold">`, `kbd` → `<kbd className="rounded border bg-muted px-1 py-0.5 font-mono text-[11px]">`, text as is.
   - `help-doc-view.tsx`: section headings are `<h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">`. Steps are an `<ol>` with a numbered badge per item; a step with `video.steps[id]` gets an `<a>` chip (icon `video`, text `formatDuration(at)`, `aria-label` `Xem bước ${n} trong video (${formatDuration(at)})`, `target="_blank" rel="noopener noreferrer"`). The video block is a link around the thumbnail (state `thumbFailed` → text card with the `video` icon, the title and the duration) plus the `Xem trên YouTube` link with the `externalLink` icon; with `navigator.onLine === false` it renders only the offline sentence. Icons through `<AppIcon name=… />`; no colour codes (amber box: `border-[hsl(var(--icon-warn)/0.4)] bg-[hsl(var(--icon-warn)/0.1)]`; the `--icon-*` tokens are bare HSL channels, so the alpha goes inside `hsl()` — Tailwind 3 cannot add a `/10` modifier to a `var()` colour).
4. Run → PASS.
5. Commit: `feat(frontend): help document view`.

### Task 2.5 — `HelpPanel`

Behaviour (brainstorm §1 "Khung Hướng dẫn", "Phím và focus", "Trạng thái của khung"):

- Renders nothing while `helpOpen` is false.
- Doc shown: the related doc picked inside the panel (`viewKey`, reset when `pathname` changes), else `index.resolve(pathname)`.
- Header: `<h2 id="help-panel-title" tabIndex={-1}>` with the doc title (or `Hướng dẫn`), a close button `aria-label="Đóng hướng dẫn"` (icon `close`), and with `viewKey` a `Quay lại` button (icon `back`).
- Body: `useHelpIndex()` null and `useHelpLoadFailed()` false → `Đang tải hướng dẫn…`; null and failed → `Không tải được hướng dẫn. Kiểm tra kết nối mạng rồi thử lại.` and a `Button variant="outline" size="sm"` `Thử lại` that calls `loadHelpIndex()`; a doc → `HelpDocView` with `videoFor(doc.key)` and `accessibleRelated(doc, index, canAccess)`; no doc → `Chưa có hướng dẫn cho màn hình này.` and, when `docsInSameGroup(...)` is not empty, `Hướng dẫn cùng nhóm:` with one button per doc (opens it in the panel).
- Layout inside the aside: header `shrink-0 border-b`, body `<div data-testid="help-panel-body" className="min-h-0 flex-1 overflow-y-auto p-4">` (the only part that scrolls, so a long doc stays readable in the bottom sheet and in the full-height panel), footer `shrink-0 border-t`.
- Footer with a doc: `Cập nhật dd/mm/yyyy` from `updated`.
- Focus: on open, remember `document.activeElement` and focus the title. On close (or unmount) give the focus back to the remembered element only when it is still connected **and** the focus is inside the panel or on `document.body` at that moment. The panel is not modal: a user who clicked into another field and pressed F1 to close keeps the focus in that field.
- Esc: a `keydown` listener added with `addEventListener` on the `<aside>` (not `onKeyDown`, for `jsx-a11y`) closes the panel and stops propagation; Esc outside the panel does nothing.
- Below `md`: a backdrop `<button type="button" aria-label="Đóng hướng dẫn" className="fixed inset-0 z-40 bg-black/40 md:hidden">` closes it.
- `<aside data-testid="help-panel" aria-labelledby="help-panel-title" className={HELP_PANEL_CLASS}>`, exported constant:
  `'fixed inset-x-0 bottom-0 z-50 flex max-h-[85dvh] flex-col rounded-t-2xl border-t bg-card pb-safe shadow-xl md:absolute md:inset-x-auto md:bottom-0 md:right-0 md:top-16 md:z-30 md:max-h-none md:w-[400px] md:rounded-none md:border-l md:border-t-0 md:pb-0 min-[1600px]:static min-[1600px]:col-start-3 min-[1600px]:row-start-2 min-[1600px]:z-auto min-[1600px]:shadow-none'`.

1. Write the failing test `src/components/help/help-panel.test.tsx`. Render `<MemoryRouter initialEntries={[path]}><input aria-label="ô trang" /><input aria-label="ô khác" /><Link to="/products">sang hàng hóa</Link><HelpPanel /></MemoryRouter>`; set `useAuthStore` with a user `u1` holding `stock_out.view`, `stock_out.create`, `receipts.view`, `receipts.create`, `products.view`; mock `load-help`. Cases:
   - `helpOpen: false` → no `help-panel`.
   - open on `/stock-out/new` → heading `Phiếu xuất kho`, the purpose, `Cập nhật 15/10/2026`; the heading has the focus.
   - focus `ô trang`, then open (`act(() => useUiStore.getState().openHelp())`), click `Đóng hướng dẫn` (the one inside the panel) → `helpOpen` false and `ô trang` has the focus again.
   - focus `ô trang`, open, then focus `ô khác` (`act(() => screen.getByLabelText('ô khác').focus())`), close with `act(() => useUiStore.getState().closeHelp())` → `ô khác` keeps the focus.
   - Esc on the panel title → closes; Esc on `ô trang` → stays open.
   - click `Phiếu thu` (related) → heading `Phiếu thu` and a `Quay lại` button; `Quay lại` → `Phiếu xuất kho`.
   - click `Phiếu thu`, then click the link `sang hàng hóa` → the panel stays open and shows `Chưa có hướng dẫn cho màn hình này.`, no `Quay lại` (the route changed, so the content follows it and the picked doc is dropped).
   - user without `receipts.create` → no `Phiếu thu` button.
   - `/products` → `Chưa có hướng dẫn cho màn hình này.` and a `Nhóm hàng hóa` button.
   - `useHelpIndex` returning `null` → `Đang tải hướng dẫn…`.
   - `useHelpIndex` returning `null` and `useHelpLoadFailed` returning `true` → `Không tải được hướng dẫn…`; clicking `Thử lại` calls `loadHelpIndex` once.
   - the aside has classes `fixed`, `max-h-[85dvh]`, `md:w-[400px]`, `md:absolute`, `min-[1600px]:static`, `min-[1600px]:col-start-3`; `help-panel-body` has `min-h-0`, `flex-1`, `overflow-y-auto`; a backdrop button with class `md:hidden` exists.
2. Run → FAIL: `Failed to resolve import "./help-panel"`.
3. Implement `src/components/help/help-panel.tsx` as described (`useCanAccessRoute()` for `canAccess`; `visibleNavGroups(hasPermission, isInRole)` from `@/components/layout/nav-config` for the groups; `useHelpIndex`, `useHelpLoadFailed` and `loadHelpIndex` from `@/features/help/load-help`; the date formatted from the `YYYY-MM-DD` string by splitting, not through `Date`, so no time zone can shift it).
4. Run → PASS.
5. Commit: `feat(frontend): help panel`.

### Task 2.6 — F1

1. Write the failing test `src/features/help/use-help-shortcut.test.ts` (`renderHook(() => useHelpShortcut())`, reset `helpOpen` in `beforeEach`, remove added DOM in `afterEach`):
   - `const e = new KeyboardEvent('keydown', { key: 'F1', cancelable: true, bubbles: true }); act(() => window.dispatchEvent(e))` → `helpOpen` true and `e.defaultPrevented`; a second F1 → false;
   - F1 with `ctrlKey`, `altKey`, `shiftKey` or `metaKey`, and with `repeat: true` → no change;
   - with `<div role="dialog">` or `<div role="alertdialog">` appended to `document.body` (a modal dialog) → no change;
   - with `<div data-radix-popper-content-wrapper><div role="dialog"></div></div>` (a popover: a grid popover, a typeahead list, the first-visit hint) → toggles;
   - a key `F2` → no change; after unmount F1 → no change.
2. Run → FAIL: `Failed to resolve import "./use-help-shortcut"`.
3. Implement:
   - `src/lib/focus.ts`: add, next to `DIALOG_SELECTOR`,
     ```ts
     /** Radix wraps every popper (popover, select list, tooltip) in this element; a role="dialog" inside it is a popover, not a modal. */
     export const POPPER_WRAPPER_SELECTOR = '[data-radix-popper-content-wrapper]';
     ```
     Leave the local `POPOVER` constant of `stock-voucher-form-page.tsx` alone: the P12 branches edit that file, and switching it to the shared constant can follow their merge.
   - `use-help-shortcut.ts`: an effect adds `window.addEventListener('keydown', onKey, true)` (capture, so grid handlers that stop propagation cannot swallow F1). `onKey` returns unless `e.key === 'F1'` with no modifier and `!e.repeat`; returns if `[...document.querySelectorAll(DIALOG_SELECTOR)].some((el) => !el.closest(POPPER_WRAPPER_SELECTOR))` (a modal dialog is open; both constants from `@/lib/focus`); else `e.preventDefault()` and `useUiStore.getState().toggleHelp()`.
4. Run → PASS.
5. Commit: `feat(frontend): F1 opens the help panel`.

### Task 2.7 — Layout: render the panel and the F1 hook

1. Write the failing test `src/components/layout/__tests__/app-layout-help.test.tsx`, with the mocks of `app-layout-scroll.test.tsx` (`useBranchContext`, `useNotificationHub`, `../header/app-header`) plus `vi.mock('@/features/help/load-help', () => ({ useHelpIndex: () => null, useHelpLoadFailed: () => false, loadHelpIndex: vi.fn() }))`:
   - `helpOpen: false` → the grid (`screen.getByText('header').parentElement`) has class `relative` and not `min-[1600px]:grid-cols-[auto_1fr_400px]`; no `help-panel`;
   - `helpOpen: true` → the grid has `min-[1600px]:grid-cols-[auto_1fr_400px]` and `help-panel` is rendered;
   - F1 on `window` toggles `helpOpen` (the hook is mounted).
2. Run → FAIL (class missing / no panel).
3. In `app-layout.tsx`: read `helpOpen` from `ui-store`; call `useHelpShortcut()`; add `relative` to the grid's classes and `helpOpen && 'min-[1600px]:grid-cols-[auto_1fr_400px]'` through `cn`; render `<HelpPanel />` right after `</main>`. Do not touch the existing classes (`app-layout-scroll.test.tsx` asserts them).
4. Run `src/components/layout` → PASS.
5. Commit: `feat(frontend): help panel in the app layout`.

### Task 2.8 — First-visit hint logic

1. Write the failing test `src/features/help/hint.test.ts` for `shouldShowHint`: true for a user, a doc, panel closed, not seen, not off; false for each of: no user, no doc, `helpOpen`, the key in `seen[user]`, `off[user]`; another user's `seen` list does not count.
2. Run → FAIL: `Failed to resolve import "./hint"`.
3. Implement `src/features/help/hint.ts` and `src/features/help/use-first-visit-hint.ts`:
   ```ts
   export function useFirstVisitHint() {
     const userId = useAuthStore((s) => s.user?.id);
     const index = useHelpIndex();
     const { pathname } = useLocation();
     const doc = index?.resolve(pathname) ?? null;
     const helpOpen = useUiStore((s) => s.helpOpen);
     const seen = useUiStore((s) => s.helpHintSeen);
     const off = useUiStore((s) => s.helpHintsOff);
     const { markHelpHintSeen, turnOffHelpHints, openHelp } = useUiStore.getState();
     const key = doc?.key;
     // The panel shows this screen's help, so its hint counts as seen.
     useEffect(() => { if (helpOpen && userId && key) markHelpHintSeen(userId, key); }, [helpOpen, userId, key, markHelpHintSeen]);
     const dismiss = () => { if (userId && key) markHelpHintSeen(userId, key); };
     return {
       visible: shouldShowHint({ userId, doc, helpOpen, seen, off }),
       doc,
       dismiss,
       openFromHint: () => { dismiss(); openHelp(); },
       turnOff: () => { if (userId) turnOffHelpHints(userId); },
     };
   }
   ```
4. Run → PASS.
5. Commit: `feat(frontend): first-visit hint rule`.

### Task 2.9 — `HelpButton`, `FirstVisitHint`, header and user menu

1. Write the failing tests:
   - `src/components/help/help-button.test.tsx`: the button is named `Hướng dẫn màn hình này (F1)`, has classes `hidden` and `md:flex`, `aria-expanded="false"`; a click sets `helpOpen` true and `aria-expanded="true"`; `showDot` renders `data-testid="help-hint-dot"`, without it there is none; a `ref` reaches the `<button>`.
   - `src/components/help/first-visit-hint.test.tsx`: harness
     ```tsx
     function Harness() {
       const hint = useFirstVisitHint();
       const a = useRef<HTMLButtonElement>(null);
       return (
         <>
           {/* eslint-disable-next-line jsx-a11y/no-autofocus -- the page's own focus, which the hint must not take */}
           <input aria-label="ô trang" autoFocus />
           <input aria-label="ô khác" />
           <button ref={a}>neo</button>
           <FirstVisitHint hint={hint} desktopAnchorRef={a} mobileAnchorRef={a} />
         </>
       );
     }
     ```
     in a `MemoryRouter` at `/stock-out/new`, user `u1`, mocked `load-help`. Cases: shows `Lần đầu dùng Phiếu xuất kho?` and `3 bước chính`; `ô trang` keeps the focus; the popover content has the attribute `data-help-hint`; focus moves to `ô khác` (`await userEvent.tab()`) → the hint is still shown and `helpHintSeen.u1` is still empty; `Ẩn` → hint gone and `helpHintSeen.u1` contains `stock-out.form`; `Xem hướng dẫn` → `helpOpen` true and seen; `Không hiện gợi ý nữa` → `helpHintsOff.u1` true and gone; Esc (`userEvent.keyboard('{Escape}')`) → gone and seen; a click outside (`await userEvent.click(screen.getByText('neo'))`) → gone and seen; at `/products` → nothing; with `helpHintSeen: { u1: ['stock-out.form'] }` → nothing; with `helpOpen: true` → nothing.
   - `src/components/layout/header/__tests__/app-header.test.tsx` (add, with `vi.mock('@/features/help/load-help', () => ({ useHelpIndex: () => null, useHelpLoadFailed: () => false, loadHelpIndex: vi.fn() }))` at the top): the header renders the `Hướng dẫn màn hình này (F1)` button; opening the user menu (`userEvent.click(screen.getByRole('button', { name: 'Mở menu người dùng' }))`) shows a menu item `Hướng dẫn màn hình này` with class `md:hidden`; selecting it sets `helpOpen` true.
2. Run `src/components/help src/components/layout/header` → FAIL: missing modules / button not found.
3. Implement:
   - `help-button.tsx`: `forwardRef<HTMLButtonElement, { showDot: boolean }>`; `className="relative hidden h-11 w-11 items-center justify-center rounded-md text-header-fg hover:bg-header-active md:flex"`, `aria-label` and `title` `Hướng dẫn màn hình này (F1)`, `aria-expanded={helpOpen}`, `onClick={toggleHelp}`, `<AppIcon name="help" size={20} />`, dot `<span data-testid="help-hint-dot" className="absolute right-2 top-2 h-2 w-2 rounded-full bg-[hsl(var(--icon-warn))] ring-2 ring-header-bg" />`.
   - `first-visit-hint.tsx`: returns null unless `hint.visible && hint.doc`. `const desktop = useMediaQuery('(min-width: 768px)', true);` `<Popover open onOpenChange={(o) => { if (!o) hint.dismiss(); }}>` with `<PopoverAnchor virtualRef={desktop ? desktopAnchorRef : mobileAnchorRef} />` and `<PopoverContent data-help-hint="" side="bottom" align="end" className="w-80 p-3" onOpenAutoFocus={(e) => e.preventDefault()} onFocusOutside={(e) => e.preventDefault()}>` (Radix closes a non-modal popover on any `focusin` outside it; the hint must survive Tab and Enter between fields, P10) containing: a title line (icon `helpHint`) `Lần đầu dùng {doc.title}?`; on desktop (`hidden md:block`) `Bấm ? hoặc F1 để xem {n} bước chính{video ? \` và video ${formatDuration(video.duration)}\` : ''}. Khung mở bên phải, vẫn nhập liệu được.` (with `?` in `<strong>` and `F1` in `<kbd>`); on mobile (`md:hidden`) `Mở menu tài khoản rồi chọn Hướng dẫn màn hình này.`; buttons `Xem hướng dẫn` (`Button size="sm"`, icon `help` with `tone="none"`, `onClick={hint.openFromHint}`), `Ẩn` (`variant="outline"`, `onClick={hint.dismiss}`) and a text button `Không hiện gợi ý nữa` (`onClick={hint.turnOff}`).
   - `header-user-menu.tsx`: props `triggerRef?: React.Ref<HTMLButtonElement>` (on the trigger `<button>`) and `onOpenHelp?: () => void`; when `onOpenHelp` is given, a first item `<DropdownMenuItem className="md:hidden" onSelect={onOpenHelp}><AppIcon name="help" /> Hướng dẫn màn hình này</DropdownMenuItem>` before `Cài đặt của tôi`.
   - `app-header.tsx`: `const hint = useFirstVisitHint(); const helpRef = useRef<HTMLButtonElement>(null); const userRef = useRef<HTMLButtonElement>(null); const openHelp = useUiStore((s) => s.openHelp);` render `<HelpButton ref={helpRef} showDot={hint.visible} />` right before `<HeaderNotifications />`, pass `triggerRef={userRef}` and `onOpenHelp={openHelp}` to `HeaderUserMenu`, and render `<FirstVisitHint hint={hint} desktopAnchorRef={helpRef} mobileAnchorRef={userRef} />` at the end of the header.
4. Run `src/components/help src/components/layout src/features/help` → PASS.
5. Commit: `feat(frontend): help button, user menu entry and first-visit hint`.

## Verification

```bash
cd "$WT/frontend"
npm run test -- --testTimeout=30000 --maxWorkers=3 src/components src/features/help src/hooks src/stores
npm run typecheck && npm run lint
npm run test -- --testTimeout=30000 --maxWorkers=3      # full suite: no failure outside BASELINE.md
```

## Exit Criteria

- F1, the **?** button (from `md`) and the user-menu item (below `md`) open the panel; Esc inside and the close button close it and give the focus back, unless the user has moved the focus elsewhere. F1 is blocked only by a modal dialog.
- The panel shows the doc of the route, related and same-group help limited by route access, the loading, load-failed (with `Thử lại`) and empty states, scrolls inside its body, and hides the video parts while `videos.json` is empty.
- The first-visit hint shows once per user and screen, never takes the focus, survives focus moves, and every dismissal of P10 marks the screen seen.
- The full frontend suite has no failure outside `BASELINE.md`.
