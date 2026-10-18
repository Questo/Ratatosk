import { Center, Loader } from '@mantine/core'
import { useQuery } from '@tanstack/react-query'
import { LoginScreen } from './auth/LoginScreen'
import { noAdminAccessMessage, sessionQuery } from './auth/session'
import { Shell } from './shell/Shell'

export function App() {
  const session = useQuery(sessionQuery)

  if (session.isPending) {
    return (
      <Center mih="100vh">
        <Loader />
      </Center>
    )
  }

  if (session.data?.status === 'admin') return <Shell email={session.data.email} />

  return <LoginScreen notice={session.data?.status === 'notAdmin' ? noAdminAccessMessage : undefined} />
}
