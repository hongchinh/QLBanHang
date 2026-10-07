export const branchKeys = {
  all: ['branches'] as const,
  lists: () => [...branchKeys.all, 'list'] as const,
  me: () => [...branchKeys.all, 'me'] as const,
};
