import { useCallback, useEffect, useId, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { LayoutList } from 'lucide-react';
import { Input } from '@/components/ui/input';
import { cn } from '@/lib/utils';
import { useProductSearch } from '@/features/products/hooks';
import type { ProductSuggestion } from '@/features/products/types';
import { ProductCatalogDialog } from '@/features/products/components/product-catalog-dialog';

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
}

const PRICING_LABEL: Record<ProductSuggestion['pricingMode'], string> = {
  PerUnit: 'ĐV',
  PerSquareMeter: 'm²',
  PerLinearMeter: 'm dài',
  PerCubicMeter: 'm³',
};

const currency = new Intl.NumberFormat('vi-VN');

export function ProductTypeaheadCell({
  value,
  onChange,
  onSelect,
  placeholder,
  variant = 'default',
  inputId,
  nextFocusId,
  openOnFocus = true,
}: Props) {
  const [open, setOpen] = useState(false);
  const [catalogOpen, setCatalogOpen] = useState(false);
  const [highlightedIndex, setHighlightedIndex] = useState(0);
  const [dropdownRect, setDropdownRect] = useState<DOMRect | null>(null);
  const containerRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);
  const dropdownRef = useRef<HTMLDivElement>(null);
  const reactId = useId();
  const listboxId = `product-listbox-${reactId}`;

  const search = useProductSearch(value);
  const items = search.data ?? [];
  const isLoading = search.isLoading && value.trim().length >= 1;
  const isError = search.isError;

  const syncDropdownRect = useCallback(() => {
    setDropdownRect(inputRef.current?.getBoundingClientRect() ?? null);
  }, []);

  useEffect(() => {
    function onDocClick(e: MouseEvent) {
      const target = e.target as Node;
      if (containerRef.current?.contains(target)) return;
      if (dropdownRef.current?.contains(target)) return;
      setOpen(false);
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

  useEffect(() => {
    if (highlightedIndex >= items.length) setHighlightedIndex(0);
  }, [items.length, highlightedIndex]);

  const commit = useCallback(
    (s: ProductSuggestion) => {
      onSelect(s);
      setOpen(false);
      if (nextFocusId) {
        setTimeout(() => document.getElementById(nextFocusId)?.focus(), 0);
      }
    },
    [onSelect, nextFocusId],
  );

  function handleKeyDown(e: React.KeyboardEvent<HTMLInputElement>) {
    if (!open || items.length === 0) {
      if (e.key === 'Escape' && open) {
        e.preventDefault();
        setOpen(false);
      }
      return;
    }
    switch (e.key) {
      case 'ArrowDown':
        e.preventDefault();
        setHighlightedIndex((i) => (i + 1) % items.length);
        break;
      case 'ArrowUp':
        e.preventDefault();
        setHighlightedIndex((i) => (i - 1 + items.length) % items.length);
        break;
      case 'Tab':
        e.preventDefault();
        setHighlightedIndex((i) =>
          e.shiftKey ? (i - 1 + items.length) % items.length : (i + 1) % items.length,
        );
        break;
      case 'Enter':
        e.preventDefault();
        commit(items[highlightedIndex]);
        break;
      case 'Escape':
        e.preventDefault();
        setOpen(false);
        break;
    }
  }

  const activeOptionId =
    open && items[highlightedIndex] ? `product-option-${items[highlightedIndex].id}` : undefined;

  const triggerProps = {
    ref: inputRef,
    id: inputId,
    value,
    onFocus: () => {
      syncDropdownRect();
      if (openOnFocus) setOpen(true);
    },
    onChange: (e: React.ChangeEvent<HTMLInputElement>) => {
      onChange(e.target.value);
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

  const showDropdown = open && dropdownRect && value.trim().length >= 1;

  const dropdown = showDropdown
    ? createPortal(
        <div
          ref={dropdownRef}
          className="z-50 min-w-[min(760px,calc(100vw-80px))] max-w-[calc(100vw-40px)] max-h-80 overflow-auto rounded-md border bg-popover text-popover-foreground shadow-md"
          style={{
            position: 'fixed',
            left: dropdownRect.left,
            top: dropdownRect.bottom + 4,
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
              <LayoutList className="h-3.5 w-3.5" aria-hidden="true" />
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
    </div>
  );
}
