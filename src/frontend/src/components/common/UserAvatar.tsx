import { Bot } from 'lucide-react'
import { AuthedImg } from '../chat/AuthedImg'

const ZERO_GUID = '00000000-0000-0000-0000-000000000000'

interface UserAvatarProps {
  userId: string
  displayName: string
  profileImageUrl: string | null
  avatarUrl?: string | null
  size?: number
}

function colorFromUserId(userId: string | null | undefined): string {
  if (!userId) return 'hsl(0, 0%, 60%)'
  let hash = 0
  for (let i = 0; i < userId.length; i++) {
    hash = userId.charCodeAt(i) + ((hash << 5) - hash)
  }
  const h = Math.abs(hash) % 360
  return `hsl(${h}, 55%, 45%)`
}

export function UserAvatar({ userId, displayName, profileImageUrl, avatarUrl, size = 32 }: UserAvatarProps) {
  const circleStyle: React.CSSProperties = {
    width: size,
    height: size,
    borderRadius: '50%',
    flexShrink: 0,
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
  }

  if (!userId || userId === ZERO_GUID) {
    return (
      <div style={{ ...circleStyle, background: 'hsl(var(--muted))', color: 'hsl(var(--muted-foreground))' }}>
        <Bot style={{ width: size * 0.55, height: size * 0.55 }} strokeWidth={1.75} />
      </div>
    )
  }

  if (profileImageUrl) {
    return (
      <AuthedImg
        src={profileImageUrl}
        alt={displayName}
        className="rounded-full object-cover"
        style={{ width: size, height: size }}
      />
    )
  }

  if (avatarUrl) {
    return (
      <img
        src={avatarUrl}
        alt={displayName}
        style={{ width: size, height: size, borderRadius: '50%', flexShrink: 0, objectFit: 'cover' }}
      />
    )
  }

  return (
    <div
      style={{
        ...circleStyle,
        background: colorFromUserId(userId),
        color: '#fff',
        fontSize: size * 0.4,
        fontWeight: 600,
        userSelect: 'none',
      }}
    >
      {displayName.charAt(0).toUpperCase()}
    </div>
  )
}
