import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import { authApi, type LoginRequest } from './api';
import { useAuthStore } from '@/stores/auth-store';
import { useBranchStore } from '@/stores/branch-store';

export function useLogin() {
  const setAuth = useAuthStore((s) => s.setAuth);
  return useMutation({
    mutationFn: (req: LoginRequest) => authApi.login(req),
    onSuccess: (data) => {
      setAuth(data.accessToken, data.expiresAt, data.user);
    },
  });
}

export function useLogout() {
  const localLogout = useAuthStore((s) => s.logout);
  const qc = useQueryClient();
  const navigate = useNavigate();

  return useMutation({
    // Best-effort server-side revocation. Whether it succeeds or not, we still
    // tear down the local session so the UI stays consistent.
    mutationFn: () => authApi.logout().catch(() => undefined),
    onSettled: () => {
      localLogout();
      useBranchStore.getState().clear();
      qc.clear();
      if ('caches' in window) void caches.delete('api-cache');
      navigate('/login', { replace: true });
    },
  });
}
