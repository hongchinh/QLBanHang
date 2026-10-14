import { render, screen, fireEvent } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { PushPermissionPrompt } from './PushPermissionPrompt'
import { isPushPromptVisible } from './push-prompt-visibility'
import type { PushState } from '@/hooks/usePushNotification'

describe('PushPermissionPrompt', () => {
  it('không render khi state bukan idle', () => {
    const { container } = render(
      <PushPermissionPrompt state="granted" onEnable={vi.fn()} onDismiss={vi.fn()} />
    )
    expect(container.firstChild).toBeNull()
  })

  it('render banner khi state là idle', () => {
    render(<PushPermissionPrompt state="idle" onEnable={vi.fn()} onDismiss={vi.fn()} />)
    expect(screen.getByRole('button', { name: /bật thông báo/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /để sau/i })).toBeInTheDocument()
  })

  it('render loading state khi state là loading', () => {
    render(<PushPermissionPrompt state="loading" onEnable={vi.fn()} onDismiss={vi.fn()} />)
    expect(screen.getByRole('button', { name: /đang xử lý/i })).toBeDisabled()
  })

  it('render error state với retry button', () => {
    render(<PushPermissionPrompt state="error" onEnable={vi.fn()} onDismiss={vi.fn()} />)
    expect(screen.getByRole('button', { name: /thử lại/i })).toBeInTheDocument()
  })

  it('không render khi state là denied', () => {
    const { container } = render(
      <PushPermissionPrompt state="denied" onEnable={vi.fn()} onDismiss={vi.fn()} />
    )
    expect(container.firstChild).toBeNull()
  })

  it('gọi onEnable khi click Bật thông báo', () => {
    const onEnable = vi.fn()
    render(<PushPermissionPrompt state="idle" onEnable={onEnable} onDismiss={vi.fn()} />)
    fireEvent.click(screen.getByRole('button', { name: /bật thông báo/i }))
    expect(onEnable).toHaveBeenCalledOnce()
  })

  it('gọi onDismiss khi click Để sau', () => {
    const onDismiss = vi.fn()
    render(<PushPermissionPrompt state="idle" onEnable={vi.fn()} onDismiss={onDismiss} />)
    fireEvent.click(screen.getByRole('button', { name: /để sau/i }))
    expect(onDismiss).toHaveBeenCalledOnce()
  })

  // App asks isPushPromptVisible whether the shell gets a banner slot. The prompt has to agree in every state,
  // or the slot stays empty (or the prompt goes missing).
  it.each<PushState>(['idle', 'loading', 'error', 'granted', 'denied', 'unsupported'])(
    'renders something exactly when isPushPromptVisible says so (%s)',
    (state) => {
      const { container } = render(<PushPermissionPrompt state={state} onEnable={vi.fn()} onDismiss={vi.fn()} />)
      expect(container.firstChild !== null).toBe(isPushPromptVisible(state))
    },
  )
})
