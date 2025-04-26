using UnityEngine;

namespace MatchThreeSystem
{
        [CreateAssetMenu(fileName = "Piece Type", menuName = "SO/PieceType", order = 0)]
        public class PieceInfo : ScriptableObject
        {
                public PieceType type;
                public Sprite sprite;
        }
}