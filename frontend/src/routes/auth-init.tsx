import { useEffect, useState } from 'react';
import { authApi } from '@/features/auth/api';
import { useAuthStore } from '@/stores/auth-store';

interface Props {
  children: React.ReactNode;
}

// Single-flight at module scope so React StrictMode's double-invoke of useEffect
// in dev fires only one /auth/refresh. Two concurrent refreshes with the same
// rotating cookie trigger backend reuse-detection, which revokes the whole token
// family and logs the user out on F5.
let bootInFlight: Promise<void> | null = null;

function bootSession(): Promise<void> {
  if (bootInFlight) return bootInFlight;
  bootInFlight = (async () => {
    try {
      const pair = await authApi.refresh();
      // Push the access token into the store BEFORE /auth/me — the axios request
      // interceptor reads it from the store to attach the Authorization header.
      useAuthStore.getState().setToken(pair.accessToken, pair.accessTokenExpiresAt);
      const user = await authApi.me();
      useAuthStore.getState().setUser(user);
    } catch (error) {
      if (import.meta.env.DEV) {
        console.warn('Auth boot refresh failed; no valid refresh cookie/session was available.', error);
      }
      // Best-effort: clear any partial state so ProtectedRoute kicks to /login.
      useAuthStore.getState().logout();
    }
  })().finally(() => {
    bootInFlight = null;
  });
  return bootInFlight;
}

export function AuthInit({ children }: Props) {
  const [ready, setReady] = useState(false);

  useEffect(() => {
    let cancelled = false;
    bootSession().finally(() => {
      if (!cancelled) setReady(true);
    });
    return () => {
      cancelled = true;
    };
  }, []);

  if (!ready) {
    return (
      <div className="flex flex-1 items-center justify-center text-sm text-muted-foreground">
        Đang tải...
      </div>
    );
  }
  return <>{children}</>;
}
