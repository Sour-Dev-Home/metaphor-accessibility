# Where Steam keeps its libraries on Windows

- **Source:** a Steam client install on Windows 11 (steam.exe file version 11.23.65.88), read with PowerShell 7.6.10:
  `Get-ItemProperty HKCU:\Software\Valve\Steam` and `Get-Content` on the files below. Nothing was launched or changed.
- **Saved:** 2026-11-18.
- **Game build:** none; this is about Steam, not the game. `scripts/GameLocator.psm1` relies on it (#267).

## Steps

1. `(Get-ItemProperty HKCU:\Software\Valve\Steam).SteamPath` gives Steam's folder, with forward slashes
   (`X:/.../Steam`).
2. `<SteamPath>/steamapps/libraryfolders.vdf` lists the libraries. Its shape, with the values replaced:

   ```text
   "libraryfolders"
   {
       "0"
       {
           "path"      "<folder, backslashes doubled>"
           "label"     "<value>"
           "contentid" "<value>"
           ...
           "apps"
           {
               "<appid>"   "<size>"
           }
       }
       "1"
       { ... }
   }
   ```

   Each `"path"` line holds one library folder, in quotes, with each backslash written twice (`C:\\Games`). Keys and
   values are separated by tabs. Steam's own folder is listed as library `"0"`.
3. Each library keeps installed games in `steamapps\common\<folder>`. On this machine the game's exe was
   `steamapps\common\<folder>\METAPHOR.exe`; the script doesn't assume the folder's name and looks in every folder
   there.

## Not checked

- Other Steam versions, Linux or Steam Deck layouts, and the older `LibraryFolders` format (keys `"1"`, `"2"` with the
  path as the value) are [NEEDS VERIFICATION]. The script finds nothing in such a file and asks for the path instead.
