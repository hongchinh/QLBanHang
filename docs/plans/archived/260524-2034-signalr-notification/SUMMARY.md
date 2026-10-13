# SignalR Real-time Notification Badge

## Goal

Thêm SignalR vào QLBanHang để cập nhật badge số đỏ trên chuông notification ngay lập tức khi có notification mới — thay vì chờ polling 60 giây. Tất cả tab đang mở của cùng một user đều được cập nhật đồng thời.

## Scope

**In scope:**
- `IRealtimeNotifier` interface (Application layer) + `SignalRNotifier` implementation (WebApi layer)
- `NotificationHub` — ASP.NET Core SignalR hub, gom connection vào group `user-{userId}`
- Sửa `NotificationService.SendAsync` — gọi `IRealtimeNotifier.NotifyUserAsync` sau khi save DB
- Cấu hình SignalR trong `Program.cs`: `AddSignalR`, `MapHub`, JWT `OnMessageReceived`
- `useNotificationHub` React hook — connect SignalR, invalidate `unread-count` query khi nhận `NewNotification`
- Mount hook trong `AppLayout`

**Out of scope:**
- Toast/snackbar hiển thị nội dung notification (chỉ badge)
- Typing indicators, presence, hay bất kỳ tính năng SignalR nào khác
- Backfill notifications cho user khi connect (đã có REST API + polling)

## Assumptions

- ASP.NET Core SignalR đã có sẵn trong .NET SDK — không cần cài NuGet package thêm
- Token JWT được lưu trong Zustand store (`useAuthStore.getState().accessToken`) — dùng làm `accessTokenFactory`
- CORS đã có `AllowCredentials()` → SignalR long-polling hoạt động; không cần sửa CORS
- Integration tests sẽ dùng `WebAppFactory` — SignalR registered, nhưng không có client kết nối → `SendAsync` trên group rỗng là no-op, không gây lỗi

## Risks

- **JWT PostConfigure ordering**: `PostConfigure` chạy sau tất cả `Configure` calls — `OnMessageReceived` sẽ override `Events` nếu `Configure` cũng set `Events`. Kiểm tra: hiện tại `Configure` không set `Events`, chỉ set `TokenValidationParameters`.
- **Railway WebSocket support**: Railway hỗ trợ WebSocket — SignalR sẽ dùng WebSocket transport. Nếu không, SignalR tự fallback về Long Polling (vẫn hoạt động nhờ CORS `AllowCredentials`).

## Phases

- [ ] Phase 01 — Backend SignalR (M) — `phase-01-backend.md`
- [ ] Phase 02 — Frontend Hook (S) — `phase-02-frontend.md`

## Final Verification

```bash
# Backend build
cd backend && dotnet build src/OrderMgmt.WebApi

# Backend integration tests (toàn bộ suite)
cd backend && dotnet test tests/OrderMgmt.IntegrationTests --logger "console;verbosity=normal"

# Frontend typecheck
cd frontend && npm run typecheck

# Frontend tests
cd frontend && npm run test

# Frontend build
cd frontend && npm run build
```

**Manual smoke test:**
1. Mở 2 tab, cùng login vào app
2. Tab 1: chuyển trạng thái báo giá sang Confirmed
3. Tab 2: badge chuông cập nhật ngay (không cần đợi 60s)

## Rollback / Recovery

- Phase 01 (backend): revert commits, xóa `NotificationHub.cs` + `IRealtimeNotifier.cs` + `SignalRNotifier.cs`, restore `NotificationService.cs` và `Program.cs`
- Phase 02 (frontend): revert commits, xóa `useNotificationHub.ts`, restore `app-layout.tsx`
- Không có database migration — rollback hoàn toàn bằng `git revert`
