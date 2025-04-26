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
        public int CellSize = 1;
        [Header("Swap Settings")] 
        public float SwapDuration = 0.5f;
        public Ease SwapEase = Ease.OutBack;
        [Header("Fall Settings")]
        public float FallDuration = 1.0f;
        public Ease FallEase = Ease.InQuint;
    }
}