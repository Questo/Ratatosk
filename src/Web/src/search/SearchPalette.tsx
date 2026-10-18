import { useId, useState, type KeyboardEvent } from 'react'
import { ActionIcon, Alert, Box, Button, Center, Group, Input, Kbd, Loader, Text } from '@mantine/core'
import { useDebouncedValue, useHotkeys } from '@mantine/hooks'
import { createSpotlight, Spotlight } from '@mantine/spotlight'
import { useQuery } from '@tanstack/react-query'
import { searchQuery, type SearchHit } from './search'

const minTermLength = 2

function SearchIcon() {
  return (
    <svg width={18} height={18} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2} aria-hidden="true">
      <circle cx="11" cy="11" r="7" />
      <path d="m20 20-3.5-3.5" />
    </svg>
  )
}

// The header's command palette: Spotlight's modal, mod+K hotkey and action list, fed by GET /search.
// The server does the matching, so Spotlight's own client-side filtering isn't used.
export function SearchPalette({ onSelect }: { onSelect: (hit: SearchHit) => void }) {
  // One store per palette, so tests (and remounts) start closed
  const [[store, palette]] = useState(createSpotlight)
  const [input, setInput] = useState('')
  const trimmed = input.trim()
  const [term] = useDebouncedValue(trimmed, 250)
  const search = useQuery({ ...searchQuery(term), enabled: term.length >= minTermLength })
  const listId = `search-results-${useId().replace(/[^a-zA-Z0-9_-]/g, '')}`

  // Spotlight's own shortcut (mod+K) fires in inputs too; "/" is only a shortcut outside them.
  // shift+/ covers layouts where "/" needs Shift (it is "?" on US layouts, so it never matches there)
  useHotkeys([
    ['/', palette.open],
    ['shift+/', palette.open],
  ])

  const groups = search.data?.groups.filter((group) => group.hits.length > 0 || group.failed) ?? []
  const hits = groups.flatMap((group) => group.hits)
  const optionId = (hit: SearchHit) => `${listId}-${hit.kind}-${hit.id}`
  const showResults = trimmed.length >= minTermLength && search.isSuccess

  // The highlight belongs to the results it was moved in, so new results start on the first hit.
  // It is kept here rather than in Spotlight's store, whose selection wraps around at the ends
  const [highlight, setHighlight] = useState({ data: search.data, index: 0 })
  const highlighted = showResults ? hits[highlight.data === search.data ? highlight.index : 0] : undefined

  const choose = (hit: SearchHit) => {
    palette.close()
    onSelect(hit)
  }

  const onKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
      event.preventDefault()
      if (!highlighted) return
      const next = hits.indexOf(highlighted) + (event.key === 'ArrowDown' ? 1 : -1)
      setHighlight({ data: search.data, index: Math.min(Math.max(next, 0), hits.length - 1) })
    } else if (event.key === 'Enter' && highlighted) {
      event.preventDefault()
      choose(highlighted)
    }
  }

  let content
  if (trimmed.length < minTermLength) {
    content = <Text c="dimmed">Type at least 2 characters to search Products and Orders</Text>
  } else if (search.isError) {
    content = (
      <Alert color="red" role="alert">
        <Group justify="space-between">
          <Text size="sm">{search.error.message}</Text>
          <Button size="xs" variant="default" onClick={() => void search.refetch()}>
            Retry
          </Button>
        </Group>
      </Alert>
    )
  } else if (search.isPending) {
    content = (
      <Center>
        <Loader aria-label="Searching" />
      </Center>
    )
  } else if (groups.length === 0) {
    content = <Spotlight.Empty>No matches for "{term}"</Spotlight.Empty>
  }

  return (
    <>
      <Button
        variant="default"
        visibleFrom="sm"
        leftSection={<SearchIcon />}
        rightSection={<Kbd size="xs">Ctrl K</Kbd>}
        onClick={palette.open}
      >
        Search...
      </Button>
      <ActionIcon variant="default" size="lg" hiddenFrom="sm" aria-label="Search" onClick={palette.open}>
        <SearchIcon />
      </ActionIcon>

      <Spotlight.Root
        store={store}
        title="Search"
        // Spotlight zeroes the modal's padding, which leaves the title against the edge
        styles={{ header: { paddingInline: 'var(--mantine-spacing-md)' } }}
        query={input}
        onQueryChange={setInput}
        tagsToIgnore={[]}
      >
        <Input
          data-autofocus
          size="lg"
          classNames={{ input: Spotlight.classes.search }}
          leftSection={<SearchIcon />}
          placeholder="Search Products and Orders..."
          aria-label="Search Products and Orders"
          role="combobox"
          aria-autocomplete="list"
          aria-expanded={showResults && hits.length > 0}
          aria-controls={listId}
          aria-activedescendant={highlighted && optionId(highlighted)}
          value={input}
          onChange={(event) => setInput(event.currentTarget.value)}
          onKeyDown={onKeyDown}
        />
        {content && <Box p="md">{content}</Box>}
        {showResults && groups.length > 0 && (
          <Spotlight.ActionsList id={listId} role="listbox" aria-label="Search results">
            {groups.map((group) => {
              const headingId = `${listId}-${group.kind}`
              return (
                // Spotlight draws its group label with CSS, which screen readers can't name a group by
                <Spotlight.ActionsGroup key={group.kind} label="" role="group" aria-labelledby={headingId}>
                  <Text id={headingId} component="h3" size="xs" fw={700} c="dimmed" tt="uppercase" px="md" mb={4}>
                    {group.label}
                  </Text>
                  {group.failed ? (
                    <Text size="sm" c="red" px="md">
                      Couldn't search {group.label}.
                    </Text>
                  ) : (
                    group.hits.map((hit) => (
                      <Spotlight.Action
                        key={hit.id}
                        // Spotlight.Action drops an id prop, and aria-activedescendant needs one
                        ref={(node: HTMLButtonElement | null) => {
                          if (node) node.id = optionId(hit)
                        }}
                        role="option"
                        aria-selected={hit === highlighted}
                        data-selected={hit === highlighted || undefined}
                        label={hit.title}
                        description={hit.subtitle}
                        onClick={() => choose(hit)}
                      />
                    ))
                  )}
                </Spotlight.ActionsGroup>
              )
            })}
          </Spotlight.ActionsList>
        )}
      </Spotlight.Root>
    </>
  )
}
