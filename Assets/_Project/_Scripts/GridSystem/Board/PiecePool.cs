using System;
using System.Collections.Generic;
using UnityEngine;

namespace MatchThreeSystem
{
    public class PiecePool : PoolerBase<Piece>
    {
        [SerializeField] private List<PieceInfo> _pieceInfos;

        protected override void Awake()
        {
            base.Awake();
            _pieceInfos = new List<PieceInfo>(Resources.LoadAll<PieceInfo>("PieceInfos"));
        }

        protected override void Init(Piece obj)
        {
            obj.transform.localScale = Vector3.one;
        }

        protected override void GetSetup(Piece obj)
        {
        }

        public Piece GetPiece(PieceType type)
        {
            var piece = Get();
            var pieceInfo = _pieceInfos.Find(x => x.type == type);

            piece.Init(pieceInfo);
            return piece;
        }

        public Piece GetRandomPiece()
        {
            var randomIndex = UnityEngine.Random.Range(0, _pieceInfos.Count);
            var pieceInfo = _pieceInfos[randomIndex];

            var piece = Get();
            piece.Init(pieceInfo);
            return piece;
        }

        public void ReturnPiece(Piece piece)
        {
            Return(piece);
        }
    }
}