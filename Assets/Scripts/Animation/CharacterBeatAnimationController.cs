using System.Collections.Generic;
using BeatSpell.Prototype;
using BeatSpell.Rhythm;
using BeatSpell.Spell;
using UnityEngine;

namespace BeatSpell.Animation
{
    public enum CharacterAnimState
    {
        Idle,
        Chant,
        CastSuccess,
        CastFailed
    }

    /// <summary>
    /// Idle 由 BeatIdleAnimator 按 BeatFloat 驱动；合法 Chant 走 5 个 Animator state 池。
    /// 匹配成功听 OnSpellCast；失败听 MatchFailed / EmptyCast / VirtualCastFailed，生命周期对齐 0.5 拍留痕。
    /// </summary>
    public class CharacterBeatAnimationController : MonoBehaviour
    {
        [SerializeField] private BeatClock beatClock;
        [SerializeField] private BeatIdleAnimator idleAnimator;
        [SerializeField] private Animator actionAnimator;
        [SerializeField] private RhythmPrototypeBootstrap bootstrap;
        [SerializeField] private string[] chantStateNames =
        {
            "Chant_0",
            "Chant_1",
            "Chant_2",
            "Chant_3",
            "Chant_4"
        };
        [SerializeField] private string castSuccessStateName = "CastSuccess";
        [SerializeField] private string castFailedStateName = "CastFailed";

        private CharacterAnimState currentState = CharacterAnimState.Idle;
        private float stateStartBeatFloat;
        private int stateChangeCount;
        private string lastRequestedAnimatorState = string.Empty;
        private int lastChantIndex = -1;
        private int previousChantIndex = -1;
        private float activeChantHeadBeatFloat;
        private float failedStateStartBeatFloat;
        private float failedStateEndBeatFloat;
        private bool hasActiveChantContext;
        private bool subscribedBuffer;
        private bool subscribedCaster;
        private bool warnedMissingAnimator;
        private bool warnedMissingController;
        private bool warnedMissingCastSuccessClip;
        private readonly HashSet<string> warnedMissingStates = new HashSet<string>();

        public string CurrentStateName => currentState.ToString();
        public CharacterAnimState CurrentState => currentState;
        public int CurrentFrameIndex => idleAnimator != null ? idleAnimator.CurrentFrameIndex : 0;
        public string CurrentAnimatorStateName => lastRequestedAnimatorState;
        public float StateStartBeatFloat => stateStartBeatFloat;
        public int StateChangeCount => stateChangeCount;
        public bool IsIdleAnimatorEnabled => idleAnimator != null && idleAnimator.enabled;
        public bool IsActionAnimatorEnabled => actionAnimator != null && actionAnimator.enabled;
        public int ChantPoolCount => chantStateNames != null ? chantStateNames.Length : 0;
        public int LastChantIndex => lastChantIndex;
        public int PreviousChantIndex => previousChantIndex;
        public float ActiveChantHeadBeatFloat => activeChantHeadBeatFloat;
        public bool HasActiveChantContext => hasActiveChantContext;
        public float FailedStateStartBeatFloat => failedStateStartBeatFloat;
        public float FailedStateEndBeatFloat => failedStateEndBeatFloat;

        public void Bind(
            BeatClock clock,
            BeatIdleAnimator idle,
            Animator animator,
            RhythmPrototypeBootstrap host = null)
        {
            UnsubscribeEvents();
            beatClock = clock;
            idleAnimator = idle;
            actionAnimator = animator;
            if (host != null)
            {
                bootstrap = host;
            }

            SubscribeEvents();
        }

        public void ResetDebugState()
        {
            stateChangeCount = 0;
            lastChantIndex = -1;
            previousChantIndex = -1;
            activeChantHeadBeatFloat = 0f;
            failedStateStartBeatFloat = 0f;
            failedStateEndBeatFloat = 0f;
            hasActiveChantContext = false;
            warnedMissingAnimator = false;
            warnedMissingController = false;
            warnedMissingCastSuccessClip = false;
            warnedMissingStates.Clear();
            ReturnToIdle();
            stateChangeCount = 0;
        }

        public void PlayChant()
        {
            PlayChantFromPool(beatClock != null ? beatClock.BeatFloat : 0f);
        }

        public void PlayChantFromPool(float ownerBeatFloat)
        {
            int index = PickChantIndex();
            previousChantIndex = lastChantIndex;
            lastChantIndex = index;
            hasActiveChantContext = true;
            ClearFailedLifecycle();
            activeChantHeadBeatFloat = bootstrap != null
                ? bootstrap.HeadBufferBeatFloat
                : ownerBeatFloat;

            string stateName = ResolveChantStateName(index);
            EnterActionState(CharacterAnimState.Chant, stateName);
            Debug.Log(
                $"[Anim] ChantPool | index={index} prev={previousChantIndex} head={activeChantHeadBeatFloat:0.###} | state={stateName}");
        }

        public void PlayCastSuccess()
        {
            hasActiveChantContext = false;
            ClearFailedLifecycle();
            EnterActionState(CharacterAnimState.CastSuccess, castSuccessStateName);
        }

        public void PlayCastFailed()
        {
            hasActiveChantContext = false;
            float start = ResolveFailedStartBeatFloat();
            float delay = bootstrap != null ? bootstrap.FailedInputMarkerClearDelayBeats : 0.5f;
            failedStateStartBeatFloat = start;
            failedStateEndBeatFloat = start + delay;
            EnterActionState(CharacterAnimState.CastFailed, castFailedStateName);
            Debug.Log(
                $"[Anim] CastFailed | start={failedStateStartBeatFloat:0.###} end={failedStateEndBeatFloat:0.###}");
        }

        public void ReturnToIdle()
        {
            hasActiveChantContext = false;
            ClearFailedLifecycle();
            ApplyState(CharacterAnimState.Idle, string.Empty);
            if (actionAnimator != null)
            {
                actionAnimator.enabled = false;
            }

            if (idleAnimator != null)
            {
                idleAnimator.enabled = true;
                if (beatClock != null && beatClock.IsRunning)
                {
                    idleAnimator.SyncFrameFromClock();
                }
                else
                {
                    idleAnimator.ResetDebugState();
                }
            }

            Debug.Log($"[Anim] State | name=Idle | beatFloat={stateStartBeatFloat:0.###} | idleEnabled=True");
        }

        /// <summary>
        /// MCP：按指定拍位置检查咏唱表现生命周期，不依赖 Play 时间流逝。
        /// </summary>
        public void EvaluateChantLifecycle(float beatFloat)
        {
            if (!ShouldReturnIdleFromChant(beatFloat))
            {
                return;
            }

            ReturnToIdle();
        }

        /// <summary>
        /// MCP：模拟 CastSuccess Clip 播完。Edit 模式 Animator 不推进时间。
        /// </summary>
        public void EvaluateCastSuccessComplete()
        {
            if (currentState != CharacterAnimState.CastSuccess)
            {
                return;
            }

            ReturnToIdle();
        }

        /// <summary>
        /// MCP：按指定拍位置检查失败动画生命周期，对齐失败留痕 0.5 拍。
        /// </summary>
        public void EvaluateCastFailedLifecycle(float beatFloat)
        {
            if (currentState != CharacterAnimState.CastFailed)
            {
                return;
            }

            if (beatFloat >= failedStateEndBeatFloat)
            {
                ReturnToIdle();
            }
        }

        private void Awake()
        {
            CacheRefs();
        }

        private void OnEnable()
        {
            CacheRefs();
            SubscribeEvents();
            if (currentState == CharacterAnimState.Idle)
            {
                ApplyIdleOwnership();
            }
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
        }

        private void Update()
        {
            if (beatClock == null || !beatClock.IsRunning)
            {
                return;
            }

            EvaluateChantLifecycle(beatClock.BeatFloat);
            EvaluateCastSuccessClipComplete();
            EvaluateCastFailedLifecycle(beatClock.BeatFloat);
        }

        private void CacheRefs()
        {
            if (beatClock == null)
            {
                beatClock = FindFirstObjectByType<BeatClock>();
            }

            if (idleAnimator == null)
            {
                idleAnimator = GetComponent<BeatIdleAnimator>();
            }

            if (bootstrap == null)
            {
                bootstrap = FindFirstObjectByType<RhythmPrototypeBootstrap>();
            }

            if (actionAnimator == null)
            {
                Transform visual = transform.Find("PlayerVisual");
                if (visual != null)
                {
                    actionAnimator = visual.GetComponent<Animator>();
                }

                if (actionAnimator == null)
                {
                    Animator[] animators = GetComponentsInChildren<Animator>(true);
                    for (int i = 0; i < animators.Length; i++)
                    {
                        if (animators[i] != null && animators[i].gameObject != gameObject)
                        {
                            actionAnimator = animators[i];
                            break;
                        }
                    }
                }
            }
        }

        private void SubscribeEvents()
        {
            if (bootstrap == null)
            {
                return;
            }

            if (!subscribedBuffer && bootstrap.InputBuffer != null)
            {
                bootstrap.InputBuffer.OnEntryAdded -= HandleEntryAdded;
                bootstrap.InputBuffer.OnEntryAdded += HandleEntryAdded;
                bootstrap.InputBuffer.OnCleared -= HandleBufferCleared;
                bootstrap.InputBuffer.OnCleared += HandleBufferCleared;
                subscribedBuffer = true;
            }

            if (!subscribedCaster && bootstrap.Caster != null)
            {
                bootstrap.Caster.OnSpellCast -= HandleSpellCast;
                bootstrap.Caster.OnSpellCast += HandleSpellCast;
                bootstrap.Caster.OnMatchFailed -= HandleCastFailed;
                bootstrap.Caster.OnMatchFailed += HandleCastFailed;
                bootstrap.Caster.OnEmptyCastPenalty -= HandleCastFailed;
                bootstrap.Caster.OnEmptyCastPenalty += HandleCastFailed;
                bootstrap.Caster.OnVirtualCastFailed -= HandleCastFailed;
                bootstrap.Caster.OnVirtualCastFailed += HandleCastFailed;
                subscribedCaster = true;
            }
        }

        private void UnsubscribeEvents()
        {
            if (bootstrap != null && bootstrap.InputBuffer != null && subscribedBuffer)
            {
                bootstrap.InputBuffer.OnEntryAdded -= HandleEntryAdded;
                bootstrap.InputBuffer.OnCleared -= HandleBufferCleared;
            }

            if (bootstrap != null && bootstrap.Caster != null && subscribedCaster)
            {
                bootstrap.Caster.OnSpellCast -= HandleSpellCast;
                bootstrap.Caster.OnMatchFailed -= HandleCastFailed;
                bootstrap.Caster.OnEmptyCastPenalty -= HandleCastFailed;
                bootstrap.Caster.OnVirtualCastFailed -= HandleCastFailed;
            }

            subscribedBuffer = false;
            subscribedCaster = false;
        }

        private void HandleSpellCast(SpellDefinition spell, float damageFactor, float damage)
        {
            PlayCastSuccess();
        }

        private void HandleCastFailed()
        {
            PlayCastFailed();
        }

        private void HandleEntryAdded(RhythmInputEntry entry)
        {
            if (entry.InputType != BeatInputType.Chant)
            {
                return;
            }

            PlayChantFromPool(entry.BeatFloat);
        }

        private void HandleBufferCleared()
        {
            if (currentState != CharacterAnimState.Chant)
            {
                return;
            }

            ReturnToIdle();
        }

        private bool ShouldReturnIdleFromChant(float beatFloat)
        {
            if (!hasActiveChantContext || currentState != CharacterAnimState.Chant)
            {
                return false;
            }

            float lifetime = bootstrap != null ? bootstrap.ChantLifetimeBeats : 4f;
            return beatFloat >= activeChantHeadBeatFloat + lifetime;
        }

        private float ResolveFailedStartBeatFloat()
        {
            if (bootstrap == null)
            {
                return beatClock != null ? beatClock.BeatFloat : 0f;
            }

            if (bootstrap.LastSpellLog == "VirtualCastFailed")
            {
                return bootstrap.LastVirtualCastFailedBeatFloat;
            }

            return bootstrap.LastProcessedBeatFloat;
        }

        private void ClearFailedLifecycle()
        {
            failedStateStartBeatFloat = 0f;
            failedStateEndBeatFloat = 0f;
        }

        private void EvaluateCastSuccessClipComplete()
        {
            if (currentState != CharacterAnimState.CastSuccess || actionAnimator == null || !actionAnimator.enabled)
            {
                return;
            }

            if (actionAnimator.IsInTransition(0))
            {
                return;
            }

            AnimatorStateInfo info = actionAnimator.GetCurrentAnimatorStateInfo(0);
            if (!info.IsName(castSuccessStateName))
            {
                return;
            }

            if (!HasPlayableCurrentClip())
            {
                WarnOnce(ref warnedMissingCastSuccessClip, "[Anim] MissingCastSuccessClip | 保持 CastSuccess 直到被覆盖或 MCP ReturnToIdle");
                return;
            }

            if (info.normalizedTime >= 1f)
            {
                ReturnToIdle();
            }
        }

        private bool HasPlayableCurrentClip()
        {
            if (actionAnimator == null)
            {
                return false;
            }

            AnimatorClipInfo[] clips = actionAnimator.GetCurrentAnimatorClipInfo(0);
            if (clips == null || clips.Length == 0 || clips[0].clip == null)
            {
                return false;
            }

            return clips[0].clip.length > 0f;
        }

        private int PickChantIndex()
        {
            int count = ChantPoolCount;
            if (count <= 0)
            {
                return 0;
            }

            if (count == 1 || lastChantIndex < 0)
            {
                return count == 1 ? 0 : Random.Range(0, count);
            }

            int next = Random.Range(0, count - 1);
            if (next >= lastChantIndex)
            {
                next++;
            }

            return next;
        }

        private string ResolveChantStateName(int index)
        {
            if (ChantPoolCount <= 0)
            {
                return "Chant_0";
            }

            int wrapped = ((index % ChantPoolCount) + ChantPoolCount) % ChantPoolCount;
            string name = chantStateNames[wrapped];
            return string.IsNullOrEmpty(name) ? $"Chant_{wrapped}" : name;
        }

        private void EnterActionState(CharacterAnimState state, string animatorStateName)
        {
            ApplyState(state, animatorStateName);
            if (idleAnimator != null)
            {
                idleAnimator.enabled = false;
            }

            TryPlayAnimatorState(animatorStateName);
            Debug.Log(
                $"[Anim] State | name={state} | animator={animatorStateName} | beatFloat={stateStartBeatFloat:0.###} | idleEnabled=False");
        }

        private void ApplyState(CharacterAnimState state, string animatorStateName)
        {
            currentState = state;
            lastRequestedAnimatorState = animatorStateName ?? string.Empty;
            stateStartBeatFloat = beatClock != null ? beatClock.BeatFloat : 0f;
            stateChangeCount++;
        }

        private void ApplyIdleOwnership()
        {
            if (actionAnimator != null)
            {
                actionAnimator.enabled = false;
            }

            if (idleAnimator != null)
            {
                idleAnimator.enabled = true;
            }
        }

        private void TryPlayAnimatorState(string stateName)
        {
            if (string.IsNullOrEmpty(stateName))
            {
                return;
            }

            if (actionAnimator == null)
            {
                WarnOnce(ref warnedMissingAnimator, "[Anim] MissingAnimator | 非 Idle 需要 PlayerVisual 上的 Animator");
                return;
            }

            actionAnimator.enabled = true;
            if (actionAnimator.runtimeAnimatorController == null)
            {
                WarnOnce(ref warnedMissingController, "[Anim] MissingAnimatorController | 请在 Inspector 指定 Controller 并配置 Clip");
                return;
            }

            int hash = Animator.StringToHash(stateName);
            if (!actionAnimator.HasState(0, hash))
            {
                if (warnedMissingStates.Add(stateName))
                {
                    Debug.LogWarning($"[Anim] MissingAnimatorState | state={stateName}");
                }

                return;
            }

            actionAnimator.Play(hash, 0, 0f);
            actionAnimator.Update(0f);
        }

        private static void WarnOnce(ref bool flag, string message)
        {
            if (flag)
            {
                return;
            }

            flag = true;
            Debug.LogWarning(message);
        }
    }
}
