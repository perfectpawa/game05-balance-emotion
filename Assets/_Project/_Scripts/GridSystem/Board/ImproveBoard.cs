using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DG.Tweening;
using KBCore.Refs;
using MatchThreeSystem;
using UnityEngine;

public class ImproveBoard: ValidatedMonoBehaviour
{
    #region Fields and References

    [Header("References")] 
    [SerializeField, Anywhere] private InputReader _inputReader;

    [SerializeField, Anywhere] private PiecePool _piecePool;
    [SerializeField, Anywhere] private SpriteMask _spriteMask;

    [Header("Grid Settings")]
    [SerializeField] private BoardInfo _boardInfo;
    [SerializeField] private bool _boardDebug = false;

    private Camera _mainCamera;
    
    private Vector3 _rootPosition;

    private GridSystem2D<Cell<Piece>> _grid;
    [SerializeField] private Vector2Int _firstSelectedPos;
    [SerializeField] private Vector2Int _secondSelectedPos;
    
    private List<Vector2Int> _matchPositions;

    #endregion
    
    #region Unity Lifecycle

    private void Start()
    {
        InitializeVariables();
        SetupInput();
        InitializeGrid();
    }
    private void OnDestroy() => _inputReader.OnSelect -= OnSelectPiece;

    #endregion
    
    #region Initialization
    private void InitializeVariables()
    {
        _rootPosition = new Vector2(_boardInfo.Width, _boardInfo.Height) / 2 * -1 * _boardInfo.CellSize;
        _matchPositions = new List<Vector2Int>();
        
        _mainCamera = Camera.main;

        _spriteMask.transform.localScale = new Vector3(
            _boardInfo.Width * _boardInfo.CellSize, 
            (_boardInfo.Height + _boardInfo.MaskOffset) * _boardInfo.CellSize, 
            1
        );
            
        _spriteMask.transform.position += new Vector3(0, _boardInfo.MaskOffset / 2, 0);
        
        _firstSelectedPos = Vector2Int.one * -1;
        _secondSelectedPos = Vector2Int.one * -1;
    }

    private void SetupInput()
    {
        _inputReader.EnablePlayerAction();
        _inputReader.OnSelect += OnSelectPiece;
    }

    private async void InitializeGrid()
    {
        _grid = GridSystem2D<Cell<Piece>>.VerticalGrid(_boardInfo.Width, _boardInfo.Height, _boardInfo.Extra,
            _boardInfo.CellSize, _rootPosition, _boardDebug);
            
        await GenerateAndDropPieces();
    }

    private async Task GenerateAndDropPieces()
    {
        GeneratePiecesInVisibleCell();
        // GeneratePiecesInExtraCellWithoutMatch();
        // await MakePiecesFall();
        // GeneratePiecesInExtraCell();
    }
    #endregion
    
    #region Generate Action
    private Piece CreateRandomPiece(int x, int y)
    {
        var type = _piecePool.GetRandomPieceType();
        return CreatePiece(x,y,type);
    }

    private Piece CreatePiece(int x, int y, PieceType type)
    {
        var cell = new Cell<Piece>(_grid, x, y);
        _grid.SetValue(x, y, cell);
            
        var piece = _piecePool.GetPiece(type);
        cell.SetItem(piece);

        return piece;
    }
    private void GeneratePiecesInExtraCell()
    {
        for (var x = 0; x < _boardInfo.Width; x++)
        {
            for (var y = _boardInfo.Extra; y < _boardInfo.Height + _boardInfo.Extra; y++)
            {
                if (_grid.GetValue(x, y) == null)
                {
                    CreateRandomPiece(x, y);
                }
            }
        }
    }
    
    private void GeneratePiecesInExtraCellWithoutMatch()
    {
        for (var x = 0; x < _boardInfo.Width; x++)
        {
            for (var y = _boardInfo.Extra; y < _boardInfo.Height + _boardInfo.Extra; y++)
            {
                if (_grid.GetValue(x, y) != null) continue;
                
                var pos = new Vector2Int(x, y);
                var piece = CreateRandomPiece(x, y);

                for (var i = 0; i < _piecePool.PieceTypeCount() - 1; i++)
                {
                    if (IsVerticalMatchValid(pos, true) || IsHorizontalMatchValid(pos, true))
                    {
                        _piecePool.ChangePieceInfoToNextType(piece);
                    }
                    else break;
                }
            }
        }
    }

    private void GeneratePiecesInVisibleCell()
    {
        for (var x = 0; x < _boardInfo.Width; x++)
        {
            for (var y = 0; y < _boardInfo.Height; y++)
            {
                if (_grid.GetValue(x, y) == null)
                {
                    CreateRandomPiece(x, y);
                }
            }
        }
    }
    #endregion

    private async void OnSelectPiece()
    {
        if (_firstSelectedPos != Vector2Int.one * -1 && _secondSelectedPos != Vector2Int.one * -1) return;
        
        var selectGridPos = _grid.GetXY(_mainCamera.ScreenToWorldPoint(_inputReader.SelectPosition));

        if (IsEmptyPosition(selectGridPos)) return;
        if (!IsValidPosition(selectGridPos)) return;

        if (_firstSelectedPos == Vector2Int.one * -1)
        {
            _firstSelectedPos = selectGridPos;
            PopOutPiece(_firstSelectedPos);
            return;
        }

        if (IsNeighbor(_firstSelectedPos, selectGridPos))
        {
            _secondSelectedPos = selectGridPos;
            PopOutPiece(_secondSelectedPos);
            await HandleSwapSelectedPiece();
        }
        else
        {
            PopOffPiece(_firstSelectedPos);
            
            _firstSelectedPos = selectGridPos;
            PopOutPiece(_firstSelectedPos);
        }
    }
    
    private async Task HandleSwapSelectedPiece()
    {
        var firstCell = _grid.GetValue(_firstSelectedPos.x, _firstSelectedPos.y);
        var secondCell = _grid.GetValue(_secondSelectedPos.x, _secondSelectedPos.y);
        
        var firstPiece = firstCell.GetItem();
        var secondPiece = secondCell.GetItem();
        
        var sequence = DOTween.Sequence();

        sequence.Append(firstPiece.transform
            .DOLocalMove(secondCell.GetPosition(), _boardInfo.SwapDuration)
            .SetEase(_boardInfo.SwapEase)
        );
        
        sequence.Join(secondPiece.transform
            .DOLocalMove(firstCell.GetPosition(), _boardInfo.SwapDuration)
            .SetEase(_boardInfo.SwapEase)
        );

        SwapCellItem(firstCell, secondCell);
        
        var firstMatch = FindMatchesAtPos(_firstSelectedPos);
        var secondMatch = FindMatchesAtPos(_secondSelectedPos);

        if (firstMatch.Count == 0 && secondMatch.Count == 0)
        {
            sequence.Append(firstPiece.transform
                .DOLocalMove(firstCell.GetPosition(), _boardInfo.SwapDuration)
                .SetEase(_boardInfo.SwapEase)
            );
        
            sequence.Join(secondPiece.transform
                .DOLocalMove(secondCell.GetPosition(), _boardInfo.SwapDuration)
                .SetEase(_boardInfo.SwapEase)
            ); 
            
            SwapCellItem(firstCell, secondCell);
        }

        sequence.Append(PopOffTween(_firstSelectedPos));
        sequence.Join(PopOffTween(_secondSelectedPos));
        
        await sequence.Play().AsyncWaitForCompletion();
        
        _firstSelectedPos = Vector2Int.one * -1;
        _secondSelectedPos = Vector2Int.one * -1;
    }
    
    private void SwapCellItem(Cell<Piece> firstCell, Cell<Piece> secondCell)
    {
        var firstPiece = firstCell.GetItem();
        var secondPiece = secondCell.GetItem();

        firstCell.SetItem(secondPiece, false);
        secondCell.SetItem(firstPiece, false);
    }

    #region Piece Action
    private async Task MakePiecesFall()
    {
        
    }
    #endregion

    #region Match Finding
    private List<Vector2Int> FindMatchesAtPos(Vector2Int pos, bool checkExtra = false)
    {
        var matchPositions = new List<Vector2Int>();
        
        var startY = checkExtra ? _boardInfo.Extra : 0;
        var endY = checkExtra ? _boardInfo.Height + _boardInfo.Extra : _boardInfo.Height;

        var isHorizontalValid = IsVerticalMatchValid(pos, checkExtra);
        var isVerticalValid = IsHorizontalMatchValid(pos, checkExtra);

        if (!isHorizontalValid && !isVerticalValid)
        {
            return matchPositions;
        }
        
        matchPositions.Add(pos);

        if (isHorizontalValid)
        {
            var maxStep = Mathf.Min(pos.y - startY, endY - pos.y);
            for (var step = 0; step <= maxStep; step++)
            {
                var leftPos = new Vector2Int(pos.x, pos.y - step);
                var rightPos = new Vector2Int(pos.x, pos.y + step);
                
                if (AreTwoMatching(pos, rightPos)) matchPositions.Add(rightPos);
                if (AreTwoMatching(pos, leftPos)) matchPositions.Add(leftPos);
            }
        }
        
        if (isVerticalValid)
        {
            var maxStep = Mathf.Min(pos.x, _boardInfo.Width - pos.x);
            for (var step = 0; step <= maxStep; step++)
            {
                var upPos = new Vector2Int(pos.x + step, pos.y);
                var downPos = new Vector2Int(pos.x - step, pos.y);
                
                if (AreTwoMatching(pos, upPos)) matchPositions.Add(upPos);
                if (AreTwoMatching(pos, downPos)) matchPositions.Add(downPos);
            }
        }
        
        return matchPositions;
    }
    private bool AreTwoMatching(Vector2Int posA, Vector2Int posB)
    {
        var a = _grid.GetValue(posA.x, posA.y);
        var b = _grid.GetValue(posB.x, posB.y);

        return a != null && b != null && a.GetItem().Type == b.GetItem().Type;
    }
    private bool IsVerticalMatchValid(Vector2Int pos, bool checkExtra = false)
    {
        var startY = checkExtra ? _boardInfo.Extra : 0;
        var endY = checkExtra ? _boardInfo.Height + _boardInfo.Extra : _boardInfo.Height;

        var currentY = pos.y - 1;
        var currentPos = new Vector2Int(pos.x, currentY);
        var count = 1;
        while (currentY >= startY && IsValidPosition(currentPos, checkExtra) && AreTwoMatching(pos, currentPos))
        {
            currentY--;
            currentPos.y = currentY;
            count++;
            if (count >= 3) return true; 
        }
        
        currentY = pos.y + 1;
        currentPos = new Vector2Int(pos.x, currentY);
        while (currentY < endY && IsValidPosition(currentPos, checkExtra) && AreTwoMatching(pos, currentPos))
        {
            currentY++;
            currentPos.y = currentY;
            count++;
            if (count >= 3) return true; 
        }
        
        return false;
    }
    private bool IsHorizontalMatchValid(Vector2Int pos, bool checkExtra = false)
    {
        var startX = 0;
        var endX = _boardInfo.Width;

        var currentX = pos.x - 1;
        var currentPos = new Vector2Int(currentX, pos.y);
        var count = 1;
        while (currentX >= startX && IsValidPosition(currentPos, checkExtra) && AreTwoMatching(pos, currentPos))
        {
            currentX--;
            currentPos.x = currentX;
            count++;
            if (count >= 3) return true; 
        }
        
        currentX = pos.x + 1;
        currentPos = new Vector2Int(currentX, pos.y);
        while (currentX < endX && IsValidPosition(currentPos, checkExtra) && AreTwoMatching(pos, currentPos))
        {
            currentX++;
            currentPos.x = currentX;
            count++;
            if (count >= 3) return true; 
        }
        
        return false;
    }
    #endregion
    
    private void PopOffPiece(Vector2Int pos)
    {
        var sequence = DOTween.Sequence();
            
        var piece = _grid.GetValue(pos.x, pos.y).GetItem();;
        
        sequence.Append(PopOffTween(pos));
    }
    
    private void PopOutPiece(Vector2Int pos)
    {
        var sequence = DOTween.Sequence();
            
        var piece = _grid.GetValue(pos.x, pos.y).GetItem();;
        
        sequence.Append(PopOutTween(pos));
    }

    private Tween PopOutTween(Vector2Int pos)
    {
        var piece = _grid.GetValue(pos.x, pos.y).GetItem();;
        
        return piece.transform
            .DOScale(Vector3.one * _boardInfo.SelectPopOutScale, _boardInfo.SelectPopDuration)
            .SetEase(_boardInfo.SelectPopSwapEase);
    }

    private Tween PopOffTween(Vector2Int pos)
    {
        var piece = _grid.GetValue(pos.x, pos.y).GetItem();;
        
        return piece.transform
            .DOScale(Vector3.one * _boardInfo.SelectPopOffScale, _boardInfo.SelectPopDuration)
            .SetEase(_boardInfo.SelectPopSwapEase);
    }
    
    #region Utils
    private static bool IsNeighbor(Vector2Int first, Vector2Int second) => Mathf.Abs(first.x - second.x) + Mathf.Abs(first.y - second.y) == 1;
    private bool IsEmptyPosition(Vector2Int pos) => _grid.GetValue(pos.x, pos.y) == null;
    private bool IsValidPosition(Vector2Int pos, bool checkExtra = false) 
        => pos.x >= 0 && pos.x < _boardInfo.Width && pos.y >= 0 && pos.y < _boardInfo.Height + (checkExtra? _boardInfo.Extra : 0);
    #endregion
}
