import { Search, X } from 'lucide-react';

interface SidebarSearchProps {
  value: string;
  onChange: (value: string) => void;
  onEnter: () => void;
  onArrowDown: () => void;
  onClear: () => void;
  // null when not searching.
  resultCount: number | null;
  inputRef: React.RefObject<HTMLInputElement>;
  controlsId: string;
}

export function SidebarSearch({
  value,
  onChange,
  onEnter,
  onArrowDown,
  onClear,
  resultCount,
  inputRef,
  controlsId,
}: SidebarSearchProps) {
  const handleKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Enter') {
      e.preventDefault();
      onEnter();
    } else if (e.key === 'ArrowDown') {
      e.preventDefault();
      onArrowDown();
    } else if (e.key === 'Escape') {
      e.preventDefault();
      onClear();
    }
  };

  return (
    <div className="shrink-0 border-b p-3">
      <div className="relative">
        <Search
          aria-hidden
          className="pointer-events-none absolute left-2.5 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground"
        />
        <input
          ref={inputRef}
          type="search"
          aria-label="Tìm chức năng"
          placeholder="Tìm chức năng…"
          aria-controls={controlsId}
          value={value}
          onChange={(e) => onChange(e.target.value)}
          onKeyDown={handleKeyDown}
          className="h-9 w-full rounded-md border bg-background pl-8 pr-8 text-sm placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring [&::-webkit-search-cancel-button]:hidden"
        />
        {value !== '' && (
          <button
            type="button"
            aria-label="Xóa tìm kiếm"
            onClick={onClear}
            className="absolute right-1.5 top-1/2 flex h-6 w-6 -translate-y-1/2 items-center justify-center rounded-sm text-muted-foreground hover:bg-accent hover:text-accent-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
          >
            <X className="h-4 w-4" />
          </button>
        )}
      </div>
      <div aria-live="polite" className="sr-only">
        {resultCount === null ? '' : `${resultCount} kết quả`}
      </div>
    </div>
  );
}
