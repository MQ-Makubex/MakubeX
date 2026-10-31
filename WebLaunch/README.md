# MakubeX Chapter01 public playtest

Compiled Unity v010 browser player with the 2026-10-29 art upgrade. Desktop WASD / hold right mouse to look / E to interact / ESC settings on the memory map. First visit downloads the game assets. Chapter progression stays in this browser's local storage.

Only compiled browser player files belong in this repository. Unity/Blender source projects, source audio package and verification logs remain in the local workspace.

Resource binaries are gzip-compressed and split below the hosting asset size limit. The page assembles Blob URLs after native browser decompression. This preserves the built resources byte-for-byte; it does not run a substitute game.

Web entry: the approved cover appears first. Only Start loads the Unity loader, resource manifest and core player binaries. Download and engine initialization progress stays at the bottom of the cover. The single Start click is queued into Unity's `MakubeX App.StartFromWeb` method, which advances to the memory map after its artwork is ready. Loading errors keep the cover visible and offer a reload. The page starts no music before Start and attempts to quit its own player on pagehide.

Run controller regression checks with Node 24: `node --test tests/launch.test.cjs`. Browser QA must additionally verify the real Unity splash and memory map; the controller fixture does not substitute for a Unity run.

This directory publishes the v010 web entry source, its controller test, and the resource manifest used by that release. The manifest refers to generated Unity WebGL files and compressed chunks, which are not included here; it is a record of the deployed build rather than a standalone development server. The Unity game source is in `../UnityProject/`.
