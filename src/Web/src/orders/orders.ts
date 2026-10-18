import { keepPreviousData, queryOptions } from '@tanstack/react-query'
import { api } from '../api/client'

export type OrderStatus = 'Placed' | 'Confirmed' | 'Cancelled'

export const orderStatuses: OrderStatus[] = ['Placed', 'Confirmed', 'Cancelled']

export interface OrderLine {
  sku: string
  quantity: number
  unitPrice: number
  currency: string
}

export interface Order {
  id: string
  customerId: string
  status: OrderStatus
  lines: OrderLine[]
  createdUtc: string
  lastUpdatedUtc: string
  // Null unless the Order is Cancelled, and for Orders cancelled before the reason was recorded
  cancellationReason: string | null
}

// The Order as it was right after one event in its stream
export interface OrderHistoryEntry {
  version: number
  event: string
  occurredAtUtc: string
  order: Order
  reservedSkus: string[]
}

export interface OrderPage {
  items: Order[]
  totalItems: number
  page: number
  pageSize: number
  totalPages: number
}

export const statusColors: Record<OrderStatus, string> = {
  Placed: 'blue',
  Confirmed: 'green',
  Cancelled: 'red',
}

export const pageSize = 25

// Every Order query starts with this key, so one invalidation refetches the list and any open detail
export const ordersKey = ['orders'] as const

// A null status lists every Order
export function ordersQuery(status: OrderStatus | null, page: number) {
  const params = new URLSearchParams()
  params.set('page', String(page))
  params.set('pageSize', String(pageSize))
  if (status) params.set('status', status)

  return queryOptions({
    queryKey: [...ordersKey, 'list', { status, page }],
    queryFn: () => api.get<OrderPage>(`/api/orders?${params}`),
    placeholderData: keepPreviousData,
  })
}

export function orderQuery(orderId: string) {
  return queryOptions({
    queryKey: [...ordersKey, 'detail', orderId],
    queryFn: () => api.get<Order>(`/api/orders/${encodeURIComponent(orderId)}`),
  })
}

export function orderHistoryQuery(orderId: string) {
  return queryOptions({
    queryKey: [...ordersKey, 'history', orderId],
    queryFn: () => api.get<OrderHistoryEntry[]>(`/api/orders/${encodeURIComponent(orderId)}/history`),
  })
}

// The number of Orders in a status: the totalItems of a one-row page filtered by it
export function orderCountQuery(status: OrderStatus) {
  const params = new URLSearchParams()
  params.set('status', status)
  params.set('page', '1')
  params.set('pageSize', '1')

  return queryOptions({
    queryKey: [...ordersKey, 'count', status],
    queryFn: () => api.get<OrderPage>(`/api/orders?${params}`),
    select: (page) => page.totalItems,
  })
}
