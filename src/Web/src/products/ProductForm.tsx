import { useState } from 'react'
import { Alert, Button, Group, NumberInput, Stack, Textarea, TextInput } from '@mantine/core'
import { isNotEmpty, useForm } from '@mantine/form'
import { useMutation } from '@tanstack/react-query'
import { api, ApiError } from '../api/client'
import type { Product } from './products'

interface ProductValues {
  name: string
  sku: string
  description: string
  // NumberInput reports '' while the field is empty
  price: number | string
}

function saveErrorMessage(error: unknown): string {
  if (error instanceof ApiError && error.apiMessage) return error.apiMessage
  return 'Saving the Product failed. Try again.'
}

// Adds a Product, or edits the given one. Only required fields are checked here: the API owns the rules.
export function ProductForm({ product, onSaved }: { product?: Product; onSaved: () => void }) {
  const [error, setError] = useState<string | null>(null)

  const form = useForm<ProductValues>({
    mode: 'uncontrolled',
    initialValues: {
      name: product?.name ?? '',
      sku: product?.sku ?? '',
      description: product?.description ?? '',
      price: product?.price ?? '',
    },
    validate: {
      name: isNotEmpty('Name is required'),
      sku: isNotEmpty('SKU is required'),
      description: isNotEmpty('Description is required'),
      price: (value) => (value === '' ? 'Price is required' : null),
    },
  })

  const save = useMutation({
    mutationFn: ({ name, sku, description, price }: ProductValues) =>
      product
        ? api.put(`/api/products/${product.id}`, { name, description, price: Number(price) })
        : api.post('/api/products', { name, sku, description, price: Number(price) }),
    onSuccess: onSaved,
    onError: (e) => setError(saveErrorMessage(e)),
  })

  const handleSubmit = (values: ProductValues) => {
    setError(null)
    save.mutate(values)
  }

  return (
    <form onSubmit={form.onSubmit(handleSubmit)} noValidate>
      <Stack>
        {error && (
          <Alert color="red" role="alert">
            {error}
          </Alert>
        )}
        <TextInput label="Name" withAsterisk key={form.key('name')} {...form.getInputProps('name')} />
        {product ? (
          <TextInput label="SKU" value={product.sku} disabled />
        ) : (
          <TextInput
            label="SKU"
            description="e.g. TS-3FA7C1"
            withAsterisk
            key={form.key('sku')}
            {...form.getInputProps('sku')}
          />
        )}
        <Textarea
          label="Description"
          rows={4}
          withAsterisk
          key={form.key('description')}
          {...form.getInputProps('description')}
        />
        <NumberInput
          label="Price"
          withAsterisk
          key={form.key('price')}
          {...form.getInputProps('price')}
        />
        <Group justify="flex-end">
          <Button type="submit" loading={save.isPending}>
            Save
          </Button>
        </Group>
      </Stack>
    </form>
  )
}
