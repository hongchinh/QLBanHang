import { useEffect, useId, useRef, useState, type KeyboardEvent } from 'react';
import { AppIcon } from '@/components/icons';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { cn } from '@/lib/utils';
import { useDebouncedValue } from '@/lib/use-debounced-value';
import { usePartnerSearch } from '@/features/stock-vouchers/hooks';
import type { PartnerSearchItem } from '@/features/stock-vouchers/types';
import type { PartnerType, StockDirection } from '@/features/stock-reasons/types';
import { partnerSearchSummary, type PartnerNoun } from './search-summary';

export type { PartnerNoun } from './search-summary';

// A partner search hook: `useSearch(keyword, reasonId, activeOnly)`, e.g. the cash voucher partner search.
export type PartnerSearchHook = (
  keyword: string,
  reasonId?: string,
  activeOnly?: boolean,
) => { data?: PartnerSearchItem[]; isFetching: boolean; isLoading?: boolean; isError?: boolean };

// Default: GET /stock-vouchers/partners of the voucher direction.
function useStockPartnerSearch(
  keyword: string,
  reasonId: string | undefined,
  activeOnly: boolean | undefined,
  type: StockDirection | undefined,
) {
  return usePartnerSearch(type ?? 'In', keyword, reasonId, activeOnly);
}

// `type` picks the default stock voucher search; a caller with its own `useSearch` needs no type.
export type PartnerSearchSource =
  | { type: StockDirection; useSearch?: undefined }
  | { type?: StockDirection; useSearch: PartnerSearchHook };

export type PartnerAutocompleteProps = PartnerSearchSource & {
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
  // Only active partners (the voucher forms); the list header says "đang dùng".
  activeOnly?: boolean;
  // What the list calls its results.
  noun?: PartnerNoun;
  // h-7 input and buttons (dense forms).
  compact?: boolean;
  // Focus target after a pick (the partner name input).
  nameInputId?: string;
  // Enables the + button, F4 and the "Thêm mới" row.
  onAddNew?: (keyword: string) => void;
  // Enables the "Xem danh mục đầy đủ" row.
  onOpenCatalog?: (keyword: string) => void;
  // Reports whether text is typed but no partner is picked.
  onUnpickedChange?: (hasUnpickedText: boolean) => void;
};

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
  useSearch,
  activeOnly,
  noun = 'đối tượng',
  compact,
  onAddNew,
}: PartnerAutocompleteProps) {
  const [keyword, setKeyword] = useState('');
  const [isOpen, setIsOpen] = useState(false);
  const [highlightedIndex, setHighlightedIndex] = useState(0);

  const debouncedKeyword = useDebouncedValue(keyword, 250);
  // Exactly one search hook call per render, so the hook order stays stable.
  const usePartnerResults: (
    keyword: string,
    reasonId: string | undefined,
    activeOnly: boolean | undefined,
    type: StockDirection | undefined,
  ) => ReturnType<PartnerSearchHook> = useSearch ?? useStockPartnerSearch;
  const search = usePartnerResults(debouncedKeyword, requireReason ? reasonId : undefined, activeOnly, type);
  const { data } = search;
  const isLoading = search.isLoading ?? (search.isFetching && !data);
  const isError = search.isError ?? false;
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
  } else if (partnerType === 'None') {
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

  return (
    <div className="space-y-1" ref={containerRef}>
      <div className="relative">
        <Input
          id={inputId}
          ref={inputRef}
          value={value ? value.code : keyword}
          title={value?.name}
          onChange={(e) => {
            // Typing while a value is selected starts a new search.
            if (value) onSelect(null);
            setKeyword(e.target.value);
            setIsOpen(e.target.value.length > 0);
          }}
          onKeyDown={handleKeyDown}
          onFocus={() => {
            if (keyword.length > 0) setIsOpen(true);
          }}
          role="combobox"
          aria-expanded={showDropdown}
          aria-controls={listboxId}
          aria-autocomplete="list"
          aria-activedescendant={activeOptionId}
          placeholder={effectivePlaceholder}
          disabled={isDisabled}
          autoComplete="off"
          className={cn(compact && 'h-7', value && 'pr-8')}
        />
        {value && !isDisabled && (
          <button
            type="button"
            aria-label="Bỏ chọn đối tượng"
            onClick={handleClear}
            className="absolute right-2 top-1/2 -translate-y-1/2 rounded-sm p-0.5 text-muted-foreground hover:bg-accent hover:text-accent-foreground focus:outline-none focus:ring-2 focus:ring-ring"
            tabIndex={-1}
          >
            <AppIcon name="close" />
          </button>
        )}
      </div>

      {showDropdown && (
        <div className="relative">
          <div className="absolute left-0 z-50 mt-1 flex max-h-80 min-w-[min(760px,calc(100vw-80px))] max-w-[calc(100vw-40px)] flex-col overflow-hidden rounded-md border bg-popover text-popover-foreground shadow-md">
            <div className="flex shrink-0 items-center justify-between gap-3 border-b px-2 py-1 text-xs text-muted-foreground">
              {isLoading ? (
                <span>Đang tìm kiếm...</span>
              ) : isError ? (
                <span className="text-destructive">Không thể tải danh sách. Vui lòng thử lại.</span>
              ) : (
                <span>{partnerSearchSummary({ count: results.length, noun, activeOnly: !!activeOnly })}</span>
              )}
              <span>{onAddNew ? '↑↓ duyệt · Enter chọn · F4 thêm mới · Esc đóng' : '↑↓ duyệt · Enter chọn · Esc đóng'}</span>
            </div>
            <div className="overflow-auto">
              {/* eslint-disable-next-line jsx-a11y/no-noninteractive-element-to-interactive-role -- WAI-ARIA combobox listbox pattern */}
              <table className="qldh-lookup-table" id={listboxId} role="listbox">
                <thead className="qldh-lookup-table-header">
                  <tr>
                    <th className="px-2 py-1">Mã</th>
                    <th className="px-2 py-1">Tên</th>
                    <th className="px-2 py-1">MST</th>
                    <th className="px-2 py-1">Địa chỉ</th>
                    <th className="px-2 py-1">SĐT</th>
                    <th className="px-2 py-1">Loại</th>
                  </tr>
                </thead>
                <tbody>
                  {!isLoading && !isError && results.length === 0 && (
                    <tr>
                      <td colSpan={6} className="px-2 py-3 text-center text-muted-foreground">
                        {`Không tìm thấy ${noun} "${keyword.trim()}"`}
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
                          <td className="px-2 py-1 align-top text-muted-foreground">{p.phoneNumber ?? ''}</td>
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
        </div>
      )}
    </div>
  );
}
