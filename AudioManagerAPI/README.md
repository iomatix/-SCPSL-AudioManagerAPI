# SCPSL-AudioManagerAPI

## Description

`SCPSL-AudioManagerAPI` is a robust library for managing spatial and global audio in SCP: Secret Laboratory server-side plugins. Built on top of LabAPI’s `SpeakerToy`, it provides a high-level interface for playing audio clips, managing speaker lifecycles, and applying player-specific filters. With thread-safe operations and integration with `ControllerIdManager`, it ensures seamless audio playback across SCP:SL’s multi-threaded environment. The library supports both simple and advanced use cases, from ambient sounds to role-specific announcements, without requiring Exiled dependencies.

## Features

- **Spatial Audio**: Play audio at specific world positions with customizable volume, range, and spatialization.
- **Global Audio**: Broadcast audio to all players, ideal for announcements or events.
- **Player Filters**: Restrict audio to specific players based on role, zone, or room using `ISpeakerWithPlayerFilter`.
- **Thread-Safe Management**: Handles concurrent audio operations with a shared, thread-safe speaker registry.
- **Controller ID System**: Integrates with `ControllerIdManager` for unique speaker IDs (1-255) across plugins.
- **Audio Caching**: Uses `AudioCache` with LRU eviction for efficient audio sample management.
- **Flexible Factories**: Offers `StaticSpeakerFactory` for simple static access and `DefaultSpeakerFactory` for advanced control.
- **Persistence**: Supports persistent speaker states for recovery after interruptions.
- **Priority and Queuing**: Manages audio priorities (`AudioPriority`) and queues for smooth playback.

## Installation

Install the `SCPSL-AudioManagerAPI` package via NuGet:

```bash
dotnet add package SCPSL-AudioManagerAPI --version 1.4.2
```

Ensure you have the following dependencies in your SCP:SL plugin project:
- `LabApi` (version 1.1.0 or compatible)
- `UnityEngine.CoreModule`
- `MEC` (for coroutines)

## Project Setup

Add the `SCPSL-AudioManagerAPI` package to your SCP:SL plugin project. Ensure you reference `UnityEngine.CoreModule` for `Vector3`, `LabApi` for `SpeakerToy` and `Player`, and `MEC` for coroutines (used for fading and lifespan management).

Example `.csproj` snippet:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="SCPSL-AudioManagerAPI" Version="1.4.2" />
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

Replace `path\to\` with the actual paths to the dependency DLLs in your SCP:SL server installation (e.g., `SCPSL_Data\Managed`).

## Usage

### 1. Using DefaultAudioManager (Recommended)

The `DefaultAudioManager` provides a plug-and-play interface for common audio tasks, ideal for most SCP:SL plugins.

```csharp
using AudioManagerAPI.Defaults;
using UnityEngine;
using LabApi.Features.Console;

public class AudioPlugin
{
    public void Initialize()
    {
        DefaultAudioManager.RegisterDefaults(cacheSize: 50);
        DefaultAudioManager.RegisterAudio("ambientSound", () => Assembly.GetExecutingAssembly().GetManifestResourceStream("MyPlugin.Audio.ambient.wav"));
    }

    public void PlayAudio()
    {
        byte id = DefaultAudioManager.Play("ambientSound");
        DefaultAudioManager.Play("announcement", queue: true);
        DefaultAudioManager.FadeIn(id, 2f);
        DefaultAudioManager.Pause(id);
        DefaultAudioManager.Resume(id);
        DefaultAudioManager.Skip(id, 1);
        DefaultAudioManager.SetVolume(id, 0.5f);
        DefaultAudioManager.SetPosition(id, new Vector3(10f, 0f, 0f));
        DefaultAudioManager.ClearQueue(id);
        DefaultAudioManager.FadeOut(id, 2f);
        DefaultAudioManager.Stop(id);
    }
}
```

### 2. Using StaticSpeakerFactory (Recommended for Simple Speaker Management)

The `StaticSpeakerFactory` in the `AudioManagerAPI.Features.Static` namespace provides a static interface for managing `DefaultSpeakerToyAdapter` instances. It leverages a shared, thread-safe speaker registry aligned with the global `ControllerIdManager`, ideal for simple audio playback scenarios.

```csharp
using AudioManagerAPI.Features.Static;
using AudioManagerAPI.Features.Speakers;
using UnityEngine;
using LabApi.Features.Console;
using LabApi.Player;

public class AudioPlugin
{
    public void PlayZoneSound(Vector3 position, string zoneName)
    {
        byte controllerId = 1;
        ISpeaker speaker = StaticSpeakerFactory.GetSpeaker(controllerId) ?? StaticSpeakerFactory.CreateSpeaker(position, controllerId);

        if (speaker != null)
        {
            float[] audioSamples = GetAudioSamples(); // Assume method to get PCM samples
            speaker.Play(audioSamples, loop: false);
            speaker.SetVolume(0.8f);
            speaker.SetSpatialization(true);
            speaker.SetMaxDistance(20f);
            if (speaker is ISpeakerWithPlayerFilter filterSpeaker)
            {
                filterSpeaker.SetValidPlayers(p => p.CurrentRoom?.Zone == zoneName);
            }
            Logger.Info($"[AudioPlugin] PlayZoneSound: Playing sound with controller ID {controllerId} in zone {zoneName}.");
        }
        else
        {
            Logger.Warn($"[AudioPlugin] PlayZoneSound: Failed to get or create speaker for controller ID {controllerId}.");
        }
    }

    public void RemoveSpeaker(byte controllerId)
    {
        if (StaticSpeakerFactory.RemoveSpeaker(controllerId))
        {
            Logger.Info($"[AudioPlugin] RemoveSpeaker: Removed speaker for controller ID {controllerId}.");
        }
        else
        {
            Logger.Warn($"[AudioPlugin] RemoveSpeaker: No speaker found for controller ID {controllerId}.");
        }
    }

    public void OnRoundEnded()
    {
        StaticSpeakerFactory.ClearSpeakers();
        Logger.Info($"[AudioPlugin] OnRoundEnded: Cleared all speakers for round restart.");
    }
}
```

### 3. Using DefaultSpeakerFactory (Advanced)

The `DefaultSpeakerFactory` in the `AudioManagerAPI.Defaults` namespace provides an instantiable factory for advanced use cases, compatible with `AudioManager`. Use `StaticSpeakerFactory` for simpler, static access.

```csharp
using AudioManagerAPI.Defaults;
using AudioManagerAPI.Features.Speakers;
using UnityEngine;
using LabApi.Features.Console;
using LabApi.Player;

public class AdvancedAudioPlugin
{
    private readonly ISpeakerFactory speakerFactory;

    public AdvancedAudioPlugin()
    {
        speakerFactory = new DefaultSpeakerFactory();
    }

    public void PlayRoleBasedSound(Vector3 position, string roleType)
    {
        byte controllerId = 1;
        ISpeaker speaker = speakerFactory.GetSpeaker(controllerId) ?? speakerFactory.CreateSpeaker(position, controllerId);

        if (speaker != null)
        {
            float[] audioSamples = GetAudioSamples(); // Assume method to get PCM samples
            speaker.Play(audioSamples, loop: false);
            speaker.SetVolume(0.8f);
            speaker.SetSpatialization(true);
            speaker.SetMaxDistance(20f);
            if (speaker is ISpeakerWithPlayerFilter filterSpeaker)
            {
                filterSpeaker.SetValidPlayers(p => p.Role.ToString() == roleType);
            }
            Logger.Info($"[AdvancedAudioPlugin] PlayRoleBasedSound: Playing sound with controller ID {controllerId} for role {roleType}.");
        }
        else
        {
            Logger.Warn($"[AdvancedAudioPlugin] PlayRoleBasedSound: Failed to get or create speaker for controller ID {controllerId}.");
        }
    }

    public void RemoveSpeaker(byte controllerId)
    {
        if (speakerFactory.RemoveSpeaker(controllerId))
        {
            Logger.Info($"[AdvancedAudioPlugin] RemoveSpeaker: Removed speaker for controller ID {controllerId}.");
        }
        else
        {
            Logger.Warn($"[AdvancedAudioPlugin] RemoveSpeaker: No speaker found for controller ID {controllerId}.");
        }
    }

    public void OnRoundEnded()
    {
        speakerFactory.ClearSpeakers();
        Logger.Info($"[AdvancedAudioPlugin] OnRoundEnded: Cleared all speakers for round restart.");
    }
}
```

### 4. Initialize AudioManager (Advanced)

Create an instance of `AudioManager` for advanced control, suitable for complex plugins requiring custom audio management. Use `StaticSpeakerFactory.Instance` for static access.

```csharp
using AudioManagerAPI;
using AudioManagerAPI.Features.Static;
using AudioManagerAPI.Features.Management;
using System;
using System.Reflection;
using UnityEngine;
using LabApi.Features.Console;
using LabApi.Player;

public class CustomAudioManager
{
    private readonly IAudioManager audioManager;

    public CustomAudioManager()
    {
        audioManager = new AudioManager(StaticSpeakerFactory.Instance, cacheSize: 20);
        RegisterAudioResources();
    }

    private void RegisterAudioResources()
    {
        var assembly = Assembly.GetExecutingAssembly();
        audioManager.RegisterAudio("ambientSound", () => 
            assembly.GetManifestResourceStream("MyPlugin.Audio.ambient.wav"));
    }

    public void PlayZoneSound(Vector3 position, string zoneName)
    {
        byte controllerId = audioManager.PlayAudio(
            key: "ambientSound",
            position: position,
            loop: false,
            volume: 0.8f,
            minDistance: 5f,
            maxDistance: 50f,
            isSpatial: true,
            priority: AudioPriority.High,
            configureSpeaker: speaker =>
            {
                if (speaker is ISpeakerWithPlayerFilter filterSpeaker)
                {
                    filterSpeaker.SetValidPlayers(p => p.CurrentRoom?.Zone == zoneName);
                }
            },
            queue: false,
            persistent: true
        );

        if (audioManager.IsValidController(controllerId))
        {
            audioManager.FadeInAudio(controllerId, 1f);
            audioManager.SetSpeakerPosition(controllerId, new Vector3(position.x + 5f, position.y, position.z));
            Logger.Info($"[CustomAudioManager] PlayZoneSound: Played sound with controller ID {controllerId} in zone {zoneName}.");
        }
        else
        {
            Logger.Warn($"[CustomAudioManager] PlayZoneSound: Failed to play sound in zone {zoneName}.");
        }
    }

    public void PlayGlobalAnnouncement()
    {
        byte controllerId = audioManager.PlayGlobalAudio(
            key: "ambientSound",
            loop: false,
            volume: 0.8f,
            priority: AudioPriority.High,
            queue: true
        );

        if (audioManager.IsValidController(controllerId))
        {
            audioManager.FadeInAudio(controllerId, 1f);
            audioManager.ClearSpeakerQueue(controllerId);
            Logger.Info($"[CustomAudioManager] PlayGlobalAnnouncement: Played global sound with controller ID {controllerId}.");
        }
    }
}
```

## Extension Methods

The `AudioManagerAPI` provides extension methods for `IAudioManager` to simplify common operations:

```csharp
using AudioManagerAPI.Extensions;

public void Example()
{
    IAudioManager audioManager = new AudioManager(StaticSpeakerFactory.Instance, cacheSize: 20);
    audioManager.Play("ambientSound", new Vector3(10f, 0f, 0f));
    audioManager.FadeIn(1, 2f);
    audioManager.SetVolume(1, 0.5f);
}
```

## Manage Speakers

Use `StaticSpeakerFactory` or `DefaultSpeakerFactory` to create and manage speakers directly:

```csharp
using AudioManagerAPI.Features.Static;
using AudioManagerAPI.Features.Speakers;
using UnityEngine;

public void ManageSpeakers()
{
    ISpeaker speaker = StaticSpeakerFactory.CreateSpeaker(new Vector3(0f, 0f, 0f), 1);
    if (speaker != null)
    {
        speaker.Play(GetAudioSamples(), loop: false);
        StaticSpeakerFactory.RemoveSpeaker(1);
    }
    StaticSpeakerFactory.ClearSpeakers();
}
```

## Audio Requirements

- **Format**: Audio clips must be WAV files, loaded as PCM samples (e.g., via `Assembly.GetManifestResourceStream`).
- **Registration**: Register audio with `IAudioManager.RegisterAudio` before playback.
- **Caching**: Use `AudioCache` for efficient memory management, specifying `cacheSize` in `AudioManager` or `DefaultAudioManager`.

## Audio Control and Prioritization

- **Priority**: Use `AudioPriority` (Low, Medium, High) to manage playback precedence.
- **Queuing**: Queue audio clips with `queue: true` to play sequentially.
- **Fading**: Use `FadeInAudio` and `FadeOutAudio` for smooth transitions.
- **Validation**: Check `IsValidController` before manipulating speakers.

## API Reference

### Key Classes and Interfaces

| Name                     | Namespace                             | Description                                                                 |
|--------------------------|---------------------------------------|-----------------------------------------------------------------------------|
| `IAudioManager`          | `AudioManagerAPI.Features.Management` | Defines the contract for audio playback and speaker lifecycle management.    |
| `AudioManager`           | `AudioManagerAPI.Features.Management` | Implements audio management with caching and shared controller IDs.          |
| `ISpeaker`               | `AudioManagerAPI.Features.Speakers`   | Represents a speaker for playing, queuing, pausing, resuming, skipping, and fading audio. |
| `ISpeakerWithPlayerFilter` | `AudioManagerAPI.Features.Speakers`   | Extends `ISpeaker` to support player-specific audibility, volume, position, range, and spatialization. |
| `ISpeakerFactory`        | `AudioManagerAPI.Features.Speakers`   | Defines a factory for creating and managing speaker instances.               |
| `AudioCache`             | `AudioManagerAPI.Cache`               | Manages audio samples with LRU eviction and lazy loading.                    |
| `ControllerIdManager`    | `AudioManagerAPI.Controllers`         | Static class for managing unique controller IDs with priority-based eviction and queuing. |
| `AudioPriority`          | `AudioManagerAPI.Features.Enums`      | Enum defining audio priority levels (Low, Medium, High).                     |
| `DefaultAudioManager`    | `AudioManagerAPI.Defaults`            | Simplifies audio management with default settings and convenience methods.   |
| `DefaultSpeakerToyAdapter` | `AudioManagerAPI.Defaults`          | Default LabAPI `SpeakerToy` adapter with full feature support.               |
| `DefaultSpeakerFactory`  | `AudioManagerAPI.Defaults`            | Instantiable factory for creating and managing `DefaultSpeakerToyAdapter` instances with a thread-safe registry. |
| `StaticSpeakerFactory`   | `AudioManagerAPI.Features.Static`     | Static wrapper around `DefaultSpeakerFactory` for simplified speaker management, aligned with global `ControllerIdManager`. |
| `SpeakerState`           | `AudioManagerAPI.Speakers.State`      | Stores persistent speaker state for recovery (position, volume, queued clips). |

### Important Methods

- `IAudioManager.PlayAudio`: Plays audio at a specific position with configurable parameters.
- `IAudioManager.PlayGlobalAudio`: Plays audio for all players.
- `ISpeakerFactory.CreateSpeaker`: Creates a new speaker at a specified position.
- `ISpeaker.Play`: Plays audio samples with optional looping.
- `ISpeakerWithPlayerFilter.SetValidPlayers`: Sets a filter to control which players hear the audio.
- `StaticSpeakerFactory.ClearSpeakers`: Clears all registered speakers.

## Events

- **OnRoundEnded**: Call `ClearSpeakers` to clean up speakers and prevent memory leaks.
- **OnPlayerSpawned**: Use player filters to target specific roles or zones.
- **OnAudioPlaybackFinished**: Monitor playback completion for queued audio.

## Notes

- **Controller ID Synchronization**: `ControllerIdManager` ensures no ID conflicts by maintaining a shared pool of IDs (1-255). High-priority audio can evict lower-priority speakers or be queued for later allocation.
- **Speaker Lifecycle Management**: Use `StaticSpeakerFactory` or `DefaultSpeakerFactory` methods (`GetSpeaker`, `RemoveSpeaker`, `ClearSpeakers`) to manage speakers. Call `ClearSpeakers` on round restarts to prevent memory leaks.
- **Thread Safety**: All operations (ID allocation, caching, speaker management) are thread-safe using locks.
- **Dependencies**: Requires `UnityEngine.CoreModule`, `LabApi`, and `MEC`. Fully compatible with LabAPI’s `Player` and `SpeakerToy` classes, aligning with Northwood’s SCP:SL ecosystem. No Exiled dependencies are used.
- **Logging**: Uses `LabApi.Features.Console.Logger` for debugging. Integrate with your plugin’s logging system for additional context.
- **Spatial Audio**: Use `isSpatial: true` for positional effects (e.g., zone-specific sounds) and `isSpatial: false` for global sounds (e.g., announcements). Non-spatial audio defaults to `Vector3.zero`.
- **Fading and Queuing**: Use `FadeInAudio`/`FadeOutAudio` for smooth transitions, `ClearSpeakerQueue` to reset queues, and `GetQueueStatus` to monitor queue state.
- **Persistent Speakers**: Use `persistent: true` to retain speaker state (position, volume, queued clips, playback position) for recovery via `RecoverSpeaker`, validated by `ValidateState`.
- **Playback Position**: For persistent speakers, playback position is approximated by trimming samples, as `AudioTransmitter` does not natively support seeking.
- **Player Filters**: Use `ISpeakerWithPlayerFilter.SetValidPlayers` to control which players hear audio. Examples:
  - Play audio in a specific zone: `p => p.CurrentRoom?.Zone == "HeavyContainmentZone"`.
  - Play audio for specific roles: `p => p.Role.ToString() == "Scientist"`.
  - Play audio in specific rooms: `p => p.CurrentRoom?.Name == "HCZ_079"`.

## Contributing

Contributions are welcome! Please submit pull requests or issues to the [GitHub repository](https://github.com/your-repo/SCPSL-AudioManagerAPI). Ensure code follows SCP:SL plugin conventions and includes tests for new features.

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.