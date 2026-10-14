import {
  useEffect,
  useId,
  useRef,
  useState,
  type KeyboardEvent,
  type ReactNode,
} from "react";
import { AppIcon } from "@/components/icons";
import { Input } from "@/components/ui/input";
import { useDebouncedValue } from "@/lib/use-debounced-value";
import { cn } from "@/lib/utils";

export interface SearchComboboxProps<T> {
  id: string;
  ariaLabel: string;
  placeholder: string;
  // Shown in the input while the user is not typing.
  selectedLabel: string;
  // A hook: called on every render with the keyword debounced by 250 ms ('' lists the first results on focus).
  useSearch: (keyword: string) => {
    data?: T[];
    isLoading: boolean;
    isError: boolean;
  };
  getKey: (item: T) => string;
  renderItem: (item: T) => ReactNode;
  onSelect: (item: T) => void;
  // Shows a clear button while a label is selected.
  onClear?: () => void;
  // Native tooltip of the input.
  title?: string;
  invalid?: boolean;
  // Row texts and clear button label; default to the partner picker wording.
  emptyText?: string;
  errorText?: string;
  clearLabel?: string;
}

// Behind DebtPartnerPicker and the ledger PartnerPicker. CashPartnerFilter and CustomerPicker intentionally do NOT
// use it: they open only after typing / clear the selection on type / differ in Escape and error handling.
// Search-as-you-type picker: opens on focus, arrows move, Enter or a click selects, Escape or a click outside
// closes and restores the selected label.
export function SearchCombobox<T>({
  id,
  ariaLabel,
  placeholder,
  selectedLabel,
  useSearch,
  getKey,
  renderItem,
  onSelect,
  onClear,
  title,
  invalid,
  emptyText = "Không tìm thấy đối tượng phù hợp",
  errorText = "Không thể tải danh sách đối tượng.",
  clearLabel = "Bỏ chọn đối tượng",
}: SearchComboboxProps<T>) {
  // null: the input shows the selected label.
  const [keyword, setKeyword] = useState<string | null>(null);
  const [isOpen, setIsOpen] = useState(false);
  const [highlighted, setHighlighted] = useState(0);
  const debouncedKeyword = useDebouncedValue(keyword ?? "", 250);
  const { data, isLoading, isError } = useSearch(debouncedKeyword);
  const results = data ?? [];
  const containerRef = useRef<HTMLDivElement>(null);
  const listboxId = `${id}-listbox-${useId()}`;
  const canClear = !!onClear && !!selectedLabel && keyword === null;
  const hasActiveOption =
    isOpen && !isLoading && !isError && !!results[highlighted];

  useEffect(() => setHighlighted(0), [data]);

  useEffect(() => {
    function onDocMouseDown(e: MouseEvent) {
      if (
        containerRef.current &&
        !containerRef.current.contains(e.target as Node)
      ) {
        setIsOpen(false);
        setKeyword(null);
      }
    }
    document.addEventListener("mousedown", onDocMouseDown);
    return () => document.removeEventListener("mousedown", onDocMouseDown);
  }, []);

  function pick(item: T) {
    onSelect(item);
    setKeyword(null);
    setIsOpen(false);
  }

  function onKeyDown(e: KeyboardEvent<HTMLInputElement>) {
    if (e.key === "Escape") {
      setIsOpen(false);
      setKeyword(null);
      return;
    }
    if (!isOpen || results.length === 0) return;
    if (e.key === "ArrowDown" || e.key === "ArrowUp") {
      e.preventDefault();
      const step = e.key === "ArrowDown" ? 1 : -1;
      setHighlighted((i) => (i + step + results.length) % results.length);
    } else if (e.key === "Enter") {
      e.preventDefault();
      const choice = results[highlighted];
      if (choice) pick(choice);
    }
  }

  return (
    <div className="relative" ref={containerRef}>
      <Input
        id={id}
        role="combobox"
        aria-label={ariaLabel}
        aria-expanded={isOpen}
        aria-controls={isOpen ? listboxId : undefined}
        aria-activedescendant={
          hasActiveOption ? `${listboxId}-${highlighted}` : undefined
        }
        aria-invalid={invalid || undefined}
        aria-autocomplete="list"
        autoComplete="off"
        placeholder={placeholder}
        value={keyword ?? selectedLabel}
        title={title}
        className={canClear ? "pr-8" : undefined}
        onFocus={() => setIsOpen(true)}
        onKeyDown={onKeyDown}
        onChange={(e) => {
          setKeyword(e.target.value);
          setIsOpen(true);
        }}
      />
      {canClear && (
        <button
          type="button"
          aria-label={clearLabel}
          tabIndex={-1}
          onClick={() => onClear()}
          className="absolute right-2 top-1/2 -translate-y-1/2 rounded-sm p-0.5 text-muted-foreground hover:bg-accent"
        >
          <AppIcon name="close" />
        </button>
      )}
      {isOpen && (
        <ul
          id={listboxId}
          role="listbox"
          aria-label={ariaLabel}
          className="absolute left-0 z-50 mt-1 max-h-80 min-w-full overflow-auto rounded-md border bg-popover py-1 text-sm text-popover-foreground shadow-md sm:w-96"
        >
          {isLoading && (
            <li
              role="presentation"
              className="px-2 py-2 text-center text-muted-foreground"
            >
              Đang tìm kiếm...
            </li>
          )}
          {!isLoading && isError && (
            <li
              role="presentation"
              className="px-2 py-2 text-center text-destructive"
            >
              {errorText}
            </li>
          )}
          {!isLoading && !isError && results.length === 0 && (
            <li
              role="presentation"
              className="px-2 py-2 text-center text-muted-foreground"
            >
              {emptyText}
            </li>
          )}
          {!isLoading &&
            !isError &&
            results.map((item, i) => (
              <li
                key={getKey(item)}
                id={`${listboxId}-${i}`}
                role="option"
                aria-selected={i === highlighted}
                onMouseDown={(e) => {
                  // Avoid the blur before the selection runs.
                  e.preventDefault();
                  pick(item);
                }}
                onMouseEnter={() => setHighlighted(i)}
                className={cn(
                  "flex cursor-pointer gap-2 px-2 py-1",
                  i === highlighted && "qldh-lookup-row-highlight",
                )}
              >
                {renderItem(item)}
              </li>
            ))}
        </ul>
      )}
    </div>
  );
}
