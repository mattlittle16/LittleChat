import { openDB, type IDBPDatabase } from 'idb'
import type { OutboxMessage } from '../types'

const DB_NAME = 'littlechat-outbox'
const STORE = 'messages'

let dbPromise: Promise<IDBPDatabase> | null = null

function getDb(): Promise<IDBPDatabase> {
  if (!dbPromise) {
    dbPromise = openDB(DB_NAME, 1, {
      upgrade(db) {
        if (!db.objectStoreNames.contains(STORE)) {
          db.createObjectStore(STORE, { keyPath: 'clientId' })
        }
      },
    })
  }
  return dbPromise
}

export async function addToOutbox(message: OutboxMessage): Promise<void> {
  const db = await getDb()
  await db.put(STORE, message)
}

export async function deleteFromOutbox(clientId: string): Promise<void> {
  const db = await getDb()
  await db.delete(STORE, clientId)
}

export async function getAllPending(): Promise<OutboxMessage[]> {
  const db = await getDb()
  return db.getAll(STORE)
}

export async function updateStatus(
  clientId: string,
  status: OutboxMessage['status']
): Promise<void> {
  const db = await getDb()
  const existing = await db.get(STORE, clientId)
  if (existing) {
    await db.put(STORE, { ...existing, status })
  }
}
