import { useEffect, useRef, useState, type RefObject } from 'react';
import { AppIcon } from '@/components/icons';
import { Input } from '@/components/ui/input';
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { useCustomers } from '@/features/customers/hooks';
import { useSuppliers } from '@/features/suppliers/hooks';
import { useDebouncedValue } from '@/lib/use-debounced-value';
import { cn } from '@/lib/utils';
import type { CustomerGroup } from '@/features/customers/types';

const GROUP_LABELS: Record<CustomerGroup, string> = {
  Company: 'Công ty',
  Agent: 'Đại lý',
  Retail: 'Khách lẻ',
  Project: 'Công trình',
};

const ALL_GROUPS: CustomerGroup[] = ['Company', 'Agent', 'Retail', 'Project'];

export interface PartnerCatalogListProps {
  role: 'customer' | 'supplier';
  query: string;
  onQueryChange: (q: string) => void;
  onSelect: (id: string) => void;
  /** Lets the dialog's role tabs return the focus to the search box; the list owns one when omitted. */
  searchRef?: RefObject<HTMLInputElement>;
}

// Controlled by the dialog, which renders it with key={role}: the role never changes within an instance, so the
// hook below is always the same one.
export function PartnerCatalogList({ role, query, onQueryChange, onSelect, searchRef: sharedSearchRef }: PartnerCatalogListProps) {
  const [group, setGroup] = useState<CustomerGroup | undefined>(undefined);
  const [page, setPage] = useState(1);
  const [highlighted, setHighlighted] = useState(0);
  const ownSearchRef = useRef<HTMLInputElement>(null);
  const searchRef = sharedSearchRef ?? ownSearchRef;
  const bodyRef = useRef<HTMLTableSectionElement>(null);

  const debouncedQuery = useDebouncedValue(query, 250);

  const useList = role === 'customer' ? useCustomers : useSuppliers;
  const { data, isLoading, isPlaceholderData } = useList({
    page,
    pageSize: 20,
    search: debouncedQuery || undefined,
    group: group,
    status: 'Active',
  });

  const noun = role === 'customer' ? 'khách hàng' : 'nhà cung cấp';
  const items = data?.items ?? [];
  const totalPages = data?.totalPages ?? 1;
  const active = Math.min(highlighted, Math.max(items.length - 1, 0));

  // Not autoFocus: the dialog keeps recording what had the focus before it opened.
  useEffect(() => {
    searchRef.current?.focus();
  }, [searchRef]);

  useEffect(() => {
    bodyRef.current?.children[active]?.scrollIntoView?.({ block: 'nearest' });
  }, [active, data]);

  function handleQueryChange(value: string) {
    onQueryChange(value);
    setPage(1);
    setHighlighted(0);
  }

  function handleGroupChange(value: string) {
    setGroup(value === 'all' ? undefined : (value as CustomerGroup));
    setPage(1);
    setHighlighted(0);
  }

  function goToPage(next: number) {
    setPage(next);
    setHighlighted(0);
  }

  // A mouse click leaves the focus on the tab (the browser focuses it after Radix activated it on mousedown), so
  // the arrow keys and Enter would stop driving the list. The keyboard keeps the focus on the tab it moved to.
  function focusSearch() {
    searchRef.current?.focus();
  }

  function handleSearchKeyDown(e: React.KeyboardEvent<HTMLInputElement>) {
    if (e.key === 'ArrowDown') {
      e.preventDefault();
      setHighlighted(Math.min(active + 1, Math.max(items.length - 1, 0)));
    } else if (e.key === 'ArrowUp') {
      e.preventDefault();
      setHighlighted(Math.max(active - 1, 0));
    } else if (e.key === 'Enter') {
      e.preventDefault();
      // The rows on screen belong to an older query while the typed text is still waiting for its search.
      if (query !== debouncedQuery || isPlaceholderData) return;
      const item = items[active];
      if (item) onSelect(item.id);
    }
  }

  return (
    <div className="flex h-full flex-col">
      {/* Search */}
      <div className="border-b p-3">
        <div className="relative">
          <AppIcon name="search" className="absolute left-2.5 top-2.5" />
          <Input
            className="pl-8"
            placeholder={`Tìm mã, tên ${noun}...`}
            value={query}
            onChange={(e) => handleQueryChange(e.target.value)}
            onKeyDown={handleSearchKeyDown}
            ref={searchRef}
          />
        </div>
      </div>

      {/* Group tabs */}
      <div className="border-b px-3 py-2 overflow-x-auto">
        <Tabs value={group ?? 'all'} onValueChange={handleGroupChange}>
          <TabsList className="h-auto flex-wrap gap-1 bg-transparent p-0">
            <TabsTrigger value="all" className="h-7 px-2 text-xs" onMouseUp={focusSearch}>Tất cả</TabsTrigger>
            {ALL_GROUPS.map((g) => (
              <TabsTrigger key={g} value={g} className="h-7 px-2 text-xs" onMouseUp={focusSearch}>
                {GROUP_LABELS[g]}
              </TabsTrigger>
            ))}
          </TabsList>
        </Tabs>
      </div>

      {/* Table */}
      <div className="flex-1 overflow-auto">
        <table className="w-full text-sm">
          <thead className="sticky top-0 bg-muted/80 backdrop-blur-sm">
            <tr className="border-b">
              <th className="px-3 py-2 text-left font-medium text-muted-foreground">Mã</th>
              <th className="px-3 py-2 text-left font-medium text-muted-foreground">Tên</th>
              <th className="px-3 py-2 text-left font-medium text-muted-foreground">MST</th>
              <th className="px-3 py-2 text-left font-medium text-muted-foreground">Người LH</th>
              <th className="px-3 py-2 text-left font-medium text-muted-foreground">SĐT</th>
              <th className="px-3 py-2 text-left font-medium text-muted-foreground">Nhóm</th>
            </tr>
          </thead>
          <tbody ref={bodyRef}>
            {isLoading && (
              <>
                {Array.from({ length: 8 }).map((_, i) => (
                  <tr key={i} className="border-b animate-pulse">
                    <td colSpan={6} className="px-3 py-2">
                      <div className="h-4 rounded bg-muted" />
                    </td>
                  </tr>
                ))}
              </>
            )}
            {!isLoading && items.length === 0 && (
              <tr>
                <td colSpan={6} className="px-3 py-8 text-center text-muted-foreground">
                  Không tìm thấy {noun} phù hợp
                </td>
              </tr>
            )}
            {!isLoading &&
              items.map((item, i) => (
                <tr
                  key={item.id}
                  aria-selected={i === active}
                  className={cn(
                    'cursor-pointer border-b transition-colors hover:bg-muted/50',
                    i === active && 'qldh-lookup-row-highlight',
                  )}
                  onClick={() => onSelect(item.id)}
                >
                  <td className="px-3 py-2 font-mono text-xs">{item.code}</td>
                  <td className="px-3 py-2">{item.name}</td>
                  <td className="px-3 py-2 text-muted-foreground">{item.taxCode ?? '—'}</td>
                  <td className="px-3 py-2 text-muted-foreground">{item.contactPerson ?? '—'}</td>
                  <td className="px-3 py-2 text-muted-foreground">{item.phoneNumber ?? '—'}</td>
                  <td className="px-3 py-2 text-muted-foreground">{GROUP_LABELS[item.group]}</td>
                </tr>
              ))}
          </tbody>
        </table>
      </div>

      {/* Pagination */}
      <div className="flex items-center justify-between border-t px-3 py-2 text-sm">
        <span className="text-muted-foreground">
          Trang {page} / {totalPages}
          {data && <span className="ml-2 text-xs">({data.totalItems} {noun})</span>}
        </span>
        <div className="flex gap-1">
          <button
            type="button"
            className="inline-flex items-center gap-1 rounded border px-2 py-1 text-xs disabled:opacity-40 hover:bg-muted"
            disabled={page <= 1}
            onClick={() => goToPage(page - 1)}
          >
            <AppIcon name="chevronLeft" size={14} /> Trước
          </button>
          <button
            type="button"
            className="inline-flex items-center gap-1 rounded border px-2 py-1 text-xs disabled:opacity-40 hover:bg-muted"
            disabled={page >= totalPages}
            onClick={() => goToPage(page + 1)}
          >
            Sau <AppIcon name="chevronRight" size={14} />
          </button>
        </div>
      </div>
    </div>
  );
}
