using UnityEngine;

/// <summary>
/// 아이템 효과 인터페이스 (Strategy Pattern)
/// </summary>
public interface IItemEffect
{
    void Execute(GameObject player);
}

// ============================================
// Kit 아이템 효과
// ============================================
public class KitItemEffect : IItemEffect
{
    public void Execute(GameObject player)
    {
        Debug.Log("[KitItemEffect] Kit 아이템 사용! 체력 회복");
    }
}

// ============================================
// Shield 아이템 효과
// ============================================
public class ShieldItemEffect : IItemEffect
{
    private float shieldDuration = 5f;

    public void Execute(GameObject player)
    {
        Debug.Log("[ShieldItemEffect] Shield 아이템 사용! 5초간 무적");
    }

}

// ============================================
// Bomb 아이템 효과
// ============================================
public class BombItemEffect : IItemEffect
{
    public void Execute(GameObject player)
    {
        Debug.Log("[BombItemEffect] Bomb 아이템 사용! 주변 적 제거");

    }
}

// ============================================
// Shuffle 아이템 효과
// ============================================
public class ShuffleItemEffect : IItemEffect
{
    public void Execute(GameObject player)
    {
        Debug.Log("[ShuffleItemEffect] Shuffle 아이템 사용! 퍼즐 섞기");
    }
}

// ============================================
// Hint 아이템 효과
// ============================================
public class HintItemEffect : IItemEffect
{
    public void Execute(GameObject player)
    {
        Debug.Log("[HintItemEffect] Hint 아이템 사용! 힌트 표시");
    }
}