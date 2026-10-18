import { useEffect } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { api } from '../api/client'
import { ordersKey } from '../orders/orders'
import { stockKey } from '../stock/stock'
import { activityKey, activityLimit, type Activity, type ChangeSignal } from './activity'

const reconnectDelayMs = 5000

// Keeps one EventSource on /api/changes open while mounted, refetches the Order and Stock queries
// whenever an Order or a Stock record changes, and records every signal for the dashboard's feed.
// Signals carry no state; the read endpoints stay the source of truth.
export function useChangeStream() {
  const queryClient = useQueryClient()

  useEffect(() => {
    let source: EventSource
    let reconnect: ReturnType<typeof setTimeout> | undefined
    let unmounted = false

    // The feed lives only in the cache, so it must outlast the dashboard being off screen
    queryClient.setQueryDefaults(activityKey, { gcTime: Infinity })

    const invalidateOrders = () => void queryClient.invalidateQueries({ queryKey: ordersKey })
    const invalidateStock = () => void queryClient.invalidateQueries({ queryKey: stockKey })
    const record = (signal: ChangeSignal) =>
      queryClient.setQueryData<Activity[]>(activityKey, (activity = []) =>
        [{ ...signal, receivedAt: new Date() }, ...activity].slice(0, activityLimit),
      )

    const connect = () => {
      source = new EventSource('/api/changes')
      // Also catches up on signals missed while disconnected
      source.addEventListener('open', () => {
        invalidateOrders()
        invalidateStock()
      })
      source.addEventListener('change', (event: MessageEvent<string>) => {
        const signal = JSON.parse(event.data) as ChangeSignal
        record(signal)
        if (signal.kind === 'order') invalidateOrders()
        else if (signal.kind === 'stock') invalidateStock()
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
