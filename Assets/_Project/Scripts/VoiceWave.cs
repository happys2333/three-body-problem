using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace ThreeBodyProblem
{
    public static class VoiceWave
    {
        public static byte[] EncodeMono16(float[] samples, int sourceRate, int channels, int targetRate = 16000)
        {
            if (samples == null) throw new ArgumentNullException(nameof(samples));
            if (sourceRate < 1) throw new ArgumentOutOfRangeException(nameof(sourceRate));
            if (channels < 1) throw new ArgumentOutOfRangeException(nameof(channels));
            if (targetRate < 1) throw new ArgumentOutOfRangeException(nameof(targetRate));

            int sourceFrames = samples.Length / channels;
            int count = checked((int)((long)sourceFrames * targetRate / sourceRate));
            int dataBytes = checked(count * sizeof(short));
            using (var stream = new MemoryStream(checked(44 + dataBytes)))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + dataBytes);
                writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)1);
                writer.Write(targetRate);
                writer.Write(checked(targetRate * sizeof(short)));
                writer.Write((short)sizeof(short));
                writer.Write((short)16);
                writer.Write(Encoding.ASCII.GetBytes("data"));
                writer.Write(dataBytes);
                for (int i = 0; i < count; i++)
                {
                    // Convert before multiplying. A few seconds of hardware
                    // audio can already exceed the range of a 32-bit product.
                    double position = (double)i * sourceRate / targetRate;
                    int a = Math.Min(sourceFrames - 1, (int)position);
                    int b = Math.Min(sourceFrames - 1, a + 1);
                    float value = 0f;
                    for (int channel = 0; channel < channels; channel++)
                        value += Mathf.Lerp(samples[a * channels + channel], samples[b * channels + channel], (float)(position - a));
                    writer.Write((short)(Mathf.Clamp(value / channels, -1, 1) * 32767));
                }
                return stream.ToArray();
            }
        }
        public static AudioClip DecodePcm16(byte[] wav)
        {
            using(var reader=new BinaryReader(new MemoryStream(wav)))
            {
                if(Encoding.ASCII.GetString(reader.ReadBytes(4))!="RIFF")return null;reader.ReadInt32();if(Encoding.ASCII.GetString(reader.ReadBytes(4))!="WAVE")return null;
                int channels=1,rate=16000,bits=16,format=1;byte[] data=null;
                while(reader.BaseStream.Position+8<=reader.BaseStream.Length)
                {
                    string chunk=Encoding.ASCII.GetString(reader.ReadBytes(4));int bytes=reader.ReadInt32();if(bytes<0||reader.BaseStream.Position+bytes>reader.BaseStream.Length)return null;
                    long end=reader.BaseStream.Position+bytes;
                    if(chunk=="fmt "){format=reader.ReadInt16();channels=reader.ReadInt16();rate=reader.ReadInt32();reader.ReadInt32();reader.ReadInt16();bits=reader.ReadInt16();}
                    else if(chunk=="data")data=reader.ReadBytes(bytes);
                    reader.BaseStream.Position=end+(bytes%2);
                }
                if(data==null||bits!=16||format!=1||channels<1)return null;
                float[] samples=new float[data.Length/2];for(int i=0;i<samples.Length;i++)samples[i]=BitConverter.ToInt16(data,i*2)/32768f;
                var clip=AudioClip.Create("NPC speech",samples.Length/channels,channels,rate,false);clip.SetData(samples,0);return clip;
            }
        }
    }
}
