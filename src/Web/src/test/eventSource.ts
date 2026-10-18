// jsdom has no EventSource. This stand-in records every instance so tests can drive the stream:
// emit open, change and error events, and check whether the code under test closed it.
export class FakeEventSource extends EventTarget {
  static readonly CONNECTING = 0
  static readonly OPEN = 1
  static readonly CLOSED = 2
  static instances: FakeEventSource[] = []

  readonly CONNECTING = 0
  readonly OPEN = 1
  readonly CLOSED = 2
  readonly url: string
  readonly withCredentials = false
  readyState = FakeEventSource.CONNECTING
  closed = false
  onopen: ((event: Event) => void) | null = null
  onmessage: ((event: MessageEvent) => void) | null = null
  onerror: ((event: Event) => void) | null = null

  constructor(url: string | URL) {
    super()
    this.url = String(url)
    FakeEventSource.instances.push(this)
  }

  // The newest instance, the one a reconnect opened last
  static latest() {
    return FakeEventSource.instances[FakeEventSource.instances.length - 1]
  }

  close() {
    this.closed = true
    this.readyState = FakeEventSource.CLOSED
  }

  emitOpen() {
    this.readyState = FakeEventSource.OPEN
    this.dispatchEvent(new Event('open'))
  }

  emitChange(signal: { kind: 'product' | 'stock' | 'order'; id: string; version: number }) {
    this.dispatchEvent(new MessageEvent('change', { data: JSON.stringify(signal) }))
  }

  // closed: the browser gave up (e.g. a 401), rather than reconnecting on its own
  emitError({ closed }: { closed: boolean }) {
    this.readyState = closed ? FakeEventSource.CLOSED : FakeEventSource.CONNECTING
    this.dispatchEvent(new Event('error'))
  }
}
