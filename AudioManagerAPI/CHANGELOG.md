# SCPSL-AudioManagerAPI — Changelog

## 🆕 Changelog — Version 2.3.1 — API Native Paranoia & Spatial Tracking

### Core API Extensions
- **Smart Dual-Channel Injection**: Added `PlaySpatialSmart` to `IAudioManager`. This API automatically handles the "Owner vs World" split by creating a filtered 3D session for nearby entities and a private 2D/3D isolated session for the owner, eliminating audio phasing (phasing/comb filtering) for the trigger source.
- **Universal Orbit Engine**: Integrated `PlayOrbitingAudio` into the core API. The trigonometric vector update loop is now a first-class citizen of the engine, supporting arbitrary position providers, custom radii, and angular velocity scalars without manual plugin-side coroutine management.
- **Predictive Trajectory Sync**: Added `PlayTrackingAudio` for frame-perfect anatomical tracking. Specifically optimized for head-level projection (1.65m offset) to lock auditory sources to player silhouettes during high-speed character movement.

### Performance & Safety
- **Lifespan Cleanup Optimization**: Enhanced `LifespanCleanupCoroutine` with an adaptive 100ms hardware warm-up buffer, ensuring 3D speaker components initialize their internal buffers before the truncation timer starts counting.
- **Transient Debounce Gate**: Implemented a global-level transient debouncer (`isTransient`) within `PlayAudioAutoManaged`. Prevents command queue starvation and hardware controller exhaustion caused by high-frequency input spam (e.g., rapid flashlight toggling).
- **Network Stability**: Added `ExecuteTransientNetworkFlush` mechanism—a soft-stop trigger that allows network buffers to finish outputting short audio tails (transients) before physically destroying the session, eliminating the "click/pop" cutoff sound at the end of audio samples.

### API Maintenance
- **IoC Constructor Alignment**: Updated `AudioManager` to require `AudioConfig` on instantiation. This removes the need for duplicate disk I/O reads during the bootstrap phase.
- **Interface Evolution**: Added `SetSessionPlayerFilter` and expanded contract definitions to support dynamic runtime filter updates for active streams.

## 🆕 Changelog — Version 2.3.0 — Enterprise Stability & Synchronized Core

### Main-Thread Marshalling (Thread-Safety)
- Introduced `DestroySessionDeferred` bound to `MEC.Segment.Update` to safely marshal session teardowns and Unity network object destruction back to the Main Thread.
- Prevents structural thread affinity deadlocks and native Unity memory crashes caused by background audio worker threads executing cleanup callbacks.
- Implemented a single-frame deferral engine in `FadeOutAudio` to prevent eviction race conditions on early execution frames.

### Micro-Transient Netcode (Soft-Stop & Network Flush)
- Overrode hard session truncation for short audio lifetimes (< 0.5s) to eliminate the "disappearing click" network bug.
- Implemented an adaptive timing gate: the pipeline now instantly mutes the audio playback data layer (`FadeOutAudio(id, 0f)`) but delays physical session destruction by `250ms` (`DelayedSessionDestroy`).
- Guarantees that raw UDP packet buffers successfully flush out of the network card queue to remote clients before the session ID is recycled.

### Non-Blocking Cache & Predictive Warmup
- Optimized `PlayAudio` by extracting `audioCache.Get(key)` completely outside of the global management `lockObject` sector.
- Prevents full-engine lock contentions and server-wide audio freezing during fallback disk I/O read operations.
- Upgraded `AudioCache` to proactively pre-decode registered audio assets asynchronously using `ThreadPool.QueueUserWorkItem` upon registration.
- Eliminates first-play disk I/O stuttering, converting lazy runtime lookups into deterministic, zero-allocation $O(1)$ operations during dynamic gameplay passes.

### Rotation-Aware Local Space Tracking
- Introduced `PlayTrackingAudio` API driven by a zero-allocation reactive frame-by-frame transformation loop.
- Sound anchors now dynamically follow a player's model relative to their local orientation matrix (anchored at neck-level `1.65f` elevation, with a `1mm` look-vector thrust to maximize directional HRTF immersive accuracy).
- Integrated a `100ms` physical speaker engine warm-up buffer to compensate for asynchronous Unity scene component instantiation delays.

### Bootstrapper IoC Alignment
- Refactored `AudioManager` constructor and `DefaultAudioManager` facade to share a single, pre-validated `AudioConfig` instance using Inversion of Control (IoC).
- Eliminates redundant duplicate disk serialization passes on plugin startup, cutting config I/O cycles in half.
- Embedded runtime input sanitization (`Validate`) directly into the model to block corrupt JSON parameters from crashing the engine.

## 🆕 Changelog — Version 2.2.0

### Float‑Native Audio Pipeline
- All real‑time streaming APIs now use `float[]` PCM (-1..1)
- `ISpeaker.AppendPcm(float[])` is now the primary low‑level method
- `short[]` overloads remain for backward compatibility only
- Eliminates quantization, clipping, and integer PCM artifacts

### AudioCache Improvements
- Full adaptive WAV loader (16/24/32‑bit PCM, IEEE float)
- Automatic mono downmix
- Automatic resampling to 48 kHz
- All decoded audio is stored as `float[]`

### MP3 Support (NEW)
- AudioCache can now decode `.mp3` files
- MP3 is automatically converted to float PCM
- No changes required in plugin code

### API Cleanup
- `AppendPcmData(int, float[])` added as the primary streaming method
- `AppendPcmData(int, short[])` marked as `[Obsolete]`
- `ISpeaker.AppendPcm(short[])` marked as `[Obsolete]`
- `SpeakerState.PcmQueue` now stores `float[]`

### Backwards Compatibility
- All existing static audio playback continues to work
- Legacy short[] APIs still function but are deprecated

## 2.1.1 - 2.1.2 — Stream‑Only Sessions (No Audio Key Required)

### Added

- `IAudioManager.CreateStreamSession(...)`
	- Creates a spatial audio session without requiring any audio key or cached clip.
	- Designed for real‑time PCM pipelines such as proximity voice chat.

- Stream‑only mode in `SpeakerState`
	- `IsStreamOnly = true`
	- skips static audio playback
	- initializes a physical speaker ready for PCM input only

### Changed

- `InitializePhysicalSpeaker(...)`
	- safely handles `initialSamples == null`
	- configures spatialization, volume, distance, and filters
	- does not attempt to play or queue static audio

### Fixed

- Resolved issues with audio-streaming interruptions.
- Removed warnings such as: `[AudioManagerAPI] Audio with key X not found in cache.` Stream‑only sessions no longer require dummy audio keys.
- Ensured `AppendPcmData` works even when no static audio was ever loaded.

## 2.1.0 — Real-Time PCM Streaming

**Release focus:** dynamic audio, proximity voice, and live PCM pipelines.

### Added

- `IAudioManager.AppendPcmData(int sessionId, short[] pcm)`  
  - Appends raw 48kHz, mono, 16-bit PCM buffers to an existing session.  
  - Enables real-time streaming for proximity voice chat, synthesized speech, and procedural audio.

- `ISpeaker.AppendPcm(short[] pcm)`  
  - Low-level method for appending PCM directly to the hardware speaker buffer.  
  - Implemented by the default LabAPI `SpeakerToy` adapter.

- `SpeakerState.PcmQueue`  
  - FIFO queue of PCM buffers stored per session.  
  - Used when a session is evicted from hardware and later recovered.

### Behavior

- If a session has an active physical speaker, PCM is forwarded immediately.  
- If a session is evicted, PCM is queued in `SpeakerState.PcmQueue`.  
- When the session is recovered, queued PCM is played automatically.

### Compatibility

- Fully backwards compatible with 2.0.0 for static audio playback.  
- No changes required for existing plugins that do not use real-time streaming.

### Use-cases

- SCP proximity voice chat  
- Live microphone input  
- AI-generated speech  
- Dynamic and procedural sound effects  

---

## 2.0.0 — Session-Based Architecture Overhaul

- Replaced hardware Controller IDs (`byte`) with abstract Session IDs (`int`).  
- Introduced `ControllerIdManager` for priority-based hardware allocation and eviction.  
- Added `AudioCache` for lazy-loading and caching decoded audio.  
- Removed direct public access to `ISpeaker` to ensure thread safety and state persistence.  
- Added support for:
  - spatial audio  
  - per-session volume, distance, and filters  
  - hardware eviction and recovery  
  - auto-cleanup and lifespan-based sessions  

**Breaking changes (from 1.9.x):**

- All APIs now use `int sessionId` instead of `byte controllerId`.  
- `DestroySpeaker(id)` replaced by `Stop(id)` or `DestroySession(id)`.  
- `Action<ISpeaker>` configuration delegates removed in favor of `validPlayersFilter`.

---

## 1.x — Initial Releases

- Basic audio playback via LabAPI `SpeakerToy`.  
- Global and spatial playback.  
- Simple caching and basic control (play, stop, loop).  

*(Older versions summarized; see git history for full details.)*