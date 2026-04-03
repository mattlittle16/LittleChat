import { describe, it, expect, beforeEach, vi } from 'vitest'
import { useSidebarGroupStore } from '../stores/sidebarGroupStore'
import type { SidebarGroup } from '../types'

vi.mock('../services/apiClient', () => ({
  api: {
    get: vi.fn(),
    post: vi.fn(),
    patch: vi.fn().mockResolvedValue(undefined),
    put: vi.fn().mockResolvedValue(undefined),
    delete: vi.fn().mockResolvedValue(undefined),
    putForm: vi.fn().mockResolvedValue(undefined),
  },
}))

import { api } from '../services/apiClient'
const mockApi = api as unknown as {
  get: ReturnType<typeof vi.fn>
  post: ReturnType<typeof vi.fn>
  patch: ReturnType<typeof vi.fn>
  put: ReturnType<typeof vi.fn>
  delete: ReturnType<typeof vi.fn>
}

function makeGroup(overrides: Partial<SidebarGroup> = {}): SidebarGroup {
  return {
    id: 'g1',
    name: 'Group 1',
    displayOrder: 0,
    isCollapsed: false,
    roomIds: [],
    ...overrides,
  }
}

beforeEach(() => {
  useSidebarGroupStore.setState({ groups: [] })
  vi.clearAllMocks()
})

describe('assignRoom', () => {
  it('adds a room to the target group', async () => {
    useSidebarGroupStore.setState({ groups: [makeGroup({ id: 'g1', roomIds: [] })] })
    await useSidebarGroupStore.getState().assignRoom('g1', 'r1')
    expect(useSidebarGroupStore.getState().groups[0].roomIds).toContain('r1')
  })

  it('removes the room from all other groups (exclusive membership)', async () => {
    useSidebarGroupStore.setState({
      groups: [
        makeGroup({ id: 'g1', roomIds: ['r1', 'r2'] }),
        makeGroup({ id: 'g2', roomIds: [] }),
      ],
    })
    await useSidebarGroupStore.getState().assignRoom('g2', 'r1')
    const groups = useSidebarGroupStore.getState().groups
    expect(groups.find(g => g.id === 'g1')!.roomIds).not.toContain('r1')
    expect(groups.find(g => g.id === 'g2')!.roomIds).toContain('r1')
  })

  it('does not duplicate the room if already in the target group', async () => {
    useSidebarGroupStore.setState({ groups: [makeGroup({ id: 'g1', roomIds: ['r1'] })] })
    await useSidebarGroupStore.getState().assignRoom('g1', 'r1')
    expect(useSidebarGroupStore.getState().groups[0].roomIds.filter(id => id === 'r1')).toHaveLength(1)
  })
})

describe('unassignRoom', () => {
  it('removes the room from all groups', async () => {
    useSidebarGroupStore.setState({
      groups: [
        makeGroup({ id: 'g1', roomIds: ['r1', 'r2'] }),
        makeGroup({ id: 'g2', roomIds: ['r1'] }),
      ],
    })
    await useSidebarGroupStore.getState().unassignRoom('r1')
    const groups = useSidebarGroupStore.getState().groups
    expect(groups.find(g => g.id === 'g1')!.roomIds).not.toContain('r1')
    expect(groups.find(g => g.id === 'g2')!.roomIds).not.toContain('r1')
    // Other rooms preserved
    expect(groups.find(g => g.id === 'g1')!.roomIds).toContain('r2')
  })
})

describe('reorderRooms', () => {
  it('updates the target group roomIds optimistically', async () => {
    useSidebarGroupStore.setState({ groups: [makeGroup({ id: 'g1', roomIds: ['r1', 'r2', 'r3'] })] })
    await useSidebarGroupStore.getState().reorderRooms('g1', ['r3', 'r1', 'r2'])
    expect(useSidebarGroupStore.getState().groups[0].roomIds).toEqual(['r3', 'r1', 'r2'])
  })

  it('rolls back to the previous state when the API call fails', async () => {
    const original = ['r1', 'r2']
    useSidebarGroupStore.setState({ groups: [makeGroup({ id: 'g1', roomIds: original })] })
    mockApi.patch.mockRejectedValueOnce(new Error('server error'))

    await expect(
      useSidebarGroupStore.getState().reorderRooms('g1', ['r2', 'r1'])
    ).rejects.toThrow('Failed to save topic order')

    expect(useSidebarGroupStore.getState().groups[0].roomIds).toEqual(original)
  })
})
