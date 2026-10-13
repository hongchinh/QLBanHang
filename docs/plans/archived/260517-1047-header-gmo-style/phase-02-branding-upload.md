# Phase 02 — Branding Upload (BE + FE)

**Status:** [ ] pending
**Complexity:** M

## Objective
Cho admin (`user_settings.manage`) upload **2 ảnh logo công ty** (logo ngang + logo vuông) trong **tab "Logo công ty"** thuộc page `/settings/my-quotation-settings` hiện có. Lưu **DB BLOB** (`varbinary(max)`), serve qua endpoint stream image + ETag để browser cache. Brand block trong header dùng logo này — variant `full` khi sidebar mở, variant `mark` khi sidebar collapsed/mobile.

## Files

**Tạo mới**
- `backend/src/OrderMgmt.Domain/Branding/SystemBranding.cs`
- `backend/src/OrderMgmt.Application/System/Branding/Interfaces/IBrandingService.cs`
- `backend/src/OrderMgmt.Application/System/Branding/Services/BrandingService.cs`
- `backend/src/OrderMgmt.Application/System/Branding/Models/BrandingDto.cs`
- `backend/src/OrderMgmt.Application/System/Branding/Models/LogoStreamResult.cs`
- `backend/src/OrderMgmt.WebApi/Controllers/SettingsController.cs`
- `backend/src/OrderMgmt.Infrastructure/Persistence/Migrations/<timestamp>_AddSystemBranding.cs` (auto-generated)
- `frontend/src/components/ui/tabs.tsx` (shadcn `tabs` — chỉ tạo nếu chưa có)
- `frontend/src/features/branding/api.ts`
- `frontend/src/features/branding/hooks.ts`
- `frontend/src/features/branding/branding-tab.tsx` (UI tab "Logo công ty")

**Sửa**
- `backend/src/OrderMgmt.Infrastructure/Persistence/AppDbContext.cs` — thêm `DbSet<SystemBranding>` + Fluent config + seed singleton row
- `frontend/src/components/layout/header/brand-block.tsx` — đọc `useBranding()` + variant theo `collapsed`, fallback icon+text khi chưa upload
- `frontend/src/pages/settings/my-quotation-settings-page.tsx` — wrap nội dung hiện tại trong tab "Cài đặt báo giá" + render tab "Logo công ty" có điều kiện theo permission
- `frontend/package.json` — thêm `@radix-ui/react-tabs` (nếu shadcn `tabs` chưa có)

**KHÔNG đụng**
- `Program.cs` — không cần `UseStaticFiles` (DB BLOB, không filesystem).
- Router config — không tạo route mới (`/admin/branding` bỏ; reuse `/settings/my-quotation-settings`).
- `app-layout.tsx` nav config — không thêm nav item "Branding".

## Tasks

1. **Cài shadcn `tabs`** (nếu chưa có):
   ```
   cd frontend && npx shadcn@latest add tabs
   ```
   Verify `frontend/src/components/ui/tabs.tsx` tồn tại + dependency `@radix-ui/react-tabs` trong `package.json`.

2. Tạo entity `SystemBranding` trong `backend/src/OrderMgmt.Domain/Branding/`:
   ```csharp
   public sealed class SystemBranding {
       public int Id { get; set; } // singleton: Id = 1
       public byte[]? LogoFull { get; set; }
       public string? LogoFullContentType { get; set; } // "image/png" | "image/jpeg" | "image/svg+xml"
       public byte[]? LogoMark { get; set; }
       public string? LogoMarkContentType { get; set; }
       public DateTimeOffset UpdatedAt { get; set; }
       public Guid? UpdatedBy { get; set; }
   }
   ```

3. Cập nhật `AppDbContext`:
   - `DbSet<SystemBranding> SystemBranding`.
   - Fluent config: table `system_branding`, PK `Id`.
   - `LogoFull` + `LogoMark`: `HasColumnType("varbinary(max)")`.
   - `LogoFullContentType` + `LogoMarkContentType`: `HasMaxLength(64)`.
   - Seed singleton: `HasData(new SystemBranding { Id = 1, UpdatedAt = DateTimeOffset.UnixEpoch })` (logos null).

4. Tạo migration:
   ```
   dotnet ef migrations add AddSystemBranding `
     -p backend/src/OrderMgmt.Infrastructure `
     -s backend/src/OrderMgmt.WebApi
   ```

5. Tạo DTOs:
   - `BrandingDto { bool HasLogoFull; bool HasLogoMark; DateTimeOffset UpdatedAt; }` — **không** trả bytes; FE chỉ cần biết có/không + version để cache-bust.
   - `LogoStreamResult { byte[] Content; string ContentType; string ETag; }` (record).

6. Tạo `IBrandingService` + `BrandingService`:
   - `Task<BrandingDto> GetMetaAsync(CancellationToken ct)` — trả `HasLogoFull/Mark` + `UpdatedAt`.
   - `Task<LogoStreamResult?> GetLogoAsync(string variant, CancellationToken ct)` — variant ∈ `"full"|"mark"`; return `null` nếu chưa upload; ETag = `"\"{UpdatedAt.ToUnixTimeMilliseconds()}-{variant}\""`.
   - `Task<BrandingDto> UpdateAsync(IFormFile? logoFull, IFormFile? logoMark, Guid userId, CancellationToken ct)`:
     - Validate mỗi file (nếu có): size ≤ 2 MB, content-type ∈ `image/png`, `image/jpeg`, `image/svg+xml`.
     - Cho phép upload riêng từng variant (1 trong 2 nullable) — admin có thể chỉ thay logo mark.
     - Đọc `IFormFile` → `byte[]`, lưu vào cột tương ứng + content type.
     - Update `UpdatedAt = DateTimeOffset.UtcNow` + `UpdatedBy = userId`.

7. Tạo `SettingsController`:
   ```csharp
   [ApiController]
   public class SettingsController : ApiControllerBase {
       private readonly IBrandingService _branding;
       public SettingsController(IBrandingService branding) { _branding = branding; }

       [HttpGet("/api/settings/branding")]
       [Authorize]
       public async Task<ActionResult<ApiResponse<BrandingDto>>> GetBranding(CancellationToken ct)
           => Success(await _branding.GetMetaAsync(ct));

       [HttpGet("/api/settings/branding/logo")]
       [Authorize]
       [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Client)]
       public async Task<IActionResult> GetLogo([FromQuery] string variant, CancellationToken ct) {
           if (variant != "full" && variant != "mark") return BadRequest();
           var result = await _branding.GetLogoAsync(variant, ct);
           if (result == null) return NotFound();
           // ETag handling: trả 304 nếu If-None-Match khớp
           var ifNoneMatch = Request.Headers.IfNoneMatch.ToString();
           if (ifNoneMatch == result.ETag) return StatusCode(StatusCodes.Status304NotModified);
           Response.Headers.ETag = result.ETag;
           Response.Headers.CacheControl = "private, max-age=300";
           return File(result.Content, result.ContentType);
       }

       [HttpPut("/api/settings/branding")]
       [HasPermission(Permissions.UserSettings.Manage)]
       [RequestSizeLimit(5 * 1024 * 1024)] // 2 file × 2MB + overhead
       public async Task<ActionResult<ApiResponse<BrandingDto>>> UpdateBranding(
           IFormFile? logoFull, IFormFile? logoMark, CancellationToken ct) {
           if (logoFull == null && logoMark == null)
               return BadRequest("Cần ít nhất 1 file.");
           return Success(await _branding.UpdateAsync(logoFull, logoMark, CurrentUserId, ct));
       }
   }
   ```
   - `CurrentUserId` lấy từ pattern hiện có (`ApiControllerBase` hoặc `ICurrentUser`).

8. Cập nhật `Program.cs`:
   - `services.AddScoped<IBrandingService, BrandingService>();`
   - **Không** cần `UseStaticFiles` cho branding (DB BLOB).

9. Apply migration:
   ```
   dotnet ef database update `
     -p backend/src/OrderMgmt.Infrastructure `
     -s backend/src/OrderMgmt.WebApi
   ```

10. Tạo `frontend/src/features/branding/api.ts`:
    ```ts
    export interface BrandingMeta {
      hasLogoFull: boolean;
      hasLogoMark: boolean;
      updatedAt: string;
    }
    export const getBrandingMeta = (): Promise<BrandingMeta> =>
      apiClient.get('/api/settings/branding').then(r => r.data.data);

    // URL helpers (KHÔNG fetch — dùng làm <img src>)
    export const logoUrl = (variant: 'full' | 'mark', version: string) =>
      `/api/settings/branding/logo?variant=${variant}&v=${encodeURIComponent(version)}`;

    export const uploadBranding = (logoFull?: File, logoMark?: File): Promise<BrandingMeta> => {
      const fd = new FormData();
      if (logoFull) fd.append('logoFull', logoFull);
      if (logoMark) fd.append('logoMark', logoMark);
      return apiClient.put('/api/settings/branding', fd).then(r => r.data.data);
    };
    ```
    - `v=<updatedAt>` cache-bust khi admin upload mới (kèm ETag server-side là 2 lớp bảo vệ).

11. Tạo `frontend/src/features/branding/hooks.ts`:
    - `useBrandingMeta()`: React Query, key `['branding-meta']`, `staleTime: 5 * 60_000`, `placeholderData: { hasLogoFull: false, hasLogoMark: false, updatedAt: '' }`.
    - `useUploadBranding()`: useMutation, onSuccess `queryClient.invalidateQueries({queryKey:['branding-meta']})` + toast success.

12. Sửa `frontend/src/components/layout/header/brand-block.tsx`:
    - Signature: `<BrandBlock collapsed={boolean} />`.
    - Call `useBrandingMeta()`.
    - Logic render:
      - Nếu `collapsed` + `meta.hasLogoMark` → `<img src={logoUrl('mark', meta.updatedAt)} alt="Logo" className="h-8 w-8 object-contain" />`.
      - Nếu `!collapsed` + `meta.hasLogoFull` → `<img src={logoUrl('full', meta.updatedAt)} alt="Logo" className="h-8 object-contain" />`.
      - Fallback (chưa upload variant tương ứng): icon `FileText` + text "QLBanHang" (như placeholder phase 01); khi collapsed thì chỉ icon.
    - Bọc trong `<Link to="/">` để click logo về home.
    - Fix height container để tránh layout shift khi `<img>` load.

13. Sửa `frontend/src/pages/settings/my-quotation-settings-page.tsx`:
    - Import `Tabs, TabsList, TabsTrigger, TabsContent` từ `@/components/ui/tabs`.
    - Import `useAuthStore` để check `hasPermission('user_settings.manage')`.
    - Wrap nội dung hiện tại trong tab "Cài đặt báo giá" (`value="quotation"`).
    - Render tab "Logo công ty" (`value="branding"`) **chỉ khi** `hasPermission('user_settings.manage')`.
    - TabsList ẩn hoàn toàn nếu user không có quyền branding (chỉ 1 tab) — tránh UI lạ.
    - TabsContent "branding" → render `<BrandingTab />`.

14. Tạo `frontend/src/features/branding/branding-tab.tsx`:
    - Layout 2 column (responsive: 1 col dưới `md`), mỗi col là 1 `Card`:
      - **Card 1 — "Logo ngang"**: dùng cho menu mở rộng, recommended `240×64px`, PNG/JPG/SVG, ≤ 2MB.
        - Preview hiện tại (call `logoUrl('full', meta.updatedAt)` nếu `meta.hasLogoFull`, else placeholder text "Chưa có logo").
        - File input + preview `URL.createObjectURL(file)` trước khi upload.
        - Button "Tải lên logo ngang" disabled khi chưa chọn file.
      - **Card 2 — "Logo vuông"**: dùng cho menu thu gọn, favicon, mobile. Recommended `64×64px`, ưu tiên SVG. Cấu trúc tương tự.
    - 1 button "Lưu thay đổi" submit cả 2 (chỉ gửi file đã chọn — `uploadBranding(logoFull, logoMark)`).
    - Hint text dưới mỗi card giải thích "logo này sẽ hiện khi sidebar [mở rộng/thu gọn]".
    - Toast success/error qua `@/lib/use-toast`.
    - Client-side validate size + mime trước khi gọi API.

15. Build BE (chỉ projects đã sửa, theo memory `feedback_build_skip_when_app_running.md`):
    ```
    dotnet build backend/src/OrderMgmt.Domain
    dotnet build backend/src/OrderMgmt.Application
    dotnet build backend/src/OrderMgmt.Infrastructure
    dotnet build backend/src/OrderMgmt.WebApi
    ```

16. Build FE: `cd frontend && npm run typecheck && npm run lint`.

17. Manual test:
    - Login admin → vào `/settings/my-quotation-settings` → thấy 2 tab.
    - Tab "Logo công ty" → upload logo ngang PNG 500KB → preview hiện → toast success.
    - Header brand block hiển thị logo ngang ngay (không cần F5).
    - Click hamburger collapse sidebar → brand block **chưa** đổi (vì chưa upload logo mark) → fallback icon.
    - Upload tiếp logo vuông SVG → collapsed brand block đổi sang logo vuông.
    - F5 → cả 2 vẫn còn; DevTools Network: lần thứ 2 request logo → status `304 Not Modified` (ETag work).
    - Logout, login user thường (Sales) → vào `/settings/my-quotation-settings` → **không** thấy tab "Logo công ty" (chỉ thấy nội dung cài đặt báo giá như cũ, không có tabs UI).
    - User thường gọi `PUT /api/settings/branding` qua curl → 403.
    - Thử upload file 5MB → error 400 hoặc client-side block.
    - Thử upload file `.exe` rename `.png` → BE validate content-type sai → error.

## Verification

```
# Backend (chỉ projects đã đổi)
dotnet build backend/src/OrderMgmt.Domain backend/src/OrderMgmt.Application backend/src/OrderMgmt.Infrastructure backend/src/OrderMgmt.WebApi

# Frontend
cd frontend && npm run typecheck && npm run lint

# Manual: admin upload 2 variant → verify header swap theo collapse state
```

## Exit Criteria
- [ ] Migration `AddSystemBranding` applied; row `Id=1` tồn tại trong `system_branding` với cả 4 cột logo nullable.
- [ ] `GET /api/settings/branding` → `{hasLogoFull, hasLogoMark, updatedAt}` (không trả bytes).
- [ ] `GET /api/settings/branding/logo?variant=full` chưa upload → 404; sau upload → 200 + `Content-Type` đúng + `ETag` header.
- [ ] Request lần 2 với `If-None-Match` khớp ETag → 304.
- [ ] `PUT /api/settings/branding` với admin upload 1 hoặc 2 file ≤2MB → 200 + meta cập nhật.
- [ ] `PUT /api/settings/branding` không file → 400.
- [ ] `PUT /api/settings/branding` với user thường → 403.
- [ ] Upload file >2MB hoặc mime sai → 400 + lỗi rõ ràng.
- [ ] Page `/settings/my-quotation-settings`: admin thấy 2 tab; user thường thấy nội dung cài đặt báo giá (không có TabsList).
- [ ] Brand block expanded → logo full; collapsed → logo mark; thiếu variant nào → fallback icon+text.
- [ ] F5 vẫn giữ logo (DB persist + browser cache 5 phút).
- [ ] FE client-side validate size + mime trước khi gọi API.
