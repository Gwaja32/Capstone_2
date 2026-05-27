using UnityEngine;

public static class TransformExtensions
{
    /// <summary>
    /// 자식 오브젝트의 하위 계층(Deep)까지 탐색하여 해당 이름의 Transform을 찾습니다.
    /// </summary>
    public static Transform FindDeepChild(this Transform parent, string id)
    {
        // 1. 현재 자식들 중에서 먼저 검색
        Transform result = parent.Find(id);
        if (result != null) return result;

        // 2. 못 찾았다면 자식의 자식들까지 재귀(Recursive) 탐색
        foreach (Transform child in parent)
        {
            result = child.FindDeepChild(id);
            if (result != null) return result;
        }

        return null;
    }
}