# LabApi.Extensions - Architecture API Registry

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

---

## 📦 Class: DefaultAudioManager

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

### 🔹 `GetTransmitter()`
**Description:** Gets a value indicating whether the speaker's audio queue is currently empty. Safely evaluates to true if the underlying transmitter does not exist. <value> <c>true</c> if there are no queued audio clips or no transmitter; otherwise, <c>false</c>. </value>
```csharp
public bool IsQueueEmpty => (SpeakerToy.GetTransmitter(speakerToy.ControllerId)?.AudioClipSamples.Count ?? 0) == 0;
```

### 🔹 `GetTransmitter()`
**Description:** Gets the number of audio clips currently queued for playback. <value> An integer representing the number of queued clips. Returns 0 if no transmitter is found. </value>
```csharp
public int QueuedClipsCount => SpeakerToy.GetTransmitter(speakerToy.ControllerId)?.AudioClipSamples.Count ?? 0;
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

### 🔹 `UnknownMethod()`
**Description:** A list of audio clips that are pending playback, represented as tuples: (<c>key</c>, <c>loop</c>).
```csharp
public List<(string key, bool loop)> QueuedClips { get; set; } = new List<(string key, bool loop)>();
```

### 🔹 `UnknownMethod()`
**Description:** A FIFO queue of PCM buffers waiting to be played by this session. Used for real-time audio streaming.
```csharp
public Queue<float[]> PcmQueue { get; } = new Queue<float[]>();
```

---

## 📦 Class: StaticSpeakerFactory

---

## 📦 Class: ApiLogger

---

