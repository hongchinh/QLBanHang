import type { DebtSourceType } from '@/features/cash-common/types';
import type { CashBookSourceType } from '@/features/cash-reports/types';
import { DocumentCodeLink } from '@/pages/cash-reports/document-code-link';

interface BranchDocumentLinkProps {
  sourceType: DebtSourceType | CashBookSourceType;
  sourceId: string;
  code: string;
  // The branch of the document.
  branchCode: string;
  // The working branch: the document screens open only its documents.
  workingBranchCode: string | undefined;
}

// A document code that links to its screen only when the document belongs to the working branch: the screen
// rejects a document of another branch (403). Without access to the screen it is plain text as well.
export function BranchDocumentLink({ sourceType, sourceId, code, branchCode, workingBranchCode }: BranchDocumentLinkProps) {
  if (workingBranchCode !== undefined && branchCode === workingBranchCode) {
    return <DocumentCodeLink sourceType={sourceType} sourceId={sourceId} code={code} />;
  }
  return <span title={`Chứng từ của chi nhánh ${branchCode}`}>{code}</span>;
}
