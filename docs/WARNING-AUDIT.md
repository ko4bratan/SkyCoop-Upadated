# Six compiler warnings audited

The initial warnings were not suppressed. Relevant source fixes now produce a clean Release build.

| Original site | Warning | Finding and change | Evidence / remaining limit |
|---|---|---|---|
| Supporters.cs, WebClient construction | SYSLIB0014 | Optional HTTP used deprecated WebClient, returned silently on network failure and disposed an async shared client. Replaced by shared HttpClient, 15-second timeout, observed exceptions, generation guard and main-thread application; Force now actually requests refresh. | Compiles cleanly; offline/HTTP/parse events added. Live optional-service behavior still needs runtime test. |
| ModsValidation.cs, SHA256Managed.Create | SYSLIB0021 | Algorithm must stay SHA-256 for mod checksums; only the obsolete implementation factory changed to SHA256.Create. | Actual method body tested against known SHA-256 vector, missing-file sentinel and stream disposal. |
| Shared.cs, SHA256Managed.Create | SYSLIB0021 | Same replacement, preserving UTF-8 encoding, lowercase output and null/empty sentinel. | Actual method body tested against known vector and null sentinel; wire format unchanged. |
| SteamConnect.cs, friend ID compared to null | CS8073 | CSteamID is a value type; the condition was always true and allowed invalid zero/default IDs. Use its real IsValid() check. | Compiles against Steamworks.NET20.2.0; live invite fallback still untested. |
| SteamConnect.cs, lobby server ID compared to null | CS8073 | Same always-true value-type check. Use IsValid() before attempting host connection. | Compiles; actual Steam lobby/native ABI test pending. |
| Patches.cs, active Scene compared to null | CS8073 | Scene is a value type. Use the game's Scene.IsValid() API before accessing its name in the fallback path. | Compiles against pinned 2.55 interop; actual scene-travel behavior pending. |

No compatibility claim is inferred from warning-free compilation. The optional supporters endpoint is unchanged; its data is not a runtime dependency for multiplayer.
