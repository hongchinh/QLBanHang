import { useEffect, useRef, useState, type ComponentProps } from 'react';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { cn } from '@/lib/utils';
import { focusById } from '@/lib/enter-focus';
import { usePartnerPermissions } from '@/features/customers/use-partner-permissions';
import type { PartnerSearchItem } from '@/features/stock-vouchers/types';
import type { PartnerType, StockDirection } from '@/features/stock-reasons/types';
import {
  PartnerAutocomplete,
  type PartnerNoun,
  type PartnerSearchHook,
  type PartnerSearchSource,
} from './partner-autocomplete';
import { PartnerCatalogDialog } from './partner-catalog-dialog';
import { PartnerQuickAddDialog } from './partner-quick-add-dialog';
import { StopFormEvents } from './stop-form-events';

export interface PartnerCodeNameFieldProps {
  // 'Đối tượng' | 'Khách hàng'
  label: string;
  required?: boolean;
  // 'customer' = quotation: customers only.
  scope: 'partner' | 'customer';
  // From the reason; default 'customer'. Picks the quick add's checked role and the catalog's start tab.
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
  preferredRole = 'customer',
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

  const { canCreateCustomer, canCreateSupplier, canViewCustomer, canViewSupplier } = usePartnerPermissions();
  const canAdd = !disabled && (isCustomer ? canCreateCustomer : canCreateCustomer || canCreateSupplier);
  const canOpenCatalog = !disabled && (isCustomer ? canViewCustomer : canViewCustomer || canViewSupplier);
  const [quickAdd, setQuickAdd] = useState({ open: false, seed: '' });
  const [catalog, setCatalog] = useState({ open: false, seed: '' });
  // A pick from a dialog keeps the focus on the name input; a cancel returns it to the code input.
  const skipRestoreFocus = useRef(false);
  const pickTimer = useRef<ReturnType<typeof setTimeout>>();
  useEffect(() => () => clearTimeout(pickTimer.current), []);
  // The quick add can finish after the user cancelled it while saving: that late result is not picked.
  const quickAddOpenRef = useRef(false);
  useEffect(() => {
    quickAddOpenRef.current = quickAdd.open;
  }, [quickAdd.open]);

  function pick(item: PartnerSearchItem) {
    skipRestoreFocus.current = true;
    onSelect(item);
    clearTimeout(pickTimer.current);
    pickTimer.current = setTimeout(() => focusById(nameInputId, 'end'), 0);
  }

  function handleCloseAutoFocus(e: Event) {
    e.preventDefault();
    if (skipRestoreFocus.current) {
      skipRestoreFocus.current = false;
      return;
    }
    focusById(codeInputId);
  }

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
          onAddNew={
            canAdd
              ? (seed) => {
                  skipRestoreFocus.current = false;
                  setQuickAdd({ open: true, seed });
                }
              : undefined
          }
          onOpenCatalog={
            canOpenCatalog
              ? (seed) => {
                  skipRestoreFocus.current = false;
                  setCatalog({ open: true, seed });
                }
              : undefined
          }
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
      {/* After the row: the CSS needs the name input to stay the last child of .partner-code-name. */}
      <StopFormEvents>
        <PartnerQuickAddDialog
          open={quickAdd.open}
          onOpenChange={(open) => setQuickAdd((q) => ({ ...q, open }))}
          initialName={quickAdd.seed}
          scope={scope}
          preferredRole={preferredRole}
          onCreated={(created) => {
            if (quickAddOpenRef.current) pick(created);
          }}
          onCloseAutoFocus={handleCloseAutoFocus}
        />
      </StopFormEvents>
      <StopFormEvents>
        <PartnerCatalogDialog
          open={catalog.open}
          onOpenChange={(open) => setCatalog((c) => ({ ...c, open }))}
          initialQuery={catalog.seed}
          scope={scope}
          preferredRole={preferredRole}
          onSelect={pick}
          onCloseAutoFocus={handleCloseAutoFocus}
        />
      </StopFormEvents>
    </>
  );
}
