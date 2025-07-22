# SCPSL-AudioManagerAPI

[![NuGet Version](https://img.shields.io/nuget/v/SCPSL-AudioManagerAPI.svg)](https://www.nuget.org/packages/SCPSL-AudioManagerAPI/)  
A lightweight, reusable C# library for managing audio playback in SCP: Secret Laboratory (SCP:SL) plugins using LabAPI. It provides a robust system for loading, caching, and playing audio through `SpeakerToy` instances, with centralized controller ID management, advanced audio control (volume, range, spatialization, fading, queuing), and prioritization for enhanced gameplay.

> ⚠️ **Warning**  
> This plugin is currently in active development. New features are being added regularly, and not all functionality has been fully tested in live gameplay.  
> If you encounter any issues or bugs, please report them on the [official GitHub repository](https://github.com/iomatix/-SCPSL-AudioManagerAPI/issues).

## Features

- **Centralized Controller ID Management**: Uses `ControllerIdManager` to ensure unique speaker IDs (1-255) across plugins, with priority-based eviction and queuing for high-priority audio.
- **LRU Audio Caching**: Efficiently manages audio samples with lazy loading and least-recently-used (LRU) eviction via `AudioCache`.
- **Flexible Speaker Abstraction**: Supports custom speaker implementations through `ISpeaker`, `ISpeakerWithPlayerFilter`, and `ISpeakerFactory` interfaces.
- **Advanced Audio Control**: Configurable volume (0.0 to 1.0), minimum/maximum distance, spatialization (3D audio), and fade-in/fade-out for precise audio tuning.
- **Audio Queuing**: Supports queuing multiple audio clips with optional looping and smooth transitions via fade-in/fade-out.
- **Audio Prioritization**: Supports Low, Medium, and High priorities, with queuing for high-priority audio when IDs are limited.
- **Pause/Resume/Skip**: Pause, resume, or skip audio clips, including queued clips, for dynamic control.
- **Thread-Safe Operations**: Handles concurrent audio playback, caching, and ID allocation safely.
- **LabAPI Compatibility**: Optimized for SCP:SL, integrating seamlessly with `SpeakerToy` for spatial and global audio playback.
- **Default System**: Simplifies usage with `DefaultAudioManager`, offering plug-and-play methods for common audio tasks.

## Installation

Install the `SCPSL-AudioManagerAPI` package via NuGet:

```bash
dotnet add package SCPSL-AudioManagerAPI --version 1.1.0
```

Or, in Visual Studio, use the NuGet Package Manager to search for `SCPSL-AudioManagerAPI`.

## Project Setup

Add the `SCPSL-AudioManagerAPI` package to your SCP:SL plugin project. Ensure you reference `UnityEngine.CoreModule` for `Vector3`, `LabApi` for `SpeakerToy`, and `MEC` for coroutines (used for fading).

Example `.csproj` snippet:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="SCPSL-AudioManagerAPI" Version="1.1.0" />
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
DefaultAudioManager.FadeOut(id, 2f); // Fade out and stop
DefaultAudioManager.Stop(id); // Stop and destroy speaker
```

### 2. Implement ISpeaker and ISpeakerFactory (Advanced)

For custom speaker implementations, create a class compatible with LabAPI's `SpeakerToy`.

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

    public void Play(float[] samples, bool loop)
    {
        var transmitter = SpeakerToy.GetTransmitter(speakerToy.ControllerId);
        transmitter?.Play(samples, queue: false, loop: loop);
        SetVolume(targetVolume);
    }

    public void Queue(float[] samples, bool loop)
    {
        var transmitter = SpeakerToy.GetTransmitter(speakerToy.ControllerId);
        transmitter?.Play(samples, queue: true, loop: loop);
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

    public void FadeOut(float duration)
    {
        if (duration > 0)
        {
            Timing.RunCoroutine(FadeVolume(speakerToy.Volume, 0f, duration, stopOnComplete: true));
        }
    }

    public void SetValidPlayers(Func<Player, bool> playerFilter)
    {
        var transmitter = SpeakerToy.GetTransmitter(speakerToy.ControllerId);
        if (transmitter != null)
        {
            transmitter.ValidPlayers = playerFilter as Func<Player, bool>;
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

    private IEnumerator<float> FadeVolume(float startVolume, float endVolume, float duration, bool stopOnComplete = false)
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

### 3. Initialize AudioManager (Advanced)

Create an instance of `AudioManager` for advanced control.

```csharp
using AudioManagerAPI;
using AudioManagerAPI.Features.Management;
using System;
using System.Reflection;

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
                if (speaker is LabApiSpeaker labSpeaker)
                {
                    labSpeaker.SetValidPlayers(p => p == targetPlayer);
                }
            });
        if (audioManager.IsValidController(controllerId))
        {
            audioManager.FadeInAudio(controllerId, 1f); // Fade in over 1 second
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
            Log.Info($"Played global scream with controller ID {controllerId}.");
        }
    }
}
```

### 4. Manage Speakers

Control audio playback with advanced features.

```csharp
// Pause and resume
audioManager.PauseAudio(controllerId);
audioManager.ResumeAudio(controllerId);

// Skip clips
audioManager.SkipAudio(controllerId, 1); // Skip current clip

// Fade in/out
audioManager.FadeInAudio(controllerId, 2f); // Fade in over 2 seconds
audioManager.FadeOutAudio(controllerId, 2f); // Fade out and stop over 2 seconds

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

## Audio Control and Prioritization

- **Volume**: Set between 0.0 (mute) and 1.0 (full volume) to control loudness (`SetVolume`).
- **MinDistance/MaxDistance**: Define the range where audio starts to fall off and drops to zero, in Unity units (`SetMinDistance`, `SetMaxDistance`).
- **Spatialization**: Enable/disable 3D audio (`SetSpatialization`) for positional or ambient effects. Non-spatial audio uses `Vector3.zero` for global playback.
- **Priority**: Use `AudioPriority` (Low, Medium, High) to prioritize critical sounds. High-priority audio can evict lower-priority speakers or be queued if IDs are unavailable.
- **Fading**: Smoothly transition volume with `FadeIn` and `FadeOut` for immersive effects.
- **Queuing**: Queue multiple clips to play sequentially, with optional looping and fade transitions.

## API Reference

### Key Classes and Interfaces

| Name                     | Namespace                             | Description                                                                 |
|--------------------------|---------------------------------------|-----------------------------------------------------------------------------|
| `IAudioManager`          | `AudioManagerAPI.Features.Management` | Defines the contract for audio playback and speaker lifecycle management.    |
| `AudioManager`           | `AudioManagerAPI.Features.Management` | Implements audio management with caching and shared controller IDs.          |
| `ISpeaker`               | `AudioManagerAPI.Features.Speakers`   | Represents a speaker for playing, queuing, pausing, resuming, skipping, and fading audio. |
| `ISpeakerWithPlayerFilter` | `AudioManagerAPI.Features.Speakers`   | Extends `ISpeaker` to support player-specific audibility, volume, range, and spatialization. |
| `ISpeakerFactory`        | `AudioManagerAPI.Features.Speakers`   | Defines a factory for creating speaker instances.                            |
| `AudioCache`             | `AudioManagerAPI.Cache`               | Manages audio samples with LRU eviction and lazy loading.                    |
| `ControllerIdManager`     | `AudioManagerAPI`                    | Static class for managing unique controller IDs with priority-based eviction and queuing. |
| `AudioPriority`          | `AudioManagerAPI`                    | Enum defining audio priority levels (Low, Medium, High).                     |
| `DefaultAudioManager`    | `AudioManagerAPI.Defaults`            | Simplifies audio management with default settings and convenience methods.   |
| `DefaultSpeakerToyAdapter` | `AudioManagerAPI.Defaults`            | Default LabAPI `SpeakerToy` adapter with full feature support.               |
| `DefaultSpeakerFactory`  | `AudioManagerAPI.Defaults`            | Creates `DefaultSpeakerToyAdapter` instances for default usage.              |

### Important Methods

- **`IAudioManager.RegisterAudio(string key, Func<Stream> streamProvider)`**: Registers a WAV stream for lazy loading.
- **`IAudioManager.PlayAudio(string key, Vector3 position, bool loop, float volume, float minDistance, float maxDistance, bool isSpatial, AudioPriority priority, Action<ISpeaker> configureSpeaker, bool queue)`**: Plays or queues audio with optional configuration.
- **`IAudioManager.PlayGlobalAudio(string key, bool loop, float volume, AudioPriority priority, bool queue)`**: Plays or queues audio globally, audible to all players.
- **`IAudioManager.PauseAudio(byte controllerId)`**: Pauses audio playback.
- **`IAudioManager.ResumeAudio(byte controllerId)`**: Resumes paused audio.
- **`IAudioManager.SkipAudio(byte controllerId, int count)`**: Skips the current or queued clips.
- **`IAudioManager.FadeInAudio(byte controllerId, float duration)`**: Fades in audio volume.
- **`IAudioManager.FadeOutAudio(byte controllerId, float duration)`**: Fades out and stops audio.
- **`IAudioManager.StopAudio(byte controllerId)`**: Stops audio playback.
- **`IAudioManager.DestroySpeaker(byte controllerId)`**: Destroys a speaker and releases its ID.
- **`IAudioManager.CleanupAllSpeakers()`**: Cleans up all active speakers and releases their IDs.
- **`IAudioManager.GetSpeaker(byte controllerId)`**: Retrieves a speaker instance for further configuration.

## Notes

- **Controller ID Synchronization**: `ControllerIdManager` ensures no ID conflicts by maintaining a shared pool of IDs (1-255). High-priority audio can evict lower-priority speakers or be queued for later allocation.
- **Thread Safety**: All operations (ID allocation, caching, speaker management) are thread-safe using locks.
- **Dependencies**: Requires `UnityEngine.CoreModule`, `LabApi`, and `MEC`. Ensure these are available in your SCP:SL environment.
- **Logging**: Use your plugin’s logging system (e.g., Exiled’s `Log`) for debugging playback, prioritization, or resource errors.
- **Spatial Audio**: Use `isSpatial: true` for positional effects (e.g., screams) and `isSpatial: false` for ambient sounds (e.g., background music). Non-spatial audio defaults to `Vector3.zero` for global playback.
- **Fading and Queuing**: Use `FadeIn`/`FadeOut` for smooth transitions and `queue: true` to play multiple clips sequentially.

## Contributing

Contributions are welcome! Please submit issues or pull requests to the [GitHub repository](https://github.com/ioMatix/SCPSL-AudioManagerAPI).

## License

This project is licensed under the GNU Lesser General Public License v3.0 (LGPL3). See the [LICENSE](LICENSE) file for details.