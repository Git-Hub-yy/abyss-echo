using System;
using UnityEngine;

namespace AbyssEcho.Audio
{
    /// <summary>
    /// 程序化音频合成器：运行时生成 AudioClip，无需任何外部素材。
    ///
    /// 为什么程序化：
    ///   · 项目目前没有任何音频素材，也没有可靠的 CC0 恐怖音效来源
    ///   · 合成参数可控，同一套代码能出多种变体（不同房间的心跳、不同材质的脚步）
    ///   · 无版权风险、无下载依赖
    ///
    /// 所有合成都是纯采样计算（正弦/噪声/包络），没有使用第三方库。
    /// </summary>
    public static class ProceduralAudio
    {
        private const int SampleRate = 44100;

        // ────────────────────────── 基础工具 ──────────────────────────

        /// <summary>把 float 采样数组打包成 AudioClip。</summary>
        private static AudioClip MakeClip(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>线性淡入淡出包络。</summary>
        private static float Envelope(float t, float duration, float attack, float release)
        {
            if (t < attack) return t / Mathf.Max(0.0001f, attack);
            if (t > duration - release) return Mathf.Max(0f, (duration - t) / Mathf.Max(0.0001f, release));
            return 1f;
        }

        /// <summary>指数衰减包络。</summary>
        private static float ExpDecay(float t, float decay)
        {
            return Mathf.Exp(-t * decay);
        }

        /// <summary>简单的确定性噪声（不用 Random，保证每次生成一致）。</summary>
        private static float Noise(ref uint state)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return (state & 0xFFFFFF) / 8388608f - 1f;   // -1..1
        }

        // ────────────────────────── 心跳 ──────────────────────────

        /// <summary>
        /// 心跳声：两下低沉脉冲（"咚-咚"），模拟紧张时的心跳。
        /// 用于玩家恐惧值高时循环播放。
        /// </summary>
        public static AudioClip CreateHeartbeat(float bpm = 90f, float intensity = 1f)
        {
            float period = 60f / Mathf.Clamp(bpm, 40f, 180f);
            float beat = 0.10f;                       // 单次搏动时长
            float firstGap = period * 0.32f;          // 第二下距离第一下的时间
            int total = Mathf.CeilToInt(period * SampleRate);
            var data = new float[total];

            void AddThump(float startSec, float amp, float freq)
            {
                int start = Mathf.RoundToInt(startSec * SampleRate);
                int len = Mathf.RoundToInt(beat * SampleRate);
                for (int i = 0; i < len && start + i < data.Length; i++)
                {
                    float t = i / (float)SampleRate;
                    // 低频正弦 + 快速衰减 = 胸腔搏动感
                    float s = Mathf.Sin(2f * Mathf.PI * freq * t);
                    s *= ExpDecay(t, 28f);
                    data[start + i] += s * amp;
                }
            }

            float a = Mathf.Clamp01(intensity);
            AddThump(0f, 0.85f * a, 52f);
            AddThump(firstGap, 0.62f * a, 46f);

            return MakeClip("Heartbeat", data);
        }

        // ────────────────────────── 脚步 ──────────────────────────

        /// <summary>
        /// 脚步：短促噪声脉冲 + 低频撞击。
        /// metalSurface=true 时使用更强的金属高频泛音（走廊是金属地面）。
        /// </summary>
        public static AudioClip CreateFootstep(bool metalSurface, int variant = 0)
        {
            float dur = metalSurface ? 0.16f : 0.12f;
            int len = Mathf.RoundToInt(dur * SampleRate);
            var data = new float[len];
            uint seed = (uint)(0x1234 + variant * 7919 + (metalSurface ? 1 : 0));

            float lp = 0f;   // 一阶低通状态
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)SampleRate;
                float env = ExpDecay(t, metalSurface ? 34f : 46f);

                float n = Noise(ref seed);
                // 低通滤波：metal 保留更多高频，混凝土更闷
                lp += (n - lp) * (metalSurface ? 0.55f : 0.22f);

                float body = Mathf.Sin(2f * Mathf.PI * (metalSurface ? 120f : 85f) * t) * 0.55f;
                float click = metalSurface
                    ? Mathf.Sin(2f * Mathf.PI * 2400f * t) * 0.16f * ExpDecay(t, 90f)
                    : 0f;

                data[i] = (lp * 0.75f + body + click) * env * 0.85f;
            }

            return MakeClip(metalSurface ? "Footstep_Metal" : "Footstep_Concrete", data);
        }

        // ────────────────────────── 门 ──────────────────────────

        /// <summary>金属门滑动声：低频嗡嗡 + 摩擦噪声，持续约 0.8 秒。</summary>
        public static AudioClip CreateDoorSlide(float duration = 0.8f)
        {
            int len = Mathf.RoundToInt(duration * SampleRate);
            var data = new float[len];
            uint seed = 0xBEEF;

            float lp = 0f;
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)SampleRate;
                float p = t / duration;

                // 起停都软，避免爆音
                float env = Mathf.Min(1f, p * 8f) * Mathf.Min(1f, (1f - p) * 8f);

                float n = Noise(ref seed);
                lp += (n - lp) * 0.30f;

                // 电机嗡嗡：频率随进程轻微上扬
                float motor = Mathf.Sin(2f * Mathf.PI * (92f + p * 18f) * t) * 0.34f;
                // 摩擦：带通后的噪声
                float friction = (n - lp) * 0.20f;

                data[i] = (motor + friction) * env * 0.9f;
            }

            return MakeClip("Door_Slide", data);
        }

        // ────────────────────────── 突发惊吓 ──────────────────────────

        /// <summary>
        /// 突发惊吓（Jumpscare）：不协和高频弦音 + 噪声冲击，快速衰减。
        /// 使用时音量必须压低（>= -12dB），避免损伤玩家听觉。
        /// </summary>
        public static AudioClip CreateStinger(float duration = 0.9f)
        {
            int len = Mathf.RoundToInt(duration * SampleRate);
            var data = new float[len];
            uint seed = 0x5EED;

            float lp = 0f;
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)SampleRate;
                float env = ExpDecay(t, 3.2f);

                // 不协和组合：小二度 + 三全音感
                float s = 0f;
                s += Mathf.Sin(2f * Mathf.PI * 311f * t) * 0.30f;
                s += Mathf.Sin(2f * Mathf.PI * 329f * t) * 0.26f;
                s += Mathf.Sin(2f * Mathf.PI * 466f * t) * 0.22f;
                s += Mathf.Sin(2f * Mathf.PI * 622f * t) * 0.14f;

                // 噪声冲击（前 80ms 最猛）
                float n = Noise(ref seed);
                lp += (n - lp) * 0.6f;
                float burst = lp * 0.5f * Mathf.Clamp01(1f - t / 0.08f);

                data[i] = Mathf.Clamp((s + burst) * env, -1f, 1f) * 0.8f;
            }

            return MakeClip("Stinger", data);
        }

        // ────────────────────────── 环境底噪 ──────────────────────────

        /// <summary>
        /// 深潜舱环境底噪：极低频嗡鸣 + 缓慢起伏的水压噪声。
        /// 循环播放，音量很低，用于填充"死寂"。
        /// </summary>
        public static AudioClip CreateAmbientDrone(float duration = 8f)
        {
            int len = Mathf.RoundToInt(duration * SampleRate);
            var data = new float[len];
            uint seed = 0xD00D;

            float lp = 0f;
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)SampleRate;
                float p = t / duration;

                // 低频嗡鸣（两个相近频率产生缓慢拍频）
                float drone = Mathf.Sin(2f * Mathf.PI * 43f * t) * 0.22f
                            + Mathf.Sin(2f * Mathf.PI * 46.5f * t) * 0.18f;

                // 水压噪声
                float n = Noise(ref seed);
                lp += (n - lp) * 0.02f;
                float water = lp * 0.55f;

                // 缓慢起伏
                float swell = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * 0.12f * t);

                // 首尾交叉淡化，保证无缝循环
                float loopEnv = 1f;
                float fade = 0.5f;
                if (t < fade) loopEnv = t / fade;
                else if (t > duration - fade) loopEnv = (duration - t) / fade;

                data[i] = (drone + water) * swell * loopEnv * 0.6f;
            }

            return MakeClip("Ambient_Drone", data);
        }

        // ────────────────────────── 提示音 ──────────────────────────

        /// <summary>准星提示音：极短的高频"滴"声，交互可用时播放。</summary>
        public static AudioClip CreateUiBlip(float freq = 1200f, float duration = 0.06f)
        {
            int len = Mathf.RoundToInt(duration * SampleRate);
            var data = new float[len];
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)SampleRate;
                float env = Envelope(t, duration, 0.004f, 0.03f);
                data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.25f;
            }
            return MakeClip("UI_Blip", data);
        }

        /// <summary>错误音：两声低沉的"嗡"，用于密码错误。</summary>
        public static AudioClip CreateErrorBuzz()
        {
            float dur = 0.30f;
            int len = Mathf.RoundToInt(dur * SampleRate);
            var data = new float[len];
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)SampleRate;
                float env = Envelope(t, dur, 0.01f, 0.08f);
                // 方波感（奇次谐波）更刺耳，符合"错误"语义
                float s = Mathf.Sin(2f * Mathf.PI * 160f * t)
                        + Mathf.Sin(2f * Mathf.PI * 480f * t) * 0.33f
                        + Mathf.Sin(2f * Mathf.PI * 800f * t) * 0.20f;
                data[i] = s * env * 0.22f;
            }
            return MakeClip("Error_Buzz", data);
        }

        /// <summary>
        /// 磁带录音播放时的"人声质感"音频。
        ///
        /// ⚠️ 为什么需要（玩家实测"录音没声音"）：
        ///   TapeRecorder 原来只播放了一声 UI 提示音，
        ///   之后就全是文字——没有任何"这是录音"的听觉线索。
        ///
        /// 做法（不依赖真人配音）：
        ///   · 用带调制的共振峰（formant）模拟说话的抑扬
        ///   · 叠加磁带底噪（60Hz 交流声 + 高频嘶声）
        ///   · 加轻微的"转速不稳"（wow & flutter）
        ///   听感上是"隔着磁带听到有人在说话"，但听不清具体内容——
        ///   正好符合这个游戏的调性（你听到的是回声，不是录音）。
        ///
        /// seed 不同则音高起伏不同，两段录音听起来不一样。
        /// </summary>
        public static AudioClip CreateTapeVoice(string seed, float duration = 10f)
        {
            int len = Mathf.RoundToInt(duration * SampleRate);
            var data = new float[len];

            // 用 seed 生成稳定的随机序列（同一段录音每次听都一样）
            var rng = new System.Random(seed != null ? seed.GetHashCode() : 0);

            // 预先规划一串"音节"：每段 0.12~0.35 秒，中间有停顿
            var syllables = new System.Collections.Generic.List<(float start, float dur, float freq, float amp)>();
            float t0 = 0.3f;
            while (t0 < duration - 0.5f)
            {
                float d = 0.12f + (float)rng.NextDouble() * 0.23f;
                float f = 95f + (float)rng.NextDouble() * 85f;    // 基频 95~180Hz（低沉男声区）
                float a = 0.55f + (float)rng.NextDouble() * 0.45f;
                syllables.Add((t0, d, f, a));

                // 音节之间：大部分短停顿，偶尔长停顿（换气）
                float gap = (rng.NextDouble() < 0.18) ? 0.35f + (float)rng.NextDouble() * 0.4f
                                                       : 0.02f + (float)rng.NextDouble() * 0.08f;
                t0 += d + gap;
            }

            for (int i = 0; i < len; i++)
            {
                float t = i / (float)SampleRate;
                float sample = 0f;

                // ① "说话"部分：共振峰合成
                foreach (var s in syllables)
                {
                    if (t < s.start || t > s.start + s.dur) continue;

                    float lt = t - s.start;
                    float env = Envelope(lt, s.dur, 0.02f, 0.05f);

                    // wow & flutter：磁带转速不稳，让音高轻微抖动
                    float wow = 1f + Mathf.Sin(2f * Mathf.PI * 0.7f * t) * 0.012f
                                   + Mathf.Sin(2f * Mathf.PI * 5.3f * t) * 0.004f;
                    float f0 = s.freq * wow;

                    // 基频 + 前三个共振峰（元音的听觉特征）
                    float v = Mathf.Sin(2f * Mathf.PI * f0 * t) * 0.55f
                            + Mathf.Sin(2f * Mathf.PI * f0 * 2.1f * t) * 0.28f
                            + Mathf.Sin(2f * Mathf.PI * f0 * 3.4f * t) * 0.14f
                            + Mathf.Sin(2f * Mathf.PI * f0 * 5.2f * t) * 0.06f;

                    sample += v * env * s.amp;
                }

                // ② 磁带底噪：交流声 + 嘶声
                float hum = Mathf.Sin(2f * Mathf.PI * 50f * t) * 0.006f;
                float hiss = ((float)rng.NextDouble() * 2f - 1f) * 0.008f;

                // ③ 整体包络（开头淡入、结尾淡出）
                float masterEnv = Envelope(t, duration, 0.15f, 0.4f);

                data[i] = Mathf.Clamp((sample * 0.30f + hum + hiss) * masterEnv, -1f, 1f);
            }

            return MakeClip("Tape_Voice_" + (seed ?? "0"), data);
        }

        /// <summary>磁带插入/开始播放时的机械声（咔哒 + 转动）。</summary>
        public static AudioClip CreateTapeClick(float duration = 0.45f)
        {
            int len = Mathf.RoundToInt(duration * SampleRate);
            var data = new float[len];
            var rng = new System.Random(3141);

            for (int i = 0; i < len; i++)
            {
                float t = i / (float)SampleRate;
                float s = 0f;

                // 前 60ms：机械咔哒（高频瞬态）
                if (t < 0.06f)
                {
                    float env = Envelope(t, 0.06f, 0.002f, 0.05f);
                    s += ((float)rng.NextDouble() * 2f - 1f) * env * 0.35f;
                }

                // 之后：马达转动（低频嗡 + 轻微摩擦）
                if (t > 0.08f)
                {
                    float mt = t - 0.08f;
                    float env = Envelope(mt, duration - 0.08f, 0.05f, 0.2f);
                    s += Mathf.Sin(2f * Mathf.PI * 78f * mt) * env * 0.12f;
                    s += ((float)rng.NextDouble() * 2f - 1f) * env * 0.02f;
                }

                data[i] = Mathf.Clamp(s, -1f, 1f);
            }
            return MakeClip("Tape_Click", data);
        }
    }
}