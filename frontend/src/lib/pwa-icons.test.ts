import { describe, expect, it } from 'vitest'
import { brandingIconUrl, iconLinkTags } from './pwa-icons'

describe('brandingIconUrl', () => {
  it('uses the dev proxy path without an API base', () => {
    expect(brandingIconUrl('', 192)).toBe('/api/settings/branding/icon/192')
  })

  // In production the API is on another origin; a relative /api path would hit the frontend host.
  it('uses the absolute API base in production', () => {
    expect(brandingIconUrl('https://api.example.com/api', 180))
      .toBe('https://api.example.com/api/settings/branding/icon/180')
  })
})

describe('iconLinkTags', () => {
  it('adds the favicon and the 180px apple-touch-icon to the head', () => {
    expect(iconLinkTags('https://api.example.com/api')).toEqual([
      {
        tag: 'link',
        attrs: { rel: 'icon', type: 'image/png', href: 'https://api.example.com/api/settings/branding/icon/192' },
        injectTo: 'head',
      },
      {
        tag: 'link',
        attrs: { rel: 'apple-touch-icon', sizes: '180x180', href: 'https://api.example.com/api/settings/branding/icon/180' },
        injectTo: 'head',
      },
    ])
  })
})
