using UnityEngine;
using Roadnest.Core;

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

    [Header("내구도 / Durability")]
    [Min(1)] public int maxDurability = 100;
    [Min(0)] public int departDurabilityLoss = 5;
    [Min(0)] public int settleDurabilityGain = 3;
    [Min(0)] public int repairAmount = 10;
    [Min(0)] public int settleRepairAmount = 15;

    [Header("캐릭터 / Characters")]
    [Min(1)] public int maxCharacterHp = 100;
    [Min(0)] public int hpLossMissingOne = 15;
    [Min(0)] public int hpLossMissingBoth = 35;

    [Header("하루 / Day")]
    [Min(1)] public int eventsPerDay = 5;

    // 코어는 Unity 타입을 모르므로 순수 C# 규칙 객체로 변환해 넘긴다
    // Core knows no Unity types, so hand it a plain rules object
    public RunRules ToRules()
    {
        return new RunRules
        {
            StartFuel = startFuel,
            StartFood = startFood,
            StartWater = startWater,
            ResourceCap = resourceCap,
            DepartFuelCost = departFuelCost,
            MaxDurability = maxDurability,
            DepartDurabilityLoss = departDurabilityLoss,
            SettleDurabilityGain = settleDurabilityGain,
            RepairAmount = repairAmount,
            SettleRepairAmount = settleRepairAmount,
            MaxCharacterHp = maxCharacterHp,
            HpLossMissingOne = hpLossMissingOne,
            HpLossMissingBoth = hpLossMissingBoth,
            EventsPerDay = eventsPerDay
        };
    }
}