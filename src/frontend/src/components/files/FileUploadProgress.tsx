interface FileUploadProgressProps {
  fileName: string
  progress: number // 0-100
}

export function FileUploadProgress({ fileName, progress }: FileUploadProgressProps) {
  return (
    <div className="flex items-center gap-2 text-xs text-muted-foreground">
      <span className="truncate max-w-[160px]">📎 {fileName}</span>
      <div className="flex-1 h-1.5 rounded-full bg-muted overflow-hidden min-w-[80px]">
        <div
          className="h-full bg-primary transition-all duration-150"
          style={{ width: `${progress}%` }}
        />
      </div>
      <span className="w-8 text-right">{progress}%</span>
    </div>
  )
}
