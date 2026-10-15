import videosJson from './videos.json';
import type { HelpVideo } from './types';

export const helpVideos = videosJson as Readonly<Record<string, HelpVideo>>;

export function videoFor(
  key: string,
  videos: Readonly<Record<string, HelpVideo>> = helpVideos,
): HelpVideo | undefined {
  return videos[key];
}

export function youtubeUrl(youtubeId: string, atSeconds?: number): string {
  const base = `https://www.youtube.com/watch?v=${youtubeId}`;
  return atSeconds === undefined ? base : `${base}&t=${atSeconds}s`;
}

export function youtubeThumb(youtubeId: string): string {
  return `https://i.ytimg.com/vi/${youtubeId}/mqdefault.jpg`;
}

export function formatDuration(seconds: number): string {
  const m = Math.floor(seconds / 60);
  const s = Math.floor(seconds % 60);
  return `${m}:${String(s).padStart(2, '0')}`;
}
