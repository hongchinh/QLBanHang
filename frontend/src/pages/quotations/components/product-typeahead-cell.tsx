import { useCallback, useEffect, useId, useLayoutEffect, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { AppIcon } from '@/components/icons';
import { Input } from '@/components/ui/input';
import { StopFormEvents } from '@/components/partner-autocomplete/stop-form-events';
import { cn } from '@/lib/utils';
import { useDialogOpening } from '@/lib/use-dialog-opening';
import { useProductSearch } from '@/features/products/hooks';
import type { ProductSuggestion } from '@/features/products/types';
import { ProductCatalogDialog } from '@/features/products/components/product-catalog-dialog';
import { ProductQuickAddDialog } from '@/features/products/components/product-quick-add-dialog';
import { useAuthStore } from '@/stores/auth-store';

interface Props {
  value: string;
  onChange: (value: string) => void;
  onSelect: (s: ProductSuggestion) => void;
  placeholder?: string;
  variant?: 'default' | 'cell';
  /** ID gán cho input này. */
  inputId?: string;
  /** ID của input cần focus ngay sau khi commit. */
  nextFocusId?: string;
  /** Mở danh sách gợi ý khi ô nhận focus. Mặc định true (báo giá giữ nguyên). */
  openOnFocus?: boolean;
  /** Dòng "Thêm mới hàng hóa" và F4 (cần quyền products.create). Mặc định tắt. */
  allowQuickAdd?: boolean;
}

type ListRow = { kind: 'result'; item: ProductSuggestion } | { kind: 'addNew' };

const PRICING_LABEL: Record<ProductSuggestion['pricingMode'], string> = {
  PerUnit: 'ĐV',
  PerSquareMeter: 'm²',
  PerLinearMeter: 'm dài',
  PerCubicMeter: 'm³',
};

const currency = new Intl.NumberFormat('vi-VN');

// Gap between the input and the list, and between the list and the window edge.
const DROPDOWN_GAP = 4;
const WINDOW_MARGIN = 8;
// The list never grows past this (the max-h-80 class), however many results there are.
const MAX_LIST_HEIGHT = 320;
// The full catalog gives the focus back to the box once its close animation has run (well under this); a focus of the
// box later than this after a pick is the user's own.
const PICK_FOCUS_BACK_MS = 1000;

interface Placement {
  above: boolean;
  /** Set only when the list does not fit on the side it opens to. */
  maxHeight?: number;
}

export function ProductTypeaheadCell({
  value,
  onChange,
  onSelect,
  placeholder,
  variant = 'default',
  inputId,
  nextFocusId,
  openOnFocus = true,
  allowQuickAdd = false,
}: Props) {
  const [open, setOpen] = useState(false);
  const [catalogOpen, setCatalogOpen] = useState(false);
  const [highlightedIndex, setHighlightedIndex] = useState(0);
  // An Enter that came before the results of the typed text; settled once they arrive.
  const [enterWaiting, setEnterWaiting] = useState(false);
  const [dropdownRect, setDropdownRect] = useState<DOMRect | null>(null);
  const [placement, setPlacement] = useState<Placement>({ above: false });
  const containerRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);
  const dropdownRef = useRef<HTMLDivElement>(null);
  const reactId = useId();
  const listboxId = `product-listbox-${reactId}`;

  const search = useProductSearch(value);
  const items = search.data ?? [];
  const isLoading = search.isLoading && value.trim().length >= 1;
  const isError = search.isError;
  // Pending: the results shown are not (yet) the results of the typed text.
  const pending = value.trim() !== (search.settledQuery ?? value).trim() || !!search.isFetching;

  // Every quick add branch (F4, waiting Enter, the add row) hangs on canAdd; without it the cell works as before.
  const canCreate = useAuthStore((s) => s.hasPermission('products.create'));
  const canAdd = !!allowQuickAdd && canCreate;
  const quickAdd = useDialogOpening();
  // The product a save in the quick add dialog created; picked once the dialog has closed.
  const createdRef = useRef<ProductSuggestion | null>(null);

  const rows: ListRow[] = items.map((item) => ({ kind: 'result', item }));
  if (canAdd && !isLoading && !isError && !pending && value.trim() !== '') rows.push({ kind: 'addNew' });
  const addNewId = `${listboxId}-add-new`;
  const rowId = (row: ListRow) => (row.kind === 'result' ? `product-option-${row.item.id}` : addNewId);

  const syncDropdownRect = useCallback(() => {
    setDropdownRect(inputRef.current?.getBoundingClientRect() ?? null);
  }, []);

  useEffect(() => {
    function onDocClick(e: MouseEvent) {
      const target = e.target as Node;
      if (containerRef.current?.contains(target)) return;
      if (dropdownRef.current?.contains(target)) return;
      setOpen(false);
      setEnterWaiting(false);
    }
    document.addEventListener('mousedown', onDocClick);
    return () => document.removeEventListener('mousedown', onDocClick);
  }, []);

  useEffect(() => {
    if (!open) return;
    syncDropdownRect();
    window.addEventListener('resize', syncDropdownRect);
    window.addEventListener('scroll', syncDropdownRect, true);
    return () => {
      window.removeEventListener('resize', syncDropdownRect);
      window.removeEventListener('scroll', syncDropdownRect, true);
    };
  }, [open, syncDropdownRect]);

  const showDropdown = open && dropdownRect && value.trim().length >= 1;

  // Open below the input; flip above it when the list does not fit below and there is more room above, and cap
  // the height to the side it opens to. Unchanged (below, CSS max-h) when it fits.
  useLayoutEffect(() => {
    if (!showDropdown || !dropdownRect || !dropdownRef.current) return;
    const natural = Math.min(dropdownRef.current.scrollHeight, MAX_LIST_HEIGHT);
    const room = DROPDOWN_GAP + WINDOW_MARGIN;
    const below = window.innerHeight - dropdownRect.bottom - room;
    const above = dropdownRect.top - room;
    const flip = natural > below && above > below;
    const available = flip ? above : below;
    const next: Placement = { above: flip, maxHeight: natural > available ? Math.max(available, 0) : undefined };
    setPlacement((prev) => (prev.above === next.above && prev.maxHeight === next.maxHeight ? prev : next));
  }, [showDropdown, dropdownRect, rows.length, isLoading, isError]);

  useEffect(() => {
    if (highlightedIndex >= rows.length) setHighlightedIndex(0);
  }, [rows.length, highlightedIndex]);

  // Keep the highlighted option inside the scrolling list.
  const highlightedRow = rows[highlightedIndex];
  const highlightedId = highlightedRow ? rowId(highlightedRow) : undefined;
  useEffect(() => {
    if (!open || !highlightedId) return;
    const list = dropdownRef.current;
    const option = document.getElementById(highlightedId);
    if (!list || !option) return;
    // Scroll by hand: scrollIntoView would leave the option under the sticky table header.
    const header = list.querySelector('thead')?.getBoundingClientRect().height ?? 0;
    const box = list.getBoundingClientRect();
    const row = option.getBoundingClientRect();
    // The header sticks inside the list border, so measure from the padding box.
    const top = box.top + list.clientTop + header;
    const bottom = box.bottom - list.clientTop;
    if (row.top < top) list.scrollTop -= top - row.top;
    else if (row.bottom > bottom) list.scrollTop += row.bottom - bottom;
  }, [open, highlightedId]);

  // When the last pick was made (performance.now()), until the box loses or gets the focus. The full catalog gives the
  // focus back to the box as it closes: the list the pick has just closed must not open again on that focus, or Tab
  // and Enter would act on it (Enter picks its highlighted row). The focus may go somewhere else while the catalog
  // closes; a focus of the box that comes later than that is the user's own. A pick that moves on to the next cell
  // (nextFocusId) leaves the box to the user's next focus.
  const pickedAtRef = useRef<number | null>(null);

  const commit = useCallback(
    (s: ProductSuggestion) => {
      onSelect(s);
      setOpen(false);
      if (nextFocusId) {
        setTimeout(() => document.getElementById(nextFocusId)?.focus(), 0);
      } else {
        pickedAtRef.current = performance.now();
      }
    },
    [onSelect, nextFocusId],
  );

  // The add row and F4: the dialog starts from the typed text.
  function openQuickAdd() {
    setOpen(false);
    setEnterWaiting(false);
    quickAdd.show(value.trim());
  }

  // The results of a waiting Enter arrived: exact code, else the first result, else just show the add row.
  function settleWaitingEnter() {
    setEnterWaiting(false);
    // A failed search leaves stale data behind: commit nothing from it.
    if (isError) return;
    const upper = value.trim().toUpperCase();
    const pick = items.find((s) => s.code.toUpperCase() === upper) ?? items[0];
    if (pick) {
      commit(pick);
    } else {
      setOpen(true);
      setHighlightedIndex(0);
    }
  }
  const settleWaitingEnterRef = useRef(settleWaitingEnter);
  useEffect(() => {
    settleWaitingEnterRef.current = settleWaitingEnter;
  });
  useEffect(() => {
    if (enterWaiting && !pending) settleWaitingEnterRef.current();
  }, [enterWaiting, pending, search.data]);

  function handleKeyDown(e: React.KeyboardEvent<HTMLInputElement>) {
    // The Enter that commits a Telex/VNI composition must not pick or add on half-composed text.
    if (e.nativeEvent.isComposing) return;
    if (e.key === 'F4') {
      if (!canAdd) return;
      e.preventDefault();
      openQuickAdd();
      return;
    }
    // Enter before the results of the typed text: wait for them rather than act on older ones.
    if (e.key === 'Enter' && open && canAdd && value.trim() !== '' && pending) {
      e.preventDefault();
      setEnterWaiting(true);
      return;
    }
    // Tab and the arrows only ever walk the results; the add row is reached with the arrows (or F4), never with Tab.
    if (!open || rows.length === 0 || (e.key === 'Tab' && items.length === 0)) {
      if (e.key === 'Escape' && open) {
        e.preventDefault();
        setOpen(false);
        setEnterWaiting(false);
      }
      return;
    }
    switch (e.key) {
      case 'ArrowDown':
        e.preventDefault();
        setHighlightedIndex((i) => (i + 1) % rows.length);
        break;
      case 'ArrowUp':
        e.preventDefault();
        setHighlightedIndex((i) => (i - 1 + rows.length) % rows.length);
        break;
      case 'Tab':
        e.preventDefault();
        setHighlightedIndex((i) =>
          // Clamp first: from the add row (index = items.length) Tab goes to the first result.
          e.shiftKey ? (i - 1 + items.length) % items.length : (Math.min(i, items.length - 1) + 1) % items.length,
        );
        break;
      case 'Enter': {
        e.preventDefault();
        const row = rows[highlightedIndex];
        if (!row) break;
        if (row.kind === 'result') commit(row.item);
        else openQuickAdd();
        break;
      }
      case 'Escape':
        e.preventDefault();
        setOpen(false);
        setEnterWaiting(false);
        break;
    }
  }

  const activeOptionId = open && highlightedId ? highlightedId : undefined;

  const triggerProps = {
    ref: inputRef,
    id: inputId,
    value,
    onFocus: () => {
      syncDropdownRect();
      const pickedAt = pickedAtRef.current;
      pickedAtRef.current = null;
      const focusBackFromPick = pickedAt !== null && performance.now() - pickedAt < PICK_FOCUS_BACK_MS;
      if (openOnFocus && !focusBackFromPick) setOpen(true);
    },
    onBlur: () => {
      pickedAtRef.current = null;
      // A waiting Enter belongs to the cell the user has just left.
      setEnterWaiting(false);
    },
    onChange: (e: React.ChangeEvent<HTMLInputElement>) => {
      onChange(e.target.value);
      setEnterWaiting(false);
      syncDropdownRect();
      setOpen(true);
    },
    onKeyDown: handleKeyDown,
    placeholder: placeholder ?? 'Mã / tên hàng',
    role: 'combobox' as const,
    'aria-expanded': open,
    'aria-controls': listboxId,
    'aria-autocomplete': 'list' as const,
    'aria-activedescendant': activeOptionId,
    autoComplete: 'off',
  };

  const trigger =
    variant === 'cell' ? (
      <input className="cell-input" {...triggerProps} />
    ) : (
      <Input {...triggerProps} />
    );

  const dropdown = showDropdown
    ? createPortal(
        <div
          ref={dropdownRef}
          className="z-50 min-w-[min(760px,calc(100vw-80px))] max-w-[calc(100vw-40px)] max-h-80 overflow-auto rounded-md border bg-popover text-popover-foreground shadow-md"
          style={{
            position: 'fixed',
            left: dropdownRect.left,
            ...(placement.above
              ? { bottom: window.innerHeight - dropdownRect.top + DROPDOWN_GAP }
              : { top: dropdownRect.bottom + DROPDOWN_GAP }),
            ...(placement.maxHeight != null && { maxHeight: placement.maxHeight }),
            // A modal dialog sets pointer-events: none on <body>, where this list is portaled.
            pointerEvents: 'auto',
          }}
        >
          <div className="flex items-center justify-between border-b bg-muted/30 px-2 py-1.5 text-xs text-muted-foreground">
            <span>
              {isLoading
                ? 'Đang tìm kiếm...'
                : isError
                ? 'Lỗi tải danh sách'
                : `Tìm thấy ${items.length} sản phẩm`}
            </span>
            <span className="flex items-center gap-1.5">
              <kbd className="rounded border bg-background px-1 py-0.5 font-mono text-[10px]">Tab</kbd>
              duyệt
              <kbd className="rounded border bg-background px-1 py-0.5 font-mono text-[10px]">Enter</kbd>
              chọn
              <kbd className="rounded border bg-background px-1 py-0.5 font-mono text-[10px]">Esc</kbd>
              đóng
              {canAdd && (
                <>
                  <kbd className="rounded border bg-background px-1 py-0.5 font-mono text-[10px]">F4</kbd>
                  thêm mới
                </>
              )}
            </span>
          </div>
          {/* eslint-disable-next-line jsx-a11y/no-noninteractive-element-to-interactive-role -- WAI-ARIA combobox listbox pattern */}
          <table className="qldh-lookup-table" id={listboxId} role="listbox">
            <thead className="qldh-lookup-table-header">
              <tr>
                <th className="px-2 py-1">Mã</th>
                <th className="px-2 py-1">Tên</th>
                <th className="px-2 py-1">Loại</th>
                <th className="px-2 py-1">Quy cách</th>
                <th className="px-2 py-1 text-right">Giá bán</th>
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
                    Không thể tải danh sách. Vui lòng thử lại.
                  </td>
                </tr>
              )}
              {!isLoading && !isError && items.length === 0 && (
                <tr>
                  <td colSpan={5} className="px-2 py-3 text-center text-muted-foreground">
                    Không tìm thấy sản phẩm phù hợp
                  </td>
                </tr>
              )}
              {!isLoading &&
                !isError &&
                items.map((s, i) => {
                  const highlighted = i === highlightedIndex;
                  return (
                    <tr
                      key={s.id}
                      id={`product-option-${s.id}`}
                      role="option"
                      aria-selected={highlighted}
                      onMouseDown={(e) => {
                        e.preventDefault();
                        commit(s);
                      }}
                      onMouseEnter={() => setHighlightedIndex(i)}
                      className={cn(
                        'cursor-pointer border-t',
                        highlighted && 'qldh-lookup-row-highlight',
                      )}
                    >
                      <td className="px-2 py-1 font-mono">{s.code}</td>
                      <td className="px-2 py-1">{s.name}</td>
                      <td className="px-2 py-1">{PRICING_LABEL[s.pricingMode]}</td>
                      <td className="px-2 py-1 text-xs text-muted-foreground">{s.specification ?? ''}</td>
                      <td className="px-2 py-1 text-right tabular-nums">
                        {s.defaultPrice != null ? currency.format(s.defaultPrice) : ''}
                      </td>
                    </tr>
                  );
                })}
              {rows[rows.length - 1]?.kind === 'addNew' && (
                <tr
                  id={addNewId}
                  role="option"
                  aria-selected={highlightedIndex === rows.length - 1}
                  onMouseDown={(e) => {
                    e.preventDefault();
                    openQuickAdd();
                  }}
                  onMouseEnter={() => setHighlightedIndex(rows.length - 1)}
                  className={cn(
                    'cursor-pointer border-t',
                    highlightedIndex === rows.length - 1 && 'qldh-lookup-row-highlight',
                  )}
                >
                  <td colSpan={5} className="px-2 py-1.5">
                    <span className="flex items-center gap-2">
                      <AppIcon name="add" size={14} />
                      <span>{`Thêm mới hàng hóa "${value.trim()}"`}</span>
                      <kbd className="ml-auto rounded border bg-background px-1 py-0.5 font-mono text-[10px]">
                        {items.length === 0 ? 'Enter / F4' : 'F4'}
                      </kbd>
                    </span>
                  </td>
                </tr>
              )}
            </tbody>
          </table>
          {/* Catalog browser button */}
          <div className="border-t">
            <button
              type="button"
              className="flex w-full items-center gap-2 px-3 py-2 text-sm text-muted-foreground hover:bg-muted/50 hover:text-foreground transition-colors"
              onMouseDown={(e) => {
                e.preventDefault();
                setOpen(false);
                setCatalogOpen(true);
              }}
            >
              <AppIcon name="listPicker" size={14} />
              Xem danh mục đầy đủ
            </button>
          </div>
        </div>,
        document.body,
      )
    : null;

  return (
    <div ref={containerRef} className="relative">
      {trigger}
      {dropdown}
      <ProductCatalogDialog
        open={catalogOpen}
        onOpenChange={setCatalogOpen}
        initialQuery={value}
        onSelect={(s) => {
          commit(s);
          setCatalogOpen(false);
        }}
      />
      <StopFormEvents>
        <ProductQuickAddDialog
          open={quickAdd.open}
          onOpenChange={quickAdd.onOpenChange}
          seed={quickAdd.seed}
          onCreated={(s) => {
            if (quickAdd.isCurrent()) createdRef.current = s;
          }}
          onCloseAutoFocus={(e) => {
            e.preventDefault();
            const created = createdRef.current;
            createdRef.current = null;
            if (created) {
              commit(created);
              // Without nextFocusId (opening stock) the focus stays in the cell, as after a pick from the list;
              // commit has set pickedAtRef, so the list does not reopen. With nextFocusId, commit moves on.
              if (!nextFocusId) inputRef.current?.focus();
              return;
            }
            // Cancel: back to the cell with the typed text, the list stays closed.
            pickedAtRef.current = performance.now();
            inputRef.current?.focus();
          }}
        />
      </StopFormEvents>
    </div>
  );
}
