namespace AudioManagerAPI.Decoders
{
    using NLayer;
    using System;
    using System.Buffers;
    using System.IO;

    /// <summary>
    /// Provides cross-platform, high-performance MPEG-1 Layer III (MP3) decoding capabilities
    /// using a fully managed parsing engine with zero native OS dependencies.
    /// </summary>
    internal static class Mp3Decoder
    {
        /// <summary>
        /// Decodes an MP3 binary stream directly into a rented float PCM array.
        /// Native Linux/Docker compliant - completely bypasses Windows ACM subsystems.
        /// </summary>
        /// <param name="mp3Stream">The source compressed input stream containing MPEG data.</param>
        /// <param name="totalSamples">The exact number of valid float samples written to the rented array.</param>
        /// <param name="sampleRate">The native sampling rate discovered in the MP3 frame headers.</param>
        /// <param name="channels">The channel count (e.g., 1 for Mono, 2 for Stereo) of the source asset.</param>
        /// <returns>A pooled float array containing the linear PCM data; must be returned to ArrayPool after usage.</returns>
        public static float[] DecodeMp3ToFloatRented(Stream mp3Stream, out int totalSamples, out int sampleRate, out int channels)
        {
            if (mp3Stream == null)
                throw new ArgumentNullException(nameof(mp3Stream));

            try
            {
                // NLayer operates entirely in managed memory, safeguarding against Linux DllNotFoundExceptions
                using (var mpegFile = new MpegFile(mp3Stream))
                {
                    sampleRate = mpegFile.SampleRate;
                    channels = mpegFile.Channels;

                    long sampleLength = mpegFile.Length;
                    if (sampleLength <= 0)
                    {
                        totalSamples = 0;
                        return null;
                    }

                    totalSamples = (int)sampleLength;

                    // Allocation Optimization: Rent the buffer directly to prevent transient heap spikes
                    float[] rentedBuffer = ArrayPool<float>.Shared.Rent(totalSamples);

                    // Decode and stream directly into the rented block without intermediate byte arrays
                    int samplesRead = mpegFile.ReadSamples(rentedBuffer, 0, totalSamples);

                    // Handle VBR (Variable Bitrate) size discrepancies or frame paddings safely
                    totalSamples = samplesRead;
                    return rentedBuffer;
                }
            }
            catch (Exception)
            {
                totalSamples = 0;
                sampleRate = 0;
                channels = 0;
                return null;
            }
        }
    }
}