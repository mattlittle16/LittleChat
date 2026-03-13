import { AuthedImg } from '../chat/AuthedImg'

interface UserAvatarProps {
  userId: string
  displayName: string
  profileImageUrl: string | null
  avatarUrl?: string | null
  size?: number
}

function colorFromUserId(userId: string): string {
  // Simple deterministic color from userId characters
  let hash = 0
  for (let i = 0; i < userId.length; i++) {
    hash = userId.charCodeAt(i) + ((hash << 5) - hash)
  }
  const h = Math.abs(hash) % 360
  return `hsl(${h}, 55%, 45%)`
}

export function UserAvatar({ userId, displayName, profileImageUrl, avatarUrl, size = 32 }: UserAvatarProps) {
  const style: React.CSSProperties = {
    width: size,
    height: size,
    borderRadius: '50%',
    flexShrink: 0,
    objectFit: 'cover',
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
        style={style}
      />
    )
  }

  return (
    <div
      style={{
        ...style,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
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
