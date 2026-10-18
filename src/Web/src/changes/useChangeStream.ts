import { useEffect } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { api } from '../api/client'
import { ordersKey } from '../orders/orders'

// One signal from GET /changes: the read model of this kind and id changed to this version
interface ChangeSignal {
  kind: 'product' | 'stock' | 'order'
  id: string
  version: number
}

const reconnectDelayMs = 5000

// Keeps one EventSource on /api/changes open while mounted and refetches the Order queries
// whenever an Order changes. Signals carry no state; the read endpoints stay the source of truth.
export function useChangeStream() {
  const queryClient = useQueryClient()

  useEffect(() => {
    let source: EventSource
    let reconnect: ReturnType<typeof setTimeout> | undefined
    let unmounted = false

    const invalidateOrders = () => void queryClient.invalidateQueries({ queryKey: ordersKey })

    const connect = () => {
      source = new EventSource('/api/changes')
      // Also catches up on signals missed while disconnected
      source.addEventListener('open', invalidateOrders)
      source.addEventListener('change', (event: MessageEvent<string>) => {
        const signal = JSON.parse(event.data) as ChangeSignal
        if (signal.kind === 'order') invalidateOrders()
      })
      source.addEventListener('error', () => {
        // While CONNECTING the browser retries on its own. CLOSED means it gave up, e.g. on a 401
        // once the access cookie expired: /auth/me refreshes the cookie through the client first.
        if (source.readyState !== EventSource.CLOSED) return
        reconnect = setTimeout(() => {
          void api
            .get('/api/auth/me')
            .catch(() => {})
            .then(() => {
              if (!unmounted) connect()
            })
        }, reconnectDelayMs)
      })
    }

    connect()

    return () => {
      unmounted = true
      clearTimeout(reconnect)
      source.close()
    }
  }, [queryClient])
}
