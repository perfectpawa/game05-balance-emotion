using System;
using System.Collections.Generic;
using System.Linq;
using KBCore.Refs;
using MatchThreeSystem;
using UnityEngine;

public class LevelManager : ValidatedMonoBehaviour
{
    [SerializeField, Anywhere] private Board _board;

    [SerializeField] private List<ExplodedPiece> _explodedPieces;
    
    private void OnEnable()
    {
        _board.CompleteExplode += HandleCompleteExplodePiece;
    }
    private void OnDisable()
    {
        _board.CompleteExplode -= HandleCompleteExplodePiece;
    }

    private void Start()
    {
        _explodedPieces = new List<ExplodedPiece>();
        
        foreach (var pieceType in Enum.GetValues(typeof(PieceType)).Cast<PieceType>())
        {
            if (pieceType == PieceType.None) continue;
            
            _explodedPieces.Add(new ExplodedPiece
            {
                Type = pieceType,
                Count = 0
            });
        }
    }

    private void HandleCompleteExplodePiece(Dictionary<PieceType, int> explodedPieceData)
    {
        foreach (var piece in explodedPieceData)
        {
            var explodedPiece = _explodedPieces.FirstOrDefault(x => x.Type == piece.Key);

            if (explodedPiece != null && explodedPiece.Type != PieceType.None)
            {
                explodedPiece.Count += piece.Value;
            }
        }
    }
}

[Serializable]
public class ExplodedPiece
{
    public PieceType Type;
    public int Count;
}