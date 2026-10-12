using UnityEngine;

/// <summary>
/// 임시 수치 한 곳. 인스펙터에서 밸런싱.
/// Tuning values in one place, editable in the inspector.
/// </summary>
[CreateAssetMenu(menuName = "ROADNEST/Game Config", fileName = "GameConfig")]
public class GameConfig : ScriptableObject
{
    [Header("시작 자원 / Start Resources")]
    public int startFuel = 5;
    public int startFood = 5;
    public int startWater = 5;

    [Header("상한 / Cap")]
    [Min(1)] public int resourceCap = 99;

    [Header("출발 / Depart")]
    [Min(0)] public int departFuelCost = 1;
}