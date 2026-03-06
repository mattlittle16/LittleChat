import { login } from '../services/authService'

export function LandingPage() {
  return (
    <div
      className="relative flex min-h-screen items-center justify-center overflow-hidden"
      style={{ background: 'hsl(var(--background))' }}
    >
      {/* Decorative shapes */}
      <div aria-hidden="true" className="pointer-events-none absolute inset-0">
        {/* Top-left: large organic blob */}
        <div style={{
          position: 'absolute',
          top: '-8%',
          left: '-12%',
          width: '52%',
          paddingBottom: '46%',
          borderRadius: '62% 38% 46% 54% / 60% 44% 56% 40%',
          background: 'radial-gradient(ellipse at 40% 40%, hsl(243 72% 60% / 0.82), transparent 68%)',
          filter: 'blur(40px)',
          transform: 'rotate(-12deg)',
        }} />
        {/* Bottom-right: wide stretched diamond-ish */}
        <div style={{
          position: 'absolute',
          bottom: '-10%',
          right: '-8%',
          width: '48%',
          paddingBottom: '40%',
          borderRadius: '38% 62% 55% 45% / 48% 38% 62% 52%',
          background: 'radial-gradient(ellipse at 60% 60%, hsl(260 72% 65% / 0.75), transparent 68%)',
          filter: 'blur(48px)',
          transform: 'rotate(18deg)',
        }} />
        {/* Mid-right: smaller accent shape */}
        <div style={{
          position: 'absolute',
          top: '35%',
          right: '8%',
          width: '28%',
          paddingBottom: '32%',
          borderRadius: '44% 56% 38% 62% / 55% 42% 58% 45%',
          background: 'radial-gradient(ellipse at 50% 50%, hsl(220 72% 65% / 0.65), transparent 65%)',
          filter: 'blur(36px)',
          transform: 'rotate(8deg)',
        }} />
        {/* Bottom-left: thin horizontal bar shape */}
        <div style={{
          position: 'absolute',
          bottom: '15%',
          left: '5%',
          width: '30%',
          paddingBottom: '14%',
          borderRadius: '70% 30% 65% 35% / 40% 60% 40% 60%',
          background: 'radial-gradient(ellipse at 40% 60%, hsl(250 65% 68% / 0.60), transparent 70%)',
          filter: 'blur(32px)',
          transform: 'rotate(-6deg)',
        }} />
      </div>

      {/* Sign-in card */}
      <div
        className="relative z-10 flex flex-col items-center gap-5 rounded-2xl px-10 py-10 shadow-2xl"
        style={{
          background: 'hsl(var(--background) / 0.75)',
          backdropFilter: 'blur(16px)',
          border: '1px solid hsl(var(--border))',
          minWidth: '320px',
        }}
      >
        <div className="flex flex-col items-center gap-1">
          <h1 className="text-2xl font-bold tracking-tight" style={{ color: 'hsl(var(--foreground))' }}>
            MattLab Chat
          </h1>
          <p className="text-sm" style={{ color: 'hsl(var(--muted-foreground))' }}>
            A private chat for your group.
          </p>
        </div>

        <button
          onClick={login}
          className="w-full rounded-lg px-6 py-2.5 text-sm font-medium transition-opacity hover:opacity-90"
          style={{
            background: 'hsl(var(--primary))',
            color: 'hsl(var(--primary-foreground))',
          }}
        >
          Sign In
        </button>
      </div>
    </div>
  )
}
