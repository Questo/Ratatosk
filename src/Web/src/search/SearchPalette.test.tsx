import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { delay, http, HttpResponse } from 'msw'
import { describe, expect, it, vi } from 'vitest'
import { renderWithProviders } from '../test/render'
import { server } from '../test/server'
import type { SearchGroup, SearchHit } from './search'
import { SearchPalette } from './SearchPalette'

const widget: SearchHit = { id: 'p-1', title: 'Search Widget', subtitle: 'SW-0001 · 12.50', kind: 'product' }
const gadget: SearchHit = { id: 'p-2', title: 'Widget Gadget', subtitle: 'WG-0002 · 3.00', kind: 'product' }
const order: SearchHit = { id: 'o-1', title: '1a2b3c4d', subtitle: 'Confirmed · 1 line · 2026-10-18', kind: 'order' }

const products = (hits: SearchHit[], failed = false): SearchGroup => ({ kind: 'product', label: 'Products', hits, failed })
const orders = (hits: SearchHit[], failed = false): SearchGroup => ({ kind: 'order', label: 'Orders', hits, failed })
const results = (...groups: SearchGroup[]) =>
  HttpResponse.json({ message: null, data: { groups }, timestamp: new Date().toISOString() })

// Serves /api/search from a function of the term and records every term asked for
function fakeSearch(respond: (term: string) => Response | Promise<Response>) {
  const terms: string[] = []
  server.use(
    http.get('/api/search', ({ request }) => {
      const term = new URL(request.url).searchParams.get('q') ?? ''
      terms.push(term)
      return respond(term)
    }),
  )
  return terms
}

function renderPalette() {
  const onSelect = vi.fn()
  renderWithProviders(<SearchPalette onSelect={onSelect} />)
  return { onSelect, user: userEvent.setup() }
}

async function openAndType(user: ReturnType<typeof userEvent.setup>, text: string) {
  await user.click(screen.getByRole('button', { name: /^Search\.\.\./ }))
  const input = await screen.findByRole('combobox', { name: 'Search Products and Orders' })
  await user.type(input, text)
  return input
}

describe('SearchPalette', () => {
  it('opens with Ctrl+K with the input focused, and closes with Escape', async () => {
    const { user } = renderPalette()

    await user.keyboard('{Control>}k{/Control}')

    const dialog = await screen.findByRole('dialog', { name: 'Search' })
    await waitFor(() => expect(within(dialog).getByRole('combobox')).toHaveFocus())

    await user.keyboard('{Escape}')
    await waitFor(() => expect(screen.queryByRole('dialog', { name: 'Search' })).not.toBeInTheDocument())
  })

  it('opens with "/" when no input is focused, but not when "/" is typed into an input', async () => {
    const onSelect = vi.fn()
    renderWithProviders(
      <>
        <input aria-label="Other field" />
        <SearchPalette onSelect={onSelect} />
      </>,
    )
    const user = userEvent.setup()

    await user.type(screen.getByLabelText('Other field'), '/')
    expect(screen.getByLabelText('Other field')).toHaveValue('/')
    expect(screen.queryByRole('dialog', { name: 'Search' })).not.toBeInTheDocument()

    await user.click(document.body)
    await user.keyboard('/')
    const input = await screen.findByRole('combobox', { name: 'Search Products and Orders' })
    // The hotkey's keypress isn't typed into the palette
    expect(input).toHaveValue('')
  })

  it('shows the hint under 2 characters and sends no request', async () => {
    const terms = fakeSearch(() => results(products([widget]), orders([])))
    const { user } = renderPalette()

    expect(screen.getByRole('button', { name: 'Search' })).toBeInTheDocument()
    await openAndType(user, ' w ')

    expect(screen.getByText('Type at least 2 characters to search Products and Orders')).toBeInTheDocument()
    await new Promise((resolve) => setTimeout(resolve, 400))
    expect(terms).toEqual([])
  })

  it('shows hits under their group headings and hides groups without hits', async () => {
    const terms = fakeSearch(() => results(products([widget, gadget]), orders([])))
    const { user } = renderPalette()

    const input = await openAndType(user, 'widget')

    const productsGroup = await screen.findByRole('group', { name: 'Products' })
    expect(within(productsGroup).getByRole('heading', { name: 'Products' })).toBeInTheDocument()
    const options = within(productsGroup).getAllByRole('option')
    expect(options).toHaveLength(2)
    expect(options[0]).toHaveTextContent('Search Widget')
    expect(options[0]).toHaveTextContent('SW-0001 · 12.50')
    expect(screen.queryByRole('group', { name: 'Orders' })).not.toBeInTheDocument()
    expect(screen.getByRole('listbox')).toHaveAttribute('id', input.getAttribute('aria-controls'))
    expect(input).toHaveAttribute('aria-expanded', 'true')
    expect(terms).toEqual(['widget'])
  })

  it('says when nothing matches', async () => {
    fakeSearch(() => results(products([]), orders([])))
    const { user } = renderPalette()

    const input = await openAndType(user, 'zzzz')

    expect(await screen.findByText('No matches for "zzzz"')).toBeInTheDocument()
    expect(input).toHaveAttribute('aria-expanded', 'false')
  })

  it('shows an error with a Retry that recovers', async () => {
    let fail = true
    fakeSearch(() =>
      fail
        ? HttpResponse.json({ message: 'Search is down.' }, { status: 500 })
        : results(products([widget]), orders([])),
    )
    const { user } = renderPalette()

    await openAndType(user, 'widget')

    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent('Search is down.')
    fail = false
    await user.click(within(alert).getByRole('button', { name: 'Retry' }))

    expect(await screen.findByRole('option', { name: /Search Widget/ })).toBeInTheDocument()
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it("says which group couldn't be searched", async () => {
    fakeSearch(() => results(products([widget]), orders([], true)))
    const { user } = renderPalette()

    await openAndType(user, 'widget')

    const ordersGroup = await screen.findByRole('group', { name: 'Orders' })
    expect(within(ordersGroup).getByText("Couldn't search Orders.")).toBeInTheDocument()
    expect(screen.getByRole('option', { name: /Search Widget/ })).toBeInTheDocument()
  })

  it('moves the highlight with Down and Up across groups, stopping at the ends, and selects with Enter', async () => {
    fakeSearch(() => results(products([widget, gadget]), orders([order])))
    const { user, onSelect } = renderPalette()

    const input = await openAndType(user, 'wi')
    const first = await screen.findByRole('option', { name: /Search Widget/ })
    const second = screen.getByRole('option', { name: /Widget Gadget/ })
    const third = screen.getByRole('option', { name: /1a2b3c4d/ })

    expect(first).toHaveAttribute('aria-selected', 'true')
    expect(input).toHaveAttribute('aria-activedescendant', first.id)

    await user.keyboard('{ArrowUp}')
    expect(first).toHaveAttribute('aria-selected', 'true')

    await user.keyboard('{ArrowDown}{ArrowDown}{ArrowDown}')
    expect(third).toHaveAttribute('aria-selected', 'true')
    expect(first).toHaveAttribute('aria-selected', 'false')
    expect(input).toHaveAttribute('aria-activedescendant', third.id)

    await user.keyboard('{ArrowUp}')
    expect(second).toHaveAttribute('aria-selected', 'true')
    expect(input).toHaveAttribute('aria-activedescendant', second.id)

    await user.keyboard('{Enter}')
    expect(onSelect).toHaveBeenCalledWith(gadget)
    await waitFor(() => expect(screen.queryByRole('dialog', { name: 'Search' })).not.toBeInTheDocument())
  })

  it('selects a clicked hit and clears the input for next time', async () => {
    fakeSearch(() => results(products([widget]), orders([order])))
    const { user, onSelect } = renderPalette()

    await openAndType(user, 'wi')
    await user.click(await screen.findByRole('option', { name: /1a2b3c4d/ }))

    expect(onSelect).toHaveBeenCalledWith(order)
    await waitFor(() => expect(screen.queryByRole('dialog', { name: 'Search' })).not.toBeInTheDocument())
    await user.keyboard('{Control>}k{/Control}')
    expect(await screen.findByRole('combobox', { name: 'Search Products and Orders' })).toHaveValue('')
  })

  it("never shows an older term's slow response over a newer term's results", async () => {
    let slowAnswered = false
    const terms = fakeSearch(async (term) => {
      if (term === 'wi') {
        await delay(600)
        slowAnswered = true
        return results(products([gadget]), orders([]))
      }
      return results(products([widget]), orders([]))
    })
    const { user } = renderPalette()

    const input = await openAndType(user, 'wi')
    await waitFor(() => expect(terms).toEqual(['wi']))
    await user.type(input, 'd')

    expect(await screen.findByRole('option', { name: /Search Widget/ })).toBeInTheDocument()
    await waitFor(() => expect(slowAnswered).toBe(true), { timeout: 2000 })
    await new Promise((resolve) => setTimeout(resolve, 50))
    expect(screen.getByRole('option', { name: /Search Widget/ })).toBeInTheDocument()
    expect(screen.queryByRole('option', { name: /Widget Gadget/ })).not.toBeInTheDocument()
    expect(terms).toEqual(['wi', 'wid'])
  })
})
