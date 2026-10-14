import type { TileQuery } from './components/tile-card';

// The props of a StatTile that follow its query.
export function statTileState(query: TileQuery) {
  return {
    loading: query.isLoading,
    // The figures of the previous key while the new one loads (keepPreviousData).
    stale: query.isPlaceholderData === true,
    // A failed background refresh keeps the figures on screen.
    error: query.isError && query.data === undefined,
    onRetry: () => void query.refetch(),
  };
}
