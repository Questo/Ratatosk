import { useState } from 'react'
import { Alert, Button, Center, Group, Loader, Modal, Pagination, Stack, Table, Text, Title } from '@mantine/core'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { actionLabels, stockKey, stockQuery, type Stock, type StockAction } from './stock'
import { StockChangeForm } from './StockChangeForm'

type Dialog = { action: StockAction; stock: Stock }

const actions: StockAction[] = ['restock', 'reserve', 'release']

export function StockScreen() {
  const queryClient = useQueryClient()
  const [page, setPage] = useState(1)
  const [dialog, setDialog] = useState<Dialog | null>(null)

  const stock = useQuery(stockQuery(page))

  const close = () => setDialog(null)
  const closeAndRefetch = () => {
    setDialog(null)
    void queryClient.invalidateQueries({ queryKey: stockKey })
  }

  const rows = stock.data?.items.map((record) => (
    <Table.Tr key={record.productId}>
      <Table.Td>{record.sku}</Table.Td>
      <Table.Td>{record.onHand}</Table.Td>
      <Table.Td>{record.reserved}</Table.Td>
      <Table.Td>{record.available}</Table.Td>
      <Table.Td>{record.unit}</Table.Td>
      <Table.Td>
        <Group gap="xs" wrap="nowrap">
          {actions.map((action) => (
            <Button key={action} size="xs" variant="default" onClick={() => setDialog({ action, stock: record })}>
              {actionLabels[action]}
            </Button>
          ))}
        </Group>
      </Table.Td>
    </Table.Tr>
  ))

  return (
    <Stack>
      <Title order={2}>Stock</Title>
      {stock.isError ? (
        <Alert color="red" role="alert">
          {stock.error.message}
        </Alert>
      ) : stock.isPending ? (
        <Center p="md">
          <Loader />
        </Center>
      ) : stock.data.items.length === 0 ? (
        <Text>No Stock records found.</Text>
      ) : (
        <>
          <Table.ScrollContainer minWidth={700}>
            <Table striped>
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>SKU</Table.Th>
                  <Table.Th>On hand</Table.Th>
                  <Table.Th>Reserved</Table.Th>
                  <Table.Th>Available</Table.Th>
                  <Table.Th>Unit</Table.Th>
                  <Table.Th aria-label="Actions" />
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>{rows}</Table.Tbody>
            </Table>
          </Table.ScrollContainer>
          {stock.data.totalPages > 1 && <Pagination total={stock.data.totalPages} value={page} onChange={setPage} />}
        </>
      )}

      <Modal
        opened={dialog !== null}
        onClose={close}
        title={dialog && `${actionLabels[dialog.action]} ${dialog.stock.sku}`}
      >
        {dialog && (
          <StockChangeForm
            key={`${dialog.action}-${dialog.stock.productId}`}
            stock={dialog.stock}
            action={dialog.action}
            onChanged={closeAndRefetch}
          />
        )}
      </Modal>
    </Stack>
  )
}
