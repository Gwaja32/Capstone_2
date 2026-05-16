using UnityEngine;

[CreateAssetMenu(fileName = "NewCharacterData", menuName = "Character/Data")]
public class CharacterData : ScriptableObject
{
    [Header("Character Info")]
    public string characterName;

    [Header("Stats")]
    public float maxHealth = 100f;
    public float maxStamina = 100f;
    public float staminaRegenRate = 20f;
    public float moveSpeed = 1f;

    [Header("Combat Settings")]
    public float attackRange = 1.2f;
    public float attackDuration = 1.0f;
    public float parryDuration = 1.0f;

    [Header("Animation Clips")]
    public AnimationClip topHitClip;
    public AnimationClip sideHitClip;
}