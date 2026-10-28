using UnityEngine;

namespace NeonRelay
{
    // Original synthesized assets: no downloads, external samples, or licensing dependencies.
    public sealed class RelayAudio : MonoBehaviour
    {
        AudioSource music, effects;
        AudioClip[] cues;
        public float MusicVolume { get; private set; }
        public float EffectsVolume { get; private set; }
        public void Initialize()
        {
            music = gameObject.AddComponent<AudioSource>(); effects = gameObject.AddComponent<AudioSource>();
            music.loop = true; music.clip = CreateMusic();
            cues = new[] { Tone("Jump", 330, 880, .17f), Tone("Slide", 180, 60, .23f),
                Tone("Data cell", 660, 1320, .19f), Tone("Signal lost", 220, 35, .65f) };
            SetMusic(PlayerPrefs.GetFloat("NeonRelay.Music", .35f));
            SetEffects(PlayerPrefs.GetFloat("NeonRelay.Effects", .65f)); music.Play();
        }
        public void SetMusic(float value) { MusicVolume = value; music.volume = value * .5f; PlayerPrefs.SetFloat("NeonRelay.Music", value); }
        public void SetEffects(float value) { EffectsVolume = value; effects.volume = value; PlayerPrefs.SetFloat("NeonRelay.Effects", value); }
        public void Cue(int index) { effects.PlayOneShot(cues[index], .45f); }
        static AudioClip Tone(string name, float from, float to, float duration)
        {
            int count = (int)(22050 * duration); float[] samples = new float[count]; float phase = 0;
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / count; phase += Mathf.Lerp(from, to, t) * 2 * Mathf.PI / 22050;
                samples[i] = Mathf.Sin(phase) * (1 - t) * Mathf.Min(t * 40, 1) * .6f;
            }
            var clip = AudioClip.Create(name, count, 1, 22050, false); clip.SetData(samples, 0); return clip;
        }
        static AudioClip CreateMusic()
        {
            const int rate = 22050; const float beat = .5f; float[] data = new float[rate * 16];
            float[] notes = { 110, 130.81f, 164.81f, 146.83f, 110, 164.81f, 196, 146.83f };
            for (int i = 0; i < data.Length; i++)
            {
                float t = (float)i / rate, pulse = t % beat; int n = (int)(t / beat);
                float bass = Mathf.Sin(2 * Mathf.PI * notes[(n / 4) % 8] * t) * Mathf.Exp(-pulse * 7) * .18f;
                float arp = Mathf.Sin(2 * Mathf.PI * notes[n % 8] * 4 * t) * Mathf.Exp(-(t % .25f) * 18) * .075f;
                float kick = Mathf.Sin(2 * Mathf.PI * (45 * pulse + 6 * (1 - Mathf.Exp(-pulse * 30)))) * Mathf.Exp(-pulse * 24) * .22f;
                data[i] = (bass + arp + kick) * Mathf.Min(1, t * 30) * Mathf.Min(1, (16 - t) * 30);
            }
            var clip = AudioClip.Create("Orbital pulse - original procedural soundtrack", data.Length, 1, rate, false);
            clip.SetData(data, 0); return clip;
        }
        void OnApplicationQuit() { PlayerPrefs.Save(); }
    }
}
