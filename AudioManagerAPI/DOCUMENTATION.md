# AudioManagerAPI - Architecture API Registry

## 📦 Class: `AudioCache`

Thread-safe audio sample cache implementing predictive background pre-decoding, lock-free multi-format parsing, and least-recently-used (LRU) memory eviction with ArrayPool optimization.

---

## 📦 Class: `AudioConfig`

Configuration data profile representing system capabilities and engine defaults.

### 🔹 `Validate()`
**Description:** Validates parameters defensively and forces hard system clamps to guarantee downstream pipeline operations never throw exceptions.
```csharp
public void Validate()
```

---

## 📦 Class: `AudioConfigLoader`

Thread-safe I/O orchestration engine responsible for serialization, deserialization, and defensive recovery of system configuration files.

### 🔹 `LoadOrCreate()`
**Description:** Resolves the system configuration from disk with atomic fallback integrity.
```csharp
public static AudioConfig LoadOrCreate()
```

### 🔹 `SaveConfig()`
**Description:** Explicitly pushes a runtime configuration instance to memory disk blocks.
```csharp
public static void SaveConfig(AudioConfig config)
```

---

## 📦 Class: `ControllerIdManager`

Manages physical audio controller IDs (1-254) with priority-based allocation. Manages abstract session states independently of physical IDs.

### 🔹 `TryAllocate()`
**Description:** Registers a new audio session and attempts to allocate a physical controller ID.
```csharp
public static bool TryAllocate(AudioPriority priority, Action stopCallback, SpeakerState state, out int sessionId, out byte controllerId)
```

### 🔹 `ReleaseController()`
**Description:** Releases a controller ID, making it available for reuse.
```csharp
public static void ReleaseController(byte controllerId)
```

### 🔹 `TryGetActiveController()`
**Description:** Returns physical ID of the controller currently associated with a session, if any. Returns false if session doesn't exist or has no active controller.
```csharp
public static bool TryGetActiveController(int sessionId, out byte controllerId)
```

### 🔹 `GetSessionState()`
**Description:** Retrieves the state object associated with a session ID.
```csharp
public static SpeakerState GetSessionState(int sessionId)
```

### 🔹 `DestroySession()`
**Description:** Removes a session entirely, freeing its state memory.
```csharp
public static void DestroySession(int sessionId)
```

### 🔹 `FullReset()`
**Description:** Completely resets the controller manager state. Must be called during round restarts to prevent ID exhaustion and memory leaks.
```csharp
public static void FullReset()
```

---

## 📦 Class: `Mp3Decoder`

Provides cross-platform, high-performance MPEG-1 Layer III (MP3) decoding capabilities using a fully managed parsing engine with zero native OS dependencies.

### 🔹 `DecodeMp3ToFloatRented()`
**Description:** Decodes an MP3 binary stream directly into a rented float PCM array. Native Linux/Docker compliant - completely bypasses Windows ACM subsystems.
```csharp
public static float[] DecodeMp3ToFloatRented(Stream mp3Stream, out int totalSamples, out int sampleRate, out int channels)
```
**Parameters:**
- `mp3Stream`: The source compressed input stream containing MPEG data.
- `totalSamples`: The exact number of valid float samples written to the rented array.
- `sampleRate`: The native sampling rate discovered in the MP3 frame headers.
- `channels`: The channel count (e.g., 1 for Mono, 2 for Stereo) of the source asset.
**Returns:** A pooled float array containing the linear PCM data; must be returned to ArrayPool after usage.

---

## 📦 Class: `DefaultAudioManager`

Provides a static, ready-to-use entry point for the AudioManager.

> **Note:** The manager is preconfigured with the default LabAPI-based speaker implementation. Configuration settings are automatically loaded from an external configuration file on first access. If the configuration file does not exist (e.g. `AudioConfig.json`), it will be generated automatically on the first launch with default values such as the speaker factory type and audio cache size. Example usage in a plugin: ` DefaultAudioManager.RegisterAudio( "explosionSound", () => Assembly.GetExecutingAssembly() .GetManifestResourceStream("MyPlugin.Audio.explosion.wav")); int sessionId = DefaultAudioManager.Play( "explosionSound", queue: true, fadeInDuration: 2f); DefaultAudioManager.Pause(sessionId); DefaultAudioManager.Resume(sessionId); DefaultAudioManager.Skip(sessionId, 1); DefaultAudioManager.FadeOut(sessionId, 2f); DefaultAudioManager.Stop(sessionId); `

### 🔹 `Instance`
**Description:** Singleton AudioManager instance initialized lazily on first access using configuration settings from AudioConfig.json.
```csharp
public static IAudioManager Instance => _lazyInstance.Value;
```

### 🔹 `RegisterAudio()`
**Description:** Registers an audio stream provider for a given key.
```csharp
public static void RegisterAudio(string key, Func<Stream> streamProvider)
```
**Parameters:**
- `key`: The unique key for the audio.
- `streamProvider`: A function that provides the audio stream.

### 🔹 `Play()`
**Description:** Plays the audio registered under the given key with default parameters: non-spatial, full volume, no looping, low priority, audible to all ready players.
```csharp
public static int Play(string key, bool queue = false, float fadeInDuration = 0f) => Instance.PlayGlobalAudio( key, loop: false, volume: 1f, priority: AudioPriority.Low, validPlayersFilter: null, queue: queue, fadeInDuration: fadeInDuration );
```
**Parameters:**
- `key`: The unique key of a previously registered audio stream.
- `queue`: Whether to queue the audio instead of playing immediately.
- `fadeInDuration`: The duration of the fade-in effect in seconds (0 for no fade).
**Returns:** The session ID allocated for this playback request, or 0 if initialization failed.

### 🔹 `Pause()`
**Description:** Pauses playback of the audio associated with the specified session ID.
```csharp
public static void Pause(int sessionId) => Instance.PauseAudio(sessionId);
```
**Parameters:**
- `sessionId`: The session ID to pause.

### 🔹 `Resume()`
**Description:** Resumes playback of the paused audio associated with the specified session ID.
```csharp
public static void Resume(int sessionId) => Instance.ResumeAudio(sessionId);
```
**Parameters:**
- `sessionId`: The session ID to resume.

### 🔹 `Skip()`
**Description:** Skips the current or queued audio clips for the specified session ID.
```csharp
public static void Skip(int sessionId, int count = 1) => Instance.SkipAudio(sessionId, count);
```
**Parameters:**
- `sessionId`: The session ID to skip clips on.
- `count`: The number of clips to skip, including the current one.

### 🔹 `FadeIn()`
**Description:** Fades in the audio volume for the specified session ID over the given duration.
```csharp
public static void FadeIn(int sessionId, float duration) => Instance.FadeInAudio(sessionId, duration);
```
**Parameters:**
- `sessionId`: The session ID to fade in.
- `duration`: The duration of the fade-in in seconds.

### 🔹 `FadeOut()`
**Description:** Fades out the audio volume for the specified session ID over the given duration and stops playback.
```csharp
public static void FadeOut(int sessionId, float duration) => Instance.FadeOutAudio(sessionId, duration);
```
**Parameters:**
- `sessionId`: The session ID to fade out.
- `duration`: The duration of the fade-out in seconds.

### 🔹 `Stop()`
**Description:** Stops playback and destroys the session entirely, releasing resources and states.
```csharp
public static void Stop(int sessionId) => Instance.DestroySession(sessionId);
```
**Parameters:**
- `sessionId`: The session ID to stop and destroy.

---

## 📦 Class: `DefaultSpeakerFactory`

Default factory that produces and manages `DefaultSpeakerToyAdapter` instances. Operates strictly at the physical hardware layer of LabAPI using byte-based controller IDs.

### 🔹 `CreateSpeaker()`
**Description:** Creates a new physical speaker adapter for the specified position and hardware controller ID. If a speaker already exists for this ID, its position is updated instead.
```csharp
public ISpeaker CreateSpeaker(Vector3 position, byte controllerId)
```
**Parameters:**
- `position`: The 3D world position for physical audio playback.
- `controllerId`: The unique hardware controller ID allocated by the system (1-254).
**Returns:** An `ISpeaker` instance, or null if LabAPI fails to create the underlying object.

### 🔹 `GetSpeaker()`
**Description:** Retrieves an existing physical speaker by its hardware controller ID. Attempts to find unregistered instances in the game world if missing from the internal registry.
```csharp
public ISpeaker GetSpeaker(byte controllerId)
```
**Parameters:**
- `controllerId`: The hardware controller ID of the speaker.
**Returns:** The `ISpeaker` instance, or null if not found in the registry or the game world.

### 🔹 `RemoveSpeaker()`
**Description:** Removes a speaker from the factory's management and explicitly destroys its physical representation.
```csharp
public bool RemoveSpeaker(byte controllerId)
```
**Parameters:**
- `controllerId`: The hardware controller ID of the speaker to remove.
**Returns:** True if the speaker was successfully removed and destroyed, false otherwise.

### 🔹 `ClearSpeakers()`
**Description:** Clears all managed speakers and destroys their physical representations in the game world.
```csharp
public void ClearSpeakers()
```

---

## 📦 Class: `DefaultSpeakerToyAdapter`

Adapts LabAPI's `SpeakerToy` to the `ISpeakerWithPlayerFilter` interface, including pause, resume, skip, and fade-in/fade-out functionality. Acts as the physical layer implementation for audio playback.

### 🔹 `QueueEmpty`
**Description:** Occurs when the speaker's audio queue becomes empty.
```csharp
public event Action QueueEmpty;
```

### 🔹 `IsValid`
**Description:** Gets a value indicating whether the underlying LabAPI SpeakerToy instance is valid and operational.
```csharp
public bool IsValid => speakerToy?.Base != null;
```

### 🔹 `IsQueueEmpty`
**Description:** Gets a value indicating whether the speaker's audio queue is currently empty. Safely evaluates to true if the underlying transmitter does not exist.
```csharp
public bool IsQueueEmpty => (SpeakerToy.GetTransmitter(speakerToy.ControllerId)?.AudioClipSamples.Count ?? 0) == 0;
```

### 🔹 `QueuedClipsCount`
**Description:** Gets the number of audio clips currently queued for playback.
```csharp
public int QueuedClipsCount => SpeakerToy.GetTransmitter(speakerToy.ControllerId)?.AudioClipSamples.Count ?? 0;
```

### 🔹 `ValidPlayers`
**Description:** Gets or sets the player filter used to determine which players can hear audio from this speaker.

> **Remarks:** This property is typically used to restrict playback to a subset of players. For example, only players in a certain room or team.
```csharp
public Func<Player, bool> ValidPlayers
```

### 🔹 `DefaultSpeakerToyAdapter()`
**Description:** Initializes a new instance of the `DefaultSpeakerToyAdapter` class.
```csharp
public DefaultSpeakerToyAdapter(SpeakerToy speakerToy)
```
**Parameters:**
- `speakerToy`: The underlying LabAPI SpeakerToy instance to adapt.

### 🔹 `Play()`
**Description:** Plays the provided audio samples with the specified looping behavior and starting position.
```csharp
public void Play(float[] samples, bool loop, float playbackPosition = 0f)
```
**Parameters:**
- `samples`: The float array of PCM audio samples to play.
- `loop`: Whether the audio should loop continuously.
- `playbackPosition`: The starting playback position in seconds.

### 🔹 `Queue()`
**Description:** Queues the provided list of audio samples to be played after the current playback finishes.
```csharp
public void Queue(float[] samples, bool loop)
```
**Parameters:**
- `samples`: The float array of PCM audio samples to queue.
- `loop`: Whether the queued audio should loop when it plays.

### 🔹 `ClearQueue()`
**Description:** Clears all currently queued audio samples for this physical speaker.
```csharp
public void ClearQueue()
```

### 🔹 `Stop()`
**Description:** Immediately stops the current audio playback.
```csharp
public void Stop()
```

### 🔹 `Destroy()`
**Description:** Destroys the underlying LabAPI SpeakerToy and releases its allocated game resources.
```csharp
public void Destroy()
```

### 🔹 `Pause()`
**Description:** Pauses the current audio playback, maintaining the current position.
```csharp
public void Pause()
```

### 🔹 `Resume()`
**Description:** Resumes the previously paused audio playback.
```csharp
public void Resume()
```

### 🔹 `Skip()`
**Description:** Skips the specified number of queued clips. If the count exceeds the queue length, playback stops.
```csharp
public void Skip(int count)
```
**Parameters:**
- `count`: The number of clips to skip.

### 🔹 `FadeIn()`
**Description:** Fades in the audio volume from 0 to the target volume over the specified duration.
```csharp
public void FadeIn(float duration)
```
**Parameters:**
- `duration`: The duration of the fade-in effect in seconds.

### 🔹 `FadeOut()`
**Description:** Fades out the audio volume to 0 over the specified duration and optionally stops playback upon completion.
```csharp
public void FadeOut(float duration, Action onComplete = null)
```
**Parameters:**
- `duration`: The duration of the fade-out effect in seconds.
- `onComplete`: Optional delegate invoked when the fade-out is complete.

### 🔹 `SetValidPlayers()`
**Description:** Binds an allocation-free generic state-passing filter context directly to the speaker evaluation loop execution matrix.
```csharp
public void SetValidPlayers(Func<Player, object, bool> filter, object state)
```

### 🔹 `SetVolume()`
**Description:** Sets the playback volume for the speaker. Use 0.0f to mute and 1.0f for maximum volume. Intermediate values apply proportionally.
```csharp
public void SetVolume(float volume)
```
**Parameters:**
- `volume`: A float value ranging from 0.0 to 1.0.

### 🔹 `SetMinDistance()`
**Description:** Sets the minimum distance at which the audio starts to attenuate. Ensures the value is non-negative.
```csharp
public void SetMinDistance(float minDistance)
```
**Parameters:**
- `minDistance`: The minimum distance in meters.

### 🔹 `SetMaxDistance()`
**Description:** Sets the maximum distance beyond which the audio is no longer audible. Ensures the value is non-negative.
```csharp
public void SetMaxDistance(float maxDistance)
```
**Parameters:**
- `maxDistance`: The maximum distance in meters.

### 🔹 `SetSpatialization()`
**Description:** Enables or disables spatial audio playback for the speaker.
```csharp
public void SetSpatialization(bool isSpatial)
```
**Parameters:**
- `isSpatial`: If `true`, enables 3D spatial audio; if `false`, plays audio in a non-spatial (2D) context.

### 🔹 `SetPosition()`
**Description:** Sets the 3D world position of the speaker.
```csharp
public void SetPosition(Vector3 position)
```
**Parameters:**
- `position`: The new position vector in the 3D context.

### 🔹 `GetPlaybackPosition()`
**Description:** Gets the current playback position in seconds based on the audio transmitter's state.
```csharp
public float GetPlaybackPosition()
```
**Returns:** The current playback position in seconds, or 0f if no active transmitter exists.

---

## 📦 Enum: `AudioPriority`

Represents priority levels used to determine which audio sources should take precedence when multiple sounds compete for playback.

> **Note:** Higher priority audio may interrupt or override lower priority sounds depending on the audio manager's playback rules. Priority levels: `Lowest` – Background or non-essential audio that should never interrupt other sounds. `Low` – Ambient or minor sound effects with minimal importance. `Medium` – Default priority used for standard gameplay or interaction sounds. `High` – Important sounds that may override lower priority audio. `Max` – Critical audio that should always take precedence over all other sounds.

---

## 📦 Class: `AudioRegistryExtensions`

Provides advanced high-performance utility extensions to automate embedded manifest assembly resource registration onto the Audio Engine.

### 🔹 `TryRegisterEmbeddedResource()`
**Description:** Attempts to locate and register an individual embedded .wav audio asset from the assembly manifest into the core audio engine.
```csharp
public static bool TryRegisterEmbeddedResource(this IAudioManager audioEngine, Assembly assembly, string audioKey)
```
**Parameters:**
- `audioEngine`: The active `IAudioManager` system engine engine core layer instance.
- `assembly`: The targeted compiled `Assembly` matrix housing the embedded binary audio file stream.
- `audioKey`: The unique target tracking key lookup string profile configuration (e.g. "drs.siren_loop").
**Returns:** `true` if the embedded resource stream was successfully discovered and registered cleanly; otherwise, `false`.

### 🔹 `RegisterEmbeddedResources()`
**Description:** Systematically scans the provided assembly manifest layout to register a mass array batch of audio resource keys simultaneously.
```csharp
public static void RegisterEmbeddedResources(this IAudioManager audioEngine, Assembly assembly, IEnumerable<string> audioKeys)
```
**Parameters:**
- `audioEngine`: The active `IAudioManager` system engine engine core layer instance.
- `assembly`: The targeted compiled `Assembly` matrix housing the embedded binary audio assets.
- `audioKeys`: The collection sequence tracking all explicit audio keys configured for deployment.

---

## 📦 Class: `AudioFilters`

Provides common SCP:SL-specific audio filters for use with speaker configurations. Evaluated dynamically per player during audio transmission with zero heap allocation overhead.

### 🔹 `ByRole()`
**Description:** Filters players by their role type.
```csharp
public static Func<Player, bool> ByRole(RoleTypeId roleType)
```

### 🔹 `ByTeam()`
**Description:** Filters players by their team.
```csharp
public static Func<Player, bool> ByTeam(Team team)
```

### 🔹 `ByDistance()`
**Description:** Filters players within a specified distance from a position using optimized squared magnitude.
```csharp
public static Func<Player, bool> ByDistance(Vector3 position, float maxDistance)
```

### 🔹 `IsAlive()`
**Description:** Filters players who are currently alive.
```csharp
public static Func<Player, bool> IsAlive()
```

### 🔹 `IsInRoomWhereLightsAre()`
**Description:** Filters players in a room where the lights match the specified state without LINQ or lambda allocations.
```csharp
public static Func<Player, bool> IsInRoomWhereLightsAre(bool lightsEnabled)
```

### 🔹 `IsConditionTrue()`
**Description:** Filters players based on a dynamically evaluated condition.
```csharp
public static Func<Player, bool> IsConditionTrue(Func<bool> condition)
```
**Parameters:**
- `condition`: A function returning a boolean condition (e.g., () => Round.IsStarted).

### 🔹 `IsInRoom()`
**Description:** Filters players in a specific room type.
```csharp
public static Func<Player, bool> IsInRoom(RoomName roomType)
```

---

## 📦 Class: `AudioManager`

High-performance, deadlock-free audio orchestration router utilizing fine-grained concurrency controls and lock-free state operations to secure server frame-rate stability.

---

## 📦 Class: `AudioOptions`

Represents the read-only runtime audio playback settings currently used by the `AudioManager`. These values are loaded from the configuration file during initialization and dictate the system's behavior.

### 🔹 `CacheSize`
**Description:** Gets the maximum number of audio clips stored in the LRU cache.
```csharp
public int CacheSize { get; internal set; }
```

### 🔹 `UseDefaultSpeakerFactory`
**Description:** Gets a value indicating whether the built-in default speaker factory is being used.
```csharp
public bool UseDefaultSpeakerFactory { get; internal set; }
```

### 🔹 `DefaultFadeInDuration`
**Description:** Gets the default duration (in seconds) for fade-in effects across the API.
```csharp
public float DefaultFadeInDuration { get; internal set; }
```

### 🔹 `DefaultFadeOutDuration`
**Description:** Gets the default duration (in seconds) for fade-out effects across the API.
```csharp
public float DefaultFadeOutDuration { get; internal set; }
```

---

## 📦 Interface: `IAudioManager`

Defines the contract for managing audio playback and session lifecycle in SCP: Secret Laboratory.

---

## 📦 Struct: `OrbitSettings`

Encapsulates the configuration profile for trigonometric audio orbital movement.

---

## 📦 Interface: `ISpeaker`

Represents a physical hardware speaker capable of playing audio samples, managing volume, and spatial positioning.

---

## 📦 Interface: `ISpeakerFactory`

Defines a hardware-level factory for creating and managing physical speaker instances based on controller IDs.

---

## 📦 Interface: `ISpeakerWithPlayerFilter`

Extends `ISpeaker` to support LabAPI's player-specific audio filtering capabilities.

---

## 📦 Class: `SpeakerExtensions`

Provides extension methods for configuring and managing the lifecycle of physical `ISpeaker` instances.

### 🔹 `RestoreQueue()`
**Description:** Restores queued clips from the session state to the speaker's playback queue without LINQ allocation.
```csharp
public static int RestoreQueue(this ISpeaker speaker, SpeakerState state, AudioCache audioCache)
```

### 🔹 `static()`
**Description:** Retrieves the current queue status safely with zero allocation.
```csharp
public static (int queuedCount, string currentClip) GetQueueStatus(this ISpeaker speaker, SpeakerState state = null)
```

---

## 📦 Class: `SpeakerState`

Represents the abstract runtime configuration of an audio session, used for routing, recovery, playback resumption, and lifecycle management.

### 🔹 `Key`
**Description:** The unique audio key used to identify and retrieve the associated PCM samples.
```csharp
public string Key { get; set; }
```

### 🔹 `PhysicalSpeaker`
**Description:** Instance of the physical speaker currently allocated to this session, if any.
```csharp
public ISpeaker PhysicalSpeaker;
```

### 🔹 `HasPhysicalSpeaker`
**Description:** Indicates whether the device has a physical speaker.
```csharp
public bool HasPhysicalSpeaker;
```

### 🔹 `Position`
**Description:** The 3D world-space position where the audio was originally placed. Used to reinstantiate spatial audio correctly upon recovery.
```csharp
public Vector3 Position { get; set; }
```

### 🔹 `Loop`
**Description:** Indicates whether the audio should loop during playback.
```csharp
public bool Loop { get; set; }
```

### 🔹 `Volume`
**Description:** The playback volume for this session (range: 0.0 to 1.0).
```csharp
public float Volume { get; set; }
```

### 🔹 `MinDistance`
**Description:** The minimum audible distance from the audio source before volume begins to fall off.
```csharp
public float MinDistance { get; set; }
```

### 🔹 `MaxDistance`
**Description:** The maximum distance beyond which the audio is no longer heard.
```csharp
public float MaxDistance { get; set; }
```

### 🔹 `IsSpatial`
**Description:** Whether this session uses 3D spatial audio positioning.
```csharp
public bool IsSpatial { get; set; }
```

### 🔹 `Priority`
**Description:** Defines the priority level of the session when allocating physical playback resources.
```csharp
public AudioPriority Priority { get; set; }
```

### 🔹 `PlayerFilter`
**Description:** An optional filter function to determine which players are valid listeners for playback. If provided, audio will only be transmitted to players that satisfy the condition.
```csharp
public Func<Player, bool> PlayerFilter { get; set; }
```

### 🔹 `FilterState`
**Description:** Stores the untyped reference to the custom state object used for allocation-free filtering.
```csharp
public object FilterState { get; set; }
```

### 🔹 `StatePlayerFilter`
**Description:** Stores the compiled non-generic bridge delegate for state-passing evaluation loops.
```csharp
public Func<Player, object, bool> StatePlayerFilter { get; set; }
```

### 🔹 `QueuedClips`
**Description:** A list of audio clips that are pending playback, represented as tuples: (`key`, `loop`).
```csharp
public List<(string key, bool loop)> QueuedClips { get; set; } = new List<(string key, bool loop)>();
```

### 🔹 `Persistent`
**Description:** Whether this session is flagged for persistence across physical evictions.
```csharp
public bool Persistent { get; set; }
```

### 🔹 `Lifespan`
**Description:** Optional time (in seconds) to live for this session. If set and `AutoCleanup` is true, the audio auto-fades and stops after this lifespan.
```csharp
public float? Lifespan { get; set; }
```

### 🔹 `AutoCleanup`
**Description:** Indicates whether the session should automatically stop and fade out after its lifespan ends.
```csharp
public bool AutoCleanup { get; set; }
```

### 🔹 `PlaybackPosition`
**Description:** The playback position (in seconds or samples, depending on implementation) where audio should resume.
```csharp
public float PlaybackPosition { get; set; }
```

### 🔹 `IsPaused`
**Description:** Indicates whether the session was explicitly paused. Required to restore the correct state if a physical speaker is evicted and later re-allocated.
```csharp
public bool IsPaused { get; set; }
```

### 🔹 `PcmQueue`
**Description:** A FIFO queue of PCM buffers waiting to be played by this session. Used for real-time audio streaming.
```csharp
public Queue<float[]> PcmQueue { get; } = new Queue<float[]>();
```

### 🔹 `IsStreamOnly`
**Description:** Indicates wheter the session is stream only - without static audio. Used for real-time audio streaming.
```csharp
public bool IsStreamOnly { get; set; }
```

---

## 📦 Class: `StaticSpeakerFactory`

Provides static access to a centralized `ISpeakerFactory` implementation. In V2.0.0, direct manipulation of physical speakers by external plugins is prohibited to maintain session synchronization. This class solely provides the singleton instance.

### 🔹 `Instance`
**Description:** Retrieves the shared `ISpeakerFactory` instance used natively by the API. Exposed strictly for initializing the router (`AudioManagerAPI.Features.Management.AudioManager`) or dependency injection. Plugins must never cast or use this to manually create/destroy physical speakers.
```csharp
public static ISpeakerFactory Instance => factory;
```
**Returns:** The shared global `ISpeakerFactory` instance wrapper layer.

---
