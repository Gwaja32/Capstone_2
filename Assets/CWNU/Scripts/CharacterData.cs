using UnityEngine;

[CreateAssetMenu(fileName = "NewCharacterData", menuName = "Character/Data")]
public class CharacterData : ScriptableObject
{
    [Header("Character Info")]
    public string characterName;

    [Header("Stats")]
    public float maxHealth = 100f;
    public float moveSpeed = 1f;

    [Header("Stamina Stats")]
    public float maxStamina = 100f;             //최대 스태미나
    public float staminaRegenRate = 5f;        //스태미나 회복량
    public float guardStaminaCost = 30f;       // 가드 성공 시 스태미나 소모
    public float attackStaminaCost = 20f;      // 일반 공격 시 스태미나 소모
    public float parryStaminaCost = 20f;       // 패링 시도 시 스태미나 소모
    public float criticalAttackStaminaCost = 15f; //크리티컬 공격시 스테미나 소모

    [Header("Combat Settings")]
    public float attackRange = 1.2f;
    public float attackDuration = 1.0f;
    public float parryDuration = 1.0f;

    [Header("Animation Clips")]
    public AnimationClip topHitClip;
    public AnimationClip sideHitClip;
}