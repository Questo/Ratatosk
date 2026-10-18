import { act, screen, waitFor, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { useChangeStream } from '../changes/useChangeStream'
import type { OrderStatus } from '../orders/orders'
import type { Stock } from '../stock/stock'
import { FakeEventSource } from '../test/eventSource'
import { renderWithProviders } from '../test/render'
import { server } from '../test/server'
import { DashboardScreen } from './DashboardScreen'

const envelope = (data: unknown) => HttpResponse.json({ message: null, data, timestamp: new Date().toISOString() })

const stockRecord = (sku: string, onHand: number, reserved: number): Stock => ({
  productId: `p-${sku}`,
  sku,
  onHand,
  reserved,
  available: onHand - reserved,
  unit: 'pcs',
  version: 1,
  lastUpdatedUtc: '2026-10-18T10:00:00Z',
})

// Counts per status and the low-stock page are mutable, so a test can change them between requests.
// Every request is recorded so the tests can check what the dashboard asked for.
function fakeApi(
  options: { counts?: Record<OrderStatus, number>; lowStock?: Stock[]; lowStockTotal?: number } = {},
) {
  const state = {
    counts: options.counts ?? { Placed: 0, Confirmed: 0, Cancelled: 0 },
    lowStock: options.lowStock ?? [],
    lowStockTotal: options.lowStockTotal,
  }
  const requests = { orders: [] as URLSearchParams[], inventory: [] as URLSearchParams[] }

  server.use(
    http.get('/api/orders', ({ request }) => {
      const params = new URL(request.url).searchParams
      requests.orders.push(params)
      const totalItems = state.counts[params.get('status') as OrderStatus]
      return envelope({ items: [], totalItems, page: 1, pageSize: 1, totalPages: totalItems })
    }),
    http.get('/api/inventory', ({ request }) => {
      requests.inventory.push(new URL(request.url).searchParams)
      const items = state.lowStock
      return envelope({ items, totalItems: state.lowStockTotal ?? items.length, page: 1, pageSize: 10, totalPages: 1 })
    }),
  )
  return { state, requests }
}

function DashboardWithStream() {
  useChangeStream()
  return <DashboardScreen />
}

const stream = () => FakeEventSource.latest()
const section = (name: string) => within(screen.getByRole('region', { name }))
const count = async (status: string) => within(await screen.findByRole('group', { name: status }))

describe('DashboardScreen', () => {
  it('shows the count of each status from the totalItems of a status-filtered request', async () => {
    const api = fakeApi({ counts: { Placed: 4, Confirmed: 12, Cancelled: 1 } })

    renderWithProviders(<DashboardScreen />)

    expect(screen.getByRole('heading', { level: 2, name: 'Dashboard' })).toBeInTheDocument()
    expect(await (await count('Placed')).findByText('4')).toBeInTheDocument()
    expect((await count('Confirmed')).getByText('12')).toBeInTheDocument()
    expect((await count('Cancelled')).getByText('1')).toBeInTheDocument()
    expect(api.requests.orders.map((params) => Object.fromEntries(params))).toEqual(
      expect.arrayContaining(
        ['Placed', 'Confirmed', 'Cancelled'].map((status) => ({ status, page: '1', pageSize: '1' })),
      ),
    )
    expect(api.requests.orders).toHaveLength(3)
  })

  it('lists the low-stock records under the threshold and says how many more there are', async () => {
    const api = fakeApi({ lowStock: [stockRecord('BW-0002', 3, 3), stockRecord('BW-0001', 10, 6)], lowStockTotal: 5 })

    renderWithProviders(<DashboardScreen />)

    const lowStock = section('Low stock')
    const rows = await lowStock.findAllByRole('row')
    expect(rows.map((row) => Array.from(row.querySelectorAll('th, td'), (cell) => cell.textContent))).toEqual([
      ['SKU', 'Available', 'On hand', 'Reserved'],
      ['BW-0002', '0', '3', '3'],
      ['BW-0001', '4', '10', '6'],
    ])
    expect(lowStock.getByText('and 3 more')).toBeInTheDocument()
    expect(Object.fromEntries(api.requests.inventory[0])).toEqual({ maxAvailable: '5', page: '1', pageSize: '10' })
  })

  it('shows no "more" line when every low-stock record is listed', async () => {
    fakeApi({ lowStock: [stockRecord('BW-0001', 2, 0)] })

    renderWithProviders(<DashboardScreen />)

    expect(await section('Low stock').findByText('BW-0001')).toBeInTheDocument()
    expect(screen.queryByText(/more$/)).not.toBeInTheDocument()
  })

  it('shows the empty texts with no low stock and no changes', async () => {
    fakeApi()

    renderWithProviders(<DashboardScreen />)

    expect(await section('Low stock').findByText('No low-stock Products.')).toBeInTheDocument()
    expect(section('Recent activity').getByText('No changes since you signed in.')).toBeInTheDocument()
    expect(await (await count('Placed')).findByText('0')).toBeInTheDocument()
  })

  it('shows an error in one section without hiding the others', async () => {
    fakeApi({ counts: { Placed: 2, Confirmed: 0, Cancelled: 0 } })
    server.use(http.get('/api/inventory', () => HttpResponse.json({ message: 'Stock is down.' }, { status: 500 })))

    renderWithProviders(<DashboardScreen />)

    expect(await section('Low stock').findByRole('alert')).toHaveTextContent('Stock is down.')
    expect(await (await count('Placed')).findByText('2')).toBeInTheDocument()
    expect(section('Recent activity').getByText('No changes since you signed in.')).toBeInTheDocument()
  })

  it('shows an error when the Order counts fail', async () => {
    fakeApi()
    server.use(http.get('/api/orders', () => HttpResponse.json({ message: 'Orders are down.' }, { status: 500 })))

    renderWithProviders(<DashboardScreen />)

    expect(await section('Order counts').findByRole('alert')).toHaveTextContent('Orders are down.')
    expect(await section('Low stock').findByText('No low-stock Products.')).toBeInTheDocument()
  })

  it('refetches the counts on an order signal', async () => {
    const api = fakeApi({ counts: { Placed: 1, Confirmed: 0, Cancelled: 0 } })
    renderWithProviders(<DashboardWithStream />)
    await (await count('Placed')).findByText('1')

    api.state.counts = { Placed: 1, Confirmed: 1, Cancelled: 0 }
    act(() => stream().emitChange({ kind: 'order', id: 'o-1', version: 1 }))

    expect(await (await count('Confirmed')).findByText('1')).toBeInTheDocument()
    expect(api.requests.orders).toHaveLength(6)
  })

  it('refetches the low-stock list on a stock signal', async () => {
    const api = fakeApi()
    renderWithProviders(<DashboardWithStream />)
    await section('Low stock').findByText('No low-stock Products.')

    api.state.lowStock = [stockRecord('BW-0001', 5, 2)]
    act(() => stream().emitChange({ kind: 'stock', id: 'p-BW-0001', version: 3 }))

    expect(await section('Low stock').findByText('BW-0001')).toBeInTheDocument()
    expect(api.requests.inventory).toHaveLength(2)
    expect(api.requests.orders).toHaveLength(3)
  })

  it('lists the signals newest first and keeps only the 20 most recent', async () => {
    fakeApi()
    renderWithProviders(<DashboardWithStream />)

    act(() => {
      stream().emitChange({ kind: 'product', id: 'p-1', version: 1 })
      stream().emitChange({ kind: 'stock', id: 'p-1', version: 1 })
      stream().emitChange({ kind: 'order', id: 'o-1', version: 1 })
    })

    const activity = section('Recent activity')
    await waitFor(() => expect(activity.getAllByRole('row')).toHaveLength(4))
    const cells = (row: HTMLElement) => within(row).getAllByRole('cell').map((cell) => cell.textContent)
    const [, newest, middle, oldest] = activity.getAllByRole('row')
    expect(cells(newest).slice(0, 3)).toEqual(['Order', 'o-1', '1'])
    expect(cells(middle).slice(0, 3)).toEqual(['Stock', 'p-1', '1'])
    expect(cells(oldest).slice(0, 3)).toEqual(['Product', 'p-1', '1'])
    expect(cells(newest)[3]).toMatch(/\d/)

    act(() => {
      for (let version = 2; version <= 19; version++) stream().emitChange({ kind: 'order', id: 'o-1', version })
    })

    await waitFor(() => expect(activity.getAllByRole('row')).toHaveLength(21))
    const rows = activity.getAllByRole('row')
    expect(cells(rows[1]).slice(0, 3)).toEqual(['Order', 'o-1', '19'])
    expect(cells(rows[20]).slice(0, 3)).toEqual(['Stock', 'p-1', '1'])
    expect(activity.queryByRole('cell', { name: 'Product' })).not.toBeInTheDocument()
  })
})
