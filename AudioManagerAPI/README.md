# SCPSL-AudioManagerAPI

[![NuGet Version](https://img.shields.io/nuget/v/SCPSL-AudioManagerAPI.svg)](https://www.nuget.org/packages/SCPSL-AudioManagerAPI/)  
A lightweight, high-performance, and robust C# library for managing audio playback in SCP: Secret Laboratory (SCP:SL) plugins using LabAPI. 

Designed to integrate seamlessly with Northwood’s LabAPI ecosystem, **AudioManagerAPI V2.0.0** introduces a powerful **Session-Based Architecture**. It abstracts away hardware limitations, providing a reliable system for loading, caching, and playing audio with advanced control (volume, position, range, spatialization, zero-allocation player filtering, and hardware eviction recovery).

> ⚠️ **Important: V2.0.0 Breaking Changes** > Version 2.0.0 is a major architectural overhaul. Hardware Controller IDs (`byte`) have been completely replaced by abstract Session IDs (`int`). Direct manipulation of `ISpeaker` is no longer permitted in the public API to ensure thread safety and state persistence during hardware eviction. See the Migration Guide below.

## Powered by AudioManagerAPI
- [SCP-575 NPC](https://github.com/iomatix/-SCPSL-SCP-575-NPC)
- [Omega-Warhead](https://github.com/iomatix/-SCPSL-OmegaWarhead)

---

## 🏗️ Architecture Overview (V2.0.0)

The core of V2.0.0 relies on the separation between **Abstract Sessions** and **Physical Hardware**:
1. **Session IDs (`int`)**: When you play audio, you are allocated a virtual Session ID. This session holds your state (volume, position, filters, queue).
2. **Hardware Eviction**: LabAPI is limited to 254 physical controllers. If the server runs out of physical speakers, the `ControllerIdManager` will safely *evict* lower-priority physical speakers. Your session remains alive in the background.
3. **The Router**: `AudioManager` acts as a router. When you send a command (e.g., `Pause(sessionId)`), the router updates the abstract state and routes the command to the physical LabAPI speaker *only if* it is currently allocated.

---

## 🚀 Quick Start

### 1. Registering Audio
Before playing audio, register your `48kHz, Mono, Signed 16-bit PCM .wav` files. The new asynchronous `AudioCache` will handle lazy-loading without blocking the server thread.

```csharp
using AudioManagerAPI.Defaults;
using System.Reflection;

public void OnPluginStart()
{
    DefaultAudioManager.RegisterAudio("my_custom_sound", () => 
        Assembly.GetExecutingAssembly().GetManifestResourceStream("MyPlugin.Audio.sound.wav"));
}
```

### 2. Playing Audio
Use the `DefaultAudioManager` facade to play audio. It returns an `int` Session ID.

```csharp
// Play audio globally for all players
int sessionId = DefaultAudioManager.Play("my_custom_sound", queue: false, fadeInDuration: 1.5f);

// Pause and Resume
DefaultAudioManager.Pause(sessionId);
DefaultAudioManager.Resume(sessionId);

// Stop and Destroy Session
DefaultAudioManager.Stop(sessionId);
```

### 3. Advanced Playback (Spatial & Filtered)
For 3D audio or targeting specific players, use the `Instance` directly.

```csharp
using AudioManagerAPI.Defaults;
using AudioManagerAPI.Features.Enums;
using AudioManagerAPI.Features.Filters;

int spatialSessionId = DefaultAudioManager.Instance.PlayAudio(
    key: "my_custom_sound",
    position: new Vector3(10f, 0f, 10f),
    loop: false,
    volume: 0.8f,
    minDistance: 5f,
    maxDistance: 25f,
    isSpatial: true,
    priority: AudioPriority.High,
    validPlayersFilter: AudioFilters.ByRole(RoleTypeId.ClassD) // Zero-allocation filter
);
```

---

## ⚙️ Configuration
On the first launch, the API generates `Configs/AudioConfig.json` in the server root.
This file is fault-tolerant and will safely fall back to defaults if misconfigured.

```json
{
  "CacheSize": 50,
  "UseDefaultSpeakerFactory": true,
  "DefaultFadeInDuration": 1.0,
  "DefaultFadeOutDuration": 1.0
}
```
*Note: `CacheSize` determines how many decoded WAV files are kept in the LRU RAM cache.*

---

## 🔄 Migration Guide (V1.9.x to V2.0.0)
To upgrade your plugins to V2.0.0, apply the following changes:

1. Change ID Types: Replace all `byte controllerId` variables with `int sessionId`.
2. Update API Calls: `PlayGlobalAudio` and `PlayAudio` now return `int`.
3. Update API Calls: Change `DestroySpeaker(id)` to `Stop(id)` or `DestroySession(id)`.
4. Remove `Action<ISpeaker>` Configurations: Direct access to `ISpeaker` during instantiation is removed. Instead of passing a configure delegate, use the dedicated `validPlayersFilter` parameter natively supported by the `PlayAudio` method.
5. Use `AudioFilters` Safely: Custom filters are now `Func<Player, bool>`. If you use dynamic conditions, wrap them properly to avoid pass-by-value bugs (e.g., `AudioFilters.IsConditionTrue(() => Round.IsStarted)`).

---

## 📄 License
This project is licensed under the MIT License. See the `LICENSE` file for details.

