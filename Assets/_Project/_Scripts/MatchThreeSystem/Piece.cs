using KBCore.Refs;
using UnityEngine;

namespace MatchThreeSystem
{
    public class Piece : ValidatedMonoBehaviour
    {
        [SerializeField, Self] private SpriteRenderer spriteRenderer;
        public PieceInfo info;
        
        public PieceType Type => info.type;

        public void Init(PieceInfo initInfo) {
            if (info == null) {
                Debug.LogWarning("PieceInfo is not set.");
                return;
            }
            
            info = initInfo;
            spriteRenderer.sprite = info.sprite;
        }
    }
    
    public enum PieceType
    {
        Red,
        Green,
        Blue,
        Yellow,
        Purple
    }
}