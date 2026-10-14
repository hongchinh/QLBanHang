import type { PushState } from '@/hooks/usePushNotification';

// Whether PushPermissionPrompt has anything to show in this state. App asks the same question before it hands
// the shell a banner (an element that renders nothing would still get a slot), so the rule lives here once.
export function isPushPromptVisible(state: PushState): boolean {
  return state !== 'granted' && state !== 'denied' && state !== 'unsupported';
}
