# Steamworks `ESteamInputType` values

- **Source:** Steamworks documentation, `ISteamInput` page, `ESteamInputType` section:
  <https://partner.steamgames.com/doc/api/ISteamInput#ESteamInputType>, read with WebFetch.
- **Saved:** 2026-11-22.
- **Game build:** none (Steam's API, not the game).

`GetInputTypeForHandle` returns the device model of a controller handle as an `ESteamInputType`.
`GetControllerForGamepadIndex` returns the handle tied to an emulated gamepad slot, or 0.

| Name | Value |
|---|---|
| `k_ESteamInputType_Unknown` | 0 |
| `k_ESteamInputType_SteamController` | 1 |
| `k_ESteamInputType_XBox360Controller` | 2 |
| `k_ESteamInputType_XBoxOneController` | 3 |
| `k_ESteamInputType_GenericGamepad` | 4 |
| `k_ESteamInputType_PS4Controller` | 5 |
| `k_ESteamInputType_AppleMFiController` | 6 |
| `k_ESteamInputType_AndroidController` | 7 |
| `k_ESteamInputType_SwitchJoyConPair` | 8 |
| `k_ESteamInputType_SwitchJoyConSingle` | 9 |
| `k_ESteamInputType_SwitchProController` | 10 |
| `k_ESteamInputType_MobileTouch` | 11 |
| `k_ESteamInputType_PS3Controller` | 12 |
| `k_ESteamInputType_PS5Controller` | 13 |
| `k_ESteamInputType_SteamDeckController` | 14 |
| `k_ESteamInputType_SteamOSHandheld` | 15 |
| `k_ESteamInputType_Switch2ProController` | 16 |
| `k_ESteamInputType_SteamController2026` | 17 |
| `k_ESteamInputType_SteamFrameControllerPair` | 18 |
| `k_ESteamInputType_Count` | 19 |
| `k_ESteamInputType_MaximumPossibleValue` | 255 |
