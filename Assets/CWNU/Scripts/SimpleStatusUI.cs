using UnityEngine;
using UnityEngine.UI;

public class SimpleStatusUI : MonoBehaviour
{
    // 이미 배치된 UI이니 데이터 소스만 연결해주면 됩니다.
    public TPSFixedMovement playerSource;
    public EnemyAI enemySource;

    public Image hpBar;
    public Image staminaBar;

    void Update()
    {
        // 데이터가 연결된 놈만 업데이트
        if (playerSource != null)
        {
            hpBar.fillAmount = playerSource.getCurrentHealth() / playerSource.getMaxHealth();
            staminaBar.fillAmount = playerSource.getCurrentStamina() / playerSource.getMaxStamina();
        }
        else if (enemySource != null)
        {
            hpBar.fillAmount = enemySource.currentHealth / enemySource.maxHealth;
            staminaBar.fillAmount = enemySource.currentStamina / enemySource.maxStamina;
        }
    }
}