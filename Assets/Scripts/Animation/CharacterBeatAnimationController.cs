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
    /// P3：Idle 由 BeatIdleAnimator 按 BeatFloat 驱动；合法 Chant 从 5 个 Animator state 池抽一个播放。
    /// CastSuccess / CastFailed 入口保留，本轮不订阅施法结果事件。
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
        private bool hasActiveChantContext;
        private bool subscribed;
        private bool warnedMissingAnimator;
        private bool warnedMissingController;
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

        public void Bind(
            BeatClock clock,
            BeatIdleAnimator idle,
            Animator animator,
            RhythmPrototypeBootstrap host = null)
        {
            UnsubscribeBuffer();
            beatClock = clock;
            idleAnimator = idle;
            actionAnimator = animator;
            if (host != null)
            {
                bootstrap = host;
            }

            SubscribeBuffer();
        }

        public void ResetDebugState()
        {
            stateChangeCount = 0;
            lastChantIndex = -1;
            previousChantIndex = -1;
            activeChantHeadBeatFloat = 0f;
            hasActiveChantContext = false;
            warnedMissingAnimator = false;
            warnedMissingController = false;
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
            EnterActionState(CharacterAnimState.CastSuccess, castSuccessStateName);
        }

        public void PlayCastFailed()
        {
            hasActiveChantContext = false;
            EnterActionState(CharacterAnimState.CastFailed, castFailedStateName);
        }

        public void ReturnToIdle()
        {
            hasActiveChantContext = false;
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

        private void Awake()
        {
            CacheRefs();
        }

        private void OnEnable()
        {
            CacheRefs();
            SubscribeBuffer();
            if (currentState == CharacterAnimState.Idle)
            {
                ApplyIdleOwnership();
            }
        }

        private void OnDisable()
        {
            UnsubscribeBuffer();
        }

        private void Update()
        {
            if (beatClock == null || !beatClock.IsRunning)
            {
                return;
            }

            EvaluateChantLifecycle(beatClock.BeatFloat);
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

        private void SubscribeBuffer()
        {
            if (subscribed || bootstrap == null || bootstrap.InputBuffer == null)
            {
                return;
            }

            bootstrap.InputBuffer.OnEntryAdded -= HandleEntryAdded;
            bootstrap.InputBuffer.OnEntryAdded += HandleEntryAdded;
            bootstrap.InputBuffer.OnCleared -= HandleBufferCleared;
            bootstrap.InputBuffer.OnCleared += HandleBufferCleared;
            subscribed = true;
        }

        private void UnsubscribeBuffer()
        {
            if (!subscribed || bootstrap == null || bootstrap.InputBuffer == null)
            {
                subscribed = false;
                return;
            }

            bootstrap.InputBuffer.OnEntryAdded -= HandleEntryAdded;
            bootstrap.InputBuffer.OnCleared -= HandleBufferCleared;
            subscribed = false;
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
