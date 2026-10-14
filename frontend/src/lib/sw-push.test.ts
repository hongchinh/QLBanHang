import { describe, expect, it, vi } from 'vitest'
import { DEFAULT_PUSH_TITLE, focusOrOpenWindow, NAVIGATE_MESSAGE, notificationTag, parsePushPayload, resolveTargetUrl } from './sw-push'

describe('parsePushPayload', () => {
  it('reads title, body and url from the backend payload', () => {
    expect(parsePushPayload('{"title":"Báo giá BG-001","body":"Đã xác nhận","url":"/quotations/1"}'))
      .toEqual({ title: 'Báo giá BG-001', body: 'Đã xác nhận', url: '/quotations/1' })
  })

  it('keeps a non-empty tag', () => {
    expect(parsePushPayload('{"title":"t","body":"b","url":"/x","tag":"fund:1"}').tag).toBe('fund:1')
  })

  it.each([
    ['{"title":"t","body":"b","url":"/x","tag":""}'],
    ['{"title":"t","body":"b","url":"/x","tag":5}'],
  ])('drops an empty or non-string tag (%s)', (text) => {
    expect(parsePushPayload(text)).not.toHaveProperty('tag')
  })

  it('fills missing or non-string fields with defaults', () => {
    expect(parsePushPayload('{"body":3}')).toEqual({ title: DEFAULT_PUSH_TITLE, body: '', url: '/' })
    expect(parsePushPayload('{"title":"","url":""}')).toEqual({ title: DEFAULT_PUSH_TITLE, body: '', url: '/' })
  })

  it.each(['not json', '42', '[1,2]', 'null'])('treats %s as a plain-text body', (text) => {
    expect(parsePushPayload(text)).toEqual({ title: DEFAULT_PUSH_TITLE, body: text, url: '/' })
  })

  it.each([null, undefined, ''])('handles an empty push (%s)', (text) => {
    expect(parsePushPayload(text)).toEqual({ title: DEFAULT_PUSH_TITLE, body: '', url: '/' })
  })
})

describe('notificationTag', () => {
  it('uses the tag when there is one', () => {
    expect(notificationTag({ title: 't', body: 'b', url: '/x', tag: 'fund:1' })).toBe('fund:1')
  })

  it('falls back to the url', () => {
    expect(notificationTag({ title: 't', body: 'b', url: '/quotations/1' })).toBe('/quotations/1')
  })
})

describe('resolveTargetUrl', () => {
  const origin = 'https://app.example.com'

  it.each([
    ['/quotations/1', '/quotations/1'],
    ['/reports/cash-book?fundId=2#top', '/reports/cash-book?fundId=2#top'],
    ['quotations/1', '/quotations/1'],
    ['https://app.example.com/customers?q=a', '/customers?q=a'],
  ])('keeps same-origin %s as %s', (url, expected) => {
    expect(resolveTargetUrl(url, origin)).toBe(expected)
  })

  it.each([
    'https://evil.example.com/phish',
    '//evil.example.com/phish',
    'javascript:alert(1)',
    'data:text/html,hi',
    'http://[bad',
    '',
    undefined,
  ])('falls back to / for %s', (url) => {
    expect(resolveTargetUrl(url, origin)).toBe('/')
  })
})

describe('focusOrOpenWindow', () => {
  function fakeClients(windowCount: number) {
    const windows = Array.from({ length: windowCount }, () => ({
      focus: vi.fn().mockResolvedValue(undefined),
      postMessage: vi.fn(),
    }))
    return {
      windows,
      clients: {
        matchAll: vi.fn().mockResolvedValue(windows),
        openWindow: vi.fn().mockResolvedValue(null),
      },
    }
  }

  it('focuses the first open window and asks it to navigate', async () => {
    const { windows, clients } = fakeClients(2)
    await focusOrOpenWindow(clients, '/quotations/1')
    expect(clients.matchAll).toHaveBeenCalledWith({ type: 'window', includeUncontrolled: true })
    expect(windows[0].focus).toHaveBeenCalledOnce()
    expect(windows[0].postMessage).toHaveBeenCalledWith({ type: NAVIGATE_MESSAGE, url: '/quotations/1' })
    expect(windows[1].focus).not.toHaveBeenCalled()
    expect(clients.openWindow).not.toHaveBeenCalled()
  })

  it('opens a new window when the app is not open', async () => {
    const { clients } = fakeClients(0)
    await focusOrOpenWindow(clients, '/quotations/1')
    expect(clients.openWindow).toHaveBeenCalledWith('/quotations/1')
  })
})
