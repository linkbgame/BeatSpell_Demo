using System.Collections.Generic;
using BeatSpell.Rhythm;
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
    /// P2：覆盖式角色动画骨架。Idle 由 BeatIdleAnimator 按 BeatFloat 驱动；
    /// 非 Idle 交给 Animator Controller / Clip，具体动作由用户在 Unity 中配置。
    /// </summary>
    public class CharacterBeatAnimationController : MonoBehaviour
    {
        [SerializeField] private BeatClock beatClock;
        [SerializeField] private BeatIdleAnimator idleAnimator;
        [SerializeField] private Animator actionAnimator;
        [SerializeField] private string chantStateName = "Chant";
        [SerializeField] private string castSuccessStateName = "CastSuccess";
        [SerializeField] private string castFailedStateName = "CastFailed";

        private CharacterAnimState currentState = CharacterAnimState.Idle;
        private float stateStartBeatFloat;
        private int stateChangeCount;
        private string lastRequestedAnimatorState = string.Empty;
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

        public void Bind(BeatClock clock, BeatIdleAnimator idle, Animator animator)
        {
            beatClock = clock;
            idleAnimator = idle;
            actionAnimator = animator;
        }

        public void ResetDebugState()
        {
            stateChangeCount = 0;
            warnedMissingAnimator = false;
            warnedMissingController = false;
            warnedMissingStates.Clear();
            ReturnToIdle();
            stateChangeCount = 0;
        }

        public void PlayChant()
        {
            EnterActionState(CharacterAnimState.Chant, chantStateName);
        }

        public void PlayCastSuccess()
        {
            EnterActionState(CharacterAnimState.CastSuccess, castSuccessStateName);
        }

        public void PlayCastFailed()
        {
            EnterActionState(CharacterAnimState.CastFailed, castFailedStateName);
        }

        public void ReturnToIdle()
        {
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

        private void Awake()
        {
            CacheRefs();
        }

        private void OnEnable()
        {
            CacheRefs();
            if (currentState == CharacterAnimState.Idle)
            {
                ApplyIdleOwnership();
            }
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
