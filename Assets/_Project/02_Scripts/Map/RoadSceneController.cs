using System;
using System.Collections.Generic;
using DG.Tweening;
using Roadnest.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 주행 연출 진입점이자 코어(DayFlow)와 화면을 잇는 곳. 씬 전체에서 Update는 여기 하나뿐.
/// Entry point of the driving scene and the bridge between the core (DayFlow) and the view. The only Update in the scene.
/// </summary>
public class RoadSceneController : MonoBehaviour
{
    [SerializeField] ScrollController scroll;
    [SerializeField] CaravanVisual caravan;
    [SerializeField] ExhaustEmitter exhaust;
    [SerializeField] AudioManager audioManager;
    [SerializeField] DayBanner dayBanner;
    [SerializeField] GameConfig config;
    [SerializeField] ResourceHud resourceHud;
    [SerializeField] DurabilityHud durabilityHud;
    [SerializeField] CharactorHud charactorHud;
    [SerializeField] ChoicePanel choicePanel;
    [SerializeField] EventPanel eventPanel;
    [SerializeField] DistributionPanel distributionPanel;
    [SerializeField] FixOrCraftPanel fixOrCraftPanel;
    [SerializeField] FixPanel fixPanel;
    [SerializeField] GameOverPanel gameOverPanel;
    [SerializeField] float gameOverDelay = 0.8f;
    [SerializeField] ChoiceSlotData departChoice;
    [SerializeField] ChoiceSlotData settleChoice;
    [SerializeField] TextAsset eventsJson;
    [SerializeField] int fixedSeed;                     // 0이면 매 판 랜덤 / 0 = random every run
    [SerializeField] float ignitionPuffDelay = 0.35f;   // 시동 시작 후 배기가스까지 / delay from ignition start to the puff
    [SerializeField] float panelDelay = 0.7f;           // 시동 후 패널 등장까지 텀 / pause before the panel appears
    [SerializeField] float waitMin = 5f;                // 이벤트 전 랜덤 대기 / random wait before an event
    [SerializeField] float waitMax = 10f;

    ChoiceSlotData[] _choices;
    readonly bool[] _enabled = new bool[2];
    Action<int> _onPicked;
    Action<int> _onEventPicked;
    Action _onResultContinue;
    DayFlow _flow;
    TweenCallback _onWaitDone;
    Action _showEvent;
    Action _onDistributionConfirm;
    Action<MaintenanceChoice> _onMaintenancePicked;
    Action<ItemType> _onRepairUse;
    Action _onRepairBack;
    Action _onNextDayBanner;
    Action _onRestart;
    TweenCallback _onGameOver;
    bool _moving;

    public ResourceStore Store { get; private set; }



    void Awake()
    {
        RunRules rules = config.ToRules();
        Store = new ResourceStore(rules.ResourceCap);
        RunState run = RunState.CreateDefault(rules, Store);

        var errors = new List<string>();
        EventTable table = EventTable.FromJson(eventsJson.text, errors);
        if (table == null)
        {
            for (int i = 0; i < errors.Count; i++) Debug.LogError(errors[i]);
            enabled = false;
            return;
        }

        ulong seed = fixedSeed != 0 ? (ulong)fixedSeed : (ulong)DateTime.UtcNow.Ticks;
        _flow = new DayFlow(run, table, seed);
#if UNITY_EDITOR
        Debug.Log("[Roadnest] seed = " + seed);
#endif

        resourceHud.Bind(Store);
        durabilityHud.Bind(run.Durability, rules.MaxDurability);
        charactorHud.Bind(run.Characters);

        _choices = new[] { departChoice, settleChoice };
        _onPicked = OnPicked;
        _onEventPicked = OnEventPicked;
        _onResultContinue = OnResultContinue;
        _onWaitDone = OnWaitDone;
        _showEvent = ShowEvent;
        _onDistributionConfirm = OnDistributionConfirm;
        _onMaintenancePicked = OnMaintenancePicked;
        _onRepairUse = OnRepairUse;
        _onRepairBack = OnRepairBack;
        _onNextDayBanner = ShowChoices;
        _onRestart = OnRestart;
        _onGameOver = ShowGameOver;
        departChoice.cost.amount = rules.DepartFuelCost;   // 문구의 {amount}와 실제 소모량을 일치시킴 / keep text and cost in sync
    }

    void Start()
    {
        if (_flow == null) return;
        dayBanner.Show(_flow.Run.Day, OnBannerDone);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        scroll.Tick(dt);
        caravan.Tick(dt, scroll.Speed01);
        exhaust.Tick(dt, scroll.Speed01);
    }

    public void StartDay()
    {
        _moving = true;
        scroll.StartMoving();
        caravan.StartMoving();
        exhaust.StartEmitting();
        audioManager.StartDrive();
    }

    public void StopCar()
    {
        _moving = false;
        scroll.StopMoving();
        caravan.StopMoving();
        exhaust.StopEmitting();
        audioManager.StartIdle();
    }

    void ParkCar()
    {
        caravan.TurnOff();
        audioManager.EngineOff();
    }

    // DAY 배너 종료 -> 시동 연출 -> 패널 / banner done -> ignition -> panel
    void OnBannerDone()
    {
        audioManager.PlayIgnition();
        exhaust.Burst(ignitionPuffDelay);
        caravan.PlayIgnition(OnIgnited);
    }

    void OnIgnited()
    {
        audioManager.StartIdle();
        DOVirtual.DelayedCall(panelDelay, ShowChoices).SetLink(gameObject);
    }

    void ShowChoices()
    {
        _enabled[0] = _flow.CanDepart;
        _enabled[1] = true;
        choicePanel.Show(_flow.Run.Day, _choices, 2, _enabled, _onPicked);
    }

    // 0 = 출발, 1 = 정착 / 0 = depart, 1 = settle
    void OnPicked(int index)
    {
        bool depart = index == 0;
        if (!_flow.ChooseMode(depart ? MoveMode.Depart : MoveMode.Settle))
        {
            ShowChoices();
            return;
        }

        durabilityHud.Show(_flow.Run.Durability);   // 출발 -5 / 정착 +3 반영 / reflect the depart/settle change

        if (depart) StartDay();
        else ParkCar();

        // 기존: if (_flow.Phase == DayPhase.Event) ShowEvent(); else HandleFlowEnd();
        if (_flow.Phase == DayPhase.Event) BeginWait();
        else HandleFlowEnd();
    }

    void ShowEvent() => eventPanel.ShowEvent(_flow, _onEventPicked);

    // 다음 이벤트까지 랜덤 대기 / random wait until the next event
    void BeginWait()
    {
        float t = UnityEngine.Random.Range(waitMin, waitMax);
        DOVirtual.DelayedCall(t, _onWaitDone).SetLink(gameObject);
    }

    // 주행 중이면 멈추고, 예고형은 느낌표 후 패널, 돌발형은 바로 패널
    // Stop if driving; telegraphed shows "!" then the panel, sudden goes straight to the panel
    void OnWaitDone()
    {
        if (_moving) StopCar();
        if (_flow.CurrentEvent.Trigger == TriggerType.Telegraphed) caravan.ShowAlert(_showEvent);
        else ShowEvent();
    }

    void OnEventPicked(int choiceIndex)
    {
        EventDef ev = _flow.CurrentEvent;
        resourceHud.Hold();     // 확인 누를 때까지 자원 표시를 멈춤 / freeze the HUD until Done
        ChoiceResult result = _flow.Choose(choiceIndex);
        if (!result.Success)
        {
            resourceHud.Release();
            ShowEvent();
            return;
        }
        resourceHud.ShowCost(ev.Choices[choiceIndex].Cost);   // 비용은 즉시, 보상은 확인 때 / cost now, rewards on Done
        durabilityHud.ShowCost(ev.Choices[choiceIndex].Cost);
        charactorHud.ShowCost(ev.Choices[choiceIndex].Cost);
        eventPanel.ShowResult(ev, result, _flow.Run, _onResultContinue);
    }

    void OnResultContinue()
    {
        resourceHud.Release();  // 이때 자원 변화 연출 / resource feedback plays here
        durabilityHud.Show(_flow.Run.Durability);   // 보상 반영 / apply rewards
        charactorHud.Show(_flow.Run.Characters);
        if (_flow.Phase != DayPhase.Event) { HandleFlowEnd(); return; }

        eventPanel.Hide();
        if (_flow.Mode == MoveMode.Depart) StartDay();
        BeginWait();
    }

    // 분배 단계면 분배 패널, 아니면 런 종료(게임오버 패널은 3-4에서)
    // Distribution phase opens the panel; otherwise the run is over (game over panel comes in 3-4)
    void HandleFlowEnd()
    {
        eventPanel.Hide();
        if (_moving) StopCar();

        if (_flow.Phase == DayPhase.Distribution)
        {
            distributionPanel.Show(_flow, _onDistributionConfirm);
            return;
        }

        DOVirtual.DelayedCall(gameOverDelay, _onGameOver).SetLink(gameObject);
    }

    void ShowGameOver() => gameOverPanel.Show(_flow.Run.EndReason, _flow.Run.Day, _onRestart);

    void OnRestart() => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);

    // 확정 -> 자원/HP 반영 -> 정비 선택 패널 (게임오버면 종료 처리)
    // Confirm -> apply resources/HP -> maintenance panel (game over goes to the end flow)
    void OnDistributionConfirm()
    {
        _flow.ConfirmDistribution();
        distributionPanel.Hide();
        charactorHud.Show(_flow.Run.Characters);

        if (_flow.Phase == DayPhase.Over) { HandleFlowEnd(); return; }

        fixOrCraftPanel.Show(_flow, _onMaintenancePicked);
    }

    // 수리는 FixPanel로, 제작/넘어가기는 바로 하루 종료
    // Repair opens FixPanel; craft/pass ends the day right away
    void OnMaintenancePicked(MaintenanceChoice choice)
    {
        if (choice == MaintenanceChoice.Repair)
        {
            fixOrCraftPanel.HideBody();
            fixPanel.Show(_flow, _onRepairUse, _onRepairBack);
            return;
        }

        if (!_flow.Maintain(choice))
        {
            fixOrCraftPanel.Show(_flow, _onMaintenancePicked);
            return;
        }
        EndMaintenance();
    }

    void OnRepairUse(ItemType item)
    {
        if (!_flow.Repair(item))
        {
            fixPanel.Show(_flow, _onRepairUse, _onRepairBack);
            return;
        }
        fixPanel.Hide();
        EndMaintenance();
    }

    void OnRepairBack()
    {
        fixPanel.Hide();
        fixOrCraftPanel.ShowBody(_flow, _onMaintenancePicked);
    }

    // 정비 종료 -> 내구도 반영 -> 다음 날 배너 -> 출발/정착 선택
    // Maintenance done -> durability -> next-day banner -> depart/settle
    void EndMaintenance()
    {
        fixOrCraftPanel.Hide();
        durabilityHud.Show(_flow.Run.Durability);

        // 다음 날 배너 앞에 연출을 넣을 자리 / room for a presentation before the banner
        dayBanner.Show(_flow.Run.Day, _onNextDayBanner);
    }

#if UNITY_EDITOR
    // 임시 테스트, 확인 후 삭제 / temporary test menu, remove after checking
    [ContextMenu("Test/Fuel +1")] void TestFuelUp() => Store.Add(ResourceType.Fuel, 1);
    [ContextMenu("Test/Fuel -1")] void TestFuelDown() => Store.Add(ResourceType.Fuel, -1);
    [ContextMenu("Test/Food +1")] void TestFoodUp() => Store.Add(ResourceType.Food, 1);
    [ContextMenu("Test/Food -1")] void TestFoodDown() => Store.Add(ResourceType.Food, -1);
    [ContextMenu("Test/Water +1")] void TestWaterUp() => Store.Add(ResourceType.Water, 1);
    [ContextMenu("Test/Water -1")] void TestWaterDown() => Store.Add(ResourceType.Water, -1);
    [ContextMenu("Test/Items +1 (all)")]
    void TestItemsUp()
    {
        ItemStore it = _flow.Run.Items;
        it.Add(ItemType.RepairTool, 1);
        it.Add(ItemType.RepairKitLow, 1);
        it.Add(ItemType.RepairKitHigh, 1);
    }
#endif
}