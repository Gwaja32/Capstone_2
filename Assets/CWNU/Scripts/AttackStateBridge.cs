using UnityEngine;

public class AttackStateBridge : StateMachineBehaviour
{
    // 이 스크립트가 붙은 애니메이션 상태(State)가 종료될 때 자동으로 호출됩니다.
    override public void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // 애니메이터가 붙은 오브젝트에서 이동 스크립트를 찾습니다.
        var movement = animator.GetComponent<TPSFixedMovement>();

        if (movement != null)
        {
            // 공격이 끝났으니 다시 조작 가능 상태로 변경!
            movement.isInteracting = true;
        }
    }
}