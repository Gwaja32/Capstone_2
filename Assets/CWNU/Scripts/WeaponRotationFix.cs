using UnityEngine;

public class WeaponRotationFix : StateMachineBehaviour
{
    // 여기서 인스펙터를 통해 몇 도 돌릴지 설정 (X, Y, Z)
    public Vector3 rotationOffset = new Vector3(90f, 0f, 0f);

    // 애니메이션이 시작될 때 딱 한 번 실행
    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        WeaponFixer fixer = animator.GetComponent<WeaponFixer>();
        if (fixer != null)
        {
            fixer.ApplyOffset(rotationOffset);
        }
    }

    // 애니메이션이 끝날 때(혹은 중간에 캔슬돼서 다른 상태로 넘어갈 때) 무조건 실행
    override public void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        WeaponFixer fixer = animator.GetComponent<WeaponFixer>();
        if (fixer != null)
        {
            fixer.ResetOffset();
        }
    }
}