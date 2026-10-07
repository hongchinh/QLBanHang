import { useProduct } from '@/features/products/hooks';
import { useAuthStore } from '@/stores/auth-store';
import { formatMoneyForDisplay } from '@/pages/quotations/utils/money-input';
import type { ProductSuggestion } from '@/features/products/types';
import { toProductSuggestion } from '@/features/products/to-product-suggestion';
import { Button } from '@/components/ui/button';

interface Props {
  productId: string | null;
  onSelect: (product: ProductSuggestion) => void;
}

const PRICING_MODE_LABEL: Record<string, string> = {
  PerUnit: 'Theo đơn vị',
  PerSquareMeter: 'Theo diện tích (m²)',
  PerLinearMeter: 'Theo dài (m)',
  PerCubicMeter: 'Theo thể tích (m³)',
};

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div>
      <p className="mb-1 text-xs font-medium text-muted-foreground uppercase tracking-wide">{title}</p>
      <div className="rounded-md border divide-y text-sm">{children}</div>
    </div>
  );
}

function Row({ label, value, mono }: { label: string; value?: string | null; mono?: boolean }) {
  return (
    <div className="flex justify-between px-3 py-1.5">
      <span className="text-muted-foreground">{label}</span>
      <span className={mono ? 'tabular-nums' : ''}>{value ?? '—'}</span>
    </div>
  );
}

export function ProductCatalogDetail({ productId, onSelect }: Props) {
  const { data: product, isLoading } = useProduct(productId ?? undefined);
  const canViewCost = useAuthStore((s) => s.hasPermission('quotations.view_cost'));

  function handleSelect() {
    if (!product) return;
    onSelect(toProductSuggestion(product));
  }

  if (productId === null) {
    return (
      <div className="flex h-full items-center justify-center text-muted-foreground text-sm">
        ← Chọn sản phẩm để xem chi tiết
      </div>
    );
  }

  if (isLoading) {
    return (
      <div className="flex h-full items-center justify-center text-muted-foreground text-sm">
        Đang tải...
      </div>
    );
  }

  if (!product) return null;

  return (
    <div className="flex h-full flex-col overflow-y-auto p-4 gap-4">
      {/* Header */}
      <div>
        <p className="text-xs text-muted-foreground font-mono">{product.code}</p>
        <h3 className="text-base font-semibold leading-snug">{product.name}</h3>
        {product.productGroupName && (
          <p className="text-xs text-muted-foreground">Nhóm: {product.productGroupName}</p>
        )}
      </div>

      {/* Thông tin cơ bản */}
      <Section title="Thông tin cơ bản">
        <Row label="ĐVT" value={product.unitName} />
        <Row label="Quy cách" value={product.specification} />
        <Row label="Loại tính giá" value={PRICING_MODE_LABEL[product.pricingMode]} />
      </Section>

      {/* Kích thước — chỉ hiện nếu có ít nhất 1 field */}
      {(product.length || product.width || product.thickness || product.density) && (
        <Section title="Kích thước">
          {(product.length || product.width || product.thickness) && (
            <Row
              label="D × R × Dày"
              value={`${product.length ?? '—'} × ${product.width ?? '—'} × ${product.thickness ?? '—'} mm`}
            />
          )}
          {product.density != null && (
            <Row label="Mật độ" value={`${product.density} kg/m³`} />
          )}
        </Section>
      )}

      {/* Giá & Thuế */}
      <Section title="Giá & Thuế">
        <Row label="Giá bán" value={formatMoneyForDisplay(product.defaultPrice)} mono />
        {canViewCost && product.costPrice != null && (
          <Row label="Giá nhập" value={formatMoneyForDisplay(product.costPrice)} mono />
        )}
        {product.defaultTaxRate != null && (
          <Row label="Thuế" value={`${product.defaultTaxRate}%`} />
        )}
      </Section>

      {/* Nút chọn */}
      <div className="mt-auto pt-2">
        <Button onClick={handleSelect} className="w-full">
          Chọn sản phẩm này
        </Button>
      </div>
    </div>
  );
}
