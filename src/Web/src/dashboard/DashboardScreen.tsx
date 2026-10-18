import { useId, type ReactNode } from 'react'
import { Alert, Badge, Center, Loader, Paper, SimpleGrid, Stack, Table, Text, Title } from '@mantine/core'
import { useQueries, useQuery } from '@tanstack/react-query'
import { activityQuery, type ChangeSignal } from '../changes/activity'
import { orderCountQuery, orderStatuses, statusColors } from '../orders/orders'
import { lowStockQuery } from '../stock/stock'

const kindLabels: Record<ChangeSignal['kind'], string> = {
  product: 'Product',
  stock: 'Stock',
  order: 'Order',
}

function Section({ title, children }: { title: string; children: ReactNode }) {
  const titleId = useId()
  return (
    <Paper component="section" withBorder p="md" miw={0} aria-labelledby={titleId}>
      <Stack>
        <Title order={3} id={titleId}>
          {title}
        </Title>
        {children}
      </Stack>
    </Paper>
  )
}

function Pending() {
  return (
    <Center p="md">
      <Loader />
    </Center>
  )
}

function OrderCounts() {
  const counts = useQueries({ queries: orderStatuses.map((status) => orderCountQuery(status)) })
  const failed = counts.find((count) => count.isError)

  return (
    <Section title="Order counts">
      {failed ? (
        <Alert color="red" role="alert">
          {failed.error?.message}
        </Alert>
      ) : counts.some((count) => count.isPending) ? (
        <Pending />
      ) : (
        <SimpleGrid cols={{ base: 1, xs: 3 }}>
          {orderStatuses.map((status, i) => (
            <Paper key={status} withBorder p="sm" role="group" aria-label={status}>
              <Stack gap="xs" align="flex-start">
                <Badge color={statusColors[status]}>{status}</Badge>
                <Text fz="xl" fw={700}>
                  {counts[i].data}
                </Text>
              </Stack>
            </Paper>
          ))}
        </SimpleGrid>
      )}
    </Section>
  )
}

function LowStock() {
  const stock = useQuery(lowStockQuery())
  const more = stock.data ? stock.data.totalItems - stock.data.items.length : 0

  return (
    <Section title="Low stock">
      {stock.isError ? (
        <Alert color="red" role="alert">
          {stock.error.message}
        </Alert>
      ) : stock.isPending ? (
        <Pending />
      ) : stock.data.items.length === 0 ? (
        <Text>No low-stock Products.</Text>
      ) : (
        <>
          <Table.ScrollContainer minWidth={400}>
            <Table striped>
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>SKU</Table.Th>
                  <Table.Th>Available</Table.Th>
                  <Table.Th>On hand</Table.Th>
                  <Table.Th>Reserved</Table.Th>
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {stock.data.items.map((record) => (
                  <Table.Tr key={record.productId}>
                    <Table.Td>{record.sku}</Table.Td>
                    <Table.Td>{record.available}</Table.Td>
                    <Table.Td>{record.onHand}</Table.Td>
                    <Table.Td>{record.reserved}</Table.Td>
                  </Table.Tr>
                ))}
              </Table.Tbody>
            </Table>
          </Table.ScrollContainer>
          {more > 0 && <Text size="sm">and {more} more</Text>}
        </>
      )}
    </Section>
  )
}

// Read from the cache only: useChangeStream records the signals, nothing is fetched
function RecentActivity() {
  const { data: activity = [] } = useQuery(activityQuery)

  return (
    <Section title="Recent activity">
      {activity.length === 0 ? (
        <Text>No changes since you signed in.</Text>
      ) : (
        <Table.ScrollContainer minWidth={500}>
          <Table striped>
            <Table.Thead>
              <Table.Tr>
                <Table.Th>Kind</Table.Th>
                <Table.Th>Id</Table.Th>
                <Table.Th>Version</Table.Th>
                <Table.Th>Received</Table.Th>
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {activity.map((entry) => (
                <Table.Tr key={`${entry.kind}-${entry.id}-${entry.version}`}>
                  <Table.Td>{kindLabels[entry.kind]}</Table.Td>
                  <Table.Td>{entry.id}</Table.Td>
                  <Table.Td>{entry.version}</Table.Td>
                  <Table.Td>{entry.receivedAt.toLocaleTimeString()}</Table.Td>
                </Table.Tr>
              ))}
            </Table.Tbody>
          </Table>
        </Table.ScrollContainer>
      )}
    </Section>
  )
}

export function DashboardScreen() {
  return (
    <Stack>
      <Title order={2}>Dashboard</Title>
      <OrderCounts />
      <SimpleGrid cols={{ base: 1, lg: 2 }}>
        <LowStock />
        <RecentActivity />
      </SimpleGrid>
    </Stack>
  )
}
