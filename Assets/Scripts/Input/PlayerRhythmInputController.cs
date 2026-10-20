using System;
using BeatSpell.Prototype;
using BeatSpell.Rhythm;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BeatSpell.RhythmInput
{
    /// <summary>
    /// 咏唱 / 施法输入层。在 performed 回调里取 dspTime，不在 Update 中 GetComponent。
    /// 第 2 轮可直接订阅 OnRhythmInput。
    /// </summary>
    public class PlayerRhythmInputController : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private BeatClock beatClock;
        [SerializeField] private RhythmPrototypeBootstrap bootstrap;

        public event Action<BeatInputType, BeatTimingGrade, float, int> OnRhythmInput;

        private InputAction chantAction;
        private InputAction castAction;
        private bool ownsFallbackActions;

        public void Bind(BeatClock clock, RhythmPrototypeBootstrap host, InputActionAsset actions)
        {
            beatClock = clock;
            bootstrap = host;
            if (actions != null)
            {
                inputActions = actions;
            }
        }

        private void Awake()
        {
            if (beatClock == null)
            {
                beatClock = GetComponent<BeatClock>();
            }

            if (bootstrap == null)
            {
                bootstrap = GetComponent<RhythmPrototypeBootstrap>();
            }

            ResolveActions();
        }

        private void OnEnable()
        {
            Subscribe(chantAction, OnChantPerformed);
            Subscribe(castAction, OnCastPerformed);
            chantAction?.Enable();
            castAction?.Enable();
        }

        private void OnDisable()
        {
            Unsubscribe(chantAction, OnChantPerformed);
            Unsubscribe(castAction, OnCastPerformed);
            chantAction?.Disable();
            castAction?.Disable();
        }

        private void OnDestroy()
        {
            if (!ownsFallbackActions)
            {
                return;
            }

            chantAction?.Dispose();
            castAction?.Dispose();
        }

        private void OnChantPerformed(InputAction.CallbackContext context)
        {
            if (!context.performed)
            {
                return;
            }

            Dispatch(BeatInputType.Chant);
        }

        private void OnCastPerformed(InputAction.CallbackContext context)
        {
            if (!context.performed)
            {
                return;
            }

            Dispatch(BeatInputType.Cast);
        }

        private void Dispatch(BeatInputType type)
        {
            if (bootstrap == null)
            {
                return;
            }

            bootstrap.ProcessLiveInput(type);
        }

        /// <summary>
        /// 由 Bootstrap 在完成判定后回写，向外抛出第 2 轮可订阅的事件。
        /// </summary>
        public void RaiseRhythmInput(BeatInputType type, BeatTimingGrade grade, float deltaMs, int beatIndex)
        {
            OnRhythmInput?.Invoke(type, grade, deltaMs, beatIndex);
        }

        private void ResolveActions()
        {
            if (inputActions != null)
            {
                InputActionMap map = inputActions.FindActionMap("BeatSpell", throwIfNotFound: false);
                chantAction = map?.FindAction("Chant", throwIfNotFound: false);
                castAction = map?.FindAction("Cast", throwIfNotFound: false);
                map?.Enable();
            }

            if (chantAction != null && castAction != null)
            {
                return;
            }

            ownsFallbackActions = true;
            chantAction = new InputAction("Chant", InputActionType.Button);
            chantAction.AddBinding("<Keyboard>/j");
            chantAction.AddBinding("<Gamepad>/buttonSouth");
            castAction = new InputAction("Cast", InputActionType.Button);
            castAction.AddBinding("<Keyboard>/k");
            castAction.AddBinding("<Gamepad>/buttonEast");
        }

        private static void Subscribe(InputAction action, Action<InputAction.CallbackContext> handler)
        {
            if (action == null)
            {
                return;
            }

            action.performed += handler;
        }

        private static void Unsubscribe(InputAction action, Action<InputAction.CallbackContext> handler)
        {
            if (action == null)
            {
                return;
            }

            action.performed -= handler;
        }
    }
}
