using UnityEngine;

namespace MatchThreeSystem
{
    public class Board : MonoBehaviour
    {
        [Header("Grid Settings")]
        [SerializeField] private float _maskOffset = 0.5f;
        [SerializeField] private int width = 5;
        [SerializeField] private int height = 5;
        [SerializeField] private int extra = 0;
        [SerializeField] private int cellSize = 1;
        [SerializeField] private Vector3 originPosition = Vector3.zero;
        [SerializeField] private bool debug = false;
        
        private GridSystem2D<Cell<Piece>> _grid;
        
        #region Unity Lifecycle
        private void Start()
        {
            InitializeGrid();
        }
        #endregion
        #region Initialization
        private async void InitializeGrid()
        {
            _grid = GridSystem2D<Cell<Piece>>.VerticalGrid(width, height, extra, cellSize, originPosition, debug);
        }
        #endregion

    }
}

