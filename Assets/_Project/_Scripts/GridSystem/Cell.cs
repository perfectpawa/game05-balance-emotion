using UnityEngine;

namespace MatchThreeSystem
{
    public class Cell<T>
    {
        private GridSystem2D<Cell<T>> _grid;
        private int _x;
        private int _y;
        private T _item;

        public Cell(GridSystem2D<Cell<T>> grid, int x, int y)
        {
            _grid = grid;
            _x = x;
            _y = y;
        }

        public void SetItem(T value)
        {
            _item = value;

            //update position for the object
            var pos = _grid.GetWorldPositionCenter(_x, _y);

            if (_item is MonoBehaviour monoBehaviour)
            {
                monoBehaviour.transform.position = pos;
            }
            else
            {
                Debug.LogError($"{_item} is not a MonoBehaviour");
            }
        }


        public T GetItem() => _item;

    }
}