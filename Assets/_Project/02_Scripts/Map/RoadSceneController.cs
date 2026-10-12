using System;
using Roadnest.Core;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// 주행 연출 진입점. 씬 전체에서 Update는 여기 하나뿐.
/// Entry point of the driving scene. The only Update in the scene.
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
    [SerializeField] ChoicePanel choicePanel;
    [SerializeField] ChoiceSlotData departChoice;
    [SerializeField] ChoiceSlotData settleChoice;
    [SerializeField] float ignitionPuffDelay = 0.35f;   // 시동 시작 후 배기가스까지 / delay from ignition start to the puff
    [SerializeField] float panelDelay = 0.7f;   // 시동 후 패널 등장까지 텀 / pause before the panel appears

    ChoiceSlotData[] _choices;
    readonly bool[] _enabled = new bool[2];
    Action<int> _onPicked;

    public ResourceStore Store { get; private set; }

    int _day = 1;

    void Awake()
    {
        Store = new ResourceStore(config.resourceCap);
        Store.Reset(config.startFuel, config.startFood, config.startWater);
        resourceHud.Bind(Store);

        _choices = new[] { departChoice, settleChoice };
        _onPicked = OnPicked;
        departChoice.cost.amount = config.departFuelCost;   // 문구의 {amount}와 실제 소모량을 일치시킴 / keep text and cost in sync
    }

    void Start() => dayBanner.Show(_day, OnBannerDone);

    void Update()
    {
        float dt = Time.deltaTime;
        scroll.Tick(dt);
        caravan.Tick(dt, scroll.Speed01);
        exhaust.Tick(dt, scroll.Speed01);
    }

    public void StartDay()
    {
        scroll.StartMoving();
        caravan.StartMoving();
        exhaust.StartEmitting();
        audioManager.StartDrive();
    }

    public void StopCar()
    {
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
        _enabled[0] = Store.Has(ResourceType.Fuel, config.departFuelCost);
        _enabled[1] = true;
        choicePanel.Show(_day, _choices, 2, _enabled, _onPicked);
    }

    // 0 = 이동, 1 = 정착 / 0 = depart, 1 = settle
    void OnPicked(int index)
    {
        if (index == 0)
        {
            Store.TryConsume(ResourceType.Fuel, config.departFuelCost);
            StartDay();
        }
        else
        {
            ParkCar();
        }
    }

#if UNITY_EDITOR
    // 임시 테스트, 확인 후 삭제 / temporary test menu, remove after checking
    [ContextMenu("Test/Fuel +1")] void TestFuelUp() => Store.Add(ResourceType.Fuel, 1);
    [ContextMenu("Test/Fuel -1")] void TestFuelDown() => Store.Add(ResourceType.Fuel, -1);
    [ContextMenu("Test/Food +1")] void TestFoodUp() => Store.Add(ResourceType.Food, 1);
    [ContextMenu("Test/Food -1")] void TestFoodDown() => Store.Add(ResourceType.Food, -1);
    [ContextMenu("Test/Water +1")] void TestWaterUp() => Store.Add(ResourceType.Water, 1);
    [ContextMenu("Test/Water -1")] void TestWaterDown() => Store.Add(ResourceType.Water, -1);
#endif
}