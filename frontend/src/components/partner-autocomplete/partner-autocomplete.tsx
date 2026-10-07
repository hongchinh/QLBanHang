import { useEffect, useId, useRef, useState, type KeyboardEvent } from 'react';
import { X } from 'lucide-react';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { cn } from '@/lib/utils';
import { useDebouncedValue } from '@/lib/use-debounced-value';
import { usePartnerSearch } from '@/features/stock-vouchers/hooks';
import type { PartnerSearchItem } from '@/features/stock-vouchers/types';
import type { PartnerType, StockDirection } from '@/features/stock-reasons/types';

export interface PartnerAutocompleteProps {
  type: StockDirection;
  reasonId?: string;
  partnerType?: PartnerType;
  // Form mode (default): needs a reason and searches the reason's partner role.
  // Filter mode (false): always enabled and searches every role.
  requireReason?: boolean;
  value: { id: string; code: string; name: string } | null;
  onSelect: (partner: PartnerSearchItem | null) => void;
  disabled?: boolean;
  inputId?: string;
  placeholder?: string;
}

// Same interaction as CustomerAutocomplete, backed by GET /stock-vouchers/partners (D3).
export function PartnerAutocomplete({
  type,
  reasonId,
  partnerType,
  requireReason = true,
  value,
  onSelect,
  disabled,
  inputId,
  placeholder = 'Nhập mã / tên / MST đối tượng...',
}: PartnerAutocompleteProps) {
  const [keyword, setKeyword] = useState('');
  const [isOpen, setIsOpen] = useState(false);
  const [highlightedIndex, setHighlightedIndex] = useState(0);

  const debouncedKeyword = useDebouncedValue(keyword, 250);
  const { data, isLoading, isError } = usePartnerSearch(
    type,
    debouncedKeyword,
    requireReason ? reasonId : undefined,
  );
  const results: PartnerSearchItem[] = data ?? [];

  const inputRef = useRef<HTMLInputElement>(null);
  const containerRef = useRef<HTMLDivElement>(null);
  const reactId = useId();
  const listboxId = `partner-listbox-${reactId}`;

  useEffect(() => {
    setHighlightedIndex(0);
  }, [debouncedKeyword, data]);

  useEffect(() => {
    function onDocClick(e: MouseEvent) {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) setIsOpen(false);
    }
    document.addEventListener('mousedown', onDocClick);
    return () => document.removeEventListener('mousedown', onDocClick);
  }, []);

  let effectivePlaceholder = placeholder;
  let blocked = false;
  if (requireReason && !reasonId) {
    blocked = true;
    effectivePlaceholder = 'Chọn lý do trước';
  } else if (requireReason && partnerType === 'None') {
    blocked = true;
    effectivePlaceholder = 'Không cần đối tượng';
  }
  const isDisabled = disabled || blocked;

  const showDropdown = isOpen && !isDisabled && debouncedKeyword.trim().length > 0;
  const activeOptionId =
    showDropdown && results[highlightedIndex] ? `partner-option-${results[highlightedIndex].id}` : undefined;

  function selectPartner(p: PartnerSearchItem) {
    onSelect(p);
    setKeyword('');
    setIsOpen(false);
  }

  function handleClear() {
    onSelect(null);
    setKeyword('');
    setIsOpen(false);
    setTimeout(() => inputRef.current?.focus(), 0);
  }

  function handleKeyDown(e: KeyboardEvent<HTMLInputElement>) {
    if (!isOpen) {
      if (e.key === 'ArrowDown' && keyword.length > 0) {
        e.preventDefault();
        setIsOpen(true);
      }
      return;
    }
    switch (e.key) {
      case 'ArrowDown':
        e.preventDefault();
        if (results.length === 0) return;
        setHighlightedIndex((i) => (i + 1) % results.length);
        break;
      case 'ArrowUp':
        e.preventDefault();
        if (results.length === 0) return;
        setHighlightedIndex((i) => (i - 1 + results.length) % results.length);
        break;
      case 'Tab':
        if (results.length === 0) return;
        e.preventDefault();
        setHighlightedIndex((i) =>
          e.shiftKey ? (i - 1 + results.length) % results.length : (i + 1) % results.length,
        );
        break;
      case 'Enter': {
        e.preventDefault();
        if (results.length === 0) return;
        const upper = keyword.trim().toUpperCase();
        const exact = upper.length > 0 ? results.find((r) => r.code.toUpperCase() === upper) : undefined;
        const pick = exact ?? results[highlightedIndex];
        if (pick) selectPartner(pick);
        break;
      }
      case 'Escape':
        e.preventDefault();
        setIsOpen(false);
        break;
    }
  }

  const comboboxProps = {
    id: inputId,
    ref: inputRef,
    onKeyDown: handleKeyDown,
    onFocus: () => {
      if (keyword.length > 0) setIsOpen(true);
    },
    role: 'combobox' as const,
    'aria-expanded': showDropdown,
    'aria-controls': listboxId,
    'aria-autocomplete': 'list' as const,
    'aria-activedescendant': activeOptionId,
    placeholder: effectivePlaceholder,
    disabled: isDisabled,
    autoComplete: 'off',
  };

  return (
    <div className="space-y-1" ref={containerRef}>
      <div className="relative">
        {value ? (
          <>
            <Input
              {...comboboxProps}
              value={value.code}
              title={value.name}
              onChange={(e) => {
                // Typing while a value is selected starts a new search.
                onSelect(null);
                setKeyword(e.target.value);
                setIsOpen(true);
              }}
              className="pr-8"
            />
            {!isDisabled && (
              <button
                type="button"
                aria-label="Bỏ chọn đối tượng"
                onClick={handleClear}
                className="absolute right-2 top-1/2 -translate-y-1/2 rounded-sm p-0.5 text-muted-foreground hover:bg-accent hover:text-accent-foreground focus:outline-none focus:ring-2 focus:ring-ring"
                tabIndex={-1}
              >
                <X className="h-4 w-4" />
              </button>
            )}
          </>
        ) : (
          <Input
            {...comboboxProps}
            value={keyword}
            onChange={(e) => {
              setKeyword(e.target.value);
              setIsOpen(e.target.value.length > 0);
            }}
          />
        )}
      </div>

      {showDropdown && (
        <div className="relative">
          <div className="absolute left-0 z-50 mt-1 min-w-[min(640px,calc(100vw-80px))] max-w-[calc(100vw-40px)] max-h-80 overflow-auto rounded-md border bg-popover text-popover-foreground shadow-md">
            {/* eslint-disable-next-line jsx-a11y/no-noninteractive-element-to-interactive-role -- WAI-ARIA combobox listbox pattern */}
            <table className="qldh-lookup-table" id={listboxId} role="listbox">
              <thead className="qldh-lookup-table-header">
                <tr>
                  <th className="px-2 py-1">Mã</th>
                  <th className="px-2 py-1">Tên</th>
                  <th className="px-2 py-1">MST</th>
                  <th className="px-2 py-1">Địa chỉ</th>
                  <th className="px-2 py-1">Loại</th>
                </tr>
              </thead>
              <tbody>
                {isLoading && (
                  <tr>
                    <td colSpan={5} className="px-2 py-3 text-center text-muted-foreground">
                      Đang tìm kiếm...
                    </td>
                  </tr>
                )}
                {!isLoading && isError && (
                  <tr>
                    <td colSpan={5} className="px-2 py-3 text-center text-destructive">
                      Không thể tải danh sách đối tượng. Vui lòng thử lại.
                    </td>
                  </tr>
                )}
                {!isLoading && !isError && results.length === 0 && (
                  <tr>
                    <td colSpan={5} className="px-2 py-3 text-center text-muted-foreground">
                      Không tìm thấy đối tượng phù hợp
                    </td>
                  </tr>
                )}
                {!isLoading &&
                  !isError &&
                  results.map((p, i) => {
                    const highlighted = i === highlightedIndex;
                    return (
                      <tr
                        key={p.id}
                        id={`partner-option-${p.id}`}
                        role="option"
                        aria-selected={highlighted}
                        onMouseDown={(e) => {
                          // Avoid blur before the selection runs.
                          e.preventDefault();
                          selectPartner(p);
                        }}
                        onMouseEnter={() => setHighlightedIndex(i)}
                        className={cn('cursor-pointer border-t', highlighted && 'qldh-lookup-row-highlight')}
                      >
                        <td className="px-2 py-1 align-top">{p.code}</td>
                        <td className="px-2 py-1 align-top">{p.name}</td>
                        <td className="px-2 py-1 align-top text-muted-foreground">{p.taxCode ?? ''}</td>
                        <td className="px-2 py-1 align-top text-muted-foreground">{p.companyAddress ?? ''}</td>
                        <td className="px-2 py-1 align-top">
                          <span className="flex gap-1">
                            {p.isCustomer && <Badge variant="secondary">Khách hàng</Badge>}
                            {p.isSupplier && <Badge variant="secondary">NCC</Badge>}
                          </span>
                        </td>
                      </tr>
                    );
                  })}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </div>
  );
}
