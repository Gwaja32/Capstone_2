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
        // 데이터와 UI가 모두 연결된 상태에서만 실시간 업데이트 수행
        if (playerSource != null && hpBar != null && staminaBar != null)
        {
            hpBar.fillAmount = playerSource.getCurrentHealth() / playerSource.getMaxHealth();
            staminaBar.fillAmount = playerSource.getCurrentStamina() / playerSource.getMaxStamina();
        }
        else if (enemySource != null && hpBar != null && staminaBar != null)
        {
            // 분모가 0이 되어 fillAmount가 터지는 현상 방지 예외 처리
            float maxHp = enemySource.maxHealth > 0 ? enemySource.maxHealth : 1f;
            float maxStamina = enemySource.maxStamina > 0 ? enemySource.maxStamina : 1f;

            hpBar.fillAmount = enemySource.currentHealth / maxHp;
            staminaBar.fillAmount = enemySource.currentStamina / maxStamina;
        }
    }
}