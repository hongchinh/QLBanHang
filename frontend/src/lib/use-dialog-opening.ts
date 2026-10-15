import { useRef, useState } from 'react';

// Open state of a dialog plus a token per opening. Closing retires the token, so the callbacks a dialog handed out
// during an earlier opening (a save or a detail load that finishes after Esc, even after reopening) become no-ops.
export function useDialogOpening() {
  const current = useRef(0);
  const [state, setState] = useState({ open: false, seed: '', token: 0 });
  const renderToken = state.token;
  return {
    open: state.open,
    seed: state.seed,
    show(seed: string) {
      current.current += 1;
      setState({ open: true, seed, token: current.current });
    },
    onOpenChange(open: boolean) {
      if (renderToken !== current.current) return;
      if (!open) current.current += 1;
      setState((s) => ({ ...s, open }));
    },
    // Only for the callbacks of this render; true while its opening is still the current one.
    isCurrent: () => renderToken === current.current,
  };
}
