import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { App } from './App'
import { FakeEventSource } from './test/eventSource'
import { renderWithProviders } from './test/render'
import { server } from './test/server'

const unauthorized = () => HttpResponse.json({ message: 'Unauthorized.' }, { status: 401 })
const me = (email: string, role: string) =>
  HttpResponse.json({ message: null, data: { email, role }, timestamp: new Date().toISOString() })
const emptyPage = () =>
  HttpResponse.json({
    message: null,
    data: { items: [], totalItems: 0, page: 1, pageSize: 25, totalPages: 0 },
    timestamp: new Date().toISOString(),
  })
const ok = () => HttpResponse.json({ message: null, data: {}, timestamp: new Date().toISOString() })

// A fake API: /me reflects whoever logged in last; logout and failed refreshes clear it.
// The Admin shell loads Products and, from the navbar, Stock and Orders, so it serves an empty list of each.
function fakeAuthApi(options: { signedInAs?: { email: string; role: string }; password?: string } = {}) {
  let user = options.signedInAs ?? null
  const calls = { login: 0, logout: 0 }
  const accounts: Record<string, string> = {
    'admin@factory.test': 'Admin',
    'user@factory.test': 'User',
  }

  server.use(
    http.get('/api/auth/me', () => (user ? me(user.email, user.role) : unauthorized())),
    http.post('/api/auth/refresh', unauthorized),
    http.post('/api/auth/login', async ({ request }) => {
      calls.login++
      const body = (await request.json()) as { email: string; password: string }
      const role = accounts[body.email]
      if (!role || body.password !== (options.password ?? 'Secret123!')) return unauthorized()
      user = { email: body.email, role }
      return ok()
    }),
    http.post('/api/auth/logout', () => {
      calls.logout++
      user = null
      return new HttpResponse(null, { status: 204 })
    }),
    http.get('/api/products', emptyPage),
    http.get('/api/inventory', emptyPage),
    http.get('/api/orders', emptyPage),
  )
  return calls
}

async function signIn(email: string, password: string) {
  const user = userEvent.setup()
  await user.type(await screen.findByLabelText(/email/i), email)
  await user.type(screen.getByLabelText(/password/i, { selector: 'input' }), password)
  await user.click(screen.getByRole('button', { name: 'Sign in' }))
}

describe('App', () => {
  it('shows the login screen when /auth/me returns 401', async () => {
    fakeAuthApi()

    renderWithProviders(<App />)

    expect(await screen.findByRole('button', { name: 'Sign in' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Ratatosk back office' })).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: /sign up/i })).not.toBeInTheDocument()
  })

  it('shows the shell with the email when /auth/me returns an Admin', async () => {
    fakeAuthApi({ signedInAs: { email: 'admin@factory.test', role: 'Admin' } })

    renderWithProviders(<App />)

    expect(await screen.findByText('admin@factory.test')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Sign out' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Products' })).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Welcome' })).not.toBeInTheDocument()
  })

  it('switches between the Products and Stock screens from the navbar', async () => {
    fakeAuthApi({ signedInAs: { email: 'admin@factory.test', role: 'Admin' } })
    const user = userEvent.setup()
    renderWithProviders(<App />)

    const navbar = within(await screen.findByRole('navigation'))
    const products = navbar.getByRole('button', { name: 'Products' })
    const stock = navbar.getByRole('button', { name: 'Stock' })
    expect(products).toHaveAttribute('data-active')
    expect(products).toHaveAttribute('aria-current', 'page')
    expect(stock).not.toHaveAttribute('data-active')
    expect(stock).not.toHaveAttribute('aria-current')
    expect(screen.getByRole('heading', { name: 'Products' })).toBeInTheDocument()

    await user.click(stock)

    expect(await screen.findByRole('heading', { name: 'Stock' })).toBeInTheDocument()
    expect(await screen.findByText('No Stock records found.')).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Products' })).not.toBeInTheDocument()
    expect(stock).toHaveAttribute('data-active')
    expect(stock).toHaveAttribute('aria-current', 'page')
    expect(products).not.toHaveAttribute('data-active')

    await user.click(products)

    expect(await screen.findByRole('heading', { name: 'Products' })).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Stock' })).not.toBeInTheDocument()
    expect(products).toHaveAttribute('data-active')
  })

  it('shows the Orders screen from the navbar', async () => {
    fakeAuthApi({ signedInAs: { email: 'admin@factory.test', role: 'Admin' } })
    const user = userEvent.setup()
    renderWithProviders(<App />)

    const navbar = within(await screen.findByRole('navigation'))
    expect(navbar.getAllByRole('button').map((link) => link.textContent)).toEqual(['Products', 'Stock', 'Orders'])
    const orders = navbar.getByRole('button', { name: 'Orders' })

    await user.click(orders)

    expect(await screen.findByRole('heading', { name: 'Orders' })).toBeInTheDocument()
    expect(await screen.findByText('No Orders found.')).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Products' })).not.toBeInTheDocument()
    expect(orders).toHaveAttribute('data-active')
    expect(orders).toHaveAttribute('aria-current', 'page')
  })

  it('keeps one change stream open while signed in and closes it on sign-out', async () => {
    fakeAuthApi({ signedInAs: { email: 'admin@factory.test', role: 'Admin' } })
    const user = userEvent.setup()
    renderWithProviders(<App />)
    await screen.findByText('admin@factory.test')

    expect(FakeEventSource.instances).toHaveLength(1)
    const stream = FakeEventSource.latest()
    expect(stream.url).toBe('/api/changes')
    expect(stream.closed).toBe(false)

    await user.click(within(screen.getByRole('navigation')).getByRole('button', { name: 'Stock' }))
    expect(FakeEventSource.instances).toHaveLength(1)

    await user.click(screen.getByRole('button', { name: 'Sign out' }))

    expect(await screen.findByRole('button', { name: 'Sign in' })).toBeInTheDocument()
    expect(stream.closed).toBe(true)
  })

  it('opens the navbar from the burger on small screens and closes it when a screen is chosen', async () => {
    fakeAuthApi({ signedInAs: { email: 'admin@factory.test', role: 'Admin' } })
    const user = userEvent.setup()
    renderWithProviders(<App />)

    const burger = await screen.findByRole('button', { name: 'Toggle navigation' })
    expect(burger).toHaveAttribute('aria-expanded', 'false')

    await user.click(burger)
    expect(burger).toHaveAttribute('aria-expanded', 'true')

    await user.click(within(screen.getByRole('navigation')).getByRole('button', { name: 'Stock' }))

    expect(await screen.findByRole('heading', { name: 'Stock' })).toBeInTheDocument()
    expect(burger).toHaveAttribute('aria-expanded', 'false')
  })

  it('logs a non-Admin session out on load and says so', async () => {
    const calls = fakeAuthApi({ signedInAs: { email: 'user@factory.test', role: 'User' } })

    renderWithProviders(<App />)

    expect(await screen.findByRole('alert')).toHaveTextContent("This account doesn't have Admin access.")
    expect(calls.logout).toBe(1)
  })

  it('shows "Invalid email or password." for bad credentials', async () => {
    fakeAuthApi()
    renderWithProviders(<App />)

    await signIn('admin@factory.test', 'wrong-password')

    expect(await screen.findByRole('alert')).toHaveTextContent('Invalid email or password.')
    expect(screen.queryByRole('button', { name: 'Sign out' })).not.toBeInTheDocument()
  })

  it("shows the API's message for other login failures", async () => {
    fakeAuthApi()
    server.use(
      http.post('/api/auth/login', () => HttpResponse.json({ message: 'Email is not valid.' }, { status: 400 })),
    )
    renderWithProviders(<App />)

    await signIn('admin@factory.test', 'Secret123!')

    expect(await screen.findByRole('alert')).toHaveTextContent('Email is not valid.')
  })

  it('shows a generic message when a failed login has no message', async () => {
    fakeAuthApi()
    server.use(http.post('/api/auth/login', () => new HttpResponse(null, { status: 500 })))
    renderWithProviders(<App />)

    await signIn('admin@factory.test', 'Secret123!')

    expect(await screen.findByRole('alert')).toHaveTextContent('Sign-in failed. Try again.')
  })

  it('logs a non-Admin out after login and shows the no Admin access message', async () => {
    const calls = fakeAuthApi()
    renderWithProviders(<App />)

    await signIn('user@factory.test', 'Secret123!')

    expect(await screen.findByRole('alert')).toHaveTextContent("This account doesn't have Admin access.")
    expect(calls.logout).toBe(1)
    expect(screen.queryByRole('button', { name: 'Sign out' })).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Sign in' })).toBeInTheDocument()
  })

  it('shows the shell after an Admin login', async () => {
    const calls = fakeAuthApi()
    renderWithProviders(<App />)

    await signIn('admin@factory.test', 'Secret123!')

    expect(await screen.findByText('admin@factory.test')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Sign out' })).toBeInTheDocument()
    expect(calls.logout).toBe(0)
  })

  it('signs out through logout and returns to the login screen', async () => {
    const calls = fakeAuthApi({ signedInAs: { email: 'admin@factory.test', role: 'Admin' } })
    renderWithProviders(<App />)

    await userEvent.setup().click(await screen.findByRole('button', { name: 'Sign out' }))

    expect(await screen.findByRole('button', { name: 'Sign in' })).toBeInTheDocument()
    expect(calls.logout).toBe(1)
    expect(screen.queryByText('admin@factory.test')).not.toBeInTheDocument()
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it('shows validation errors for empty fields and sends no request', async () => {
    const calls = fakeAuthApi()
    renderWithProviders(<App />)

    await userEvent.setup().click(await screen.findByRole('button', { name: 'Sign in' }))

    expect(await screen.findByText('Email is required')).toBeInTheDocument()
    expect(screen.getByText('Password is required')).toBeInTheDocument()
    expect(calls.login).toBe(0)
  })

  it('rejects an email that does not look like one and sends no request', async () => {
    const calls = fakeAuthApi()
    renderWithProviders(<App />)

    await signIn('not-an-email', 'Secret123!')

    expect(await screen.findByText('Enter a valid email')).toBeInTheDocument()
    expect(calls.login).toBe(0)
  })
})
