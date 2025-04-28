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
    private Vector2Int _firstSelectedPos;
    private Vector2Int _secondSelectedPos;
    
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
        // GeneratePiecesInExtraCellWithoutMatch();
        // await MakePiecesFall();
        // GeneratePiecesInExtraCell();
    }
    #endregion

    private async void OnSelectPiece()
    {
        if (_firstSelectedPos == Vector2Int.one * -1 && _secondSelectedPos == Vector2Int.one * -1) return;
        
        var selectGridPos = _grid.GetXY(_mainCamera.ScreenToWorldPoint(_inputReader.SelectPosition));

        if (IsEmptyPosition(selectGridPos)) return;
        if (!IsValidPosition(selectGridPos)) return;

        if (_firstSelectedPos == Vector2Int.one * -1)
        {
            _firstSelectedPos = selectGridPos;
            return;
        }

        if (IsNeighbor(_firstSelectedPos, selectGridPos))
        {
            _secondSelectedPos = selectGridPos;
            PopOutPieces(_secondSelectedPos);
            
            await HandleSwapSelectedPiece();
        }
        else
        {
            PopOffPieces(_firstSelectedPos);
            
            _firstSelectedPos = selectGridPos;
            PopOutPieces(_firstSelectedPos);
        }
    }
    
    private async Task HandleSwapSelectedPiece()
    {
        var firstCell = _grid.GetValue(_firstSelectedPos.x, _firstSelectedPos.y);
        var secondCell = _grid.GetValue(_secondSelectedPos.x, _secondSelectedPos.y);
        
        var firstPiece = firstCell.GetItem();
        var secondPiece = secondCell.GetItem();
        
        var sequence = DOTween.Sequence();
        
        sequence.Append(
            firstPiece.transform
                .DOLocalMove(secondCell.GetPosition(), _boardInfo.SwapDuration)
                .SetEase(_boardInfo.SwapEase)
        );
        
        sequence.Join(
            secondPiece.transform
                .DOLocalMove(firstCell.GetPosition(), _boardInfo.SwapDuration)
                .SetEase(_boardInfo.SwapEase)
        );
    }
    
    #region Match Finding
    private List<Vector2Int> FindMatchesAtPos(Vector2Int pos, bool checkExtra = false)
    {
        return null;
    }


    #endregion
    
    private void PopOffPieces(Vector2Int pos)
    {
        var sequence = DOTween.Sequence();
            
        var piece = _grid.GetValue(pos.x, pos.y).GetItem();;
        
        sequence.Append(
            piece.transform
                .DOScale(Vector3.one * _boardInfo.SelectPopOffScale, _boardInfo.SelectPopDuration)
                .SetEase(_boardInfo.SelectPopSwapEase)
        );
    }
    
    private void PopOutPieces(Vector2Int pos)
    {
        var sequence = DOTween.Sequence();
            
        var piece = _grid.GetValue(pos.x, pos.y).GetItem();;
        
        sequence.Append(
            piece.transform
                .DOScale(Vector3.one * _boardInfo.SelectPopOutScale, _boardInfo.SelectPopDuration)
                .SetEase(_boardInfo.SelectPopSwapEase)
        );
    }
    
    #region Utils
    private static bool IsNeighbor(Vector2Int first, Vector2Int second) => Mathf.Abs(first.x - second.x) + Mathf.Abs(first.y - second.y) == 1;
    private bool IsEmptyPosition(Vector2Int pos) => _grid.GetValue(pos.x, pos.y) == null;
    private bool IsValidPosition(Vector2Int pos) => pos.x >= 0 && pos.x < _boardInfo.Width && pos.y >= 0 && pos.y < _boardInfo.Height;
    #endregion
}
