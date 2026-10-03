using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace uiuxforgedev.bolduisystemDEMO
{
    public class HealthBarWithBuffer : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Slider healthSlider;
        [SerializeField] private Slider bufferSlider;
        [SerializeField] private TextMeshProUGUI healthText;
        [SerializeField] private ParticleSystem healParticles;

        [Header("Health Settings")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float changeAmount = 10f;
        [SerializeField] private bool isDamageMode = true;

        [Header("Buffer Settings")]
        [SerializeField] private float bufferDelay = 0.5f;
        [SerializeField] private float bufferLerpSpeed = 5f;

        [Header("Heal Animation")]
        [SerializeField] private float healLerpSpeed = 5f;

        private float currentHealth;
        private float displayedHealth;
        private float bufferHealth;
        private float delayTimer;
        private bool isHealing;

        private void Awake()
        {
            StopHealParticles();
            currentHealth = maxHealth;
            displayedHealth = maxHealth;
            bufferHealth = maxHealth;

            healthSlider.minValue = 0f;
            healthSlider.maxValue = maxHealth;

            bufferSlider.minValue = 0f;
            bufferSlider.maxValue = maxHealth;

            UpdateUIInstant();
        }

        private void Update()
        {
            UpdateHealAnimation();
            UpdateBuffer();
        }

        /// <summary>
        /// Will drain or regain the progress bar based on the damage mode. Safe to call from code or UI Events.
        /// </summary>
        public void OnButtonClick()
        {
            if (isDamageMode)
                ApplyDamage(changeAmount);
            else
                Heal(changeAmount);
        }

        /// <summary>
        /// Set whether the progress bar is in drain or regain mode. Safe to call from code or UI Events.
        /// </summary>
        public void SetDamageMode(bool damageMode)
        {
            isDamageMode = damageMode;
        }

        private void ApplyDamage(float amount)
        {
            StopHealParticles();

            currentHealth = Mathf.Clamp(currentHealth - amount, 0f, maxHealth);
            
            delayTimer = bufferDelay;
            
            isHealing = false;
            
            UpdateHealthUI();
        }

        private void Heal(float amount)
        {
            currentHealth = Mathf.Clamp(currentHealth + amount, 0f, maxHealth);

            // Heal snaps buffer instantly
            bufferHealth = currentHealth;
            isHealing = true;
            StartHealParticles();
            //UpdateUIInstant();
        }

        private void UpdateHealAnimation()
        {
            if (!isHealing)
                return;

            displayedHealth = Mathf.Lerp(displayedHealth, currentHealth, healLerpSpeed * Time.deltaTime);

            if (Mathf.Abs(displayedHealth - currentHealth) < 0.1f)
            {
                displayedHealth = currentHealth;
                isHealing = false;
                StopHealParticles();
            }

            healthSlider.value = displayedHealth;
            healthText.text = $"{Mathf.RoundToInt(displayedHealth)} / {Mathf.RoundToInt(maxHealth)}";

            // heal syncs buffer instantly (industry standard)
            bufferHealth = displayedHealth;
            bufferSlider.value = bufferHealth;
        }

        private void UpdateBuffer()
        {
            if (bufferHealth <= currentHealth)
                return;

            if (delayTimer > 0f)
            {
                delayTimer -= Time.deltaTime;
                return;
            }

            bufferHealth = Mathf.Lerp(bufferHealth, currentHealth, bufferLerpSpeed * Time.deltaTime);

            if (Mathf.Abs(bufferHealth - currentHealth) < 0.1f)
                bufferHealth = currentHealth;

            bufferSlider.value = bufferHealth;
        }

        private void UpdateHealthUI()
        {
            displayedHealth = currentHealth;
            healthSlider.value = displayedHealth;
            healthText.text = $"{Mathf.RoundToInt(currentHealth)} / {Mathf.RoundToInt(maxHealth)}";
        }

        private void UpdateUIInstant()
        {
            healthSlider.value = currentHealth;
            bufferSlider.value = bufferHealth;
            healthText.text = $"{Mathf.RoundToInt(currentHealth)} / {Mathf.RoundToInt(maxHealth)}";
        }

        private void StartHealParticles()
        {
            if (healParticles == null)
                return;

            if (!healParticles.gameObject.activeSelf)
                healParticles.gameObject.SetActive(true);

            if (!healParticles.isPlaying)
                healParticles.Play();
        }

        private void StopHealParticles()
        {
            if (healParticles == null)
                return;

            healParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);

            // disable after fully stopped
            healParticles.gameObject.SetActive(false);
        }

        
    }
}