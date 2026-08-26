import { useCallback, useEffect, useState } from "react";
import {
  forgetFolder,
  pickFolder,
  queryFolderPermission,
  readFileListJson,
  readFolderJson,
  recallFolder,
  rememberFolder,
  requestFolderPermission,
  supportsFolders,
  writeFolderJson,
} from "./folder";
import type { FolderFile, FolderHandle } from "./folder";

// Re-exported so screens only need one import for the folder feature.
export type { FolderFile } from "./folder";

export interface Folder {
  /** Whether this browser can hold on to a folder at all (Chromium only). */
  supported: boolean;
  /** Name of the connected folder, or null when none is attached. */
  name: string | null;
  /** True when the folder is attached AND writable right now. */
  ready: boolean;
  /** A folder is remembered but its permission lapsed — offer Reconnect. */
  needsReconnect: boolean;
  /** Opens the picker. Resolves to the folder's JSON files, or null if cancelled. */
  choose: () => Promise<FolderFile[] | null>;
  /** Re-asks for permission on the remembered folder, without re-picking it. */
  reconnect: () => Promise<FolderFile[] | null>;
  disconnect: () => Promise<void>;
  /** Re-reads the attached folder. Null when nothing is attached. */
  reload: () => Promise<FolderFile[] | null>;
  /** Writes one JSON file. A no-op (returning false) when no folder is attached. */
  write: (fileName: string, text: string) => Promise<boolean>;
  /** Read-only fallback for browsers with no File System Access API. */
  readFileList: (list: FileList | null) => Promise<FolderFile[]>;
}

/**
 * Attaches the editor to a folder of JSON files, remembered across sessions.
 *
 * `key` namespaces the stored handle, so grids and levels can be pointed at
 * two different folders at once.
 */
export function useFolder(key: string): Folder {
  const [handle, setHandle] = useState<FolderHandle | null>(null);
  const [ready, setReady] = useState(false);
  const supported = supportsFolders();

  // A handle survives a restart in IndexedDB, but its permission usually does
  // not — restore it anyway, so the UI can offer Reconnect rather than making
  // the user hunt for the folder again.
  useEffect(() => {
    let cancelled = false;
    void recallFolder(key).then(async (stored) => {
      if (!stored || cancelled) return;
      const granted = await queryFolderPermission(stored);
      if (cancelled) return;
      setHandle(stored);
      setReady(granted);
    });
    return () => {
      cancelled = true;
    };
  }, [key]);

  const choose = useCallback(async () => {
    const picked = await pickFolder(key);
    if (!picked) return null;
    setHandle(picked);
    setReady(true);
    await rememberFolder(key, picked);
    return readFolderJson(picked);
  }, [key]);

  const reconnect = useCallback(async () => {
    if (!handle) return null;
    const granted = await requestFolderPermission(handle);
    setReady(granted);
    return granted ? readFolderJson(handle) : null;
  }, [handle]);

  const reload = useCallback(async () => {
    if (!handle || !ready) return null;
    return readFolderJson(handle);
  }, [handle, ready]);

  const disconnect = useCallback(async () => {
    setHandle(null);
    setReady(false);
    await forgetFolder(key);
  }, [key]);

  const write = useCallback(
    async (fileName: string, text: string) => {
      if (!handle || !ready) return false;
      try {
        await writeFolderJson(handle, fileName, text);
        return true;
      } catch {
        // A folder can go away underneath us (unplugged drive, revoked grant);
        // that must not take the in-browser save down with it.
        setReady(false);
        return false;
      }
    },
    [handle, ready]
  );

  return {
    supported,
    name: handle?.name ?? null,
    ready,
    needsReconnect: !!handle && !ready,
    choose,
    reconnect,
    disconnect,
    reload,
    write,
    readFileList: readFileListJson,
  };
}
