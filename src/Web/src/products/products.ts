import { keepPreviousData, queryOptions } from '@tanstack/react-query'
import { api } from '../api/client'

export interface Product {
  id: string
  name: string
  sku: string
  description: string
  price: number
  lastUpdatedUtc: string
}

export interface ProductPage {
  items: Product[]
  totalItems: number
  page: number
  pageSize: number
  totalPages: number
}

export const pageSize = 25

// Every Products query starts with this key, so one invalidation refetches them all
export const productsKey = ['products'] as const

export function productsQuery(searchTerm: string, page: number) {
  const params = new URLSearchParams()
  if (searchTerm) params.set('searchTerm', searchTerm)
  params.set('page', String(page))
  params.set('pageSize', String(pageSize))

  return queryOptions({
    queryKey: [...productsKey, { searchTerm, page }],
    queryFn: () => api.get<ProductPage>(`/api/products?${params}`),
    placeholderData: keepPreviousData,
  })
}
