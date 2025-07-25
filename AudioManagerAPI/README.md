# SCPSL-AudioManagerAPI

[![NuGet Version](https://img.shields.io/nuget/v/SCPSL-AudioManagerAPI.svg)](https://www.nuget.org/packages/SCPSL-AudioManagerAPI/)  
A lightweight, reusable C# library for managing audio playback in SCP: Secret Laboratory (SCP:SL) plugins using LabAPI. Designed to integrate seamlessly with Northwood’s LabAPI ecosystem, it provides a robust system for loading, caching, and playing audio through `SpeakerToy` instances, with centralized controller ID management, advanced audio control (volume, position, range, spatialization, fading, queuing), speaker lifecycle management, and prioritization for enhanced gameplay.

> ⚠️ **Warning**  
> This plugin is currently in active development. New features are being added regularly, and not all functionality has been fully tested in live gameplay.  
> If you encounter any issues or bugs, please report them on the [official GitHub repository](https://github.com/ioMatix/-SCPSL-AudioManagerAPI/issues).

## Description

`SCPSL-AudioManagerAPI` is a robust library for managing spatial and global audio in SCP: Secret Laboratory server-side plugins. Built on top of LabAPI’s `SpeakerToy`, it provides a high-level interface for playing audio clips, managing speaker lifecycles, and applying player-specific filters. With thread-safe operations and integration with `ControllerIdManager`, it ensures seamless audio playback across SCP:SL’s multi-threaded environment. The library supports both simple and advanced use cases, from ambient sounds to role-specific announcements, without requiring Exiled dependencies.

## Features

- **Spatial Audio**: Play audio at specific world positions with customizable volume, range, and spatialization.
- **Global Audio**: Broadcast audio to all players, ideal for announcements or events.
- **Player Filters**: Restrict audio to specific players based on role, team, room, or custom conditions using `AudioFilters`.
- **Thread-Safe Management**: Handles concurrent audio operations with a shared, thread-safe speaker registry.
- **Controller ID System**: Integrates with `ControllerIdManager` for unique speaker IDs (1-255) across plugins.
- **Audio Caching**: Uses `AudioCache` with LRU eviction for efficient audio sample management.
- **Flexible Factories**: Offers `StaticSpeakerFactory` for simple static access and `DefaultSpeakerFactory` for advanced control.
- **Persistence**: Supports persistent speaker states for recovery after interruptions.
- **Priority and Queuing**: Manages audio priorities (`AudioPriority`) and queues for smooth playback.

## Installation

Install the `SCPSL-AudioManagerAPI` package via NuGet:

```bash
dotnet add package SCPSL-AudioManagerAPI --version 1.5.2
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
    <PackageReference Include="SCPSL-AudioManagerAPI" Version="1.5.2" />
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

### 5. Using Audio Filters

The `AudioFilters` class in the `AudioManagerAPI.Features.Filters` namespace provides predefined filters to control which players hear audio from a speaker. These filters are accessible via `StaticSpeakerFactory.AudioFilters` for convenience and can be used with `ISpeakerWithPlayerFilter.SetValidPlayers` to target specific players based on role, team, position, room, or custom conditions. Filters can be combined to create complex audio playback scenarios, such as playing audio only to certain roles in specific rooms during events like blackouts.

#### Available Filters
- `ByRole(RoleTypeId roleType)`: Filters players by their role (e.g., `RoleTypeId.Scp173`).
- `ByTeam(Team team)`: Filters players by their team (e.g., `Team.SCP`).
- `ByDistance(Vector3 position, float maxDistance)`: Filters players within a specified distance from a position.
- `IsAlive()`: Filters players who are alive.
- `IsInRoomWhereLightsAre(bool lightsEnabled)`: Filters players in rooms with lights enabled (`true`) or disabled (`false`).
- `IsConditionTrue(bool condition)`: Filters players based on a boolean condition (e.g., event active).
- `IsInRoom(RoomName roomType)`: Filters players in a specific room type (e.g., `RoomName.EzIntercom`).

#### Example: Playing Audio for SCPs During a Blackout
This example plays audio only to SCP players who are alive and in a dark room during a blackout event.

```csharp
using AudioManagerAPI.Features.Static;
using UnityEngine;
using LabApi.Features.Console;
using LabApi.Features.Wrappers;
using MapGeneration;
using PlayerRoles;

public class BlackoutAudioPlugin
{
    public void PlayBlackoutSound(Vector3 position)
    {
        byte controllerId = 1;
        ISpeaker speaker = StaticSpeakerFactory.GetSpeaker(controllerId) ?? StaticSpeakerFactory.CreateSpeaker(position, controllerId);

        if (speaker != null && speaker is ISpeakerWithPlayerFilter filterSpeaker)
        {
            float[] audioSamples = GetAudioSamples(); // Assume method to get PCM samples
            bool isBlackoutActive = true; // Example: Check blackout event state
            filterSpeaker.SetValidPlayers(new[]
            {
                StaticSpeakerFactory.AudioFilters.ByTeam(Team.SCP),
                StaticSpeakerFactory.AudioFilters.IsAlive(),
                StaticSpeakerFactory.AudioFilters.IsInRoomWhereLightsAre(false),
                StaticSpeakerFactory.AudioFilters.IsConditionTrue(isBlackoutActive)
            });
            speaker.Play(audioSamples, loop: false);
            speaker.SetVolume(0.8f);
            speaker.SetSpatialization(true);
            speaker.SetMaxDistance(20f);
            Logger.Info($"[BlackoutAudioPlugin] Playing blackout sound for SCPs at position {position}.");
        }
        else
        {
            Logger.Warn($"[BlackoutAudioPlugin] Failed to get or create speaker for controller ID {controllerId}.");
        }
    }

    public void OnRoundEnded()
    {
        StaticSpeakerFactory.ClearSpeakers();
        Logger.Info("[BlackoutAudioPlugin] Cleared all speakers for round restart.");
    }
}
```

#### Example: Playing Audio in a Specific Room
This example plays audio only to players in the EZ Intercom room who are alive.

```csharp
using AudioManagerAPI.Features.Static;
using UnityEngine;
using LabApi.Features.Console;
using LabApi.Features.Wrappers;
using MapGeneration;

public class IntercomAudioPlugin
{
    public void PlayIntercomSound(Vector3 position)
    {
        byte controllerId = 2;
        ISpeaker speaker = StaticSpeakerFactory.GetSpeaker(controllerId) ?? StaticSpeakerFactory.CreateSpeaker(position, controllerId);

        if (speaker != null && speaker is ISpeakerWithPlayerFilter filterSpeaker)
        {
            float[] audioSamples = GetAudioSamples(); // Assume method to get PCM samples
            filterSpeaker.SetValidPlayers(new[]
            {
                StaticSpeakerFactory.AudioFilters.IsInRoom(RoomName.EzIntercom),
                StaticSpeakerFactory.AudioFilters.IsAlive()
            });
            speaker.Play(audioSamples, loop: false);
            speaker.SetVolume(0.9f);
            speaker.SetSpatialization(true);
            speaker.SetMaxDistance(15f);
            Logger.Info($"[IntercomAudioPlugin] Playing sound in EZ Intercom at position {position}.");
        }
        else
        {
            Logger.Warn($"[IntercomAudioPlugin] Failed to get or create speaker for controller ID {controllerId}.");
        }
    }

    public void OnRoundEnded()
    {
        StaticSpeakerFactory.ClearSpeakers();
        Logger.Info("[IntercomAudioPlugin] Cleared all speakers for round restart.");
    }
}
```

#### Example: Proximity-Based Audio for Scientists
This example plays audio only to Scientists within 10 units of the speaker’s position.

```csharp
using AudioManagerAPI.Features.Static;
using UnityEngine;
using LabApi.Features.Console;
using LabApi.Features.Wrappers;
using PlayerRoles;

public class ProximityAudioPlugin
{
    public void PlayProximitySound(Vector3 position)
    {
        byte controllerId = 3;
        ISpeaker speaker = StaticSpeakerFactory.GetSpeaker(controllerId) ?? StaticSpeakerFactory.CreateSpeaker(position, controllerId);

        if (speaker != null && speaker is ISpeakerWithPlayerFilter filterSpeaker)
        {
            float[] audioSamples = GetAudioSamples(); // Assume method to get PCM samples
            filterSpeaker.SetValidPlayers(new[]
            {
                StaticSpeakerFactory.AudioFilters.ByRole(RoleTypeId.Scientist),
                StaticSpeakerFactory.AudioFilters.ByDistance(position, 10f)
            });
            speaker.Play(audioSamples, loop: false);
            speaker.SetVolume(0.7f);
            speaker.SetSpatialization(true);
            speaker.SetMaxDistance(10f);
            Logger.Info($"[ProximityAudioPlugin] Playing sound for Scientists near position {position}.");
        }
        else
        {
            Logger.Warn($"[ProximityAudioPlugin] Failed to get or create speaker for controller ID {controllerId}.");
        }
    }

    public void OnRoundEnded()
    {
        StaticSpeakerFactory.ClearSpeakers();
        Logger.Info("[ProximityAudioPlugin] Cleared all speakers for round restart.");
    }
}
```

#### Notes on Using AudioFilters
- **Accessing Filters**: Use `StaticSpeakerFactory.AudioFilters` to access the predefined filters, which are defined in `AudioManagerAPI.Features.Filters.AudioFilters`.
- **Combining Filters**: Pass multiple filters to `SetValidPlayers(IEnumerable<Func<Player, bool>>)`. A player must pass all filters to hear the audio.
- **Performance**: Filters like `ByDistance` involve calculations. Cache the position parameter if used frequently (e.g., per frame for many players).
- **Thread Safety**: Filters are stateless and thread-safe, compatible with the thread-safe `StaticSpeakerFactory` and `DefaultSpeakerFactory`.
- **Logging**: Filters include logging for edge cases (e.g., null players or rooms) using `LabApi.Features.Console.Logger`. Check logs for debugging.
- **Custom Filters**: For scenarios not covered by predefined filters, use `SetValidPlayers` with a custom `Func<Player, bool>` (e.g., `p => p.CurrentRoom?.Zone == "HeavyContainmentZone"`).

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
| `AudioFilters`           | `AudioManagerAPI.Features.Filters`    | Provides predefined filters for controlling which players hear audio based on role, team, position, room, or custom conditions. |
| `SpeakerState`           | `AudioManagerAPI.Speakers.State`      | Stores persistent speaker state for recovery (position, volume, queued clips). |

### Important Methods

- `IAudioManager.PlayAudio`: Plays audio at a specific position with configurable parameters.
- `IAudioManager.PlayGlobalAudio`: Plays audio for all players.
- `ISpeakerFactory.CreateSpeaker`: Creates a new speaker at a specified position.
- `ISpeaker.Play`: Plays audio samples with optional looping.
- `ISpeakerWithPlayerFilter.SetValidPlayers`: Sets a filter to control which players hear the audio.
- `StaticSpeakerFactory.ClearSpeakers`: Clears all registered speakers.
- `StaticSpeakerFactory.AudioFilters.*`: Provides predefined filters for role, team, distance, room, and custom conditions.

## Events

- **OnRoundEnded**: Call `ClearSpeakers` to clean up speakers and prevent memory leaks.
- **OnPlayerSpawned**: Use player filters to target specific roles or zones.
- **OnAudioPlaybackFinished**: Monitor playback completion for queued audio.

## Notes

- **Controller ID Synchronization**: `ControllerIdManager` ensures no ID conflicts by maintaining a shared pool of IDs (1-255). High-priority audio can evict lower-priority speakers or be queued for later allocation.
- **Speaker Lifecycle Management**: Use `StaticSpeakerFactory` or `DefaultSpeakerFactory` methods (`GetSpeaker`, `RemoveSpeaker`, `ClearSpeakers`) to manage speakers. Call `ClearSpeakers` on round restarts to prevent memory leaks.
- **Thread Safety**: All operations (ID allocation, caching, speaker management, filters) are thread-safe using locks.
- **Dependencies**: Requires `UnityEngine.CoreModule`, `LabApi`, and `MEC`. Fully compatible with LabAPI’s `Player` and `SpeakerToy` classes, aligning with Northwood’s SCP:SL ecosystem. No Exiled dependencies are used.
- **Logging**: Uses `LabApi.Features.Console.Logger` for debugging. Integrate with your plugin’s logging system for additional context.
- **Spatial Audio**: Use `isSpatial: true` for positional effects (e.g., zone-specific sounds) and `isSpatial: false` for global sounds (e.g., announcements). Non-spatial audio defaults to `Vector3.zero`.
- **Fading and Queuing**: Use `FadeInAudio`/`FadeOutAudio` for smooth transitions, `ClearSpeakerQueue` to reset queues, and `GetQueueStatus` to monitor queue state.
- **Persistent Speakers**: Use `persistent: true` to retain speaker state (position, volume, queued clips, playback position) for recovery via `RecoverSpeaker`, validated by `ValidateState`.
- **Playback Position**: For persistent speakers, playback position is approximated by trimming samples, as `AudioTransmitter` does not natively support seeking.
- **Player Filters**: Use `StaticSpeakerFactory.AudioFilters` or custom `Func<Player, bool>` with `ISpeakerWithPlayerFilter.SetValidPlayers` to control which players hear audio.

## Contributing

Contributions are welcome! Please submit pull requests or issues to the [GitHub repository](https://github.com/ioMatix/-SCPSL-AudioManagerAPI). Ensure code follows SCP:SL plugin conventions and includes tests for new features.

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.