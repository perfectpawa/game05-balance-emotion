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

        [Header("Grid Settings")] [SerializeField]
        private float _maskOffset = 0.5f;

        [SerializeField] private int width = 5;
        [SerializeField] private int height = 5;
        [SerializeField] private int extra = 5;
        [SerializeField] private int cellSize = 1;
        [SerializeField] private Vector3 originPosition = Vector3.zero;
        [SerializeField] private bool debug = false;

        [Header("Swap Settings")] [FormerlySerializedAs("swapDuration"), SerializeField]
        private float _swapDuration = 0.5f;

        [SerializeField] private Ease _swapEase = Ease.OutBack;

        [Header("Fall Settings")] [SerializeField]
        private float _fallDuration = 1.0f;

        [SerializeField] private Ease _fallEase = Ease.InQuint;

        private Camera _mainCamera;
        private GridSystem2D<Cell<Piece>> _grid;
        [SerializeField] private Vector2Int _firstSelectedPiece;
        [SerializeField] private Vector2Int _secondSelectedPiece;

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
            _mainCamera = Camera.main;
            _firstSelectedPiece = Vector2Int.one * -1;
            _secondSelectedPiece = Vector2Int.one * -1;

            _spriteMask.transform.localScale = new Vector3(width, height + _maskOffset, 1);
            _spriteMask.transform.position += new Vector3(0, _maskOffset / 2, 0);

            _explodedPiece = new Dictionary<PieceType, int>();
        }

        private void SetupInput()
        {
            _inputReader.EnablePlayerAction();
            _inputReader.OnSelect += OnSelectPiece;
        }

        private async void InitializeGrid()
        {
            _grid = GridSystem2D<Cell<Piece>>.VerticalGrid(width, height, extra, cellSize, originPosition, debug);

            await GenerateAndDropPieces();
        }

        private async Task GenerateAndDropPieces()
        {
            GeneratePiecesInExtraCell();
            await MakePiecesFall();
            GeneratePiecesInExtraCell();
        }

        #endregion

        #region Grid Management
        private void CreatePiece(int x, int y)
        {
            var cell = new Cell<Piece>(_grid, x, y);
            _grid.SetValue(x, y, cell);

            cell.SetItem(_piecePool.GetRandomPiece());
        }
        private void GeneratePiecesInExtraCell()
        {
            for (var x = 0; x < width; x++)
            {
                for (var y = extra; y < height + extra; y++)
                {
                    if (_grid.GetValue(x, y) == null)
                    {
                        CreatePiece(x, y);
                    }
                }
            }
        }
        #endregion

        #region Piece Action
        private async Task MakePiecesFall()
        {
            var sequence = DOTween.Sequence();
            for (var x = 0; x < width; x++)
            {
                int emptyRow = -1;

                for (var y = 0; y < height + extra; y++)
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
                            .DOLocalMove(_grid.GetWorldPositionCenter(x, emptyRow), _fallDuration)
                            .SetEase(_fallEase));

                        emptyRow++;
                    }
                }
            }

            await sequence.AsyncWaitForCompletion();
        }
        private async Task ExplodePieces(List<Vector2Int> matches)
        {
            List<Piece> explodedPieces = new List<Piece>();
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
        private List<Vector2Int> FindMatches()
        {
            if (_firstSelectedPiece != Vector2Int.one * -1 && _secondSelectedPiece != Vector2Int.one * -1)
                return FindMatchesRelateToSelect();

            return FindMatchAllGrid();
        }
        private List<Vector2Int> FindMatchesRelateToSelect()
        {
            HashSet<Vector2Int> matches = new();

            //check first selected piece
            //check horizontal matches of first selected piece
            for (var x = 0; x < width - 2; x++)
            {
                if (AreThreeMatching(new Vector2Int(x, _firstSelectedPiece.y),
                        new Vector2Int(x + 1, _firstSelectedPiece.y), new Vector2Int(x + 2, _firstSelectedPiece.y)))
                {
                    matches.UnionWith(new[]
                    {
                        new Vector2Int(x, _firstSelectedPiece.y), new Vector2Int(x + 1, _firstSelectedPiece.y),
                        new Vector2Int(x + 2, _firstSelectedPiece.y)
                    });
                }
            }

            //check vertical matches of first selected piece
            for (var y = 0; y < height - 2; y++)
            {
                if (AreThreeMatching(new Vector2Int(_firstSelectedPiece.x, y),
                        new Vector2Int(_firstSelectedPiece.x, y + 1), new Vector2Int(_firstSelectedPiece.x, y + 2)))
                {
                    matches.UnionWith(new[]
                    {
                        new Vector2Int(_firstSelectedPiece.x, y), new Vector2Int(_firstSelectedPiece.x, y + 1),
                        new Vector2Int(_firstSelectedPiece.x, y + 2)
                    });
                }
            }

            //check second selected piece
            //check horizontal matches of second selected piece
            for (var x = 0; x < width - 2; x++)
            {
                if (AreThreeMatching(new Vector2Int(x, _secondSelectedPiece.y),
                        new Vector2Int(x + 1, _secondSelectedPiece.y), new Vector2Int(x + 2, _secondSelectedPiece.y)))
                {
                    matches.UnionWith(new[]
                    {
                        new Vector2Int(x, _secondSelectedPiece.y), new Vector2Int(x + 1, _secondSelectedPiece.y),
                        new Vector2Int(x + 2, _secondSelectedPiece.y)
                    });
                }
            }

            //check vertical matches of second selected piece
            for (var y = 0; y < height - 2; y++)
            {
                if (AreThreeMatching(new Vector2Int(_secondSelectedPiece.x, y),
                        new Vector2Int(_secondSelectedPiece.x, y + 1), new Vector2Int(_secondSelectedPiece.x, y + 2)))
                {
                    matches.UnionWith(new[]
                    {
                        new Vector2Int(_secondSelectedPiece.x, y), new Vector2Int(_secondSelectedPiece.x, y + 1),
                        new Vector2Int(_secondSelectedPiece.x, y + 2)
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
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width - 2; x++)
                {
                    if (AreThreeMatching(new Vector2Int(x, y), new Vector2Int(x + 1, y), new Vector2Int(x + 2, y)))
                    {
                        matches.UnionWith(new[]
                            { new Vector2Int(x, y), new Vector2Int(x + 1, y), new Vector2Int(x + 2, y) });
                    }
                }
            }

            // Vertical Matches
            for (var x = 0; x < width; x++)
            {
                for (var y = 0; y < height - 2; y++)
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

        private async Task SwapSelectedPieces()
        {
            var firstCell = _grid.GetValue(_firstSelectedPiece.x, _firstSelectedPiece.y);
            var secondCell = _grid.GetValue(_secondSelectedPiece.x, _secondSelectedPiece.y);

            var sequence = DOTween.Sequence();

            sequence.Join(firstCell.GetItem().transform
                .DOLocalMove(_grid.GetWorldPositionCenter(_secondSelectedPiece.x, _secondSelectedPiece.y),
                    _swapDuration)
                .SetEase(_swapEase));

            sequence.Join(secondCell.GetItem().transform
                .DOLocalMove(_grid.GetWorldPositionCenter(_firstSelectedPiece.x, _firstSelectedPiece.y), _swapDuration)
                .SetEase(_swapEase));

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

        #region Utils
        private static bool IsNeighbor(Vector2Int first, Vector2Int second) => Mathf.Abs(first.x - second.x) + Mathf.Abs(first.y - second.y) == 1;
        private bool IsEmptyPosition(Vector2Int pos) => _grid.GetValue(pos.x, pos.y) == null;
        private bool IsValidPosition(Vector2Int pos) => pos.x >= 0 && pos.x < width && pos.y >= 0 && pos.y < height;
        #endregion
    }
}