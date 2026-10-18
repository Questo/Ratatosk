import { queryOptions } from '@tanstack/react-query'
import { api } from '../api/client'

export interface SearchHit {
  id: string
  title: string
  subtitle: string
  // Which screen opens the hit; the Shell maps it ('product', 'order')
  kind: string
}

export interface SearchGroup {
  kind: string
  label: string
  hits: SearchHit[]
  // The API couldn't search this kind; hits is empty
  failed: boolean
}

export interface SearchResults {
  groups: SearchGroup[]
}

// What the Shell passes a screen when a search hit asks it to open an entity; seq grows on every
// selection, so picking the same entity again reopens it
export interface OpenEntityRequest {
  kind: 'product' | 'order'
  id: string
  seq: number
}

// Keyed by the term and without placeholderData, so a slower response for an older term is never shown
export function searchQuery(term: string) {
  return queryOptions({
    queryKey: ['search', term],
    queryFn: () => api.get<SearchResults>(`/api/search?q=${encodeURIComponent(term)}`),
  })
}
