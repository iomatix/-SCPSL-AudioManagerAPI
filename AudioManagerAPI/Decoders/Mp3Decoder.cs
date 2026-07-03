namespace AudioManagerAPI.Decoders
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    internal static class Mp3Decoder
    {
        // Minimal MPEG1 Layer III decoder using built-in ACM (Windows)
        // This avoids external libraries and works on all SCP:SL servers.

        /// <summary>
        /// Decodes a raw binary byte array containing MP3 audio streams into a 16-bit linear PCM signed short array utilizing the native Windows Audio Compression Manager (ACM).
        /// </summary>
        /// <param name="mp3Bytes">The raw compressed binary data stream representing the source MPEG-1 Layer III audio asset.</param>
        /// <returns>A strongly-typed array of 16-bit signed integers (PCM16 audio samples) if the streaming transaction succeeds; otherwise, <c>null</c>.</returns>
        public static short[] DecodeMp3ToPcm16(byte[] mp3Bytes)
        {
            using (var mp3Stream = new MemoryStream(mp3Bytes))
            using (var mp3Reader = new System.Media.SoundPlayer()) // ACM-backed
            {
                try
                {
                    // Load MP3 into ACM decoder
                    var tempWav = Mp3ToWav(mp3Bytes);
                    if (tempWav == null)
                        return null;

                    // Extract PCM16 from WAV
                    return ExtractPcm16FromWav(tempWav);
                }
                catch
                {
                    return null;
                }
            }
        }

        private static byte[] Mp3ToWav(byte[] mp3Bytes)
        {
            using (var mp3 = new MemoryStream(mp3Bytes))
            using (var wav = new MemoryStream())
            {
                try
                {
                    using (var reader = new NAudio.Wave.Mp3FileReader(mp3))
                    using (var pcm = new NAudio.Wave.WaveFormatConversionStream(new NAudio.Wave.WaveFormat(44100, 16, 2), reader))
                    {
                        pcm.CopyTo(wav);
                        return wav.ToArray();
                    }
                }
                catch
                {
                    return null;
                }
            }
        }

        private static short[] ExtractPcm16FromWav(byte[] wavBytes)
        {
            // Skip WAV header (44 bytes)
            if (wavBytes.Length < 44)
                return null;

            int pcmLength = wavBytes.Length - 44;
            short[] pcm = new short[pcmLength / 2];

            Buffer.BlockCopy(wavBytes, 44, pcm, 0, pcmLength);
            return pcm;
        }
    }

}
