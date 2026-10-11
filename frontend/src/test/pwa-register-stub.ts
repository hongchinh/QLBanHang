// Stand-in for the build-time `virtual:pwa-register/react` module (vitest alias).
export function useRegisterSW() {
  return {
    needRefresh: [false, () => undefined] as const,
    offlineReady: [false, () => undefined] as const,
    updateServiceWorker: () => Promise.resolve(),
  };
}
