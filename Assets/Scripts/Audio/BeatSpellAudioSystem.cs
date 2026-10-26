using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BeatSpell.Audio
{
    /// <summary>
    /// 独立音频系统：SFX 池化播放、BGM 预留、分组音量/静音。
    /// 本轮不订阅玩法事件，由外部或 ContextMenu / MCP 直接调用 Play。
    /// </summary>
    public class BeatSpellAudioSystem : MonoBehaviour
    {
        public static BeatSpellAudioSystem Instance { get; private set; }

        [Header("Cues")]
        [SerializeField] private List<BeatSpellAudioCue> cues = new List<BeatSpellAudioCue>();

        [Header("Sources")]
        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private int sfxPoolSize = 8;
        [SerializeField] private bool persistAcrossScenes;

        [Header("Bus Volume")]
        [SerializeField] [Range(0f, 1f)] private float masterVolume = 1f;
        [SerializeField] [Range(0f, 1f)] private float bgmVolume = 1f;
        [SerializeField] [Range(0f, 1f)] private float sfxVolume = 1f;
        [SerializeField] [Range(0f, 1f)] private float uiVolume = 1f;
        [SerializeField] [Range(0f, 1f)] private float combatVolume = 1f;
        [SerializeField] [Range(0f, 1f)] private float voiceVolume = 1f;

        [Header("Bus Mute")]
        [SerializeField] private bool masterMuted;
        [SerializeField] private bool bgmMuted;
        [SerializeField] private bool sfxMuted;
        [SerializeField] private bool uiMuted;
        [SerializeField] private bool combatMuted;
        [SerializeField] private bool voiceMuted;

        private readonly Dictionary<BeatSpellAudioEventId, BeatSpellAudioCue> cueMap =
            new Dictionary<BeatSpellAudioEventId, BeatSpellAudioCue>();
        private readonly Dictionary<BeatSpellAudioEventId, double> lastPlayDspTime =
            new Dictionary<BeatSpellAudioEventId, double>();
        private readonly List<AudioSource> sfxPool = new List<AudioSource>();

        private int sfxPoolIndex;
        private BeatSpellAudioEventId currentBgmEvent;
        private BeatSpellAudioCue currentBgmCue;
        private Coroutine bgmFadeRoutine;

        public string LastAudioLog { get; private set; } = string.Empty;
        public BeatSpellAudioEventId LastPlayedEventId { get; private set; }
        public bool LastPlaySucceeded { get; private set; }
        public BeatSpellAudioEventId LastMissingClipEventId { get; private set; }
        public BeatSpellAudioEventId CurrentBgmEventId => currentBgmEvent;
        public bool IsBgmPlaying => bgmSource != null && bgmSource.isPlaying;
        public int CueCount => cues != null ? cues.Count : 0;

        public int PlayingSfxCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < sfxPool.Count; i++)
                {
                    if (sfxPool[i] != null && sfxPool[i].isPlaying)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        private void Reset()
        {
            cues = CreateDefaultCues();
            sfxPoolSize = 8;
            persistAcrossScenes = false;
            masterVolume = 1f;
            bgmVolume = 1f;
            sfxVolume = 1f;
            uiVolume = 1f;
            combatVolume = 1f;
            voiceVolume = 1f;
        }

        private void Awake()
        {
            if (persistAcrossScenes)
            {
                if (Instance != null && Instance != this)
                {
                    Destroy(gameObject);
                    return;
                }

                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (Instance == null)
            {
                Instance = this;
            }

            EnsureSources();
            RebuildCueMap();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public bool Play(BeatSpellAudioEventId eventId)
        {
            return Play(eventId, 1f);
        }

        public bool Play(BeatSpellAudioEventId eventId, float volumeScale)
        {
            BeatSpellAudioCue cue = GetCue(eventId);
            if (cue == null)
            {
                LogMissing(eventId, "Cue");
                return false;
            }

            if (cue.Bus == BeatSpellAudioBus.Bgm)
            {
                return PlayBgm(eventId);
            }

            if (cue.Clip == null)
            {
                LogMissing(eventId, "Clip");
                return false;
            }

            if (IsOnCooldown(eventId, cue.CooldownSeconds))
            {
                LastPlaySucceeded = false;
                LastPlayedEventId = eventId;
                LastAudioLog = $"[Audio] Cooldown | event={eventId}";
                Debug.Log(LastAudioLog);
                return false;
            }

            float volume = ResolveVolume(cue, volumeScale);
            AudioSource source = AcquireSfxSource(cue);
            source.clip = cue.Clip;
            source.volume = volume;
            source.pitch = cue.Pitch;
            source.loop = false;
            source.Play();

            MarkPlayed(eventId);
            LastPlaySucceeded = true;
            LastPlayedEventId = eventId;
            LastAudioLog =
                $"[Audio] Play | event={eventId} | bus={cue.Bus} | volume={volume:0.##} | overlap={(cue.AllowOverlap ? 1 : 0)}";
            Debug.Log(LastAudioLog);
            return true;
        }

        public bool PlayBgm(BeatSpellAudioEventId eventId, bool restartIfSame = false)
        {
            BeatSpellAudioCue cue = GetCue(eventId);
            if (cue == null)
            {
                LogMissing(eventId, "Cue");
                return false;
            }

            if (cue.Clip == null)
            {
                LogMissing(eventId, "Clip");
                return false;
            }

            EnsureSources();
            if (!restartIfSame && currentBgmEvent == eventId && bgmSource.isPlaying)
            {
                LastPlaySucceeded = true;
                LastPlayedEventId = eventId;
                LastAudioLog = $"[Audio] BgmAlreadyPlaying | event={eventId}";
                Debug.Log(LastAudioLog);
                return true;
            }

            StopBgmFade();
            currentBgmEvent = eventId;
            currentBgmCue = cue;
            bgmSource.clip = cue.Clip;
            bgmSource.loop = cue.Loop;
            bgmSource.pitch = cue.Pitch;
            bgmSource.volume = ResolveVolume(cue, 1f);
            bgmSource.Play();

            LastPlaySucceeded = true;
            LastPlayedEventId = eventId;
            LastAudioLog = $"[Audio] BgmPlay | event={eventId} | loop={(cue.Loop ? 1 : 0)}";
            Debug.Log(LastAudioLog);
            return true;
        }

        public void StopBgm(float fadeSeconds = 0f)
        {
            EnsureSources();
            StopBgmFade();

            if (fadeSeconds <= 0f || bgmSource == null || !bgmSource.isPlaying)
            {
                StopBgmImmediate();
                return;
            }

            bgmFadeRoutine = StartCoroutine(FadeOutBgm(fadeSeconds));
        }

        public void SetBusVolume(BeatSpellAudioBus bus, float volume)
        {
            float clamped = Mathf.Clamp01(volume);
            switch (bus)
            {
                case BeatSpellAudioBus.Master:
                    masterVolume = clamped;
                    break;
                case BeatSpellAudioBus.Bgm:
                    bgmVolume = clamped;
                    break;
                case BeatSpellAudioBus.Sfx:
                    sfxVolume = clamped;
                    break;
                case BeatSpellAudioBus.Ui:
                    uiVolume = clamped;
                    break;
                case BeatSpellAudioBus.Combat:
                    combatVolume = clamped;
                    break;
                case BeatSpellAudioBus.Voice:
                    voiceVolume = clamped;
                    break;
            }

            ApplyBgmVolume();
            Debug.Log($"[Audio] BusVolume | bus={bus} | volume={clamped:0.##}");
        }

        public void SetBusMuted(BeatSpellAudioBus bus, bool muted)
        {
            switch (bus)
            {
                case BeatSpellAudioBus.Master:
                    masterMuted = muted;
                    break;
                case BeatSpellAudioBus.Bgm:
                    bgmMuted = muted;
                    break;
                case BeatSpellAudioBus.Sfx:
                    sfxMuted = muted;
                    break;
                case BeatSpellAudioBus.Ui:
                    uiMuted = muted;
                    break;
                case BeatSpellAudioBus.Combat:
                    combatMuted = muted;
                    break;
                case BeatSpellAudioBus.Voice:
                    voiceMuted = muted;
                    break;
            }

            ApplyBgmVolume();
            Debug.Log($"[Audio] BusMute | bus={bus} | muted={(muted ? 1 : 0)}");
        }

        public float GetBusVolume(BeatSpellAudioBus bus)
        {
            return bus switch
            {
                BeatSpellAudioBus.Master => masterVolume,
                BeatSpellAudioBus.Bgm => bgmVolume,
                BeatSpellAudioBus.Sfx => sfxVolume,
                BeatSpellAudioBus.Ui => uiVolume,
                BeatSpellAudioBus.Combat => combatVolume,
                BeatSpellAudioBus.Voice => voiceVolume,
                _ => 1f
            };
        }

        public bool IsBusMuted(BeatSpellAudioBus bus)
        {
            return bus switch
            {
                BeatSpellAudioBus.Master => masterMuted,
                BeatSpellAudioBus.Bgm => bgmMuted,
                BeatSpellAudioBus.Sfx => sfxMuted,
                BeatSpellAudioBus.Ui => uiMuted,
                BeatSpellAudioBus.Combat => combatMuted,
                BeatSpellAudioBus.Voice => voiceMuted,
                _ => false
            };
        }

        public BeatSpellAudioCue GetCue(BeatSpellAudioEventId eventId)
        {
            if (cueMap.Count == 0)
            {
                RebuildCueMap();
            }

            cueMap.TryGetValue(eventId, out BeatSpellAudioCue cue);
            return cue;
        }

        public float ResolvePreviewVolume(BeatSpellAudioEventId eventId, float volumeScale = 1f)
        {
            BeatSpellAudioCue cue = GetCue(eventId);
            return cue == null ? 0f : ResolveVolume(cue, volumeScale);
        }

        public int CountPlaying(BeatSpellAudioEventId eventId)
        {
            BeatSpellAudioCue cue = GetCue(eventId);
            if (cue == null || cue.Clip == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < sfxPool.Count; i++)
            {
                AudioSource source = sfxPool[i];
                if (source != null && source.isPlaying && source.clip == cue.Clip)
                {
                    count++;
                }
            }

            return count;
        }

        public bool HasClip(BeatSpellAudioEventId eventId)
        {
            BeatSpellAudioCue cue = GetCue(eventId);
            return cue != null && cue.Clip != null;
        }

        public void PrepareSources()
        {
            EnsureSources();
            RebuildCueMap();
        }

        public void RebuildCueMap()
        {
            cueMap.Clear();
            if (cues == null)
            {
                return;
            }

            for (int i = 0; i < cues.Count; i++)
            {
                BeatSpellAudioCue cue = cues[i];
                if (cue == null)
                {
                    continue;
                }

                cueMap[cue.EventId] = cue;
            }
        }

        [ContextMenu("Audio/Play ChantNormal")]
        public bool PlayChantNormal() => Play(BeatSpellAudioEventId.ChantNormal);

        [ContextMenu("Audio/Play ChantPerfect")]
        public bool PlayChantPerfect() => Play(BeatSpellAudioEventId.ChantPerfect);

        [ContextMenu("Audio/Play CastSuccess")]
        public bool PlayCastSuccess() => Play(BeatSpellAudioEventId.CastSuccess);

        [ContextMenu("Audio/Play SpellNormal")]
        public bool PlaySpellNormal() => Play(BeatSpellAudioEventId.SpellNormal);

        [ContextMenu("Audio/Play SpellPerfect")]
        public bool PlaySpellPerfect() => Play(BeatSpellAudioEventId.SpellPerfect);

        [ContextMenu("Audio/Play CastFailed")]
        public bool PlayCastFailed() => Play(BeatSpellAudioEventId.CastFailed);

        [ContextMenu("Audio/Play Reserved BgmBattle")]
        public bool PlayReservedBgmBattle() => PlayBgm(BeatSpellAudioEventId.BgmBattle);

        [ContextMenu("Audio/Play Reserved EnemyAttack")]
        public bool PlayReservedEnemyAttack() => Play(BeatSpellAudioEventId.EnemyAttack);

        private static List<BeatSpellAudioCue> CreateDefaultCues()
        {
            return new List<BeatSpellAudioCue>
            {
                new BeatSpellAudioCue(BeatSpellAudioEventId.ChantNormal, BeatSpellAudioBus.Combat),
                new BeatSpellAudioCue(BeatSpellAudioEventId.ChantPerfect, BeatSpellAudioBus.Combat),
                new BeatSpellAudioCue(BeatSpellAudioEventId.CastSuccess, BeatSpellAudioBus.Combat),
                new BeatSpellAudioCue(BeatSpellAudioEventId.SpellNormal, BeatSpellAudioBus.Combat),
                new BeatSpellAudioCue(BeatSpellAudioEventId.SpellPerfect, BeatSpellAudioBus.Combat),
                new BeatSpellAudioCue(BeatSpellAudioEventId.CastFailed, BeatSpellAudioBus.Combat),
                new BeatSpellAudioCue(BeatSpellAudioEventId.BgmBattle, BeatSpellAudioBus.Bgm, loop: true),
                new BeatSpellAudioCue(BeatSpellAudioEventId.UiConfirm, BeatSpellAudioBus.Ui),
                new BeatSpellAudioCue(BeatSpellAudioEventId.UiCancel, BeatSpellAudioBus.Ui),
                new BeatSpellAudioCue(BeatSpellAudioEventId.EnemyWarning, BeatSpellAudioBus.Combat),
                new BeatSpellAudioCue(BeatSpellAudioEventId.EnemyAttack, BeatSpellAudioBus.Combat),
                new BeatSpellAudioCue(BeatSpellAudioEventId.EnemyHit, BeatSpellAudioBus.Combat),
                new BeatSpellAudioCue(BeatSpellAudioEventId.PlayerHit, BeatSpellAudioBus.Combat),
                new BeatSpellAudioCue(BeatSpellAudioEventId.Victory, BeatSpellAudioBus.Ui),
                new BeatSpellAudioCue(BeatSpellAudioEventId.Defeat, BeatSpellAudioBus.Ui)
            };
        }

        private void EnsureSources()
        {
            if (bgmSource == null)
            {
                bgmSource = GetOrCreateChildSource("BgmSource", playOnAwake: false, loop: true);
            }

            int targetSize = Mathf.Max(2, sfxPoolSize);
            Transform poolRoot = transform.Find("SfxPool");
            if (poolRoot == null)
            {
                GameObject poolObject = new GameObject("SfxPool");
                poolObject.transform.SetParent(transform, false);
                poolRoot = poolObject.transform;
            }

            sfxPool.Clear();
            for (int i = 0; i < targetSize; i++)
            {
                string childName = $"Sfx_{i}";
                Transform child = poolRoot.Find(childName);
                AudioSource source = child != null
                    ? child.GetComponent<AudioSource>()
                    : GetOrCreateChildSource(childName, playOnAwake: false, loop: false, parent: poolRoot);
                source.playOnAwake = false;
                source.loop = false;
                sfxPool.Add(source);
            }
        }

        private AudioSource GetOrCreateChildSource(string childName, bool playOnAwake, bool loop, Transform parent = null)
        {
            Transform owner = parent != null ? parent : transform;
            Transform child = owner.Find(childName);
            if (child == null)
            {
                GameObject childObject = new GameObject(childName);
                childObject.transform.SetParent(owner, false);
                child = childObject.transform;
            }

            AudioSource source = child.GetComponent<AudioSource>();
            if (source == null)
            {
                source = child.gameObject.AddComponent<AudioSource>();
            }

            source.playOnAwake = playOnAwake;
            source.loop = loop;
            source.spatialBlend = 0f;
            return source;
        }

        private AudioSource AcquireSfxSource(BeatSpellAudioCue cue)
        {
            EnsureSources();
            if (!cue.AllowOverlap)
            {
                StopPlayingClip(cue.Clip);
            }

            for (int i = 0; i < sfxPool.Count; i++)
            {
                int index = (sfxPoolIndex + i) % sfxPool.Count;
                AudioSource candidate = sfxPool[index];
                if (candidate != null && !candidate.isPlaying)
                {
                    sfxPoolIndex = (index + 1) % sfxPool.Count;
                    return candidate;
                }
            }

            AudioSource fallback = sfxPool[sfxPoolIndex];
            sfxPoolIndex = (sfxPoolIndex + 1) % sfxPool.Count;
            return fallback;
        }

        private void StopPlayingClip(AudioClip clip)
        {
            if (clip == null)
            {
                return;
            }

            for (int i = 0; i < sfxPool.Count; i++)
            {
                AudioSource source = sfxPool[i];
                if (source != null && source.isPlaying && source.clip == clip)
                {
                    source.Stop();
                }
            }
        }

        private bool IsOnCooldown(BeatSpellAudioEventId eventId, float cooldownSeconds)
        {
            if (cooldownSeconds <= 0f)
            {
                return false;
            }

            if (!lastPlayDspTime.TryGetValue(eventId, out double lastDsp))
            {
                return false;
            }

            return AudioSettings.dspTime < lastDsp + cooldownSeconds;
        }

        private void MarkPlayed(BeatSpellAudioEventId eventId)
        {
            lastPlayDspTime[eventId] = AudioSettings.dspTime;
        }

        private float ResolveVolume(BeatSpellAudioCue cue, float volumeScale)
        {
            if (IsBusMuted(BeatSpellAudioBus.Master) || IsBusMuted(cue.Bus))
            {
                return 0f;
            }

            float volume = cue.Volume * Mathf.Max(0f, volumeScale) * masterVolume;
            switch (cue.Bus)
            {
                case BeatSpellAudioBus.Bgm:
                    volume *= bgmVolume;
                    break;
                case BeatSpellAudioBus.Sfx:
                    volume *= sfxVolume;
                    break;
                case BeatSpellAudioBus.Ui:
                    volume *= uiVolume;
                    break;
                case BeatSpellAudioBus.Combat:
                    if (sfxMuted)
                    {
                        return 0f;
                    }

                    volume *= sfxVolume * combatVolume;
                    break;
                case BeatSpellAudioBus.Voice:
                    volume *= voiceVolume;
                    break;
            }

            return Mathf.Clamp01(volume);
        }

        private void ApplyBgmVolume()
        {
            if (bgmSource == null || currentBgmCue == null)
            {
                return;
            }

            bgmSource.volume = ResolveVolume(currentBgmCue, 1f);
        }

        private void StopBgmImmediate()
        {
            if (bgmSource != null)
            {
                bgmSource.Stop();
                bgmSource.clip = null;
            }

            currentBgmCue = null;
            currentBgmEvent = default;
            LastAudioLog = "[Audio] BgmStop";
            Debug.Log(LastAudioLog);
        }

        private void StopBgmFade()
        {
            if (bgmFadeRoutine == null)
            {
                return;
            }

            StopCoroutine(bgmFadeRoutine);
            bgmFadeRoutine = null;
        }

        private IEnumerator FadeOutBgm(float fadeSeconds)
        {
            float startVolume = bgmSource.volume;
            float elapsed = 0f;
            while (elapsed < fadeSeconds && bgmSource != null)
            {
                elapsed += Time.unscaledDeltaTime;
                bgmSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / fadeSeconds);
                yield return null;
            }

            StopBgmImmediate();
            bgmFadeRoutine = null;
        }

        private void LogMissing(BeatSpellAudioEventId eventId, string kind)
        {
            LastPlaySucceeded = false;
            LastPlayedEventId = eventId;
            LastMissingClipEventId = eventId;
            LastAudioLog = $"[Audio] Missing{kind} | event={eventId}";
            Debug.LogWarning(LastAudioLog);
        }
    }
}
