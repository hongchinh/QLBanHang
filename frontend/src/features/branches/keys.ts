export const branchKeys = {
  all: ['branches'] as const,
  lists: () => [...branchKeys.all, 'list'] as const,
  me: () => [...branchKeys.all, 'me'] as const,
  demoDeletion: (id: string | null) => [...branchKeys.all, 'demo-deletion', id] as const,
};
