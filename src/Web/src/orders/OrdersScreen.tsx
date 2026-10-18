import { useState } from 'react'
import { Alert, Badge, Button, Center, Loader, Modal, Pagination, Select, Stack, Table, Tabs, Text, Title } from '@mantine/core'
import { useQuery } from '@tanstack/react-query'
import { OrderDetail } from './OrderDetail'
import { OrderHistory } from './OrderHistory'
import { orderStatuses, ordersQuery, statusColors, type OrderStatus } from './orders'

const all = 'All'
const statusOptions = [all, ...orderStatuses]

export function OrdersScreen() {
  // The page belongs to the status it was chosen for, so changing the filter starts on page 1
  const [filter, setFilter] = useState<{ status: OrderStatus | null; page: number }>({ status: null, page: 1 })
  const [openOrderId, setOpenOrderId] = useState<string | null>(null)

  const orders = useQuery(ordersQuery(filter.status, filter.page))

  const rows = orders.data?.items.map((order) => (
    <Table.Tr key={order.id}>
      <Table.Td>{order.id}</Table.Td>
      <Table.Td>
        <Badge color={statusColors[order.status]}>{order.status}</Badge>
      </Table.Td>
      <Table.Td>{order.lines.length}</Table.Td>
      <Table.Td>{new Date(order.createdUtc).toLocaleString()}</Table.Td>
      <Table.Td>{new Date(order.lastUpdatedUtc).toLocaleString()}</Table.Td>
      <Table.Td>
        <Button size="xs" variant="default" onClick={() => setOpenOrderId(order.id)}>
          View
        </Button>
      </Table.Td>
    </Table.Tr>
  ))

  return (
    <Stack>
      <Title order={2}>Orders</Title>
      <Select
        label="Status"
        data={statusOptions}
        value={filter.status ?? all}
        allowDeselect={false}
        onChange={(value) =>
          setFilter({ status: value === all || value === null ? null : (value as OrderStatus), page: 1 })
        }
        maw={240}
      />
      {orders.isError ? (
        <Alert color="red" role="alert">
          {orders.error.message}
        </Alert>
      ) : orders.isPending ? (
        <Center p="md">
          <Loader />
        </Center>
      ) : orders.data.items.length === 0 ? (
        <Text>No Orders found.</Text>
      ) : (
        <>
          <Table.ScrollContainer minWidth={900}>
            <Table striped>
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>Order</Table.Th>
                  <Table.Th>Status</Table.Th>
                  <Table.Th>Order lines</Table.Th>
                  <Table.Th>Placed</Table.Th>
                  <Table.Th>Last updated</Table.Th>
                  <Table.Th aria-label="Actions" />
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>{rows}</Table.Tbody>
            </Table>
          </Table.ScrollContainer>
          {orders.data.totalPages > 1 && (
            <Pagination
              total={orders.data.totalPages}
              value={filter.page}
              onChange={(page) => setFilter({ ...filter, page })}
            />
          )}
        </>
      )}

      <Modal opened={openOrderId !== null} onClose={() => setOpenOrderId(null)} title={openOrderId && `Order ${openOrderId}`} size="lg">
        {openOrderId && (
          // keepMounted off, so the history is only fetched while its tab is shown
          <Tabs defaultValue="details" keepMounted={false}>
            <Tabs.List mb="md">
              <Tabs.Tab value="details">Details</Tabs.Tab>
              <Tabs.Tab value="history">History</Tabs.Tab>
            </Tabs.List>
            <Tabs.Panel value="details">
              <OrderDetail orderId={openOrderId} />
            </Tabs.Panel>
            <Tabs.Panel value="history">
              <OrderHistory orderId={openOrderId} />
            </Tabs.Panel>
          </Tabs>
        )}
      </Modal>
    </Stack>
  )
}
