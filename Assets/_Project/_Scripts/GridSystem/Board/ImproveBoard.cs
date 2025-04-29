using System;
using System.Collections.Generic;
using System.Linq;
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
    
    private Vector2Int _firstSelectedPos;
    private Vector2Int _secondSelectedPos;
    private bool _enableSelection;

    [SerializeField] private List<Vector2Int> _matchPositions;

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
        _enableSelection = true;
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
        GeneratePiecesInExtraCellWithoutMatch();
        await MakePiecesFall();
        GeneratePiecesInExtraCell();
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
    #endregion
    private void OnSelectPiece()
    {
        if (!_enableSelection) return;
        
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
            
            HandleSwapSelectedPiece();
        }
        else
        {
            PopOffPiece(_firstSelectedPos);
            
            _firstSelectedPos = selectGridPos;
            PopOutPiece(_firstSelectedPos);
        }
    }
    private async void HandleSwapSelectedPiece()
    {
        _enableSelection = false;
        
        await SwapPieces(_firstSelectedPos, _secondSelectedPos);
        
        var firstMatches = FindMatchesAtPos(_firstSelectedPos);
        var secondMatches = FindMatchesAtPos(_secondSelectedPos);
        
        var popOffTask = PopOffPieces(new List<Vector2Int>{_firstSelectedPos, _secondSelectedPos});
        
        if (firstMatches.Count == 0 && secondMatches.Count == 0)
        {
            await SwapPieces(_firstSelectedPos, _secondSelectedPos);
            await popOffTask;
            
            _enableSelection = true;
        }
        else
        {
            await popOffTask;
            var matches = firstMatches.Union(secondMatches).ToList();
            
            HandleResolveBoard(matches);
        }
        
        _firstSelectedPos = Vector2Int.one * -1;
        _secondSelectedPos = Vector2Int.one * -1;
    }
    private async void HandleResolveBoard(List<Vector2Int> matches)
    {
        await ExplodePieces(matches);
        await MakePiecesFall();
        GeneratePiecesInExtraCell();

        var fallCol = new Dictionary<int, bool>();
        for (var x = 0; x <= _boardInfo.Width; x++) fallCol[x] = false;

        foreach (var match in matches) fallCol[match.x] = true;
        
        matches = FindMatchesAtColumns(fallCol);

        while (matches.Count() >= 3)
        {
            await ExplodePieces(matches);
            await MakePiecesFall();
            GeneratePiecesInExtraCell();

            for (var x = 0; x <= _boardInfo.Width; x++) fallCol[x] = false;
            foreach (var match in matches) fallCol[match.x] = true;
            
            matches = FindMatchesAtColumns(fallCol);
        }
        
        _enableSelection = true;
    }
    #region Piece Action
    private async Task SwapPieces(Vector2Int firstPos, Vector2Int secondPos)
    {
        var firstCell = _grid.GetValue(firstPos.x, firstPos.y);
        var secondCell = _grid.GetValue(secondPos.x, secondPos.y);

        var sequence = DOTween.Sequence();

        sequence.Join(firstCell.GetItem().transform
            .DOLocalMove(_grid.GetWorldPositionCenter(secondPos.x, secondPos.y),
                _boardInfo.SwapDuration)
            .SetEase(_boardInfo.SwapEase));

        sequence.Join(secondCell.GetItem().transform
            .DOLocalMove(_grid.GetWorldPositionCenter(firstPos.x, firstPos.y), _boardInfo.SwapDuration)
            .SetEase(_boardInfo.SwapEase));

        _grid.SetValue(firstPos.x, firstPos.y, secondCell);
        _grid.SetValue(secondPos.x, secondPos.y, firstCell);

        await sequence.AsyncWaitForCompletion();
    }
    private async Task MakePiecesFall()
    {
        var sequence = DOTween.Sequence();
        for (var x = 0; x < _boardInfo.Width; x++)
        {
            var emptyRow = -1;

            for (var y = 0; y < _boardInfo.Height + _boardInfo.Extra; y++)
            {
                var cell = _grid.GetValue(x, y);
                if (cell == null)
                {
                    if (emptyRow == -1) emptyRow = y;
                    continue;
                }

                if (emptyRow != -1)
                {
                    var piece = cell.GetItem();
                    _grid.SetValue(x, emptyRow, cell);
                    _grid.SetValue(x, y, null);

                    sequence.Join(piece.transform
                        .DOLocalMove(_grid.GetWorldPositionCenter(x, emptyRow), _boardInfo.FallDuration)
                        .SetEase(_boardInfo.FallEase));
                    
                    emptyRow++;
                }
            }
        }
            
        await sequence.AsyncWaitForCompletion();
    }
    private async Task ExplodePieces(List<Vector2Int> matches)
    {
        var explodedPieces = new List<Piece>();
        
        var sequence = DOTween.Sequence();

        foreach (var match in matches)
        {
            var piece = _grid.GetValue(match.x, match.y).GetItem();

            if (piece == null)
            {
                Debug.LogError($"Piece is null at {matches}");
            }
            
            explodedPieces.Add(piece);
            _grid.SetValue(match.x, match.y, null);
            
            sequence.Append(ExplodeTween(piece));
        }
        
        sequence.Play();

        sequence.OnComplete(() =>
        {
            foreach (var piece in explodedPieces)
            {
                _piecePool.ReturnPiece(piece);
            }
        });
        
        //TODO: run explode event here

        await sequence.AsyncWaitForCompletion();
    }
    private Tween ExplodeTween(Piece piece)
    {
        return piece.transform
            .DOScale(Vector3.zero, _boardInfo.ExplodeDuration)
            .SetEase(_boardInfo.ExplodeEase);
    }
    #endregion

    #region Match Finding

    private List<Vector2Int> FindMatchesAtColumns(Dictionary<int, bool> colDict, bool checkExtra = false)
    {
        var totalMatches = new HashSet<Vector2Int>();

        for (var x = 0; x < _boardInfo.Width; x++)
        {
            if (!colDict[x]) continue;
            
            var matches = FindMatchesAtColumn(x, checkExtra);
            
            foreach (var match in matches) totalMatches.Add(match);
        }
        
        return totalMatches.ToList();
    }
    private List<Vector2Int> FindMatchesAtColumn(int col, bool checkExtra = false)
    {
        var totalMatches = new HashSet<Vector2Int>();

        for (var y = 0; y < _boardInfo.Height; y++)
        {
            var matches = FindMatchesAtPos(new Vector2Int(col, y), checkExtra);
            
            foreach (var match in matches) totalMatches.Add(match);
        }

        return totalMatches.ToList();
    }
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
            var lockLeft = false;
            var lockRight = false;
            var maxStep = Mathf.Max(pos.y - startY, endY - pos.y);
            for (var step = 0; step <= maxStep; step++)
            {
                if (lockLeft && lockRight) break;
                
                var leftPos = new Vector2Int(pos.x, pos.y - step);
                var rightPos = new Vector2Int(pos.x, pos.y + step);

                if (!lockRight)
                {
                    if (IsValidPosition(rightPos, false) && AreTwoMatching(pos, rightPos)) matchPositions.Add(rightPos);
                    else lockRight = true;
                }

                if (!lockLeft)
                {
                    if (IsValidPosition(leftPos, false) && AreTwoMatching(pos, leftPos)) matchPositions.Add(leftPos);
                    else lockLeft = true;
                }
            }
        }
        
        if (isVerticalValid)
        {
            var lockUp = false;
            var lockDown = false;
            var maxStep = Mathf.Max(pos.x, _boardInfo.Width - pos.x);
            for (var step = 0; step <= maxStep; step++)
            {
                if (lockUp && lockDown) break;
                
                var upPos = new Vector2Int(pos.x + step, pos.y);
                var downPos = new Vector2Int(pos.x - step, pos.y);

                if (!lockUp)
                {
                    if (IsValidPosition(upPos, false) && AreTwoMatching(pos, upPos)) matchPositions.Add(upPos);
                    else lockUp = true;
                }

                if (!lockDown)
                {
                    if (IsValidPosition(downPos, false) && AreTwoMatching(pos, downPos)) matchPositions.Add(downPos);
                    else lockDown = true;
                }
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

    #region Pop Piece
    private void PopOffPiece(Vector2Int pos)
    {
        var sequence = DOTween.Sequence();
            
        var piece = _grid.GetValue(pos.x, pos.y).GetItem();;
        
        sequence.Append(PopOffTween(pos));
    }
    private async Task PopOffPieces(List<Vector2Int> positions)
    {
        var sequence = DOTween.Sequence();

        foreach (var pos in from pos in positions let piece = _grid.GetValue(pos.x, pos.y).GetItem() select pos)
        {
            sequence.Join(PopOffTween(pos));
        }
        
        await sequence.AsyncWaitForCompletion();
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
    #endregion
    
    #region Utils
    private static bool IsNeighbor(Vector2Int first, Vector2Int second) => Mathf.Abs(first.x - second.x) + Mathf.Abs(first.y - second.y) == 1;
    private bool IsEmptyPosition(Vector2Int pos) => _grid.GetValue(pos.x, pos.y) == null;
    private bool IsValidPosition(Vector2Int pos, bool checkExtra = false) 
        => pos.x >= 0 && pos.x < _boardInfo.Width && pos.y >= 0 && pos.y < _boardInfo.Height + (checkExtra? _boardInfo.Extra : 0);
    #endregion
}
