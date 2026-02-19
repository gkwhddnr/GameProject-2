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
        // TODO: 체력 회복 로직 구현
    }
}

// ============================================
// Shield 아이템 효과 (★ 수정됨)
// ============================================
public class ShieldItemEffect : IItemEffect
{
    public void Execute(GameObject player)
    {
        Debug.Log("[ShieldItemEffect] Shield 아이템 사용! 영구 보호막 활성화");

        // ShieldEffectController에서 Shield 활성화
        ShieldEffectController controller = Object.FindFirstObjectByType<ShieldEffectController>();

        if (controller == null)
        {
            Debug.LogError("[ShieldItemEffect] ShieldEffectController를 찾을 수 없습니다!");
            return;
        }

        if (controller.IsShieldActive)
        {
            Debug.LogWarning("[ShieldItemEffect] Shield가 이미 활성화되어 있습니다! 사용할 수 없습니다.");
            return;
        }

        controller.ActivateShield(player);
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
        // TODO: Bomb 효과 구현
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
        // TODO: Shuffle 효과 구현
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
        // TODO: Hint 효과 구현
    }
}