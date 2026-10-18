import { useState } from 'react'
import { Alert, Badge, Button, Center, Group, Loader, Stack, Table, Text } from '@mantine/core'
import { useQuery } from '@tanstack/react-query'
import { orderHistoryQuery, statusColors } from './orders'

// Steps through the Order's events one at a time, showing the Order as it was after each.
// Loaded through orderHistoryQuery, so an order change signal refetches it too.
export function OrderHistory({ orderId }: { orderId: string }) {
  const history = useQuery(orderHistoryQuery(orderId))
  // Null follows the latest version, so a refetch with new events moves the view along
  const [selected, setSelected] = useState<number | null>(null)

  if (history.isError) {
    return (
      <Alert color="red" role="alert">
        {history.error.message}
      </Alert>
    )
  }

  if (history.isPending) {
    return (
      <Center p="md">
        <Loader />
      </Center>
    )
  }

  const entries = history.data
  const total = entries.length
  const index = selected === null ? total - 1 : Math.min(selected, total - 1)
  const entry = entries[index]
  if (!entry) return null

  const { order, reservedSkus } = entry
  const goTo = (next: number) => setSelected(next >= total - 1 ? null : next)

  return (
    <Stack>
      <Group justify="space-between">
        <Button variant="default" onClick={() => goTo(index - 1)} disabled={index === 0}>
          Previous
        </Button>
        <Text ta="center">
          Version {entry.version} of {total} · <strong>{entry.event}</strong> ·{' '}
          {new Date(entry.occurredAtUtc).toLocaleString()}
        </Text>
        <Button variant="default" onClick={() => goTo(index + 1)} disabled={index === total - 1}>
          Next
        </Button>
      </Group>
      <div>
        <Badge color={statusColors[order.status]}>{order.status}</Badge>
      </div>
      {order.status === 'Cancelled' && (
        <div>
          <Text size="sm" c="dimmed">
            Cancellation reason
          </Text>
          <Text>{order.cancellationReason ?? 'Not recorded'}</Text>
        </div>
      )}
      <Table.ScrollContainer minWidth={400}>
        <Table striped>
          <Table.Thead>
            <Table.Tr>
              <Table.Th>SKU</Table.Th>
              <Table.Th>Quantity</Table.Th>
              <Table.Th>Unit price</Table.Th>
              <Table.Th>Reserved</Table.Th>
            </Table.Tr>
          </Table.Thead>
          <Table.Tbody>
            {order.lines.map((line) => (
              <Table.Tr key={line.sku}>
                <Table.Td>{line.sku}</Table.Td>
                <Table.Td>{line.quantity}</Table.Td>
                <Table.Td>
                  {line.unitPrice.toFixed(2)} {line.currency}
                </Table.Td>
                <Table.Td>{reservedSkus.includes(line.sku) ? 'Yes' : 'No'}</Table.Td>
              </Table.Tr>
            ))}
          </Table.Tbody>
        </Table>
      </Table.ScrollContainer>
    </Stack>
  )
}
