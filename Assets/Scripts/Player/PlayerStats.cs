using System;
using UnityEngine;

namespace Willowstead.Player
{
    /// <summary>
    /// Manages player vital stats (Health & Stamina), regeneration logic,
    /// stamina consumption during sprinting/farming, damage handling, and stat events.
    /// </summary>
    public class PlayerStats : MonoBehaviour
    {
        public static PlayerStats Instance { get; private set; }

        [Header("Health Settings")]
        [SerializeField] private float _maxHealth = 100f;
        [SerializeField] private float _currentHealth = 100f;

        [Header("Stamina Settings")]
        [SerializeField] private float _maxStamina = 100f;
        [SerializeField] private float _currentStamina = 100f;
        [SerializeField] private float _staminaRegenRate = 20f;        // Points regenerated per second
        [SerializeField] private float _staminaRegenDelay = 1.2f;       // Delay in seconds after last stamina use before regen begins
        [SerializeField] private float _sprintStaminaCost = 14f;        // Stamina cost per second of sprinting

        [Header("Invulnerability")]
        [SerializeField] private float _invulnerabilityDuration = 0.6f;

        private float _lastStaminaUseTime;
        private float _invulnerabilityTimer;
        private bool _isGodMode = false;

        public float MaxHealth => _maxHealth;
        public float CurrentHealth => _currentHealth;
        public float MaxStamina => _maxStamina;
        public float CurrentStamina => _currentStamina;
        public float HealthPercent => Mathf.Clamp01(_maxHealth > 0f ? _currentHealth / _maxHealth : 0f);
        public float StaminaPercent => Mathf.Clamp01(_maxStamina > 0f ? _currentStamina / _maxStamina : 0f);
        public bool IsDead => _currentHealth <= 0f;
        public bool IsExhausted => _currentStamina <= 0.05f;
        public bool GodMode { get => _isGodMode; set => _isGodMode = value; }

        public event Action<float, float> OnHealthChanged;      // (current, max)
        public event Action<float, float> OnStaminaChanged;     // (current, max)
        public event Action OnPlayerDied;
        public event Action OnStaminaExhausted;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(this);
                return;
            }

            _currentHealth = _maxHealth;
            _currentStamina = _maxStamina;
        }

        private void Update()
        {
            if (_invulnerabilityTimer > 0f)
            {
                _invulnerabilityTimer -= Time.deltaTime;
            }

            // Stamina Regeneration
            if (_currentStamina < _maxStamina && Time.time >= _lastStaminaUseTime + _staminaRegenDelay)
            {
                _currentStamina = Mathf.Min(_maxStamina, _currentStamina + _staminaRegenRate * Time.deltaTime);
                OnStaminaChanged?.Invoke(_currentStamina, _maxStamina);
            }
        }

        /// <summary>
        /// Attempts to consume a given amount of stamina. Returns true if sufficient stamina was available.
        /// </summary>
        public bool UseStamina(float amount)
        {
            if (_isGodMode) return true;
            if (amount <= 0f) return true;

            if (_currentStamina >= amount)
            {
                _currentStamina = Mathf.Max(0f, _currentStamina - amount);
                _lastStaminaUseTime = Time.time;
                OnStaminaChanged?.Invoke(_currentStamina, _maxStamina);

                if (_currentStamina <= 0.05f)
                {
                    OnStaminaExhausted?.Invoke();
                }
                return true;
            }

            OnStaminaExhausted?.Invoke();
            return false;
        }

        /// <summary>
        /// Consumes continuous stamina (e.g. for sprinting over deltaTime). Returns true if stamina remains.
        /// </summary>
        public bool ConsumeSprintStamina(float deltaTime)
        {
            if (_isGodMode) return true;

            float cost = _sprintStaminaCost * deltaTime;
            if (_currentStamina > 0f)
            {
                _currentStamina = Mathf.Max(0f, _currentStamina - cost);
                _lastStaminaUseTime = Time.time;
                OnStaminaChanged?.Invoke(_currentStamina, _maxStamina);

                if (_currentStamina <= 0.05f)
                {
                    OnStaminaExhausted?.Invoke();
                    return false;
                }
                return true;
            }

            return false;
        }

        public bool HasStamina(float amount)
        {
            if (_isGodMode) return true;
            return _currentStamina >= amount;
        }

        /// <summary>
        /// Restores player stamina by a discrete amount.
        /// </summary>
        public void RestoreStamina(float amount)
        {
            if (amount <= 0f) return;
            _currentStamina = Mathf.Min(_maxStamina, _currentStamina + amount);
            OnStaminaChanged?.Invoke(_currentStamina, _maxStamina);
        }

        /// <summary>
        /// Applies damage to player health unless invulnerable or in god mode.
        /// </summary>
        public void TakeDamage(float amount)
        {
            if (_isGodMode || IsDead || _invulnerabilityTimer > 0f) return;
            if (amount <= 0f) return;

            _invulnerabilityTimer = _invulnerabilityDuration;
            _currentHealth = Mathf.Max(0f, _currentHealth - amount);
            OnHealthChanged?.Invoke(_currentHealth, _maxHealth);

            if (_currentHealth <= 0f)
            {
                Die();
            }
        }

        /// <summary>
        /// Restores player health by a discrete amount.
        /// </summary>
        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f) return;

            _currentHealth = Mathf.Min(_maxHealth, _currentHealth + amount);
            OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
        }

        public void SetHealth(float current, float max)
        {
            _maxHealth = Mathf.Max(1f, max);
            _currentHealth = Mathf.Clamp(current, 0f, _maxHealth);
            OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
        }

        public void SetStamina(float current, float max)
        {
            _maxStamina = Mathf.Max(1f, max);
            _currentStamina = Mathf.Clamp(current, 0f, _maxStamina);
            OnStaminaChanged?.Invoke(_currentStamina, _maxStamina);
        }

        private void Die()
        {
            Debug.Log("[PlayerStats] Player has died.");
            OnPlayerDied?.Invoke();
        }
    }
}
