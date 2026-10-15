import { forwardRef } from 'react';
import { AppIcon } from '@/components/icons';
import { useUiStore } from '@/stores/ui-store';

const LABEL = 'Hướng dẫn màn hình này (F1)';

// Desktop only: on small screens the user menu carries the same entry.
export const HelpButton = forwardRef<HTMLButtonElement, { showDot: boolean }>(function HelpButton(
  { showDot },
  ref,
) {
  const helpOpen = useUiStore((s) => s.helpOpen);
  const toggleHelp = useUiStore((s) => s.toggleHelp);
  return (
    <button
      ref={ref}
      type="button"
      aria-label={LABEL}
      title={LABEL}
      aria-expanded={helpOpen}
      onClick={toggleHelp}
      className="relative hidden h-11 w-11 items-center justify-center rounded-md text-header-fg hover:bg-header-active md:flex"
    >
      <AppIcon name="help" size={20} />
      {showDot && (
        <span
          data-testid="help-hint-dot"
          className="absolute right-2 top-2 h-2 w-2 rounded-full bg-[hsl(var(--icon-warn))] ring-2 ring-header-bg"
        />
      )}
    </button>
  );
});
