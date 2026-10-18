import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { delay, http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { renderWithProviders } from '../test/render'
import { server } from '../test/server'
import type { Product } from './products'
import { ProductsScreen } from './ProductsScreen'

const envelope = (data: unknown, status = 200) =>
  HttpResponse.json({ message: null, data, timestamp: new Date().toISOString() }, { status })
const failure = (message: string, status = 400) => HttpResponse.json({ message }, { status })

function product(n: number, overrides: Partial<Product> = {}): Product {
  return {
    id: `00000000-0000-0000-0000-${String(n).padStart(12, '0')}`,
    name: `Product ${n}`,
    sku: `PR-${String(n).padStart(4, '0')}`,
    description: `Description of product ${n}`,
    price: n,
    lastUpdatedUtc: '2026-10-18T00:00:00Z',
    ...overrides,
  }
}

const widget = product(1, { name: 'Blue Widget', sku: 'BW-0001', description: 'A small blue widget', price: 12.5 })
const gadget = product(2, { name: 'Red Gadget', sku: 'RG-0002', description: 'A large red gadget', price: 7 })

// A fake Products API over an in-memory list, recording every request it serves
function fakeProductsApi(initial: Product[] = [widget, gadget]) {
  const products = [...initial]
  const searches: URLSearchParams[] = []
  const posts: unknown[] = []
  const puts: { id: string; body: unknown }[] = []
  const deletes: string[] = []

  server.use(
    http.get('/api/products', ({ request }) => {
      const params = new URL(request.url).searchParams
      searches.push(params)
      const term = params.get('searchTerm')?.toLowerCase()
      const page = Number(params.get('page') ?? 1)
      const pageSize = Number(params.get('pageSize') ?? 25)
      const matches = term
        ? products.filter((p) => [p.name, p.sku, p.description].some((field) => field.toLowerCase().includes(term)))
        : products
      return envelope({
        items: matches.slice((page - 1) * pageSize, page * pageSize),
        totalItems: matches.length,
        page,
        pageSize,
        totalPages: Math.ceil(matches.length / pageSize),
      })
    }),
    http.post('/api/products', async ({ request }) => {
      const body = (await request.json()) as Omit<Product, 'id' | 'lastUpdatedUtc'>
      posts.push(body)
      const added = product(products.length + 100, body)
      products.unshift(added)
      return envelope(added.id, 201)
    }),
    http.put('/api/products/:id', async ({ params, request }) => {
      const body = (await request.json()) as Pick<Product, 'name' | 'description' | 'price'>
      const id = params.id as string
      puts.push({ id, body })
      const index = products.findIndex((p) => p.id === id)
      products[index] = { ...products[index], ...body }
      return envelope(null)
    }),
    http.delete('/api/products/:id', ({ params }) => {
      const id = params.id as string
      deletes.push(id)
      products.splice(
        products.findIndex((p) => p.id === id),
        1,
      )
      return new HttpResponse(null, { status: 204 })
    }),
  )
  return { searches, posts, puts, deletes }
}

const lastSearch = (searches: URLSearchParams[]) => searches[searches.length - 1]

async function rowOf(name: string) {
  return (await screen.findByRole('cell', { name })).closest('tr')!
}

async function fillAddForm(values: { name: string; sku: string; description: string; price: string }) {
  const user = userEvent.setup()
  const dialog = within(await screen.findByRole('dialog', { name: 'Add Product' }))
  await user.type(dialog.getByLabelText(/^Name/), values.name)
  await user.type(dialog.getByLabelText(/^SKU/), values.sku)
  await user.type(dialog.getByLabelText(/^Description/), values.description)
  await user.type(dialog.getByLabelText(/^Price/), values.price)
  return dialog
}

const validNew = { name: 'Test Widget', sku: 'TS-ABC123', description: 'A sturdy test widget', price: '9.5' }

describe('ProductsScreen', () => {
  it('lists the first page of Products in a table', async () => {
    const { searches } = fakeProductsApi()

    renderWithProviders(<ProductsScreen />)

    expect(screen.getByRole('heading', { name: 'Products' })).toBeInTheDocument()
    expect(screen.getByRole('textbox', { name: 'Search' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Add Product' })).toBeInTheDocument()
    const row = within(await rowOf('Blue Widget'))
    for (const header of ['Name', 'SKU', 'Description', 'Price']) {
      expect(screen.getByRole('columnheader', { name: header })).toBeInTheDocument()
    }
    expect(row.getByRole('cell', { name: 'BW-0001' })).toBeInTheDocument()
    expect(row.getByRole('cell', { name: 'A small blue widget' })).toBeInTheDocument()
    expect(row.getByRole('cell', { name: '12.50' })).toBeInTheDocument()
    expect(row.getByRole('button', { name: 'Edit' })).toBeInTheDocument()
    expect(row.getByRole('button', { name: 'Remove' })).toBeInTheDocument()
    expect(screen.getByRole('cell', { name: '7.00' })).toBeInTheDocument()
    expect(searches).toHaveLength(1)
    expect(Object.fromEntries(searches[0])).toEqual({ page: '1', pageSize: '25' })
  })

  it('sends the debounced search term and shows the matching Products', async () => {
    const { searches } = fakeProductsApi()
    renderWithProviders(<ProductsScreen />)
    await rowOf('Blue Widget')

    await userEvent.setup().type(screen.getByRole('textbox', { name: 'Search' }), '  gadget ')

    await waitFor(() => expect(screen.queryByRole('cell', { name: 'Blue Widget' })).not.toBeInTheDocument())
    expect(screen.getByRole('cell', { name: 'Red Gadget' })).toBeInTheDocument()
    expect(lastSearch(searches).get('searchTerm')).toBe('gadget')
    // Debounced: the intermediate keystrokes never reach the API
    expect(searches.map((s) => s.get('searchTerm'))).toEqual([null, 'gadget'])
  })

  it('shows "No Products found." when nothing matches', async () => {
    const { searches } = fakeProductsApi()
    renderWithProviders(<ProductsScreen />)
    await rowOf('Blue Widget')

    await userEvent.setup().type(screen.getByRole('textbox', { name: 'Search' }), 'nothing like it')

    expect(await screen.findByText('No Products found.')).toBeInTheDocument()
    expect(lastSearch(searches).get('searchTerm')).toBe('nothing like it')
  })

  it('sends no searchTerm for a whitespace-only term', async () => {
    const { searches } = fakeProductsApi()
    renderWithProviders(<ProductsScreen />)
    await rowOf('Blue Widget')

    await userEvent.setup().type(screen.getByRole('textbox', { name: 'Search' }), '   ')
    await new Promise((resolve) => setTimeout(resolve, 400))

    expect(searches.every((s) => !s.has('searchTerm'))).toBe(true)
  })

  it('pages through more than 25 Products and goes back to page 1 for a new term', async () => {
    const { searches } = fakeProductsApi(Array.from({ length: 30 }, (_, i) => product(i + 1)))
    const user = userEvent.setup()
    renderWithProviders(<ProductsScreen />)
    await rowOf('Product 1')
    expect(screen.queryByRole('cell', { name: 'Product 26' })).not.toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: '2' }))

    expect(await screen.findByRole('cell', { name: 'Product 26' })).toBeInTheDocument()
    expect(screen.queryByRole('cell', { name: 'Product 1' })).not.toBeInTheDocument()
    expect(lastSearch(searches).get('page')).toBe('2')

    await user.type(screen.getByRole('textbox', { name: 'Search' }), 'Product')

    await waitFor(() => expect(lastSearch(searches).get('searchTerm')).toBe('Product'))
    expect(lastSearch(searches).get('page')).toBe('1')
    expect(await screen.findByRole('cell', { name: 'Product 1' })).toBeInTheDocument()

    // Clearing the term is a new term too: it doesn't bring back the page chosen before
    await user.clear(screen.getByRole('textbox', { name: 'Search' }))

    await waitFor(() => expect(lastSearch(searches).has('searchTerm')).toBe(false))
    expect(lastSearch(searches).get('page')).toBe('1')
    expect(searches.filter((s) => !s.has('searchTerm')).map((s) => s.get('page'))).toEqual(['1', '2', '1'])
  })

  it('hides the pagination control when there is one page', async () => {
    fakeProductsApi()

    renderWithProviders(<ProductsScreen />)

    await rowOf('Blue Widget')
    expect(screen.queryByRole('button', { name: '1' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: '2' })).not.toBeInTheDocument()
  })

  it('shows the error message when the list fails to load', async () => {
    server.use(http.get('/api/products', () => failure('minPrice cannot be greater than maxPrice')))

    renderWithProviders(<ProductsScreen />)

    expect(await screen.findByRole('alert')).toHaveTextContent('minPrice cannot be greater than maxPrice')
  })

  describe('Add', () => {
    it('requires every field and sends no request', async () => {
      const { posts } = fakeProductsApi()
      const user = userEvent.setup()
      renderWithProviders(<ProductsScreen />)

      await user.click(screen.getByRole('button', { name: 'Add Product' }))
      const dialog = within(await screen.findByRole('dialog', { name: 'Add Product' }))
      expect(dialog.getByText('e.g. TS-3FA7C1')).toBeInTheDocument()
      await user.click(dialog.getByRole('button', { name: 'Save' }))

      for (const field of ['Name', 'SKU', 'Description', 'Price']) {
        expect(await dialog.findByText(`${field} is required`)).toBeInTheDocument()
      }
      expect(posts).toHaveLength(0)
    })

    it('posts the Product, closes the modal and shows the new Product', async () => {
      const { posts } = fakeProductsApi()
      const user = userEvent.setup()
      renderWithProviders(<ProductsScreen />)
      await rowOf('Blue Widget')

      await user.click(screen.getByRole('button', { name: 'Add Product' }))
      const dialog = await fillAddForm(validNew)
      await user.click(dialog.getByRole('button', { name: 'Save' }))

      await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
      expect(posts).toEqual([{ name: 'Test Widget', sku: 'TS-ABC123', description: 'A sturdy test widget', price: 9.5 }])
      const row = within(await rowOf('Test Widget'))
      expect(row.getByRole('cell', { name: '9.50' })).toBeInTheDocument()
    })

    it.each([['Invalid SKU format'], ['SKU TS-ABC123 is already in use']])(
      'keeps the modal open with the entered values and shows "%s"',
      async (message) => {
        fakeProductsApi()
        server.use(http.post('/api/products', () => failure(message)))
        const user = userEvent.setup()
        renderWithProviders(<ProductsScreen />)

        await user.click(screen.getByRole('button', { name: 'Add Product' }))
        const dialog = await fillAddForm(validNew)
        await user.click(dialog.getByRole('button', { name: 'Save' }))

        expect(await dialog.findByRole('alert')).toHaveTextContent(message)
        expect(dialog.getByLabelText(/^Name/)).toHaveValue('Test Widget')
        expect(dialog.getByLabelText(/^SKU/)).toHaveValue('TS-ABC123')
        expect(dialog.getByLabelText(/^Description/)).toHaveValue('A sturdy test widget')
        expect(dialog.getByLabelText(/^Price/)).toHaveValue('9.5')
      },
    )

    it('shows a generic message when the failure has no message', async () => {
      fakeProductsApi()
      server.use(http.post('/api/products', () => new HttpResponse(null, { status: 500 })))
      const user = userEvent.setup()
      renderWithProviders(<ProductsScreen />)

      await user.click(screen.getByRole('button', { name: 'Add Product' }))
      const dialog = await fillAddForm(validNew)
      await user.click(dialog.getByRole('button', { name: 'Save' }))

      expect(await dialog.findByRole('alert')).toHaveTextContent('Saving the Product failed. Try again.')
    })

    it('shows the loading state while saving and clears the error on a new submit', async () => {
      fakeProductsApi()
      let attempts = 0
      server.use(
        http.post('/api/products', async () => {
          await delay(100)
          return ++attempts === 1 ? failure('Invalid SKU format') : envelope('new-id', 201)
        }),
      )
      const user = userEvent.setup()
      renderWithProviders(<ProductsScreen />)

      await user.click(screen.getByRole('button', { name: 'Add Product' }))
      const dialog = await fillAddForm(validNew)
      await user.click(dialog.getByRole('button', { name: 'Save' }))
      expect(dialog.getByRole('button', { name: 'Save' })).toHaveAttribute('data-loading')
      expect(await dialog.findByRole('alert')).toHaveTextContent('Invalid SKU format')

      await user.click(dialog.getByRole('button', { name: 'Save' }))

      expect(dialog.queryByRole('alert')).not.toBeInTheDocument()
      await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    })
  })

  describe('Edit', () => {
    it('prefills the form, puts the changes, closes the modal and shows the new name', async () => {
      const { puts } = fakeProductsApi()
      const user = userEvent.setup()
      renderWithProviders(<ProductsScreen />)

      await user.click(within(await rowOf('Blue Widget')).getByRole('button', { name: 'Edit' }))
      const dialog = within(await screen.findByRole('dialog', { name: 'Edit Product' }))
      expect(dialog.getByLabelText(/^Name/)).toHaveValue('Blue Widget')
      expect(dialog.getByLabelText(/^SKU/)).toHaveValue('BW-0001')
      expect(dialog.getByLabelText(/^SKU/)).toBeDisabled()
      expect(dialog.getByLabelText(/^Description/)).toHaveValue('A small blue widget')
      expect(dialog.getByLabelText(/^Price/)).toHaveValue('12.5')

      await user.clear(dialog.getByLabelText(/^Name/))
      await user.type(dialog.getByLabelText(/^Name/), 'Navy Widget')
      await user.click(dialog.getByRole('button', { name: 'Save' }))

      await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
      expect(puts).toEqual([
        { id: widget.id, body: { name: 'Navy Widget', description: 'A small blue widget', price: 12.5 } },
      ])
      expect(await screen.findByRole('cell', { name: 'Navy Widget' })).toBeInTheDocument()
      expect(screen.queryByRole('cell', { name: 'Blue Widget' })).not.toBeInTheDocument()
    })

    it('requires every field and sends no request', async () => {
      const { puts } = fakeProductsApi()
      const user = userEvent.setup()
      renderWithProviders(<ProductsScreen />)

      await user.click(within(await rowOf('Blue Widget')).getByRole('button', { name: 'Edit' }))
      const dialog = within(await screen.findByRole('dialog', { name: 'Edit Product' }))
      await user.clear(dialog.getByLabelText(/^Name/))
      await user.clear(dialog.getByLabelText(/^Description/))
      await user.clear(dialog.getByLabelText(/^Price/))
      await user.click(dialog.getByRole('button', { name: 'Save' }))

      for (const field of ['Name', 'Description', 'Price']) {
        expect(await dialog.findByText(`${field} is required`)).toBeInTheDocument()
      }
      expect(puts).toHaveLength(0)
    })

    it("keeps the modal open and shows the API's validation message", async () => {
      fakeProductsApi()
      server.use(http.put('/api/products/:id', () => failure('Product name must be at least 3 characters')))
      const user = userEvent.setup()
      renderWithProviders(<ProductsScreen />)

      await user.click(within(await rowOf('Blue Widget')).getByRole('button', { name: 'Edit' }))
      const dialog = within(await screen.findByRole('dialog', { name: 'Edit Product' }))
      await user.clear(dialog.getByLabelText(/^Name/))
      await user.type(dialog.getByLabelText(/^Name/), 'ab')
      await user.click(dialog.getByRole('button', { name: 'Save' }))

      expect(await dialog.findByRole('alert')).toHaveTextContent('Product name must be at least 3 characters')
      expect(dialog.getByLabelText(/^Name/)).toHaveValue('ab')
    })
  })

  describe('Product removal', () => {
    it('cancels without a request', async () => {
      const { deletes } = fakeProductsApi()
      const user = userEvent.setup()
      renderWithProviders(<ProductsScreen />)

      await user.click(within(await rowOf('Blue Widget')).getByRole('button', { name: 'Remove' }))
      const dialog = within(await screen.findByRole('dialog', { name: 'Remove Product' }))
      expect(dialog.getByText('Remove Blue Widget?')).toBeInTheDocument()
      await user.click(dialog.getByRole('button', { name: 'Cancel' }))

      await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
      expect(deletes).toHaveLength(0)
      expect(screen.getByRole('cell', { name: 'Blue Widget' })).toBeInTheDocument()
    })

    it('deletes the Product, closes the modal and drops the row', async () => {
      const { deletes } = fakeProductsApi()
      const user = userEvent.setup()
      renderWithProviders(<ProductsScreen />)

      await user.click(within(await rowOf('Blue Widget')).getByRole('button', { name: 'Remove' }))
      const dialog = within(await screen.findByRole('dialog', { name: 'Remove Product' }))
      await user.click(dialog.getByRole('button', { name: 'Remove' }))

      await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
      expect(deletes).toEqual([widget.id])
      await waitFor(() => expect(screen.queryByRole('cell', { name: 'Blue Widget' })).not.toBeInTheDocument())
      expect(screen.getByRole('cell', { name: 'Red Gadget' })).toBeInTheDocument()
    })

    it.each([
      [404, 'This Product no longer exists.'],
      [500, 'Removing the Product failed. Try again.'],
    ])('shows the failure for a %i in the confirmation', async (status, message) => {
      fakeProductsApi()
      server.use(http.delete('/api/products/:id', () => new HttpResponse(null, { status })))
      const user = userEvent.setup()
      renderWithProviders(<ProductsScreen />)

      await user.click(within(await rowOf('Blue Widget')).getByRole('button', { name: 'Remove' }))
      const dialog = within(await screen.findByRole('dialog', { name: 'Remove Product' }))
      await user.click(dialog.getByRole('button', { name: 'Remove' }))

      expect(await dialog.findByRole('alert')).toHaveTextContent(message)
      expect(screen.getByRole('dialog', { name: 'Remove Product' })).toBeInTheDocument()
    })
  })
})
