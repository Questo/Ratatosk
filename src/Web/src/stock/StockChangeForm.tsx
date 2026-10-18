import { useState } from 'react'
import { Alert, Button, Group, NumberInput, Stack } from '@mantine/core'
import { useForm } from '@mantine/form'
import { useMutation } from '@tanstack/react-query'
import { api, ApiError } from '../api/client'
import { actionLabels, type Stock, type StockAction } from './stock'

interface StockChangeValues {
  // NumberInput reports '' while the field is empty
  quantity: number | string
}

function changeErrorMessage(error: unknown): string {
  if (error instanceof ApiError && error.apiMessage) return error.apiMessage
  return 'Changing the Stock failed. Try again.'
}

// Restocks, reserves or releases a quantity of one SKU's Stock. Only a filled-in quantity is checked here: the API owns the rules.
export function StockChangeForm({
  stock,
  action,
  onChanged,
}: {
  stock: Stock
  action: StockAction
  onChanged: () => void
}) {
  const [error, setError] = useState<string | null>(null)

  const form = useForm<StockChangeValues>({
    mode: 'uncontrolled',
    initialValues: { quantity: '' },
    validate: {
      quantity: (value) => (value === '' ? 'Quantity is required' : null),
    },
  })

  const change = useMutation({
    mutationFn: ({ quantity }: StockChangeValues) =>
      api.post(`/api/inventory/${stock.productId}/${action}`, { quantity: Number(quantity) }),
    onSuccess: onChanged,
    onError: (e) => setError(changeErrorMessage(e)),
  })

  const handleSubmit = (values: StockChangeValues) => {
    setError(null)
    change.mutate(values)
  }

  return (
    <form onSubmit={form.onSubmit(handleSubmit)} noValidate>
      <Stack>
        {error && (
          <Alert color="red" role="alert">
            {error}
          </Alert>
        )}
        <NumberInput
          label="Quantity"
          allowDecimal={false}
          withAsterisk
          key={form.key('quantity')}
          {...form.getInputProps('quantity')}
        />
        <Group justify="flex-end">
          <Button type="submit" loading={change.isPending}>
            {actionLabels[action]}
          </Button>
        </Group>
      </Stack>
    </form>
  )
}
