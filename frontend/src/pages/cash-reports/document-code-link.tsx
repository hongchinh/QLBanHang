import { Link } from 'react-router-dom';
import type { DebtSourceType } from '@/features/cash-common/types';
import type { CashBookSourceType } from '@/features/cash-reports/types';
import { useCanAccessRoute } from '@/hooks/useCanAccessRoute';
import { documentLink } from './document-link';

// A document code linking to its screen; plain text when the user may not open that screen.
export function DocumentCodeLink({
  sourceType,
  sourceId,
  code,
}: {
  sourceType: DebtSourceType | CashBookSourceType;
  sourceId: string;
  code: string;
}) {
  const canAccess = useCanAccessRoute();
  const to = documentLink(sourceType, sourceId);
  if (!canAccess(to)) return <>{code}</>;
  return (
    <Link to={to} className="text-primary hover:underline">
      {code}
    </Link>
  );
}
