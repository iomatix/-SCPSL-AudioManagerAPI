using AudioManagerAPI.Features.Management;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace AudioManagerAPI.Features.Extensions
{
    /// <summary>
    /// Provides advanced high-performance utility extensions to automate embedded manifest assembly resource registration onto the Audio Engine.
    /// </summary>
    internal static class AudioRegistryExtensions
    {
        /// <summary>
        /// Attempts to locate and register an individual embedded .wav audio asset from the assembly manifest into the core audio engine.
        /// </summary>
        /// <param name="audioEngine">The active <see cref="IAudioManager"/> system engine engine core layer instance.</param>
        /// <param name="assembly">The targeted compiled <see cref="Assembly"/> matrix housing the embedded binary audio file stream.</param>
        /// <param name="audioKey">The unique target tracking key lookup string profile configuration (e.g. "drs.siren_loop").</param>
        /// <returns><c>true</c> if the embedded resource stream was successfully discovered and registered cleanly; otherwise, <c>false</c>.</returns>
        public static bool TryRegisterEmbeddedResource(this IAudioManager audioEngine, Assembly assembly, string audioKey)
        {
            if (audioEngine is null || assembly is null || string.IsNullOrEmpty(audioKey)) return false;

            string[] resourceNames = assembly.GetManifestResourceNames();
            int count = resourceNames.Length;

            for (int i = 0; i < count; i++)
            {
                string res = resourceNames[i];

                // Perform safe casing-invariant validation passes checking against flat and normalized configuration layouts
                if (res.EndsWith($"{audioKey}.wav", StringComparison.OrdinalIgnoreCase) ||
                    res.EndsWith($"{audioKey.Replace(".", "_")}.wav", StringComparison.OrdinalIgnoreCase))
                {
                    audioEngine.RegisterAudio(audioKey, () => assembly.GetManifestResourceStream(res));
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Systematically scans the provided assembly manifest layout to register a mass array batch of audio resource keys simultaneously.
        /// </summary>
        /// <param name="audioEngine">The active <see cref="IAudioManager"/> system engine engine core layer instance.</param>
        /// <param name="assembly">The targeted compiled <see cref="Assembly"/> matrix housing the embedded binary audio assets.</param>
        /// <param name="audioKeys">The collection sequence tracking all explicit audio keys configured for deployment.</param>
        public static void RegisterEmbeddedResources(this IAudioManager audioEngine, Assembly assembly, IEnumerable<string> audioKeys)
        {
            if (audioEngine is null || assembly is null || audioKeys is null) return;

            foreach (string key in audioKeys)
            {
                if (!string.IsNullOrEmpty(key))
                {
                    audioEngine.TryRegisterEmbeddedResource(assembly, key);
                }
            }
        }
    }
}