import { useState } from 'react';
import { AppIcon } from '@/components/icons';
import { Input } from '@/components/ui/input';
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { useCustomers } from '@/features/customers/hooks';
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

interface Props {
  initialQuery: string;
  onSelect: (id: string) => void;
}

export function CustomerCatalogList({ initialQuery, onSelect }: Props) {
  const [query, setQuery] = useState(initialQuery);
  const [group, setGroup] = useState<CustomerGroup | undefined>(undefined);
  const [page, setPage] = useState(1);

  const debouncedQuery = useDebouncedValue(query, 250);

  const { data, isLoading } = useCustomers({
    page,
    pageSize: 20,
    search: debouncedQuery || undefined,
    group: group,
    status: 'Active',
  });

  const items = data?.items ?? [];
  const totalPages = data?.totalPages ?? 1;

  function handleGroupChange(value: string) {
    setGroup(value === 'all' ? undefined : (value as CustomerGroup));
    setPage(1);
  }

  return (
    <div className="flex h-full flex-col">
      {/* Search */}
      <div className="border-b p-3">
        <div className="relative">
          <AppIcon name="search" className="absolute left-2.5 top-2.5" />
          <Input
            className="pl-8"
            placeholder="Tìm mã, tên khách hàng..."
            value={query}
            onChange={(e) => { setQuery(e.target.value); setPage(1); }}
            autoFocus
          />
        </div>
      </div>

      {/* Group tabs */}
      <div className="border-b px-3 py-2 overflow-x-auto">
        <Tabs value={group ?? 'all'} onValueChange={handleGroupChange}>
          <TabsList className="h-auto flex-wrap gap-1 bg-transparent p-0">
            <TabsTrigger value="all" className="h-7 px-2 text-xs">Tất cả</TabsTrigger>
            {ALL_GROUPS.map((g) => (
              <TabsTrigger key={g} value={g} className="h-7 px-2 text-xs">
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
              <th className="px-3 py-2 text-left font-medium text-muted-foreground">Mã KH</th>
              <th className="px-3 py-2 text-left font-medium text-muted-foreground">Tên</th>
              <th className="px-3 py-2 text-left font-medium text-muted-foreground">MST</th>
              <th className="px-3 py-2 text-left font-medium text-muted-foreground">Người LH</th>
              <th className="px-3 py-2 text-left font-medium text-muted-foreground">SĐT</th>
              <th className="px-3 py-2 text-left font-medium text-muted-foreground">Nhóm</th>
            </tr>
          </thead>
          <tbody>
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
                  Không tìm thấy khách hàng phù hợp
                </td>
              </tr>
            )}
            {!isLoading &&
              items.map((item) => (
                <tr
                  key={item.id}
                  className={cn('cursor-pointer border-b transition-colors hover:bg-muted/50')}
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
          {data && <span className="ml-2 text-xs">({data.totalItems} khách hàng)</span>}
        </span>
        <div className="flex gap-1">
          <button
            type="button"
            className="rounded border px-2 py-1 text-xs disabled:opacity-40 hover:bg-muted"
            disabled={page <= 1}
            onClick={() => setPage((p) => p - 1)}
          >
            ← Trước
          </button>
          <button
            type="button"
            className="rounded border px-2 py-1 text-xs disabled:opacity-40 hover:bg-muted"
            disabled={page >= totalPages}
            onClick={() => setPage((p) => p + 1)}
          >
            Sau →
          </button>
        </div>
      </div>
    </div>
  );
}
