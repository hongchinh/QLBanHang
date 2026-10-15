# Phase 02 — Hỗ trợ iOS: icon, meta, safe-area, hướng dẫn cài

**Status:** [x] complete
**Complexity:** M

## Objective

Trên iPhone (iOS 16.4+): app cài lên màn hình chính có icon branding 180px; nội dung không nằm dưới tai thỏ hay thanh home; Safari chưa cài hiện hướng dẫn "Chia sẻ → Thêm vào MH chính" thay vì im lặng. Favicon dùng URL tuyệt đối của API như manifest, nên hết hỏng trên production.

Hiện trạng:
- [SettingsController.cs:47-63](../../../backend/src/OrderMgmt.WebApi/Controllers/SettingsController.cs#L47-L63) chỉ nhận size 192 và 512.
- [index.html](../../../frontend/index.html) chỉ có viewport và favicon tương đối `/api/settings/branding/icon/192`.
- [usePushNotification.ts](../../../frontend/src/hooks/usePushNotification.ts) trả `'unsupported'` khi không có `Notification` (Safari iOS chưa cài), và [push-prompt-visibility.ts](../../../frontend/src/components/push-prompt-visibility.ts) ẩn banner ở trạng thái đó.
- Chưa có tiện ích safe-area nào.

## Files

- `backend/src/OrderMgmt.WebApi/Controllers/SettingsController.cs`
- `backend/tests/OrderMgmt.IntegrationTests/Settings/BrandingIconTests.cs`
- `frontend/src/lib/pwa-icons.ts` (mới), `frontend/src/lib/pwa-icons.test.ts` (mới)
- `frontend/vite.config.ts`
- `frontend/index.html`, `frontend/src/index-html.test.ts` (mới)
- `frontend/src/index.css`, `frontend/src/index-css-safe-area.test.ts` (mới)
- `frontend/src/components/layout/app-shell.tsx`, `frontend/src/components/layout/__tests__/app-shell.test.tsx`
- `frontend/src/components/layout/app-layout.tsx`, `frontend/src/components/layout/__tests__/app-layout-scroll.test.tsx`
- `frontend/src/components/layout/header/header-search-mobile-sheet.tsx`, `frontend/src/components/layout/header/__tests__/header-search-mobile-sheet.test.tsx` (mới)
- `frontend/src/lib/ios-pwa.ts` (mới), `frontend/src/lib/ios-pwa.test.ts` (mới)
- `frontend/src/hooks/usePushNotification.ts`, `frontend/src/hooks/usePushNotification.test.ts`
- `frontend/src/components/PushPermissionPrompt.tsx`, `frontend/src/components/PushPermissionPrompt.test.tsx`
- `frontend/src/App.tsx`, `frontend/src/App.shell.test.tsx` (popup cập nhật, Task 11)
- `frontend/src/components/ui/toaster.tsx`, `frontend/src/components/ui/toaster.test.tsx` (mới)

## Tasks

### Task 1 — Icon branding 180px (backend)

Lệnh test chạy từ `backend`, với PostgreSQL cục bộ (không bao giờ dùng `qldonhang_test`):

```bash
export TEST_DB_CONNECTION="Host=localhost;Port=5432;Database=qldonhang_integtest;Username=postgres;Password=1"
```

1. **Viết test thất bại** — trong `tests/OrderMgmt.IntegrationTests/Settings/BrandingIconTests.cs`, thêm `[InlineData(180)]` vào theory `GetIcon_NoLogoUploaded_Returns200Png` (trên `[InlineData(192)]`), và thêm một test kiểm kích thước ảnh:

   ```csharp
   // iOS reads apple-touch-icon at 180x180; the renderer must honour the requested size.
   [Fact]
   public async Task GetIcon_180_IsA180PxSquarePng()
   {
       var bytes = await _client.GetByteArrayAsync("/api/settings/branding/icon/180");

       using var bitmap = SkiaSharp.SKBitmap.Decode(bytes);
       bitmap.Width.Should().Be(180);
       bitmap.Height.Should().Be(180);
   }
   ```

   Nếu project test không tham chiếu được `SkiaSharp` (lỗi biên dịch `SkiaSharp` không tồn tại), đọc kích thước từ header PNG thay vì thêm package: chiều rộng là 4 byte big-endian ở offset 16, chiều cao ở offset 20 (`System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4))`).

2. **Chạy test, xác nhận thất bại** — `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~BrandingIconTests"` / Expected: FAIL ở `GetIcon_NoLogoUploaded_Returns200Png(size: 180)` (`Expected ... OK, but found BadRequest`) và `GetIcon_180_IsA180PxSquarePng` (`HttpRequestException ... 400`).

3. **Viết code tối thiểu** — trong `SettingsController.GetPwaIcon`, thay điều kiện:

   ```csharp
   // 180 is the iOS apple-touch-icon; 192 and 512 are the manifest icons.
   if (size is not (180 or 192 or 512))
       return BadRequest();
   ```

4. **Chạy test, xác nhận đạt** — cùng lệnh / Expected: PASS (cả `GetIcon_InvalidSize_Returns400` với 100 và 0 vẫn đạt).

5. **Commit** — `git commit -am "feat(branding): serve a 180px PWA icon for iOS"`

### Task 2 — `pwa-icons.ts`: URL icon và thẻ link favicon + apple-touch-icon

`vite.config.ts` import file này, và `tsconfig.node.json` kiểm kiểu nó với lib ES2023 không có DOM: file phải thuần, không dùng alias `@/`, không import gì từ `src`.

Hợp đồng:

```ts
export interface IconLinkTag {
  tag: 'link'
  attrs: Record<string, string>
  injectTo: 'head'
}

// apiBase rỗng (dev, Vite proxy) → '/api/settings/branding/icon/<size>'; có giá trị (production,
// ví dụ 'https://backend.example.com/api') → '<apiBase>/settings/branding/icon/<size>'.
export function brandingIconUrl(apiBase: string, size: number): string

// Thẻ chèn vào <head>: favicon PNG 192 và apple-touch-icon 180.
export function iconLinkTags(apiBase: string): IconLinkTag[]
```

Mọi lệnh frontend chạy từ `frontend`.

1. **Viết test thất bại** — tạo `src/lib/pwa-icons.test.ts`:

   ```ts
   import { describe, expect, it } from 'vitest'
   import { brandingIconUrl, iconLinkTags } from './pwa-icons'

   describe('brandingIconUrl', () => {
     it('uses the dev proxy path without an API base', () => {
       expect(brandingIconUrl('', 192)).toBe('/api/settings/branding/icon/192')
     })

     // In production the API is on another origin; a relative /api path would hit the frontend host.
     it('uses the absolute API base in production', () => {
       expect(brandingIconUrl('https://api.example.com/api', 180))
         .toBe('https://api.example.com/api/settings/branding/icon/180')
     })
   })

   describe('iconLinkTags', () => {
     it('adds the favicon and the 180px apple-touch-icon to the head', () => {
       expect(iconLinkTags('https://api.example.com/api')).toEqual([
         {
           tag: 'link',
           attrs: { rel: 'icon', type: 'image/png', href: 'https://api.example.com/api/settings/branding/icon/192' },
           injectTo: 'head',
         },
         {
           tag: 'link',
           attrs: { rel: 'apple-touch-icon', sizes: '180x180', href: 'https://api.example.com/api/settings/branding/icon/180' },
           injectTo: 'head',
         },
       ])
     })
   })
   ```

2. **Chạy test, xác nhận thất bại** — `npx vitest run src/lib/pwa-icons.test.ts` / Expected: FAIL, `Failed to resolve import "./pwa-icons"`.

3. **Viết code tối thiểu** — tạo `src/lib/pwa-icons.ts`:

   ```ts
   // Imported by vite.config.ts (type-checked by tsconfig.node.json, no DOM): keep it pure and free of '@/' imports.

   export interface IconLinkTag { tag: 'link'; attrs: Record<string, string>; injectTo: 'head' }

   export function brandingIconUrl(apiBase: string, size: number): string {
     return apiBase ? `${apiBase}/settings/branding/icon/${size}` : `/api/settings/branding/icon/${size}`
   }

   export function iconLinkTags(apiBase: string): IconLinkTag[] {
     return [
       { tag: 'link', attrs: { rel: 'icon', type: 'image/png', href: brandingIconUrl(apiBase, 192) }, injectTo: 'head' },
       {
         tag: 'link',
         attrs: { rel: 'apple-touch-icon', sizes: '180x180', href: brandingIconUrl(apiBase, 180) },
         injectTo: 'head',
       },
     ]
   }
   ```

4. **Chạy test, xác nhận đạt** — `npx vitest run src/lib/pwa-icons.test.ts` / Expected: PASS.

5. **Commit** — `git add src/lib/pwa-icons.ts src/lib/pwa-icons.test.ts && git commit -m "feat(pwa): build branding icon links from the API base"`

### Task 3 — Dùng `pwa-icons` trong `vite.config.ts`, meta iOS trong `index.html`

1. **Viết test thất bại** — tạo `src/index-html.test.ts` (đọc file theo cách của `nav-tokens.test.ts`):

   ```ts
   import { readFileSync } from 'node:fs'
   import { resolve } from 'node:path'
   import { describe, expect, it } from 'vitest'

   const html = readFileSync(resolve(process.cwd(), 'index.html'), 'utf8')

   describe('index.html', () => {
     // viewport-fit=cover makes env(safe-area-inset-*) non-zero on notched iPhones.
     it('lets the page reach the screen edges so safe-area insets apply', () => {
       expect(html).toContain('content="width=device-width, initial-scale=1.0, viewport-fit=cover"')
     })

     it.each([
       '<meta name="theme-color" content="#1e40af" />',
       '<meta name="mobile-web-app-capable" content="yes" />',
       '<meta name="apple-mobile-web-app-capable" content="yes" />',
       '<meta name="apple-mobile-web-app-title" content="QLĐơnHàng" />',
       // default, not black-translucent: content never sits under the status bar.
       '<meta name="apple-mobile-web-app-status-bar-style" content="default" />',
     ])('has %s', (tag) => {
       expect(html).toContain(tag)
     })

     // The favicon and apple-touch-icon are injected by vite.config.ts with the absolute API base; a relative
     // /api path is served by the frontend host in production.
     it('has no relative branding icon link', () => {
       expect(html).not.toContain('/api/settings/branding/icon')
     })
   })
   ```

2. **Chạy test, xác nhận thất bại** — `npx vitest run src/index-html.test.ts` / Expected: FAIL ở viewport, năm thẻ meta và "no relative branding icon link".

3. **Viết code tối thiểu**:
   - `index.html`: xóa dòng `<link rel="icon" … href="/api/settings/branding/icon/192" />`; thay thẻ viewport và thêm meta ngay dưới nó:

     ```html
     <meta name="viewport" content="width=device-width, initial-scale=1.0, viewport-fit=cover" />
     <meta name="theme-color" content="#1e40af" />
     <meta name="mobile-web-app-capable" content="yes" />
     <meta name="apple-mobile-web-app-capable" content="yes" />
     <meta name="apple-mobile-web-app-title" content="QLĐơnHàng" />
     <meta name="apple-mobile-web-app-status-bar-style" content="default" />
     ```

   - `vite.config.ts`: `import { brandingIconUrl, iconLinkTags } from './src/lib/pwa-icons'`; xóa hàm cục bộ `iconSrc`; manifest dùng `brandingIconUrl(apiBase, 192)` và `brandingIconUrl(apiBase, 512)`; thêm plugin vào đầu mảng `plugins`:

     ```ts
     // Favicon and apple-touch-icon point at the API origin, like the manifest icons.
     { name: 'branding-icon-links', transformIndexHtml: () => iconLinkTags(apiBase) },
     ```

4. **Chạy test, xác nhận đạt** — `npx vitest run src/index-html.test.ts src/lib/pwa-icons.test.ts && npm run typecheck && npx tsc -p tsconfig.node.json && npm run build` / Expected: PASS; sau build, `grep -c 'rel="apple-touch-icon"' dist/index.html` trả `1` và `grep -c 'rel="icon"' dist/index.html` trả `1`.

5. **Commit** — `git add src/index-html.test.ts && git commit -am "feat(pwa): iOS meta tags and API-origin favicon and apple-touch-icon"`

### Task 4 — Tiện ích safe-area

1. **Viết test thất bại** — tạo `src/index-css-safe-area.test.ts`:

   ```ts
   import { readFileSync } from 'node:fs'
   import { resolve } from 'node:path'
   import { describe, expect, it } from 'vitest'

   const css = readFileSync(resolve(process.cwd(), 'src/index.css'), 'utf8')

   // env() is 0 on desktop and Android, so these are no-ops there; on a notched iPhone they keep content
   // clear of the notch (landscape) and the home indicator.
   describe('safe-area utilities', () => {
     it.each([
       ['pt-safe', 'padding-top', 'top'],
       ['pb-safe', 'padding-bottom', 'bottom'],
       ['pl-safe', 'padding-left', 'left'],
       ['pr-safe', 'padding-right', 'right'],
     ])('.%s sets %s from env(safe-area-inset-%s)', (cls, property, side) => {
       const rule = new RegExp(`\\.${cls}\\s*\\{\\s*${property}:\\s*env\\(safe-area-inset-${side}\\);?\\s*\\}`)
       expect(css).toMatch(rule)
     })

     it('declares them in the utilities layer so they win over component classes', () => {
       expect(css).toMatch(/@layer utilities\s*\{[^}]*\.pt-safe/)
     })
   })
   ```

2. **Chạy test, xác nhận thất bại** — `npx vitest run src/index-css-safe-area.test.ts` / Expected: FAIL, 5 test không khớp.

3. **Viết code tối thiểu** — cuối `src/index.css`:

   ```css
   /* Safe-area padding for notched iPhones (viewport-fit=cover in index.html); 0 everywhere else. */
   @layer utilities {
     .pt-safe { padding-top: env(safe-area-inset-top); }
     .pb-safe { padding-bottom: env(safe-area-inset-bottom); }
     .pl-safe { padding-left: env(safe-area-inset-left); }
     .pr-safe { padding-right: env(safe-area-inset-right); }
   }
   ```

4. **Chạy test, xác nhận đạt** — `npx vitest run src/index-css-safe-area.test.ts` / Expected: PASS.

5. **Commit** — `git add src/index.css src/index-css-safe-area.test.ts && git commit -m "feat(pwa): safe-area padding utilities"`

### Task 5 — Áp safe-area cho `AppShell`

`AppShell` là cột cao bằng cửa sổ chứa banner, header và vùng cuộn của mọi trang (kể cả thanh hành động `sticky top-0` của form báo giá và phiếu kho). Đệm trái/phải/đáy ở gốc của nó phủ tất cả. Không thêm `pt-safe`: thanh trạng thái `default` đã đẩy nội dung xuống.

1. **Viết test thất bại** — trong `src/components/layout/__tests__/app-shell.test.tsx`, thêm:

   ```tsx
   // Everything in the app lives in this column, so its padding keeps the header, the push banner, the sticky
   // form bars and the page clear of the home indicator and (landscape) the notch.
   it('pads the column by the left, right and bottom safe-area insets', () => {
     render(<AppShell banner={null}><div>app</div></AppShell>);
     expect(screen.getByTestId('app-shell')).toHaveClass('pl-safe', 'pr-safe', 'pb-safe');
     expect(screen.getByTestId('app-shell')).not.toHaveClass('pt-safe');
   });
   ```

2. **Chạy test, xác nhận thất bại** — `npx vitest run src/components/layout/__tests__/app-shell.test.tsx` / Expected: FAIL, thiếu class `pl-safe`.

3. **Viết code tối thiểu** — trong `app-shell.tsx`, đổi `className="flex h-screen flex-col"` thành `className="flex h-screen flex-col pb-safe pl-safe pr-safe"`, và thêm vào chú thích trên hàm một câu: "Its safe-area padding keeps the app clear of the iPhone home indicator and, in landscape, the notch."

4. **Chạy test, xác nhận đạt** — `npx vitest run src/components/layout/__tests__/app-shell.test.tsx src/App.shell.test.tsx` / Expected: PASS.

5. **Commit** — `git commit -am "feat(pwa): keep the app shell inside the iPhone safe area"`

### Task 6 — Áp safe-area cho drawer mobile

Drawer là `fixed inset-y-0 left-0`, tính theo viewport chứ không theo `AppShell`, nên cần đệm riêng.

1. **Viết test thất bại** — trong `src/components/layout/__tests__/app-layout-scroll.test.tsx`, thêm test dùng cùng `render` của test đầu file (mock `useBranchContext` trả `ready: true`):

   ```tsx
   // The drawer is fixed to the viewport, outside the shell's padding.
   it('keeps the mobile drawer inside the safe area', () => {
     vi.mocked(useBranchContext).mockReturnValue({ myBranches: undefined, workingBranch: null, ready: true });
     const { container } = render(
       <MemoryRouter initialEntries={['/']}>
         <Routes>
           <Route element={<AppLayout />}>
             <Route path="/" element={<div>Page content</div>} />
           </Route>
         </Routes>
       </MemoryRouter>,
     );
     const drawer = container.querySelector('aside.fixed');
     expect(drawer).toHaveClass('pt-safe', 'pb-safe', 'pl-safe');
   });
   ```

2. **Chạy test, xác nhận thất bại** — `npx vitest run src/components/layout/__tests__/app-layout-scroll.test.tsx` / Expected: FAIL, thiếu class `pt-safe`.

3. **Viết code tối thiểu** — trong `app-layout.tsx`, thêm `pt-safe pb-safe pl-safe` vào chuỗi class đầu của `cn(...)` cho `<aside>` drawer mobile (chuỗi bắt đầu bằng `'fixed inset-y-0 left-0 z-50 …'`).

4. **Chạy test, xác nhận đạt** — cùng lệnh / Expected: PASS.

5. **Commit** — `git commit -am "feat(pwa): keep the mobile drawer inside the safe area"`

### Task 7 — Áp safe-area cho sheet tìm kiếm mobile

1. **Viết test thất bại** — tạo `src/components/layout/header/__tests__/header-search-mobile-sheet.test.tsx`:

   ```tsx
   import { fireEvent, render, screen } from '@testing-library/react'
   import { MemoryRouter } from 'react-router-dom'
   import { describe, expect, it, vi } from 'vitest'
   import { HeaderSearchMobileSheet } from '../header-search-mobile-sheet'

   vi.mock('@/features/search/hooks', () => ({
     SEARCH_MIN_LENGTH: 2,
     useGlobalSearch: () => ({ data: undefined, isFetching: false }),
   }))

   describe('HeaderSearchMobileSheet', () => {
     // The sheet is fixed to the top of the viewport, outside the shell's padding.
     it('keeps the open sheet inside the safe area', () => {
       render(<MemoryRouter><HeaderSearchMobileSheet /></MemoryRouter>)
       fireEvent.click(screen.getByRole('button', { name: 'Mở tìm kiếm' }))
       expect(screen.getByRole('dialog')).toHaveClass(
         'pb-3',
         'pt-[max(0.75rem,env(safe-area-inset-top))]',
         'pl-[max(0.75rem,env(safe-area-inset-left))]',
         'pr-[max(0.75rem,env(safe-area-inset-right))]',
       )
     })
   })
   ```

   Nếu `HeaderSearchMobileSheet` cần thêm provider (ví dụ `QueryClientProvider` vì `useAuthStore` hay `SearchResultsList` gọi query), bọc thêm provider đó theo cách `header-search.test.tsx` làm.

2. **Chạy test, xác nhận thất bại** — `npx vitest run src/components/layout/header/__tests__/header-search-mobile-sheet.test.tsx` / Expected: FAIL, dialog thiếu class `pb-3` và các class `max(...)`.

3. **Viết code tối thiểu** — trong `header-search-mobile-sheet.tsx`, đổi `<SheetContent side="top" className="p-3">` thành:

   ```tsx
   {/* At least the old 12px, more where the notch (landscape) or status bar needs it. pt-safe would replace
       the 12px with env() = 0 on desktop and Android. */}
   <SheetContent
     side="top"
     className="pb-3 pt-[max(0.75rem,env(safe-area-inset-top))] pl-[max(0.75rem,env(safe-area-inset-left))] pr-[max(0.75rem,env(safe-area-inset-right))]"
   >
   ```

   Sheet có đệm riêng (khác drawer và `AppShell` vốn không có đệm), nên dùng `max()` thay cho tiện ích `*-safe`.

4. **Chạy test, xác nhận đạt** — cùng lệnh / Expected: PASS.

5. **Commit** — `git add src/components/layout/header && git commit -m "feat(pwa): keep the mobile search sheet inside the safe area"`

### Task 8 — `ios-pwa.ts`: nhận biết iOS chưa cài

Hợp đồng:

```ts
export interface IosEnv {
  userAgent: string
  maxTouchPoints: number
  standalone?: boolean            // navigator.standalone (chỉ Safari iOS có)
  displayModeStandalone: boolean  // matchMedia('(display-mode: standalone)').matches
}

// iPhone/iPad/iPod trong UA, hoặc iPadOS giả UA Mac ('Macintosh' + maxTouchPoints > 1).
export function isIosDevice(env: Pick<IosEnv, 'userAgent' | 'maxTouchPoints'>): boolean

// iOS và không chạy standalone: chỉ app đã cài mới nhận push trên iOS 16.4+.
export function needsIosInstall(env: IosEnv): boolean

// Đọc IosEnv từ navigator và window (matchMedia thiếu → displayModeStandalone = false).
export function readIosEnv(): IosEnv
```

1. **Viết test thất bại** — tạo `src/lib/ios-pwa.test.ts`:

   ```ts
   import { describe, expect, it } from 'vitest'
   import { isIosDevice, needsIosInstall } from './ios-pwa'

   const IPHONE = 'Mozilla/5.0 (iPhone; CPU iPhone OS 17_4 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.4 Mobile/15E148 Safari/604.1'
   const IPAD_DESKTOP_UA = 'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.4 Safari/605.1.15'
   const ANDROID = 'Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0 Mobile Safari/537.36'

   describe('isIosDevice', () => {
     it.each([
       [IPHONE, 5, true],
       [IPAD_DESKTOP_UA, 5, true], // iPadOS asks for the desktop site
       [IPAD_DESKTOP_UA, 0, false], // a real Mac
       [ANDROID, 5, false],
     ])('%s with %i touch points → %s', (userAgent, maxTouchPoints, expected) => {
       expect(isIosDevice({ userAgent, maxTouchPoints })).toBe(expected)
     })
   })

   describe('needsIosInstall', () => {
     it('is true in Safari on an iPhone', () => {
       expect(needsIosInstall({ userAgent: IPHONE, maxTouchPoints: 5, standalone: false, displayModeStandalone: false })).toBe(true)
     })

     it.each([
       { standalone: true, displayModeStandalone: false },
       { standalone: undefined, displayModeStandalone: true },
     ])('is false once installed (%o)', (flags) => {
       expect(needsIosInstall({ userAgent: IPHONE, maxTouchPoints: 5, ...flags })).toBe(false)
     })

     it('is false off iOS', () => {
       expect(needsIosInstall({ userAgent: ANDROID, maxTouchPoints: 5, displayModeStandalone: false })).toBe(false)
     })
   })
   ```

2. **Chạy test, xác nhận thất bại** — `npx vitest run src/lib/ios-pwa.test.ts` / Expected: FAIL, `Failed to resolve import "./ios-pwa"`.

3. **Viết code tối thiểu** — tạo `src/lib/ios-pwa.ts`:

   ```ts
   // iOS only delivers web push to an app added to the home screen (iOS 16.4+); Safari tabs have no Notification API.

   export interface IosEnv {
     userAgent: string
     maxTouchPoints: number
     standalone?: boolean
     displayModeStandalone: boolean
   }

   export function isIosDevice({ userAgent, maxTouchPoints }: Pick<IosEnv, 'userAgent' | 'maxTouchPoints'>): boolean {
     if (/iPhone|iPad|iPod/.test(userAgent)) return true
     return /Macintosh/.test(userAgent) && maxTouchPoints > 1
   }

   export function needsIosInstall(env: IosEnv): boolean {
     return isIosDevice(env) && env.standalone !== true && !env.displayModeStandalone
   }

   export function readIosEnv(): IosEnv {
     return {
       userAgent: navigator.userAgent,
       maxTouchPoints: navigator.maxTouchPoints ?? 0,
       standalone: (navigator as Navigator & { standalone?: boolean }).standalone,
       displayModeStandalone:
         typeof window.matchMedia === 'function' && window.matchMedia('(display-mode: standalone)').matches,
     }
   }
   ```

4. **Chạy test, xác nhận đạt** — `npx vitest run src/lib/ios-pwa.test.ts` / Expected: PASS.

5. **Commit** — `git add src/lib/ios-pwa.ts src/lib/ios-pwa.test.ts && git commit -m "feat(pwa): detect iOS Safari that needs the app installed for push"`

### Task 9 — Trạng thái `'ios-needs-install'` của `usePushNotification`

`PushState` thêm `'ios-needs-install'`. Trạng thái đầu kiểm `needsIosInstall(readIosEnv())` **trước** kiểm `'Notification' in window` (Safari iOS chưa cài không có `Notification`). `subscribe` ở trạng thái này không làm gì (banner không có nút bật). iOS đã cài đi luồng hiện có: quyền xin từ nút bấm, đúng yêu cầu thao tác người dùng của iOS.

1. **Viết test thất bại** — trong `src/hooks/usePushNotification.test.ts`, thêm `describe` mới cuối file:

   ```ts
   describe('usePushNotification on iOS', () => {
     const IPHONE = 'Mozilla/5.0 (iPhone; CPU iPhone OS 17_4 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.4 Mobile/15E148 Safari/604.1'

     function stubIos(standalone: boolean) {
       vi.spyOn(navigator, 'userAgent', 'get').mockReturnValue(IPHONE)
       // jsdom has no maxTouchPoints at all, so spyOn would throw: define it.
       Object.defineProperty(navigator, 'maxTouchPoints', { value: 5, configurable: true })
       Object.defineProperty(navigator, 'standalone', { value: standalone, configurable: true })
       vi.stubGlobal('matchMedia', vi.fn().mockReturnValue({ matches: false }))
     }

     afterEach(() => {
       vi.restoreAllMocks()
       vi.unstubAllGlobals()
       delete (navigator as { standalone?: boolean }).standalone
       delete (navigator as { maxTouchPoints?: number }).maxTouchPoints
     })

     it('asks Safari users to install the app first, even without the Notification API', () => {
       stubIos(false)
       const originalNotification = window.Notification
       // @ts-expect-error Safari on iOS has no Notification API until the app is installed
       delete window.Notification
       try {
         const { result } = renderHook(() => usePushNotification('BFakeVapidKey'))
         expect(result.current.state).toBe('ios-needs-install')
       } finally {
         window.Notification = originalNotification
       }
     })

     it('uses the normal flow once installed', () => {
       stubIos(true)
       Object.defineProperty(window, 'Notification', {
         value: { permission: 'default', requestPermission: vi.fn() },
         configurable: true,
       })
       stubPushManager()
       const { result } = renderHook(() => usePushNotification('BFakeVapidKey'))
       expect(result.current.state).toBe('idle')
     })

     it('does not subscribe while the app is not installed', async () => {
       stubIos(false)
       const requestPermission = vi.fn()
       Object.defineProperty(window, 'Notification', {
         value: { permission: 'default', requestPermission },
         configurable: true,
       })
       const { result } = renderHook(() => usePushNotification('BFakeVapidKey'))
       await act(async () => { await result.current.subscribe() })
       expect(requestPermission).not.toHaveBeenCalled()
       expect(result.current.state).toBe('ios-needs-install')
     })
   })
   ```

   `userAgent` là getter trên `Navigator.prototype`, `vi.spyOn(navigator, 'userAgent', 'get')` tìm được nó. `maxTouchPoints` không tồn tại trong jsdom 29 nên phải `defineProperty` như trên.

2. **Chạy test, xác nhận thất bại** — `npx vitest run src/hooks/usePushNotification.test.ts` / Expected: FAIL — test 1 nhận `'unsupported'`, test 3 nhận state khác `'ios-needs-install'` và `requestPermission` được gọi; test 2 có thể đã đạt.

3. **Viết code tối thiểu** — trong `src/hooks/usePushNotification.ts`:
   - `import { needsIosInstall, readIosEnv } from '@/lib/ios-pwa'`
   - `export type PushState = 'idle' | 'loading' | 'granted' | 'denied' | 'unsupported' | 'error' | 'ios-needs-install'`
   - Dòng đầu của hàm khởi tạo `useState`: `if (needsIosInstall(readIosEnv())) return 'ios-needs-install'`
   - Dòng đầu của `subscribe`: `if (state === 'ios-needs-install') return` và thêm `state` vào mảng phụ thuộc của `useCallback` (`[state, vapidPublicKey]`).

4. **Chạy test, xác nhận đạt** — `npx vitest run src/hooks/usePushNotification.test.ts` / Expected: PASS (cả 5 test cũ).

5. **Commit** — `git commit -am "feat(pwa): report iOS Safari as needing install before push"`

### Task 10 — Hướng dẫn cài trên iOS trong `PushPermissionPrompt`

`isPushPromptVisible` đã trả `true` cho `'ios-needs-install'` (chỉ loại `granted`, `denied`, `unsupported`), nên `App.tsx` đã cấp chỗ cho banner và nút "Để sau" dùng cùng cơ chế ẩn 30 ngày. Chỉ cần nội dung banner.

1. **Viết test thất bại** — trong `src/components/PushPermissionPrompt.test.tsx`:
   - thêm `'ios-needs-install'` vào mảng của `it.each<PushState>([...])` "renders something exactly when isPushPromptVisible says so";
   - thêm test:

   ```tsx
   it('tells iOS Safari users how to install the app instead of offering to enable', () => {
     const onDismiss = vi.fn()
     render(<PushPermissionPrompt state="ios-needs-install" onEnable={vi.fn()} onDismiss={onDismiss} />)
     expect(screen.getByText(/Chia sẻ/)).toBeInTheDocument()
     expect(screen.getByText(/Thêm vào MH chính/)).toBeInTheDocument()
     expect(screen.queryByRole('button', { name: /bật thông báo/i })).not.toBeInTheDocument()
     fireEvent.click(screen.getByRole('button', { name: /để sau/i }))
     expect(onDismiss).toHaveBeenCalledOnce()
   })

   it('keeps the iOS hint short with py-2', () => {
     const { container } = render(<PushPermissionPrompt state="ios-needs-install" onEnable={vi.fn()} onDismiss={vi.fn()} />)
     expect(container.firstElementChild).toHaveClass('py-2')
   })
   ```

2. **Chạy test, xác nhận thất bại** — `npx vitest run src/components/PushPermissionPrompt.test.tsx` / Expected: FAIL — không có chữ "Chia sẻ", nút "Bật thông báo" vẫn hiện.

3. **Viết code tối thiểu** — trong `PushPermissionPrompt.tsx`, sau dòng `if (!isPushPromptVisible(state)) return null`:

   ```tsx
   // iOS delivers push only to the installed app, and has no beforeinstallprompt: explain the manual install.
   if (state === 'ios-needs-install') {
     return (
       <div className="flex items-center justify-between gap-4 rounded-lg border border-blue-200 bg-blue-50 px-4 py-2">
         <div className="text-sm text-blue-900">
           Để nhận thông báo trên iPhone, bấm <strong>Chia sẻ</strong> rồi <strong>Thêm vào MH chính</strong>, sau đó mở app từ màn hình chính.
         </div>
         <button onClick={onDismiss} className="shrink-0 rounded px-3 py-1 text-sm text-blue-600 hover:text-blue-800">
           Để sau
         </button>
       </div>
     )
   }
   ```

4. **Chạy test, xác nhận đạt** — `npx vitest run src/components/PushPermissionPrompt.test.tsx src/App.shell.test.tsx` / Expected: PASS.

5. **Commit** — `git commit -am "feat(pwa): show the iOS install hint in the push banner"`

### Task 11 — Popup "Có phiên bản mới" và vùng toast ở đáy màn hình

Hai phần tử `fixed` ở đáy nằm ngoài `AppShell`: popup cập nhật service worker trong `App.tsx` (`fixed bottom-4 right-4`) và viewport của toast trong `components/ui/toaster.tsx` (`fixed bottom-0 … p-4` dưới `sm`). Trên iPhone có thanh home, cả hai cách đáy 16px, nằm trong vùng khoảng 34px của thanh home; popup cập nhật chắc chắn hiện trên iOS ngay sau bản deploy này.

1. **Viết test thất bại**:
   - Trong `src/App.shell.test.tsx`, thêm cờ `const sw = vi.hoisted(() => ({ needRefresh: false }));` cạnh các cờ khác, mock module ảo:

     ```tsx
     vi.mock('virtual:pwa-register/react', () => ({
       useRegisterSW: () => ({
         needRefresh: [sw.needRefresh, vi.fn()],
         offlineReady: [false, vi.fn()],
         updateServiceWorker: vi.fn(),
       }),
     }));
     ```

     thêm `sw.needRefresh = false;` vào `afterEach`, và test:

     ```tsx
     // Fixed to the viewport, outside the shell's padding: keep it above the iPhone home indicator.
     it('keeps the update popup above the home indicator', () => {
       sw.needRefresh = true;
       window.history.pushState({}, '', '/404');
       render(<App />);
       const popup = screen.getByText('Có phiên bản mới').parentElement!;
       expect(popup).toHaveClass('bottom-[max(1rem,env(safe-area-inset-bottom))]');
       expect(popup).not.toHaveClass('bottom-4');
     });
     ```

   - Tạo `src/components/ui/toaster.test.tsx`:

     ```tsx
     import { render } from '@testing-library/react'
     import { describe, expect, it } from 'vitest'
     import { Toaster } from './toaster'

     describe('Toaster', () => {
       // On phones the toasts stack from the bottom edge; keep them above the iPhone home indicator.
       it('pads the phone viewport by the bottom safe-area inset', () => {
         render(<Toaster />)
         const viewport = document.querySelector('ol')!
         expect(viewport).toHaveClass('pb-[max(1rem,env(safe-area-inset-bottom))]', 'sm:pb-4')
       })
     })
     ```

2. **Chạy test, xác nhận thất bại** — `npx vitest run src/App.shell.test.tsx src/components/ui/toaster.test.tsx` / Expected: FAIL, thiếu các class `max(...)`.

3. **Viết code tối thiểu**:
   - `App.tsx`: trong class của popup cập nhật, thay `bottom-4` bằng `bottom-[max(1rem,env(safe-area-inset-bottom))]`.
   - `toaster.tsx`: trong class của `ToastPrimitive.Viewport`, thêm `pb-[max(1rem,env(safe-area-inset-bottom))] sm:pb-4` ngay sau `p-4`.

4. **Chạy test, xác nhận đạt** — cùng lệnh / Expected: PASS.

5. **Commit** — `git add src/App.tsx src/App.shell.test.tsx src/components/ui/toaster.tsx src/components/ui/toaster.test.tsx && git commit -m "feat(pwa): keep the update popup and toasts above the iPhone home indicator"`

## Verification

Từ `frontend`:

- `npm run typecheck`
- `npx tsc -p tsconfig.node.json`
- `npm run lint`
- `npm run test`
- `npm run build`, rồi `grep -c 'rel="apple-touch-icon"' dist/index.html` (1) và `grep -c 'viewport-fit=cover' dist/index.html` (1)

Từ `backend` (với `TEST_DB_CONNECTION` ở Task 1):

- `dotnet build`
- `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~BrandingIconTests"`

Thử tay (Chrome DevTools, chế độ thiết bị iPhone 14 Pro): header, drawer, sheet tìm kiếm hiển thị bình thường; desktop 1366×657 không đổi (form phiếu kho không cuộn thừa). Thử trên iPhone thật theo mục 3–4 của Final Verification trong [SUMMARY.md](SUMMARY.md).

## Exit Criteria

- `GET /api/settings/branding/icon/180` trả PNG 180×180; 100 và 0 vẫn trả 400.
- `dist/index.html` có favicon và `apple-touch-icon` trỏ tới origin của API, có `viewport-fit=cover` và các meta iOS.
- Popup cập nhật và vùng toast trên điện thoại cách đáy ít nhất 16px và không nằm dưới thanh home.
- `pt-safe`/`pb-safe`/`pl-safe`/`pr-safe` tồn tại; `AppShell` và drawer mobile dùng chúng; sheet tìm kiếm mobile giữ ít nhất 12px và tăng theo safe-area; giao diện desktop không đổi.
- Safari iOS chưa cài thấy hướng dẫn cài; iOS đã cài đi luồng bật thông báo hiện có.
- Toàn bộ test frontend, typecheck, lint, build và test backend liên quan đạt.
