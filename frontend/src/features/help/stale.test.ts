import { describe, expect, it } from 'vitest';
import { formatStaleReport, staleWarnings, type StaleInput } from './stale';
import type { HelpVideo } from './types';

const VIDEO: HelpVideo = {
  youtubeId: 'abc',
  title: 'Phiếu xuất kho',
  duration: 60,
  recordedAt: '2026-10-10',
  steps: { reason: { at: 0, hash: 'h-reason' }, save: { at: 20, hash: 'h-save' } },
};

const BASE: StaleInput = {
  key: 'stock-out.form',
  updated: '2026-10-15',
  helpFile: { at: 1000, day: '2026-10-15' },
  sources: { at: 900, day: '2026-10-14' },
  unmatchedSources: [],
  steps: [{ id: 'reason', hash: 'h-reason' }, { id: 'save', hash: 'h-save' }],
};

const kinds = (input: StaleInput) => staleWarnings(input).map((w) => w.kind);

describe('staleWarnings', () => {
  it('returns nothing when everything is up to date', () => {
    expect(staleWarnings(BASE)).toEqual([]);
    expect(staleWarnings({ ...BASE, video: { ...VIDEO, recordedAt: '2026-10-14' } })).toEqual([]);
  });

  it('sources-no-match: once per unmatched glob', () => {
    const w = staleWarnings({ ...BASE, unmatchedSources: ['a/**', 'b/*.ts'] });
    expect(w).toEqual([
      { key: 'stock-out.form', kind: 'sources-no-match', detail: 'Nguồn "a/**" không khớp file nào, sửa lại sources' },
      { key: 'stock-out.form', kind: 'sources-no-match', detail: 'Nguồn "b/*.ts" không khớp file nào, sửa lại sources' },
    ]);
  });

  it('sources-newer-than-help: fires when the sources commit is later', () => {
    const w = staleWarnings({ ...BASE, sources: { at: 1001, day: '2026-10-16' } });
    expect(w).toEqual([
      {
        key: 'stock-out.form',
        kind: 'sources-newer-than-help',
        detail: 'Code đổi ngày 2026-10-16, hướng dẫn sửa lần cuối ngày 2026-10-15',
      },
    ]);
  });

  it('sources-newer-than-help: does not fire on equal time or null stamps', () => {
    expect(kinds({ ...BASE, sources: { at: 1000, day: '2026-10-15' } })).toEqual([]);
    expect(kinds({ ...BASE, sources: null })).toEqual([]);
    expect(kinds({ ...BASE, helpFile: null, updated: '2026-10-15' })).toEqual([]);
  });

  it('sources-newer-than-help: orders by time, not by local day (later time zone)', () => {
    expect(kinds({ ...BASE, sources: { at: 1001, day: '2026-10-14' } })).toEqual(['sources-newer-than-help']);
  });

  it('updated-older-than-commit: fires when updated is before the help commit day', () => {
    const w = staleWarnings({ ...BASE, updated: '2026-10-14' });
    expect(w).toEqual([
      {
        key: 'stock-out.form',
        kind: 'updated-older-than-commit',
        detail: 'updated (2026-10-14) cũ hơn lần commit cuối (2026-10-15)',
      },
    ]);
  });

  it('updated-older-than-commit: does not fire on the same day or without a commit', () => {
    expect(kinds({ ...BASE, updated: '2026-10-15' })).toEqual([]);
    expect(kinds({ ...BASE, updated: '2020-01-01', helpFile: null })).toEqual([]);
  });

  it('sources-newer-than-video: fires when the code changed after recording', () => {
    const w = staleWarnings({ ...BASE, video: VIDEO });
    expect(w).toEqual([
      {
        key: 'stock-out.form',
        kind: 'sources-newer-than-video',
        detail: 'Code đổi ngày 2026-10-14 sau khi quay video ngày 2026-10-10',
      },
    ]);
  });

  it('sources-newer-than-video: does not fire on the same day, with no video or no sources', () => {
    expect(kinds({ ...BASE, video: { ...VIDEO, recordedAt: '2026-10-14' } })).toEqual([]);
    expect(kinds({ ...BASE, sources: null, video: VIDEO })).toEqual([]);
    expect(kinds({ ...BASE, video: undefined })).toEqual([]);
  });

  it('narration-changed: fires for a video step whose hash differs', () => {
    const w = staleWarnings({
      ...BASE,
      sources: null,
      video: VIDEO,
      steps: [{ id: 'reason', hash: 'h-reason' }, { id: 'save', hash: 'h-save-new' }],
    });
    expect(w).toEqual([{ key: 'stock-out.form', kind: 'narration-changed', detail: 'Bước save đã sửa câu sau khi quay' }]);
  });

  it('step-not-in-video: fires for a current step missing from the video', () => {
    const w = staleWarnings({
      ...BASE,
      sources: null,
      video: VIDEO,
      steps: [...BASE.steps, { id: 'print', hash: 'h-print' }],
    });
    expect(w).toEqual([{ key: 'stock-out.form', kind: 'step-not-in-video', detail: 'Bước print chưa có trong video' }]);
  });

  it('narration-changed and step-not-in-video do not fire without a video or when all steps match', () => {
    expect(kinds({ ...BASE, steps: [{ id: 'x', hash: 'y' }] })).toEqual([]);
    expect(kinds({ ...BASE, sources: null, video: VIDEO })).toEqual([]);
  });

  it('reports several kinds at once in the table order', () => {
    const w = staleWarnings({
      ...BASE,
      updated: '2026-10-01',
      sources: { at: 2000, day: '2026-10-16' },
      unmatchedSources: ['gone/**'],
      steps: [{ id: 'reason', hash: 'changed' }, { id: 'print', hash: 'h-print' }],
      video: VIDEO,
    });
    expect(w.map((x) => x.kind)).toEqual([
      'sources-no-match',
      'sources-newer-than-help',
      'updated-older-than-commit',
      'sources-newer-than-video',
      'narration-changed',
      'step-not-in-video',
    ]);
  });
});

describe('formatStaleReport', () => {
  it('says there are no warnings', () => {
    expect(formatStaleReport([])).toBe('Không có cảnh báo.');
  });

  it('lists the count and one line per warning in input order', () => {
    expect(
      formatStaleReport([
        { key: 'b', kind: 'step-not-in-video', detail: 'Bước x chưa có trong video' },
        { key: 'a', kind: 'sources-no-match', detail: 'Nguồn "z" không khớp file nào, sửa lại sources' },
      ]),
    ).toBe('2 cảnh báo:\n- b: Bước x chưa có trong video\n- a: Nguồn "z" không khớp file nào, sửa lại sources');
  });
});
