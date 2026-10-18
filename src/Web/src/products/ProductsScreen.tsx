import { useEffect, useState } from 'react'
import { Alert, Button, Center, Group, Loader, Modal, Pagination, Stack, Table, Text, TextInput, Title } from '@mantine/core'
import { useDebouncedValue } from '@mantine/hooks'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import type { OpenEntityRequest } from '../search/search'
import { ProductForm } from './ProductForm'
import { productQuery, productsKey, productsQuery, type Product } from './products'
import { RemoveProduct } from './RemoveProduct'

type Dialog = { kind: 'add' } | { kind: 'edit'; product: Product } | { kind: 'remove'; product: Product }

export function ProductsScreen({ openRequest = null }: { openRequest?: OpenEntityRequest | null }) {
  const queryClient = useQueryClient()
  const [search, setSearch] = useState('')
  const [searchTerm] = useDebouncedValue(search.trim(), 300)
  // The page belongs to the term it was chosen for, so every new term starts on page 1
  const [paging, setPaging] = useState({ searchTerm, page: 1 })
  if (paging.searchTerm !== searchTerm) setPaging({ searchTerm, page: 1 })
  const page = paging.searchTerm === searchTerm ? paging.page : 1
  const [dialog, setDialog] = useState<Dialog | null>(null)
  // A search hit opens its Product's edit dialog, fetched by id since it may not be on this page
  const [opening, setOpening] = useState<{ seq: number; error: string | null } | null>(null)
  if (openRequest && openRequest.seq !== opening?.seq) {
    setOpening({ seq: openRequest.seq, error: null })
    setDialog(null)
  }

  useEffect(() => {
    if (!openRequest) return
    let current = true
    queryClient.fetchQuery(productQuery(openRequest.id)).then(
      (product) => {
        if (current) setDialog({ kind: 'edit', product })
      },
      (error: Error) => {
        if (current) setOpening({ seq: openRequest.seq, error: error.message })
      },
    )
    return () => {
      current = false
    }
  }, [openRequest, queryClient])

  const products = useQuery(productsQuery(searchTerm, page))

  const close = () => setDialog(null)
  const closeAndRefetch = () => {
    setDialog(null)
    void queryClient.invalidateQueries({ queryKey: productsKey })
  }

  const rows = products.data?.items.map((product) => (
    <Table.Tr key={product.id}>
      <Table.Td>{product.name}</Table.Td>
      <Table.Td>{product.sku}</Table.Td>
      <Table.Td>{product.description}</Table.Td>
      <Table.Td>{product.price.toFixed(2)}</Table.Td>
      <Table.Td>
        <Group gap="xs" wrap="nowrap">
          <Button size="xs" variant="default" onClick={() => setDialog({ kind: 'edit', product })}>
            Edit
          </Button>
          <Button size="xs" variant="default" color="red" onClick={() => setDialog({ kind: 'remove', product })}>
            Remove
          </Button>
        </Group>
      </Table.Td>
    </Table.Tr>
  ))

  return (
    <Stack>
      <Group justify="space-between">
        <Title order={2}>Products</Title>
        <Button onClick={() => setDialog({ kind: 'add' })}>Add Product</Button>
      </Group>
      <TextInput label="Search" value={search} onChange={(event) => setSearch(event.currentTarget.value)} />
      {opening?.error && (
        <Alert color="red" role="alert" title="Couldn't open the Product">
          {opening.error}
        </Alert>
      )}
      {products.isError ? (
        <Alert color="red" role="alert">
          {products.error.message}
        </Alert>
      ) : products.isPending ? (
        <Center p="md">
          <Loader />
        </Center>
      ) : products.data.items.length === 0 ? (
        <Text>No Products found.</Text>
      ) : (
        <>
          <Table.ScrollContainer minWidth={700}>
            <Table striped>
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>Name</Table.Th>
                  <Table.Th>SKU</Table.Th>
                  <Table.Th>Description</Table.Th>
                  <Table.Th>Price</Table.Th>
                  <Table.Th aria-label="Actions" />
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>{rows}</Table.Tbody>
            </Table>
          </Table.ScrollContainer>
          {products.data.totalPages > 1 && (
            <Pagination
              total={products.data.totalPages}
              value={page}
              onChange={(value) => setPaging({ searchTerm, page: value })}
            />
          )}
        </>
      )}

      <Modal opened={dialog?.kind === 'add'} onClose={close} title="Add Product">
        <ProductForm onSaved={closeAndRefetch} />
      </Modal>
      <Modal opened={dialog?.kind === 'edit'} onClose={close} title="Edit Product">
        {dialog?.kind === 'edit' && <ProductForm key={dialog.product.id} product={dialog.product} onSaved={closeAndRefetch} />}
      </Modal>
      <Modal opened={dialog?.kind === 'remove'} onClose={close} title="Remove Product">
        {dialog?.kind === 'remove' && (
          <RemoveProduct product={dialog.product} onRemoved={closeAndRefetch} onCancel={close} />
        )}
      </Modal>
    </Stack>
  )
}
