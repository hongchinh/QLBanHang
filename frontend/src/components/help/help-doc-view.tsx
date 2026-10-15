import { useState } from 'react';
import { AppIcon } from '@/components/icons';
import { formatDuration, youtubeThumb, youtubeUrl } from '@/features/help/videos';
import type { HelpDoc, HelpVideo } from '@/features/help/types';
import { HelpInline } from './help-inline';

const HEADING = 'text-xs font-semibold uppercase tracking-wide text-muted-foreground';

interface HelpDocViewProps {
  doc: HelpDoc;
  video?: HelpVideo;
  related: HelpDoc[];
  onOpenRelated(key: string): void;
}

function VideoBlock({ video }: { video: HelpVideo }) {
  const [thumbFailed, setThumbFailed] = useState(false);
  if (typeof navigator !== 'undefined' && navigator.onLine === false) {
    return <p className="text-sm text-muted-foreground">Cần kết nối mạng để xem video</p>;
  }
  return (
    <div className="space-y-2">
      <a
        href={youtubeUrl(video.youtubeId)}
        target="_blank"
        rel="noopener noreferrer"
        className="relative block overflow-hidden rounded-md border bg-muted"
      >
        {thumbFailed ? (
          <span className="flex aspect-video flex-col items-center justify-center gap-1 p-3 text-center text-sm">
            <AppIcon name="video" size={24} />
            <span className="font-medium">{video.title}</span>
            <span className="text-xs text-muted-foreground">{formatDuration(video.duration)}</span>
          </span>
        ) : (
          <>
            <img
              src={youtubeThumb(video.youtubeId)}
              alt={video.title}
              className="aspect-video w-full object-cover"
              onError={() => setThumbFailed(true)}
            />
            <span className="absolute bottom-1 right-1 rounded bg-black/70 px-1.5 py-0.5 text-xs text-white">
              {formatDuration(video.duration)}
            </span>
          </>
        )}
      </a>
      <a
        href={youtubeUrl(video.youtubeId)}
        target="_blank"
        rel="noopener noreferrer"
        className="inline-flex items-center gap-1 text-sm text-primary hover:underline"
      >
        <AppIcon name="externalLink" size={14} />
        Xem trên YouTube
      </a>
    </div>
  );
}

export function HelpDocView({ doc, video, related, onOpenRelated }: HelpDocViewProps) {
  return (
    <div className="space-y-5 text-sm">
      {video && <VideoBlock key={video.youtubeId} video={video} />}

      <section className="space-y-1.5">
        <h3 className={HEADING}>Màn hình này dùng để</h3>
        <p>{doc.purpose}</p>
      </section>

      <section className="space-y-1.5">
        <h3 className={HEADING}>Các bước chính</h3>
        <ol className="space-y-2">
          {doc.steps.map((step, i) => {
            const at = video?.steps[step.id]?.at;
            return (
              <li key={step.id} className="flex items-start gap-2">
                <span className="mt-0.5 flex h-5 w-5 shrink-0 items-center justify-center rounded-full bg-primary/10 text-xs font-semibold text-primary">
                  {i + 1}
                </span>
                <span className="min-w-0 flex-1">
                  <HelpInline text={step.text} />
                </span>
                {video && at !== undefined && (
                  <a
                    href={youtubeUrl(video.youtubeId, at)}
                    target="_blank"
                    rel="noopener noreferrer"
                    aria-label={`Xem bước ${i + 1} trong video (${formatDuration(at)})`}
                    className="inline-flex shrink-0 items-center gap-1 rounded-full border px-2 py-0.5 text-xs text-primary hover:bg-accent"
                  >
                    <AppIcon name="video" size={12} />
                    {formatDuration(at)}
                  </a>
                )}
              </li>
            );
          })}
        </ol>
      </section>

      {doc.notes.length > 0 && (
        <section className="space-y-1.5 rounded-md border border-[hsl(var(--icon-warn)/0.4)] bg-[hsl(var(--icon-warn)/0.1)] p-3">
          <h3 className={HEADING}>
            <AppIcon name="warning" size={14} className="mr-1 inline align-text-bottom" />
            Lưu ý
          </h3>
          <ul className="list-disc space-y-1 pl-5">
            {doc.notes.map((note) => (
              <li key={note}>
                <HelpInline text={note} />
              </li>
            ))}
          </ul>
        </section>
      )}

      {doc.shortcuts.length > 0 && (
        <section className="hidden space-y-1.5 md:block">
          <h3 className={HEADING}>Phím tắt</h3>
          <ul className="space-y-1">
            {doc.shortcuts.map((s) => (
              <li key={s.keys} className="flex items-center justify-between gap-2">
                <span>{s.label}</span>
                <kbd className="rounded border bg-muted px-1 py-0.5 font-mono text-[11px]">{s.keys}</kbd>
              </li>
            ))}
          </ul>
        </section>
      )}

      {related.length > 0 && (
        <section className="space-y-1.5">
          <h3 className={HEADING}>Xem thêm</h3>
          <ul className="space-y-1">
            {related.map((r) => (
              <li key={r.key}>
                <button
                  type="button"
                  onClick={() => onOpenRelated(r.key)}
                  className="text-primary hover:underline"
                >
                  {r.title}
                </button>
              </li>
            ))}
          </ul>
        </section>
      )}
    </div>
  );
}
