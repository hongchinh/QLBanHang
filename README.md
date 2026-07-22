# QLDonHang - Phần mềm Quản lý Báo giá

Hệ thống quản lý quy trình báo giá theo luồng:

> Draft → Sent → Confirmed → Cancelled

> **Scope hiện tại**: báo giá là chứng từ trung tâm. Hệ thống đã có auth/RBAC, khách hàng, hàng hóa, báo giá, export Excel/PDF, dashboard, báo cáo doanh thu, quản lý user/role, cấu hình mẫu báo giá theo user, branding, notification, global search và tạo mã QR thanh toán (VietQR).

---

## Tech stack

**Backend**

- .NET 9 Web API, Clean Architecture (Domain / Application / Infrastructure / WebApi)
- PostgreSQL 16 + EF Core 9 (Npgsql)
- JWT Bearer + Role-based + Permission-based authorization (policy `perm:<permission_code>`)
- FluentValidation, Mapster, Serilog, Swagger
- BCrypt password hashing
- Global exception handler → standard API response
- ClosedXML + LibreOffice cho export Excel/PDF báo giá

**Frontend**

- Vite + React 18 + TypeScript
- TailwindCSS + shadcn/ui style components
- React Router v6, TanStack Query, TanStack Table
- Axios, React Hook Form + Zod, Zustand (persisted auth store)
- Lucide React, Recharts, date-fns

**Database**

- PostgreSQL local qua docker-compose (`postgres:16-alpine`)
- pgAdmin tại `http://localhost:5050`

---

## Cấu trúc thư mục

```
QLDonHang/
├── backend/
│   ├── OrderMgmt.sln
│   ├── Directory.Build.props
│   └── src/
│       ├── OrderMgmt.Domain/        # Entities, Enums, Constants, DomainException
│       ├── OrderMgmt.Application/   # DTOs, Interfaces, Services, Validators, Mapping
│       ├── OrderMgmt.Infrastructure/# DbContext, Migrations, JWT, BCrypt, Seed
│       └── OrderMgmt.WebApi/        # Controllers, Middleware, Authorization, Program.cs
├── frontend/
│   ├── package.json
│   ├── vite.config.ts
│   └── src/
│       ├── components/    # ui/, layout/, auth helpers, customer autocomplete
│       ├── features/      # auth, customers, products, quotations, dashboard, reports, admin...
│       ├── pages/         # route components
│       ├── routes/        # ProtectedRoute
│       ├── stores/        # zustand stores
│       ├── lib/           # api-client, query-client, utils
│       ├── App.tsx
│       └── main.tsx
├── docs/                  # BD / PDR / architecture
├── docker-compose.yml
└── README.md
```

---

## Chạy local

### 1) Khởi động PostgreSQL

```powershell
docker compose up -d postgres
```

`docker-compose.yml` tạo database `qldonhang` với user `qldh` / `qldh_dev_password`. Cấu hình `appsettings.Development.json` hiện đang trỏ tới `Host=localhost;Port=5432;Database=qldonhang_test;Username=postgres;Password=1`, vì vậy khi dùng database từ docker-compose cần override connection string trước khi chạy backend:

```powershell
$env:ConnectionStrings__Default="Host=localhost;Port=5432;Database=qldonhang;Username=qldh;Password=qldh_dev_password"
```

(Tùy chọn) pgAdmin map ở http://localhost:5050 (admin@qldh.local / admin). Backend development cũng dùng port 5050, nên chỉ chạy pgAdmin khi không chạy backend trên port mặc định hoặc đổi một trong hai port.

### 2) Backend

```powershell
cd backend
dotnet restore
dotnet build
dotnet run --project src/OrderMgmt.WebApi
```

- API: http://localhost:5050
- Swagger: http://localhost:5050/swagger
- Health: http://localhost:5050/health/live và http://localhost:5050/health/ready

Khi khởi động, nếu `Database:AutoMigrateAndSeed = true` (mặc định), hệ thống sẽ tự động:

- Chạy EF migrations.
- Seed permissions, 5 roles (`ADMIN`, `SALES`, `ACCOUNTANT`, `WAREHOUSE`, `MANAGER`).
- Seed user `admin` / `Admin@123`.
- Seed danh mục nhóm hàng và đơn vị tính mặc định.

### 3) Frontend

```powershell
cd frontend
npm install
npm run dev
```

- App: http://localhost:5173
- Dev proxy `/api` → http://localhost:5050 đã cấu hình sẵn.

Đăng nhập: `admin` / `Admin@123`.

---

## EF Core migrations

Khi thêm/đổi entity trong `OrderMgmt.Domain`:

```powershell
cd backend
dotnet ef migrations add <Name> --project src/OrderMgmt.Infrastructure --startup-project src/OrderMgmt.WebApi -o Persistence/Migrations
dotnet ef database update --project src/OrderMgmt.Infrastructure --startup-project src/OrderMgmt.WebApi
```

Cần cài CLI lần đầu: `dotnet tool install --global dotnet-ef`.

---

## Phân quyền

- Roles được seed sẵn — đã ánh xạ phù hợp với 5 vai trò trong tài liệu BD (Admin / Sales / Accountant / Warehouse / Manager).
- Permission codes theo chuẩn `<module>.<action>` (xem `OrderMgmt.Domain/Constants/Permissions.cs`).
- Bảo vệ endpoint bằng attribute:

```csharp
[HasPermission(Permissions.Customers.Update)]
public async Task<...> Update(...)
```

- Frontend kiểm tra qua `useAuthStore.hasPermission('customers.update')` hoặc `<ProtectedRoute permission="customers.update">`.

---

## Standard API response

```json
{
  "success": true,
  "data": { /* ... */ },
  "error": null,
  "timestamp": "2026-05-12T10:30:00+00:00"
}
```

Khi lỗi:

```json
{
  "success": false,
  "data": null,
  "error": {
    "code": "VALIDATION",
    "message": "Dữ liệu không hợp lệ.",
    "details": { "Name": ["Tên khách hàng là bắt buộc"] }
  }
}
```

`GlobalExceptionMiddleware` map các exception (`DomainException`, `NotFoundException`, `ConflictException`, `ForbiddenException`, FluentValidation `ValidationException`, …) sang HTTP status tương ứng.

---

## Thêm một module mới

Theo pattern hiện tại, module nghiệp vụ nên đi qua Domain → Application → Infrastructure → WebApi, sau đó thêm frontend feature/page tương ứng.

**Backend**:

1. `Domain/Entities/<Module>/Foo.cs` — entity kế thừa `BaseEntity` nếu là nghiệp vụ soft-delete/audit.
2. `Infrastructure/Persistence/Configurations/FooConfiguration.cs` — EF mapping + indexes + query filter.
3. `Application/Common/Interfaces/IAppDbContext.cs` — thêm `DbSet<Foo>`.
4. `Application/<Module>/Foos/Models/FooDto.cs` — DTO + request types.
5. `Application/<Module>/Foos/Validators/FooValidators.cs` — FluentValidation.
6. `Application/<Module>/Foos/Interfaces/IFooService.cs` + `Services/FooService.cs`.
7. `Application/DependencyInjection.cs` — đăng ký service.
8. `Domain/Constants/Permissions.cs` — thêm permission codes.
9. `Infrastructure/Persistence/Seed/DbSeeder.cs` — seed permissions vào roles phù hợp.
10. `WebApi/Controllers/FoosController.cs` — endpoints + `[HasPermission(...)]`.
11. Migration: `dotnet ef migrations add Add<Module>`.

**Frontend**:

1. `features/<module>/types.ts` — TS types khớp DTO.
2. `features/<module>/api.ts` — gọi API.
3. `features/<module>/hooks.ts` — TanStack Query hooks.
4. `features/<module>/schema.ts` — Zod schema.
5. `pages/<module>/<module>-list-page.tsx` + `<module>-form-page.tsx`.
6. `App.tsx` — thêm `<Route>` bọc trong `<ProtectedRoute permission="...">`.
7. `components/layout/app-layout.tsx` — thêm nav item.

---

## Tài liệu

- [docs/SUMMARY.md](docs/SUMMARY.md) — index tài liệu.
- [docs/architecture/system-architecture.md](docs/architecture/system-architecture.md) — kiến trúc hệ thống hiện tại.
- [docs/codebase/directory-structure.md](docs/codebase/directory-structure.md) — cấu trúc repo và module.
- [docs/code-standard/conventions.md](docs/code-standard/conventions.md) — conventions backend/frontend.
- [docs/project-pdr/product-goals.md](docs/project-pdr/product-goals.md) — mục tiêu sản phẩm và phạm vi.

---

## Deploy Railway

Hệ thống đóng gói sẵn 2 Dockerfile + Caddyfile để deploy lên [Railway](https://railway.com) theo mô hình 3 service: PostgreSQL, Backend, Frontend.

### 1) Tạo project + PostgreSQL service

1. Đăng nhập railway.com → **New Project** → **Empty Project**.
2. Trong project: **+ New** → **Database** → **Add PostgreSQL**.
3. Mở service PostgreSQL → tab **Variables** → ghi nhận `DATABASE_URL` (hoặc `PGHOST`, `PGPORT`, `PGUSER`, `PGPASSWORD`, `PGDATABASE`). Railway cung cấp `DATABASE_URL` dạng `postgresql://user:pass@host:port/db`.

> Backend cần connection string dạng Npgsql: `Host=...;Port=...;Database=...;Username=...;Password=...;SSL Mode=Require;Trust Server Certificate=true`. Có thể dùng biến `${{Postgres.PGHOST}}` v.v. để tham chiếu chéo giữa các service (xem bước 4).

### 2) Tạo Backend service

1. **+ New** → **GitHub Repo** → chọn repo này.
2. Settings → **Root Directory**: `/backend`
3. Settings → **Build**:
   - Builder: **Dockerfile**
   - Dockerfile Path: `Dockerfile` (mặc định — Railway sẽ build `backend/Dockerfile`).
4. Settings → **Networking** → **Generate Domain** (sau khi set xong biến môi trường).

### 3) Tạo Frontend service

1. **+ New** → **GitHub Repo** → chọn cùng repo.
2. Settings → **Root Directory**: `/frontend`
3. Settings → **Build**: Builder = **Dockerfile**, Dockerfile Path = `Dockerfile`.
4. Settings → **Networking** → **Generate Domain**.

> Vite inline biến `VITE_API_BASE_URL` vào bundle **tại thời điểm build**, vì vậy nếu thay đổi URL backend phải **Redeploy** frontend (không chỉ restart).

### 4) Set biến môi trường — Backend

Trong service Backend → tab **Variables**, thêm:

| Biến | Giá trị |
|---|---|
| `ConnectionStrings__DefaultConnection` | `Host=${{Postgres.PGHOST}};Port=${{Postgres.PGPORT}};Database=${{Postgres.PGDATABASE}};Username=${{Postgres.PGUSER}};Password=${{Postgres.PGPASSWORD}};SSL Mode=Require;Trust Server Certificate=true` |
| `Frontend__Url` | URL frontend Railway (vd. `https://qldh-frontend.up.railway.app`) — dùng cho CORS |
| `Jwt__Secret` | Chuỗi ngẫu nhiên ≥ 32 ký tự (vd. `openssl rand -base64 48`) |
| `Jwt__Issuer` | `OrderMgmt` |
| `Jwt__Audience` | `OrderMgmtClient` |
| `Jwt__ExpiresInMinutes` | `60` |
| `Seed__AdminPassword` | Mật khẩu admin khởi tạo (vd. `Admin@123` — đổi ngay sau login) |
| `Database__AutoMigrateAndSeed` | `true` — bật auto migrate + seed cho MVP |
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `AuthCookie__SameSite` | `None` (khi frontend khác domain với backend) |
| `AuthCookie__Secure` | `true` |

> Railway tự inject biến `PORT`. Backend lắng nghe trên `http://0.0.0.0:${PORT}` (xem `backend/Dockerfile` ENTRYPOINT).

### 5) Set biến môi trường — Frontend

Trong service Frontend → tab **Variables**, thêm:

| Biến | Giá trị |
|---|---|
| `VITE_API_BASE_URL` | URL backend Railway + `/api` (vd. `https://qldh-backend.up.railway.app/api`) |

Để Railway truyền biến vào build stage của Docker, mở **Settings → Build → Build Args** và khai báo `VITE_API_BASE_URL` (Railway sẽ tự forward từ Variables vào `ARG`).

### 6) Generate domain + Redeploy

1. Backend service → **Settings → Networking → Generate Domain**.
2. Copy URL backend → cập nhật `VITE_API_BASE_URL` ở frontend service.
3. Frontend service → **Settings → Networking → Generate Domain**.
4. Copy URL frontend → cập nhật `Frontend__Url` ở backend service.
5. Mỗi service → menu **⋯ → Redeploy** để áp dụng biến môi trường mới.

### 7) Smoke test

| Endpoint | Kỳ vọng |
|---|---|
| `https://<backend>/health` | 200 `Healthy` |
| `https://<backend>/health/ready` | 200 `Healthy` (đã kết nối Postgres) |
| `https://<frontend>` | Trang login QLDonHang |
| Login `admin` / `Seed__AdminPassword` | Vào dashboard |

### Troubleshooting

**502 Bad Gateway**
- Mở **Deploy Logs** của service backend. Lỗi phổ biến: backend bind sai port → kiểm tra log có dòng `Now listening on: http://0.0.0.0:<PORT>`. Nếu thiếu, kiểm tra ENTRYPOINT của Dockerfile có expand `${PORT}` không (dùng `sh -c`, không phải JSON array).
- Backend crash sớm: thiếu `Jwt__Secret` (< 32 ký tự) → app fail-fast tại startup. Set lại biến rồi Redeploy.
- Healthcheck timeout: tăng **Settings → Deploy → Healthcheck Timeout** lên 300s cho lần đầu (migrate có thể chạy chậm).

**CORS error (`No 'Access-Control-Allow-Origin'`)**
- Backend chỉ allow origin có trong `Frontend__Url`. Kiểm tra biến này = URL frontend chính xác (không có dấu `/` cuối, đúng scheme `https://`).
- Nếu app gọi qua nhiều domain (preview, custom), thêm vào `Cors__Origins__0`, `Cors__Origins__1`, … (ASP.NET binding format cho array).
- Cookie auth khác domain: phải có `AuthCookie__SameSite=None` + `AuthCookie__Secure=true`, browser sẽ từ chối cookie nếu không đủ điều kiện.
- **Triệu chứng**: login OK nhưng F5 (refresh trang) tự logout → cookie `qldh.refresh` không attach vào `POST /auth/refresh` cross-site. Backend đã fail-fast tại startup nếu `SameSite=None` mà thiếu `Secure=true`.

**Database connection / migration**
- `relation "..." does not exist` → migrate chưa chạy. Bật `Database__AutoMigrateAndSeed=true` rồi Redeploy.
- `password authentication failed` → connection string sai. Dùng `${{Postgres.PGUSER}}` thay vì hard-code.
- `SSL connection is required` → thêm `SSL Mode=Require;Trust Server Certificate=true` vào cuối connection string.
- `28P01` / `connection refused` → reference biến Postgres sai service name (chú ý tên service Postgres trong Railway).

**Frontend không gọi đúng API**
- DevTools → Network → request URL vẫn là `/api/...` (relative) → biến `VITE_API_BASE_URL` không được inject vào build. Khai báo trong **Build Args** rồi Redeploy frontend service (không phải Restart).

**Frontend build lỗi `npm error EUSAGE … Missing: esbuild@0.28.0 from lock file`**
- Nguyên nhân: `vitest@4` peer trên `vite^6+`, nhưng project pin `vite@5`. npm 10 (Railway) và npm 11 (local) resolve `package-lock.json` khác nhau nên `npm ci` từ chối.
- Đã fix trong `frontend/Dockerfile`: dùng `npm install --legacy-peer-deps` thay cho `npm ci`.
- Fix bền vững (làm sau): hoặc nâng `vite` lên `^6`, hoặc hạ `vitest` xuống `^3.x` để khớp peer.

---

## Lộ trình tiếp theo (theo BD §21 MVP)

- **Giai đoạn 1**: Hàng hóa → Báo giá → In báo giá PDF → Đơn hàng → In đơn hàng → Biên bản bàn giao → Báo cáo doanh thu/lợi nhuận cơ bản.
- **Giai đoạn 2**: Trạng thái giao hàng, ghi nhận thanh toán, công nợ, lịch sử chỉnh sửa, cấu hình mẫu in. (QR thanh toán đã hoàn thành — xem phần Tài liệu.)
- **Giai đoạn 3**: Dashboard tổng quan, báo cáo nâng cao theo khách hàng/sản phẩm/nhân viên, import Excel.
