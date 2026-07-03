# Audio Manager API - Architecture API Registry

## 📦 Class: AudioCache

### 🔹 `Register()`
**Description:** Registers a stream provider and dispatches a background worker thread to proactively pre-decode the asset into RAM, eliminating first-play disk I/O stutter.
```csharp
public void Register(string key, Func<Stream> streamProvider)
```

### 🔹 `Get()`
**Description:** Resolves audio sample references, executing on-demand lock-free decoding only if the predictive background warmup has not completed yet.
```csharp
public float[] Get(string key)
```

---

## 📦 Class: AudioConfig

### 🔹 `Validate()`
**Description:** Validates parameters defensively and forces hard system clamps to guarantee downstream pipeline operations never throw exceptions.
```csharp
public void Validate()
```

---

## 📦 Class: AudioConfigLoader

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

## 📦 Class: ControllerIdManager

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

## 📦 Class: Mp3Decoder

### 🔹 `DecodeMp3ToPcm16()`
**Description:** Decodes a raw binary byte array containing MP3 audio streams into a 16-bit linear PCM signed short array utilizing the native Windows Audio Compression Manager (ACM).
```csharp
public static short[] DecodeMp3ToPcm16(byte[] mp3Bytes)
```

---

## 📦 Class: DefaultAudioManager

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

### 🔹 `Play()`
**Description:** Plays the audio registered under the given key with default parameters: non-spatial, full volume, no looping, low priority, audible to all ready players. The session ID allocated for this playback request, or 0 if initialization failed. </returns>
```csharp
public static int Play(string key, bool queue = false, float fadeInDuration = 0f) => Instance.PlayGlobalAudio( key, loop: false, volume: 1f, priority: AudioPriority.Low, validPlayersFilter: null, queue: queue, fadeInDuration: fadeInDuration );
```

### 🔹 `Pause()`
**Description:** Pauses playback of the audio associated with the specified session ID.
```csharp
public static void Pause(int sessionId) => Instance.PauseAudio(sessionId);
```

### 🔹 `Resume()`
**Description:** Resumes playback of the paused audio associated with the specified session ID.
```csharp
public static void Resume(int sessionId) => Instance.ResumeAudio(sessionId);
```

### 🔹 `Skip()`
**Description:** Skips the current or queued audio clips for the specified session ID.
```csharp
public static void Skip(int sessionId, int count = 1) => Instance.SkipAudio(sessionId, count);
```

### 🔹 `FadeIn()`
**Description:** Fades in the audio volume for the specified session ID over the given duration.
```csharp
public static void FadeIn(int sessionId, float duration) => Instance.FadeInAudio(sessionId, duration);
```

### 🔹 `FadeOut()`
**Description:** Fades out the audio volume for the specified session ID over the given duration and stops playback.
```csharp
public static void FadeOut(int sessionId, float duration) => Instance.FadeOutAudio(sessionId, duration);
```

### 🔹 `Stop()`
**Description:** Stops playback and destroys the session entirely, releasing resources and states.
```csharp
public static void Stop(int sessionId) => Instance.DestroySession(sessionId);
```

---

## 📦 Class: DefaultSpeakerFactory

### 🔹 `CreateSpeaker()`
**Description:** Creates a new physical speaker adapter for the specified position and hardware controller ID. If a speaker already exists for this ID, its position is updated instead.
```csharp
public ISpeaker CreateSpeaker(Vector3 position, byte controllerId)
```

### 🔹 `GetSpeaker()`
**Description:** Retrieves an existing physical speaker by its hardware controller ID. Attempts to find unregistered instances in the game world if missing from the internal registry.
```csharp
public ISpeaker GetSpeaker(byte controllerId)
```

### 🔹 `RemoveSpeaker()`
**Description:** Removes a speaker from the factory's management and explicitly destroys its physical representation.
```csharp
public bool RemoveSpeaker(byte controllerId)
```

### 🔹 `ClearSpeakers()`
**Description:** Clears all managed speakers and destroys their physical representations in the game world.
```csharp
public void ClearSpeakers()
```

---

## 📦 Class: DefaultSpeakerToyAdapter

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
**Description:** Gets a value indicating whether the speaker's audio queue is currently empty. Safely evaluates to true if the underlying transmitter does not exist. <value> <c>true</c> if there are no queued audio clips or no transmitter; otherwise, <c>false</c>. </value>
```csharp
public bool IsQueueEmpty => (SpeakerToy.GetTransmitter(speakerToy.ControllerId)?.AudioClipSamples.Count ?? 0) == 0;
```

### 🔹 `QueuedClipsCount`
**Description:** Gets the number of audio clips currently queued for playback. <value> An integer representing the number of queued clips. Returns 0 if no transmitter is found. </value>
```csharp
public int QueuedClipsCount => SpeakerToy.GetTransmitter(speakerToy.ControllerId)?.AudioClipSamples.Count ?? 0;
```

### 🔹 `ValidPlayers`
**Description:** Gets or sets the player filter used to determine which players can hear audio from this speaker. <value> A delegate that returns <c>true</c> for valid players who should receive audio playback. </value> <remarks> This property is typically used to restrict playback to a subset of players. For example, only players in a certain room or team. </remarks>
```csharp
public Func<Player, bool> ValidPlayers
```

### 🔹 `DefaultSpeakerToyAdapter()`
**Description:** Initializes a new instance of the <see cref="DefaultSpeakerToyAdapter"/> class.
```csharp
public DefaultSpeakerToyAdapter(SpeakerToy speakerToy)
```

### 🔹 `Play()`
**Description:** Plays the provided audio samples with the specified looping behavior and starting position.
```csharp
public void Play(float[] samples, bool loop, float playbackPosition = 0f)
```

### 🔹 `Queue()`
**Description:** Queues the provided list of audio samples to be played after the current playback finishes.
```csharp
public void Queue(float[] samples, bool loop)
```

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

### 🔹 `FadeIn()`
**Description:** Fades in the audio volume from 0 to the target volume over the specified duration.
```csharp
public void FadeIn(float duration)
```

### 🔹 `FadeOut()`
**Description:** Fades out the audio volume to 0 over the specified duration and optionally stops playback upon completion.
```csharp
public void FadeOut(float duration, Action onComplete = null)
```

### 🔹 `SetValidPlayers()`
**Description:** Sets the filter function for valid players. <example> <code> adapter.SetValidPlayers(p => Player.ReadyList.Contains(p)); </code> </example>
```csharp
public void SetValidPlayers(Func<Player, bool> playerFilter)
```

### 🔹 `SetVolume()`
**Description:** Sets the playback volume for the speaker. Use 0.0f to mute and 1.0f for maximum volume. Intermediate values apply proportionally.
```csharp
public void SetVolume(float volume)
```

### 🔹 `SetMinDistance()`
**Description:** Sets the minimum distance at which the audio starts to attenuate. Ensures the value is non-negative.
```csharp
public void SetMinDistance(float minDistance)
```

### 🔹 `SetMaxDistance()`
**Description:** Sets the maximum distance beyond which the audio is no longer audible. Ensures the value is non-negative.
```csharp
public void SetMaxDistance(float maxDistance)
```

### 🔹 `SetSpatialization()`
**Description:** Enables or disables spatial audio playback for the speaker. If <c>true</c>, enables 3D spatial audio; if <c>false</c>, plays audio in a non-spatial (2D) context. </param>
```csharp
public void SetSpatialization(bool isSpatial)
```

### 🔹 `SetPosition()`
**Description:** Sets the 3D world position of the speaker.
```csharp
public void SetPosition(Vector3 position)
```

### 🔹 `GetPlaybackPosition()`
**Description:** Gets the current playback position in seconds based on the audio transmitter's state.
```csharp
public float GetPlaybackPosition()
```

---

## 📦 Class: AudioRegistryExtensions

### 🔹 `TryRegisterEmbeddedResource()`
**Description:** Attempts to locate and register an individual embedded .wav audio asset from the assembly manifest into the core audio engine.
```csharp
public static bool TryRegisterEmbeddedResource(this IAudioManager audioEngine, Assembly assembly, string audioKey)
```

### 🔹 `RegisterEmbeddedResources()`
**Description:** Systematically scans the provided assembly manifest layout to register a mass array batch of audio resource keys simultaneously.
```csharp
public static void RegisterEmbeddedResources(this IAudioManager audioEngine, Assembly assembly, IEnumerable<string> audioKeys)
```

---

## 📦 Class: AudioFilters

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
**Description:** Filters players within a specified distance from a position.
```csharp
public static Func<Player, bool> ByDistance(Vector3 position, float maxDistance)
```

### 🔹 `IsAlive()`
**Description:** Filters players who are currently alive.
```csharp
public static Func<Player, bool> IsAlive()
```

### 🔹 `IsInRoomWhereLightsAre()`
**Description:** Filters players in a room where the lights are in the specified state (enabled or disabled).
```csharp
public static Func<Player, bool> IsInRoomWhereLightsAre(bool lightsEnabled)
```

### 🔹 `IsConditionTrue()`
**Description:** Filters players based on a dynamically evaluated condition.
```csharp
public static Func<Player, bool> IsConditionTrue(Func<bool> condition)
```

### 🔹 `IsInRoom()`
**Description:** Filters players in a specific room type.
```csharp
public static Func<Player, bool> IsInRoom(RoomName roomType)
```

---

## 📦 Class: AudioOptions

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

## 📦 Class: SpeakerExtensions

### 🔹 `Configure()`
**Description:** Configures the specified physical speaker with audio settings, spatialization, and player filtering.
```csharp
public static ISpeaker Configure(this ISpeaker speaker, float volume, float minDistance, float maxDistance, bool isSpatial, Func<Player, bool> playerFilter = null)
```

### 🔹 `UpdatePlaybackPosition()`
**Description:** Updates the playback position in the session state for persistent speakers.
```csharp
public static bool UpdatePlaybackPosition(this ISpeaker speaker, byte controllerId, SpeakerState state)
```

### 🔹 `SetVolume()`
**Description:** Sets the volume for the specified physical speaker.
```csharp
public static bool SetVolume(this ISpeaker speaker, float volume)
```

### 🔹 `SetPosition()`
**Description:** Sets the 3D world position for the specified physical speaker.
```csharp
public static bool SetPosition(this ISpeaker speaker, Vector3 position)
```

### 🔹 `RestoreQueue()`
**Description:** Restores queued clips from the session state to the speaker's playback queue.
```csharp
public static int RestoreQueue(this ISpeaker speaker, SpeakerState state, AudioCache audioCache)
```

### 🔹 `ClearQueue()`
**Description:** Clears the playback queue for the specified physical speaker.
```csharp
public static bool ClearQueue(this ISpeaker speaker, SpeakerState state = null)
```

### 🔹 `static()`
**Description:** Retrieves the current queue status for the specified physical speaker.
```csharp
public static (int queuedCount, string currentClip) GetQueueStatus(this ISpeaker speaker, SpeakerState state = null)
```

### 🔹 `ValidateState()`
**Description:** Validates the abstract session state for consistency and correctness.
```csharp
public static bool ValidateState(this SpeakerState state)
```

### 🔹 `StartAutoStop()`
**Description:** Initiates a coroutine that automatically stops and fades out the physical speaker after a set lifespan.
```csharp
public static void StartAutoStop(this ISpeaker speaker, byte controllerId, float lifespan, bool autoCleanup, Action<byte> fadeOutAction)
```

---

## 📦 Class: SpeakerState

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

### 🔹 `UnknownMember()`
**Description:** A list of audio clips that are pending playback, represented as tuples: (<c>key</c>, <c>loop</c>).
```csharp
public List<(string key, bool loop)> QueuedClips { get; set; } = new List<(string key, bool loop)>();
```

### 🔹 `Persistent`
**Description:** Whether this session is flagged for persistence across physical evictions.
```csharp
public bool Persistent { get; set; }
```

### 🔹 `Lifespan`
**Description:** Optional time (in seconds) to live for this session. If set and <see cref="AutoCleanup"/> is true, the audio auto-fades and stops after this lifespan.
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

## 📦 Class: StaticSpeakerFactory

### 🔹 `Instance`
**Description:** Retrieves the shared <see cref="ISpeakerFactory"/> instance used natively by the API. Exposed strictly for initializing the router (<see cref="AudioManagerAPI.Features.Management.AudioManager"/>) or dependency injection. Plugins must never cast or use this to manually create/destroy physical speakers.
```csharp
public static ISpeakerFactory Instance => factory;
```

---

## 📦 Class: ApiLogger

---

