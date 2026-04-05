using UnityEngine;

public class WeaponFixer : MonoBehaviour
{
    public Transform weaponTransform; // 인스펙터에서 검 오브젝트를 드래그해서 넣기
    private Quaternion originalRotation;

    // 무기 각도 틀기
    public void ApplyOffset(Vector3 offset)
    {
        if (weaponTransform != null)
        {
            originalRotation = weaponTransform.localRotation; // 원래 각도 저장
            weaponTransform.localRotation *= Quaternion.Euler(offset); // 원하는 만큼 회전 추가
        }
    }

    // 무기 원래대로 복구
    public void ResetOffset()
    {
        if (weaponTransform != null)
        {
            weaponTransform.localRotation = originalRotation; // 저장해둔 원래 각도로 롤백
        }
    }
}