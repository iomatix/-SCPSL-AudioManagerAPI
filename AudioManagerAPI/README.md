# SCPSL-AudioManagerAPI

[![NuGet Version](https://img.shields.io/nuget/v/SCPSL-AudioManagerAPI.svg)](https://www.nuget.org/packages/SCPSL-AudioManagerAPI/)  
A lightweight, high-performance, and robust C# library for managing audio playback in SCP: Secret Laboratory (SCP:SL) plugins using LabAPI.

Designed to integrate seamlessly with Northwood’s LabAPI ecosystem, **AudioManagerAPI** introduces a powerful **Session-Based Architecture**. It abstracts away hardware limitations, providing a reliable system for loading, caching, and playing audio with advanced control (volume, position, range, spatialization, zero-allocation player filtering, and hardware eviction recovery).

> ⚠️ **Important: V2.0.0 Breaking Changes**  
> Version 2.0.0 is a major architectural overhaul. Hardware Controller IDs (`byte`) have been completely replaced by abstract Session IDs (`int`). Direct manipulation of `ISpeaker` is no longer permitted in the public API to ensure thread safety and state persistence during hardware eviction. See the Migration Guide below.

## Powered by AudioManagerAPI

- [SCP-575 NPC](https://github.com/iomatix/-SCPSL-SCP-575-NPC)
- [Omega-Warhead](https://github.com/iomatix/-SCPSL-OmegaWarhead)
- [SCP-Immersive-Voice](https://github.com/iomatix/-SCPSL-SCP-Immersive-Voice)

---

## 🏗️ Architecture Overview (V2.1.0)

The core of the architecture relies on the separation between **Abstract Sessions** and **Physical Hardware**:

1. **Session IDs (`int`)**  
   When you play audio, you are allocated a virtual Session ID. This session holds your state (volume, position, filters, queue, PCM buffers).

2. **Hardware Eviction**  
   LabAPI is limited to 254 physical controllers. If the server runs out of physical speakers, the `ControllerIdManager` will safely *evict* lower-priority physical speakers. Your session remains alive in the background.

3. **The Router**  
   `AudioManager` acts as a router. When you send a command (e.g., `Pause(sessionId)`), the router updates the abstract state and routes the command to the physical LabAPI speaker *only if* it is currently allocated.

4. **Real‑Time Streaming Layer (NEW in 2.1.0)**  
   Sessions can now receive raw PCM buffers dynamically. The router forwards PCM to hardware when available, or queues it for later playback if the session is temporarily evicted.

---

## 🚀 Quick Start

### 1. Registering Audio

Before playing audio, register your `48kHz, Mono, Signed 16-bit PCM .wav` files. The asynchronous `AudioCache` will handle lazy-loading without blocking the server thread.
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

## 🎤 Real‑Time PCM Streaming (NEW in 2.1.0)

AudioManagerAPI 2.1.0 introduces native real‑time PCM streaming, allowing plugins to push raw audio data directly into an active session.

This feature is ideal for:

- proximity voice chat  
- live microphone input  
- AI-generated speech  
- dynamic sound effects  
- procedural audio systems  

### PCM Format Requirements

- 48 kHz  
- Mono  
- Signed 16‑bit PCM  
- Provided as a `short[]` buffer  

### Example: 🚀 Creating a Stream‑Only Audio Session

```csharp
int sessionId = DefaultAudioManager.Instance.CreateStreamSession(
    position: player.Position,
    isSpatial: true,
    minDistance: 0.05f,
    maxDistance: 20f,
    volume: 1f,
    priority: AudioPriority.High,
    validPlayersFilter: p => p.PlayerId != player.PlayerId
);
```

#### This creates a fully initialized audio session with:

- a physical speaker (if available)
- spatialization
- distance attenuation
- player filtering
- no static audio clip
- ready to receive PCM

### Example: 🔊 Streaming PCM Into an Existing Session

Whenever new PCM arrives (e.g., from an Opus decoder):

```csharp
    short[] pcmBuffer = GetDecodedPcm(); // 48kHz mono PCM
    DefaultAudioManager.Instance.AppendPcmData(sessionId, pcmBuffer);
```

#### The PCM is:

- queued in `SpeakerState.PcmQueue`
- forwarded to the physical speaker (if allocated)
- played immediately in real time

### Example: 🔁 Updating the Speaker Position

For moving audio sources (e.g., players), update the session position:
```csharp
DefaultAudioManager.Instance.SetSessionPosition(sessionId, player.Position);
```

### Example: 🧹 Cleaning Up

To stop a streaming session:
```csharp
DefaultAudioManager.Instance.DestroySession(sessionId);
```

### NOTES

- Stream‑only sessions do not require any audio key.
- `CreateStreamSession` is the recommended way to implement proximity voice or any dynamic audio pipeline.
- PCM is processed in real time and does not require pre‑loaded audio clips.
- Works seamlessly with `ISpeakerWithPlayerFilter` for per‑player visibility.

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

## 🔄 Migration Guide

### Migration from V1.9.x to V2.0.0

1. **Change ID Types**  
   Replace all `byte controllerId` variables with `int sessionId`.

2. **Update API Calls**  
   `PlayGlobalAudio` and `PlayAudio` now return `int`.

3. **Update API Calls**  
   Change `DestroySpeaker(id)` to `Stop(id)` or `DestroySession(id)`.

4. **Remove `Action<ISpeaker>` Configurations**  
   Direct access to `ISpeaker` during instantiation is removed. Instead of passing a configure delegate, use the dedicated `validPlayersFilter` parameter natively supported by the `PlayAudio` method.

5. **Use `AudioFilters` Safely**  
   Custom filters are now `Func<Player, bool>`. If you use dynamic conditions, wrap them properly to avoid pass-by-value bugs (e.g., `AudioFilters.IsConditionTrue(() => Round.IsStarted)`).

### Migration from V2.0.0 to V2.1.0

Version **2.1.0** is backwards compatible with 2.0.0 for all existing static audio use-cases.  
New functionality is opt-in and focused on real-time PCM streaming.

**New in 2.1.0:**

1. `IAudioManager.AppendPcmData(int sessionId, short[] pcm)`  
   - Allows streaming raw PCM buffers into an existing session.  
   - Intended for proximity voice, synthesized speech, and other dynamic audio sources.  
   - The caller is responsible for providing 48kHz, mono, 16-bit PCM.

2. `ISpeaker.AppendPcm(short[] pcm)`  
   - Low-level method used by the router to forward PCM directly to the hardware speaker buffer.  
   - Implemented by the default LabAPI-based speaker adapter.

3. `SpeakerState.PcmQueue`  
   - A FIFO queue of PCM buffers used to store audio when a session is temporarily evicted from hardware.  
   - Ensures that no audio is lost during hardware eviction and recovery.

**No breaking changes:**

- Existing code using `Play`, `PlayAudio`, `PlayGlobalAudio`, `Pause`, `Resume`, `FadeIn`, `FadeOut`, `Stop`, `DestroySession` continues to work without modification.  
- Real-time streaming is an additive feature.

---

## 📄 License

This project is licensed under the MIT License. See the `LICENSE` file for details.
