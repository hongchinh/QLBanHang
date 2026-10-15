import { describe, expect, it } from 'vitest';
import { formatDuration, videoFor, youtubeThumb, youtubeUrl } from './videos';
import type { HelpVideo } from './types';

const video: HelpVideo = {
  youtubeId: 'abc',
  title: 'Phiếu xuất kho',
  duration: 155,
  recordedAt: '2026-10-15',
  steps: {},
};

describe('videoFor', () => {
  it('finds a video by key', () => {
    expect(videoFor('k', { k: video })).toBe(video);
  });

  it('returns undefined for a missing key', () => {
    expect(videoFor('missing', { k: video })).toBeUndefined();
  });

  it('has no videos in round 1', () => {
    expect(videoFor('stock-out.form')).toBeUndefined();
  });
});

describe('youtube helpers', () => {
  it('builds a watch url, with an optional start offset', () => {
    expect(youtubeUrl('abc')).toBe('https://www.youtube.com/watch?v=abc');
    expect(youtubeUrl('abc', 21)).toBe('https://www.youtube.com/watch?v=abc&t=21s');
    expect(youtubeUrl('abc', 0)).toBe('https://www.youtube.com/watch?v=abc&t=0s');
  });

  it('builds a thumbnail url', () => {
    expect(youtubeThumb('abc')).toBe('https://i.ytimg.com/vi/abc/mqdefault.jpg');
  });
});

describe('formatDuration', () => {
  it('formats seconds as m:ss', () => {
    expect(formatDuration(155)).toBe('2:35');
    expect(formatDuration(8)).toBe('0:08');
    expect(formatDuration(600)).toBe('10:00');
  });
});
