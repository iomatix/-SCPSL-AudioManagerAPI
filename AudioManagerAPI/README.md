# SCPSL-AudioManagerAPI

[![NuGet Version](https://img.shields.io/nuget/v/SCPSL-AudioManagerAPI.svg)](https://www.nuget.org/packages/SCPSL-AudioManagerAPI/)  
A lightweight, reusable C# library for managing audio playback in SCP: Secret Laboratory (SCP:SL) plugins using LabAPI. It provides a robust system for loading, caching, and playing audio through `SpeakerToy` instances, with centralized controller ID management, advanced audio control (volume, range, spatialization), and prioritization for enhanced gameplay.

## Features

- **Centralized Controller ID Management**: Uses `ControllerIdManager` to ensure unique speaker IDs (1-255) across plugins, with priority-based eviction and queuing for high-priority audio.
- **LRU Audio Caching**: Efficiently manages audio samples with lazy loading and least-recently-used (LRU) eviction via `AudioCache`.
- **Flexible Speaker Abstraction**: Supports custom speaker implementations through `ISpeaker`, `ISpeakerWithPlayerFilter`, and `ISpeakerFactory` interfaces.
- **Advanced Audio Control**: Configurable volume (0.0 to 1.0), minimum/maximum distance, and spatialization (3D audio) for precise audio tuning.
- **Audio Prioritization**: Supports Low, Medium, and High priorities, with queuing for high-priority audio when IDs are limited.
- **Thread-Safe Operations**: Handles concurrent audio playback, caching, and ID allocation safely.
- **LabAPI Compatibility**: Optimized for SCP:SL, integrating seamlessly with `SpeakerToy` for spatial and global audio playback.

## Installation

Install the `SCPSL-AudioManagerAPI` package via NuGet:

```bash
dotnet add package SCPSL-AudioManagerAPI --version 1.0.0
```

Or, in Visual Studio, use the NuGet Package Manager to search for `SCPSL-AudioManagerAPI`.

## Project Setup

Add the `SCPSL-AudioManagerAPI` package to your SCP:SL plugin project. Ensure you reference `UnityEngine.CoreModule` for `Vector3` and `LabApi` for `SpeakerToy` integration.

Example `.csproj` snippet:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="SCPSL-AudioManagerAPI" Version="1.0.0" />
    <Reference Include="LabApi">
      <HintPath>path\to\LabApi.dll</HintPath>
    </Reference>
    <Reference Include="UnityEngine.CoreModule">
      <HintPath>path\to\UnityEngine.CoreModule.dll</HintPath>
    </Reference>
  </ItemGroup>
</Project>
```

## Usage

### 1. Implement ISpeaker and ISpeakerFactory

Create a custom speaker implementation compatible with LabAPI's `SpeakerToy`.

```csharp
using AudioManagerAPI.Features.Speakers;
using LabApi.Features.Wrappers;
using UnityEngine;

public class LabApiSpeaker : ISpeakerWithPlayerFilter
{
    private readonly SpeakerToy speakerToy;

    public LabApiSpeaker(SpeakerToy speakerToy)
    {
        this.speakerToy = speakerToy ?? throw new ArgumentNullException(nameof(speakerToy));
    }

    public void Play(float[] samples, bool loop)
    {
        var transmitter = SpeakerToy.GetTransmitter(speakerToy.ControllerId);
        transmitter?.Play(samples, queue: false, loop: loop);
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

    public void SetValidPlayers(Func<object, bool> playerFilter)
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

### 2. Initialize AudioManager

Create an instance of `AudioManager` with your `ISpeakerFactory` and register audio resources. The `AudioCache` supports only 48kHz, Mono, Signed 16-bit PCM WAV files.

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
}
```

### 3. Play Audio

Use `IAudioManager` to play audio at specific positions, either locally or globally, with volume, range, spatialization, and priority settings.

- **Local Playback** (specific players):
  ```csharp
  using UnityEngine;

  public void PlayScream(Vector3 position, Player targetPlayer)
  {
      byte? controllerId = audioManager.PlayAudio("myplugin.scream", position, false, 
          volume: 0.8f, minDistance: 5f, maxDistance: 50f, isSpatial: true, priority: AudioPriority.High, speaker =>
      {
          if (speaker is LabApiSpeaker labSpeaker)
          {
              labSpeaker.SetValidPlayers(p => p == targetPlayer);
          }
      });

      if (controllerId.HasValue)
      {
          Log.Info($"Played scream with controller ID {controllerId.Value}.");
      }
  }
  ```

- **Global Playback** (all players):
  ```csharp
  public void PlayGlobalScream(Vector3 position)
  {
      byte? controllerId = audioManager.PlayGlobalAudio("myplugin.scream", position, false, 
          volume: 0.8f, minDistance: 5f, maxDistance: 50f, isSpatial: true, priority: AudioPriority.High);
      if (controllerId.HasValue)
      {
          Log.Info($"Played global scream with controller ID {controllerId.Value}.");
      }
  }
  ```

### 4. Manage Speakers

- **Stop Audio**: Stop playback for a specific speaker.
  ```csharp
  audioManager.StopAudio(controllerId);
  ```

- **Destroy Speaker**: Free resources for a specific speaker.
  ```csharp
  audioManager.DestroySpeaker(controllerId);
  ```

- **Retrieve Speaker**: Access a speaker for further configuration.
  ```csharp
  ISpeaker speaker = audioManager.GetSpeaker(controllerId);
  if (speaker is ISpeakerWithPlayerFilter playerFilterSpeaker)
  {
      playerFilterSpeaker.SetVolume(0.5f);      // Adjust volume
      playerFilterSpeaker.SetMinDistance(3f);   // Adjust min distance
      playerFilterSpeaker.SetMaxDistance(30f);  // Adjust max distance
      playerFilterSpeaker.SetSpatialization(false); // Disable spatial audio
  }
  ```

### 5. Cleanup

Clean up all speakers to free resources.

```csharp
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
- **Spatialization**: Enable/disable 3D audio (`SetSpatialization`) for positional or ambient effects.
- **Priority**: Use `AudioPriority` (Low, Medium, High) to prioritize critical sounds. High-priority audio can evict lower-priority speakers or be queued if IDs are unavailable.

## API Reference

### Key Classes and Interfaces

| Name                     | Namespace                             | Description                                                                 |
|--------------------------|---------------------------------------|-----------------------------------------------------------------------------|
| `IAudioManager`          | `AudioManagerAPI.Features.Management` | Defines the contract for audio playback and speaker lifecycle management.    |
| `AudioManager`           | `AudioManagerAPI.Features.Management` | Implements audio management with caching and shared controller IDs.          |
| `ISpeaker`               | `AudioManagerAPI.Features.Speakers`   | Represents a speaker for playing audio samples at a position.                |
| `ISpeakerWithPlayerFilter` | `AudioManagerAPI.Features.Speakers`   | Extends `ISpeaker` to support player-specific audibility, volume, range, and spatialization. |
| `ISpeakerFactory`        | `AudioManagerAPI.Features.Speakers`   | Defines a factory for creating speaker instances.                            |
| `AudioCache`             | `AudioManagerAPI.Cache`               | Manages audio samples with LRU eviction and lazy loading.                    |
| `ControllerIdManager`     | `AudioManagerAPI`                    | Static class for managing unique controller IDs with priority-based eviction and queuing. |
| `AudioPriority`          | `AudioManagerAPI`                    | Enum defining audio priority levels (Low, Medium, High).                     |

### Important Methods

- **`IAudioManager.RegisterAudio(string key, Func<Stream> streamProvider)`**: Registers a WAV stream for lazy loading.
- **`IAudioManager.PlayAudio(string key, Vector3 position, bool loop, float volume, float minDistance, float maxDistance, bool isSpatial, AudioPriority priority, Action<ISpeaker> configureSpeaker)`**: Plays audio at a position with optional configuration.
- **`IAudioManager.PlayGlobalAudio(string key, Vector3 position, bool loop, float volume, float minDistance, float maxDistance, bool isSpatial, AudioPriority priority)`**: Plays audio globally, audible to all players.
- **`IAudioManager.StopAudio(byte controllerId)`**: Stops audio for a specific speaker.
- **`IAudioManager.DestroySpeaker(byte controllerId)`**: Destroys a speaker and releases its ID.
- **`IAudioManager.CleanupAllSpeakers()`**: Cleans up all active speakers and releases their IDs.
- **`IAudioManager.GetSpeaker(byte controllerId)`**: Retrieves a speaker instance for further configuration.

## Notes

- **Controller ID Synchronization**: `ControllerIdManager` ensures no ID conflicts by maintaining a shared pool of IDs (1-255). High-priority audio can evict lower-priority speakers or be queued for later allocation.
- **Thread Safety**: All operations (ID allocation, caching, speaker management) are thread-safe using locks.
- **Dependencies**: Requires `UnityEngine.CoreModule` for `Vector3` and `LabApi` for `SpeakerToy`. Ensure these are available in your SCP:SL environment.
- **Logging**: Use your plugin’s logging system (e.g., Exiled’s `Log`) for debugging playback, prioritization, or resource errors.
- **Spatial Audio**: Use `isSpatial: true` for positional effects (e.g., screams) and `isSpatial: false` for ambient sounds (e.g., background music).

## Contributing

Contributions are welcome! Please submit issues or pull requests to the [GitHub repository](https://github.com/ioMatix/SCPSL-AudioManagerAPI).

## License

This project is licensed under the GNU Lesser General Public License v3.0 (LGPL3). See the [LICENSE](LICENSE) file for details.