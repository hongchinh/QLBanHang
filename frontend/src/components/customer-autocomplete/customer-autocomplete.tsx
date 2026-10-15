import { useEffect, useId, useRef, useState, type KeyboardEvent } from 'react';
import { AppIcon } from '@/components/icons';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { cn } from '@/lib/utils';
import { useDebouncedValue } from '@/lib/use-debounced-value';
import { useCustomersSearch } from '@/features/customers/hooks';
import { PartnerCatalogDialog } from '@/components/partner-autocomplete/partner-catalog-dialog';
import type { CustomerSearchItem } from '@/features/customers/types';

export interface CustomerAutocompleteProps {
  value: { id: string; code: string; name: string } | null;
  onSelect: (customer: CustomerSearchItem) => void;
  onClear: () => void;
  onAddNewClick: () => void;
  disabled?: boolean;
  inputId?: string;
  inputAriaLabel?: string;
  placeholder?: string;
  errorMessage?: string;
}

export function CustomerAutocomplete({
  value,
  onSelect,
  onClear,
  onAddNewClick,
  disabled,
  inputId,
  inputAriaLabel,
  placeholder = 'Nhập mã / tên / MST / địa chỉ / SĐT...',
  errorMessage,
}: CustomerAutocompleteProps) {
  const [keyword, setKeyword] = useState('');
  const [isOpen, setIsOpen] = useState(false);
  const [catalogOpen, setCatalogOpen] = useState(false);
  const [highlightedIndex, setHighlightedIndex] = useState(0);

  const debouncedKeyword = useDebouncedValue(keyword, 250);
  const { data, isLoading, isError } = useCustomersSearch(debouncedKeyword, { activeOnly: true, limit: 20 });
  const results = data ?? [];

  const inputRef = useRef<HTMLInputElement>(null);
  const containerRef = useRef<HTMLDivElement>(null);
  const reactId = useId();
  const listboxId = `customer-listbox-${reactId}`;

  useEffect(() => {
    setHighlightedIndex(0);
  }, [debouncedKeyword, data]);

  useEffect(() => {
    function onDocClick(e: MouseEvent) {
      if (!containerRef.current) return;
      if (!containerRef.current.contains(e.target as Node)) {
        setIsOpen(false);
      }
    }
    document.addEventListener('mousedown', onDocClick);
    return () => document.removeEventListener('mousedown', onDocClick);
  }, []);

  const hasSelection = value !== null;
  const showDropdown = isOpen && debouncedKeyword.length > 0;
  const activeOptionId =
    showDropdown && results[highlightedIndex] ? `customer-option-${results[highlightedIndex].id}` : undefined;

  function selectCustomer(c: CustomerSearchItem) {
    onSelect(c);
    setKeyword('');
    setIsOpen(false);
  }

  function handleClear() {
    onClear();
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
        if (pick) selectCustomer(pick);
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
      <div className="flex items-stretch gap-2">
        <div className="relative flex-1">
          {hasSelection ? (
            <div className="flex items-center gap-2">
              <Input
                id={inputId}
                ref={inputRef}
                value={value.code}
                aria-label={inputAriaLabel}
                onChange={(e) => {
                  // Typing while a value is selected starts a new search.
                  onClear();
                  setKeyword(e.target.value);
                  setIsOpen(true);
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
                placeholder={placeholder}
                disabled={disabled}
                className="pr-8"
                autoComplete="off"
              />
              <button
                type="button"
                aria-label="Bỏ chọn khách hàng"
                onClick={handleClear}
                className="absolute right-2 top-1/2 -translate-y-1/2 rounded-sm p-0.5 text-muted-foreground hover:bg-accent hover:text-accent-foreground focus:outline-none focus:ring-2 focus:ring-ring"
                tabIndex={-1}
              >
                <AppIcon name="close" />
              </button>
            </div>
          ) : (
            <Input
              id={inputId}
              ref={inputRef}
              value={keyword}
              aria-label={inputAriaLabel}
              onChange={(e) => {
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
              placeholder={placeholder}
              disabled={disabled}
              autoComplete="off"
            />
          )}
        </div>
        <Button
          type="button"
          variant="outline"
          size="icon"
          onClick={onAddNewClick}
          aria-label="Thêm mới khách hàng"
          disabled={disabled}
        >
          <AppIcon name="add" />
        </Button>
      </div>

      {errorMessage && <p className="text-sm text-destructive">{errorMessage}</p>}

      <PartnerCatalogDialog
        open={catalogOpen}
        onOpenChange={setCatalogOpen}
        initialQuery={keyword}
        scope="customer"
        preferredRole="customer"
        onSelect={onSelect}
      />

      {showDropdown && (
        <div className="relative">
          <div className="absolute left-0 z-50 mt-1 min-w-[min(760px,calc(100vw-80px))] max-w-[calc(100vw-40px)] max-h-80 flex flex-col rounded-md border bg-popover text-popover-foreground shadow-md">
            <div className="flex items-center justify-between border-b bg-muted/30 px-2 py-1.5 text-xs text-muted-foreground flex-shrink-0">
              <span>
                {isLoading
                  ? 'Đang tìm kiếm...'
                  : isError
                  ? 'Lỗi tải danh sách'
                  : `Tìm thấy ${results.length} khách hàng đang hoạt động`}
              </span>
              <span className="flex items-center gap-1.5">
                <kbd className="rounded border bg-background px-1 py-0.5 font-mono text-[10px]">Tab</kbd>
                duyệt
                <kbd className="rounded border bg-background px-1 py-0.5 font-mono text-[10px]">Enter</kbd>
                chọn
                <kbd className="rounded border bg-background px-1 py-0.5 font-mono text-[10px]">Esc</kbd>
                đóng
              </span>
            </div>
            <div className="overflow-auto flex-1">
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
                  {isLoading && (
                    <tr>
                      <td colSpan={6} className="px-2 py-3 text-center text-muted-foreground">
                        Đang tìm kiếm...
                      </td>
                    </tr>
                  )}
                  {!isLoading && isError && (
                    <tr>
                      <td colSpan={6} className="px-2 py-3 text-center text-destructive">
                        Không thể tải danh sách khách hàng. Vui lòng thử lại.
                      </td>
                    </tr>
                  )}
                  {!isLoading && !isError && results.length === 0 && (
                    <tr>
                      <td colSpan={6} className="px-2 py-3 text-center text-muted-foreground">
                        Không tìm thấy khách hàng phù hợp
                      </td>
                    </tr>
                  )}
                  {!isLoading &&
                    !isError &&
                    results.map((c, i) => {
                      const highlighted = i === highlightedIndex;
                      return (
                        <tr
                          key={c.id}
                          id={`customer-option-${c.id}`}
                          role="option"
                          aria-selected={highlighted}
                          onMouseDown={(e) => {
                            // Avoid blur before click handler runs.
                            e.preventDefault();
                            selectCustomer(c);
                          }}
                          onMouseEnter={() => setHighlightedIndex(i)}
                          className={cn(
                            'cursor-pointer border-t',
                            highlighted && 'qldh-lookup-row-highlight',
                          )}
                        >
                          <td className="px-2 py-1 align-top">{c.code}</td>
                          <td className="px-2 py-1 align-top">{c.name}</td>
                          <td className="px-2 py-1 align-top text-muted-foreground">{c.taxCode ?? ''}</td>
                          <td className="px-2 py-1 align-top text-muted-foreground">{c.companyAddress ?? ''}</td>
                          <td className="px-2 py-1 align-top text-muted-foreground">{c.phoneNumber ?? ''}</td>
                          <td className="px-2 py-1 align-top">
                            <Badge variant="secondary">Khách hàng</Badge>
                          </td>
                        </tr>
                      );
                    })}
                </tbody>
              </table>
            </div>
            <div className="border-t flex-shrink-0">
              <button
                type="button"
                className="flex w-full items-center gap-2 px-3 py-2 text-sm text-muted-foreground hover:bg-muted/50 hover:text-foreground transition-colors"
                onMouseDown={(e) => {
                  e.preventDefault();
                  setIsOpen(false);
                  setCatalogOpen(true);
                }}
              >
                <AppIcon name="listPicker" size={14} />
                Xem danh mục đầy đủ
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
