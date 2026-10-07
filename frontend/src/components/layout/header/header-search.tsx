import { useCallback, useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Search } from 'lucide-react';
import { Popover, PopoverAnchor, PopoverContent } from '@/components/ui/popover';
import { useDebouncedValue } from '@/lib/use-debounced-value';
import { useGlobalSearch, SEARCH_MIN_LENGTH } from '@/features/search/hooks';
import type { CustomerSearchItem } from '@/features/search/api';
import { useAuthStore } from '@/stores/auth-store';
import { SearchResultsList } from './search-results-list';
import { flattenResultIndex, partnerResultPath, totalResultCount } from './search-results-helpers';

export function HeaderSearch() {
  const navigate = useNavigate();
  const hasPermission = useAuthStore((s) => s.hasPermission);
  const inputRef = useRef<HTMLInputElement>(null);
  const [inputValue, setInputValue] = useState('');
  const [open, setOpen] = useState(false);
  const [activeIndex, setActiveIndex] = useState(-1);

  const debounced = useDebouncedValue(inputValue.trim(), 250);
  const { data, isFetching } = useGlobalSearch(debounced);

  const close = useCallback(() => {
    setOpen(false);
    setActiveIndex(-1);
  }, []);

  const reset = useCallback(() => {
    setInputValue('');
    close();
  }, [close]);

  const handleSelectCustomer = useCallback(
    (item: CustomerSearchItem) => {
      navigate(partnerResultPath('customer', item, hasPermission));
      reset();
    },
    [navigate, reset, hasPermission],
  );

  const handleSelectSupplier = useCallback(
    (item: CustomerSearchItem) => {
      navigate(partnerResultPath('supplier', item, hasPermission));
      reset();
    },
    [navigate, reset, hasPermission],
  );

  const handleSelectQuotation = useCallback(
    (id: string) => {
      navigate(`/quotations/${id}`);
      reset();
    },
    [navigate, reset],
  );

  useEffect(() => {
    setActiveIndex(-1);
  }, [debounced]);

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') {
        e.preventDefault();
        inputRef.current?.focus();
        setOpen(true);
      }
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, []);

  const showPopover = open && inputValue.trim().length >= SEARCH_MIN_LENGTH;

  const handleKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Escape') {
      close();
      inputRef.current?.blur();
      return;
    }
    if (!showPopover) return;
    const total = totalResultCount(data);
    if (total === 0) return;
    if (e.key === 'ArrowDown') {
      e.preventDefault();
      setActiveIndex((i) => (i + 1) % total);
    } else if (e.key === 'ArrowUp') {
      e.preventDefault();
      setActiveIndex((i) => (i <= 0 ? total - 1 : i - 1));
    } else if (e.key === 'Enter') {
      const target = flattenResultIndex(data, activeIndex);
      if (target?.kind === 'customer') handleSelectCustomer(target.item);
      else if (target?.kind === 'supplier') handleSelectSupplier(target.item);
      else if (target?.kind === 'quotation') handleSelectQuotation(target.id);
    }
  };

  return (
    <Popover open={showPopover} onOpenChange={(o) => !o && close()}>
      <PopoverAnchor asChild>
        <div className="relative hidden h-10 w-full max-w-[320px] md:block md:max-w-[240px] lg:max-w-[320px]">
          <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
          <input
            ref={inputRef}
            type="search"
            value={inputValue}
            onChange={(e) => {
              setInputValue(e.target.value);
              setOpen(true);
            }}
            onFocus={() => {
              if (inputValue.trim().length >= SEARCH_MIN_LENGTH) setOpen(true);
            }}
            onKeyDown={handleKeyDown}
            aria-label="Tìm kiếm toàn cục"
            placeholder="Tìm khách hàng, mã báo giá…"
            className="h-10 w-full rounded-md border border-transparent bg-white pl-9 pr-3 text-sm text-foreground placeholder:text-muted-foreground focus:outline-none focus:ring-2 focus:ring-ring"
          />
        </div>
      </PopoverAnchor>
      <PopoverContent
        align="start"
        sideOffset={4}
        className="w-[min(420px,calc(100vw-2rem))] p-1"
        onOpenAutoFocus={(e) => e.preventDefault()}
      >
        <SearchResultsList
          data={data}
          isLoading={isFetching}
          activeIndex={activeIndex}
          onSelectCustomer={handleSelectCustomer}
          onSelectSupplier={handleSelectSupplier}
          onSelectQuotation={handleSelectQuotation}
        />
      </PopoverContent>
    </Popover>
  );
}
