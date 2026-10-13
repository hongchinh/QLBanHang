# PWA — Progressive Web App

## Goal

Biến QLBanHang thành Progressive Web App theo 4 phases: (1) installable với static precache, (2) NetworkFirst API caching, (3) push notification backend (entity + service + controller + integration vào QuotationService), (4) push notification frontend (hook + soft-prompt UX). Push notification là delivery channel của hệ thống Notification hiện có — không tạo parallel system.

## Scope

**In scope:**
- Installable: manifest + Service Worker precache + `InstallPrompt` banner
- Update prompt: `registerType: 'prompt'` + toast khi có SW version mới
- API caching: NetworkFirst cho GET `/api/**`, timeout 8s, max-age 24h
- `PushSubscription` entity (Domain) + `IPushSender` interface (Application) + `PushSenderService` (Infrastructure)
- Mở rộng `INotificationService.SendAsync` — persist in-app notification + dispatch push
- `POST /api/push/subscribe` (upsert by endpoint) và `DELETE /api/push/subscribe` (xóa theo endpoint)
- Gửi notification khi `Confirmed`, `AccountingConfirmed`, `Cancelled` — tới owner + Admin users
- `usePushNotification` hook với full state machine + soft-prompt UX

**Out of scope:**
- iOS Safari push (Phase sau)
- Background Sync / offline mutations
- Native app (React Native / Capacitor)

## Assumptions

- VAPID keys không được commit vào repo — dev dùng `user-secrets`, production dùng env var
- `WebPush` NuGet package là `WebPush` by Peter Beverloo (`WebPush` on nuget.org)
- Admin role name trong DB là `"Admin"` (đã seed)
- Placeholder icons PNG sẽ được tạo bằng script Node.js nhỏ; icons thực cần thay sau
- `vite.config.ts` hiện dùng `import { defineConfig } from 'vitest/config'` — cần split thành `vitest.config.ts` riêng để VitePWA hoạt động
- Integration tests cần Vapid config trong `WebAppFactory` — dùng giá trị rỗng; `PushSenderService` skip khi `PrivateKey` rỗng

## Risks

- **vitest/config split**: Nếu `vitest.config.ts` thiếu `plugins: [react()]`, test global imports (jsdom) sẽ fail — cần verify `npm run test` pass sau split trước khi tiếp tục
- **QuotationService constructor change**: Thêm `INotificationService` vào constructor không phá DI, nhưng cần rebuild integration tests
- **EF migration conflict**: Nếu branch khác cũng đang có migration pending, sẽ conflict — chạy migration sau khi merge hoặc rebase

## Phases

- [ ] Phase 01 — Installable + Static Precache (L) — `phase-01-installable.md`
- [ ] Phase 02 — API Runtime Cache (S) — `phase-02-api-cache.md`
- [ ] Phase 03 — Push Notification Backend (L) — `phase-03-push-backend.md`
- [ ] Phase 04 — Push Notification Frontend (M) — `phase-04-push-frontend.md`

## Final Verification

```bash
# Backend integration tests
cd backend && dotnet test tests/OrderMgmt.IntegrationTests \
  --filter "PushSubscription" \
  --logger "console;verbosity=normal"

# Frontend typecheck
cd frontend && npm run typecheck

# Frontend unit tests
cd frontend && npm run test

# Frontend build (verifies SW compilation)
cd frontend && npm run build

# Manual PWA smoke test (Chrome DevTools):
# 1. npm run preview → open http://localhost:4173
# 2. DevTools → Application → Manifest → verify installable
# 3. DevTools → Application → Service Workers → verify active
# 4. Lighthouse → PWA audit → score >= 90
```

## Rollback / Recovery

- Phase 01–02: frontend-only changes, không ảnh hưởng backend. Revert bằng `git revert`.
- Phase 03: có EF migration. Rollback: `dotnet ef migrations remove` rồi `git revert`.
- Phase 04: frontend-only hook + UI. Revert bằng `git revert`.
- Không có breaking changes đối với existing API endpoints.
