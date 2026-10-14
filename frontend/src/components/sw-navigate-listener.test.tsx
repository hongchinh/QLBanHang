import { act, render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { NAVIGATE_MESSAGE } from '@/lib/sw-push'
import { SwNavigateListener } from './sw-navigate-listener'

function Where() {
  const { pathname, search } = useLocation()
  return <div data-testid="where">{pathname + search}</div>
}

function renderAt(path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <SwNavigateListener />
      <Routes>
        <Route path="*" element={<Where />} />
      </Routes>
    </MemoryRouter>
  )
}

describe('SwNavigateListener', () => {
  let container: EventTarget
  const original = Object.getOwnPropertyDescriptor(navigator, 'serviceWorker')

  beforeEach(() => {
    container = new EventTarget()
    Object.defineProperty(navigator, 'serviceWorker', { value: container, configurable: true })
  })
  afterEach(() => {
    if (original) Object.defineProperty(navigator, 'serviceWorker', original)
    else delete (navigator as { serviceWorker?: unknown }).serviceWorker
  })

  const post = (data: unknown) =>
    act(() => { container.dispatchEvent(new MessageEvent('message', { data })) })

  it('navigates on a NAVIGATE message from the service worker', () => {
    renderAt('/')
    post({ type: NAVIGATE_MESSAGE, url: '/quotations/7?tab=history' })
    expect(screen.getByTestId('where')).toHaveTextContent('/quotations/7?tab=history')
  })

  it.each([
    { type: 'SKIP_WAITING' },
    { type: NAVIGATE_MESSAGE, url: 'https://evil.example.com' },
    { type: NAVIGATE_MESSAGE, url: 42 },
    null,
  ])('ignores %o', (data) => {
    renderAt('/customers')
    post(data)
    expect(screen.getByTestId('where')).toHaveTextContent('/customers')
  })

  it('stops listening when unmounted', () => {
    const remove = vi.spyOn(container, 'removeEventListener')
    const { unmount } = renderAt('/')
    unmount()
    expect(remove).toHaveBeenCalledWith('message', expect.any(Function))
  })

  it('renders nothing and does not throw without service worker support', () => {
    delete (navigator as { serviceWorker?: unknown }).serviceWorker
    const { container: root } = render(<MemoryRouter><SwNavigateListener /></MemoryRouter>)
    expect(root).toBeEmptyDOMElement()
  })
})
