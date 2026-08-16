using System.Collections.Generic;
using UnityEngine;

namespace Glowpulse.Audio
{
    /// <summary>Stable names for every sound the game plays.</summary>
    public static class Sfx
    {
        public const string Punch = "punch";
        public const string Kick = "kick";
        public const string Heavy = "heavy";
        public const string Finisher = "finisher";
        public const string Throw = "throw";
        public const string Grab = "grab";
        public const string Block = "block";
        public const string Parry = "parry";
        public const string GuardBreak = "guard_break";
        public const string Whoosh = "whoosh";
        public const string WhooshHeavy = "whoosh_heavy";
        public const string Dodge = "dodge";
        public const string Grunt = "grunt";
        public const string Death = "death";
        public const string BodyFall = "body_fall";
        public const string Footstep = "footstep";
        public const string Land = "land";
        public const string UiClick = "ui_click";
        public const string UiConfirm = "ui_confirm";
        public const string UiBack = "ui_back";
        public const string Chime = "chime";
    }

    /// <summary>Mix buses, so the settings menu can set levels independently.</summary>
    public enum AudioBus
    {
        Sfx = 0,
        Ambience = 1,
        Music = 2,
        Ui = 3
    }

    /// <summary>
    /// Plays every sound in the game through a small pool of AudioSources.
    ///
    /// Clips are procedurally generated placeholders by default. Calling
    /// <see cref="OverrideClip"/> swaps in a real recording under the same name,
    /// so importing audio later never touches the code that triggers it.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class AudioManager : MonoBehaviour
    {
        [SerializeField] private int _voiceCount = 20;
        [SerializeField, Range(0f, 1f)] private float _masterVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float _sfxVolume = 0.85f;
        [SerializeField, Range(0f, 1f)] private float _ambienceVolume = 0.4f;
        [SerializeField, Range(0f, 1f)] private float _musicVolume = 0.55f;
        [SerializeField, Range(0f, 1f)] private float _uiVolume = 0.7f;

        private readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>(32);
        private readonly Dictionary<string, float> _lastPlayed = new Dictionary<string, float>(32);
        private AudioSource[] _voices;
        private AudioSource _ambienceSource;
        private int _nextVoice;

        /// <summary>Minimum gap between two plays of the same sound, to stop machine-gunning.</summary>
        private const float RetriggerGuard = 0.035f;

        private static AudioManager _instance;

        public static AudioManager Instance => _instance;

        public float MasterVolume
        {
            get => _masterVolume;
            set { _masterVolume = Mathf.Clamp01(value); RefreshAmbience(); }
        }

        public float SfxVolume
        {
            get => _sfxVolume;
            set => _sfxVolume = Mathf.Clamp01(value);
        }

        public float AmbienceVolume
        {
            get => _ambienceVolume;
            set { _ambienceVolume = Mathf.Clamp01(value); RefreshAmbience(); }
        }

        public float MusicVolume
        {
            get => _musicVolume;
            set => _musicVolume = Mathf.Clamp01(value);
        }

        public float UiVolume
        {
            get => _uiVolume;
            set => _uiVolume = Mathf.Clamp01(value);
        }

        public static AudioManager Install(GameObject host)
        {
            if (_instance != null) return _instance;
            _instance = host.GetComponent<AudioManager>() ?? host.AddComponent<AudioManager>();
            return _instance;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this);
                return;
            }

            _instance = this;
            BuildVoices();
            BuildDefaultClips();
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        // ---- public API -----------------------------------------------------------

        /// <summary>Replaces a placeholder with an authored clip. Call before gameplay starts.</summary>
        public void OverrideClip(string id, AudioClip clip)
        {
            if (string.IsNullOrEmpty(id) || clip == null) return;
            _clips[id] = clip;
        }

        public AudioClip GetClip(string id)
        {
            return id != null && _clips.TryGetValue(id, out AudioClip clip) ? clip : null;
        }

        /// <summary>Plays a sound positioned in the world.</summary>
        public static void PlayAt(string id, Vector3 position, float volume = 1f, float pitchJitter = 0.08f)
        {
            _instance?.Play(id, position, true, volume, pitchJitter, AudioBus.Sfx);
        }

        /// <summary>Plays a sound with no position, e.g. UI.</summary>
        public static void Play2D(string id, float volume = 1f, float pitchJitter = 0f,
            AudioBus bus = AudioBus.Ui)
        {
            _instance?.Play(id, Vector3.zero, false, volume, pitchJitter, bus);
        }

        public void Play(string id, Vector3 position, bool spatial, float volume, float pitchJitter,
            AudioBus bus)
        {
            AudioClip clip = GetClip(id);
            if (clip == null || _voices == null) return;

            // Several enemies landing hits on the same frame would otherwise stack
            // into a single loud crack.
            if (_lastPlayed.TryGetValue(id, out float last) &&
                Time.unscaledTime - last < RetriggerGuard) return;
            _lastPlayed[id] = Time.unscaledTime;

            AudioSource source = NextVoice();
            source.clip = clip;
            source.transform.position = position;
            source.spatialBlend = spatial ? 0.85f : 0f;
            source.volume = Mathf.Clamp01(volume) * BusVolume(bus) * _masterVolume;
            source.pitch = pitchJitter > 0f
                ? 1f + Random.Range(-pitchJitter, pitchJitter)
                : 1f;
            source.minDistance = 3f;
            source.maxDistance = 42f;
            source.rolloffMode = AudioRolloffMode.Linear;

            // Combat audio must land during hit stop, when timeScale is near zero.
            source.ignoreListenerPause = true;
            source.Play();
        }

        /// <summary>Starts the looping ambience bed.</summary>
        public void PlayAmbience(AudioClip clip)
        {
            if (_ambienceSource == null || clip == null) return;
            _ambienceSource.clip = clip;
            _ambienceSource.loop = true;
            RefreshAmbience();
            _ambienceSource.Play();
        }

        public void StopAmbience()
        {
            if (_ambienceSource != null) _ambienceSource.Stop();
        }

        // ---- setup -------------------------------------------------------------------

        private void BuildVoices()
        {
            _voices = new AudioSource[Mathf.Max(4, _voiceCount)];
            for (int i = 0; i < _voices.Length; i++)
            {
                var go = new GameObject($"Voice_{i:00}");
                go.transform.SetParent(transform, false);
                AudioSource source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.dopplerLevel = 0f;
                source.spatialBlend = 0.85f;
                _voices[i] = source;
            }

            var ambience = new GameObject("Ambience");
            ambience.transform.SetParent(transform, false);
            _ambienceSource = ambience.AddComponent<AudioSource>();
            _ambienceSource.playOnAwake = false;
            _ambienceSource.loop = true;
            _ambienceSource.spatialBlend = 0f;
            _ambienceSource.dopplerLevel = 0f;
        }

        private void BuildDefaultClips()
        {
            Register(Sfx.Punch, ProceduralAudio.Punch());
            Register(Sfx.Kick, ProceduralAudio.Kick());
            Register(Sfx.Heavy, ProceduralAudio.HeavyHit());
            Register(Sfx.Finisher, ProceduralAudio.HeavyHit());
            Register(Sfx.Throw, ProceduralAudio.BodyFall());
            Register(Sfx.Grab, ProceduralAudio.Block());
            Register(Sfx.Block, ProceduralAudio.Block());
            Register(Sfx.Parry, ProceduralAudio.Parry());
            Register(Sfx.GuardBreak, ProceduralAudio.GuardBreak());
            Register(Sfx.Whoosh, ProceduralAudio.Whoosh(1.2f));
            Register(Sfx.WhooshHeavy, ProceduralAudio.Whoosh(0.7f));
            Register(Sfx.Dodge, ProceduralAudio.Whoosh(1.5f));
            Register(Sfx.Grunt, ProceduralAudio.Grunt());
            Register(Sfx.Death, ProceduralAudio.Death());
            Register(Sfx.BodyFall, ProceduralAudio.BodyFall());
            Register(Sfx.Footstep, ProceduralAudio.Footstep());
            Register(Sfx.Land, ProceduralAudio.Landing());
            Register(Sfx.UiClick, ProceduralAudio.UiClick(880f));
            Register(Sfx.UiConfirm, ProceduralAudio.UiClick(1320f));
            Register(Sfx.UiBack, ProceduralAudio.UiClick(520f));
            Register(Sfx.Chime, ProceduralAudio.Chime());
        }

        private void Register(string id, AudioClip clip)
        {
            if (clip == null) return;
            clip.hideFlags = HideFlags.HideAndDontSave;
            _clips[id] = clip;
        }

        private AudioSource NextVoice()
        {
            // Round robin, preferring a source that is not currently sounding.
            for (int i = 0; i < _voices.Length; i++)
            {
                AudioSource candidate = _voices[_nextVoice];
                _nextVoice = (_nextVoice + 1) % _voices.Length;
                if (!candidate.isPlaying) return candidate;
            }

            AudioSource fallback = _voices[_nextVoice];
            _nextVoice = (_nextVoice + 1) % _voices.Length;
            return fallback;
        }

        private float BusVolume(AudioBus bus)
        {
            switch (bus)
            {
                case AudioBus.Ambience: return _ambienceVolume;
                case AudioBus.Music: return _musicVolume;
                case AudioBus.Ui: return _uiVolume;
                default: return _sfxVolume;
            }
        }

        private void RefreshAmbience()
        {
            if (_ambienceSource != null)
                _ambienceSource.volume = _ambienceVolume * _masterVolume;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;
    }
}
