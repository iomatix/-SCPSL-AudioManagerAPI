# SCPSL-AudioManagerAPI — Changelog

## 2.1.0 — Real-Time PCM Streaming

**Release focus:** dynamic audio, proximity voice, and live PCM pipelines.

### Added

- `IAudioManager.AppendPcmData(int sessionId, short[] pcm)`  
  - Appends raw 48kHz, mono, 16-bit PCM buffers to an existing session.  
  - Enables real-time streaming for proximity voice chat, synthesized speech, and procedural audio.

- `ISpeaker.AppendPcm(short[] pcm)`  
  - Low-level method for appending PCM directly to the hardware speaker buffer.  
  - Implemented by the default LabAPI `SpeakerToy` adapter.

- `SpeakerState.PcmQueue`  
  - FIFO queue of PCM buffers stored per session.  
  - Used when a session is evicted from hardware and later recovered.

### Behavior

- If a session has an active physical speaker, PCM is forwarded immediately.  
- If a session is evicted, PCM is queued in `SpeakerState.PcmQueue`.  
- When the session is recovered, queued PCM is played automatically.

### Compatibility

- Fully backwards compatible with 2.0.0 for static audio playback.  
- No changes required for existing plugins that do not use real-time streaming.

### Use-cases

- SCP proximity voice chat  
- Live microphone input  
- AI-generated speech  
- Dynamic and procedural sound effects  

---

## 2.0.0 — Session-Based Architecture Overhaul

- Replaced hardware Controller IDs (`byte`) with abstract Session IDs (`int`).  
- Introduced `ControllerIdManager` for priority-based hardware allocation and eviction.  
- Added `AudioCache` for lazy-loading and caching decoded audio.  
- Removed direct public access to `ISpeaker` to ensure thread safety and state persistence.  
- Added support for:
  - spatial audio  
  - per-session volume, distance, and filters  
  - hardware eviction and recovery  
  - auto-cleanup and lifespan-based sessions  

**Breaking changes (from 1.9.x):**

- All APIs now use `int sessionId` instead of `byte controllerId`.  
- `DestroySpeaker(id)` replaced by `Stop(id)` or `DestroySession(id)`.  
- `Action<ISpeaker>` configuration delegates removed in favor of `validPlayersFilter`.

---

## 1.x — Initial Releases

- Basic audio playback via LabAPI `SpeakerToy`.  
- Global and spatial playback.  
- Simple caching and basic control (play, stop, loop).  

*(Older versions summarized; see git history for full details.)*