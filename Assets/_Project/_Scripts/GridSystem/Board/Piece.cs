using KBCore.Refs;
using UnityEngine;

namespace MatchThreeSystem
{
    public class Piece : ValidatedMonoBehaviour
    {
        [SerializeField, Child] private SpriteRenderer spriteRenderer;
        public PieceInfo info;
        
        public PieceType Type => info.type;

        public void Init(PieceInfo initInfo) {
            info = initInfo;
            spriteRenderer.sprite = info.sprite;
        }
    }
    public enum PieceType
    {
        None,
        Attack,
        Defense,
        Health,
        Mana
    }
}