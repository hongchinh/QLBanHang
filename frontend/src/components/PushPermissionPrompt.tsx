import type { PushState } from '@/hooks/usePushNotification'
import { isPushPromptVisible } from './push-prompt-visibility'

interface Props {
  state: PushState
  onEnable(): void
  onDismiss(): void
}

export function PushPermissionPrompt({ state, onEnable, onDismiss }: Props) {
  if (!isPushPromptVisible(state)) return null

  const isError = state === 'error'
  const isLoading = state === 'loading'

  return (
    <div className="flex items-center justify-between gap-4 rounded-lg border border-blue-200 bg-blue-50 px-4 py-2">
      <div className="text-sm text-blue-900">
        {isError
          ? 'Bật thông báo thất bại. Vui lòng thử lại.'
          : 'Bật thông báo để nhận cập nhật khi trạng thái báo giá thay đổi.'}
      </div>
      <div className="flex shrink-0 gap-2">
        <button
          onClick={onEnable}
          disabled={isLoading}
          className="rounded bg-blue-700 px-3 py-1 text-sm font-semibold text-white hover:bg-blue-800 disabled:opacity-60"
        >
          {isLoading ? 'Đang xử lý...' : isError ? 'Thử lại' : 'Bật thông báo'}
        </button>
        {!isLoading && (
          <button
            onClick={onDismiss}
            className="rounded px-3 py-1 text-sm text-blue-600 hover:text-blue-800"
          >
            Để sau
          </button>
        )}
      </div>
    </div>
  )
}
