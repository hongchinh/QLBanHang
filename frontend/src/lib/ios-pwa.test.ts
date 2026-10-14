import { describe, expect, it } from 'vitest'
import { isIosDevice, needsIosInstall } from './ios-pwa'

const IPHONE = 'Mozilla/5.0 (iPhone; CPU iPhone OS 17_4 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.4 Mobile/15E148 Safari/604.1'
const IPAD_DESKTOP_UA = 'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.4 Safari/605.1.15'
const ANDROID = 'Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0 Mobile Safari/537.36'

describe('isIosDevice', () => {
  it.each([
    [IPHONE, 5, true],
    [IPAD_DESKTOP_UA, 5, true], // iPadOS asks for the desktop site
    [IPAD_DESKTOP_UA, 0, false], // a real Mac
    [ANDROID, 5, false],
  ])('%s with %i touch points → %s', (userAgent, maxTouchPoints, expected) => {
    expect(isIosDevice({ userAgent, maxTouchPoints })).toBe(expected)
  })
})

describe('needsIosInstall', () => {
  it('is true in Safari on an iPhone', () => {
    expect(needsIosInstall({ userAgent: IPHONE, maxTouchPoints: 5, standalone: false, displayModeStandalone: false })).toBe(true)
  })

  it.each([
    { standalone: true, displayModeStandalone: false },
    { standalone: undefined, displayModeStandalone: true },
  ])('is false once installed (%o)', (flags) => {
    expect(needsIosInstall({ userAgent: IPHONE, maxTouchPoints: 5, ...flags })).toBe(false)
  })

  it('is false off iOS', () => {
    expect(needsIosInstall({ userAgent: ANDROID, maxTouchPoints: 5, displayModeStandalone: false })).toBe(false)
  })
})
