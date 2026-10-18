import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { api, ApiError } from './client'
import { server } from '../test/server'

const envelope = (data: unknown) => HttpResponse.json({ message: null, data, timestamp: new Date().toISOString() })
const unauthorized = () => HttpResponse.json({ message: 'Unauthorized.' }, { status: 401 })

describe('api client', () => {
  it('unwraps the envelope data', async () => {
    server.use(http.get('/api/things', () => envelope({ id: 1 })))

    await expect(api.get('/api/things')).resolves.toEqual({ id: 1 })
  })

  it('throws the status and envelope message for a failed response', async () => {
    server.use(http.post('/api/things', () => HttpResponse.json({ message: 'Name is required.' }, { status: 400 })))

    const error = await api.post('/api/things', { name: '' }).catch((e: unknown) => e)

    expect(error).toBeInstanceOf(ApiError)
    expect(error).toMatchObject({ status: 400, apiMessage: 'Name is required.', message: 'Name is required.' })
  })

  it('refreshes once and retries the request once after a 401', async () => {
    let thingCalls = 0
    let refreshCalls = 0
    let refreshBody: string | null = null
    server.use(
      http.get('/api/things', () => (++thingCalls === 1 ? unauthorized() : envelope('fresh'))),
      http.post('/api/auth/refresh', async ({ request }) => {
        refreshCalls++
        refreshBody = await request.text()
        return envelope({})
      }),
    )

    await expect(api.get('/api/things')).resolves.toBe('fresh')
    expect(thingCalls).toBe(2)
    expect(refreshCalls).toBe(1)
    expect(refreshBody).toBe('')
  })

  it('throws the 401 when the refresh fails, without retrying', async () => {
    let thingCalls = 0
    server.use(
      http.get('/api/things', () => {
        thingCalls++
        return unauthorized()
      }),
      http.post('/api/auth/refresh', unauthorized),
    )

    await expect(api.get('/api/things')).rejects.toMatchObject({ status: 401 })
    expect(thingCalls).toBe(1)
  })

  it('throws the 401 when the retried request is still unauthorized', async () => {
    let thingCalls = 0
    server.use(
      http.get('/api/things', () => {
        thingCalls++
        return unauthorized()
      }),
      http.post('/api/auth/refresh', () => envelope({})),
    )

    await expect(api.get('/api/things')).rejects.toMatchObject({ status: 401 })
    expect(thingCalls).toBe(2)
  })

  it.each(['/api/auth/login', '/api/auth/logout'])('does not refresh after a 401 from %s', async (path) => {
    let refreshCalls = 0
    server.use(
      http.post(path, unauthorized),
      http.post('/api/auth/refresh', () => {
        refreshCalls++
        return envelope({})
      }),
    )

    await expect(api.post(path)).rejects.toMatchObject({ status: 401 })
    expect(refreshCalls).toBe(0)
  })

  it('does not refresh again after a 401 from the refresh itself', async () => {
    let refreshCalls = 0
    server.use(
      http.post('/api/auth/refresh', () => {
        refreshCalls++
        return unauthorized()
      }),
    )

    await expect(api.post('/api/auth/refresh')).rejects.toMatchObject({ status: 401 })
    expect(refreshCalls).toBe(1)
  })

  it('never sends an Authorization header', async () => {
    const headers: (string | null)[] = []
    server.use(
      http.get('/api/things', ({ request }) => {
        headers.push(request.headers.get('Authorization'))
        return headers.length === 1 ? unauthorized() : envelope('ok')
      }),
      http.post('/api/auth/refresh', ({ request }) => {
        headers.push(request.headers.get('Authorization'))
        return envelope({ accessToken: 'a', refreshToken: 'r' })
      }),
    )

    await api.get('/api/things')

    expect(headers).toEqual([null, null, null])
  })
})
