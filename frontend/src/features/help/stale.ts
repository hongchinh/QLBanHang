import type { HelpVideo } from './types';

// Pure stale-help rules for `npm run help:stale` (scripts/help-stale.ts). The CLI imports this file
// with a `.ts` extension under Node type stripping, so it may only use erasable TypeScript syntax
// and `import type`.

export interface CommitStamp {
  at: number; // committer time, seconds since the epoch (git %ct): orders commits across time zones
  day: string; // committer's local day, YYYY-MM-DD (git %cs): compared with `updated` / `recordedAt`
}

export interface StaleInput {
  key: string;
  updated: string;
  helpFile: CommitStamp | null; // last commit of the doc file (null = not committed)
  sources: CommitStamp | null; // last commit touching any `sources` glob (null = none)
  unmatchedSources: string[]; // `sources` globs that match no tracked file
  steps: { id: string; hash: string }[];
  video?: HelpVideo;
}

export type StaleKind =
  | 'sources-newer-than-help'
  | 'sources-newer-than-video'
  | 'narration-changed'
  | 'step-not-in-video'
  | 'updated-older-than-commit'
  | 'sources-no-match';

export interface StaleWarning {
  key: string;
  kind: StaleKind;
  detail: string;
}

export function staleWarnings(input: StaleInput): StaleWarning[] {
  const { key, updated, helpFile, sources, unmatchedSources, steps, video } = input;
  const out: StaleWarning[] = [];
  const add = (kind: StaleKind, detail: string) => out.push({ key, kind, detail });

  for (const glob of unmatchedSources) {
    add('sources-no-match', `Nguồn "${glob}" không khớp file nào, sửa lại sources`);
  }
  if (sources && helpFile && sources.at > helpFile.at) {
    add('sources-newer-than-help', `Code đổi ngày ${sources.day}, hướng dẫn sửa lần cuối ngày ${helpFile.day}`);
  }
  if (helpFile && updated < helpFile.day) {
    add('updated-older-than-commit', `updated (${updated}) cũ hơn lần commit cuối (${helpFile.day})`);
  }
  if (video) {
    if (sources && sources.day > video.recordedAt) {
      add('sources-newer-than-video', `Code đổi ngày ${sources.day} sau khi quay video ngày ${video.recordedAt}`);
    }
    for (const step of steps) {
      const recorded = video.steps[step.id];
      if (recorded && recorded.hash !== step.hash) add('narration-changed', `Bước ${step.id} đã sửa câu sau khi quay`);
    }
    for (const step of steps) {
      if (!video.steps[step.id]) add('step-not-in-video', `Bước ${step.id} chưa có trong video`);
    }
  }
  return out;
}

export function formatStaleReport(warnings: readonly StaleWarning[]): string {
  if (warnings.length === 0) return 'Không có cảnh báo.';
  return [`${warnings.length} cảnh báo:`, ...warnings.map((w) => `- ${w.key}: ${w.detail}`)].join('\n');
}
