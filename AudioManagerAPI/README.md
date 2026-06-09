# SCPSL-AudioManagerAPI

[![NuGet Version](https://img.shields.io/nuget/v/SCPSL-AudioManagerAPI.svg)](https://www.nuget.org/packages/SCPSL-AudioManagerAPI/)  
A lightweight, high-performance, and robust C# library for managing audio playback in SCP: Secret Laboratory (SCP:SL) plugins using LabAPI.

Designed to integrate seamlessly with Northwood’s LabAPI ecosystem, **AudioManagerAPI** introduces a powerful **Session-Based Architecture**. It abstracts away hardware limitations, providing a reliable system for loading, caching, and playing audio with advanced control (volume, position, range, spatialization, zero-allocation player filtering, and hardware eviction recovery).

> ⚠️ **Important: V2.0.0 Breaking Changes** > Version 2.0.0 is a major architectural overhaul. Hardware Controller IDs (`byte`) have been completely replaced by abstract Session IDs (`int`). Direct manipulation of `ISpeaker` is no longer permitted in the public API to ensure thread safety and state persistence during hardware eviction. See the Migration Guide below.

## Powered by AudioManagerAPI

* [SCP-575 NPC](https://github.com/iomatix/-SCPSL-SCP-575-NPC)
* [Omega-Warhead](https://github.com/iomatix/-SCPSL-OmegaWarhead)
* [SCP-Immersive-Voice](https://github.com/iomatix/-SCPSL-SCP-Immersive-Voice)

---

## 🏗️ Architecture Overview (V2.3.0)

The core of the architecture relies on the separation between **Abstract Sessions** and **Physical Hardware**:

1. **Session IDs (`int`)** – When you play audio, you are allocated a virtual Session ID. This session holds your state (volume, position, filters, queue, PCM buffers).
2. **Hardware Eviction** – LabAPI is limited to 254 physical controllers. If the server runs out of physical speakers, the `ControllerIdManager` will safely *evict* lower-priority physical speakers. Your session remains alive in the background.
3. **The Router** – `AudioManager` acts as a router. When you send a command (e.g., `Pause(sessionId)`), the router updates the abstract state and routes the command to the physical LabAPI speaker *only if* it is currently allocated.
4. **Real‑Time Streaming Layer** – Sessions can receive raw PCM buffers dynamically. The router forwards PCM to hardware when available, or queues it for later playback if the session is temporarily evicted.
5. **Main-Thread Marshalling & Deferrals (NEW in 2.3.0)** – To prevent asynchronous thread-affinity deadlocks and native Unity exceptions, all structural network speaker decompositions (`Object.Destroy`) are marshalled back to Unity's main thread loop via deferred execution gates (`MEC.Segment.Update`).
6. **Predictive Background Pre-Decoding (NEW in 2.3.0)** – The CPU overhead of disk file I/O operations and multi-format binary parsing is entirely offloaded to background worker threads immediately upon registration, converting execution runtime queries into deterministic $O(1)$ pointer swaps.

---

## 🚀 Quick Start

### 1. Registering Audio

Before playing audio, register your resource. The asynchronous predictive `AudioCache` will handle background pre-decoding immediately upon registration using the global thread pool, eliminating first-play disk I/O server stutter.

```csharp
using AudioManagerAPI.Defaults;
using System.Reflection;

public void OnPluginStart()
{
    // Allocation occurs instantly in the background without blocking the bootstrap thread
    DefaultAudioManager.RegisterAudio("my_custom_sound", () => 
        Assembly.GetExecutingAssembly().GetManifestResourceStream("MyPlugin.Audio.sound.wav"));
}
```

AudioCache automatically handles:
* WAV PCM 16/24/32‑bit / WAV IEEE float
* MPEG Layer-3 (`.mp3`) automatic stream decoding
* Automatic downmixing to mono channels
* Automatic linear resampling to 48 kHz
* Pure lock-free $O(1)$ runtime buffer retrieval

### 2. Playing Audio (2D Environmental)

Use the `DefaultAudioManager` facade to trigger flat non-spatialized sounds globally.

```csharp
// Play audio globally for all players (returns an abstract int handle)
int sessionId = DefaultAudioManager.PlayGlobal("my_custom_sound", queue: false, fadeInDuration: 1.5f);

// Pause and Resume thread-safely
DefaultAudioManager.Pause(sessionId);
DefaultAudioManager.Resume(sessionId);

// Stop and destroy session lifecycle tracking
DefaultAudioManager.Stop(sessionId);
```

### 3. Playing Audio (3D Positional Cues)

For 3D positional audio effects triggered at fixed spatial coordinates, use the high-level position facade shortcut:

```csharp
using AudioManagerAPI.Defaults;
using UnityEngine;

// Spawns a 3D spatialized prop audio effect with automated lifecycle cleanup
int structuralSessionId = DefaultAudioManager.PlayAtPosition("explosion_sound", new Vector3(10f, 4f, -15f));
```

### 4. Local-Space Transform Tracking (NEW in 2.3.0)

To play 3D audio that attaches to and dynamically tracks a moving target (e.g., weapon attachments, moving NPCs, or client hallucinations) relative to their sight and orientation vectors:

```csharp
using AudioManagerAPI.Defaults;
using AudioManagerAPI.Shared.Audio.Enums;

// Latches a spatialized audio cue to follow a player's neck level (1.65m) 
// pushed exactly 1mm forward along their look vector to lock HRTF directionality.
DefaultAudioManager.Instance.PlayTrackingAudio(
    player: targetPlayer,
    audioKey: AudioKey.LightShortCircuit,
    lifespan: 0.115f,
    hearableForAllPlayers: true
);
```

---

## 🎤 Real‑Time PCM Streaming

AudioManagerAPI introduces native real‑time PCM streaming, allowing plugins to push raw audio data directly into an active session.

### PCM Format Requirements (Float‑Native)
* 48 kHz / Mono
* Normalized float PCM (-1.0 to 1.0) provided as a `float[]` buffer
* *Note: Micro-transients (<0.5s lifespan) invoke an adaptive timing gate that enforces a soft-stop mute pass followed by a 250ms network flush to guarantee full UDP packet delivery.*

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
* a physical speaker (if available)
* spatialization
* distance attenuation
* player filtering
* no static audio clip
* ready to receive PCM

### Example: 🔊 Streaming PCM Into an Existing Session

Whenever new PCM arrives (e.g., from an Opus decoder):

```csharp
float[] pcmBuffer = GetDecodedFloatPcm(); // Normalized float PCM (-1..1)
DefaultAudioManager.Instance.AppendPcmData(sessionId, pcmBuffer);
```

Legacy short[] example (deprecated):
```csharp
short[] legacy = GetDecodedShortPcm();
DefaultAudioManager.Instance.AppendPcmData(sessionId, legacy);
```

#### The PCM is:
* queued in `SpeakerState.PcmQueue`
* forwarded to the physical speaker (if allocated)
* played immediately in real time

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
* Stream‑only sessions do not require any audio key.
* `CreateStreamSession` is the recommended way to implement proximity voice or any dynamic audio pipeline.
* Float PCM (-1..1) is processed in real time and does not require pre‑loaded audio clips.
* Works seamlessly with `ISpeakerWithPlayerFilter` for per‑player visibility.

---

## 🎧 Float‑Native Audio Pipeline

AudioManagerAPI uses a fully float‑native audio pipeline:
* AudioCache decodes all audio into float[]
* Real‑time streaming uses float[] buffers
* `ISpeaker.AppendPcm(float[])` is the primary low‑level API
* short[] overloads remain only for backward compatibility
* MP3 files are automatically decoded and converted to float[]

This ensures maximum audio quality with no quantization, clipping, or integer PCM artifacts.

---

## ⚙️ Configuration

On the first launch, the API generates `Configs/AudioConfig.json` in the server root. This file features built-in self-sanitation (`Validate()`) and is completely fault-tolerant against destructive administrative inputs.

```json
{
  "CacheSize": 50,
  "UseDefaultSpeakerFactory": true,
  "DefaultFadeInDuration": 1.0,
  "DefaultFadeOutDuration": 1.0
}
```

*Note: `CacheSize` determines how many uncompressed float tracks are kept in the Least-Recently-Used (LRU) RAM cache before eviction loops trigger.*

---

## 🔄 Migration Guide

### Migration from V1.9.x to V2.0.0

1. **Change ID Types** – Replace all `byte controllerId` variables with `int sessionId`.
2. **Update API Calls** – `PlayGlobalAudio` and `PlayAudio` now return `int`.
3. **Update API Calls** – Change `DestroySpeaker(id)` to `Stop(id)` or `DestroySession(id)`.
4. **Remove `Action<ISpeaker>` Configurations** – Direct access to `ISpeaker` during instantiation is removed. Instead of passing a configure delegate, use the dedicated `validPlayersFilter` parameter natively supported by the `PlayAudio` method.
5. **Use `AudioFilters` Safely** – Custom filters are now `Func<Player, bool>`. If you use dynamic conditions, wrap them properly to avoid pass-by-value bugs (e.g., `AudioFilters.IsConditionTrue(() => Round.IsStarted)`).

### Migration from V2.2.0 to V2.3.0

Version **2.3.0** introduces design optimizations to enforce the **Dependency Inversion Principle (SOLID)** and eliminate redundant I/O cycles. 

**Breaking Changes & Modifications:**

1. **Facade Method Refactoring** – The ambiguous method `DefaultAudioManager.Play(...)` has been removed. Replace its usage with explicit intention wrappers:
   * Use `DefaultAudioManager.PlayGlobal(...)` for 2D broadcasts.
   * Use `DefaultAudioManager.PlayAtPosition(...)` for 3D positional triggers.

2. **Inversion of Control (IoC) Constructor Change** – The `AudioManager` constructor no longer reads configuration blocks from disk internally to avoid double-read I/O contention locks. It now strictly demands a pre-validated config instance:
   ```csharp
   // Old syntax:
   public AudioManager(ISpeakerFactory factory)
   // New syntax:
   public AudioManager(ISpeakerFactory factory, AudioConfig config)
   ```

3. **Interface Promotion** – The `AudioOptions` configuration footprint has been promoted to be a core contract requirement of the `IAudioManager` interface. Ensure any custom mock/testing implementations map this property:
   ```csharp
   AudioOptions Options { get; }
   ```

---

## 📄 License

This project is licensed under the MIT License. See the `LICENSE` file for details.
```