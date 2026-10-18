import { expect, test } from '@playwright/test'

function requiredEnv(name: string): string {
  const value = process.env[name]
  if (!value) throw new Error(`${name} is not set: set it to the seeded Admin's value from the root .env`)
  return value
}

// Stock rows are ordered by SKU, 25 per page. 'AA-' plus a suffix counting down from the largest
// 8-character base-36 number sorts every run ahead of earlier ones, so the new row is always on page 1.
function newSku(): string {
  const suffix = (36 ** 8 - 1 - Date.now()).toString(36).toUpperCase().padStart(8, '0')
  return `AA-${suffix}`
}

test('an Order placed through the API shows up Confirmed in the Orders screen', async ({ page, playwright, baseURL }) => {
  const email = requiredEnv('ADMIN_EMAIL')
  const password = requiredEnv('ADMIN_PASSWORD')
  const sku = newSku()
  const name = `Smoke ${sku}`
  const quantity = 5
  const ordered = 2

  await page.goto('/')
  await page.getByLabel('Email').fill(email)
  await page.getByRole('textbox', { name: 'Password' }).fill(password)
  await page.getByRole('button', { name: 'Sign in' }).click()
  await expect(page.getByRole('banner').getByText(email)).toBeVisible()
  await expect(page.getByRole('heading', { name: 'Dashboard' })).toBeVisible()

  await page.getByRole('button', { name: 'Products' }).click()
  await page.getByRole('button', { name: 'Add Product' }).click()
  const addDialog = page.getByRole('dialog', { name: 'Add Product' })
  await addDialog.getByLabel('Name').fill(name)
  await addDialog.getByLabel('SKU').fill(sku)
  await addDialog.getByLabel('Description').fill('Created by the Playwright smoke test')
  await addDialog.getByLabel('Price').fill('9.99')
  await addDialog.getByRole('button', { name: 'Save' }).click()
  await expect(addDialog).toBeHidden()
  await expect(page.getByRole('row').filter({ hasText: sku })).toContainText(name)

  // The header's search palette finds the new Product and opens its edit dialog
  await page.keyboard.press('ControlOrMeta+K')
  await page.getByRole('combobox', { name: 'Search Products and Orders' }).fill(sku)
  await page.getByRole('option', { name: new RegExp(name) }).click()
  const editDialog = page.getByRole('dialog', { name: 'Edit Product' })
  await expect(editDialog.getByLabel('Name')).toHaveValue(name)
  await page.keyboard.press('Escape')
  await expect(editDialog).toBeHidden()

  await page.getByRole('button', { name: 'Stock' }).click()
  const stockRow = page.getByRole('row').filter({ has: page.getByRole('cell', { name: sku, exact: true }) })
  await stockRow.getByRole('button', { name: 'Restock' }).click()
  const restockDialog = page.getByRole('dialog', { name: `Restock ${sku}` })
  await restockDialog.getByLabel('Quantity').fill(String(quantity))
  await restockDialog.getByRole('button', { name: 'Restock' }).click()
  await expect(restockDialog).toBeHidden()
  // Columns: SKU, On hand, Reserved, Available, Unit, actions
  await expect(stockRow.getByRole('cell').nth(1)).toHaveText(String(quantity))
  await expect(stockRow.getByRole('cell').nth(3)).toHaveText(String(quantity))

  await page.getByRole('button', { name: 'Orders' }).click()
  await expect(page.getByRole('heading', { name: 'Orders' })).toBeVisible()

  // Its own request context with a Bearer token, so the page's cookies play no part
  const request = await playwright.request.newContext({ baseURL })
  const login = await request.post('/api/auth/login', { data: { email, password } })
  expect(login.ok()).toBe(true)
  const { accessToken } = (await login.json()).data as { accessToken: string }
  const placed = await request.post('/api/orders', {
    headers: { Authorization: `Bearer ${accessToken}` },
    data: { lines: [{ sku, quantity: ordered }] },
  })
  expect(placed.status()).toBe(202)
  const orderId = (await placed.json()).data as string
  await request.dispose()

  // No reload: the change stream is what refreshes the list
  const orderRow = page.getByRole('row').filter({ has: page.getByRole('cell', { name: orderId, exact: true }) })
  await expect(orderRow.getByText('Confirmed', { exact: true })).toBeVisible({ timeout: 15_000 })
})
