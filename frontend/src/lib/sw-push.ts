// Imported by sw.ts; kept free of DOM/service-worker globals so it is testable.

export const DEFAULT_PUSH_TITLE = 'QL Đơn Hàng'

export interface PushPayload { title: string; body: string; url: string; tag?: string }

const str = (value: unknown): string | undefined =>
  typeof value === 'string' && value !== '' ? value : undefined

export function parsePushPayload(text: string | null | undefined): PushPayload {
  const fallback: PushPayload = { title: DEFAULT_PUSH_TITLE, body: text ?? '', url: '/' }
  if (!text) return fallback
  let data: unknown
  try {
    data = JSON.parse(text)
  } catch {
    return fallback
  }
  if (typeof data !== 'object' || data === null || Array.isArray(data)) return fallback
  const record = data as Record<string, unknown>
  const payload: PushPayload = {
    title: str(record.title) ?? DEFAULT_PUSH_TITLE,
    body: typeof record.body === 'string' ? record.body : '',
    url: str(record.url) ?? '/',
  }
  const tag = str(record.tag)
  if (tag) payload.tag = tag
  return payload
}

export function notificationTag(payload: PushPayload): string {
  return payload.tag ?? payload.url
}

export function resolveTargetUrl(url: string | undefined, origin: string): string {
  if (!url) return '/'
  try {
    const target = new URL(url, origin)
    if (target.origin !== origin) return '/'
    if (target.protocol !== 'https:' && target.protocol !== 'http:') return '/'
    return target.pathname + target.search + target.hash
  } catch {
    return '/'
  }
}
