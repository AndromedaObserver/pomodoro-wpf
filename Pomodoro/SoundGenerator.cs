using System;
using System.IO;
using System.Reflection;

namespace Pomodoro
{
    /// <summary>
    /// 程序化生成 WAV 音频数据，无需外部音频文件。
    /// 所有声音通过纯数学合成生成。
    /// </summary>
    public static class SoundGenerator
    {
        private const int SampleRate = 44100;
        private const short BitsPerSample = 16;
        private const short Channels = 1;

        /// <summary>
        /// 将 float[] samples（范围 -1..1）转为 WAV 字节数组
        /// </summary>
        private static byte[] EncodeWav(float[] samples)
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);

            int dataSize = samples.Length * 2; // 16-bit = 2 bytes per sample
            int fileSize = 36 + dataSize;

            // RIFF header
            writer.Write(new[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' });
            writer.Write(fileSize);
            writer.Write(new[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E' });

            // fmt chunk
            writer.Write(new[] { (byte)'f', (byte)'m', (byte)'t', (byte)' ' });
            writer.Write(16); // chunk size
            writer.Write((short)1); // PCM
            writer.Write(Channels);
            writer.Write(SampleRate);
            writer.Write(SampleRate * Channels * BitsPerSample / 8); // byte rate
            writer.Write((short)(Channels * BitsPerSample / 8)); // block align
            writer.Write(BitsPerSample);

            // data chunk
            writer.Write(new[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' });
            writer.Write(dataSize);

            foreach (float s in samples)
            {
                short val = (short)Math.Clamp(s * 32767f, -32768f, 32767f);
                writer.Write(val);
            }

            writer.Flush();
            return ms.ToArray();
        }

        /// <summary>
        /// Create a WAV byte array that loops seamlessly
        /// </summary>
        private static byte[] LoopableWav(float[] samples, int crossfadeSamples = 256)
        {
            // Add crossfade at loop point to avoid clicks
            int len = samples.Length;
            if (crossfadeSamples > len / 2) crossfadeSamples = len / 4;

            float[] loopable = new float[len];
            Array.Copy(samples, loopable, len);

            for (int i = 0; i < crossfadeSamples; i++)
            {
                float fadeIn = (float)i / crossfadeSamples;
                float fadeOut = 1f - fadeIn;
                // blend end into beginning
                loopable[i] = samples[i] * fadeIn + samples[len - crossfadeSamples + i] * fadeOut;
                loopable[len - crossfadeSamples + i] = loopable[len - crossfadeSamples + i] * fadeOut + samples[i] * fadeIn;
            }

            return EncodeWav(loopable);
        }

        #region ===== 环境音（循环播放） =====

        /// <summary>
        /// 滴答声 - 每秒钟一个轻柔的滴答
        /// </summary>
        public static byte[] GenerateTickTock(double bpm = 60, int totalSeconds = 4)
        {
            int totalSamples = SampleRate * totalSeconds;
            int tickInterval = (int)(SampleRate * 60.0 / bpm);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                int posInCycle = i % tickInterval;
                // Short tick: 800Hz sine with fast decay
                if (posInCycle < SampleRate * 0.04) // 40ms tick
                {
                    float t = (float)posInCycle / SampleRate;
                    float envelope = (float)Math.Exp(-t * 60); // fast decay
                    samples[i] = (float)(Math.Sin(2 * Math.PI * 800 * t) * envelope * 0.3);
                }
            }

            return LoopableWav(samples);
        }

        /// <summary>
        /// 从 Resources/Sounds 目录加载真实雨声 WAV 文件
        /// </summary>
        public static byte[] LoadRainWav()
        {
            string path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Resources", "Sounds", "ambient_rain.wav");
            if (File.Exists(path))
                return File.ReadAllBytes(path);
            return null;
        }

        /// <summary>
        /// 从 Resources/Sounds 目录加载真实海浪声 WAV 文件
        /// </summary>
        public static byte[] LoadOceanWav()
        {
            string path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Resources", "Sounds", "ambient_ocean.wav");
            if (File.Exists(path))
                return File.ReadAllBytes(path);
            return null;
        }

        /// <summary>
        /// 从 Resources/Sounds 目录加载真实溪流声 WAV 文件
        /// </summary>
        public static byte[] LoadRiverWav()
        {
            string path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Resources", "Sounds", "ambient_river.wav");
            if (File.Exists(path))
                return File.ReadAllBytes(path);
            return null;
        }

        /// <summary>
        /// 从 Resources/Sounds 目录加载真实风声 WAV 文件
        /// </summary>
        public static byte[] LoadWindWav()
        {
            string path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Resources", "Sounds", "ambient_wind.wav");
            if (File.Exists(path))
                return File.ReadAllBytes(path);
            return null;
        }

        /// <summary>
        /// 从 Resources/Sounds 目录加载真实篝火声 WAV 文件
        /// </summary>
        public static byte[] LoadCampfireWav()
        {
            string path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Resources", "Sounds", "ambient_campfire.wav");
            if (File.Exists(path))
                return File.ReadAllBytes(path);
            return null;
        }

        /// <summary>
        /// 布朗噪声 - 低沉轰鸣，适合专注
        /// </summary>
        public static byte[] GenerateBrownNoise(int durationSeconds = 5)
        {
            int totalSamples = SampleRate * durationSeconds;
            float[] samples = new float[totalSamples];
            float value = 0;
            Random rng = new(99);

            for (int i = 0; i < totalSamples; i++)
            {
                // Brownian noise: integrate white noise
                value += (float)(rng.NextDouble() - 0.5) * 0.02f;
                value = Math.Clamp(value, -0.25f, 0.25f);
                samples[i] = value * 0.5f;
            }

            return LoopableWav(samples);
        }

        #endregion

        #region ===== 闹钟音（一次性播放） =====

        /// <summary>
        /// 经典闹钟 - 响亮交替频率（类似传统闹钟）
        /// </summary>
        public static byte[] GenerateAlarmClassic(int durationMs = 5000)
        {
            int totalSamples = SampleRate * durationMs / 1000;
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / SampleRate;
                // Alternating between 880Hz and 1100Hz every 250ms
                double freq = (i / (SampleRate / 4)) % 2 == 0 ? 880 : 1100;
                samples[i] = (float)(Math.Sin(2 * Math.PI * freq * t)) * 0.8f;
            }

            return EncodeWav(samples);
        }

        /// <summary>
        /// 轻柔铃铛 - 衰减音（仿门铃）
        /// </summary>
        public static byte[] GenerateAlarmBell(int durationMs = 4000)
        {
            int totalSamples = SampleRate * durationMs / 1000;
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / SampleRate;
                // Two frequencies with different decay rates for bell effect
                float env1 = (float)Math.Exp(-t * 0.8);
                float env2 = (float)Math.Exp(-t * 1.5);
                samples[i] = (float)(Math.Sin(2 * Math.PI * 880 * t) * env1 * 0.5 +
                                     Math.Sin(2 * Math.PI * 1320 * t) * env2 * 0.3);
            }

            return EncodeWav(samples);
        }

        /// <summary>
        /// 数字闹钟 - 哔哔声节奏
        /// </summary>
        public static byte[] GenerateAlarmDigital(int durationMs = 5000)
        {
            int totalSamples = SampleRate * durationMs / 1000;
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / SampleRate;
                // Beep pattern: 200ms beep, 200ms silence
                double beepPhase = (i / (SampleRate / 5)) % 2;
                float beep = beepPhase < 1 ? (float)Math.Sin(2 * Math.PI * 1000 * t) : 0;
                samples[i] = beep * 0.7f;
            }

            return EncodeWav(samples);
        }

        /// <summary>
        /// 风铃 - 上升音阶
        /// </summary>
        public static byte[] GenerateAlarmChime(int durationMs = 4000)
        {
            int totalSamples = SampleRate * durationMs / 1000;
            float[] samples = new float[totalSamples];

            double[] chimeFreqs = { 523, 659, 784, 1047 }; // C5, E5, G5, C6

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / SampleRate;
                float sample = 0;
                for (int c = 0; c < chimeFreqs.Length; c++)
                {
                    double delay = c * 0.15; // stagger start
                    if (t > delay)
                    {
                        float env = (float)Math.Exp(-(t - delay) * 0.6);
                        sample += (float)(Math.Sin(2 * Math.PI * chimeFreqs[c] * (t - delay)) * env * 0.2);
                    }
                }
                samples[i] = sample;
            }

            return EncodeWav(samples);
        }

        #endregion

        #region ===== 辅助方法 =====

        /// <summary>
        /// 根据环境音名称获取 WAV 字节数组
        /// </summary>
        public static byte[] GetAmbientSound(string name)
        {
            return name switch
            {
                "tick" => GenerateTickTock(),
                "rain" => LoadRainWav(),
                "ocean" => LoadOceanWav(),
                "river" => LoadRiverWav(),
                "wind" => LoadWindWav(),
                "campfire" => LoadCampfireWav(),
                "brown" => GenerateBrownNoise(),
                _ => null
            };
        }

        /// <summary>
        /// 根据闹钟音名称获取 WAV 字节数组
        /// </summary>
        public static byte[] GetAlarmSound(string name)
        {
            return name switch
            {
                "classic" => GenerateAlarmClassic(),
                "bell" => GenerateAlarmBell(),
                "digital" => GenerateAlarmDigital(),
                "chime" => GenerateAlarmChime(),
                _ => null
            };
        }

        /// <summary>
        /// 获取所有环境音名称列表
        /// </summary>
        public static string[] AmbientSoundNames => new[] { "无", "滴答声", "下雨声", "海浪声", "溪流声", "风声", "篝火声", "布朗噪音" };
        public static string[] AmbientSoundKeys => new[] { "", "tick", "rain", "ocean", "river", "wind", "campfire", "brown" };

        /// <summary>
        /// 获取所有闹钟音名称列表
        /// </summary>
        public static string[] AlarmSoundNames => new[] { "经典闹钟", "轻柔铃铛", "数字哔哔", "风铃" };
        public static string[] AlarmSoundKeys => new[] { "classic", "bell", "digital", "chime" };

        #endregion
    }
}
