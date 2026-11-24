# Core Audio: a process's own session volume (ISimpleAudioVolume)

- **Source:** Microsoft Learn, Win32 API reference (each page updated 2024-02-22 unless noted):
  [IAudioSessionManager::GetSimpleAudioVolume](https://learn.microsoft.com/en-us/windows/win32/api/audiopolicy/nf-audiopolicy-iaudiosessionmanager-getsimpleaudiovolume),
  [ISimpleAudioVolume](https://learn.microsoft.com/en-us/windows/win32/api/audioclient/nn-audioclient-isimpleaudiovolume),
  [ISimpleAudioVolume::SetMasterVolume](https://learn.microsoft.com/en-us/windows/win32/api/audioclient/nf-audioclient-isimpleaudiovolume-setmastervolume),
  [IMMDeviceEnumerator::GetDefaultAudioEndpoint](https://learn.microsoft.com/en-us/windows/win32/api/mmdeviceapi/nf-mmdeviceapi-immdeviceenumerator-getdefaultaudioendpoint),
  [IMMDevice::Activate](https://learn.microsoft.com/en-us/windows/win32/api/mmdeviceapi/nf-mmdeviceapi-immdevice-activate)
  (updated 2025-03-28). Wine's IDL for the GUIDs and the vtable order, which the Learn pages list alphabetically:
  https://raw.githubusercontent.com/wine-mirror/wine/master/include/mmdeviceapi.idl,
  https://raw.githubusercontent.com/wine-mirror/wine/master/include/audiopolicy.idl,
  https://raw.githubusercontent.com/wine-mirror/wine/master/include/audioclient.idl.
- **Saved:** 2026-11-24.
- **Game build:** none; this is the Windows API, not the game.

## GUIDs (Wine IDL)

- coclass `MMDeviceEnumerator`: `bcde0395-e52f-467c-8e3d-c4579291692e`
- `IMMDeviceEnumerator`: `a95664d2-9614-4f35-a746-de8db63617e6`
- `IMMDevice`: `d666063f-1587-4e43-81f1-b948e807363f`
- `IAudioSessionManager`: `bfa971f1-4d5e-40bb-935e-967039bfbee4`
- `ISimpleAudioVolume`: `87ce5498-68d6-44e5-9215-6da47ef883d8`

## Methods in declaration (vtable) order, after IUnknown's three

```idl
// IMMDeviceEnumerator
HRESULT EnumAudioEndpoints([in] EDataFlow dataFlow, [in] DWORD dwStateMask, [out] IMMDeviceCollection **ppDevices);
HRESULT GetDefaultAudioEndpoint([in] EDataFlow dataFlow, [in] ERole role, [out] IMMDevice **ppEndpoint);
HRESULT GetDevice([in] LPCWSTR pwstrId, [out] IMMDevice **ppDevice);
HRESULT RegisterEndpointNotificationCallback([in] IMMNotificationClient *pClient);
HRESULT UnregisterEndpointNotificationCallback([in] IMMNotificationClient *pClient);

// IMMDevice
HRESULT Activate([in] REFIID iid, [in] DWORD dwClsCtx, [in,unique] PROPVARIANT *pActivationParams, [out,iid_is(iid)] void **ppv);
HRESULT OpenPropertyStore([in] DWORD stgmAccess, [out] IPropertyStore **ppProperties);
HRESULT GetId([out] LPWSTR *ppstrId);
HRESULT GetState([out] DWORD *pdwState);

// IAudioSessionManager
HRESULT GetAudioSessionControl([in] LPCGUID AudioSessionGuid, [in] DWORD StreamFlags, [out] IAudioSessionControl **SessionControl);
HRESULT GetSimpleAudioVolume([in] LPCGUID AudioSessionGuid, [in] DWORD StreamFlags, [out] ISimpleAudioVolume **AudioVolume);

// ISimpleAudioVolume
HRESULT SetMasterVolume([in] float fLevel, [unique,in] LPCGUID EventContext);
HRESULT GetMasterVolume([out] float *pfLevel);
HRESULT SetMute([in] const BOOL bMute, [unique,in] LPCGUID EventContext);
HRESULT GetMute([out] BOOL *pbMute);
```

Enums (Wine IDL, no explicit values, so numbered from 0): `typedef enum _EDataFlow { eRender, eCapture, eAll,
EDataFlow_enum_count } EDataFlow;` and `typedef enum _ERole { eConsole, eMultimedia, eCommunications,
ERole_enum_count } ERole;`.

## Quotes

- GetSimpleAudioVolume, AudioSessionGuid: "If this parameter is NULL or points to the value GUID_NULL, the method
  assigns the stream to the default session." StreamFlags: "Set to TRUE if the session is cross-process. Set to FALSE
  if the session is not cross-process."
- IMMDevice::Activate, pActivationParams: "Set to NULL to activate an ... IAudioSessionManager ... interface on an
  audio endpoint device." dwClsCtx: "Alternatively, the client can avoid imposing any context restrictions by
  specifying CLSCTX_ALL."
- SetMasterVolume, fLevel: "The new master volume level. Valid volume levels are in the range 0.0 to 1.0."
  EventContext: "If the caller supplies a NULL pointer for this parameter, the client's notification method receives
  a NULL context pointer."
- ISimpleAudioVolume: "By default, the session manager sets the master volume level to 1.0 at the initial activation
  of a session. Subsequent volume changes by Sndvol or other clients are persistent across computer restarts."
