import { useState } from 'react'
import { Plus, Trash2, X } from 'lucide-react'
import { createPoll } from '../../services/enrichedMessagingApiService'

interface Props {
  roomId: string
  onClose: () => void
}

export function PollCreator({ roomId, onClose }: Props) {
  const [question, setQuestion] = useState('')
  const [options, setOptions] = useState(['', ''])
  const [voteMode, setVoteMode] = useState<'single' | 'multi'>('single')
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const addOption = () => { if (options.length < 10) setOptions([...options, '']) }
  const removeOption = (i: number) => { if (options.length > 2) setOptions(options.filter((_, idx) => idx !== i)) }
  const updateOption = (i: number, val: string) => setOptions(options.map((o, idx) => idx === i ? val : o))

  const handleSubmit = async () => {
    const validOptions = options.filter(o => o.trim())
    if (!question.trim()) { setError('Question is required.'); return }
    if (validOptions.length < 2) { setError('At least 2 non-empty options required.'); return }
    setLoading(true)
    setError(null)
    try {
      await createPoll({ roomId, question: question.trim(), options: validOptions, voteMode })
      onClose()
    } catch {
      setError('Failed to create poll.')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50">
      <div className="bg-background border border-border rounded-lg p-4 w-full max-w-md shadow-lg">
        <div className="flex items-center justify-between mb-4">
          <h2 className="font-semibold text-lg">Create Poll</h2>
          <button onClick={onClose}><X size={18} /></button>
        </div>
        <div className="space-y-3">
          <input
            className="w-full border border-border rounded px-3 py-2 text-sm bg-background"
            placeholder="Poll question…"
            value={question}
            onChange={e => setQuestion(e.target.value)}
            maxLength={500}
          />
          <div className="space-y-2">
            {options.map((opt, i) => (
              <div key={i} className="flex gap-2">
                <input
                  className="flex-1 border border-border rounded px-3 py-2 text-sm bg-background"
                  placeholder={`Option ${i + 1}`}
                  value={opt}
                  onChange={e => updateOption(i, e.target.value)}
                  maxLength={200}
                />
                {options.length > 2 && (
                  <button onClick={() => removeOption(i)} className="text-muted-foreground hover:text-destructive">
                    <Trash2 size={16} />
                  </button>
                )}
              </div>
            ))}
          </div>
          {options.length < 10 && (
            <button onClick={addOption} className="flex items-center gap-1 text-sm text-primary">
              <Plus size={14} /> Add option
            </button>
          )}
          <label className="flex items-center gap-2 text-sm cursor-pointer">
            <input
              type="checkbox"
              checked={voteMode === 'multi'}
              onChange={e => setVoteMode(e.target.checked ? 'multi' : 'single')}
            />
            Allow multiple selections
          </label>
          {error && <p className="text-sm text-destructive">{error}</p>}
          <button
            disabled={loading}
            onClick={handleSubmit}
            className="w-full bg-primary text-primary-foreground rounded px-3 py-2 text-sm font-medium hover:bg-primary/90 disabled:opacity-50"
          >
            {loading ? 'Creating…' : 'Create Poll'}
          </button>
        </div>
      </div>
    </div>
  )
}
