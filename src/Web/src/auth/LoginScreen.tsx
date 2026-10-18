import { useState } from 'react'
import { Alert, Button, Card, Center, PasswordInput, Stack, TextInput, Title } from '@mantine/core'
import { isNotEmpty, useForm } from '@mantine/form'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { api, ApiError } from '../api/client'
import { sessionQuery } from './session'

interface Credentials {
  email: string
  password: string
}

function loginErrorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.status === 401) return 'Invalid email or password.'
    if (error.apiMessage) return error.apiMessage
  }
  return 'Sign-in failed. Try again.'
}

export function LoginScreen({ notice }: { notice?: string }) {
  const queryClient = useQueryClient()
  const [error, setError] = useState<string | null>(null)

  const form = useForm<Credentials>({
    mode: 'uncontrolled',
    initialValues: { email: '', password: '' },
    validate: {
      email: (value) =>
        !value.trim() ? 'Email is required' : /^\S+@\S+\.\S+$/.test(value) ? null : 'Enter a valid email',
      password: isNotEmpty('Password is required'),
    },
  })

  const login = useMutation({
    mutationFn: async (credentials: Credentials) => {
      await api.post('/api/auth/login', credentials)
      await queryClient.fetchQuery({ ...sessionQuery, staleTime: 0 })
    },
    onError: (e) => setError(loginErrorMessage(e)),
  })

  const handleSubmit = (credentials: Credentials) => {
    setError(null)
    login.mutate(credentials)
  }

  const message = error ?? notice

  return (
    <Center mih="100vh" p="md">
      <Card withBorder shadow="sm" radius="md" p="xl" w="100%" maw={380}>
        <Title order={2} ta="center" mb="lg">
          Ratatosk back office
        </Title>
        <form onSubmit={form.onSubmit(handleSubmit)} noValidate>
          <Stack>
            {message && (
              <Alert color="red" role="alert">
                {message}
              </Alert>
            )}
            <TextInput
              label="Email"
              type="email"
              autoComplete="username"
              withAsterisk
              key={form.key('email')}
              {...form.getInputProps('email')}
            />
            <PasswordInput
              label="Password"
              autoComplete="current-password"
              withAsterisk
              key={form.key('password')}
              {...form.getInputProps('password')}
            />
            <Button type="submit" loading={login.isPending} fullWidth>
              Sign in
            </Button>
          </Stack>
        </form>
      </Card>
    </Center>
  )
}
