using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum RunState
{
    Lobby,
    Running,
    Combat,
    UpgradeSelection,
    GameOver,
    Victory,
    MapSelection,
    BossTransition
}

public enum PlayerRunPhase : byte
{
    Lobby,
    Running,
    Combat,
    RewardChoice,
    WaitingAtExit,
    Eliminated
}
