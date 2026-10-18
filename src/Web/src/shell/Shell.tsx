import { useState } from 'react'
import { AppShell, Burger, Button, Group, NavLink, Text, Title } from '@mantine/core'
import { useDisclosure } from '@mantine/hooks'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { api } from '../api/client'
import { sessionQuery } from '../auth/session'
import { useChangeStream } from '../changes/useChangeStream'
import { OrdersScreen } from '../orders/OrdersScreen'
import { ProductsScreen } from '../products/ProductsScreen'
import { SearchPalette } from '../search/SearchPalette'
import type { OpenEntityRequest, SearchHit } from '../search/search'
import { StockScreen } from '../stock/StockScreen'

type Screen = 'products' | 'stock' | 'orders'

// Which screen opens a search hit of each kind; a hit of any other kind is ignored
const searchTargets: Record<OpenEntityRequest['kind'], Screen> = {
  product: 'products',
  order: 'orders',
}

function isSearchTarget(kind: string): kind is OpenEntityRequest['kind'] {
  return Object.hasOwn(searchTargets, kind)
}

export function Shell({ email }: { email: string }) {
  const queryClient = useQueryClient()
  // One change stream for the whole signed-in session; it closes when sign-out unmounts the shell
  useChangeStream()
  const [screen, setScreen] = useState<Screen>('products')
  // Below the sm breakpoint the navbar covers the page, so it stays collapsed until the burger opens it
  const [navOpened, nav] = useDisclosure(false)
  // The entity a search hit asked to open; cleared on navigation so a screen shown again doesn't reopen it
  const [openRequest, setOpenRequest] = useState<OpenEntityRequest | null>(null)
  const show = (next: Screen) => {
    setScreen(next)
    setOpenRequest(null)
    nav.close()
  }
  const openHit = (hit: SearchHit) => {
    if (!isSearchTarget(hit.kind)) return
    setScreen(searchTargets[hit.kind])
    setOpenRequest({ kind: hit.kind, id: hit.id, seq: (openRequest?.seq ?? 0) + 1 })
    nav.close()
  }

  const signOut = useMutation({
    mutationFn: () => api.post('/api/auth/logout'),
    onSuccess: () => {
      // Signing out first re-renders the app onto the login screen; clear() alone notifies no one
      queryClient.setQueryData(sessionQuery.queryKey, { status: 'signedOut' })
      queryClient.clear()
    },
  })

  return (
    <AppShell
      header={{ height: 60 }}
      navbar={{ width: 200, breakpoint: 'sm', collapsed: { mobile: !navOpened } }}
      padding="md"
    >
      <AppShell.Header>
        <Group h="100%" px="md" justify="space-between" wrap="nowrap">
          <Group wrap="nowrap" miw={0}>
            <Burger
              opened={navOpened}
              onClick={nav.toggle}
              hiddenFrom="sm"
              size="sm"
              aria-label="Toggle navigation"
              aria-expanded={navOpened}
            />
            <Title order={3} lineClamp={1}>
              Ratatosk back office
            </Title>
          </Group>
          <SearchPalette onSelect={openHit} />
          <Group wrap="nowrap">
            <Text size="sm" visibleFrom="sm">
              {email}
            </Text>
            <Button variant="default" loading={signOut.isPending} onClick={() => signOut.mutate()}>
              Sign out
            </Button>
          </Group>
        </Group>
      </AppShell.Header>
      <AppShell.Navbar p="xs">
        <NavLink
          component="button"
          label="Products"
          active={screen === 'products'}
          aria-current={screen === 'products' ? 'page' : undefined}
          onClick={() => show('products')}
        />
        <NavLink
          component="button"
          label="Stock"
          active={screen === 'stock'}
          aria-current={screen === 'stock' ? 'page' : undefined}
          onClick={() => show('stock')}
        />
        <NavLink
          component="button"
          label="Orders"
          active={screen === 'orders'}
          aria-current={screen === 'orders' ? 'page' : undefined}
          onClick={() => show('orders')}
        />
      </AppShell.Navbar>
      <AppShell.Main>
        {screen === 'products' ? (
          <ProductsScreen openRequest={openRequest?.kind === 'product' ? openRequest : null} />
        ) : screen === 'stock' ? (
          <StockScreen />
        ) : (
          <OrdersScreen openRequest={openRequest?.kind === 'order' ? openRequest : null} />
        )}
      </AppShell.Main>
    </AppShell>
  )
}
