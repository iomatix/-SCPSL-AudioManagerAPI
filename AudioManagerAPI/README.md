```markdown
# SCPSL-AudioManagerAPI

[![NuGet Version](https://img.shields.io/nuget/v/SCPSL-AudioManagerAPI.svg)](https://www.nuget.org/packages/SCPSL-AudioManagerAPI/)  
A lightweight, reusable C# library for managing audio playback in SCP: Secret Laboratory (SCP:SL) plugins using LabAPI. It provides a robust system for loading, caching, and playing audio through `SpeakerToy` instances, with centralized controller ID management, advanced audio control (volume, position, range, spatialization, fading, queuing), and prioritization for enhanced gameplay.

> ⚠️ **Warning**  
> This plugin is currently in active development. New features are being added regularly, and not all functionality has been fully tested in live gameplay.  
> If you encounter any issues or bugs, please report them on the [official GitHub repository](https://github.com/iomatix/-SCPSL-AudioManagerAPI/issues).

## Features

- **Centralized Controller ID Management**: Uses `ControllerIdManager` to ensure unique speaker IDs (1-255) across plugins, with priority-based eviction and queuing for high-priority audio.
- **LRU Audio Caching**: Efficiently manages audio samples with lazy loading and least-recently-used (LRU) eviction via `AudioCache`.
- **Flexible Speaker Abstraction**: Supports custom speaker implementations through `ISpeaker`, `ISpeakerWithPlayerFilter`, and `ISpeakerFactory` interfaces.
- **Advanced Audio Control**: Configurable volume (0.0 to 1.0), position, minimum/maximum distance, spatialization (3D audio), and fade-in/fade-out for precise audio tuning.
- **Audio Queuing**: Supports queuing multiple audio clips with optional looping, smooth transitions via fade-in/fade-out, and queue management (clear, status).
- **Audio Prioritization**: Supports Low, Medium, and High priorities, with queuing for high-priority audio when IDs are limited.
- **Pause/Resume/Skip**: Pause, resume, or skip audio clips, including queued clips, for dynamic control.
- **Persistent Speakers**: Save and recover speaker states (position, volume, queued clips, playback position) for seamless recovery after eviction or scene reloads.
- **Thread-Safe Operations**: Handles concurrent audio playback, caching, and ID allocation safely.
- **LabAPI Compatibility**: Optimized for SCP:SL, integrating seamlessly with `SpeakerToy` for spatial and global audio playback.
- **Default System**: Simplifies usage with `DefaultAudioManager`, offering plug-and-play methods for common audio tasks.

## Installation

Install the `SCPSL-AudioManagerAPI` package via NuGet:

```bash
dotnet add package SCPSL-AudioManagerAPI --version 1.3.0
```

Or, in Visual Studio, use the NuGet Package Manager to search for `SCPSL-AudioManagerAPI`.

## Project Setup

Add the `SCPSL-AudioManagerAPI` package to your SCP:SL plugin project. Ensure you reference `UnityEngine.CoreModule` for `Vector3`, `LabApi` for `SpeakerToy`, and `MEC` for coroutines (used for fading and lifespan management).

Example `.csproj` snippet:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="SCPSL-AudioManagerAPI" Version="1.3.0" />
    <Reference Include="LabApi">
      <HintPath>path\to\LabApi.dll</HintPath>
    </Reference>
    <Reference Include="UnityEngine.CoreModule">
      <HintPath>path\to\UnityEngine.CoreModule.dll</HintPath>
    </Reference>
    <Reference Include="MEC">
      <HintPath>path\to\MEC.dll</HintPath>
    </Reference>
  </ItemGroup>
</Project>
```

## Usage

### 1. Using DefaultAudioManager (Recommended)

The `DefaultAudioManager` provides a plug-and-play interface for common audio tasks, ideal for most SCP:SL plugins.

```csharp
using AudioManagerAPI.Defaults;
using UnityEngine;

// At plugin startup
DefaultAudioManager.RegisterDefaults(cacheSize: 50);

// Register audio
DefaultAudioManager.RegisterAudio("explosionSound", () => Assembly.GetExecutingAssembly().GetManifestResourceStream("MyPlugin.Audio.explosion.wav"));

// Play audio globally with default settings (non-spatial, full volume, no looping)
byte id = DefaultAudioManager.Play("explosionSound");

// Play and queue another clip
DefaultAudioManager.Play("screamSound", queue: true);

// Control playback
DefaultAudioManager.FadeIn(id, 2f); // Fade in over 2 seconds
DefaultAudioManager.Pause(id);
DefaultAudioManager.Resume(id);
DefaultAudioManager.Skip(id, 1); // Skip current clip
DefaultAudioManager.SetVolume(id, 0.5f); // Adjust volume
DefaultAudioManager.SetPosition(id, new Vector3(10f, 0f, 0f)); // Move speaker
DefaultAudioManager.ClearQueue(id); // Clear queued clips
DefaultAudioManager.FadeOut(id, 2f); // Fade out and stop
DefaultAudioManager.Stop(id); // Stop and destroy speaker
```

### 2. Extension Methods

The `SpeakerExtensions` class provides extension methods for `ISpeaker` instances, allowing for easy configuration and lifecycle management.

#### Configure

Configures volume, position, range, spatialization, and player filters.

```csharp
speaker.Configure(volume: 0.8f, minDistance: 5f, maxDistance: 50f, isSpatial: true, configureSpeaker: customConfig, playerFilter: p => Player.ReadyList.Contains(p));
```

#### SetVolume

Dynamically adjusts the speaker’s volume.

```csharp
speaker.SetVolume(0.5f);
```

#### SetPosition

Dynamically moves the speaker to a new position.

```csharp
speaker.SetPosition(new Vector3(10f, 0f, 0f));
```

#### StartAutoStop

Initiates a coroutine to automatically stop and fade out the speaker after a specified lifespan.

```csharp
speaker.StartAutoStop(controllerId: 1, lifespan: 10f, autoCleanup: true, fadeOutAction: id => audioManager.FadeOutAudio(id, 2f));
```

#### ClearQueue

Clears the speaker’s playback queue without stopping the current clip.

```csharp
speaker.ClearQueue(state);
```

#### GetQueueStatus

Retrieves the number of queued clips and the current clip key (for persistent speakers).

```csharp
var (queuedCount, currentClip) = speaker.GetQueueStatus(state);
```

#### ValidateState

Validates a persistent speaker’s state for consistency.

```csharp
bool isValid = state.ValidateState();
```

### 3. Implement ISpeaker and ISpeakerFactory (Advanced)

For custom speaker implementations, create a class compatible with LabAPI's `SpeakerToy`. Example:

```csharp
using AudioManagerAPI.Features.Speakers;
using LabApi.Features.Wrappers;
using UnityEngine;

public class LabApiSpeaker : ISpeakerWithPlayerFilter
{
    private readonly SpeakerToy speakerToy;
    private float targetVolume;

    public LabApiSpeaker(SpeakerToy speakerToy)
    {
        this.speakerToy = speakerToy ?? throw new ArgumentNullException(nameof(speakerToy));
        targetVolume = 1f;
    }

    public Func<Player, bool> ValidPlayers
    {
        get => SpeakerToy.GetTransmitter(speakerToy.ControllerId)?.ValidPlayers;
        set => SpeakerToy.GetTransmitter(speakerToy.ControllerId).ValidPlayers = value;
    }

    public void Play(float[] samples, bool loop, float playbackPosition = 0f)
    {
        var transmitter = SpeakerToy.GetTransmitter(speakerToy.ControllerId);
        if (transmitter != null)
        {
            if (playbackPosition > 0f && samples != null)
            {
                int skipSamples = Mathf.FloorToInt(playbackPosition * AudioTransmitter.SampleRate);
                if (skipSamples >= samples.Length)
                {
                    Log.Warn($"[LabApiSpeaker] Playback position {playbackPosition}s exceeds clip length.");
                    return;
                }
                float[] trimmedSamples = new float[samples.Length - skipSamples];
                Array.Copy(samples, skipSamples, trimmedSamples, 0, trimmedSamples.Length);
                transmitter.Play(trimmedSamples, queue: false, loop: loop);
            }
            else
            {
                transmitter.Play(samples, queue: false, loop: loop);
            }
            SetVolume(targetVolume);
        }
    }

    public void Queue(float[] samples, bool loop)
    {
        var transmitter = SpeakerToy.GetTransmitter(speakerToy.ControllerId);
        transmitter?.Play(samples, queue: true, loop: loop);
    }

    public void ClearQueue()
    {
        var transmitter = SpeakerToy.GetTransmitter(speakerToy.ControllerId);
        transmitter?.AudioClipSamples.Clear();
    }

    public void Stop()
    {
        var transmitter = SpeakerToy.GetTransmitter(speakerToy.ControllerId);
        transmitter?.Stop();
    }

    public void Destroy()
    {
        speakerToy.Destroy();
    }

    public void Pause()
    {
        var transmitter = SpeakerToy.GetTransmitter(speakerToy.ControllerId);
        transmitter?.Pause();
    }

    public void Resume()
    {
        var transmitter = SpeakerToy.GetTransmitter(speakerToy.ControllerId);
        transmitter?.Resume();
    }

    public void Skip(int count)
    {
        var transmitter = SpeakerToy.GetTransmitter(speakerToy.ControllerId);
        transmitter?.Skip(count);
    }

    public void FadeIn(float duration)
    {
        if (duration > 0)
        {
            Timing.RunCoroutine(FadeVolume(0f, targetVolume, duration));
        }
    }

    public void FadeOut(float duration, Action onComplete = null)
    {
        if (duration > 0)
        {
            Timing.RunCoroutine(FadeVolume(speakerToy.Volume, 0f, duration, stopOnComplete: true, onComplete));
        }
        else
        {
            Stop();
            onComplete?.Invoke();
        }
    }

    public float GetPlaybackPosition()
    {
        var transmitter = SpeakerToy.GetTransmitter(speakerToy.ControllerId);
        return transmitter != null ? transmitter.CurrentPosition / (float)AudioTransmitter.SampleRate : 0f;
    }

    public void SetValidPlayers(Func<Player, bool> playerFilter)
    {
        var transmitter = SpeakerToy.GetTransmitter(speakerToy.ControllerId);
        if (transmitter != null)
        {
            transmitter.ValidPlayers = playerFilter;
        }
    }

    public void SetVolume(float volume)
    {
        speakerToy.Volume = Mathf.Clamp01(volume);
        targetVolume = speakerToy.Volume;
    }

    public void SetMinDistance(float minDistance)
    {
        speakerToy.MinDistance = Mathf.Max(0, minDistance);
    }

    public void SetMaxDistance(float maxDistance)
    {
        speakerToy.MaxDistance = Mathf.Max(0, maxDistance);
    }

    public void SetSpatialization(bool isSpatial)
    {
        speakerToy.IsSpatial = isSpatial;
    }

    public void SetPosition(Vector3 position)
    {
        speakerToy.Position = position;
    }

    private IEnumerator<float> FadeVolume(float startVolume, float endVolume, float duration, bool stopOnComplete = false, Action onComplete = null)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Timing.DeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            SetVolume(Mathf.Lerp(startVolume, endVolume, t));
            yield return Timing.WaitForOneFrame;
        }
        SetVolume(endVolume);
        if (stopOnComplete)
        {
            Stop();
        }
        onComplete?.Invoke();
    }
}

public class LabApiSpeakerFactory : ISpeakerFactory
{
    public ISpeaker CreateSpeaker(Vector3 position, byte controllerId)
    {
        SpeakerToy speaker = SpeakerToy.Create(position, networkSpawn: true);
        if (speaker == null) return null;
        speaker.ControllerId = controllerId;
        return new LabApiSpeaker(speaker);
    }
}
```

### 4. Initialize AudioManager (Advanced)

Create an instance of `AudioManager` for advanced control.

```csharp
using AudioManagerAPI;
using AudioManagerAPI.Features.Management;
using System;
using System.Reflection;
using UnityEngine;

public class MyPluginAudioManager
{
    private readonly IAudioManager audioManager;

    public MyPluginAudioManager()
    {
        audioManager = new AudioManager(new LabApiSpeakerFactory(), cacheSize: 20);
        RegisterAudioResources();
    }

    private void RegisterAudioResources()
    {
        var assembly = Assembly.GetExecutingAssembly();
        audioManager.RegisterAudio("myplugin.scream", () => 
            assembly.GetManifestResourceStream("MyPlugin.Audio.scream.wav"));
    }

    public void PlayScream(Vector3 position, Player targetPlayer)
    {
        byte controllerId = audioManager.PlayAudio("myplugin.scream", position, false, 
            volume: 0.8f, minDistance: 5f, maxDistance: 50f, isSpatial: true, priority: AudioPriority.High, speaker =>
            {
                if (speaker is ISpeakerWithPlayerFilter filterSpeaker)
                {
                    filterSpeaker.SetValidPlayers(p => p == targetPlayer);
                }
            }, queue: false, persistent: true);
        if (audioManager.IsValidController(controllerId))
        {
            audioManager.FadeInAudio(controllerId, 1f);
            audioManager.SetSpeakerPosition(controllerId, new Vector3(position.x + 5f, position.y, position.z));
            Log.Info($"Played scream with controller ID {controllerId}.");
        }
    }

    public void PlayGlobalScream(Vector3 position)
    {
        byte controllerId = audioManager.PlayGlobalAudio("myplugin.scream", false, 
            volume: 0.8f, priority: AudioPriority.High, queue: true);
        if (audioManager.IsValidController(controllerId))
        {
            audioManager.FadeInAudio(controllerId, 1f);
            audioManager.ClearSpeakerQueue(controllerId);
            Log.Info($"Played global scream with controller ID {controllerId}.");
        }
    }
}
```

### 5. Manage Speakers

Control audio playback with advanced features.

```csharp
// Pause and resume
audioManager.PauseAudio(controllerId);
audioManager.ResumeAudio(controllerId);

// Skip clips
audioManager.SkipAudio(controllerId, 1);

// Fade in/out
audioManager.FadeInAudio(controllerId, 2f);
audioManager.FadeOutAudio(controllerId, 2f);

// Adjust volume and position
audioManager.SetSpeakerVolume(controllerId, 0.5f);
audioManager.SetSpeakerPosition(controllerId, new Vector3(10f, 0f, 0f));

// Queue management
var (queuedCount, currentClip) = audioManager.GetQueueStatus(controllerId);
audioManager.ClearSpeakerQueue(controllerId);

// Stop and destroy
audioManager.DestroySpeaker(controllerId);

// Cleanup all speakers
audioManager.CleanupAllSpeakers();
```

## Audio Requirements

The `AudioCache` class processes WAV files with the following specifications:
- **Format**: 48kHz, Mono, Signed 16-bit PCM.
- **Header**: Expects a standard WAV header; skips the first 44 bytes during loading.
- **Recommendation**: Prefix audio keys with your plugin’s namespace (e.g., `myplugin.scream`) to avoid conflicts with other plugins.

> **Note**: The API has been tested with this configuration, and audio files play correctly. Ensure your WAV files adhere to these specifications for compatibility.

## Audio Control and Prioritization

- **Volume**: Set between 0.0 (mute) and 1.0 (full volume) to control loudness (`SetSpeakerVolume`).
- **Position**: Dynamically adjust the speaker’s 3D world-space position (`SetSpeakerPosition`).
- **MinDistance/MaxDistance**: Define the range where audio starts to fall off and drops to zero, in Unity units (`SetMinDistance`, `SetMaxDistance`).
- **Spatialization**: Enable/disable 3D audio (`SetSpatialization`) for positional or ambient effects. Non-spatial audio uses `Vector3.zero` for global playback.
- **Priority**: Use `AudioPriority` (Low, Medium, High) to prioritize critical sounds. High-priority audio can evict lower-priority speakers or be queued if IDs are unavailable.
- **Fading**: Smoothly transition volume with `FadeInAudio` and `FadeOutAudio` for immersive effects.
- **Queuing**: Queue multiple clips to play sequentially, with optional looping, clearing (`ClearSpeakerQueue`), and status checking (`GetQueueStatus`).
- **Persistence**: Use `persistent: true` to retain speaker state (position, volume, queued clips, playback position) for recovery via `RecoverSpeaker`.

> **Note**: `GetQueueStatus` returns the current clip key only for persistent speakers (via `SpeakerState.QueuedClips`). For non-persistent speakers, the current clip key is `null`.

## API Reference

### Key Classes and Interfaces

| Name                     | Namespace                             | Description                                                                 |
|--------------------------|---------------------------------------|-----------------------------------------------------------------------------|
| `IAudioManager`          | `AudioManagerAPI.Features.Management` | Defines the contract for audio playback and speaker lifecycle management.    |
| `AudioManager`           | `AudioManagerAPI.Features.Management` | Implements audio management with caching and shared controller IDs.          |
| `ISpeaker`               | `AudioManagerAPI.Features.Speakers`   | Represents a speaker for playing, queuing, pausing, resuming, skipping, and fading audio. |
| `ISpeakerWithPlayerFilter` | `AudioManagerAPI.Features.Speakers`   | Extends `ISpeaker` to support player-specific audibility, volume, position, range, and spatialization. |
| `ISpeakerFactory`        | `AudioManagerAPI.Features.Speakers`   | Defines a factory for creating speaker instances.                            |
| `AudioCache`             | `AudioManagerAPI.Cache`               | Manages audio samples with LRU eviction and lazy loading.                    |
| `ControllerIdManager`    | `AudioManagerAPI.Controllers`         | Static class for managing unique controller IDs with priority-based eviction and queuing. |
| `AudioPriority`          | `AudioManagerAPI.Features.Enums`      | Enum defining audio priority levels (Low, Medium, High).                     |
| `DefaultAudioManager`    | `AudioManagerAPI.Defaults`            | Simplifies audio management with default settings and convenience methods.   |
| `DefaultSpeakerToyAdapter` | `AudioManagerAPI.Defaults`          | Default LabAPI `SpeakerToy` adapter with full feature support.               |
| `DefaultSpeakerFactory`  | `AudioManagerAPI.Defaults`            | Creates `DefaultSpeakerToyAdapter` instances for default usage.              |
| `SpeakerState`           | `AudioManagerAPI.Speakers.State`      | Stores persistent speaker state for recovery (position, volume, queued clips). |

### Important Methods

- **`IAudioManager.RegisterAudio(string key, Func<Stream> streamProvider)`**: Registers a WAV stream for lazy loading.
- **`IAudioManager.PlayAudio(string key, Vector3 position, bool loop, float volume, float minDistance, float maxDistance, bool isSpatial, AudioPriority priority, Action<ISpeaker> configureSpeaker, bool queue, bool persistent, float? lifespan, bool autoCleanup)`**: Plays or queues audio with optional configuration.
- **`IAudioManager.PlayGlobalAudio(string key, bool loop, float volume, AudioPriority priority, bool queue, float fadeInDuration, bool persistent, float? lifespan, bool autoCleanup)`**: Plays or queues audio globally, audible to all players.
- **`IAudioManager.SetSpeakerVolume(byte controllerId, float volume)`**: Dynamically adjusts the speaker’s volume.
- **`IAudioManager.SetSpeakerPosition(byte controllerId, Vector3 position)`**: Dynamically moves the speaker to a new position.
- **`IAudioManager.RecoverSpeaker(byte controllerId, bool resetPlayback)`**: Recovers a persistent speaker with saved state.
- **`IAudioManager.PauseAudio(byte controllerId)`**: Pauses audio playback.
- **`IAudioManager.ResumeAudio(byte controllerId)`**: Resumes paused audio.
- **`IAudioManager.SkipAudio(byte controllerId, int count)`**: Skips the current or queued clips.
- **`IAudioManager.FadeInAudio(byte controllerId, float duration)`**: Fades in audio volume.
- **`IAudioManager.FadeOutAudio(byte controllerId, float duration)`**: Fades out and stops audio.
- **`IAudioManager.GetQueueStatus(byte controllerId)`**: Retrieves the number of queued clips and current clip key (persistent speakers only).
- **`IAudioManager.ClearSpeakerQueue(byte controllerId)`**: Clears the speaker’s playback queue.
- **`IAudioManager.StopAudio(byte controllerId)`**: Stops audio playback.
- **`IAudioManager.DestroySpeaker(byte controllerId, bool forceRemoveState)`**: Destroys a speaker and releases its ID.
- **`IAudioManager.CleanupAllSpeakers()`**: Cleans up all active speakers and releases their IDs.
- **`SpeakerExtensions.Configure(ISpeaker, float volume, float minDistance, float maxDistance, bool isSpatial, Action<ISpeaker> configureSpeaker, Func<Player, bool> playerFilter)`**: Configures speaker settings.
- **`SpeakerExtensions.SetVolume(ISpeaker, float volume)`**: Sets the speaker’s volume.
- **`SpeakerExtensions.SetPosition(ISpeaker, Vector3 position)`**: Sets the speaker’s position.
- **`SpeakerExtensions.ClearQueue(ISpeaker, SpeakerState)`**: Clears the speaker’s queue.
- **`SpeakerExtensions.GetQueueStatus(ISpeaker, SpeakerState)`**: Retrieves queue status.
- **`SpeakerExtensions.ValidateState(SpeakerState)`**: Validates persistent speaker state.
- **`SpeakerExtensions.StartAutoStop(ISpeaker, byte controllerId, float lifespan, bool autoCleanup, Action<byte> fadeOutAction)`**: Initiates automatic speaker cleanup.

### Events

The `IAudioManager` interface defines several events for tracking audio state changes:

- **`OnPlaybackStarted`**: Invoked when a speaker begins playback.
- **`OnPaused`**: Raised when playback is paused for a given controller ID.
- **`OnResumed`**: Raised when previously paused audio resumes playback.
- **`OnStop`**: Raised when audio playback is explicitly stopped for a given controller ID.
- **`OnSkipped`**: Raised when audio skip logic is invoked for a specified controller ID.
- **`OnQueueEmpty`**: Triggered when the audio queue for a speaker becomes empty.

These events can be used to synchronize UI elements, manage state persistence, or trigger custom logic based on audio events.

## Notes

- **Controller ID Synchronization**: `ControllerIdManager` ensures no ID conflicts by maintaining a shared pool of IDs (1-255). High-priority audio can evict lower-priority speakers or be queued for later allocation.
- **Thread Safety**: All operations (ID allocation, caching, speaker management) are thread-safe using locks.
- **Dependencies**: Requires `UnityEngine.CoreModule`, `LabApi`, and `MEC`. Ensure these are available in your SCP:SL environment.
- **Logging**: Uses `LabApi.Features.Console.Logger` for debugging. Integrate with your plugin’s logging system (e.g., Exiled’s `Log`) for additional context.
- **Spatial Audio**: Use `isSpatial: true` for positional effects (e.g., screams) and `isSpatial: false` for ambient sounds (e.g., background music). Non-spatial audio defaults to `Vector3.zero` for global playback.
- **Fading and Queuing**: Use `FadeInAudio`/`FadeOutAudio` for smooth transitions, `ClearSpeakerQueue` to reset queues, and `GetQueueStatus` to monitor queue state.
- **Persistent Speakers**: Use `persistent: true` to retain speaker state (position, volume, queued clips, playback position) for recovery via `RecoverSpeaker`, validated by `ValidateState`.
- **Playback Position**: For persistent speakers, playback position is approximated by trimming samples, as `AudioTransmitter` does not natively support seeking.

## Contributing

Contributions are welcome! Please submit issues or pull requests to the [GitHub repository](https://github.com/ioMatix/-SCPSL-AudioManagerAPI).

## License

This project is licensed under the GNU Lesser General Public License v3.0 (LGPL3). See the [LICENSE](LICENSE) file for details.
```