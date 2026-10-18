import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { delay, http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { renderWithProviders } from '../test/render'
import { server } from '../test/server'
import type { Stock } from './stock'
import { StockScreen } from './StockScreen'

const envelope = (data: unknown, status = 200) =>
  HttpResponse.json({ message: null, data, timestamp: new Date().toISOString() }, { status })
const failure = (message: string, status = 400) => HttpResponse.json({ message }, { status })

function stock(n: number, overrides: Partial<Stock> = {}): Stock {
  return {
    productId: `00000000-0000-0000-0000-${String(n).padStart(12, '0')}`,
    sku: `PR-${String(n).padStart(4, '0')}`,
    onHand: 0,
    reserved: 0,
    available: 0,
    unit: 'pcs',
    version: 1,
    lastUpdatedUtc: '2026-10-18T00:00:00Z',
    ...overrides,
  }
}

const widget = stock(1, { sku: 'BW-0001', onHand: 12, reserved: 5, available: 7, unit: 'pcs' })
const gadget = stock(2, { sku: 'RG-0002', onHand: 0, reserved: 0, available: 0, unit: 'kg' })

type Change = { productId: string; body: { quantity: number } }

// A fake Inventory API over an in-memory list, recording every request it serves.
// Like the domain, it keeps available = on hand - reserved; the rules themselves stay in the API.
function fakeInventoryApi(initial: Stock[] = [widget, gadget]) {
  const records = initial.map((record) => ({ ...record }))
  const lists: URLSearchParams[] = []
  const changes: Record<'restock' | 'reserve' | 'release', Change[]> = { restock: [], reserve: [], release: [] }

  const change = (action: keyof typeof changes, apply: (record: Stock, quantity: number) => void) =>
    http.post(`/api/inventory/:productId/${action}`, async ({ params, request }) => {
      const body = (await request.json()) as { quantity: number }
      const productId = params.productId as string
      changes[action].push({ productId, body })
      const record = records.find((r) => r.productId === productId)!
      apply(record, body.quantity)
      record.available = record.onHand - record.reserved
      return envelope(null)
    })

  server.use(
    http.get('/api/inventory', ({ request }) => {
      const params = new URL(request.url).searchParams
      lists.push(params)
      const page = Number(params.get('page') ?? 1)
      const pageSize = Number(params.get('pageSize') ?? 25)
      return envelope({
        items: records.slice((page - 1) * pageSize, page * pageSize),
        totalItems: records.length,
        page,
        pageSize,
        totalPages: Math.ceil(records.length / pageSize),
      })
    }),
    change('restock', (record, quantity) => (record.onHand += quantity)),
    change('reserve', (record, quantity) => (record.reserved += quantity)),
    change('release', (record, quantity) => (record.reserved -= quantity)),
  )
  return { lists, changes }
}

const lastList = (lists: URLSearchParams[]) => lists[lists.length - 1]

async function rowOf(sku: string) {
  return within((await screen.findByRole('cell', { name: sku })).closest('tr')!)
}

// The On hand, Reserved and Available cells of the row, once the refetch shows them
async function expectRow(sku: string, values: { onHand: number; reserved: number; available: number }) {
  const row = await rowOf(sku)
  await waitFor(() => {
    const cells = row.getAllByRole('cell').map((cell) => cell.textContent)
    expect(cells.slice(1, 4)).toEqual([String(values.onHand), String(values.reserved), String(values.available)])
  })
}

async function openDialog(sku: string, action: 'Restock' | 'Reserve' | 'Release') {
  const user = userEvent.setup()
  await user.click((await rowOf(sku)).getByRole('button', { name: action }))
  return within(await screen.findByRole('dialog', { name: `${action} ${sku}` }))
}

async function submitQuantity(dialog: Awaited<ReturnType<typeof openDialog>>, action: string, quantity: string) {
  const user = userEvent.setup()
  await user.type(dialog.getByLabelText(/^Quantity/), quantity)
  await user.click(dialog.getByRole('button', { name: action }))
}

const dialogClosed = () => waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())

describe('StockScreen', () => {
  it('lists the first page of Stock records in a table', async () => {
    const { lists } = fakeInventoryApi()

    renderWithProviders(<StockScreen />)

    expect(screen.getByRole('heading', { name: 'Stock' })).toBeInTheDocument()
    const row = await rowOf('BW-0001')
    for (const header of ['SKU', 'On hand', 'Reserved', 'Available', 'Unit']) {
      expect(screen.getByRole('columnheader', { name: header })).toBeInTheDocument()
    }
    expect(row.getAllByRole('cell').map((cell) => cell.textContent).slice(0, 5)).toEqual([
      'BW-0001',
      '12',
      '5',
      '7',
      'pcs',
    ])
    for (const action of ['Restock', 'Reserve', 'Release']) {
      expect(row.getByRole('button', { name: action })).toBeInTheDocument()
    }
    expect((await rowOf('RG-0002')).getByRole('cell', { name: 'kg' })).toBeInTheDocument()
    expect(lists).toHaveLength(1)
    expect(Object.fromEntries(lists[0])).toEqual({ page: '1', pageSize: '25' })
  })

  it('shows "No Stock records found." for an empty list', async () => {
    fakeInventoryApi([])

    renderWithProviders(<StockScreen />)

    expect(await screen.findByText('No Stock records found.')).toBeInTheDocument()
    expect(screen.queryByRole('table')).not.toBeInTheDocument()
  })

  it('shows the error message when the list fails to load', async () => {
    server.use(http.get('/api/inventory', () => failure('Page must be at least 1.')))

    renderWithProviders(<StockScreen />)

    expect(await screen.findByRole('alert')).toHaveTextContent('Page must be at least 1.')
  })

  it('pages through more than 25 Stock records', async () => {
    const { lists } = fakeInventoryApi(Array.from({ length: 30 }, (_, i) => stock(i + 1)))
    const user = userEvent.setup()
    renderWithProviders(<StockScreen />)
    await rowOf('PR-0001')
    expect(screen.queryByRole('cell', { name: 'PR-0026' })).not.toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: '2' }))

    expect(await screen.findByRole('cell', { name: 'PR-0026' })).toBeInTheDocument()
    expect(screen.queryByRole('cell', { name: 'PR-0001' })).not.toBeInTheDocument()
    expect(lastList(lists).get('page')).toBe('2')
    expect(lastList(lists).get('pageSize')).toBe('25')
  })

  it('hides the pagination control when there is one page', async () => {
    fakeInventoryApi()

    renderWithProviders(<StockScreen />)

    await rowOf('BW-0001')
    expect(screen.queryByRole('button', { name: '1' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: '2' })).not.toBeInTheDocument()
  })

  it('requires a quantity and sends no request', async () => {
    const { changes } = fakeInventoryApi()
    renderWithProviders(<StockScreen />)

    const dialog = await openDialog('RG-0002', 'Restock')
    await userEvent.setup().click(dialog.getByRole('button', { name: 'Restock' }))

    expect(await dialog.findByText('Quantity is required')).toBeInTheDocument()
    expect(changes.restock).toHaveLength(0)
  })

  it('restocks, closes the modal and shows the new on hand and available Stock', async () => {
    const { changes } = fakeInventoryApi()
    renderWithProviders(<StockScreen />)

    const dialog = await openDialog('RG-0002', 'Restock')
    await submitQuantity(dialog, 'Restock', '10')

    await dialogClosed()
    expect(changes.restock).toEqual([{ productId: gadget.productId, body: { quantity: 10 } }])
    await expectRow('RG-0002', { onHand: 10, reserved: 0, available: 10 })
  })

  it('reserves and releases by hand, updating the row after each refetch', async () => {
    const { changes } = fakeInventoryApi()
    renderWithProviders(<StockScreen />)

    await submitQuantity(await openDialog('RG-0002', 'Restock'), 'Restock', '10')
    await dialogClosed()
    await expectRow('RG-0002', { onHand: 10, reserved: 0, available: 10 })

    await submitQuantity(await openDialog('RG-0002', 'Reserve'), 'Reserve', '4')
    await dialogClosed()
    expect(changes.reserve).toEqual([{ productId: gadget.productId, body: { quantity: 4 } }])
    await expectRow('RG-0002', { onHand: 10, reserved: 4, available: 6 })

    await submitQuantity(await openDialog('RG-0002', 'Release'), 'Release', '3')
    await dialogClosed()
    expect(changes.release).toEqual([{ productId: gadget.productId, body: { quantity: 3 } }])
    await expectRow('RG-0002', { onHand: 10, reserved: 1, available: 9 })
  })

  it.each([
    ['Reserve', 'reserve', '8', 'Not enough stock available for SKU BW-0001'],
    ['Release', 'release', '6', 'Not enough reserved stock for SKU BW-0001'],
    ['Restock', 'restock', '0', 'Quantity must be greater than zero'],
  ] as const)(
    'keeps the %s modal open with the entered quantity and shows the API message',
    async (action, path, quantity, message) => {
      fakeInventoryApi()
      server.use(http.post(`/api/inventory/:productId/${path}`, () => failure(message)))
      renderWithProviders(<StockScreen />)

      const dialog = await openDialog('BW-0001', action)
      await submitQuantity(dialog, action, quantity)

      expect(await dialog.findByRole('alert')).toHaveTextContent(message)
      expect(screen.getByRole('dialog', { name: `${action} BW-0001` })).toBeInTheDocument()
      expect(dialog.getByLabelText(/^Quantity/)).toHaveValue(quantity)
    },
  )

  it('shows a generic message when the failure has no message', async () => {
    fakeInventoryApi()
    server.use(http.post('/api/inventory/:productId/reserve', () => new HttpResponse(null, { status: 500 })))
    renderWithProviders(<StockScreen />)

    const dialog = await openDialog('BW-0001', 'Reserve')
    await submitQuantity(dialog, 'Reserve', '2')

    expect(await dialog.findByRole('alert')).toHaveTextContent('Changing the Stock failed. Try again.')
  })

  it('shows the loading state while changing and clears the error on a new submit', async () => {
    fakeInventoryApi()
    let attempts = 0
    server.use(
      http.post('/api/inventory/:productId/reserve', async () => {
        await delay(100)
        return ++attempts === 1 ? failure('Not enough stock available for SKU BW-0001') : envelope(null)
      }),
    )
    const user = userEvent.setup()
    renderWithProviders(<StockScreen />)

    const dialog = await openDialog('BW-0001', 'Reserve')
    await submitQuantity(dialog, 'Reserve', '8')
    expect(dialog.getByRole('button', { name: 'Reserve' })).toHaveAttribute('data-loading')
    expect(await dialog.findByRole('alert')).toHaveTextContent('Not enough stock available for SKU BW-0001')

    await user.click(dialog.getByRole('button', { name: 'Reserve' }))

    expect(dialog.queryByRole('alert')).not.toBeInTheDocument()
    await dialogClosed()
  })
})
