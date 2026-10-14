import { describe, expect, it } from 'vitest'
import { DEFAULT_PUSH_TITLE, notificationTag, parsePushPayload } from './sw-push'

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
