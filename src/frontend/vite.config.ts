import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import path from 'path'
import { readFileSync } from 'fs'

const versionJson = JSON.parse(readFileSync('./public/version.json', 'utf-8')) as { version: string }

// https://vite.dev/config/
export default defineConfig({
  define: {
    __APP_VERSION__: JSON.stringify(versionJson.version),
  },
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  server: {
    port: 3000,
    proxy: {
      '/api': 'http://localhost:5000',
      '/hubs': {
        target: 'http://localhost:5000',
        ws: true,
      },
      '/auth/login': 'http://localhost:5000',
      '/auth/logout': 'http://localhost:5000',
      '/auth/oidc-callback': 'http://localhost:5000',
    },
  },
})
