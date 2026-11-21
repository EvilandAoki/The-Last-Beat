using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLastBeat.Player
{
    /// <summary>
    /// Traduce el Input System a valores y eventos simples. El resto del código
    /// (movimiento, combate, habilidades) no conoce el Input System: solo lee
    /// Move y se suscribe a los eventos. Así el mismo controlador podría moverse
    /// por IA o por una cinemática sin cambios.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class PlayerInputReader : MonoBehaviour
    {
        [SerializeField] private InputActionAsset actions;
        [SerializeField] private string mapName = "Gameplay";

        private InputActionMap map;
        private InputAction move;
        private InputAction attack;
        private InputAction heavyAttack;
        private InputAction ability1;
        private InputAction ability2;
        private InputAction pause;

        /// <summary>Dirección del stick o WASD, magnitud de 0 a 1.</summary>
        public Vector2 Move { get; private set; }

        /// <summary>Ataque débil (J, clic izquierdo, botón oeste).</summary>
        public event Action AttackPressed;

        /// <summary>Ataque fuerte (K, botón sur).</summary>
        public event Action HeavyAttackPressed;
        public event Action Ability1Pressed;
        public event Action Ability2Pressed;
        public event Action PausePressed;

        private void Awake()
        {
            if (actions == null)
            {
                Debug.LogError($"[{nameof(PlayerInputReader)}] Falta asignar el InputActionAsset (GameControls).", this);
                enabled = false;
                return;
            }

            map = actions.FindActionMap(mapName, throwIfNotFound: true);
            move = map.FindAction("Move", throwIfNotFound: true);
            attack = map.FindAction("Attack", throwIfNotFound: true);
            heavyAttack = map.FindAction("HeavyAttack", throwIfNotFound: true);
            ability1 = map.FindAction("Ability1", throwIfNotFound: true);
            ability2 = map.FindAction("Ability2", throwIfNotFound: true);
            pause = map.FindAction("Pause", throwIfNotFound: true);
        }

        private void OnEnable()
        {
            if (map == null) return;
            attack.performed += OnAttack;
            heavyAttack.performed += OnHeavyAttack;
            ability1.performed += OnAbility1;
            ability2.performed += OnAbility2;
            pause.performed += OnPause;
            map.Enable();
        }

        private void OnDisable()
        {
            if (map == null) return;
            attack.performed -= OnAttack;
            heavyAttack.performed -= OnHeavyAttack;
            ability1.performed -= OnAbility1;
            ability2.performed -= OnAbility2;
            pause.performed -= OnPause;
            map.Disable();
            Move = Vector2.zero;
        }

        private void Update()
        {
            Move = Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f);
        }

        /// <summary>Activa o desactiva el control del jugador (diálogos, pausa, cinemáticas).</summary>
        public void SetGameplayEnabled(bool value)
        {
            if (map == null) return;
            if (value) map.Enable();
            else
            {
                map.Disable();
                Move = Vector2.zero;
            }
        }

        private void OnAttack(InputAction.CallbackContext _) => AttackPressed?.Invoke();
        private void OnHeavyAttack(InputAction.CallbackContext _) => HeavyAttackPressed?.Invoke();
        private void OnAbility1(InputAction.CallbackContext _) => Ability1Pressed?.Invoke();
        private void OnAbility2(InputAction.CallbackContext _) => Ability2Pressed?.Invoke();
        private void OnPause(InputAction.CallbackContext _) => PausePressed?.Invoke();
    }
}
