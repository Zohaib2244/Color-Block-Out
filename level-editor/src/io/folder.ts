/*
  Connecting the editor to a real folder of JSON files.

  The browser's File System Access API is the only way to get persistent
  read/write access to a directory the user picks, and it is Chromium-only.
  Everywhere else the editor falls back to `<input webkitdirectory>`, which can
  read a folder once but cannot write to it — so a fallback browser can load a
  folder of levels but not have its saves written back out.
*/

/** A file read out of a connected folder. */
export interface FolderFile {
  name: string;
  text: string;
}

type PermissionMode = "read" | "readwrite";

/*
  Hand-written rather than taken from lib.dom: the File System Access types
  land in different TypeScript versions, and `queryPermission` is not in the
  standard at all. Only the handful of members used here are declared.
*/
interface FileHandleLike {
  kind: "file";
  name: string;
  getFile(): Promise<File>;
  createWritable(): Promise<{ write(data: string): Promise<void>; close(): Promise<void> }>;
}

interface DirectoryHandleLike {
  kind: "directory";
  name: string;
  values(): AsyncIterableIterator<FileHandleLike | DirectoryHandleLike>;
  getFileHandle(name: string, options?: { create?: boolean }): Promise<FileHandleLike>;
  queryPermission?(descriptor: { mode: PermissionMode }): Promise<PermissionState>;
  requestPermission?(descriptor: { mode: PermissionMode }): Promise<PermissionState>;
}

export type FolderHandle = DirectoryHandleLike;

interface PickerWindow {
  showDirectoryPicker?: (options?: { mode?: PermissionMode; id?: string }) => Promise<DirectoryHandleLike>;
}

export function supportsFolders(): boolean {
  return typeof (window as unknown as PickerWindow).showDirectoryPicker === "function";
}

// ── handle persistence ──────────────────────────────────────────
//
// A directory handle is structured-cloneable but not JSON-serialisable, so it
// has to live in IndexedDB rather than in localStorage with everything else.

const DB_NAME = "cbo-level-editor";
const DB_STORE = "folders";

function openDb(): Promise<IDBDatabase> {
  return new Promise((resolve, reject) => {
    const request = indexedDB.open(DB_NAME, 1);
    request.onupgradeneeded = () => request.result.createObjectStore(DB_STORE);
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error);
  });
}

function withStore<T>(mode: IDBTransactionMode, run: (store: IDBObjectStore) => IDBRequest): Promise<T | undefined> {
  return openDb()
    .then(
      (db) =>
        new Promise<T | undefined>((resolve, reject) => {
          const request = run(db.transaction(DB_STORE, mode).objectStore(DB_STORE));
          request.onsuccess = () => resolve(request.result as T | undefined);
          request.onerror = () => reject(request.error);
        })
    )
    .catch(() => undefined);
}

export function rememberFolder(key: string, handle: FolderHandle): Promise<void> {
  return withStore("readwrite", (store) => store.put(handle, key)).then(() => undefined);
}

export function recallFolder(key: string): Promise<FolderHandle | undefined> {
  return withStore<FolderHandle>("readonly", (store) => store.get(key));
}

export function forgetFolder(key: string): Promise<void> {
  return withStore("readwrite", (store) => store.delete(key)).then(() => undefined);
}

// ── picking, reading, writing ───────────────────────────────────

/** Opens the OS folder picker. Resolves to null if the user cancels. */
export async function pickFolder(id: string): Promise<FolderHandle | null> {
  const pickerWindow = window as unknown as PickerWindow;
  if (!pickerWindow.showDirectoryPicker) return null;
  try {
    // Keep the method bound to window; browser APIs may reject an unbound
    // method call with an "Illegal invocation" TypeError.
    return await pickerWindow.showDirectoryPicker({ mode: "readwrite", id });
  } catch {
    return null; // the user dismissed the picker
  }
}

/**
 * Whether the handle may still be used, without prompting. A handle restored
 * from IndexedDB after a browser restart usually reports "prompt", which needs
 * a user gesture to clear — hence the separate `requestFolderPermission`.
 */
export async function queryFolderPermission(handle: FolderHandle, mode: PermissionMode = "readwrite"): Promise<boolean> {
  if (!handle.queryPermission) return true;
  try {
    return (await handle.queryPermission({ mode })) === "granted";
  } catch {
    return false;
  }
}

/** Must be called from a user gesture. */
export async function requestFolderPermission(handle: FolderHandle, mode: PermissionMode = "readwrite"): Promise<boolean> {
  if (!handle.requestPermission) return true;
  try {
    return (await handle.requestPermission({ mode })) === "granted";
  } catch {
    return false;
  }
}

/** Every *.json directly inside the folder. Subfolders are ignored. */
export async function readFolderJson(handle: FolderHandle): Promise<FolderFile[]> {
  const files: FolderFile[] = [];
  for await (const entry of handle.values()) {
    if (entry.kind !== "file" || !entry.name.toLowerCase().endsWith(".json")) continue;
    const file = await (entry as FileHandleLike).getFile();
    files.push({ name: entry.name, text: await file.text() });
  }
  return files;
}

export async function writeFolderJson(handle: FolderHandle, fileName: string, text: string): Promise<void> {
  const file = await handle.getFileHandle(fileName, { create: true });
  const writable = await file.createWritable();
  await writable.write(text);
  await writable.close();
}

/** Reads a FileList straight off an `<input webkitdirectory>` — the read-only fallback. */
export async function readFileListJson(list: FileList | null): Promise<FolderFile[]> {
  if (!list) return [];
  const files: FolderFile[] = [];
  for (const file of Array.from(list)) {
    if (!file.name.toLowerCase().endsWith(".json")) continue;
    files.push({ name: file.name, text: await file.text() });
  }
  return files;
}

/**
 * Turns a level or grid name into something safe to use as a file name:
 * Windows' reserved characters and whitespace collapse to hyphens, and a
 * trailing dot (also illegal on Windows) is trimmed.
 */
export function toFileName(name: string, fallback: string): string {
  const safe = name
    .trim()
    .replace(/[<>:"/\\|?*\s]+/g, "-")
    .replace(/\.+$/, "");
  return `${safe || fallback}.json`;
}
