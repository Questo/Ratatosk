import { useState } from 'react'
import { AppShell, Button, Group, NavLink, Text, Title } from '@mantine/core'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { api } from '../api/client'
import { sessionQuery } from '../auth/session'
import { ProductsScreen } from '../products/ProductsScreen'
import { StockScreen } from '../stock/StockScreen'

type Screen = 'products' | 'stock'

export function Shell({ email }: { email: string }) {
  const queryClient = useQueryClient()
  const [screen, setScreen] = useState<Screen>('products')

  const signOut = useMutation({
    mutationFn: () => api.post('/api/auth/logout'),
    onSuccess: () => {
      // Signing out first re-renders the app onto the login screen; clear() alone notifies no one
      queryClient.setQueryData(sessionQuery.queryKey, { status: 'signedOut' })
      queryClient.clear()
    },
  })

  return (
    <AppShell header={{ height: 60 }} navbar={{ width: 200, breakpoint: 'sm' }} padding="md">
      <AppShell.Header>
        <Group h="100%" px="md" justify="space-between">
          <Title order={3}>Ratatosk back office</Title>
          <Group>
            <Text size="sm">{email}</Text>
            <Button variant="default" loading={signOut.isPending} onClick={() => signOut.mutate()}>
              Sign out
            </Button>
          </Group>
        </Group>
      </AppShell.Header>
      <AppShell.Navbar p="xs">
        <NavLink component="button" label="Products" active={screen === 'products'} onClick={() => setScreen('products')} />
        <NavLink component="button" label="Stock" active={screen === 'stock'} onClick={() => setScreen('stock')} />
      </AppShell.Navbar>
      <AppShell.Main>{screen === 'products' ? <ProductsScreen /> : <StockScreen />}</AppShell.Main>
    </AppShell>
  )
}
