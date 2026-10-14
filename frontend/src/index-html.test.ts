import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { describe, expect, it } from 'vitest'

const html = readFileSync(resolve(process.cwd(), 'index.html'), 'utf8')

describe('index.html', () => {
  // viewport-fit=cover makes env(safe-area-inset-*) non-zero on notched iPhones.
  it('lets the page reach the screen edges so safe-area insets apply', () => {
    expect(html).toContain('content="width=device-width, initial-scale=1.0, viewport-fit=cover"')
  })

  it.each([
    '<meta name="theme-color" content="#1e40af" />',
    '<meta name="mobile-web-app-capable" content="yes" />',
    '<meta name="apple-mobile-web-app-capable" content="yes" />',
    '<meta name="apple-mobile-web-app-title" content="QLĐơnHàng" />',
    // default, not black-translucent: content never sits under the status bar.
    '<meta name="apple-mobile-web-app-status-bar-style" content="default" />',
  ])('has %s', (tag) => {
    expect(html).toContain(tag)
  })

  // The favicon and apple-touch-icon are injected by vite.config.ts with the absolute API base; a relative
  // /api path is served by the frontend host in production.
  it('has no relative branding icon link', () => {
    expect(html).not.toContain('/api/settings/branding/icon')
  })
})
