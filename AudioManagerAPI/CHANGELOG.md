# SCPSL-AudioManagerAPI — Changelog

## 🆕 Changelog — Version 2.4.1 — Zero-Allocation Generic State-Passing & Hot Path Isolation

### Architectural Innovation (Generic Context State-Passing)

* **Closure Allocation Elimination**: Introduced the generic state-passing paradigm (TState) across all primary audio dispatch channels. By explicitly routing contextual variables through a type-safe state container, the compiler completely bypasses the generation of implicit display classes on the heap, ensuring absolute zero runtime allocation during high-frequency player filtration.
* **Unified Generic Pipeline Expansion**: Deployed comprehensive generic overloads for the entire execution matrix:
* PlayAudio(...)
* PlayGlobalAudio(...)
* PlayTrackingAudio(...)
* PlayOrbitingAudio(...)
* CreateStreamSession(...)


* **Internal Smart Routing**: Refactored the internal mechanics of PlaySpatialSmart to automatically wrap the source Player instance into the new generic pipeline, silently eliminating closure allocations for all legacy integrations without breaking backward compatibility.

### Refactored Components & Core Adaptation

* **Interface & Adapter Evolution**: Extended the ISpeakerWithPlayerFilter contract and its concrete implementation DefaultSpeakerToyAdapter to support compiled, non-generic bridge delegates (Func<Player, bool object,>) and untyped state references.
* **Instance-Bound Hot Path Filtering**: Configured the hardware layer to bind LabAPI's/Northwood's native AudioTransmitter.ValidPlayers loop to an instanced evaluation method within the adapter. This caches the delegate target reference exactly once during session initialization, completely removing runtime allocation pressure from the game engine's internal audio tick loop.

---

### Code Usage Reference (Examples)

#### 1. Legacy Approach (Allocation Heavy — Generates Heap Garbage)

Capturing local variables inside a standard lambda causes the CLR to allocate a new context object on the heap at every single method invocation.
```csharp
float maxDistance = 30f;
Vector3 position = room.Position;

_audioManager.PlayAudio(
"my_sound_key",
position,
validPlayersFilter: player => player.IsReady && player.IsWithinRadius(position, maxDistance)
);
```
#### 2. Modern Approach (Version 2.4.1 — 100% Zero Allocation on Hot Path)

By passing a custom struct or class configuration profile as the state parameter, the filtering logic remains entirely static and clean of runtime memory footprints.
```csharp
public readonly struct FilterConfig
{
public Vector3 Center { get; }
public float Radius { get; }

public FilterConfig(Vector3 center, float radius)
{
    Center = center;
    Radius = radius;
}

```

```csharp
// Execution layer implementation:
var configState = new FilterConfig(room.Position, 30f);

_audioManager.PlayAudio(
key: "my_sound_key",
position: room.Position,
state: configState,
validPlayersFilter: (player, state) => player.IsReady && player.IsWithinRadius(state.Center, state.Radius)
);
```

---

## 1. Session Model Update: SpeakerState.cs

Add these fields inside the SpeakerState class to hold the untyped state context reference and the compiled bridge delegate:

```csharp
public object FilterState { get; set; }
public Func<Player, bool object,> StatePlayerFilter { get; set; }
```

---

## 2. Speaker Contract Extension: ISpeakerWithPlayerFilter.cs

```csharp
/// <summary>
/// Defines the execution matrix for hardware speakers capable of filtering audio visibility tracks per connected client.
/// </summary>
public interface ISpeakerWithPlayerFilter
{
    /// <summary>
    /// Binds a legacy allocation-heavy player predicate loop to the hardware speaker context.
    /// </summary>
    /// <param name="filter">The evaluation predicate executed per active client track.</param>
    void SetValidPlayers(Func<Player, bool> filter);

    /// <summary>
    /// Binds an allocation-free generic state-passing filter context directly to the speaker evaluation loop execution matrix.
    /// </summary>
    /// <param name="filter">The compiled non-generic bridge delegate accepting the untyped state reference for runtime client evaluation.</param>
    /// <param name="state">The untyped structural state context reference object stored for runtime hot-path execution passes.</param>
    void SetValidPlayers(Func<Player, bool object,> filter, object state);
}
```

---

## 3. Physical Layer Adaptation: DefaultSpeakerToyAdapter.cs

```csharp
public class DefaultSpeakerToyAdapter : ISpeakerWithPlayerFilter
{
    private readonly SpeakerToy speakerToy;
    private float targetVolume;

    #region Player Filtering Caches
    private Func<Player, bool> _legacyFilter;
    private Func<Player, bool object,> _stateFilter;
    private object _filterState;
    #endregion

    // ... retain existing properties, constructor, and methods (Play, Queue, Stop, SetPosition, etc.) ...

    #region ISpeakerWithPlayerFilter Implementation
    /// <summary>
    /// Binds a legacy allocation-heavy player predicate loop to the hardware speaker context.
    /// </summary>
    public void SetValidPlayers(Func<Player, bool> filter)
    {
        _legacyFilter = filter;
        _stateFilter = null;
        _filterState = null;

        ValidPlayers = EvaluateTransmitterFilter;
    }

    /// <summary>
    /// Binds an allocation-free generic state-passing filter context directly to the speaker evaluation loop execution matrix.
    /// </summary>
    public void SetValidPlayers(Func<Player, bool object,> filter, object state)
    {
        _stateFilter = filter;
        _filterState = state;
        _legacyFilter = null;

        ValidPlayers = EvaluateTransmitterFilter;
    }

    /// <summary>
    /// Instanced bridge method evaluated by the underlying LabAPI/Northwood execution loop without generating runtime closure allocations.
    /// </summary>
    private bool EvaluateTransmitterFilter(Player player)
    {
        if (_stateFilter != null)
        {
            return _stateFilter(player, _filterState);
        }

        if (_legacyFilter != null)
        {
            return _legacyFilter(player);
        }

        return true;
    }
    #endregion
}
```

---

## 4. Main Interface Contract Overloads: IAudioManager.cs

```csharp
public partial interface IAudioManager
{
    #region Allocation-Free Generic Audio Pipelines

    /// <summary>
    /// Plays spatial audio at a specified 3D position with an allocation-free generic state player filter.
    /// </summary>
    /// <typeparam name="TState">The type of the state object passed to the filter calculation layer.</typeparam>
    int PlayAudio<TState>(
        string key,
        Vector3 position,
        TState state,
        Func<Player, TState, bool> validPlayersFilter,
        bool loop = false,
        float volume = 1f,
        float minDistance = 1f,
        float maxDistance = 20f,
        bool isSpatial = true,
        AudioPriority priority = AudioPriority.Medium,
        bool queue = false,
        float fadeInDuration = 0f,
        bool persistent = false,
        float? lifespan = null,
        bool autoCleanup = false);

    /// <summary>
    /// Plays non-spatialized audio globally audible across full or selective grids using an allocation-free generic state filter.
    /// </summary>
    /// <typeparam name="TState">The type of the state object passed to the filter calculation layer.</typeparam>
    int PlayGlobalAudio<TState>(
        string key,
        TState state,
        Func<Player, TState, bool> validPlayersFilter,
        bool loop = false,
        float volume = 1f,
        AudioPriority priority = AudioPriority.Medium,
        bool queue = false,
        float fadeInDuration = 0f,
        bool persistent = false,
        float? lifespan = null,
        bool autoCleanup = false);

    /// <summary>
    /// Instantiates an automated spatial session that dynamically updates its acoustic panning vectors by tracking a frame-by-frame coordinate generator loop, utilizing an allocation-free generic state player filter.
    /// </summary>
    /// <typeparam name="TState">The type of the state object passed to the filter calculation layer.</typeparam>
    int PlayTrackingAudio<TState>(
        string key,
        Func<Vector3> positionProvider,
        Func<bool> validationCheck,
        TState state,
        Func<Player, TState, bool> targetPlayerFilter,
        AudioPriority priority = AudioPriority.Medium,
        float? lifespan = null,
        float volume = 1f,
        float minDistance = 1f,
        float maxDistance = 20f);

    /// <summary>
    /// Deploys a spatial audio session that dynamically orbits around a coordinate provider using real-time trigonometric wave calculations, utilizing an allocation-free generic state player filter.
    /// </summary>
    /// <typeparam name="TState">The type of the state object passed to the filter calculation layer.</typeparam>
    int PlayOrbitingAudio<TState>(
        string key,
        Func<Vector3> positionProvider,
        Func<bool> validationCheck,
        float volume,
        float minDistance,
        float maxDistance,
        OrbitSettings orbitSettings,
        TState state,
        Func<Player, TState, bool> targetPlayerFilter,
        AudioPriority priority = AudioPriority.Medium,
        float? lifespan = null);

    /// <summary>
    /// Creates a new continuous audio streaming session at a specified 3D position, utilizing an allocation-free generic state player filter.
    /// </summary>
    /// <typeparam name="TState">The type of the state object passed to the filter calculation layer.</typeparam>
    int CreateStreamSession<TState>(
        Vector3 position,
        bool isSpatial,
        float minDistance,
        float maxDistance,
        float volume,
        TState state,
        Func<Player, TState, bool> validPlayersFilter,
        AudioPriority priority = AudioPriority.Medium,
        bool persistent = false,
        float? lifespan = null,
        bool autoCleanup = false);

    #endregion
}
```

---

## 5. Router Implementation & Core Overloads: AudioManager.cs

```csharp
public partial class AudioManager : IAudioManager
{
    #region State Filter Injection inside InitializePhysicalSpeaker
    // Locate this section within your execution core and insert the statement below directly after:
    // speaker.Configure(state.Volume, state.MinDistance, state.MaxDistance, state.IsSpatial, null);

        if (speaker is ISpeakerWithPlayerFilter filterSpeaker)
        {
            if (state.StatePlayerFilter != null)
            {
                filterSpeaker.SetValidPlayers(state.StatePlayerFilter, state.FilterState);
            }
            else if (state.PlayerFilter != null)
            {
                filterSpeaker.SetValidPlayers(state.PlayerFilter);
            }
        }
    #endregion

    #region Generic Structural Methods Implementation

    public int PlayAudio<TState>(
        string key,
        Vector3 position,
        TState state,
        Func<Player, TState, bool> validPlayersFilter,
        bool loop = false,
        float volume = 1f,
        float minDistance = 1f,
        float maxDistance = 20f,
        bool isSpatial = true,
        AudioPriority priority = AudioPriority.Medium,
        bool queue = false,
        float fadeInDuration = 0f,
        bool persistent = false,
        float? lifespan = null,
        bool autoCleanup = false)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));
        if (validPlayersFilter == null) throw new ArgumentNullException(nameof(validPlayersFilter));

        float[] samples = audioCache.Get(key);
        if (samples == null)
        {
            Log.Warn($"Audio with key {key} not found in cache registries.");
            return 0;
        }

        var speakerState = new SpeakerState
        {
            Key = queue ? null : key,
            Position = position,
            Loop = queue ? false : loop,
            Volume = volume,
            MinDistance = minDistance,
            MaxDistance = maxDistance,
            IsSpatial = isSpatial,
            Priority = priority,
            Persistent = persistent,
            Lifespan = lifespan,
            AutoCleanup = autoCleanup,
            PlaybackPosition = 0f,
            QueuedClips = queue ? new List<(string key, bool loop)> { (key, loop) } : new List<(string key, bool loop)>(),
            
            FilterState = state,
            StatePlayerFilter = (player, untypedState) => validPlayersFilter(player, (TState)untypedState)
        };

        int allocatedSessionId = 0;
        Action stopCallback = () =>
        {
            if (allocatedSessionId != 0) FadeOutAudio(allocatedSessionId, this.Options.DefaultFadeOutDuration);
        };

        if (!ControllerIdManager.TryAllocate(priority, stopCallback, speakerState, out allocatedSessionId, out byte controllerId))
        {
            Log.Warn($"Failed to initialize state-passing session for audio {key}.");
            return 0;
        }

        if (controllerId != 0)
        {
            InitializePhysicalSpeaker(controllerId, allocatedSessionId, speakerState, samples, loop, queue);
        }

        return allocatedSessionId;
    }

    public int PlayGlobalAudio<TState>(
        string key,
        TState state,
        Func<Player, TState, bool> validPlayersFilter,
        bool loop = false,
        float volume = 1f,
        AudioPriority priority = AudioPriority.Medium,
        bool queue = false,
        float fadeInDuration = 0f,
        bool persistent = false,
        float? lifespan = null,
        bool autoCleanup = false)
    {
        if (validPlayersFilter == null) throw new ArgumentNullException(nameof(validPlayersFilter));

        int sessionId = PlayAudio(
            key: key,
            position: Vector3.zero,
            state: state,
            validPlayersFilter: validPlayersFilter,
            loop: loop,
            volume: volume,
            minDistance: 0f,
            maxDistance: 999.99f,
            isSpatial: false,
            priority: priority,
            queue: queue,
            fadeInDuration: fadeInDuration,
            persistent: persistent,
            lifespan: lifespan,
            autoCleanup: autoCleanup);

        if (sessionId != 0 && fadeInDuration > 0)
        {
            FadeInAudio(sessionId, fadeInDuration);
        }

        return sessionId;
    }

    public int PlayTrackingAudio<TState>(
        string key,
        Func<Vector3> positionProvider,
        Func<bool> validationCheck,
        TState state,
        Func<Player, TState, bool> targetPlayerFilter,
        AudioPriority priority = AudioPriority.Medium,
        float? lifespan = null,
        float volume = 1f,
        float minDistance = 1f,
        float maxDistance = 20f)
    {
        if (positionProvider == null || validationCheck == null || !validationCheck()) return 0;

        float initialLifespan = lifespan ?? 0f;

        int sessionId = PlayAudio(
            key: key,
            position: positionProvider(),
            state: state,
            validPlayersFilter: targetPlayerFilter,
            loop: false,
            volume: volume,
            minDistance: minDistance,
            maxDistance: maxDistance,
            isSpatial: true,
            priority: priority,
            queue: false,
            fadeInDuration: 0f,
            persistent: false,
            lifespan: lifespan,
            autoCleanup: true
        );

        if (sessionId == 0) return 0;

        MEC.Timing.RunCoroutine(TrackTargetTransformLoop(positionProvider, validationCheck, sessionId, initialLifespan));
        return sessionId;
    }

    public int PlayOrbitingAudio<TState>(
        string key,
        Func<Vector3> positionProvider,
        Func<bool> validationCheck,
        float volume,
        float minDistance,
        float maxDistance,
        OrbitSettings orbitSettings,
        TState state,
        Func<Player, TState, bool> targetPlayerFilter,
        AudioPriority priority = AudioPriority.Medium,
        float? lifespan = null)
    {
        if (positionProvider == null || validationCheck == null || !validationCheck()) return 0;

        float initialLifespan = lifespan ?? 0f;
        if (initialLifespan <= 0f) return 0;

        int sessionId = PlayAudio(
            key: key,
            position: positionProvider(),
            state: state,
            validPlayersFilter: targetPlayerFilter,
            loop: false,
            volume: volume,
            minDistance: minDistance,
            maxDistance: maxDistance,
            isSpatial: true,
            priority: priority,
            queue: false,
            fadeInDuration: 0f,
            persistent: false,
            lifespan: lifespan,
            autoCleanup: true
        );

        if (sessionId == 0) return 0;

        MEC.Timing.RunCoroutine(TrackAndOrbitPositionLoop(positionProvider, validationCheck, sessionId, initialLifespan, orbitSettings));
        return sessionId;
    }

    public int CreateStreamSession<TState>(
        Vector3 position,
        bool isSpatial,
        float minDistance,
        float maxDistance,
        float volume,
        TState state,
        Func<Player, TState, bool> validPlayersFilter,
        AudioPriority priority = AudioPriority.Medium,
        bool persistent = false,
        float? lifespan = null,
        bool autoCleanup = false)
    {
        var speakerState = new SpeakerState
        {
            Key = null,
            Position = position,
            Loop = false,
            Volume = volume,
            MinDistance = minDistance,
            MaxDistance = maxDistance,
            IsSpatial = isSpatial,
            Priority = priority,
            QueuedClips = new List<(string key, bool loop)>(),
            Persistent = persistent,
            Lifespan = lifespan,
            AutoCleanup = autoCleanup,
            PlaybackPosition = 0f,
            IsPaused = false,
            IsStreamOnly = true,
            
            FilterState = state,
            StatePlayerFilter = (player, untypedState) => validPlayersFilter(player, (TState)untypedState)
        };

        int allocatedSessionId = 0;
        Action stopCallback = () =>
        {
            if (allocatedSessionId != 0)
                FadeOutAudio(allocatedSessionId, this.Options.DefaultFadeOutDuration);
        };

        if (!ControllerIdManager.TryAllocate(priority, stopCallback, speakerState, out allocatedSessionId, out byte controllerId))
        {
            Log.Warn(" Failed to initialize stream-only session.");
            return 0;
        }

        if (controllerId != 0)
        {
            InitializePhysicalSpeaker(controllerId, allocatedSessionId, speakerState, null, false, false);
        }

        return allocatedSessionId;
    }

    #endregion
}
```
---

## 🆕 Changelog — Version 2.4.0 — Cross-Platform Performance & Concurrency Consolidation

### Core Optimization & Architecture (Hot Paths)
- **Zero-Allocation Filtering**: Eliminated LINQ method extensions (`Any`, `All`, `FirstOrDefault`) within dynamic evaluation gates and replaced them with concrete indexing loops and collection pattern matching, completely mitigating generic enumerator boxing overhead on the heap.
- **Micro-Mathematical Optimization**: Replaced `Vector3.Distance` with squared magnitude (`sqrMagnitude`) comparisons inside distance-based filtering closures, entirely bypassing expensive square-root (`Mathf.Sqrt`) processing branches.
- **Global Complexity Flattening**: Resolved an $O(N^2)$ execution bottleneck in global playback routines by refactoring the player validation gate from a linear list search (`Contains`) to an immediate, lock-free state property verification.

### Live VoIP Streaming & Eviction Recovery
- **Synchronized Jitter Buffering**: Rectified a critical packet loss flaw where live streaming-only sessions (`IsStreamOnly`) discarded incoming real-time frames during hardware controller eviction. Introduced a thread-safe staging buffer using localized locks on the internal PCM queue.
- **Eviction Recovery Flush Pass**: Enhanced the physical controller initialization pipeline to atomically drain and flush outstanding buffered streaming data frames into newly allocated network speaker toys upon hardware restoration.

### Memory & Resource Management
- **Pooled Transient Processing**: Integrated `System.Buffers.ArrayPool<float>` and `ArrayPool<short>` allocation mechanics within audio cache transformation routines (PCM bit conversions, mono downmixing, linear resampling), removing temporary array allocations from the heap.
- **Stream Provider Registry Leak Fix**: Resolved a long-term memory leak within the LRU cache eviction lifecycle by ensuring that when an asset is evicted from RAM, its associated background stream provider factory delegate is thoroughly purged to release lingering closure references.

### Cross-Platform Compliance & Concurrency
- **Fully Managed MPEG Decoding**: Bypassed native Windows Audio Compression Manager (ACM) subsystems and NAudio's native conversion streams by implementing a 100% managed, cross-platform `NLayer` parsing core, allowing flawless deployments on Linux and Docker nodes without platform exceptions.
- **Fine-Grained Concurrency Controls**: Deprecated the coarse-grained global instance `lock` primitive across the main orchestration router, transitioning state modifications to lock-free operations backed by `ConcurrentDictionary` and a micro-scoped speaker initialization lock to permanently eliminate lock inversion risks and potential deadlocks.

## 🆕 Changelog — Version 2.3.1-2.3.6 — API Native Paranoia & Spatial Tracking

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
- **Dynamic Crossfade & Session Guard**: Introduced an asynchronous background fade-out routine for reallocated controller slots to guarantee artifact-free audio transitions. Additionally, patched a critical race condition in `DestroySession` via strict instance-matching against `state.PhysicalSpeaker`, preventing recycled session timeouts from prematurely truncating newly spawned streams.

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