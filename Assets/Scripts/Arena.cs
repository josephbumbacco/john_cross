using UnityEngine;

namespace JohnCross
{
    // Rectangular play area centred on the origin.
    public static class Arena
    {
        public static readonly Vector2 HalfSize = new Vector2(18f, 10f);
        const float Margin = 0.5f;

        public static bool Contains(Vector2 p) =>
            Mathf.Abs(p.x) <= HalfSize.x && Mathf.Abs(p.y) <= HalfSize.y;

        public static Vector2 Clamp(Vector2 p) => new Vector2(
            Mathf.Clamp(p.x, -HalfSize.x + Margin, HalfSize.x - Margin),
            Mathf.Clamp(p.y, -HalfSize.y + Margin, HalfSize.y - Margin));

        public static Vector2 RandomSpawnPoint() => new Vector2(
            Random.Range(-HalfSize.x + 2f, HalfSize.x - 2f),
            Random.Range(-HalfSize.y + 2f, HalfSize.y - 2f));
    }
}
