using System;
using UnityEngine;
using UnityEngine.InputSystem;
using static MatchThree_Actions;


[CreateAssetMenu(fileName = "InputReader", menuName = "New InputReader")]
public class InputReader : ScriptableObject, IPlayerActions
{
    public event Action OnSelect = delegate { };
    
    public MatchThree_Actions inputActions;
    
    public Vector2 SelectPosition => inputActions.Player.SelectPosition.ReadValue<Vector2>();
    
    public void EnablePlayerAction()
    {
        if (inputActions == null)
        {
            inputActions = new MatchThree_Actions();
            inputActions.Player.SetCallbacks(this);
        }
        inputActions.Enable();
    }

    void IPlayerActions.OnSelect(InputAction.CallbackContext context)
    {
        if (context.canceled) OnSelect?.Invoke();
    }

    public void OnSelectPosition(InputAction.CallbackContext context)
    {
    }
}