import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import type { Order } from '../orders/orders'
import type { Product } from '../products/products'
import { renderWithProviders } from '../test/render'
import { server } from '../test/server'
import { Shell } from './Shell'

const envelope = (data: unknown, status = 200) =>
  HttpResponse.json({ message: null, data, timestamp: new Date().toISOString() }, { status })
const page = (items: unknown[]) => envelope({ items, totalItems: items.length, page: 1, pageSize: 25, totalPages: 1 })

const listed: Product = {
  id: 'p0000000-0000-0000-0000-000000000001',
  name: 'Listed Lamp',
  sku: 'LL-0001',
  description: 'On the first page',
  price: 5,
  lastUpdatedUtc: '2026-10-18T10:00:00Z',
}
const widget: Product = {
  id: 'p0000000-0000-0000-0000-000000000002',
  name: 'Search Widget',
  sku: 'SW-0001',
  description: 'Not on the mocked page',
  price: 12.5,
  lastUpdatedUtc: '2026-10-18T10:00:00Z',
}
const confirmed: Order = {
  id: 'o0000000-0000-0000-0000-000000000001',
  customerId: 'c0000000-0000-0000-0000-000000000001',
  status: 'Confirmed',
  lines: [{ sku: 'SW-0001', quantity: 1, unitPrice: 12.5, currency: 'EUR' }],
  createdUtc: '2026-10-18T10:00:00Z',
  lastUpdatedUtc: '2026-10-18T10:00:01Z',
  cancellationReason: null,
}

// A back office API whose list pages never contain the entities the search finds
function fakeApi(options: { productStatus?: number } = {}) {
  server.use(
    http.get('/api/products', () => page([listed])),
    http.get('/api/products/:id', ({ params }) =>
      params.id === widget.id && !options.productStatus
        ? envelope(widget)
        : HttpResponse.json({ message: 'Product not found.' }, { status: options.productStatus ?? 404 }),
    ),
    http.get('/api/orders', ({ request }) =>
      page(new URL(request.url).searchParams.get('status') === 'Cancelled' ? [] : [confirmed]),
    ),
    http.get('/api/orders/:id', () => envelope(confirmed)),
    http.get('/api/search', () =>
      envelope({
        groups: [
          {
            kind: 'product',
            label: 'Products',
            hits: [{ id: widget.id, title: widget.name, subtitle: 'SW-0001 · 12.50', kind: 'product' }],
            failed: false,
          },
          {
            kind: 'order',
            label: 'Orders',
            hits: [{ id: confirmed.id, title: 'o0000000', subtitle: 'Confirmed · 1 line · 2026-10-18', kind: 'order' }],
            failed: false,
          },
        ],
      }),
    ),
  )
}

async function searchAndPick(user: ReturnType<typeof userEvent.setup>, option: RegExp) {
  await user.keyboard('{Control>}k{/Control}')
  await user.type(await screen.findByRole('combobox', { name: 'Search Products and Orders' }), 'widget')
  await user.click(await screen.findByRole('option', { name: option }))
}

describe('Shell search', () => {
  it('opens the Edit Product dialog for a Product hit that is not on the list page', async () => {
    fakeApi()
    renderWithProviders(<Shell email="admin@factory.test" />)
    const user = userEvent.setup()
    await user.click(screen.getByRole('button', { name: 'Orders' }))
    await screen.findByRole('heading', { name: 'Orders' })

    await searchAndPick(user, /Search Widget/)

    const dialog = within(await screen.findByRole('dialog', { name: 'Edit Product' }))
    expect(dialog.getByLabelText(/Name/)).toHaveValue('Search Widget')
    expect(screen.getByRole('heading', { name: 'Products', level: 2 })).toBeInTheDocument()
    expect(await screen.findByText('Listed Lamp')).toBeInTheDocument()
    expect(screen.queryByRole('cell', { name: 'Search Widget' })).not.toBeInTheDocument()
  })

  it('opens the Order detail for an Order hit hidden by the status filter', async () => {
    fakeApi()
    renderWithProviders(<Shell email="admin@factory.test" />)
    const user = userEvent.setup()
    await user.click(screen.getByRole('button', { name: 'Orders' }))
    await user.click(await screen.findByRole('combobox', { name: 'Status' }))
    await user.click(await screen.findByRole('option', { name: 'Cancelled', hidden: true }))
    expect(await screen.findByText('No Orders found.')).toBeInTheDocument()

    await searchAndPick(user, /o0000000/)

    const dialog = within(await screen.findByRole('dialog', { name: `Order ${confirmed.id}` }))
    expect(dialog.getByRole('tab', { name: 'Details' })).toHaveAttribute('aria-selected', 'true')
    expect(await dialog.findByText(confirmed.customerId)).toBeInTheDocument()
    expect(screen.getByRole('combobox', { name: 'Status' })).toHaveValue('Cancelled')
    expect(screen.getByText('No Orders found.')).toBeInTheDocument()
  })

  it('opens the Order detail when the Orders screen was not shown before', async () => {
    fakeApi()
    renderWithProviders(<Shell email="admin@factory.test" />)
    const user = userEvent.setup()
    await screen.findByText('Listed Lamp')

    await searchAndPick(user, /o0000000/)

    expect(await screen.findByRole('dialog', { name: `Order ${confirmed.id}` })).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Orders', level: 2 })).toBeInTheDocument()
  })

  it('shows why a Product hit could not be opened', async () => {
    fakeApi({ productStatus: 404 })
    renderWithProviders(<Shell email="admin@factory.test" />)
    const user = userEvent.setup()

    await searchAndPick(user, /Search Widget/)

    expect(await screen.findByRole('alert')).toHaveTextContent('Product not found.')
    await waitFor(() => expect(screen.queryByRole('dialog', { name: 'Edit Product' })).not.toBeInTheDocument())
  })
})
