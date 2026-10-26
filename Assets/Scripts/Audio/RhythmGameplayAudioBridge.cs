using System.Collections.Generic;
using System.Text;
using BeatSpell.Rhythm;
using BeatSpell.RhythmInput;
using BeatSpell.Spell;
using UnityEngine;

namespace BeatSpell.Audio
{
    /// <summary>
    /// 节拍/施法结果到全局 SFX 的最小桥接。Cast 只听 SpellCaster 结果，不听 OnRhythmInput 的 Cast。
    /// </summary>
    public class RhythmGameplayAudioBridge : MonoBehaviour
    {
        [SerializeField] private BeatSpellAudioSystem audioSystem;
        [SerializeField] private PlayerRhythmInputController inputController;
        [SerializeField] private SpellCaster spellCaster;

        private readonly List<BeatSpellAudioEventId> playLog = new List<BeatSpellAudioEventId>(8);

        public string LastPlayedSequence { get; private set; } = string.Empty;
        public BeatSpellAudioEventId LastPlayedEventId { get; private set; }
        public int PlayLogCount => playLog.Count;

        private void Awake()
        {
            CacheRefs();
        }

        private void OnEnable()
        {
            CacheRefs();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public void ResetPlayLog()
        {
            playLog.Clear();
            LastPlayedSequence = string.Empty;
            LastPlayedEventId = default;
        }

        public bool SequenceContains(BeatSpellAudioEventId eventId)
        {
            return playLog.Contains(eventId);
        }

        public bool SequenceEquals(params BeatSpellAudioEventId[] expected)
        {
            if (expected == null || expected.Length != playLog.Count)
            {
                return false;
            }

            for (int i = 0; i < expected.Length; i++)
            {
                if (playLog[i] != expected[i])
                {
                    return false;
                }
            }

            return true;
        }

        private void CacheRefs()
        {
            if (audioSystem == null)
            {
                audioSystem = BeatSpellAudioSystem.Instance != null
                    ? BeatSpellAudioSystem.Instance
                    : FindFirstObjectByType<BeatSpellAudioSystem>();
            }

            if (inputController == null)
            {
                inputController = FindFirstObjectByType<PlayerRhythmInputController>();
            }

            if (spellCaster == null)
            {
                spellCaster = FindFirstObjectByType<SpellCaster>();
            }
        }

        private void Subscribe()
        {
            if (inputController != null)
            {
                inputController.OnRhythmInput -= HandleRhythmInput;
                inputController.OnRhythmInput += HandleRhythmInput;
            }

            if (spellCaster != null)
            {
                spellCaster.OnSpellCast -= HandleSpellCast;
                spellCaster.OnSpellCast += HandleSpellCast;
                spellCaster.OnMatchFailed -= HandleCastFailed;
                spellCaster.OnMatchFailed += HandleCastFailed;
                spellCaster.OnEmptyCastPenalty -= HandleCastFailed;
                spellCaster.OnEmptyCastPenalty += HandleCastFailed;
                spellCaster.OnVirtualCastFailed -= HandleCastFailed;
                spellCaster.OnVirtualCastFailed += HandleCastFailed;
            }
        }

        private void Unsubscribe()
        {
            if (inputController != null)
            {
                inputController.OnRhythmInput -= HandleRhythmInput;
            }

            if (spellCaster != null)
            {
                spellCaster.OnSpellCast -= HandleSpellCast;
                spellCaster.OnMatchFailed -= HandleCastFailed;
                spellCaster.OnEmptyCastPenalty -= HandleCastFailed;
                spellCaster.OnVirtualCastFailed -= HandleCastFailed;
            }
        }

        private void HandleRhythmInput(BeatInputType type, BeatTimingGrade grade, float deltaMs, int beatIndex)
        {
            if (type != BeatInputType.Chant)
            {
                return;
            }

            Play(grade == BeatTimingGrade.Perfect
                ? BeatSpellAudioEventId.ChantPerfect
                : BeatSpellAudioEventId.ChantNormal);
        }

        private void HandleSpellCast(SpellDefinition spell, float damageFactor, float damage)
        {
            Play(BeatSpellAudioEventId.CastSuccess);
            Play(damageFactor >= 1f ? BeatSpellAudioEventId.SpellPerfect : BeatSpellAudioEventId.SpellNormal);
        }

        private void HandleCastFailed()
        {
            Play(BeatSpellAudioEventId.CastFailed);
        }

        private void Play(BeatSpellAudioEventId eventId)
        {
            if (audioSystem == null)
            {
                Debug.LogWarning($"[Audio] GameplayMissingSystem | event={eventId}");
                return;
            }

            audioSystem.Play(eventId);
            playLog.Add(eventId);
            LastPlayedEventId = eventId;
            LastPlayedSequence = BuildSequence();
            Debug.Log($"[Audio] Gameplay | event={eventId} | sequence={LastPlayedSequence}");
        }

        private string BuildSequence()
        {
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < playLog.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append('|');
                }

                builder.Append(playLog[i]);
            }

            return builder.ToString();
        }
    }
}
