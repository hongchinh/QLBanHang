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
