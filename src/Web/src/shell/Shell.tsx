import { AppShell, Button, Group, Text, Title } from '@mantine/core'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { api } from '../api/client'
import { sessionQuery } from '../auth/session'
import { ProductsScreen } from '../products/ProductsScreen'

export function Shell({ email }: { email: string }) {
  const queryClient = useQueryClient()

  const signOut = useMutation({
    mutationFn: () => api.post('/api/auth/logout'),
    onSuccess: () => {
      // Signing out first re-renders the app onto the login screen; clear() alone notifies no one
      queryClient.setQueryData(sessionQuery.queryKey, { status: 'signedOut' })
      queryClient.clear()
    },
  })

  return (
    <AppShell header={{ height: 60 }} padding="md">
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
      <AppShell.Main>
        <ProductsScreen />
      </AppShell.Main>
    </AppShell>
  )
}
