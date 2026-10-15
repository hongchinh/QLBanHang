import { useEffect, useId, useRef, useState, type KeyboardEvent } from 'react';
import { AppIcon } from '@/components/icons';
import { Input } from '@/components/ui/input';
import { cn } from '@/lib/utils';
import { useDebouncedValue } from '@/lib/use-debounced-value';
import { useCashPartnerSearch } from '@/features/cash-vouchers/hooks';
import type { CashPartnerSearchItem, CashVoucherType } from '@/features/cash-vouchers/types';

interface CashPartnerFilterProps {
  type: CashVoucherType;
  value: { id: string; code: string; name: string } | null;
  onSelect: (partner: CashPartnerSearchItem | null) => void;
}

// List filter backed by GET /cash-vouchers/partners (receipts.view / payments.view), every partner role.
// The stock PartnerAutocomplete searches /stock-vouchers/partners, which needs stock permissions.
export function CashPartnerFilter({ type, value, onSelect }: CashPartnerFilterProps) {
  const [keyword, setKeyword] = useState('');
  const [isOpen, setIsOpen] = useState(false);
  const [highlighted, setHighlighted] = useState(0);
  const debouncedKeyword = useDebouncedValue(keyword, 250);
  const { data, isLoading, isError } = useCashPartnerSearch(type, debouncedKeyword);
  const results = data ?? [];
  const containerRef = useRef<HTMLDivElement>(null);
  const listboxId = `cash-partner-filter-${useId()}`;

  useEffect(() => setHighlighted(0), [debouncedKeyword, data]);

  useEffect(() => {
    function onDocClick(e: MouseEvent) {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) setIsOpen(false);
    }
    document.addEventListener('mousedown', onDocClick);
    return () => document.removeEventListener('mousedown', onDocClick);
  }, []);

  const showDropdown = isOpen && debouncedKeyword.trim().length > 0;

  function pick(p: CashPartnerSearchItem | null) {
    onSelect(p);
    setKeyword('');
    setIsOpen(false);
  }

  function handleKeyDown(e: KeyboardEvent<HTMLInputElement>) {
    if (!showDropdown || results.length === 0) {
      if (e.key === 'Escape') setIsOpen(false);
      return;
    }
    if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
      e.preventDefault();
      const step = e.key === 'ArrowDown' ? 1 : -1;
      setHighlighted((i) => (i + step + results.length) % results.length);
    } else if (e.key === 'Enter') {
      e.preventDefault();
      pick(results[highlighted] ?? null);
    } else if (e.key === 'Escape') {
      e.preventDefault();
      setIsOpen(false);
    }
  }

  return (
    <div className="relative" ref={containerRef}>
      <Input
        role="combobox"
        aria-label="Đối tượng"
        aria-expanded={showDropdown}
        aria-controls={listboxId}
        aria-autocomplete="list"
        autoComplete="off"
        placeholder="Lọc theo đối tượng..."
        value={value ? value.code : keyword}
        title={value?.name}
        className={value ? 'pr-8' : undefined}
        onKeyDown={handleKeyDown}
        onFocus={() => setIsOpen(keyword.length > 0)}
        onChange={(e) => {
          // Typing while a partner is selected starts a new search.
          if (value) onSelect(null);
          setKeyword(e.target.value);
          setIsOpen(e.target.value.length > 0);
        }}
      />
      {value && (
        <button
          type="button"
          aria-label="Bỏ chọn đối tượng"
          tabIndex={-1}
          onClick={() => pick(null)}
          className="absolute right-2 top-1/2 -translate-y-1/2 rounded-sm p-0.5 text-muted-foreground hover:bg-accent"
        >
          <AppIcon name="close" />
        </button>
      )}
      {showDropdown && (
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
