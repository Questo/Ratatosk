import type { ReactNode } from 'react'
import { Alert, Badge, Center, Loader, SimpleGrid, Stack, Table, Text } from '@mantine/core'
import { useQuery } from '@tanstack/react-query'
import { orderQuery, statusColors } from './orders'

function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div>
      <Text size="sm" c="dimmed">
        {label}
      </Text>
      <Text component="div">{children}</Text>
    </div>
  )
}

// Loaded through orderQuery, so invalidating the Order keys refetches an open detail too
export function OrderDetail({ orderId }: { orderId: string }) {
  const order = useQuery(orderQuery(orderId))

  if (order.isError) {
    return (
      <Alert color="red" role="alert">
        {order.error.message}
      </Alert>
    )
  }

  if (order.isPending) {
    return (
      <Center p="md">
        <Loader />
      </Center>
    )
  }

  const { status, customerId, createdUtc, lastUpdatedUtc, cancellationReason, lines } = order.data

  return (
    <Stack>
      <SimpleGrid cols={{ base: 1, xs: 2 }}>
        <Field label="Status">
          <Badge color={statusColors[status]}>{status}</Badge>
        </Field>
        <Field label="Customer id">{customerId}</Field>
        <Field label="Placed">{new Date(createdUtc).toLocaleString()}</Field>
        <Field label="Last updated">{new Date(lastUpdatedUtc).toLocaleString()}</Field>
      </SimpleGrid>
      {status === 'Cancelled' && <Field label="Cancellation reason">{cancellationReason ?? 'Not recorded'}</Field>}
      <Table.ScrollContainer minWidth={400}>
        <Table striped>
          <Table.Thead>
            <Table.Tr>
              <Table.Th>SKU</Table.Th>
              <Table.Th>Quantity</Table.Th>
              <Table.Th>Unit price</Table.Th>
            </Table.Tr>
          </Table.Thead>
          <Table.Tbody>
            {lines.map((line) => (
              <Table.Tr key={line.sku}>
                <Table.Td>{line.sku}</Table.Td>
                <Table.Td>{line.quantity}</Table.Td>
                <Table.Td>
                  {line.unitPrice.toFixed(2)} {line.currency}
                </Table.Td>
              </Table.Tr>
            ))}
          </Table.Tbody>
        </Table>
      </Table.ScrollContainer>
    </Stack>
  )
}
