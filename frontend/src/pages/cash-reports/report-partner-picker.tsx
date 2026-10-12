import { useDebtPartnerSearch } from '@/features/debt/hooks';
import type { DebtPartnerSearchItem } from '@/features/debt/types';
import type { DebtSide } from '@/features/cash-common/types';
import { SearchCombobox } from '@/components/search-combobox/search-combobox';

interface ReportPartnerPickerProps {
  id: string;
  // Undefined searches every partner (used by the reconciliation).
  side?: DebtSide;
  selectedLabel: string;
  onSelect: (partner: DebtPartnerSearchItem) => void;
  // When given, a clear entry is shown that calls it.
  onClear?: () => void;
  clearLabel?: string;
}

// Partners of the side (GET /debt/partners); an empty keyword lists the first ones on focus.
export function ReportPartnerPicker({
  id,
  side,
  selectedLabel,
  onSelect,
  onClear,
  clearLabel = 'Tất cả đối tượng',
}: ReportPartnerPickerProps) {
  const useSearch = (keyword: string) => useDebtPartnerSearch(side, keyword, { includeInactive: true });
  return (
    <SearchCombobox<DebtPartnerSearchItem>
      id={id}
      ariaLabel="Đối tượng"
      placeholder="Mã hoặc tên đối tượng"
      selectedLabel={selectedLabel}
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
      clearLabel={clearLabel}
    />
  );
}
