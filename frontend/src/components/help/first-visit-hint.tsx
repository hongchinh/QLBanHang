import { useId, type RefObject } from 'react';
import { AppIcon } from '@/components/icons';
import { Button } from '@/components/ui/button';
import { Popover, PopoverAnchor, PopoverContent } from '@/components/ui/popover';
import type { useFirstVisitHint } from '@/features/help/use-first-visit-hint';
import { formatDuration, videoFor } from '@/features/help/videos';
import { useMediaQuery } from '@/hooks/useMediaQuery';
import { HelpInline } from './help-inline';

interface FirstVisitHintProps {
  hint: ReturnType<typeof useFirstVisitHint>;
  desktopAnchorRef: RefObject<HTMLElement>;
  mobileAnchorRef: RefObject<HTMLElement>;
}

export function FirstVisitHint({ hint, desktopAnchorRef, mobileAnchorRef }: FirstVisitHintProps) {
  const desktop = useMediaQuery('(min-width: 768px)', true);
  const titleId = useId();
  const { doc } = hint;
  if (!hint.visible || !doc) return null;
  const video = videoFor(doc.key);

  return (
    <Popover
      open
      onOpenChange={(o) => {
        if (!o) hint.dismiss();
      }}
    >
      <PopoverAnchor virtualRef={desktop ? desktopAnchorRef : mobileAnchorRef} />
      <PopoverContent
        data-help-hint=""
        aria-labelledby={titleId}
        side="bottom"
        align="end"
        className="w-80 max-w-[calc(100vw-2rem)] p-3"
        onOpenAutoFocus={(e) => e.preventDefault()}
        // Radix closes a non-modal popover on any focusin outside it; the hint must survive Tab and Enter between fields.
        onFocusOutside={(e) => e.preventDefault()}
      >
        <p id={titleId} className="flex items-center gap-2 text-sm font-semibold">
          <AppIcon name="helpHint" size={18} />
          Lần đầu dùng {doc.title}?
        </p>
        <p className="mt-2 hidden text-sm text-muted-foreground md:block">
          <HelpInline
            text={`Bấm **?** hoặc \`F1\` để xem ${doc.steps.length} bước chính${video ? ` và video ${formatDuration(video.duration)}` : ''}. Khung mở bên phải, vẫn nhập liệu được.`}
          />
        </p>
        <p className="mt-2 text-sm text-muted-foreground md:hidden">
          Mở menu tài khoản rồi chọn Hướng dẫn màn hình này.
        </p>
        <div className="mt-3 flex items-center gap-2">
          <Button size="sm" onClick={hint.openFromHint}>
            <AppIcon name="help" tone="none" />
            Xem hướng dẫn
          </Button>
          <Button size="sm" variant="outline" onClick={hint.dismiss}>
            Ẩn
          </Button>
        </div>
        <button
          type="button"
          onClick={hint.turnOff}
          className="mt-2 text-xs text-muted-foreground underline hover:text-foreground"
        >
          Không hiện gợi ý nữa
        </button>
      </PopoverContent>
    </Popover>
  );
}
