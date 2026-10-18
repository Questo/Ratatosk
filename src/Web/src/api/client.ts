// The only way browser code talks to the API. Auth rides on the httpOnly cookies (ADR 0003),
// so this never reads, stores or sends a token itself.

interface Envelope<T> {
  message?: string | null
  data?: T
}

export class ApiError extends Error {
  readonly status: number
  // The envelope's message, when the API sent one
  readonly apiMessage: string | null

  constructor(status: number, apiMessage: string | null) {
    super(apiMessage ?? `Request failed with status ${status}`)
    this.name = 'ApiError'
    this.status = status
    this.apiMessage = apiMessage
  }
}

const refreshPath = '/api/auth/refresh'
const noRefreshPaths = ['/api/auth/login', refreshPath, '/api/auth/logout']

async function readEnvelope<T>(response: Response): Promise<Envelope<T> | null> {
  const text = await response.text()
  if (!text) return null
  try {
    return JSON.parse(text) as Envelope<T>
  } catch {
    return null
  }
}

export async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  let response = await fetch(path, init)

  // One refresh (from the refresh_token cookie) and one retry when the access cookie has expired
  if (response.status === 401 && !noRefreshPaths.includes(path)) {
    const refreshed = await fetch(refreshPath, { method: 'POST' })
    if (refreshed.ok) response = await fetch(path, init)
  }

  const envelope = await readEnvelope<T>(response)
  if (!response.ok) throw new ApiError(response.status, envelope?.message ?? null)
  return envelope?.data as T
}

function withBody(method: string, body?: unknown): RequestInit {
  return body === undefined
    ? { method }
    : {
        method,
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body),
      }
}

export const api = {
  get: <T>(path: string) => request<T>(path),
  post: <T>(path: string, body?: unknown) => request<T>(path, withBody('POST', body)),
  put: <T>(path: string, body?: unknown) => request<T>(path, withBody('PUT', body)),
  delete: <T>(path: string) => request<T>(path, { method: 'DELETE' }),
}
