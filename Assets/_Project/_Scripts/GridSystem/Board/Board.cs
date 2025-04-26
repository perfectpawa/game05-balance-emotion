using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DG.Tweening;
using KBCore.Refs;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace MatchThreeSystem
{
    public class Board : MonoBehaviour
    {
        #region Fields and References

        [Header("References")] [SerializeField, Anywhere]
        private InputReader _inputReader;

        [SerializeField, Anywhere] private PiecePool _piecePool;
        [SerializeField, Anywhere] private SpriteMask _spriteMask;

        [Header("Grid Settings")]
        [SerializeField] private BoardInfo _boardInfo;
        [SerializeField] private bool _debug = false;

        private Vector3 _rootPosition;

        private Camera _mainCamera;
        private GridSystem2D<Cell<Piece>> _grid;
        
        private Vector2Int _firstSelectedPiece;
        private Vector2Int _secondSelectedPiece;

        private Dictionary<PieceType, int> _explodedPiece;


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
            _rootPosition = new Vector2(_boardInfo.Width, _boardInfo.Height) / 2 * -1;
            
            _mainCamera = Camera.main;
            _firstSelectedPiece = Vector2Int.one * -1;
            _secondSelectedPiece = Vector2Int.one * -1;

            _spriteMask.transform.localScale = new Vector3(_boardInfo.Width, _boardInfo.Height + _boardInfo.MaskOffset, 1);
            _spriteMask.transform.position += new Vector3(0, _boardInfo.MaskOffset / 2, 0);

            _explodedPiece = new Dictionary<PieceType, int>();
        }

        private void SetupInput()
        {
            _inputReader.EnablePlayerAction();
            _inputReader.OnSelect += OnSelectPiece;
        }

        private async void InitializeGrid()
        {
            _grid = GridSystem2D<Cell<Piece>>.VerticalGrid(_boardInfo.Width, _boardInfo.Height, _boardInfo.Extra,
                _boardInfo.CellSize, _rootPosition, _debug);
            
            await GenerateAndDropPieces();
        }

        private async Task GenerateAndDropPieces()
        {
            GeneratePiecesInExtraCellWithoutMatch();
            await MakePiecesFall();
            GeneratePiecesInExtraCell();
        }

        #endregion

        #region Grid Management
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
                    if (_grid.GetValue(x, y) == null)
                    {
                        var piece = CreateRandomPiece(x, y);
                        for (var i = 0; i < _piecePool.PieceTypeCount(); i++)
                        {
                            var matches = FindMatchesRelateToPiece((new Vector2Int(x, y)), true);
                            if (matches.Count == 0) break;
                            _piecePool.ChangePieceInfoToNextType(piece);
                        }                
                    }
                }
            }
        }
        #endregion

        #region Piece Action
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
                var piece = _grid.GetValue(match.x, match.y)?.GetItem();
                if (piece == null) continue;

                explodedPieces.Add(piece);

                _grid.SetValue(match.x, match.y, null);
                sequence.Join(piece.transform.DOScale(Vector3.zero, 0.25f).SetEase(Ease.OutBack));
            }

            //save count of pieces type to be exploded
            foreach (var piece in explodedPieces.Where(piece => !_explodedPiece.TryAdd(piece.Type, 1)))
            {
                _explodedPiece[piece.Type]++;
            }


            sequence.OnComplete(() =>
            {
                foreach (var piece in explodedPieces)
                {
                    _piecePool.ReturnPiece(piece);
                }
            });

            await sequence.AsyncWaitForCompletion();
        }
        #endregion

        #region Match Finding
        private List<Vector2Int> FindMatches(bool checkExtra = false)
        {
            if (_firstSelectedPiece != Vector2Int.one * -1 && _secondSelectedPiece != Vector2Int.one * -1)
                return FindMatchesRelateToSelect(checkExtra);

            return FindMatchAllGrid();
        }
        private List<Vector2Int> FindMatchesRelateToSelect(bool checkExtra = false)
        {
            HashSet<Vector2Int> matches = new();

            var firstMatched = FindMatchesRelateToPiece(_firstSelectedPiece, checkExtra);
            var secondMatched = FindMatchesRelateToPiece(_secondSelectedPiece, checkExtra);
            
            matches.UnionWith(firstMatched);
            matches.UnionWith(secondMatched);

            List<Vector2Int> sortedMatches = new(matches);
            sortedMatches.Sort((a, b) => a.x == b.x ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));
            return sortedMatches;
        }

        private List<Vector2Int> FindMatchesRelateToPiece(Vector2Int pos, bool checkExtra = false)
        {
            HashSet<Vector2Int> matches = new();
            
            var startY = checkExtra ? _boardInfo.Extra : 0;
            var endY = checkExtra ? _boardInfo.Height + _boardInfo.Extra : _boardInfo.Height;

            //check horizontal matches
            for (var x = 0; x < _boardInfo.Width - 2; x++)
            {
                if (AreThreeMatching(new Vector2Int(x, pos.y),
                        new Vector2Int(x + 1, pos.y), new Vector2Int(x + 2, pos.y)))
                {
                    matches.UnionWith(new[]
                    {
                        new Vector2Int(x, pos.y), new Vector2Int(x + 1, pos.y),
                        new Vector2Int(x + 2, pos.y)
                    });
                }
            }

            //check vertical matches
            for (var y = startY; y < endY - 2; y++)
            {
                if (AreThreeMatching(new Vector2Int(pos.x, y),
                        new Vector2Int(pos.x, y + 1), new Vector2Int(pos.x, y + 2)))
                {
                    matches.UnionWith(new[]
                    {
                        new Vector2Int(pos.x, y), new Vector2Int(pos.x, y + 1),
                        new Vector2Int(pos.x, y + 2)
                    });
                }
            }
            
            List<Vector2Int> sortedMatches = new(matches);
            sortedMatches.Sort((a, b) => a.x == b.x ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));
            return sortedMatches;
        }
        private List<Vector2Int> FindMatchAllGrid()
        {
            HashSet<Vector2Int> matches = new();

            // Horizontal Matches
            for (var y = 0; y < _boardInfo.Height; y++)
            {
                for (var x = 0; x < _boardInfo.Width - 2; x++)
                {
                    if (AreThreeMatching(new Vector2Int(x, y), new Vector2Int(x + 1, y), new Vector2Int(x + 2, y)))
                    {
                        matches.UnionWith(new[]
                            { new Vector2Int(x, y), new Vector2Int(x + 1, y), new Vector2Int(x + 2, y) });
                    }
                }
            }

            // Vertical Matches
            for (var x = 0; x < _boardInfo.Width; x++)
            {
                for (var y = 0; y < _boardInfo.Height - 2; y++)
                {
                    if (AreThreeMatching(new Vector2Int(x, y), new Vector2Int(x, y + 1), new Vector2Int(x, y + 2)))
                    {
                        matches.UnionWith(new[]
                            { new Vector2Int(x, y), new Vector2Int(x, y + 1), new Vector2Int(x, y + 2) });
                    }
                }
            }

            List<Vector2Int> sortedMatches = new(matches);
            sortedMatches.Sort((a, b) => a.x == b.x ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));
            return sortedMatches;
        }
        private bool AreThreeMatching(Vector2Int posA, Vector2Int posB, Vector2Int posC)
        {

            var a = _grid.GetValue(posA.x, posA.y);
            var b = _grid.GetValue(posB.x, posB.y);
            var c = _grid.GetValue(posC.x, posC.y);

            return a != null && b != null && c != null &&
                   a.GetItem().Type == b.GetItem().Type &&
                   b.GetItem().Type == c.GetItem().Type;
        }
        #endregion

        #region Selection and Swapping
        private async void OnSelectPiece()
        {
            if (_firstSelectedPiece != Vector2Int.one * -1 && _secondSelectedPiece != Vector2Int.one * -1) return;

            var gridPos = _grid.GetXY(_mainCamera.ScreenToWorldPoint(_inputReader.SelectPosition));
            if (!IsValidPosition(gridPos) || IsEmptyPosition(gridPos)) return;

            if (_firstSelectedPiece == Vector2Int.one * -1)
            {
                SelectPiece(gridPos);
            }
            else if (IsNeighbor(_firstSelectedPiece, gridPos))
            {
                SelectPiece(gridPos);
                try
                {
                    await HandleBoardAction();
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                }
                finally
                {
                    DeselectPieces();

                    //print exploded pieces
                    foreach (var piece in _explodedPiece)
                    {
                        Debug.Log($"Piece: {piece.Key}, Count: {piece.Value}");
                    }
                }
            }
            else
            {
                DeselectPieces();
                SelectPiece(gridPos);
            }
        }
        private async Task SwapSelectedPieces()
        {
            var firstCell = _grid.GetValue(_firstSelectedPiece.x, _firstSelectedPiece.y);
            var secondCell = _grid.GetValue(_secondSelectedPiece.x, _secondSelectedPiece.y);

            var sequence = DOTween.Sequence();

            sequence.Join(firstCell.GetItem().transform
                .DOLocalMove(_grid.GetWorldPositionCenter(_secondSelectedPiece.x, _secondSelectedPiece.y),
                    _boardInfo.SwapDuration)
                .SetEase(_boardInfo.SwapEase));

            sequence.Join(secondCell.GetItem().transform
                .DOLocalMove(_grid.GetWorldPositionCenter(_firstSelectedPiece.x, _firstSelectedPiece.y), _boardInfo.SwapDuration)
                .SetEase(_boardInfo.SwapEase));

            _grid.SetValue(_firstSelectedPiece.x, _firstSelectedPiece.y, secondCell);
            _grid.SetValue(_secondSelectedPiece.x, _secondSelectedPiece.y, firstCell);

            await sequence.AsyncWaitForCompletion();
        }
        private void DeselectPieces() =>
            (_firstSelectedPiece, _secondSelectedPiece) = (Vector2Int.one * -1, Vector2Int.one * -1);
        private void SelectPiece(Vector2Int pos) =>
            (_firstSelectedPiece, _secondSelectedPiece) = _firstSelectedPiece == Vector2Int.one * -1
                ? (pos, _secondSelectedPiece)
                : (_firstSelectedPiece, pos);
        #endregion

        #region Board Action
        private async Task HandleBoardAction()
        {
            await SwapSelectedPieces();

            var matches = FindMatchesRelateToSelect();

            if (matches.Count == 0)
            {
                await SwapSelectedPieces();
                return;
            }

            _explodedPiece.Clear();

            await ExplodePieces(matches);
            await MakePiecesFall();
            GeneratePiecesInExtraCell();

            matches.Clear();

            matches = FindMatchAllGrid();

            while (matches.Count > 0)
            {
                await ExplodePieces(matches);
                await MakePiecesFall();
                GeneratePiecesInExtraCell();

                matches.Clear();
                matches = FindMatchAllGrid();
            }
        }
        #endregion

        #region Utils
        private static bool IsNeighbor(Vector2Int first, Vector2Int second) => Mathf.Abs(first.x - second.x) + Mathf.Abs(first.y - second.y) == 1;
        private bool IsEmptyPosition(Vector2Int pos) => _grid.GetValue(pos.x, pos.y) == null;
        private bool IsValidPosition(Vector2Int pos) => pos.x >= 0 && pos.x < _boardInfo.Width && pos.y >= 0 && pos.y < _boardInfo.Height;
        #endregion
    }
}