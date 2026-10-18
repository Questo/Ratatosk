import { act, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { renderWithProviders } from '../test/render'
import { server } from '../test/server'
import { ordersKey, type Order, type OrderHistoryEntry } from './orders'
import { OrdersScreen } from './OrdersScreen'

const envelope = (data: unknown, status = 200) =>
  HttpResponse.json({ message: null, data, timestamp: new Date().toISOString() }, { status })
const failure = (message: string, status = 400) => HttpResponse.json({ message }, { status })

const orderId = '00000000-0000-0000-0000-000000000001'

const placed: Order = {
  id: orderId,
  customerId: 'c0000000-0000-0000-0000-000000000001',
  status: 'Placed',
  lines: [
    { sku: 'BW-0001', quantity: 2, unitPrice: 9.99, currency: 'EUR' },
    { sku: 'RG-0002', quantity: 1, unitPrice: 25, currency: 'EUR' },
  ],
  createdUtc: '2026-10-18T10:00:00Z',
  lastUpdatedUtc: '2026-10-18T10:00:00Z',
  cancellationReason: null,
}

function entry(version: number, event: string, order: Partial<Order>, reservedSkus: string[]): OrderHistoryEntry {
  const occurredAtUtc = `2026-10-18T10:00:0${version}Z`
  return { version, event, occurredAtUtc, order: { ...placed, lastUpdatedUtc: occurredAtUtc, ...order }, reservedSkus }
}

const confirmedHistory = [
  entry(1, 'OrderCreated', {}, []),
  entry(2, 'OrderLineReserved', {}, ['BW-0001']),
  entry(3, 'OrderLineReserved', {}, ['BW-0001', 'RG-0002']),
  entry(4, 'OrderConfirmed', { status: 'Confirmed' }, ['BW-0001', 'RG-0002']),
]

// A fake Orders API with one Order, recording every history request
function fakeHistoryApi(initial: OrderHistoryEntry[] = confirmedHistory) {
  const state = { history: initial }
  const requests: string[] = []
  const latest = () => state.history[state.history.length - 1].order

  server.use(
    http.get('/api/orders', () =>
      envelope({ items: [latest()], totalItems: 1, page: 1, pageSize: 25, totalPages: 1 }),
    ),
    http.get('/api/orders/:orderId', () => envelope(latest())),
    http.get('/api/orders/:orderId/history', ({ params }) => {
      requests.push(params.orderId as string)
      return envelope(state.history)
    }),
  )
  return { state, requests }
}

async function openHistory() {
  const user = userEvent.setup()
  const row = within((await screen.findByRole('cell', { name: orderId })).closest('tr')!)
  await user.click(row.getByRole('button', { name: 'View' }))
  const dialog = within(await screen.findByRole('dialog', { name: `Order ${orderId}` }))
  await user.click(dialog.getByRole('tab', { name: 'History' }))
  await dialog.findByText(/^Version \d+ of \d+/)
  return { dialog, user }
}

const reservedColumn = (dialog: ReturnType<typeof within>) =>
  ['BW-0001', 'RG-0002'].map((sku) => {
    const cells = within(dialog.getByRole('cell', { name: sku }).closest('tr')!).getAllByRole('cell')
    return cells[cells.length - 1].textContent
  })

describe('OrderHistory', () => {
  it('opens the detail on the Details tab and fetches the history only once History is shown', async () => {
    const { requests } = fakeHistoryApi()
    const user = userEvent.setup()
    renderWithProviders(<OrdersScreen />)
    const row = within((await screen.findByRole('cell', { name: orderId })).closest('tr')!)

    await user.click(row.getByRole('button', { name: 'View' }))

    const dialog = within(await screen.findByRole('dialog', { name: `Order ${orderId}` }))
    expect(dialog.getByRole('tab', { name: 'Details', selected: true })).toBeInTheDocument()
    expect(await dialog.findByText('Customer id')).toBeInTheDocument()
    expect(requests).toEqual([])

    await user.click(dialog.getByRole('tab', { name: 'History' }))

    expect(await dialog.findByText(/^Version 4 of 4/)).toBeInTheDocument()
    expect(requests).toEqual([orderId])
  })

  it('opens at the latest version', async () => {
    fakeHistoryApi()
    renderWithProviders(<OrdersScreen />)

    const { dialog } = await openHistory()

    expect(dialog.getByText(/^Version 4 of 4/)).toHaveTextContent(
      `Version 4 of 4 · OrderConfirmed · ${new Date(confirmedHistory[3].occurredAtUtc).toLocaleString()}`,
    )
    expect(dialog.getByText('Confirmed')).toBeInTheDocument()
    for (const header of ['SKU', 'Quantity', 'Unit price', 'Reserved']) {
      expect(dialog.getByRole('columnheader', { name: header })).toBeInTheDocument()
    }
    const line = within(dialog.getByRole('cell', { name: 'BW-0001' }).closest('tr')!)
    expect(line.getAllByRole('cell').map((cell) => cell.textContent)).toEqual(['BW-0001', '2', '9.99 EUR', 'Yes'])
    expect(dialog.getByRole('button', { name: 'Next' })).toBeDisabled()
    expect(dialog.getByRole('button', { name: 'Previous' })).toBeEnabled()
  })

  it('steps back and forward through the versions, and the Reserved column follows', async () => {
    fakeHistoryApi()
    renderWithProviders(<OrdersScreen />)
    const { dialog, user } = await openHistory()

    await user.click(dialog.getByRole('button', { name: 'Previous' }))
    expect(dialog.getByText(/^Version 3 of 4/)).toHaveTextContent('OrderLineReserved')
    expect(dialog.getByText('Placed')).toBeInTheDocument()
    expect(reservedColumn(dialog)).toEqual(['Yes', 'Yes'])

    await user.click(dialog.getByRole('button', { name: 'Previous' }))
    expect(reservedColumn(dialog)).toEqual(['Yes', 'No'])

    await user.click(dialog.getByRole('button', { name: 'Previous' }))
    expect(dialog.getByText(/^Version 1 of 4/)).toHaveTextContent('OrderCreated')
    expect(reservedColumn(dialog)).toEqual(['No', 'No'])
    expect(dialog.getByRole('button', { name: 'Previous' })).toBeDisabled()
    expect(dialog.getByRole('button', { name: 'Next' })).toBeEnabled()

    await user.click(dialog.getByRole('button', { name: 'Next' }))
    expect(dialog.getByText(/^Version 2 of 4/)).toHaveTextContent('OrderLineReserved')
    expect(reservedColumn(dialog)).toEqual(['Yes', 'No'])
    expect(dialog.getByRole('button', { name: 'Previous' })).toBeEnabled()
  })

  it('shows the cancellation reason at a Cancelled step', async () => {
    const reason = 'Not enough stock available for SKU RG-0002'
    fakeHistoryApi([
      entry(1, 'OrderCreated', {}, []),
      entry(2, 'OrderCancelled', { status: 'Cancelled', cancellationReason: reason }, []),
    ])
    renderWithProviders(<OrdersScreen />)

    const { dialog, user } = await openHistory()

    expect(dialog.getByText('Cancelled')).toBeInTheDocument()
    expect(dialog.getByText(reason)).toBeInTheDocument()

    await user.click(dialog.getByRole('button', { name: 'Previous' }))
    expect(dialog.queryByText(reason)).not.toBeInTheDocument()
    expect(dialog.queryByText('Cancellation reason')).not.toBeInTheDocument()
  })

  it('moves to new events on a refetch only while it is at the latest version', async () => {
    const { state } = fakeHistoryApi(confirmedHistory.slice(0, 2))
    const { queryClient } = renderWithProviders(<OrdersScreen />)
    const { dialog, user } = await openHistory()
    expect(dialog.getByText(/^Version 2 of 2/)).toBeInTheDocument()

    state.history = confirmedHistory.slice(0, 3)
    await act(() => queryClient.invalidateQueries({ queryKey: ordersKey }))
    expect(await dialog.findByText(/^Version 3 of 3/)).toBeInTheDocument()

    await user.click(dialog.getByRole('button', { name: 'Previous' }))
    state.history = confirmedHistory
    await act(() => queryClient.invalidateQueries({ queryKey: ordersKey }))
    expect(await dialog.findByText(/^Version 2 of 4/)).toBeInTheDocument()
  })

  it('shows the error message when the history fails to load', async () => {
    fakeHistoryApi()
    server.use(
      http.get('/api/orders/:orderId/history', ({ params }) => failure(`No order found with id ${params.orderId}`, 404)),
    )
    const user = userEvent.setup()
    renderWithProviders(<OrdersScreen />)
    const row = within((await screen.findByRole('cell', { name: orderId })).closest('tr')!)
    await user.click(row.getByRole('button', { name: 'View' }))
    const dialog = within(await screen.findByRole('dialog', { name: `Order ${orderId}` }))

    await user.click(dialog.getByRole('tab', { name: 'History' }))

    await waitFor(() => expect(dialog.getByRole('alert')).toHaveTextContent(`No order found with id ${orderId}`))
  })
})
