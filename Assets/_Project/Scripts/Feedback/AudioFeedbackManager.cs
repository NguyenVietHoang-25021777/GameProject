using System.Collections.Generic;
using UnityEngine;

namespace GameProject.Feedback
{
    /// <summary>
    /// Plays gameplay SFX via AudioSource.PlayOneShot with procedural AudioClip.Create fallbacks
    /// so every attack, hit, heal, and skill sounds responsive out of the box while allowing
    /// real .wav/.mp3 AudioClips to be dropped into the Inspector anytime.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class AudioFeedbackManager : MonoBehaviour
    {
        [Header("Replaceable SFX Clips (Optional - Procedural Fallbacks Auto-Generated)")]
        [SerializeField] private AudioClip attackSwingClip;
        [SerializeField] private AudioClip hitImpactClip;
        [SerializeField] private AudioClip healClip;
        [SerializeField] private AudioClip shockwaveClip;
        [SerializeField] private AudioClip lungeDashClip;
        [SerializeField] private AudioClip highLeapClip;

        [Header("Volume")]
        [Range(0f, 1f)] [SerializeField] private float masterVolume = 0.75f;

        private AudioSource _audioSource;
        private readonly List<AudioClip> _generatedClips = new List<AudioClip>();
        private bool _isPersistentOwner;

        private AudioClip _activeSwing;
        private AudioClip _activeHit;
        private AudioClip _activeHeal;
        private AudioClip _activeShockwave;
        private AudioClip _activeLunge;
        private AudioClip _activeLeap;

        private void Awake()
        {
            AudioFeedbackManager[] existing = FindObjectsByType<AudioFeedbackManager>(FindObjectsInactive.Include);
            for (int i = 0; i < existing.Length; i++)
            {
                if (existing[i] != this && existing[i]._isPersistentOwner)
                {
                    Destroy(gameObject);
                    return;
                }
            }

            _isPersistentOwner = true;
            DontDestroyOnLoad(gameObject);

            _audioSource = GetComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0f;

            InitializeClips();
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _generatedClips.Count; i++)
            {
                if (_generatedClips[i] != null)
                {
                    Destroy(_generatedClips[i]);
                }
            }

            _generatedClips.Clear();
        }

        public void PlayAttackSwing() => PlayClip(_activeSwing, 0.75f);
        public void PlayHitImpact() => PlayClip(_activeHit, 0.85f);
        public void PlayHeal() => PlayClip(_activeHeal, 0.8f);
        public void PlayShockwave() => PlayClip(_activeShockwave, 0.9f);
        public void PlayLungeDash() => PlayClip(_activeLunge, 0.8f);
        public void PlayHighLeap() => PlayClip(_activeLeap, 0.75f);

        private void PlayClip(AudioClip clip, float volumeScale)
        {
            if (_audioSource == null || clip == null)
            {
                return;
            }

            _audioSource.PlayOneShot(clip, masterVolume * volumeScale);
        }

        private void InitializeClips()
        {
            _activeSwing = attackSwingClip != null ? attackSwingClip : CreateSwingClip();
            _activeHit = hitImpactClip != null ? hitImpactClip : CreateHitImpactClip();
            _activeHeal = healClip != null ? healClip : CreateHealClip();
            _activeShockwave = shockwaveClip != null ? shockwaveClip : CreateShockwaveClip();
            _activeLunge = lungeDashClip != null ? lungeDashClip : CreateLungeClip();
            _activeLeap = highLeapClip != null ? highLeapClip : CreateLeapClip();
        }

        private AudioClip CreateSwingClip()
        {
            const int sampleRate = 44100;
            float duration = 0.14f;
            int length = Mathf.RoundToInt(sampleRate * duration);
            float[] data = new float[length];
            System.Random rng = new System.Random(101);
            float phase = 0f;
            float lpNoise = 0f;

            for (int i = 0; i < length; i++)
            {
                float t = (float)i / length;
                float env = Mathf.Sin(t * Mathf.PI) * (1f - t * 0.35f);
                float freq = Mathf.Lerp(440f, 150f, t);
                phase += 2f * Mathf.PI * freq / sampleRate;

                float rawNoise = (float)(rng.NextDouble() * 2.0 - 1.0);
                lpNoise = Mathf.Lerp(lpNoise, rawNoise, 0.18f);

                float sample = (Mathf.Sin(phase) * 0.35f + lpNoise * 0.65f) * env * 0.55f;
                data[i] = Mathf.Clamp(sample, -1f, 1f);
            }

            return RegisterClip("SFX_Procedural_Swing", data, sampleRate);
        }

        private AudioClip CreateHitImpactClip()
        {
            const int sampleRate = 44100;
            float duration = 0.13f;
            int length = Mathf.RoundToInt(sampleRate * duration);
            float[] data = new float[length];
            System.Random rng = new System.Random(202);
            float phase = 0f;

            for (int i = 0; i < length; i++)
            {
                float t = (float)i / length;
                float thudEnv = Mathf.Exp(-t * 16f);
                float crackEnv = Mathf.Exp(-t * 32f);
                float freq = Mathf.Lerp(210f, 52f, t);
                phase += 2f * Mathf.PI * freq / sampleRate;

                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                float sample = (Mathf.Sin(phase) * 0.75f * thudEnv + noise * 0.55f * crackEnv) * 0.75f;
                data[i] = Mathf.Clamp(sample, -1f, 1f);
            }

            return RegisterClip("SFX_Procedural_Hit", data, sampleRate);
        }

        private AudioClip CreateHealClip()
        {
            const int sampleRate = 44100;
            float duration = 0.48f;
            int length = Mathf.RoundToInt(sampleRate * duration);
            float[] data = new float[length];
            float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };

            for (int i = 0; i < length; i++)
            {
                float sec = (float)i / sampleRate;
                float t = (float)i / length;
                float sum = 0f;

                for (int n = 0; n < notes.Length; n++)
                {
                    float start = n * 0.065f;
                    if (sec < start) continue;
                    float local = sec - start;
                    float noteEnv = Mathf.Clamp01(local / 0.015f) * Mathf.Exp(-local * 7.5f);
                    sum += Mathf.Sin(2f * Mathf.PI * notes[n] * local) * noteEnv;
                }

                float tail = 1f - t * 0.15f;
                data[i] = Mathf.Clamp(sum * 0.28f * tail, -1f, 1f);
            }

            return RegisterClip("SFX_Procedural_Heal", data, sampleRate);
        }

        private AudioClip CreateShockwaveClip()
        {
            const int sampleRate = 44100;
            float duration = 0.36f;
            int length = Mathf.RoundToInt(sampleRate * duration);
            float[] data = new float[length];
            System.Random rng = new System.Random(303);
            float phase = 0f;
            float lpNoise = 0f;

            for (int i = 0; i < length; i++)
            {
                float t = (float)i / length;
                float bassEnv = Mathf.Exp(-t * 7.5f);
                float freq = Mathf.Lerp(145f, 36f, Mathf.Pow(t, 0.65f));
                phase += 2f * Mathf.PI * freq / sampleRate;

                float rawNoise = (float)(rng.NextDouble() * 2.0 - 1.0);
                lpNoise = Mathf.Lerp(lpNoise, rawNoise, 0.12f);

                float sample = (Mathf.Sin(phase) * 0.8f + lpNoise * 0.45f) * bassEnv * 0.78f;
                data[i] = Mathf.Clamp(sample, -1f, 1f);
            }

            return RegisterClip("SFX_Procedural_Shockwave", data, sampleRate);
        }

        private AudioClip CreateLungeClip()
        {
            const int sampleRate = 44100;
            float duration = 0.22f;
            int length = Mathf.RoundToInt(sampleRate * duration);
            float[] data = new float[length];
            System.Random rng = new System.Random(404);
            float phase = 0f;
            float lpNoise = 0f;

            for (int i = 0; i < length; i++)
            {
                float t = (float)i / length;
                float env = Mathf.Sin(t * Mathf.PI);
                float freq = t < 0.4f ? Mathf.Lerp(260f, 580f, t / 0.4f) : Mathf.Lerp(580f, 200f, (t - 0.4f) / 0.6f);
                phase += 2f * Mathf.PI * freq / sampleRate;

                float rawNoise = (float)(rng.NextDouble() * 2.0 - 1.0);
                lpNoise = Mathf.Lerp(lpNoise, rawNoise, 0.22f);

                float sample = (Mathf.Sin(phase) * 0.35f + lpNoise * 0.65f) * env * 0.55f;
                data[i] = Mathf.Clamp(sample, -1f, 1f);
            }

            return RegisterClip("SFX_Procedural_Lunge", data, sampleRate);
        }

        private AudioClip CreateLeapClip()
        {
            const int sampleRate = 44100;
            float duration = 0.22f;
            int length = Mathf.RoundToInt(sampleRate * duration);
            float[] data = new float[length];
            float phase = 0f;

            for (int i = 0; i < length; i++)
            {
                float t = (float)i / length;
                float env = Mathf.Sin(t * Mathf.PI) * (1f - t * 0.25f);
                float freq = Mathf.Lerp(210f, 760f, t * t);
                phase += 2f * Mathf.PI * freq / sampleRate;
                data[i] = Mathf.Clamp(Mathf.Sin(phase) * env * 0.5f, -1f, 1f);
            }

            return RegisterClip("SFX_Procedural_Leap", data, sampleRate);
        }

        private AudioClip RegisterClip(string clipName, float[] samples, int sampleRate)
        {
            AudioClip clip = AudioClip.Create(clipName, samples.Length, 1, sampleRate, stream: false);
            clip.SetData(samples, 0);
            _generatedClips.Add(clip);
            return clip;
        }
    }
}
