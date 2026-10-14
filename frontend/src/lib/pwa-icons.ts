// Imported by vite.config.ts (type-checked by tsconfig.node.json, no DOM): keep it pure and free of '@/' imports.

export interface IconLinkTag { tag: 'link'; attrs: Record<string, string>; injectTo: 'head' }

export function brandingIconUrl(apiBase: string, size: number): string {
  return apiBase ? `${apiBase}/settings/branding/icon/${size}` : `/api/settings/branding/icon/${size}`
}

export function iconLinkTags(apiBase: string): IconLinkTag[] {
  return [
    { tag: 'link', attrs: { rel: 'icon', type: 'image/png', href: brandingIconUrl(apiBase, 192) }, injectTo: 'head' },
    {
      tag: 'link',
      attrs: { rel: 'apple-touch-icon', sizes: '180x180', href: brandingIconUrl(apiBase, 180) },
      injectTo: 'head',
    },
  ]
}
