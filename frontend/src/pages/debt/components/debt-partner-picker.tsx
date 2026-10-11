import { useEffect, useId, useRef, useState, type KeyboardEvent } from 'react';
import { X } from 'lucide-react';
import type { DebtSide } from '@/features/cash-common/types';
import { useDebtPartnerSearch } from '@/features/debt/hooks';
import type { DebtPartnerSearchItem } from '@/features/debt/types';
import { Input } from '@/components/ui/input';
import { useDebouncedValue } from '@/lib/use-debounced-value';
import { cn } from '@/lib/utils';

export interface DebtPartnerPickerProps {
  id: string;
  // No side: any role.
  side?: DebtSide;
  // Only partners that are both customer and supplier (debt offsets).
  bothRoles?: boolean;
  selectedLabel: string;
  onSelect: (partner: DebtPartnerSearchItem) => void;
  // Shows a clear button while a partner is selected (list filters).
  onClear?: () => void;
  placeholder?: string;
}

// Partners from GET /debt/partners; an empty keyword lists the first ones on focus.
export function DebtPartnerPicker({
  id,
  side,
  bothRoles = false,
  selectedLabel,
  onSelect,
  onClear,
  placeholder = 'Mã hoặc tên đối tượng',
}: DebtPartnerPickerProps) {
  // null: the input shows the selected partner.
  const [keyword, setKeyword] = useState<string | null>(null);
  const [isOpen, setIsOpen] = useState(false);
  const [highlighted, setHighlighted] = useState(0);
  const debouncedKeyword = useDebouncedValue(keyword ?? '', 250);
  // Matching and offsets settle existing debt, which an inactive partner may still have.
  const { data, isLoading, isError } = useDebtPartnerSearch(side, debouncedKeyword, {
    bothRoles,
    includeInactive: true,
  });
  const results = data ?? [];
  const containerRef = useRef<HTMLDivElement>(null);
  const listboxId = `${id}-partners-${useId()}`;
  const canClear = !!onClear && !!selectedLabel && keyword === null;

  useEffect(() => setHighlighted(0), [data]);

  useEffect(() => {
    function onDocMouseDown(e: MouseEvent) {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) {
        setIsOpen(false);
        setKeyword(null);
      }
    }
    document.addEventListener('mousedown', onDocMouseDown);
    return () => document.removeEventListener('mousedown', onDocMouseDown);
  }, []);

  function pick(partner: DebtPartnerSearchItem) {
    onSelect(partner);
    setKeyword(null);
    setIsOpen(false);
  }

  function onKeyDown(e: KeyboardEvent<HTMLInputElement>) {
    if (e.key === 'Escape') {
      setIsOpen(false);
      setKeyword(null);
      return;
    }
    if (!isOpen || results.length === 0) return;
    if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
      e.preventDefault();
      const step = e.key === 'ArrowDown' ? 1 : -1;
      setHighlighted((i) => (i + step + results.length) % results.length);
    } else if (e.key === 'Enter') {
      e.preventDefault();
      const choice = results[highlighted];
      if (choice) pick(choice);
    }
  }

  return (
    <div className="relative" ref={containerRef}>
      <Input
        id={id}
        role="combobox"
        aria-label="Đối tượng"
        aria-expanded={isOpen}
        aria-controls={listboxId}
        aria-autocomplete="list"
        autoComplete="off"
        placeholder={placeholder}
        value={keyword ?? selectedLabel}
        title={selectedLabel || undefined}
        className={canClear ? 'pr-8' : undefined}
        onFocus={() => setIsOpen(true)}
        onKeyDown={onKeyDown}
        onChange={(e) => {
          setKeyword(e.target.value);
          setIsOpen(true);
        }}
      />
      {canClear && (
        <button
          type="button"
          aria-label="Bỏ chọn đối tượng"
          tabIndex={-1}
          onClick={onClear}
          className="absolute right-2 top-1/2 -translate-y-1/2 rounded-sm p-0.5 text-muted-foreground hover:bg-accent"
        >
          <X className="h-4 w-4" />
        </button>
      )}
      {isOpen && (
        <ul
          id={listboxId}
          role="listbox"
          className="absolute left-0 z-50 mt-1 max-h-80 min-w-full overflow-auto rounded-md border bg-popover py-1 text-sm text-popover-foreground shadow-md sm:w-96"
        >
          {isLoading && <li className="px-2 py-2 text-center text-muted-foreground">Đang tìm kiếm...</li>}
          {!isLoading && isError && (
            <li className="px-2 py-2 text-center text-destructive">Không thể tải danh sách đối tượng.</li>
          )}
          {!isLoading && !isError && results.length === 0 && (
            <li className="px-2 py-2 text-center text-muted-foreground">Không tìm thấy đối tượng phù hợp</li>
          )}
          {!isLoading &&
            !isError &&
            results.map((p, i) => (
              <li
                key={p.id}
                role="option"
                aria-selected={i === highlighted}
                onMouseDown={(e) => {
                  // Avoid the blur before the selection runs.
                  e.preventDefault();
                  pick(p);
                }}
                onMouseEnter={() => setHighlighted(i)}
                className={cn('flex cursor-pointer gap-2 px-2 py-1', i === highlighted && 'qldh-lookup-row-highlight')}
              >
                <span className="font-medium">{p.code}</span>
                <span className="truncate text-muted-foreground">{p.name}</span>
              </li>
            ))}
        </ul>
      )}
    </div>
  );
}
