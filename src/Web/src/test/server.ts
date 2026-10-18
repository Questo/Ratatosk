import { setupServer } from 'msw/node'

// Each test registers its own /api handlers with server.use(...)
export const server = setupServer()
