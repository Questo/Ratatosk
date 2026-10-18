import { queryOptions, skipToken } from '@tanstack/react-query'

// One signal from GET /changes: the read model of this kind and id changed to this version
export interface ChangeSignal {
  kind: 'product' | 'stock' | 'order'
  id: string
  version: number
}

export interface Activity extends ChangeSignal {
  receivedAt: Date
}

export const activityLimit = 20

// The signals received since sign-in, newest first. Only useChangeStream writes them; the cache is
// their only store, so queryClient.clear() on sign-out empties the feed.
export const activityKey = ['activity'] as const

export const activityQuery = queryOptions<Activity[]>({
  queryKey: activityKey,
  queryFn: skipToken,
  // Kept while no screen shows the feed
  gcTime: Infinity,
})
