# winmm PlaySound, and how .NET passes a byte array to it

- **Sources:** Microsoft Learn, "PlaySound function", https://learn.microsoft.com/en-us/previous-versions/dd743680(v=vs.85)
  (archived page, updated 2021-12-02); Wine's `include/mmsystem.h`,
  https://raw.githubusercontent.com/wine-mirror/wine/master/include/mmsystem.h, for the flag values, which the
  Microsoft page doesn't list; Microsoft Learn, "Copying and Pinning",
  https://learn.microsoft.com/en-us/dotnet/standard/native-interop/copying-and-pinning (updated 2026-07-12).
- **Saved:** 2026-11-21.
- **Game build:** none; this is the Windows API, not the game.

## PlaySound

Signature: `BOOL PlaySound(LPCTSTR pszSound, HMODULE hmod, DWORD fdwSound);`. DLL `Winmm.dll`, header `Mmsystem.h`
(include `Windows.h`); Unicode name `PlaySoundW`. Returns TRUE on success, FALSE otherwise.

Flags quoted from the Microsoft page:

- **SND_MEMORY**: "The *pszSound* parameter points to a sound loaded in memory."
- **SND_SYNC**: "The sound is played synchronously, and **PlaySound** returns after the sound event completes. This is
  the default behavior."
- **SND_ASYNC**: "The sound is played asynchronously and **PlaySound** returns immediately after beginning the sound."
  Remarks: "If you combine the **SND_MEMORY** and **SND_ASYNC** flags, the memory buffer that contains the sound must
  remain valid until the sound has completed playing."
- **SND_NODEFAULT**: "No default sound event is used. If the sound cannot be found, **PlaySound** returns silently
  without playing the default sound."
- **SND_NOSTOP**: "If this flag is not specified, **PlaySound** attempts to stop any sound that is currently playing in
  the same process. Sounds played in other processes are not affected."
- **SND_SYSTEM**: "If this flag is not set, the sound is assigned to the default audio session for the application's
  process."

Values from Wine's `mmsystem.h`:

```c
#define SND_SYNC            	0x0000  /* play synchronously (default) */
#define SND_ASYNC           	0x0001  /* play asynchronously */
#define SND_NODEFAULT       	0x0002  /* don't use default sound */
#define SND_MEMORY          	0x0004  /* lpszSoundName points to a memory file */
```

## A byte array passed by value

From "Copying and Pinning": "Reference types can be passed by value or by reference. When they are passed by value, a
pointer to the type is passed on the stack." The marshaller either pins the data ("Pinning temporarily locks the data
in its current memory location, thus keeping it from being relocated by the common language runtime's garbage
collector") or passes a copy; either way the pointer is valid for the call, so a synchronous call can't outlive it.
