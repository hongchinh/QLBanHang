import type { ComponentProps } from 'react';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { cn } from '@/lib/utils';
import type { PartnerSearchItem } from '@/features/stock-vouchers/types';
import type { PartnerType, StockDirection } from '@/features/stock-reasons/types';
import {
  PartnerAutocomplete,
  type PartnerNoun,
  type PartnerSearchHook,
  type PartnerSearchSource,
} from './partner-autocomplete';

export interface PartnerCodeNameFieldProps {
  // 'Đối tượng' | 'Khách hàng'
  label: string;
  required?: boolean;
  // 'customer' = quotation: customers only.
  scope: 'partner' | 'customer';
  // From the reason; default 'customer'. Used by the quick add dialog (Phase 03).
  preferredRole?: 'customer' | 'supplier';
  codeInputId: string;
  nameInputId: string;
  // e.g. {...form.register('partnerName')}
  nameInputProps: ComponentProps<typeof Input>;
  namePlaceholder?: string;
  value: { id: string; code: string; name: string } | null;
  onSelect: (partner: PartnerSearchItem | null) => void;
  // Default stock search.
  type?: StockDirection;
  useSearch?: PartnerSearchHook;
  // 'None' locks the field.
  partnerType?: PartnerType;
  disabled?: boolean;
  error?: string;
  onUnpickedChange?: (hasUnpickedText: boolean) => void;
}

const CUSTOMER_CODE_PLACEHOLDER = 'Nhập mã / tên / MST / địa chỉ / SĐT...';

// Label, partner code combobox and partner name input in one inline row. The code combobox only picks active
// partners and never needs a reason; the name input belongs to the caller (it may differ from the partner's name).
export function PartnerCodeNameField({
  label,
  required,
  scope,
  codeInputId,
  nameInputId,
  nameInputProps,
  namePlaceholder,
  value,
  onSelect,
  type,
  useSearch,
  partnerType,
  disabled,
  error,
  onUnpickedChange,
}: PartnerCodeNameFieldProps) {
  const isCustomer = scope === 'customer';
  const noun: PartnerNoun = isCustomer ? 'khách hàng' : 'đối tượng';
  const source: PartnerSearchSource = useSearch ? { useSearch, type } : { type: type ?? 'In' };
  const errorId = `${codeInputId}-error`;
  const effectiveNamePlaceholder = namePlaceholder ?? (isCustomer ? 'Tên khách hàng' : 'Tên đối tượng');

  return (
    <>
      <Label htmlFor={codeInputId} className={cn('field-label', required && 'required')}>
        {label}
      </Label>
      <div className="partner-code-name">
        <PartnerAutocomplete
          {...source}
          compact
          requireReason={false}
          activeOnly
          noun={noun}
          inputId={codeInputId}
          nameInputId={nameInputId}
          placeholder={isCustomer ? CUSTOMER_CODE_PLACEHOLDER : undefined}
          describedBy={error ? errorId : undefined}
          partnerType={partnerType}
          value={value}
          onSelect={onSelect}
          disabled={disabled}
          onUnpickedChange={onUnpickedChange}
        />
        <Input
          id={nameInputId}
          className="h-7"
          placeholder={effectiveNamePlaceholder}
          aria-label={effectiveNamePlaceholder}
          {...nameInputProps}
        />
      </div>
      {error && (
        <p id={errorId} role="alert" className="field-message field-message-code text-destructive">
          {error}
        </p>
      )}
    </>
  );
}
