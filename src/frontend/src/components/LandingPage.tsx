import logo from '../assets/logo.svg'
import { login } from '../services/authService'

export function LandingPage() {
  return (
    <div
      className="relative flex min-h-screen items-center justify-center overflow-hidden"
      style={{
        background: 'hsl(252 14% 97%)',
        backgroundImage: 'radial-gradient(circle, hsl(252 20% 78% / 0.5) 1px, transparent 1px)',
        backgroundSize: '24px 24px',
      }}
    >
      {/* Static purple glow behind card */}
      <div
        aria-hidden="true"
        style={{
          position: 'absolute',
          width: '560px',
          height: '560px',
          borderRadius: '50%',
          background: 'radial-gradient(ellipse at center, hsl(258 65% 52% / 0.18) 0%, hsl(258 65% 52% / 0.06) 50%, transparent 70%)',
          filter: 'blur(48px)',
          pointerEvents: 'none',
        }}
      />

      {/* Unified hero card */}
      <div
        className="relative z-10 flex flex-col items-center"
        style={{
          width: '360px',
          background: 'hsl(0 0% 100%)',
          border: '1px solid hsl(252 10% 90%)',
          borderRadius: '20px',
          boxShadow: '0 4px 6px hsl(252 12% 10% / 0.04), 0 12px 32px hsl(252 12% 10% / 0.10), 0 0 0 1px hsl(258 30% 88% / 0.3)',
          padding: '32px 28px 28px',
        }}
      >
        {/* Logo mark */}
        <div
          style={{
            width: '60px',
            height: '60px',
            borderRadius: '16px',
            background: 'hsl(258 65% 52% / 0.08)',
            border: '1px solid hsl(258 65% 52% / 0.18)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            boxShadow: '0 2px 10px hsl(258 65% 52% / 0.12)',
            marginBottom: '14px',
          }}
        >
          <img src={logo} alt="LittleChat" style={{ width: '34px', height: '34px' }} />
        </div>

        <h1
          className="text-2xl font-bold tracking-tight"
          style={{ color: 'hsl(252 12% 10%)', letterSpacing: '-0.02em', marginBottom: '3px' }}
        >
          LittleChat
        </h1>
        <p className="text-sm" style={{ color: 'hsl(252 8% 52%)', marginBottom: '20px' }}>
          Your private space to talk.
        </p>

        {/* Decorative chat preview */}
        <div
          style={{
            width: '100%',
            background: 'hsl(252 14% 97%)',
            border: '1px solid hsl(252 10% 90%)',
            borderRadius: '12px',
            padding: '12px 14px',
            display: 'flex',
            flexDirection: 'column',
            gap: '8px',
            marginBottom: '20px',
          }}
        >
          {/* Received bubble */}
          <div style={{ display: 'flex', justifyContent: 'flex-start' }}>
            <span
              style={{
                background: 'hsl(252 10% 92%)',
                color: 'hsl(252 12% 20%)',
                borderRadius: '16px 16px 16px 4px',
                padding: '6px 12px',
                fontSize: '12px',
                maxWidth: '80%',
              }}
            >
              Hey, is everyone here? 👋
            </span>
          </div>

          {/* Sent bubble */}
          <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
            <span
              style={{
                background: 'hsl(258 65% 52%)',
                color: '#fff',
                borderRadius: '16px 16px 4px 16px',
                padding: '6px 12px',
                fontSize: '12px',
                maxWidth: '80%',
              }}
            >
              Just signed in 😊
            </span>
          </div>

          {/* Typing indicator */}
          <div style={{ display: 'flex', justifyContent: 'flex-start' }}>
            <span
              style={{
                background: 'hsl(252 10% 92%)',
                borderRadius: '16px 16px 16px 4px',
                padding: '8px 14px',
                display: 'flex',
                alignItems: 'center',
                gap: '4px',
              }}
            >
              <span className="typing-dot" />
              <span className="typing-dot" />
              <span className="typing-dot" />
            </span>
          </div>
        </div>

        <div style={{ width: '100%', height: '1px', background: 'hsl(252 10% 90%)', marginBottom: '20px' }} />

        <button
          onClick={login}
          className="w-full rounded-lg px-6 py-2.5 text-sm font-medium transition-opacity hover:opacity-90 active:opacity-80"
          style={{
            background: 'hsl(258 65% 52%)',
            color: '#fff',
            letterSpacing: '0.01em',
          }}
        >
          Sign In
        </button>
      </div>
    </div>
  )
}
