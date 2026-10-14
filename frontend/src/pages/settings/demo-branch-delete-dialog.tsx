import type { Branch } from '@/features/branches/types';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';

interface DemoBranchDeleteDialogProps {
  branch: Branch | null;
  onClose: () => void;
  onDeleted: (id: string) => void;
}

// Stub: the deletion preview and the delete action are added in a later task.
export function DemoBranchDeleteDialog({ branch, onClose }: DemoBranchDeleteDialogProps) {
  return (
    <Dialog open={!!branch} onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Xóa chi nhánh demo?</DialogTitle>
        </DialogHeader>
        <DialogFooter>
          <Button variant="outline" onClick={onClose}>
            Hủy
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
