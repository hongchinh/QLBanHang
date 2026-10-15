import { useEffect, useId, useRef, useState, type KeyboardEvent } from 'react';
import { AppIcon } from '@/components/icons';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';
import { focusById } from '@/lib/enter-focus';
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
  // aria-describedby of the input (e.g. the id of an error paragraph).
  describedBy?: string;
  // Focus target after a pick (the partner name input).
  nameInputId?: string;
  // Enables the + button, F4 and the "Thêm mới" row.
  onAddNew?: (keyword: string) => void;
  // Enables the "Xem danh mục đầy đủ" row.
  onOpenCatalog?: (keyword: string) => void;
  // Reports whether text is typed but no partner is picked.
  onUnpickedChange?: (hasUnpickedText: boolean) => void;
};

// What ↑↓ / Tab walk over: the results, then the add-new row, then the catalog row.
type ListRow = { kind: 'result'; item: PartnerSearchItem } | { kind: 'addNew' } | { kind: 'catalog' };

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
  describedBy,
  nameInputId,
  onAddNew,
  onOpenCatalog,
  onUnpickedChange,
}: PartnerAutocompleteProps) {
  const [keyword, setKeyword] = useState('');
  const [isOpen, setIsOpen] = useState(false);
  const [highlightedIndex, setHighlightedIndex] = useState(0);
  // Enter pressed while the results were not yet those of the typed keyword.
  const [enterWaiting, setEnterWaiting] = useState(false);

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
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) {
        setIsOpen(false);
        setEnterWaiting(false);
      }
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

  // Pending: the results shown are not (yet) the results of the typed keyword.
  const hasList = !isLoading && !isError;
  const pending = keyword.trim() !== debouncedKeyword.trim() || !!search.isFetching;
  const rows: ListRow[] = [];
  if (hasList) for (const item of results) rows.push({ kind: 'result', item });
  if (onAddNew && hasList && !pending) rows.push({ kind: 'addNew' });
  if (onOpenCatalog) rows.push({ kind: 'catalog' });
  const activeIndex = Math.min(highlightedIndex, rows.length - 1);
  const activeRow = rows[activeIndex];
  const addNewId = `${listboxId}-add-new`;
  const catalogId = `${listboxId}-catalog`;
  function rowId(row: ListRow) {
    return row.kind === 'result' ? `${listboxId}-${row.item.id}` : row.kind === 'addNew' ? addNewId : catalogId;
  }
  const activeOptionId = showDropdown && activeRow ? rowId(activeRow) : undefined;

  // A value from outside (list, quick add, catalog, parent) or a blocked field leaves no typed text behind.
  const valueId = value?.id;
  useEffect(() => {
    if (valueId !== undefined || isDisabled) {
      setKeyword('');
      setIsOpen(false);
      setEnterWaiting(false);
    }
  }, [valueId, isDisabled]);

  const hasUnpickedText = !isDisabled && !value && keyword.trim() !== '';
  const onUnpickedChangeRef = useRef(onUnpickedChange);
  useEffect(() => {
    onUnpickedChangeRef.current = onUnpickedChange;
  });
  useEffect(() => {
    onUnpickedChangeRef.current?.(hasUnpickedText);
  }, [hasUnpickedText]);

  const focusTimer = useRef<ReturnType<typeof setTimeout>>();
  useEffect(() => () => clearTimeout(focusTimer.current), []);

  // Only results the list is showing can be matched (not the old data behind an error).
  function exactMatch() {
    const upper = keyword.trim().toUpperCase();
    return hasList && upper.length > 0 ? results.find((r) => r.code.toUpperCase() === upper) : undefined;
  }

  function selectPartner(p: PartnerSearchItem) {
    onSelect(p);
    setKeyword('');
    setIsOpen(false);
    setEnterWaiting(false);
    if (nameInputId) {
      clearTimeout(focusTimer.current);
      focusTimer.current = setTimeout(() => focusById(nameInputId, 'end'), 0);
    }
  }

  // The + button, F4 and the add-new row. With a value picked the new partner starts with an empty name.
  function openQuickAdd() {
    setIsOpen(false);
    setEnterWaiting(false);
    onAddNew?.(value ? '' : keyword.trim());
  }

  function activateFooter(kind: 'addNew' | 'catalog') {
    if (kind === 'addNew') {
      openQuickAdd();
      return;
    }
    setIsOpen(false);
    setEnterWaiting(false);
    onOpenCatalog?.(keyword.trim());
  }

  // Enter with results of the typed keyword: an exact code wins, else the highlighted row.
  function applyEnter() {
    const exact = exactMatch();
    if (exact) {
      selectPartner(exact);
      return;
    }
    if (!activeRow) return;
    if (activeRow.kind === 'result') selectPartner(activeRow.item);
    else activateFooter(activeRow.kind);
  }

  // The results of a waiting Enter arrived: exact code, else the first result, else just show the add row.
  function settleWaitingEnter() {
    setEnterWaiting(false);
    if (value || isDisabled || !hasList) return;
    const pick = exactMatch() ?? results[0];
    if (pick) {
      selectPartner(pick);
    } else {
      setIsOpen(true);
      setHighlightedIndex(0);
    }
  }
  const settleWaitingEnterRef = useRef(settleWaitingEnter);
  useEffect(() => {
    settleWaitingEnterRef.current = settleWaitingEnter;
  });
  useEffect(() => {
    if (enterWaiting && !pending) settleWaitingEnterRef.current();
  }, [enterWaiting, pending, data]);

  function moveHighlight(step: 1 | -1) {
    if (rows.length === 0) return;
    setHighlightedIndex((i) => (Math.min(i, rows.length - 1) + step + rows.length) % rows.length);
  }

  function handleClear() {
    onSelect(null);
    setKeyword('');
    setIsOpen(false);
    setTimeout(() => inputRef.current?.focus(), 0);
  }

  // A waiting Enter never activates a footer row. A plain Enter acts on the visible highlighted row; in the
  // loading / error state the only row is the catalog row, so a plain Enter there opens the catalog.
  function handleEnter(e: KeyboardEvent<HTMLInputElement>) {
    // Modified or composing Enter belongs to the form / the input method.
    if (e.shiftKey || e.ctrlKey || e.altKey || e.metaKey || e.nativeEvent.isComposing) return;
    // Picked or empty: the form walks on.
    if (isDisabled || value || keyword.trim() === '') return;
    e.preventDefault();
    if (!isOpen) setIsOpen(true);
    else if (pending) setEnterWaiting(true);
    else applyEnter();
  }

  function handleKeyDown(e: KeyboardEvent<HTMLInputElement>) {
    if (e.key === 'F4') {
      if (!onAddNew || isDisabled) return;
      e.preventDefault();
      openQuickAdd();
      return;
    }
    if (e.key === 'Enter') {
      handleEnter(e);
      return;
    }
    // The list is hidden (closed, or open but nothing searched yet): Tab follows the DOM, ArrowDown opens.
    if (!showDropdown) {
      if (e.key === 'ArrowDown' && keyword.length > 0) {
        e.preventDefault();
        setIsOpen(true);
      } else if (e.key === 'Escape' && isOpen) {
        setIsOpen(false);
        setEnterWaiting(false);
      }
      return;
    }
    switch (e.key) {
      case 'ArrowDown':
        e.preventDefault();
        moveHighlight(1);
        break;
      case 'ArrowUp':
        e.preventDefault();
        moveHighlight(-1);
        break;
      case 'Tab':
        if (rows.length === 0) return;
        e.preventDefault();
        moveHighlight(e.shiftKey ? -1 : 1);
        break;
      case 'Escape':
        e.preventDefault();
        setIsOpen(false);
        setEnterWaiting(false);
        break;
    }
  }

  return (
    <div className="space-y-1" ref={containerRef}>
      <div className="flex items-center gap-1">
        <div className="relative min-w-0 flex-1">
          <Input
            id={inputId}
            ref={inputRef}
            value={value ? value.code : keyword}
            title={value?.name}
            onChange={(e) => {
              // Typing while a value is selected starts a new search.
              if (value) onSelect(null);
              setEnterWaiting(false);
              setKeyword(e.target.value);
              setIsOpen(e.target.value.length > 0);
            }}
            onKeyDown={handleKeyDown}
            onBlur={(e) => {
              if (!containerRef.current?.contains(e.relatedTarget as Node | null)) setEnterWaiting(false);
            }}
            onFocus={() => {
              if (keyword.length > 0) setIsOpen(true);
            }}
            role="combobox"
            aria-expanded={showDropdown}
            aria-controls={listboxId}
            aria-autocomplete="list"
            aria-activedescendant={activeOptionId}
            aria-describedby={describedBy}
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
        {onAddNew && (
          <Button
            type="button"
            variant="outline"
            size="icon"
            className={compact ? 'h-7 w-7 shrink-0' : 'shrink-0'}
            aria-label={`Thêm nhanh ${noun} (F4)`}
            title={`Thêm nhanh ${noun} (F4)`}
            aria-keyshortcuts="F4"
            disabled={isDisabled}
            onClick={openQuickAdd}
          >
            <AppIcon name="add" />
          </Button>
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
                  {hasList && !pending && results.length === 0 && (
                    <tr>
                      <td colSpan={6} className="px-2 py-3 text-center text-muted-foreground">
                        {`Không tìm thấy ${noun} "${keyword.trim()}"`}
                      </td>
                    </tr>
                  )}
                  {rows.map((row, i) => {
                    const highlighted = i === activeIndex;
                    const common = {
                      id: rowId(row),
                      role: 'option' as const,
                      'aria-selected': highlighted,
                      onMouseEnter: () => setHighlightedIndex(i),
                    };
                    if (row.kind === 'result') {
                      const p = row.item;
                      return (
                        <tr
                          key={p.id}
                          {...common}
                          onMouseDown={(e) => {
                            // Avoid blur before the selection runs.
                            e.preventDefault();
                            selectPartner(p);
                          }}
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
                    }
                    const isAddNew = row.kind === 'addNew';
                    return (
                      <tr
                        key={row.kind}
                        {...common}
                        onMouseDown={(e) => {
                          e.preventDefault();
                          activateFooter(row.kind);
                        }}
                        className={cn('cursor-pointer border-t', highlighted && 'qldh-lookup-row-highlight')}
                      >
                        <td colSpan={6} className="px-2 py-1.5">
                          <span className="flex items-center gap-2">
                            <AppIcon name={isAddNew ? 'add' : 'listPicker'} size={14} />
                            <span>
                              {isAddNew ? `Thêm mới ${noun} "${keyword.trim()}"` : 'Xem danh mục đầy đủ'}
                            </span>
                            {isAddNew && (
                              <span className="ml-auto text-xs text-muted-foreground">
                                {results.length === 0 ? 'Enter / F4' : 'F4'}
                              </span>
                            )}
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
