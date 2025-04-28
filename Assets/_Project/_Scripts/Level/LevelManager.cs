using System;
using System.Collections.Generic;
using System.Linq;
using EventManager;
using MatchThreeSystem;
using UnityEngine;

public class LevelManager : MonoBehaviour
{
    [SerializeField] private List<ExplodedPieceData> _explodedPieces;
    [SerializeField] private List<PieceRatio> _pieceRatioCondition;

    private Dictionary<PieceType, int> _pieceCount;
    
    [SerializeField] private bool _ratioWinCondition;
    [SerializeField] private long _totalScore;
    
    private void Awake()
    {
        InitValue();
    }

    private void OnEnable()
    {
        GenericEventManager.StartListening<Dictionary<PieceType, int>>(EventName.BoardEvent.BoardResolved, HandleBoardResolved);
    }

    private void OnDisable()
    {
        GenericEventManager.StopListening<Dictionary<PieceType, int>>(EventName.BoardEvent.BoardResolved, HandleBoardResolved);
    }

    private void InitValue()
    {
        _ratioWinCondition = false;
        _totalScore = 0;
        
        _explodedPieces = new List<ExplodedPieceData>();
        _pieceCount = new Dictionary<PieceType, int>();
        
        foreach (var pieceType in Enum.GetValues(typeof(PieceType)).Cast<PieceType>())
        {
            if (pieceType == PieceType.None) continue;
            
            _explodedPieces.Add(new ExplodedPieceData
            {
                Type = pieceType,
                Count = 0
            });
            
            _pieceCount.Add(pieceType, 0);
        }
    }

    private void HandleBoardResolved(Dictionary<PieceType, int> explodedPieceData)
    {
        foreach (var piece in explodedPieceData)
        {
            var explodedPiece = _explodedPieces.FirstOrDefault(x => x.Type == piece.Key);

            if (explodedPiece != null && explodedPiece.Type != PieceType.None)
            {
                explodedPiece.Count += piece.Value;
                _pieceCount[piece.Key] += piece.Value;
            }
        }
        
        CheckPieceRatioWinCondition();
    }

    private void CheckPieceRatioWinCondition()
    {
        _ratioWinCondition = true;
        
        if (_pieceRatioCondition.Count < 2)
        {
            Debug.LogWarning("Please add at least 2 piece ratio condition");
            _ratioWinCondition = false;
            return;
        }
        
        var pieceRatioInfo = new Dictionary<PieceType, float>();

        var baseRatio = _pieceCount[_pieceRatioCondition[0].Type] / _pieceRatioCondition[0].Ratio;

        pieceRatioInfo.Add(_pieceRatioCondition[0].Type, baseRatio);
        
        for (var i = 1; i < _pieceRatioCondition.Count; i++)
        {
            var count = _pieceCount[_pieceRatioCondition[i].Type];

            if (count == 0)
            {
                _ratioWinCondition = false;
            }
            
            var currentRatio = count / _pieceRatioCondition[i].Ratio;
            pieceRatioInfo.Add(_pieceRatioCondition[i].Type, currentRatio);

            if (!Mathf.Approximately(baseRatio, currentRatio))
            {
                _ratioWinCondition = false;
            }
        }
    }

    private void HandleCalculateScoreWhenPieceMatch()
    {
        
    }
}

[Serializable]
public class ExplodedPieceData
{
    public PieceType Type;
    public int Count;
}

[Serializable]
public class PieceRatio
{
    public PieceType Type;
    public float Ratio;
}
