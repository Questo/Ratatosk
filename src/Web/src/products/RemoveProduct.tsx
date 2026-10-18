import { Alert, Button, Group, Stack, Text } from '@mantine/core'
import { useMutation } from '@tanstack/react-query'
import { api, ApiError } from '../api/client'
import type { Product } from './products'

function removeErrorMessage(error: unknown): string {
  if (error instanceof ApiError && error.status === 404) return 'This Product no longer exists.'
  return 'Removing the Product failed. Try again.'
}

export function RemoveProduct({
  product,
  onRemoved,
  onCancel,
}: {
  product: Product
  onRemoved: () => void
  onCancel: () => void
}) {
  const remove = useMutation({
    mutationFn: () => api.delete(`/api/products/${product.id}`),
    onSuccess: onRemoved,
  })

  return (
    <Stack>
      {remove.isError && (
        <Alert color="red" role="alert">
          {removeErrorMessage(remove.error)}
        </Alert>
      )}
      <Text>Remove {product.name}?</Text>
      <Group justify="flex-end">
        <Button variant="default" onClick={onCancel}>
          Cancel
        </Button>
        <Button color="red" loading={remove.isPending} onClick={() => remove.mutate()}>
          Remove
        </Button>
      </Group>
    </Stack>
  )
}
