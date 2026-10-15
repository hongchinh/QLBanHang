// Wraps a whole <Dialog> rendered inside a voucher <form>. The dialog content is a portal, but React events
// still bubble to the form along the component tree (submit would save the voucher, Ctrl+S / Enter would act).
// It sits outside DialogContent so Radix's own handlers on the content (FocusScope Tab loop) run first and the
// close button is covered; Escape is a document capture listener and is not touched. display: contents keeps
// it out of the form's grid.
export function StopFormEvents({ children }: { children: React.ReactNode }) {
  const stop = (e: React.SyntheticEvent) => e.stopPropagation();
  return (
    // eslint-disable-next-line jsx-a11y/no-static-element-interactions -- only stops dialog events from reaching the voucher form.
    <div style={{ display: 'contents' }} onSubmit={stop} onKeyDown={stop}>{children}</div>
  );
}
