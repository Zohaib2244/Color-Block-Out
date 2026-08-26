import { useRef, useState } from "react";
import type { Folder, FolderFile } from "../io/useFolder";
import { FolderIcon } from "./icons";

interface FolderBarProps {
  folder: Folder;
  /** What the folder holds, e.g. "grids". Used in labels and messages. */
  noun: string;
  /** Handed every JSON file found, and returns what to say about the result. */
  onFiles: (files: FolderFile[]) => Promise<string> | string;
}

/**
 * Connects a screen to a folder of JSON files on disk.
 *
 * Where the File System Access API exists (Chromium) the folder is remembered
 * and written back to, so saving also drops a `.json` next to the others.
 * Everywhere else this degrades to a one-shot `webkitdirectory` read, which
 * can load a folder but not write to it — stated plainly rather than silently
 * dropping saves on the floor.
 */
export function FolderBar({ folder, noun, onFiles }: FolderBarProps) {
  const [status, setStatus] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const fallbackRef = useRef<HTMLInputElement>(null);

  async function run(load: () => Promise<FolderFile[] | null>) {
    setBusy(true);
    try {
      const files = await load();
      if (files === null) return; // cancelled, or permission refused
      setStatus(await onFiles(files));
    } catch (error) {
      setStatus(error instanceof Error ? error.message : String(error));
    } finally {
      setBusy(false);
    }
  }

  if (!folder.supported) {
    return (
      <div className="folder-bar">
        <input
          ref={(el) => {
            fallbackRef.current = el;
            // Not a React prop — set on the element so TS doesn't need a
            // module augmentation for a non-standard attribute.
            if (el) el.setAttribute("webkitdirectory", "");
          }}
          type="file"
          multiple
          accept="application/json"
          style={{ display: "none" }}
          onChange={(e) => {
            const list = e.target.files;
            void run(() => folder.readFileList(list));
            e.target.value = "";
          }}
        />
        <button disabled={busy} onClick={() => fallbackRef.current?.click()} title={`Load every JSON in a ${noun} folder`}>
          <FolderIcon />
          Open Folder
        </button>
        <span className="muted folder-note">Read-only in this browser — saves won't be written back.</span>
        {status && <span className="folder-status">{status}</span>}
      </div>
    );
  }

  return (
    <div className="folder-bar">
      <button disabled={busy} onClick={() => void run(folder.choose)} title={`Pick a folder of ${noun} JSON files`}>
        <FolderIcon />
        {folder.name ? "Change Folder" : "Open Folder"}
      </button>

      {folder.needsReconnect && (
        <button disabled={busy} onClick={() => void run(folder.reconnect)} title="Grant access to this folder again">
          Reconnect
        </button>
      )}
      {folder.ready && (
        <button disabled={busy} onClick={() => void run(folder.reload)} title="Re-read every JSON in the folder">
          Reload
        </button>
      )}

      {folder.name && (
        <span className={folder.ready ? "folder-name" : "folder-name stale"} title={folder.name}>
          {folder.name}
          {!folder.ready && " (locked)"}
        </span>
      )}
      {folder.name && (
        <button className="danger-link" onClick={() => void folder.disconnect()} title="Stop using this folder">
          ×
        </button>
      )}

      {status && <span className="folder-status">{status}</span>}
    </div>
  );
}
