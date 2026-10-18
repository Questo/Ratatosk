import { act, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { afterEach, describe, expect, it, vi } from 'vitest'
import type { Order } from '../orders/orders'
import { OrdersScreen } from '../orders/OrdersScreen'
import { FakeEventSource } from '../test/eventSource'
import { renderWithProviders } from '../test/render'
import { server } from '../test/server'
import { useChangeStream } from './useChangeStream'

const envelope = (data: unknown) => HttpResponse.json({ message: null, data, timestamp: new Date().toISOString() })

const orderId = '00000000-0000-0000-0000-000000000001'

// The Order moves from Placed to Confirmed between the first and second request for it
function fakeApi() {
  let order: Order = {
    id: orderId,
    customerId: 'c0000000-0000-0000-0000-000000000001',
    status: 'Placed',
    lines: [{ sku: 'BW-0001', quantity: 1, unitPrice: 9.99, currency: 'EUR' }],
    createdUtc: '2026-10-18T10:00:00Z',
    lastUpdatedUtc: '2026-10-18T10:00:00Z',
    cancellationReason: null,
  }
  const calls = { lists: 0, details: 0, me: 0 }

  server.use(
    http.get('/api/orders', () => {
      calls.lists++
      return envelope({ items: [order], totalItems: 1, page: 1, pageSize: 25, totalPages: 1 })
    }),
    http.get('/api/orders/:orderId', () => {
      calls.details++
      return envelope(order)
    }),
    http.get('/api/auth/me', () => {
      calls.me++
      return envelope({ email: 'admin@factory.test', role: 'Admin' })
    }),
  )
  return {
    calls,
    confirm: () => (order = { ...order, status: 'Confirmed', lastUpdatedUtc: '2026-10-18T10:00:01Z' }),
  }
}

function StreamOnly() {
  useChangeStream()
  return null
}

function OrdersWithStream() {
  useChangeStream()
  return <OrdersScreen />
}

const stream = () => FakeEventSource.latest()

describe('useChangeStream', () => {
  afterEach(() => {
    vi.useRealTimers()
  })

  it('opens one EventSource on /api/changes and closes it on unmount', () => {
    const { unmount } = renderWithProviders(<StreamOnly />)

    expect(FakeEventSource.instances).toHaveLength(1)
    expect(stream().url).toBe('/api/changes')
    expect(stream().closed).toBe(false)

    unmount()

    expect(stream().closed).toBe(true)
  })

  it('refetches the Order list and an open detail on an order signal', async () => {
    const api = fakeApi()
    renderWithProviders(<OrdersWithStream />)
    const row = within((await screen.findByRole('cell', { name: orderId })).closest('tr')!)
    await userEvent.setup().click(row.getByRole('button', { name: 'View' }))
    const dialog = within(await screen.findByRole('dialog', { name: `Order ${orderId}` }))
    await dialog.findByText('BW-0001')
    expect(api.calls).toMatchObject({ lists: 1, details: 1 })

    api.confirm()
    act(() => stream().emitChange({ kind: 'order', id: orderId, version: 2 }))

    expect(await dialog.findByText('Confirmed')).toBeInTheDocument()
    await waitFor(() => expect(row.getByText('Confirmed')).toBeInTheDocument())
    expect(api.calls).toMatchObject({ lists: 2, details: 2 })
  })

  it('sends no Orders request for stock or product signals', async () => {
    const api = fakeApi()
    renderWithProviders(<OrdersWithStream />)
    await screen.findByRole('cell', { name: orderId })

    act(() => {
      stream().emitChange({ kind: 'stock', id: orderId, version: 3 })
      stream().emitChange({ kind: 'product', id: orderId, version: 2 })
    })
    // Give a refetch, had there been one, the time to go out
    await new Promise((resolve) => setTimeout(resolve, 50))

    expect(api.calls.lists).toBe(1)
  })

  it('refetches the Orders when the stream opens, to catch up on missed signals', async () => {
    const api = fakeApi()
    renderWithProviders(<OrdersWithStream />)
    await screen.findByRole('cell', { name: orderId })

    act(() => stream().emitOpen())

    await waitFor(() => expect(api.calls.lists).toBe(2))
  })

  it('refreshes the session through /auth/me and opens a new stream 5 seconds after it closes', async () => {
    const api = fakeApi()
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] })
    renderWithProviders(<StreamOnly />)
    const first = stream()

    act(() => first.emitError({ closed: true }))
    act(() => vi.advanceTimersByTime(4999))
    expect(api.calls.me).toBe(0)
    act(() => vi.advanceTimersByTime(1))
    vi.useRealTimers()

    await waitFor(() => expect(FakeEventSource.instances).toHaveLength(2))
    expect(api.calls.me).toBe(1)
    expect(stream()).not.toBe(first)
    expect(stream().url).toBe('/api/changes')
    expect(stream().closed).toBe(false)
  })

  it('leaves reconnecting to the browser while the stream is CONNECTING', async () => {
    const api = fakeApi()
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] })
    renderWithProviders(<StreamOnly />)

    act(() => stream().emitError({ closed: false }))
    act(() => vi.advanceTimersByTime(10_000))
    vi.useRealTimers()
    await new Promise((resolve) => setTimeout(resolve, 50))

    expect(api.calls.me).toBe(0)
    expect(FakeEventSource.instances).toHaveLength(1)
  })

  it('opens no new stream once unmounted', async () => {
    const api = fakeApi()
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] })
    const { unmount } = renderWithProviders(<StreamOnly />)

    act(() => stream().emitError({ closed: true }))
    unmount()
    act(() => vi.advanceTimersByTime(5000))
    vi.useRealTimers()
    await new Promise((resolve) => setTimeout(resolve, 50))

    expect(api.calls.me).toBe(0)
    expect(FakeEventSource.instances).toHaveLength(1)
  })
})
