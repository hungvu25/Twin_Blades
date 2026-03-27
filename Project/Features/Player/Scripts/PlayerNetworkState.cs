using Mirror;
using UnityEngine;

public class PlayerNetworkState : NetworkBehaviour
{
    [SyncVar]public bool IsMoving;

    [SyncVar]public bool IsRunning;
    [SyncVar] public bool IsAlive =true;
    [SyncVar]public Vector2 LastMoveDirection = Vector2.down;

    [Command]
    public void CmdUpdateAnimationState(bool isMoving, Vector2 lastMoveDirection, bool isRunning)
    {
        IsMoving = isMoving;
        LastMoveDirection = lastMoveDirection;
        IsRunning = isRunning;
    }
}