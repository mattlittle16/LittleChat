# Notification Audio Asset

`notification.mp3` must be added to this directory before notification sounds will work.

## Requirements

- File name: `notification.mp3`
- Duration: 0.3–0.5 seconds
- Style: Soft chime (subtle, non-intrusive ping)
- Format: MP3, mono or stereo, 44.1kHz recommended

## How It Is Used

`src/frontend/src/services/notificationService.ts` creates a lazy `HTMLAudioElement`
pointing to this bundled asset. The file is imported at build time by Vite and its
hashed URL is embedded in the bundle.

## Obtaining the Asset

You can source a suitable chime from any royalty-free sound library (e.g., freesound.org,
mixkit.co) or record/generate one yourself. Place the resulting file at:

    src/frontend/src/assets/notification.mp3

Once the file is present, no code changes are required — the import in
`notificationService.ts` will resolve automatically at the next `npm run dev` or
`npm run build`.
