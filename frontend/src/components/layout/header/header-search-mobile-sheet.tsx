import { useCallback, useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Search } from 'lucide-react';
import { Sheet, SheetContent, SheetTitle, SheetTrigger } from '@/components/ui/sheet';
import { useDebouncedValue } from '@/lib/use-debounced-value';
import { useGlobalSearch, SEARCH_MIN_LENGTH } from '@/features/search/hooks';
import { SearchResultsList } from './search-results-list';

export function HeaderSearchMobileSheet() {
  const navigate = useNavigate();
  const inputRef = useRef<HTMLInputElement>(null);
  const [open, setOpen] = useState(false);
  const [inputValue, setInputValue] = useState('');

  const debounced = useDebouncedValue(inputValue.trim(), 250);
  const { data, isFetching } = useGlobalSearch(debounced);

  const close = useCallback(() => {
    setOpen(false);
    setInputValue('');
  }, []);

  useEffect(() => {
    if (open) {
      // Focus the input after the sheet animation completes.
      const t = setTimeout(() => inputRef.current?.focus(), 50);
      return () => clearTimeout(t);
    }
  }, [open]);

  const handleSelectCustomer = (id: string) => {
    navigate(`/customers/${id}`);
    close();
  };

  const handleSelectSupplier = (id: string) => {
    navigate(`/suppliers/${id}`);
    close();
  };

  const handleSelectQuotation = (id: string) => {
    navigate(`/quotations/${id}`);
    close();
  };

  return (
    <Sheet open={open} onOpenChange={setOpen}>
      <SheetTrigger asChild>
        <button
          type="button"
          aria-label="Mở tìm kiếm"
          className="flex h-11 w-11 items-center justify-center rounded-md text-header-fg hover:bg-header-active md:hidden"
        >
          <Search className="h-5 w-5" />
        </button>
      </SheetTrigger>
      <SheetContent side="top" className="p-3">
        <SheetTitle className="sr-only">Tìm kiếm</SheetTitle>
        <div className="relative">
          <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
          <input
            ref={inputRef}
            type="search"
            value={inputValue}
            onChange={(e) => setInputValue(e.target.value)}
            aria-label="Tìm kiếm toàn cục"
            placeholder="Tìm khách hàng, mã báo giá…"
            className="h-11 w-full rounded-md border border-input bg-white pl-9 pr-3 text-sm text-foreground placeholder:text-muted-foreground focus:outline-none focus:ring-2 focus:ring-ring"
          />
        </div>
        <div className="mt-3">
          {inputValue.trim().length < SEARCH_MIN_LENGTH ? (
            <div className="px-2 py-4 text-sm text-muted-foreground">
              Nhập ít nhất {SEARCH_MIN_LENGTH} ký tự để tìm.
            </div>
          ) : (
            <SearchResultsList
              data={data}
              isLoading={isFetching}
              activeIndex={-1}
              onSelectCustomer={handleSelectCustomer}
              onSelectSupplier={handleSelectSupplier}
              onSelectQuotation={handleSelectQuotation}
            />
          )}
        </div>
      </SheetContent>
    </Sheet>
  );
}
