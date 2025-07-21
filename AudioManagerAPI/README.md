# AudioManagement

[![NuGet Version](https://img.shields.io/nuget/v/AudioManagement.svg)](https://www.nuget.org/packages/AudioManagement/)  
A lightweight, reusable C# library for managing audio playback in games, designed for SCP: Secret Laboratory plugins using LabAPI. It provides a robust system for loading, caching, and playing audio through speakers, with centralized controller ID management to prevent conflicts across multiple plugins.

## Features

- **Centralized Controller ID Management**: Ensures unique speaker IDs across all plugins using a shared `ControllerIdManager`.
- **LRU Audio Caching**: Efficiently manages audio samples with lazy loading and least-recently-used (LRU) eviction.
- **Flexible Speaker Abstraction**: Supports custom speaker implementations via `ISpeaker` and `ISpeakerFactory` interfaces.
- **Thread-Safe Operations**: Handles concurrent audio playback and resource management safely.
- **LabAPI Compatibility**: Optimized for SCP:SL plugins, integrating seamlessly with `SpeakerToy`.

## Installation

Install the `AudioManagement` package via NuGet:

```bash
dotnet add package AudioManagement --version 1.0.0
```

Or, in Visual Studio, use the NuGet Package Manager to search for `AudioManagement`.

## Usage

### 1. Setup in Your Project

Add the `AudioManagement` NuGet package to your SCP:SL plugin project. Ensure you have references to `UnityEngine.CoreModule` and `LabApi` for compatibility.

Example `.csproj` snippet:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net472</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="AudioManagement" Version="1.0.0" />
    <Reference Include="LabApi" />
    <Reference Include="UnityEngine.CoreModule" />
  </ItemGroup>
</Project>
```

### 2. Implement ISpeaker and ISpeakerFactory

Create a custom speaker implementation compatible with LabAPI's `SpeakerToy`.

```csharp
using AudioManagement;
using LabApi.Features.Wrappers;
using UnityEngine;

public class LabApiSpeaker : ISpeaker
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

### 3. Initialize AudioManager

Create an instance of `AudioManager` with your `ISpeakerFactory` and configure audio resources.

```csharp
using AudioManagement;
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

### 4. Play Audio

Use the `IAudioManager` to play audio at specific positions, with optional configuration.

```csharp
using UnityEngine;

public void PlayScream(Vector3 position, Player targetPlayer)
{
    byte? controllerId = audioManager.PlayAudio("myplugin.scream", position, false, speaker =>
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

### 5. Cleanup

Clean up speakers when done to free resources.

```csharp
audioManager.CleanupAllSpeakers();
```

## API Reference

### Key Classes and Interfaces

| Name                 | Description                                                                 |
|----------------------|-----------------------------------------------------------------------------|
| `IAudioManager`      | Defines the contract for managing audio playback and speaker lifecycle.      |
| `AudioManager`       | Implements audio management with caching and shared controller IDs.          |
| `ISpeaker`           | Represents a speaker that plays audio samples at a position.                 |
| `ISpeakerFactory`    | Defines a factory for creating speaker instances.                            |
| `AudioCache`         | Manages audio samples with LRU eviction and lazy loading.                    |
| `ControllerIdManager` | Static class for managing unique controller IDs across plugins.              |

### Important Methods

- **`IAudioManager.RegisterAudio(string key, Func<Stream> streamProvider)`**: Registers an audio stream for lazy loading.
- **`IAudioManager.PlayAudio(string key, Vector3 position, bool loop, Action<ISpeaker> configureSpeaker)`**: Plays audio at a position, with optional speaker configuration.
- **`IAudioManager.StopAudio(byte controllerId)`**: Stops audio for a specific speaker.
- **`IAudioManager.DestroySpeaker(byte controllerId)`**: Destroys a speaker and releases its ID.
- **`IAudioManager.CleanupAllSpeakers()`**: Cleans up all active speakers.

## Notes

- **Controller ID Synchronization**: The `ControllerIdManager` ensures no ID conflicts across plugins by maintaining a shared pool of IDs (1-255).
- **Thread Safety**: All operations are thread-safe, suitable for SCP:SL's multiplayer environment.
- **Dependencies**: Requires `UnityEngine.CoreModule` for `Vector3` and LabAPI for `SpeakerToy` integration.
- **Logging**: Use your plugin's logging system (e.g., Exiled's `Log`) for debugging.

## Contributing

Contributions are welcome! Please submit issues or pull requests to the [GitHub repository](https://github.com/yourusername/audiomanagement).

## License

This project is licensed under the LGPL3 License. See the [LICENSE](LICENSE) file for details.