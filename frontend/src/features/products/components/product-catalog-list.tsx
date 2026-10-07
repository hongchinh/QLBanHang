import { Search } from 'lucide-react';
import { Input } from '@/components/ui/input';
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { useProducts, useProductGroups } from '@/features/products/hooks';
import { useDebouncedValue } from '@/lib/use-debounced-value';
import { formatMoneyForDisplay } from '@/pages/quotations/utils/money-input';
import { cn } from '@/lib/utils';
import type { ProductSuggestion } from '@/features/products/types';
import { toProductSuggestion } from '@/features/products/to-product-suggestion';

interface Props {
  query: string;
  onQueryChange: (q: string) => void;
  groupId: string | undefined;
  onGroupChange: (id: string | undefined) => void;
  page: number;
  onPageChange: (p: number) => void;
  selectedId: string | null;
  onSelectId: (id: string) => void;
  onSelect: (product: ProductSuggestion) => void;
}

export function ProductCatalogList({
  query,
  onQueryChange,
  groupId,
  onGroupChange,
  page,
  onPageChange,
  selectedId,
  onSelectId,
  onSelect,
}: Props) {
  const debouncedQuery = useDebouncedValue(query, 300);
  const groups = useProductGroups();
  const products = useProducts({ search: debouncedQuery, productGroupId: groupId, page, pageSize: 20, status: 'Active' });
  const items = products.data?.items ?? [];
  const totalPages = products.data?.totalPages ?? 1;

  return (
    <div className="flex h-full flex-col border-r">
      {/* Search input */}
      <div className="border-b p-3">
        <div className="relative">
          <Search className="absolute left-2.5 top-2.5 h-4 w-4 text-muted-foreground" />
          <Input
            className="pl-8"
            placeholder="Tìm mã, tên hàng..."
            value={query}
            onChange={(e) => onQueryChange(e.target.value)}
            autoFocus
          />
        </div>
      </div>

      {/* Group tabs */}
      {groups.data && groups.data.length > 0 && (
        <div className="border-b px-3 py-2 overflow-x-auto">
          <Tabs
            value={groupId ?? 'all'}
            onValueChange={(v) => {
              onGroupChange(v === 'all' ? undefined : v);
              onPageChange(1);
            }}
          >
            <TabsList className="h-auto flex-wrap gap-1 bg-transparent p-0">
              <TabsTrigger value="all" className="h-7 px-2 text-xs">
                Tất cả
              </TabsTrigger>
              {groups.data.map((g) => (
                <TabsTrigger key={g.id} value={g.id} className="h-7 px-2 text-xs">
                  {g.name}
                </TabsTrigger>
              ))}
            </TabsList>
          </Tabs>
        </div>
      )}

      {/* Table */}
      <div className="flex-1 overflow-auto">
        <table className="w-full text-sm">
          <thead className="sticky top-0 bg-muted/80 backdrop-blur-sm">
            <tr className="border-b">
              <th className="px-3 py-2 text-left font-medium text-muted-foreground">Mã hàng</th>
              <th className="px-3 py-2 text-left font-medium text-muted-foreground">Tên hàng</th>
              <th className="px-3 py-2 text-left font-medium text-muted-foreground">ĐVT</th>
              <th className="px-3 py-2 text-left font-medium text-muted-foreground">Quy cách</th>
              <th className="px-3 py-2 text-right font-medium text-muted-foreground">Giá bán</th>
            </tr>
          </thead>
          <tbody>
            {products.isLoading && (
              <>
                {Array.from({ length: 8 }).map((_, i) => (
                  <tr key={i} className="border-b animate-pulse">
                    <td colSpan={5} className="px-3 py-2">
                      <div className="h-4 rounded bg-muted" />
                    </td>
                  </tr>
                ))}
              </>
            )}
            {!products.isLoading && items.length === 0 && (
              <tr>
                <td colSpan={5} className="px-3 py-8 text-center text-muted-foreground">
                  Không tìm thấy sản phẩm phù hợp
                </td>
              </tr>
            )}
            {!products.isLoading &&
              items.map((item) => (
                <tr
                  key={item.id}
                  className={cn(
                    'cursor-pointer border-b transition-colors hover:bg-muted/50',
                    selectedId === item.id && 'bg-primary/10 hover:bg-primary/15',
                  )}
                  onClick={() => onSelectId(item.id)}
                  onDoubleClick={() =>
                    onSelect(toProductSuggestion(item))
                  }
                >
                  <td className="px-3 py-2 font-mono text-xs">{item.code}</td>
                  <td className="px-3 py-2">{item.name}</td>
                  <td className="px-3 py-2 text-muted-foreground">{item.unitName ?? '—'}</td>
                  <td className="px-3 py-2 text-xs text-muted-foreground">{item.specification ?? '—'}</td>
                  <td className="px-3 py-2 text-right tabular-nums">
                    {formatMoneyForDisplay(item.defaultPrice)}
                  </td>
                </tr>
              ))}
          </tbody>
        </table>
      </div>

      {/* Pagination */}
      <div className="flex items-center justify-between border-t px-3 py-2 text-sm">
        <span className="text-muted-foreground">
          Trang {page} / {totalPages}
          {products.data && (
            <span className="ml-2 text-xs">({products.data.totalItems} sản phẩm)</span>
          )}
        </span>
        <div className="flex gap-1">
          <button
            className="rounded border px-2 py-1 text-xs disabled:opacity-40 hover:bg-muted"
            disabled={page <= 1}
            onClick={() => onPageChange(page - 1)}
          >
            ← Trước
          </button>
          <button
            className="rounded border px-2 py-1 text-xs disabled:opacity-40 hover:bg-muted"
            disabled={page >= totalPages}
            onClick={() => onPageChange(page + 1)}
          >
            Sau →
          </button>
        </div>
      </div>
    </div>
  );
}
