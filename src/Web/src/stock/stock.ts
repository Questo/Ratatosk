import { keepPreviousData, queryOptions } from '@tanstack/react-query'
import { api } from '../api/client'

export interface Stock {
  productId: string
  sku: string
  onHand: number
  reserved: number
  available: number
  unit: string
  version: number
  lastUpdatedUtc: string
}

export interface StockPage {
  items: Stock[]
  totalItems: number
  page: number
  pageSize: number
  totalPages: number
}

// Each action is also the last segment of its endpoint, POST /inventory/{productId}/<action>
export type StockAction = 'restock' | 'reserve' | 'release'

export const actionLabels: Record<StockAction, string> = {
  restock: 'Restock',
  reserve: 'Reserve',
  release: 'Release',
}

export const pageSize = 25

// Every Stock query starts with this key, so one invalidation refetches them all
export const stockKey = ['stock'] as const

export function stockQuery(page: number) {
  const params = new URLSearchParams()
  params.set('page', String(page))
  params.set('pageSize', String(pageSize))

  return queryOptions({
    queryKey: [...stockKey, { page }],
    queryFn: () => api.get<StockPage>(`/api/inventory?${params}`),
    placeholderData: keepPreviousData,
  })
}
