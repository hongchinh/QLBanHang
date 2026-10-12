import type { DebtSide } from '@/features/cash-common/types';
import { useDebtPartnerSearch } from '@/features/debt/hooks';
import type { DebtPartnerSearchItem } from '@/features/debt/types';
import { SearchCombobox } from '@/components/search-combobox/search-combobox';

export interface DebtPartnerPickerProps {
  id: string;
  // No side: any role.
  side?: DebtSide;
  // Only partners that are both customer and supplier (debt offsets).
  bothRoles?: boolean;
  selectedLabel: string;
  onSelect: (partner: DebtPartnerSearchItem) => void;
  // Shows a clear button while a partner is selected (list filters).
  onClear?: () => void;
  placeholder?: string;
}

// Partners from GET /debt/partners; an empty keyword lists the first ones on focus.
export function DebtPartnerPicker({
  id,
  side,
  bothRoles = false,
  selectedLabel,
  onSelect,
  onClear,
  placeholder = 'Mã hoặc tên đối tượng',
}: DebtPartnerPickerProps) {
  // Matching and offsets settle existing debt, which an inactive partner may still have.
  const useSearch = (keyword: string) => useDebtPartnerSearch(side, keyword, { bothRoles, includeInactive: true });
  return (
    <SearchCombobox<DebtPartnerSearchItem>
      id={id}
      ariaLabel="Đối tượng"
      placeholder={placeholder}
      selectedLabel={selectedLabel}
      title={selectedLabel || undefined}
      useSearch={useSearch}
      getKey={(p) => p.id}
      renderItem={(p) => (
        <>
          <span className="font-medium">{p.code}</span>
          <span className="truncate text-muted-foreground">{p.name}</span>
        </>
      )}
      onSelect={onSelect}
      onClear={onClear}
    />
  );
}
