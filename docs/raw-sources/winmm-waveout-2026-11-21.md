# winmm waveOut: opening a stream, queuing buffers, and when a buffer may be freed

- **Source:** Microsoft Learn, Win32 API reference (`mmeapi.h`, each page updated 2024-02-22):
  [waveOutOpen](https://learn.microsoft.com/en-us/windows/win32/api/mmeapi/nf-mmeapi-waveoutopen),
  [waveOutPrepareHeader](https://learn.microsoft.com/en-us/windows/win32/api/mmeapi/nf-mmeapi-waveoutprepareheader),
  [waveOutWrite](https://learn.microsoft.com/en-us/windows/win32/api/mmeapi/nf-mmeapi-waveoutwrite),
  [waveOutUnprepareHeader](https://learn.microsoft.com/en-us/windows/win32/api/mmeapi/nf-mmeapi-waveoutunprepareheader),
  [waveOutReset](https://learn.microsoft.com/en-us/windows/win32/api/mmeapi/nf-mmeapi-waveoutreset),
  [waveOutClose](https://learn.microsoft.com/en-us/windows/win32/api/mmeapi/nf-mmeapi-waveoutclose),
  [WAVEHDR](https://learn.microsoft.com/en-us/windows/win32/api/mmeapi/ns-mmeapi-wavehdr),
  [WAVEFORMATEX](https://learn.microsoft.com/en-us/windows/win32/api/mmreg/ns-mmreg-waveformatex) (`mmreg.h`);
  [GCHandleType](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.gchandletype) (.NET,
  updated 2026-05-27); Wine's `include/mmsystem.h`,
  https://raw.githubusercontent.com/wine-mirror/wine/master/include/mmsystem.h, for the constant values, which the
  Microsoft pages don't give.
- **Saved:** 2026-11-21.
- **Game build:** none; this is the Windows API, not the game.

Every function below is in `Winmm.dll` and returns an `MMRESULT`: "Returns MMSYSERR_NOERROR if successful or an error
otherwise."

## waveOutOpen

```c
MMRESULT waveOutOpen(LPHWAVEOUT phwo, UINT uDeviceID, LPCWAVEFORMATEX pwfx, DWORD_PTR dwCallback,
                     DWORD_PTR dwInstance, DWORD fdwOpen);
```

- **WAVE_MAPPER** (for `uDeviceID`): "The function selects a waveform-audio output device capable of playing the given
  format."
- `pwfx`: "You can free this structure immediately after passing it to **waveOutOpen**."
- **CALLBACK_NULL**: "No callback mechanism. This is the default setting." Remarks: "If *fdwOpen* contains the
  **CALLBACK_NULL** flag, *dwCallback* must be **NULL**. In that case, no callback mechanism is used."

## waveOutPrepareHeader

```c
MMRESULT waveOutPrepareHeader(HWAVEOUT hwo, LPWAVEHDR pwh, UINT cbwh);
```

Remarks: "Set the **lpData**, **dwBufferLength**, and **dwFlags** members of the WAVEHDR structure before calling
this function. Set the **dwFlags** member to zero." "If the method succeeds, the **WHDR_PREPARED** flag is set in the
**dwFlags** member."

## waveOutWrite

```c
MMRESULT waveOutWrite(HWAVEOUT hwo, LPWAVEHDR pwh, UINT cbwh);
```

Remarks: "When the buffer is finished, the WHDR_DONE bit is set in the **dwFlags** member of the **WAVEHDR**
structure." "The buffer must be prepared with the **waveOutPrepareHeader** function before it is passed to
**waveOutWrite**." Error **WAVERR_UNPREPARED**: "The data block pointed to by the *pwh* parameter hasn't been
prepared."

## waveOutUnprepareHeader

```c
MMRESULT waveOutUnprepareHeader(HWAVEOUT hwo, LPWAVEHDR pwh, UINT cbwh);
```

"This function must be called after the device driver is finished with a data block. You must call this function
before freeing the buffer." Error **WAVERR_STILLPLAYING**: "The data block pointed to by the *pwh* parameter is still
in the queue." Remarks: "After passing a buffer to the device driver with the **waveOutWrite** function, you must wait
until the driver is finished with the buffer before calling **waveOutUnprepareHeader**." "Unpreparing a buffer that
has not been prepared has no effect, and the function returns zero."

## waveOutReset

```c
MMRESULT waveOutReset(HWAVEOUT hwo);
```

"The **waveOutReset** function stops playback on the given waveform-audio output device and resets the current position
to zero. All pending playback buffers are marked as done (WHDR_DONE) and returned to the application." Remarks: "After
this function returns, the application can send new playback buffers to the device by calling waveOutWrite, or close
the device by calling waveOutClose."

## waveOutClose

```c
MMRESULT waveOutClose(HWAVEOUT hwo);
```

Error **WAVERR_STILLPLAYING**: "There are still buffers in the queue." Remarks: "The close operation fails if the device
is still playing a waveform-audio buffer that was previously sent by calling **waveOutWrite**. Before calling
**waveOutClose**, the application must wait for all buffers to finish playing or call the **waveOutReset** function to
terminate playback."

## WAVEHDR and WAVEFORMATEX

```c
typedef struct wavehdr_tag {
  LPSTR              lpData;
  DWORD              dwBufferLength;
  DWORD              dwBytesRecorded;
  DWORD_PTR          dwUser;
  DWORD              dwFlags;
  DWORD              dwLoops;
  struct wavehdr_tag *lpNext;
  DWORD_PTR          reserved;
} WAVEHDR;

typedef struct tWAVEFORMATEX {
  WORD  wFormatTag;
  WORD  nChannels;
  DWORD nSamplesPerSec;
  DWORD nAvgBytesPerSec;
  WORD  nBlockAlign;
  WORD  wBitsPerSample;
  WORD  cbSize;
} WAVEFORMATEX;
```

- `nChannels`: "For monophonic audio, set this member to 1. For stereo, set this member to 2."
- `nBlockAlign`: "If **wFormatTag** = WAVE_FORMAT_PCM ... set **nBlockAlign** to `(nChannels*wBitsPerSample)/8`,
  which is the size of a single audio frame."
- `wBitsPerSample`: "If **wFormatTag** = WAVE_FORMAT_PCM, then **wBitsPerSample** should be set to either 8 or 16."
- `cbSize`: "we recommend that you initialize **cbSize** to zero for WAVE_FORMAT_PCM formats."

Wine's `mmsystem.h` wraps these definitions in `#pragma pack(push,1)`. On x64 every WAVEHDR member already falls on its
natural boundary (8 + 4 + 4 + 8 + 4 + 4 + 8 + 8 = 48 bytes), so the packing changes only WAVEFORMATEX, to 18 bytes.

## Constant values (Wine's mmsystem.h)

```c
#define MMSYSERR_NOERROR      0
#define WAVERR_BASE            32
#define WAVERR_STILLPLAYING   (WAVERR_BASE + 1)    /* still something playing */
#define WAVE_MAPPER     (-1)
#define CALLBACK_NULL           __MSABI_LONG(0x00000000)      /* no callback */
#define WAVE_FORMAT_PCM     1
#define WHDR_DONE       0x00000001
#define WHDR_PREPARED   0x00000002
```

## Pinning the buffers (.NET)

`GCHandleType.Pinned`: "This handle type is similar to Normal, but allows the address of the pinned object to be
taken. This prevents the garbage collector from moving the object and hence undermines the efficiency of the garbage
collector. Use the Free() method to free the allocated handle as soon as possible."

## How the mod uses this (ADR 0019)

The device keeps a prepared buffer until it has played, so each header and sample buffer is pinned from open to close.
A buffer is taken back by calling `waveOutUnprepareHeader`: `WAVERR_STILLPLAYING` means still queued, so the mod never
reads `dwFlags` itself. Closing resets first, unprepares every buffer, then closes; the pins are freed only after
`waveOutClose` succeeds.
