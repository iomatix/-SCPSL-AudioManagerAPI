# SCPSL-AudioManagerAPI

[![NuGet Version](https://img.shields.io/nuget/v/SCPSL-AudioManagerAPI.svg)](https://www.nuget.org/packages/SCPSL-AudioManagerAPI/)  
A lightweight, reusable C# library for managing audio playback in SCP: Secret Laboratory (SCP:SL) plugins using LabAPI. Designed to integrate seamlessly with Northwood’s LabAPI ecosystem, it provides a robust system for loading, caching, and playing audio through `SpeakerToy` instances, with centralized controller ID management, advanced audio control (volume, position, range, spatialization, fading, queuing), speaker lifecycle management, and prioritization for enhanced gameplay.

> ⚠️ **Warning**  
> This library is currently in active development. New features are being added regularly, and not all functionality has been fully tested in live gameplay.  
> If you encounter any issues or bugs, please report them on the [official GitHub repository](https://github.com/ioMatix/-SCPSL-AudioManagerAPI/issues).

> ⚠️ **Warning**  
> Version 1.7.0 introduces breaking changes. `DefaultAudioManager` is now initialized automatically via `AudioConfig.json` using `System.Text.Json`. Manual initialization (e.g., `RegisterDefaults`) is no longer supported. Update your plugins to use `DefaultAudioManager.Instance` directly.

## What's New in Version 1.7.0

- **Automatic Initialization**: `DefaultAudioManager.Instance` is lazily initialized on first access using settings from `Configs/AudioConfig.json`, eliminating manual setup.
- **Configuration File**: Settings like cache size, speaker factory choice, and fade durations are loaded from `AudioConfig.json`, auto-created with defaults if missing.
- **Thread-Safe Singleton**: `DefaultAudioManager` uses `Lazy<IAudioManager>` for thread-safe, performant initialization in SCP:SL’s multi-threaded environment.
- **Dependency-Free JSON**: Configuration loading now uses `System.Text.Json`, removing external dependencies like Newtonsoft.Json.
- **Enhanced Thread-Safety**: `DefaultSpeakerFactory` uses `ConcurrentDictionary` for improved performance and thread-safety in speaker management.

## Description

`SCPSL-AudioManagerAPI` is a robust library for managing spatial and global audio in SCP: Secret Laboratory server-side plugins. Built on top of LabAPI’s `SpeakerToy`, it provides a high-level interface for playing audio clips, managing speaker lifecycles, and applying player-specific filters. With thread-safe operations and integration with `ControllerIdManager`, it ensures seamless audio playback across SCP:SL’s multi-threaded environment. The library supports both simple and advanced use cases, from ambient sounds to role-specific announcements, without requiring Exiled dependencies. Version 1.7.0 enhances configuration flexibility and thread-safety, while version 1.6.0 introduced enhanced filtering for global audio with the `PlayGlobalAudioWithFilter` method and improved documentation distinguishing spatial and non-spatial audio.

## Features

- **Spatial Audio**: Play audio at specific world positions with customizable volume, range, and spatialization.
- **Global Audio**: Broadcast audio to all players, ideal for announcements or events.
- **Global Audio with Filters**: Broadcast audio to specific players using custom filters via `PlayGlobalAudioWithFilter`, perfect for targeted announcements or events.
- **Player Filters**: Restrict audio to specific players based on role, team, room, or custom conditions using `AudioFilters`.
- **Thread-Safe Management**: Handles concurrent audio operations with a shared, thread-safe speaker registry using `ConcurrentDictionary`.
- **Controller ID System**: Integrates with `ControllerIdManager` for unique speaker IDs (1-255) across plugins.
- **Audio Caching**: Uses `AudioCache` with LRU eviction for efficient audio sample management.
- **Flexible Factories**: Offers `StaticSpeakerFactory` for simple static access and `DefaultSpeakerFactory` for advanced control.
- **Persistence**: Supports persistent speaker states for recovery after interruptions.
- **Priority and Queuing**: Manages audio priorities (`AudioPriority`) and queues for smooth playback.

## Installation

Install the `SCPSL-AudioManagerAPI` package via NuGet:

```bash
dotnet add package SCPSL-AudioManagerAPI --version 1.7.0
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
    <PackageReference Include="SCPSL-AudioManagerAPI" Version="1.7.0" />
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

## Configuration

The library loads settings from `Configs/AudioConfig.json`, which is auto-created with defaults if missing. Example configuration:

```json
{
  "CacheSize": 50,
  "UseDefaultSpeakerFactory": true,
  "DefaultFadeInDuration": 1.0,
  "DefaultFadeOutDuration": 1.0
}
```

- **CacheSize**: Number of audio clips to keep in memory (default: 50).
- **UseDefaultSpeakerFactory**: Set to `true` for `DefaultSpeakerFactory` or `false` for `StaticSpeakerFactory`.
- **DefaultFadeInDuration**: Default duration for fade-in effects (in seconds).
- **DefaultFadeOutDuration**: Default duration for fade-out effects (in seconds).

Ensure the `Configs` directory is writable to allow automatic creation of `AudioConfig.json`.

## Usage

### 1. Using DefaultAudioManager (Recommended)

The `DefaultAudioManager` provides a plug-and-play interface for common audio tasks, ideal for most SCP:SL plugins. In version 1.7.0, `DefaultAudioManager.Instance` is initialized automatically, so no manual setup is required.

```csharp
using AudioManagerAPI.Defaults;
using UnityEngine;
using LabApi.Features.Console;

public class AudioPlugin
{
    public void Initialize()
    {
        // Register audio stream
        DefaultAudioManager.RegisterAudio("ambientSound", () => 
            Assembly.GetExecutingAssembly().GetManifestResourceStream("MyPlugin.Audio.ambient.wav"));

        // WAV requirements:
        // • 16-bit PCM
        // • Mono (1 channel)
        // • 48 kHz sample rate
        // Resource name format: "MyPlugin.Audio.ambient.wav"
    }

    public void PlayAudio()
    {
        byte id = DefaultAudioManager.Play("ambientSound", queue: true, fadeInDuration: 2f);
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

    public void PlayGlobalAudioWithFilter()
    {
        byte id = DefaultAudioManager.Instance.PlayGlobalAudioWithFilter(
            key: "ambientSound",
            loop: false,
            volume: 0.8f,
            priority: AudioPriority.High,
            configureSpeaker: speaker =>
            {
                if (speaker is ISpeakerWithPlayerFilter filterSpeaker)
                {
                    filterSpeaker.SetValidPlayers(p => p.Role == RoleTypeId.Scp173);
                }
            },
            queue: false,
            fadeInDuration: 1f,
            persistent: true,
            lifespan: 10f,
            autoCleanup: true
        );

        if (id != 0)
        {
            Logger.Info($"[AudioPlugin] Played global audio with filter for SCP-173 with controller ID {id}.");
        }
        else
        {
            Logger.Warn("[AudioPlugin] Failed to play global audio with filter.");
        }
    }
}
```

### 2. Using StaticSpeakerFactory (Recommended for Simple Speaker Management)

The `StaticSpeakerFactory` in the `AudioManagerAPI.Features.Static` namespace provides a static interface for managing `DefaultSpeakerToyAdapter` instances. It leverages a shared, thread-safe speaker registry aligned with the global `ControllerIdManager`, ideal for simple audio playback scenarios. Audio must be registered using `DefaultAudioManager.RegisterAudio` before playback.

```csharp
using AudioManagerAPI.Defaults;
using AudioManagerAPI.Features.Static;
using AudioManagerAPI.Features.Speakers;
using UnityEngine;
using LabApi.Features.Console;
using LabApi.Player;

public class AudioPlugin
{
    public void Initialize()
    {
        // Register audio stream
        DefaultAudioManager.RegisterAudio("zoneSound", () => 
            Assembly.GetExecutingAssembly().GetManifestResourceStream("MyPlugin.Audio.zone.wav"));
    }

    public void PlayZoneSound(Vector3 position, string zoneName)
    {
        byte controllerId = 1;
        ISpeaker speaker = StaticSpeakerFactory.GetSpeaker(controllerId) ?? StaticSpeakerFactory.CreateSpeaker(position, controllerId);

        if (speaker != null)
        {
            // Play registered audio using the key
            DefaultAudioManager.Instance.PlayAudio(
                key: "zoneSound",
                position: position,
                loop: false,
                volume: 0.8f,
                minDistance: 5f,
                maxDistance: 20f,
                isSpatial: true,
                priority: AudioPriority.Medium,
                configureSpeaker: s =>
                {
                    if (s is ISpeakerWithPlayerFilter filterSpeaker)
                    {
                        filterSpeaker.SetValidPlayers(p => p.CurrentRoom?.Zone == zoneName);
                    }
                },
                queue: false
            );

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

The `DefaultSpeakerFactory` in the `AudioManagerAPI.Defaults` namespace provides an instantiable factory for advanced use cases, compatible with `AudioManager`. Use `StaticSpeakerFactory` for simpler, static access. Audio must be registered using `AudioManager.RegisterAudio` before playback.

```csharp
using AudioManagerAPI.Defaults;
using AudioManagerAPI.Features.Speakers;
using AudioManagerAPI.Features.Management;
using UnityEngine;
using LabApi.Features.Console;
using LabApi.Player;

public class AdvancedAudioPlugin
{
    private readonly IAudioManager audioManager;
    private readonly ISpeakerFactory speakerFactory;

    public AdvancedAudioPlugin()
    {
        speakerFactory = new DefaultSpeakerFactory();
        audioManager = new AudioManager(speakerFactory);
        RegisterAudioResources();
    }

    private void RegisterAudioResources()
    {
        audioManager.RegisterAudio("roleSound", () => 
            Assembly.GetExecutingAssembly().GetManifestResourceStream("MyPlugin.Audio.role.wav"));
    }

    public void PlayRoleBasedSound(Vector3 position, string roleType)
    {
        byte controllerId = 1;
        ISpeaker speaker = speakerFactory.GetSpeaker(controllerId) ?? speakerFactory.CreateSpeaker(position, controllerId);

        if (speaker != null)
        {
            audioManager.PlayAudio(
                key: "roleSound",
                position: position,
                loop: false,
                volume: 0.8f,
                minDistance: 5f,
                maxDistance: 20f,
                isSpatial: true,
                priority: AudioPriority.Medium,
                configureSpeaker: s =>
                {
                    if (s is ISpeakerWithPlayerFilter filterSpeaker)
                    {
                        filterSpeaker.SetValidPlayers(p => p.Role.ToString() == roleType);
                    }
                },
                queue: false
            );

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
        audioManager = new AudioManager(StaticSpeakerFactory.Instance);
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

```csharp
using AudioManagerAPI.Defaults;
using UnityEngine;
using LabApi.Features.Console;
using LabApi.Features.Wrappers;
using MapGeneration;
using PlayerRoles;

public class BlackoutAudioPlugin
{
    public void Initialize()
    {
        DefaultAudioManager.RegisterAudio("blackoutSound", () => 
            Assembly.GetExecutingAssembly().GetManifestResourceStream("MyPlugin.Audio.blackout.wav"));
    }

    public void PlayBlackoutSound()
    {
        bool isBlackoutActive = true; // Example: Check blackout event state
        byte controllerId = DefaultAudioManager.Instance.PlayGlobalAudioWithFilter(
            key: "blackoutSound",
            loop: false,
            volume: 0.8f,
            priority: AudioPriority.High,
            configureSpeaker: speaker =>
            {
                if (speaker is ISpeakerWithPlayerFilter filterSpeaker)
                {
                    filterSpeaker.SetValidPlayers(p =>
                        p.Team == Team.SCP &&
                        p.IsAlive &&
                        p.CurrentRoom != null &&
                        !p.CurrentRoom.LightsEnabled &&
                        isBlackoutActive
                    );
                }
            },
            queue: false,
            fadeInDuration: 1f,
            persistent: true,
            lifespan: 10f,
            autoCleanup: true
        );

        if (controllerId != 0)
        {
            Logger.Info($"[BlackoutAudioPlugin] Playing blackout sound for SCPs with controller ID {controllerId}.");
        }
        else
        {
            Logger.Warn("[BlackoutAudioPlugin] Failed to play blackout sound.");
        }
    }
}
```

#### Example: Playing Audio in a Specific Room

```csharp
using AudioManagerAPI.Defaults;
using AudioManagerAPI.Features.Static;
using AudioManagerAPI.Features.Speakers;
using UnityEngine;
using LabApi.Features.Console;
using LabApi.Features.Wrappers;
using MapGeneration;

public class IntercomAudioPlugin
{
    public void Initialize()
    {
        DefaultAudioManager.RegisterAudio("intercomSound", () => 
            Assembly.GetExecutingAssembly().GetManifestResourceStream("MyPlugin.Audio.intercom.wav"));
    }

    public void PlayIntercomSound(Vector3 position)
    {
        byte controllerId = 2;
        ISpeaker speaker = StaticSpeakerFactory.GetSpeaker(controllerId) ?? StaticSpeakerFactory.CreateSpeaker(position, controllerId);

        if (speaker != null)
        {
            DefaultAudioManager.Instance.PlayAudio(
                key: "intercomSound",
                position: position,
                loop: false,
                volume: 0.9f,
                minDistance: 5f,
                maxDistance: 15f,
                isSpatial: true,
                priority: AudioPriority.Medium,
                configureSpeaker: s =>
                {
                    if (s is ISpeakerWithPlayerFilter filterSpeaker)
                    {
                        filterSpeaker.SetValidPlayers(new[]
                        {
                            StaticSpeakerFactory.AudioFilters.IsInRoom(RoomName.EzIntercom),
                            StaticSpeakerFactory.AudioFilters.IsAlive()
                        });
                    }
                },
                queue: false
            );

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

```csharp
using AudioManagerAPI.Defaults;
using AudioManagerAPI.Features.Static;
using AudioManagerAPI.Features.Speakers;
using UnityEngine;
using LabApi.Features.Console;
using LabApi.Features.Wrappers;
using PlayerRoles;

public class ProximityAudioPlugin
{
    public void Initialize()
    {
        DefaultAudioManager.RegisterAudio("proximitySound", () => 
            Assembly.GetExecutingAssembly().GetManifestResourceStream("MyPlugin.Audio.proximity.wav"));
    }

    public void PlayProximitySound(Vector3 position)
    {
        byte controllerId = 3;
        ISpeaker speaker = StaticSpeakerFactory.GetSpeaker(controllerId) ?? StaticSpeakerFactory.CreateSpeaker(position, controllerId);

        if (speaker != null)
        {
            DefaultAudioManager.Instance.PlayAudio(
                key: "proximitySound",
                position: position,
                loop: false,
                volume: 0.7f,
                minDistance: 5f,
                maxDistance: 10f,
                isSpatial: true,
                priority: AudioPriority.Medium,
                configureSpeaker: s =>
                {
                    if (s is ISpeakerWithPlayerFilter filterSpeaker)
                    {
                        filterSpeaker.SetValidPlayers(new[]
                        {
                            StaticSpeakerFactory.AudioFilters.ByRole(RoleTypeId.Scientist),
                            StaticSpeakerFactory.AudioFilters.ByDistance(position, 10f)
                        });
                    }
                },
                queue: false
            );

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
- **Accessing Filters**: Use `StaticSpeakerFactory.AudioFilters` to access predefined filters in `AudioManagerAPI.Features.Filters.AudioFilters`.
- **Combining Filters**: Pass multiple filters to `SetValidPlayers(IEnumerable<Func<Player, bool>>)`. A player must pass all filters to hear the audio.
- **Performance**: Filters like `ByDistance` involve calculations. Cache the position parameter if used frequently (e.g., per frame for many players).
- **Thread Safety**: Filters are stateless and thread-safe, compatible with `StaticSpeakerFactory` and `DefaultSpeakerFactory`.
- **Logging**: Filters include logging for edge cases (e.g., null players or rooms) using `LabApi.Features.Console.Logger`. Check logs for debugging.
- **Custom Filters**: For scenarios not covered by predefined filters, use `SetValidPlayers` with a custom `Func<Player, bool>` (e.g., `p => p.CurrentRoom?.Zone == "HeavyContainmentZone"`).

## Extension Methods

The `AudioManagerAPI` provides extension methods for `IAudioManager` to simplify common operations:

```csharp
using AudioManagerAPI.Extensions;

public void Example()
{
    IAudioManager audioManager = new AudioManager(StaticSpeakerFactory.Instance);
    audioManager.RegisterAudio("ambientSound", () => 
        Assembly.GetExecutingAssembly().GetManifestResourceStream("MyPlugin.Audio.ambient.wav"));
    audioManager.Play("ambientSound", new Vector3(10f, 0f, 0f));
    audioManager.FadeIn(1, 2f);
    audioManager.SetVolume(1, 0.5f);
}
```

## Manage Speakers

Use `StaticSpeakerFactory` or `DefaultSpeakerFactory` to create and manage speakers directly. Audio must be registered using `DefaultAudioManager.RegisterAudio` or `AudioManager.RegisterAudio` before playback.

```csharp
using AudioManagerAPI.Defaults;
using AudioManagerAPI.Features.Static;
using AudioManagerAPI.Features.Speakers;
using UnityEngine;

public class SpeakerPlugin
{
    public void Initialize()
    {
        DefaultAudioManager.RegisterAudio("speakerSound", () => 
            Assembly.GetExecutingAssembly().GetManifestResourceStream("MyPlugin.Audio.speaker.wav"));
    }

    public void ManageSpeakers()
    {
        byte controllerId = 1;
        ISpeaker speaker = StaticSpeakerFactory.CreateSpeaker(new Vector3(0f, 0f, 0f), controllerId);
        if (speaker != null)
        {
            DefaultAudioManager.Instance.PlayAudio(
                key: "speakerSound",
                position: new Vector3(0f, 0f, 0f),
                loop: false,
                volume: 0.8f,
                minDistance: 5f,
                maxDistance: 20f,
                isSpatial: true,
                priority: AudioPriority.Medium,
                configureSpeaker: null,
                queue: false
            );
            StaticSpeakerFactory.RemoveSpeaker(controllerId);
        }
        StaticSpeakerFactory.ClearSpeakers();
    }
}
```

## Audio Requirements

- **Format**: Audio clips must be WAV files, loaded as PCM samples (e.g., via `Assembly.GetManifestResourceStream`).
- **Registration**: Register audio with `IAudioManager.RegisterAudio` before playback using a unique key.
- **Caching**: Use `AudioCache` for efficient memory management, specifying `cacheSize` in `AudioManager` or `DefaultAudioManager`.

## Audio Control and Prioritization

- **Priority**: Use `AudioPriority` (Low, Medium, High) to manage playback precedence.
- **Queuing**: Queue audio clips with `queue: true` to play sequentially.
- **Fading**: Use `FadeInAudio` and `FadeOutAudio` for smooth transitions.
- **Validation**: Check `IsValidController` before manipulating speakers.

## Events

The `SCPSL-AudioManagerAPI` provides events to monitor audio playback states, allowing plugins to respond to changes in speaker activity. These events are defined in the `IAudioManager` interface and triggered by operations on speakers.

- **OnPlaybackStarted**: Triggered when a speaker begins audio playback. Provides the controller ID (`byte`) of the speaker.
- **OnPaused**: Triggered when audio playback is paused for a speaker. Provides the controller ID (`byte`) of the speaker.
- **OnResumed**: Triggered when paused audio playback resumes for a speaker. Provides the controller ID (`byte`) of the speaker.
- **OnStop**: Triggered when audio playback is stopped for a speaker. Provides the controller ID (`byte`) of the speaker.
- **OnSkipped**: Triggered when audio clips are skipped for a speaker. Provides the controller ID (`byte`) and the number of clips skipped (`int`).
- **OnQueueEmpty**: Triggered when the audio queue for a speaker becomes empty. Provides the controller ID (`byte`) of the speaker.

#### Example: Subscribing to Events

```csharp
using AudioManagerAPI.Defaults;
using LabApi.Features.Console;

public class AudioEventPlugin
{
    public AudioEventPlugin()
    {
        // Register audio
        DefaultAudioManager.RegisterAudio("eventSound", () => 
            Assembly.GetExecutingAssembly().GetManifestResourceStream("MyPlugin.Audio.event.wav"));

        // Subscribe to events
        DefaultAudioManager.Instance.OnPlaybackStarted += id => 
            Logger.Info($"[AudioEventPlugin] Playback started for controller ID {id}.");
        DefaultAudioManager.Instance.OnPaused += id => 
            Logger.Info($"[AudioEventPlugin] Playback paused for controller ID {id}.");
        DefaultAudioManager.Instance.OnResumed += id => 
            Logger.Info($"[AudioEventPlugin] Playback resumed for controller ID {id}.");
        DefaultAudioManager.Instance.OnStop += id => 
            Logger.Info($"[AudioEventPlugin] Playback stopped for controller ID {id}.");
        DefaultAudioManager.Instance.OnSkipped += (id, count) => 
            Logger.Info($"[AudioEventPlugin] Skipped {count} clips for controller ID {id}.");
        DefaultAudioManager.Instance.OnQueueEmpty += id => 
            Logger.Info($"[AudioEventPlugin] Queue empty for controller ID {id}.");
    }

    public void PlayAudio()
    {
        byte id = DefaultAudioManager.Play("eventSound", queue: true, fadeInDuration: 2f);
        DefaultAudioManager.Pause(id);
        DefaultAudioManager.Resume(id);
        DefaultAudioManager.Skip(id, 1);
        DefaultAudioManager.Stop(id);
    }
}
```

## API Reference

### Key Classes and Interfaces

| Name                     | Namespace                             | Description                                                                 |
|--------------------------|---------------------------------------|-----------------------------------------------------------------------------|
| `IAudioManager`          | `AudioManagerAPI.Features.Management` | Defines the contract for audio playback and speaker lifecycle management.    |
| `AudioManager`           | `AudioManagerAPI.Features.Management` | Implements audio management with caching and shared controller IDs.          |
| `ISpeaker`               | `AudioManagerAPI.Features.Speakers`   | Represents a speaker for playing, queuing, pausing, resuming, skipping, and fading audio. |
| `ISpeakerWithPlayerFilter` | `AudioManagerAPI.Features.Speakers` | Extends `ISpeaker` to support player-specific audibility, volume, position, range, and spatialization. |
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

### Key Methods

- `IAudioManager.PlayAudio`: Plays spatial audio at a specific position with configurable parameters.
- `IAudioManager.PlayGlobalAudio`: Plays non-spatial audio for all players.
- `IAudioManager.PlayGlobalAudioWithFilter`: Plays non-spatial audio with custom speaker configuration, such as player filters.
- `ISpeakerFactory.CreateSpeaker`: Creates a new speaker at a specified position.
- `ISpeaker.Play`: Plays audio samples with optional looping (for advanced use cases).
- `ISpeakerWithPlayerFilter.SetValidPlayers`: Sets a filter to control which players hear the audio.
- `StaticSpeakerFactory.ClearSpeakers`: Clears all registered speakers.
- `StaticSpeakerFactory.AudioFilters.*`: Provides predefined filters for role, team, distance, room, and custom conditions.

## Notes

- **Global Audio with Filters**: Use `PlayGlobalAudioWithFilter` to play non-spatial audio with custom player filters, ideal for targeted announcements or events.
- **Spatial vs. Non-Spatial Audio**: `PlayAudio` is for spatial audio with 3D positioning, while `PlayGlobalAudio` and `PlayGlobalAudioWithFilter` are for non-spatial audio heard uniformly by targeted players.
- **Documentation**: Version 1.7.0 includes comprehensive XML documentation for all public methods, improving clarity and ease of use.
- **Controller ID Synchronization**: `ControllerIdManager` ensures no ID conflicts by maintaining a shared pool of IDs (1-255). High-priority audio can evict lower-priority speakers or be queued for later allocation.
- **Speaker Lifecycle Management**: Use `StaticSpeakerFactory` or `DefaultSpeakerFactory` methods (`GetSpeaker`, `RemoveSpeaker`, `ClearSpeakers`) to manage speakers. Call `ClearSpeakers` on round restarts to prevent memory leaks.
- **Thread Safety**: All operations (ID allocation, caching, speaker management, filters) are thread-safe using `ConcurrentDictionary` or locks.
- **Dependencies**: Requires `UnityEngine.CoreModule`, `LabApi`, and `MEC`. Fully compatible with LabAPI’s `Player` and `SpeakerToy` classes, aligning with Northwood’s SCP:SL ecosystem. No Exiled dependencies are used.
- **Logging**: Uses `LabApi.Features.Console.Logger` for debugging. Integrate with your plugin’s logging system for additional context.
- **Spatial Audio**: Use `isSpatial: true` for positional effects (e.g., zone-specific sounds) and `isSpatial: false` for global sounds (e.g., announcements). Non-spatial audio defaults to `Vector3.zero`.
- **Fading and Queuing**: Use `FadeInAudio`/`FadeOutAudio` for smooth transitions, `ClearSpeakerQueue` to reset queues, and `GetQueueStatus` to monitor queue state.
- **Persistent Speakers**: Use `persistent: true` to retain speaker state (position, volume, queued clips, playback position) for recovery via `RecoverSpeaker`, validated by `ValidateState`.
- **Playback Position**: For persistent speakers, playback position is approximated by trimming samples, as `AudioTransmitter` does not natively support seeking.
- **Player Filters**: Use `StaticSpeakerFactory.AudioFilters` or custom `Func<Player, bool>` with `ISpeakerWithPlayerFilter.SetValidPlayers` to control which players hear audio.

## Migration Guide for Version 1.7.0

If upgrading from version 1.6.0 or earlier:
- **Remove Manual Initialization**: Replace calls to `RegisterDefaults` or manual `DefaultAudioManager` instantiation with `DefaultAudioManager.Instance`.
- **Update Configuration**: Ensure the `Configs` directory is writable for `AudioConfig.json` creation. Review and adjust settings as needed.
- **Check Audio Registration**: Verify audio resources are registered correctly using `RegisterAudio` with a `Func<Stream>`, as `System.Text.Json` replaces Newtonsoft.Json for configuration parsing.
- **Thread-Safety**: Existing code should work without changes, but test in high-concurrency scenarios to leverage new `ConcurrentDictionary` improvements in `DefaultSpeakerFactory`.

## Contributing

Contributions are welcome! Please submit pull requests or issues to the [GitHub repository](https://github.com/ioMatix/-SCPSL-AudioManagerAPI). Ensure code follows SCP:SL plugin conventions, uses `System.Text.Json` for configuration, and includes tests for new features.

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.