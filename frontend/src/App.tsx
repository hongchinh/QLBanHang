import { useState, type ReactNode } from 'react';
import { AppIcon } from '@/components/icons';
import { BrowserRouter, Routes, useLocation } from 'react-router-dom';
import { QueryClientProvider } from '@tanstack/react-query';
import { ReactQueryDevtools } from '@tanstack/react-query-devtools';
import { queryClient } from '@/lib/query-client';
import { appRoutes } from '@/app-routes';
import { AuthInit } from '@/routes/auth-init';
import { SwNavigateListener } from '@/components/sw-navigate-listener';
import { ErrorBoundary } from '@/components/error-boundary';
import { Toaster } from '@/components/ui/toaster';
import { useRegisterSW } from 'virtual:pwa-register/react';
import { useBeforeInstallPrompt } from '@/hooks/useBeforeInstallPrompt';
import { InstallPrompt } from '@/components/InstallPrompt';
import { usePushNotification } from '@/hooks/usePushNotification';
import { PushPermissionPrompt } from '@/components/PushPermissionPrompt';
import { isPushPromptVisible } from '@/components/push-prompt-visibility';
import { AppShell } from '@/components/layout/app-shell';

// Sits inside the shell's content box, so its fallback fills that box: the viewport height is the shell's.
function RouteErrorBoundary({ children }: { children: ReactNode }) {
  const { pathname } = useLocation();
  return (
    <ErrorBoundary key={pathname} fill>
      {children}
    </ErrorBoundary>
  );
}

export function App() {
  const { canShow, install, dismiss } = useBeforeInstallPrompt();
  const {
    needRefresh: [needRefresh, setNeedRefresh],
    updateServiceWorker,
  } = useRegisterSW();

  const PUSH_DISMISS_KEY = 'push_prompt_dismissed_until';
  const vapidKey = import.meta.env.VITE_VAPID_PUBLIC_KEY;
  const { state: pushState, subscribe: subscribePush } = usePushNotification(vapidKey);

  const [pushDismissed, setPushDismissed] = useState(() => {
    const until = localStorage.getItem(PUSH_DISMISS_KEY);
    return until ? Date.now() < Number(until) : false;
  });

  const handlePushDismiss = () => {
    const until = Date.now() + 30 * 24 * 60 * 60 * 1000;
    localStorage.setItem(PUSH_DISMISS_KEY, String(until));
    setPushDismissed(true);
  };

  return (
    <ErrorBoundary>
      <InstallPrompt canShow={canShow} onInstall={install} onDismiss={dismiss} />
      <QueryClientProvider client={queryClient}>
        <AppShell
          banner={
            // An element is truthy even when the prompt would render nothing, and the shell would keep an empty
            // slot for it: ask the prompt's own rule first.
            pushDismissed || !isPushPromptVisible(pushState) ? null : (
              <PushPermissionPrompt state={pushState} onEnable={subscribePush} onDismiss={handlePushDismiss} />
            )
          }
        >
        <BrowserRouter>
          <SwNavigateListener />
          <AuthInit>
            <RouteErrorBoundary>
            <Routes>
              {appRoutes}
            </Routes>
            </RouteErrorBoundary>
          </AuthInit>
        </BrowserRouter>
        </AppShell>
        <Toaster />
        <ReactQueryDevtools initialIsOpen={false} />
      </QueryClientProvider>
      {needRefresh && (
        <div className="fixed bottom-[max(1rem,env(safe-area-inset-bottom))] right-4 z-50 flex items-center gap-3 rounded-lg bg-gray-900 px-4 py-3 text-sm text-white shadow-xl">
          <span>Có phiên bản mới</span>
          <button
            onClick={() => updateServiceWorker(true)}
            className="rounded bg-blue-500 px-3 py-1 text-xs font-semibold hover:bg-blue-400"
          >
            Cập nhật ngay
          </button>
          <button onClick={() => setNeedRefresh(false)} aria-label="Đóng" className="text-gray-400 hover:text-white"><AppIcon name="close" /></button>
        </div>
      )}
    </ErrorBoundary>
  );
}
