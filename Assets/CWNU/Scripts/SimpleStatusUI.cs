using UnityEngine;
using UnityEngine.UI;

public class SimpleStatusUI : MonoBehaviour
{
    public TPSFixedMovement playerSource;
    public EnemyAI enemySource;

    public Image hpBar;
    public Image staminaBar;

    // 🔴 [추가] 배틀 씬에 진입했을 때 BattleManager가 UI 컴포넌트들을 주입해줄 함수
    public void SetupUI(Image newHpBar, Image newStaminaBar, EnemyAI aiSource)
    {
        hpBar = newHpBar;
        staminaBar = newStaminaBar;
        enemySource = aiSource;
        playerSource = null; // 적 전용 UI이므로 플레이어 소스는 비워둡니다.
    }

    void Update()
    {
        if (playerSource != null && hpBar != null && staminaBar != null)
        {
            float maxHp = playerSource.getMaxHealth() > 0 ? (float)playerSource.getMaxHealth() : 1f;
            float maxStamina = playerSource.getMaxStamina() > 0 ? (float)playerSource.getMaxStamina() : 1f;

            hpBar.fillAmount = (float)playerSource.getCurrentHealth() / maxHp;
            staminaBar.fillAmount = (float)playerSource.getCurrentStamina() / maxStamina;
        }
        else if (enemySource != null && hpBar != null && staminaBar != null)
        {
            float maxHp = enemySource.maxHealth > 0 ? enemySource.maxHealth : 1f;
            float maxStamina = enemySource.maxStamina > 0 ? enemySource.maxStamina : 1f;

            hpBar.fillAmount = enemySource.currentHealth / maxHp;
            staminaBar.fillAmount = enemySource.currentStamina / maxStamina;
        }
    }
}