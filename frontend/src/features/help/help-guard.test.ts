import { createRoutesFromChildren } from 'react-router-dom';
import { describe, expect, it } from 'vitest';
import { appRoutes } from '@/app-routes';
import { HELP_PENDING } from './help-pending';
import { parseHelpDoc } from './parse-help';
import { flattenRoutePaths } from './route-paths';
import { helpVideos } from './videos';

const raw = import.meta.glob<string>('./content/*.md', { query: '?raw', import: 'default', eager: true });
const files = Object.entries(raw).map(([path, text]) => {
  const key = path.replace(/^.*\//, '').replace(/\.md$/, '');
  return { key, ...parseHelpDoc(text, key) };
});
const docs = files.flatMap((f) => (f.doc ? [f.doc] : []));
const byKey = new Map(docs.map((d) => [d.key, d]));
const appPaths = flattenRoutePaths(createRoutesFromChildren(appRoutes));
const owners = (route: string) => docs.filter((d) => d.routes.includes(route)).map((d) => d.key);

describe('help files', () => {
  it('are all valid', () => {
    expect(files.flatMap((f) => f.errors)).toEqual([]);
  });
  it('cover every app route once, or the route is pending', () => {
    const missing = appPaths.filter((r) => owners(r).length === 0 && !HELP_PENDING.includes(r));
    expect(missing, 'Thêm file hướng dẫn cho các route này, hoặc thêm chúng vào HELP_PENDING').toEqual([]);
    const shared = appPaths.filter((r) => owners(r).length > 1).map((r) => `${r}: ${owners(r).join(', ')}`);
    expect(shared, 'Một route chỉ được thuộc một file hướng dẫn').toEqual([]);
  });
  it('HELP_PENDING only shrinks', () => {
    const covered = HELP_PENDING.filter((r) => owners(r).length > 0);
    expect(covered, 'Xóa khỏi HELP_PENDING các route đã có hướng dẫn').toEqual([]);
    expect(HELP_PENDING.filter((r) => !appPaths.includes(r)), 'HELP_PENDING có route không tồn tại').toEqual([]);
    expect(HELP_PENDING.length, 'HELP_PENDING có route trùng').toBe(new Set(HELP_PENDING).size);
  });
  it('claim only app routes', () => {
    expect(docs.flatMap((d) => d.routes.filter((r) => !appPaths.includes(r)).map((r) => `${d.key}: ${r}`))).toEqual([]);
  });
  it('link only to other help files', () => {
    expect(docs.flatMap((d) => d.related.filter((k) => k === d.key || !byKey.has(k)).map((k) => `${d.key} -> ${k}`))).toEqual([]);
  });
});

describe('videos.json', () => {
  it('points at existing help files and steps', () => {
    const stale = Object.entries(helpVideos).flatMap(([key, video]) => {
      const doc = byKey.get(key);
      if (!doc) return [`${key}: không có file hướng dẫn`];
      return Object.keys(video.steps).filter((id) => !doc.steps.some((s) => s.id === id))
        .map((id) => `video cũ, cần quay lại: ${key} bước ${id}`);
    });
    expect(stale).toEqual([]);
  });
});
