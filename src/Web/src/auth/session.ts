import { queryOptions } from '@tanstack/react-query'
import { api, ApiError } from '../api/client'

export const adminRole = 'Admin'
export const noAdminAccessMessage = "This account doesn't have Admin access."

interface CurrentUser {
  email: string
  role: string
}

export type Session =
  | { status: 'signedOut' }
  | { status: 'notAdmin' }
  | { status: 'admin'; email: string }

async function fetchSession(): Promise<Session> {
  let user: CurrentUser
  try {
    user = await api.get<CurrentUser>('/api/auth/me')
  } catch (error) {
    if (error instanceof ApiError && error.status === 401) return { status: 'signedOut' }
    throw error
  }

  if (user.role === adminRole) return { status: 'admin', email: user.email }

  // The back office is Admin-only (ADR 0001): end any other session at once
  await api.post('/api/auth/logout')
  return { status: 'notAdmin' }
}

// A 401 resolves to signedOut rather than failing, so it is never retried
export const sessionQuery = queryOptions({
  queryKey: ['auth', 'me'],
  queryFn: fetchSession,
})
