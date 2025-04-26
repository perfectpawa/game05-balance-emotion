using DG.Tweening;
using UnityEngine;

namespace MatchThreeSystem
{
    [CreateAssetMenu(fileName = "BoardInfo", menuName = "SO/BoardInfo", order = 0)]
    public class BoardInfo : ScriptableObject
    {
        [Header("Grid Settings")] 
        public float MaskOffset = 0.5f;
        public int Width = 5;
        public int Height = 5;
        public int Extra = 5;
        public float CellSize = 1;
        [Header("Select Settings")] 
        public float SelectPopDuration = 0.1f;
        public float SelectPopOutScale = 1.2f;
        public float SelectPopOffScale = 1f;
        public Ease SelectPopSwapEase = Ease.OutBack;
        [Header("Swap Settings")] 
        public float SwapDuration = 0.5f;
        public Ease SwapEase = Ease.OutBack;
        [Header("Explode Settings")]
        public float ExplodeDuration = 0.1f;
        public Ease ExplodeEase = Ease.OutBack;
        [Header("Fall Settings")]
        public float FallDuration = 1.0f;
        public Ease FallEase = Ease.InQuint;
    }
}