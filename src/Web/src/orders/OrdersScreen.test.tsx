import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { renderWithProviders } from '../test/render'
import { server } from '../test/server'
import type { Order } from './orders'
import { OrdersScreen } from './OrdersScreen'

const envelope = (data: unknown, status = 200) =>
  HttpResponse.json({ message: null, data, timestamp: new Date().toISOString() }, { status })
const failure = (message: string, status = 400) => HttpResponse.json({ message }, { status })

const id = (n: number) => `00000000-0000-0000-0000-${String(n).padStart(12, '0')}`

function order(n: number, overrides: Partial<Order> = {}): Order {
  return {
    id: id(n),
    customerId: 'c0000000-0000-0000-0000-000000000001',
    status: 'Confirmed',
    lines: [{ sku: 'BW-0001', quantity: 1, unitPrice: 9.99, currency: 'EUR' }],
    createdUtc: '2026-10-18T10:00:00Z',
    lastUpdatedUtc: '2026-10-18T10:00:01Z',
    cancellationReason: null,
    ...overrides,
  }
}

const confirmed = order(1, {
  lines: [
    { sku: 'BW-0001', quantity: 2, unitPrice: 9.99, currency: 'EUR' },
    { sku: 'RG-0002', quantity: 1, unitPrice: 25, currency: 'EUR' },
  ],
})
const cancelled = order(2, {
  status: 'Cancelled',
  cancellationReason: 'Not enough stock available for SKU BW-0001',
})
const placed = order(3, { status: 'Placed' })

// A fake Orders API over an in-memory list, recording the query parameters of every list request
function fakeOrdersApi(initial: Order[] = [confirmed, cancelled, placed]) {
  const orders = initial.map((o) => ({ ...o }))
  const lists: URLSearchParams[] = []
  const details: string[] = []

  server.use(
    http.get('/api/orders', ({ request }) => {
      const params = new URL(request.url).searchParams
      lists.push(params)
      const page = Number(params.get('page') ?? 1)
      const pageSize = Number(params.get('pageSize') ?? 25)
      const status = params.get('status')
      const matching = orders.filter((o) => !status || o.status.toLowerCase() === status.toLowerCase())
      return envelope({
        items: matching.slice((page - 1) * pageSize, page * pageSize),
        totalItems: matching.length,
        page,
        pageSize,
        totalPages: Math.ceil(matching.length / pageSize),
      })
    }),
    http.get('/api/orders/:orderId', ({ params }) => {
      const orderId = params.orderId as string
      details.push(orderId)
      const found = orders.find((o) => o.id === orderId)
      return found ? envelope(found) : failure(`No order found with id ${orderId}`, 404)
    }),
  )
  return { orders, lists, details }
}

const lastList = (lists: URLSearchParams[]) => lists[lists.length - 1]

async function rowOf(orderId: string) {
  return within((await screen.findByRole('cell', { name: orderId })).closest('tr')!)
}

async function chooseStatus(status: string) {
  const user = userEvent.setup()
  await user.click(screen.getByRole('combobox', { name: 'Status' }))
  // jsdom never finishes the dropdown's opening transition, so its options stay display: none
  await user.click(await screen.findByRole('option', { name: status, hidden: true }))
}

async function openDetail(orderId: string) {
  await userEvent.setup().click((await rowOf(orderId)).getByRole('button', { name: 'View' }))
  return within(await screen.findByRole('dialog', { name: `Order ${orderId}` }))
}

describe('OrdersScreen', () => {
  it('lists the first page of every Order in a table', async () => {
    const { lists } = fakeOrdersApi()

    renderWithProviders(<OrdersScreen />)

    expect(screen.getByRole('heading', { name: 'Orders' })).toBeInTheDocument()
    expect(screen.getByRole('combobox', { name: 'Status' })).toHaveValue('All')
    const row = await rowOf(confirmed.id)
    for (const header of ['Order', 'Status', 'Order lines', 'Placed', 'Last updated']) {
      expect(screen.getByRole('columnheader', { name: header })).toBeInTheDocument()
    }
    expect(row.getAllByRole('cell').map((cell) => cell.textContent).slice(0, 5)).toEqual([
      confirmed.id,
      'Confirmed',
      '2',
      new Date(confirmed.createdUtc).toLocaleString(),
      new Date(confirmed.lastUpdatedUtc).toLocaleString(),
    ])
    expect(row.getByRole('button', { name: 'View' })).toBeInTheDocument()
    expect((await rowOf(cancelled.id)).getByText('Cancelled')).toBeInTheDocument()
    expect((await rowOf(placed.id)).getByText('Placed')).toBeInTheDocument()
    expect(lists).toHaveLength(1)
    expect(Object.fromEntries(lists[0])).toEqual({ page: '1', pageSize: '25' })
  })

  it('filters by status, and All sends no status parameter', async () => {
    const { lists } = fakeOrdersApi()
    renderWithProviders(<OrdersScreen />)
    await rowOf(confirmed.id)

    await chooseStatus('Cancelled')

    await waitFor(() => expect(screen.queryByRole('cell', { name: confirmed.id })).not.toBeInTheDocument())
    expect(await screen.findByRole('cell', { name: cancelled.id })).toBeInTheDocument()
    expect(screen.queryByRole('cell', { name: placed.id })).not.toBeInTheDocument()
    expect(Object.fromEntries(lastList(lists))).toEqual({ page: '1', pageSize: '25', status: 'Cancelled' })

    await chooseStatus('All')

    expect(await screen.findByRole('cell', { name: confirmed.id })).toBeInTheDocument()
    expect(Object.fromEntries(lastList(lists))).toEqual({ page: '1', pageSize: '25' })
  })

  it('goes back to page 1 when the filter changes', async () => {
    const many = Array.from({ length: 30 }, (_, i) => order(i + 1, { status: i % 2 ? 'Cancelled' : 'Confirmed' }))
    const { lists } = fakeOrdersApi(many)
    const user = userEvent.setup()
    renderWithProviders(<OrdersScreen />)
    await rowOf(id(1))

    await user.click(screen.getByRole('button', { name: '2' }))
    expect(await screen.findByRole('cell', { name: id(26) })).toBeInTheDocument()
    expect(lastList(lists).get('page')).toBe('2')

    await chooseStatus('Confirmed')

    expect(await screen.findByRole('cell', { name: id(1) })).toBeInTheDocument()
    expect(Object.fromEntries(lastList(lists))).toEqual({ page: '1', pageSize: '25', status: 'Confirmed' })
    // 15 Confirmed Orders fit on one page
    expect(screen.queryByRole('button', { name: '2' })).not.toBeInTheDocument()
  })

  it('pages through more than 25 Orders', async () => {
    const { lists } = fakeOrdersApi(Array.from({ length: 30 }, (_, i) => order(i + 1)))
    const user = userEvent.setup()
    renderWithProviders(<OrdersScreen />)
    await rowOf(id(1))
    expect(screen.queryByRole('cell', { name: id(26) })).not.toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: '2' }))

    expect(await screen.findByRole('cell', { name: id(26) })).toBeInTheDocument()
    expect(screen.queryByRole('cell', { name: id(1) })).not.toBeInTheDocument()
    expect(lastList(lists).get('page')).toBe('2')
    expect(lastList(lists).get('pageSize')).toBe('25')
  })

  it('hides the pagination control when there is one page', async () => {
    fakeOrdersApi()

    renderWithProviders(<OrdersScreen />)

    await rowOf(confirmed.id)
    expect(screen.queryByRole('button', { name: '1' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: '2' })).not.toBeInTheDocument()
  })

  it('shows "No Orders found." for an empty list', async () => {
    fakeOrdersApi([])

    renderWithProviders(<OrdersScreen />)

    expect(await screen.findByText('No Orders found.')).toBeInTheDocument()
    expect(screen.queryByRole('table')).not.toBeInTheDocument()
  })

  it('shows the error message when the list fails to load', async () => {
    server.use(
      http.get('/api/orders', () =>
        failure("Unknown status 'bogus'. Valid statuses: Placed, Confirmed, Cancelled."),
      ),
    )

    renderWithProviders(<OrdersScreen />)

    expect(await screen.findByRole('alert')).toHaveTextContent("Unknown status 'bogus'.")
  })

  it('shows a Cancelled Order with its lines, status and cancellation reason', async () => {
    const { details } = fakeOrdersApi()
    renderWithProviders(<OrdersScreen />)

    const dialog = await openDetail(cancelled.id)

    expect(await dialog.findByText('Not enough stock available for SKU BW-0001')).toBeInTheDocument()
    expect(dialog.getByText('Cancellation reason')).toBeInTheDocument()
    expect(dialog.getByText('Cancelled')).toBeInTheDocument()
    expect(dialog.getByText(cancelled.customerId)).toBeInTheDocument()
    expect(dialog.getByText(new Date(cancelled.createdUtc).toLocaleString())).toBeInTheDocument()
    expect(dialog.getByText(new Date(cancelled.lastUpdatedUtc).toLocaleString())).toBeInTheDocument()
    const line = within(dialog.getByRole('cell', { name: 'BW-0001' }).closest('tr')!)
    expect(line.getAllByRole('cell').map((cell) => cell.textContent)).toEqual(['BW-0001', '1', '9.99 EUR'])
    expect(details).toEqual([cancelled.id])
  })

  it('shows "Not recorded" for a Cancelled Order without a reason', async () => {
    fakeOrdersApi([order(1, { status: 'Cancelled', cancellationReason: null })])
    renderWithProviders(<OrdersScreen />)

    const dialog = await openDetail(id(1))

    expect(await dialog.findByText('Not recorded')).toBeInTheDocument()
    expect(dialog.getByText('Cancellation reason')).toBeInTheDocument()
  })

  it('shows no cancellation reason for a Confirmed or Placed Order', async () => {
    fakeOrdersApi()
    const user = userEvent.setup()
    renderWithProviders(<OrdersScreen />)

    let dialog = await openDetail(confirmed.id)
    for (const [sku, quantity, price] of [
      ['BW-0001', '2', '9.99 EUR'],
      ['RG-0002', '1', '25.00 EUR'],
    ]) {
      const line = within(await dialog.findByRole('cell', { name: sku }).then((cell) => cell.closest('tr')!))
      expect(line.getAllByRole('cell').map((cell) => cell.textContent)).toEqual([sku, quantity, price])
    }
    for (const header of ['SKU', 'Quantity', 'Unit price']) {
      expect(dialog.getByRole('columnheader', { name: header })).toBeInTheDocument()
    }
    expect(dialog.queryByText('Cancellation reason')).not.toBeInTheDocument()
    expect(dialog.queryByText('Not recorded')).not.toBeInTheDocument()

    await user.keyboard('{Escape}')
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())

    dialog = await openDetail(placed.id)
    // The Placed time's label and the status badge
    expect(await dialog.findAllByText('Placed')).toHaveLength(2)
    expect(dialog.queryByText('Cancellation reason')).not.toBeInTheDocument()
  })

  it('shows the error message when the Order fails to load', async () => {
    fakeOrdersApi()
    server.use(http.get('/api/orders/:orderId', ({ params }) => failure(`No order found with id ${params.orderId}`, 404)))
    renderWithProviders(<OrdersScreen />)

    const dialog = await openDetail(confirmed.id)

    expect(await dialog.findByRole('alert')).toHaveTextContent(`No order found with id ${confirmed.id}`)
  })
})
