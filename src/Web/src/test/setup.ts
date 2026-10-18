import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterAll, afterEach, beforeAll } from 'vitest'
import { FakeEventSource } from './eventSource'
import { server } from './server'

// Mantine reads these, and jsdom doesn't implement them
Object.defineProperty(window, 'matchMedia', {
  writable: true,
  value: (query: string) => ({
    matches: false,
    media: query,
    onchange: null,
    addListener: () => {},
    removeListener: () => {},
    addEventListener: () => {},
    removeEventListener: () => {},
    dispatchEvent: () => false,
  }),
})

class ResizeObserverStub {
  observe() {}
  unobserve() {}
  disconnect() {}
}
window.ResizeObserver = ResizeObserverStub
// A Select scrolls its selected option into view when the dropdown opens
Element.prototype.scrollIntoView = () => {}

// jsdom has no EventSource; the Shell opens one on /api/changes
window.EventSource = FakeEventSource as unknown as typeof EventSource

// MSW 3's name for MSW 2's onUnhandledRequest: a request no handler matches fails the test
beforeAll(() => server.listen({ onUnhandledFrame: 'error' }))
afterEach(() => {
  cleanup()
  server.resetHandlers()
  FakeEventSource.instances = []
})
afterAll(() => server.close())
