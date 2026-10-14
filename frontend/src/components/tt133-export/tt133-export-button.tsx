import { FileSpreadsheet, FileText, Loader2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { CompanyProfileHint } from './company-profile-hint';
import { useTt133Download } from '@/features/tt133-export/use-tt133-download';
import type { ExportFormat, Tt133ExportForm } from '@/features/tt133-export/types';

interface Props {
  forms: Tt133ExportForm[];
  // Pages that pick targets first take over the click; without it the button downloads the form itself.
  onSelect?: (form: Tt133ExportForm, format: ExportFormat) => void;
  disabled?: boolean;
}

const FORMATS: { format: ExportFormat; label: string }[] = [
  { format: 'xlsx', label: 'Excel' },
  { format: 'pdf', label: 'PDF' },
];

// "Xuất theo mẫu TT133": one menu entry per format, prefixed by the form code when there are several forms.
export function Tt133ExportButton({ forms, onSelect, disabled }: Props) {
  const { download, isPending } = useTt133Download();
  const choose = (form: Tt133ExportForm, format: ExportFormat) => {
    if (onSelect) onSelect(form, format);
    else void download(form, format);
  };

  return (
    <div className="flex flex-wrap items-center gap-2">
      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <Button variant="outline" disabled={disabled || forms.length === 0 || isPending}>
            {isPending ? (
              <Loader2 className="h-4 w-4 animate-spin" />
            ) : (
              <FileSpreadsheet className="h-4 w-4 text-emerald-700" />
            )}
            Xuất theo mẫu TT133
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end">
          {forms.flatMap((form) =>
            FORMATS.map(({ format, label }) => (
              <DropdownMenuItem key={`${form.code}-${format}`} onSelect={() => choose(form, format)}>
                {format === 'xlsx' ? (
                  <FileSpreadsheet className="mr-2 h-4 w-4 text-emerald-700" />
                ) : (
                  <FileText className="mr-2 h-4 w-4 text-indigo-600" />
                )}
                {forms.length > 1 ? `${form.code} · ${label}` : label}
              </DropdownMenuItem>
            )),
          )}
        </DropdownMenuContent>
      </DropdownMenu>
      <CompanyProfileHint />
    </div>
  );
}
