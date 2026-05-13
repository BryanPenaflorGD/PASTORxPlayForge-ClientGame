using System.IO;
using System.Text;
using UnityEngine;

namespace IntuitiveCreative
{
    public static class IntuitiveAudioWavUtility
    {
        public static byte[] FromAudioClip(AudioClip clip)
        {
            using (var memoryStream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(memoryStream))
                {
                    int hz = clip.frequency;
                    int channels = clip.channels;
                    int samples = clip.samples;

                writer.Write(Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + samples * channels * 2);
                writer.Write(Encoding.ASCII.GetBytes("WAVE"));
                writer.Write(Encoding.ASCII.GetBytes("fmt "));
                writer.Write(16);
                writer.Write((ushort)1);
                writer.Write((ushort)channels);
                writer.Write(hz);
                writer.Write(hz * channels * 2);
                writer.Write((ushort)(channels * 2));
                writer.Write((ushort)16);
                writer.Write(Encoding.ASCII.GetBytes("data"));
                writer.Write(samples * channels * 2);

                float[] data = new float[samples * channels];
                clip.GetData(data, 0);

                    foreach (var sample in data)
                    {
                        writer.Write((short)(sample * 32767f));
                    }
                }

                return memoryStream.ToArray();
            }
        }
    }
}
