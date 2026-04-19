using System;
using System.Collections.Generic;
using BattleUI;
using BattleUI.BattleUnit;
using BattleUI.Operation;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime;
using UI.Utility;
using UnityEngine;
using UnityEngine.EventSystems;
using BepInEx.Logging;
using UnityEngine.UI;
using TMPro;
using Il2CppInterop.Runtime.Injection;
using Il2CppList = Il2CppSystem.Collections.Generic.List<SinActionModel>;
using Unity.Mathematics;
using System.Linq;

namespace EnemyCtrl;

internal static class PluginInfo
{
    internal const string PLUGIN_GUID    = "com.samheult.enemyctrl";
    internal const string PLUGIN_NAME    = "EnemyCtrl";
    internal const string PLUGIN_VERSION = "1.0.0";
}

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class EnemyCtrlPlugin : BasePlugin
{
    public static EnemyCtrlPlugin Instance;
    public static ManualLogSource Logger;

    static Dictionary<long, List<int>> _skillBagList = new();
    public static Dictionary<long, SkillBagState> _skillBagStates = new();

    public override void Load()
    {
        Instance = this;
        Logger = Log;
        new Harmony(PluginInfo.PLUGIN_GUID).PatchAll(typeof(EnemyCtrlPatches));

        ClassInjector.RegisterTypeInIl2Cpp<EgoSelectionUI>();
    }

    private static void AddNewBag(BattleUnitModel unit)
    {
        long ptr = unit.Pointer.ToInt64();

        SkillStaticDataList skillList = Singleton<StaticDataManager>.Instance._skillList;

        List<int> aNewBag = new();
        
        if (unit.UnitDataModel?._unitKeywordList != null)
        {
            foreach (UnitAttribute unitAttribute in unit.UnitDataModel._unitAttributeList)
            {
                aNewBag.AddRange(Enumerable.Repeat(unitAttribute.skillId, unitAttribute.number));
            }
        }

        if (aNewBag.Count == 0) aNewBag.Add(1000104);

        aNewBag = aNewBag.OrderBy(item => System.Random.Shared.Next()).ToList();
        if (!_skillBagStates.ContainsKey(ptr)) _skillBagStates[ptr] = new SkillBagState();

        _skillBagStates[ptr].SkillBag.AddRange(aNewBag);
    }

    public static void RecheckSkillBag(BattleUnitModel unit, SinActionModel sam)
    {
        long ptr = unit.Pointer.ToInt64();

        if (!_skillBagStates.ContainsKey(ptr)) _skillBagStates[ptr] = new SkillBagState();

        SkillBagState state = _skillBagStates[ptr];

        if (state.SkillIdUsedLastTurn.HasValue)
        {
            state.OnDashboardSkills.Remove(state.SkillIdUsedLastTurn.Value);
        }

        while (state.OnDashboardSkills.Count < 7)
        {
            if (state.SkillBag.Count == 0) AddNewBag(unit);

            int nextSkill = state.SkillBag[0];

            state.SkillBag.RemoveAt(0);
            state.OnDashboardSkills.Add(nextSkill);
        }

        if (sam.currentSinList == null) sam.currentSinList = new();
        sam.currentSinList.Clear();

        foreach(int skillId in state.OnDashboardSkills) sam.currentSinList.Add(new UnitSinModel(skillId, unit, sam));
    }

    public class SkillBagState
    {
        public List<int> SkillBag = new();
        public List<int> OnDashboardSkills = new();
        public int? SkillIdUsedLastTurn = null;
    }

    public class EgoSelectionUI : MonoBehaviour
    {
        public EgoSelectionUI(IntPtr ptr) : base(ptr) {}

        public static EgoSelectionUI _instance;
        public static EgoSelectionUI Instance
        {
            get
            {
                if (_instance == null)
                {
                    var newUI = new GameObject("EnemyCtrlCustomEgoPanel");
                    UnityEngine.Object.DontDestroyOnLoad(newUI);
                    _instance = newUI.AddComponent<EgoSelectionUI>();
                }

                return _instance;
            }
        }

        private bool _showPanel = false;

        private const int WIDTH = 600;
        private const int HEIGHT = 250;
        private Rect _panelRect = new Rect(Screen.width / 2 - WIDTH / 2, Screen.height / 2 - HEIGHT / 2, WIDTH, HEIGHT);
        private SinActionModel _currentSam;

        private Dictionary<BattleEgoModel, bool> _egoModelPair = new();

        public void OpenPanel(SinActionModel sam)
        {
            if (sam == null) return;

            _currentSam = sam;
            _egoModelPair.Clear();

            Il2CppSystem.Collections.Generic.List<BattleEgoModel> egoList = sam.UnitModel.GetEgoModelList();

            if (egoList != null)
            {
                foreach(BattleEgoModel egoModel in egoList)
                {
                    _egoModelPair.Add(egoModel, false);
                }
            }

            _showPanel = true;
        }

        void Update()
        {
            if (!_showPanel) return;


            if (Input.GetKeyDown(KeyCode.Escape)) 
            {
                _showPanel = false;
                return;
            }

            if (Input.GetMouseButtonDown(0))
            if (!_panelRect.Contains(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y)))
            _showPanel = false;
        }

        public void DrawUI(int id)
        {
            GUILayout.BeginVertical();
            GUILayout.Space(25);

            foreach(var ego in _egoModelPair)
            {
                string name = ego.Key.AwakeningSkillModel.skillData.skillName;
                GUILayout.BeginHorizontal();

                GUI.enabled = CanUseEgo(ego.Key, ego.Value);
                if (GUILayout.Button(ego.Value ? $"<color=#FF0000>[CORROSION]</color> {name}" : name, GUILayout.Height(40))) SelectEgo(ego.Key, ego.Value);
                GUI.enabled = true;

                if (GUILayout.Button(ego.Value ? "To Awakening" : "<color=#FF0000>To Corrosion</color>", GUILayout.Width(100), GUILayout.Height(40)))
                _egoModelPair[ego.Key] = !ego.Value;

                GUILayout.EndHorizontal();
            }

            GUILayout.EndVertical();

            GUI.DragWindow(new Rect(0, 0, 10000, 20));
        }

        void OnGUI()
        {
            if (!_showPanel) return;

            _panelRect = GUI.Window(
                31037,
                _panelRect,
                (GUI.WindowFunction) DrawUI,
                "The E.G.O Files are CRAZY!"
            );
        }

        private bool CanUseEgo(BattleEgoModel bem, bool isCorrosion)
        {
            var stockManager = Singleton<SinManager>.Instance._egoStockMangaer;

            foreach(ATTRIBUTE_TYPE sin in Enum.GetValues<ATTRIBUTE_TYPE>())
            {
                if (bem.GetNeedResourceCount(sin, isCorrosion) > stockManager.GetAttributeStockNumberByAttributeType(UNIT_FACTION.ENEMY, sin)) return false;
            }
            
            return true;
        }

        private void SelectEgo(BattleEgoModel bem, bool isCorrosion)
        {
            // UnitSinModel newEgoSin = new UnitSinModel(_currentSam.UnitModel, id, _currentSam, true);
            UnitSinModel newEgoSin = new UnitSinModel(bem, _currentSam.UnitModel, _currentSam, isCorrosion, true);

            // if (EnemyCtrlPatches._defenseSkillSwapPreserve[_currentSam.Pointer])
            // _defenseSkillSwapPreserve[sam.Pointer] = bottomSin.GetSkill().GetID();

            SkillModel currentBottomSkill = _currentSam.currentSinList[0].GetSkill();

            if (currentBottomSkill != null)
            if (!currentBottomSkill.IsDefense() && !currentBottomSkill.IsEgoSkill() && !currentBottomSkill.IsEgoOverclock())
            EnemyCtrlPatches._defenseSkillSwapPreserve[_currentSam.Pointer] = currentBottomSkill.GetID();

            _currentSam.currentSinList[0] = newEgoSin;

            var controller = SingletonBehavior<BattleUIRoot>.Instance.NewOperationController;

            if (controller != null)
            {
                NewOperationSinActionSlot newOperationSinActionSlot = null;

                foreach(NewOperationSinActionSlot slot in controller._sinActionSlotList)
                {
                    if (slot?.SinAction?.Pointer == _currentSam.Pointer && slot.SinAction != null)
                    {
                        newOperationSinActionSlot = slot;
                        break;
                    }
                }

                if (newOperationSinActionSlot != null) EnemyCtrlPatches.SetEnemySinSlot(newOperationSinActionSlot._firstSinSlot, _currentSam.currentSinList[0]);
                controller.UpdateAllSlotForNormal();
            }

            _showPanel = false;
        }
    }
}


[HarmonyPatch]
internal static class EnemyCtrlPatches
{
    static bool _cmdOpen = false;
    static readonly HashSet<IntPtr> _injectedEnemies = new();
    static readonly Dictionary<IntPtr, (SinActionModel target, UnitSinModel sin)> _targets = new();
    static readonly Dictionary<IntPtr, SinActionModel> _pinned = new();
    static SinActionModel? _drag     = null;
    static UnitSinModel?   _dragSin  = null;
    static SinActionModel? _hoverSam = null;
    static readonly HashSet<int> _triggeredSlots = new();
    static int _actionSeq = 0;
    static readonly Dictionary<IntPtr, (SinActionModel playerSam, int seq)> _duelIntent = new();


    static readonly Dictionary<IntPtr, SinActionModel> _portraitSam = new();
    public static readonly Dictionary<IntPtr, int> _defenseSkillSwapPreserve = new();

    static SinActionModel? _pendingDuelEnemy  = null;
    static SinActionModel? _pendingDuelSinner = null;

    [HarmonyPatch(typeof(BattleUIRoot), nameof(BattleUIRoot.OnRoundStart))]
    [HarmonyPrefix]
    static void OnRoundStart() => _cmdOpen = true;

    [HarmonyPatch(typeof(StageController), nameof(StageController.CompleteCommand))]
    [HarmonyPrefix]
    static void OnCompleteCommand() => _cmdOpen = false;

    [HarmonyPatch(typeof(SinManager), nameof(SinManager.OnRoundStart_Before))]
    [HarmonyPostfix]
    static void OnRoundReset()
    {
        _cmdOpen = false;
        _injectedEnemies.Clear();
        _targets.Clear();
        _pinned.Clear();
        _duelIntent.Clear();
        _actionSeq = 0;
        if (_drag != null) CancelDrag();
        _drag             = null;
        _dragSin          = null;
        _hoverSam         = null;
        _pendingDuelEnemy  = null;
        _pendingDuelSinner = null;

        _portraitSam.Clear();
        _defenseSkillSwapPreserve.Clear();
    }

    [HarmonyPatch(typeof(StageModel), nameof(StageModel.Init))]
    [HarmonyPrefix]
    static void OnStageInit()
    {
        _injectedEnemies.Clear();
        _targets.Clear();
        _pinned.Clear();
        _duelIntent.Clear();
        _actionSeq        = 0;
        _drag             = null;
        _dragSin          = null;
        _hoverSam         = null;
        _pendingDuelEnemy  = null;
        _pendingDuelSinner = null;
        _triggeredSlots.Clear();

        _portraitSam.Clear();
        _defenseSkillSwapPreserve.Clear();
        EnemyCtrlPlugin._skillBagStates.Clear();
    }

    [HarmonyPatch(typeof(NewOperationController), nameof(NewOperationController.SetData))]
    [HarmonyPrefix]
    static void InjectEnemies(NewOperationController __instance,
        ref Il2CppList sinActionList, bool isRoundStart)
    {
        if (!_cmdOpen) return;
        if (!isRoundStart) return;
        try
        {
            var sinMgr = Singleton<SinManager>.Instance;
            if (sinMgr == null) return;

            var enemies = sinMgr.GetActionListByFaction(UNIT_FACTION.ENEMY);
            if (enemies == null || enemies.Count == 0) return;

            if (sinActionList == null) sinActionList = new Il2CppList();

            int needed = sinActionList.Count + enemies.Count;
            EnsureUiSlots(__instance, needed);

            for (int i = 0; i < enemies.Count; i++)
            {
                var sam = enemies[i];
                if (sam?.UnitModel == null || sam.UnitModel.IsDead()) continue;

                // if (sam.currentSinList != null && sam.currentSinList.Count > 0)
                // {
                //     UnitSinModel gameSin = sam.currentSinList[0];
                //     SkillModel gameSkill = gameSin.GetSkill() ?? gameSin.GetNewSkill();

                //     EnemyCtrlPlugin.Logger.LogMessage($"Rolled Skill {gameSkill.GetID()}");
                // }

                EnemyCtrlPlugin.RecheckSkillBag(sam.UnitModel, sam);

                sinActionList.Add(sam);
                _injectedEnemies.Add(sam.Pointer);
            }
        }
        catch { }
    }

    static void EnsureUiSlots(NewOperationController ctrl, int needed)
    {
        var sinSlots  = ctrl._sinActionSlotList;
        var portraits = ctrl._portraitlist;
        int have = sinSlots.Count;
        if (have >= needed) return;

        var sinTemplate      = sinSlots[0];
        var portraitTemplate = portraits[0];
        var sinParent        = ctrl.rect_sinActionSlotParent;
        var portraitParent   = ctrl.rect_portraitSlotParent;

        for (int i = have; i < needed; i++)
        {
            var newPortrait = UnityEngine.Object.Instantiate(portraitTemplate, portraitParent);
            var newSin      = UnityEngine.Object.Instantiate(sinTemplate, sinParent);
            portraits.Add(newPortrait);
            sinSlots.Add(newSin);
            newSin.Init(newPortrait);
        }
    }

    [HarmonyPatch(typeof(BattleUnitModel), nameof(BattleUnitModel.IsActionable), new Type[] { })]
    [HarmonyPostfix]
    static void Enemy_IsActionable(BattleUnitModel __instance, ref bool __result)
    {
        if (__result || __instance == null) return;
        if (!_cmdOpen) return;
        try
        {
            if (_injectedEnemies.Contains(IL2CPP.Il2CppObjectBaseToPtr(__instance)))
                __result = true;
        }
        catch { }
    }

    [HarmonyPatch(typeof(NewOperationSinActionSlot), nameof(NewOperationSinActionSlot.IsPossiblePointerCommon))]
    [HarmonyPrefix]
    static bool EnemySlot_BlockVanillaPointer(NewOperationSinActionSlot __instance, ref bool __result)
    {
        if (!IsEnemy(__instance._sinAction)) return true;
        if (_drag != null) { __result = false; return false; }
        return true;
    }


    [HarmonyPatch(typeof(NewOperationSinSlot), nameof(NewOperationSinSlot.PointerEnterForNormal))]
    [HarmonyPostfix]
    static void EnemySlot_EnterPending(NewOperationSinSlot __instance)
    {
        if (!_cmdOpen || _drag != null) return;
        var enemySam = __instance._sinActionSlot?._sinAction;
        if (!IsEnemy(enemySam)) return;
        try
        {
            var ctrl = SingletonBehavior<BattleUIRoot>.Instance?.NewOperationController;
            if (ctrl == null || !ctrl.IsDrawing) return;

            int lastIdx = ctrl.GetLastSelectedSlotIndex();
            if (lastIdx < 0) return;
            var slots = ctrl._sinActionSlotList;
            if (lastIdx >= slots.Count) return;
            var sinnerSam = slots[lastIdx].SinAction;
            if (sinnerSam == null || IsEnemy(sinnerSam)) return;

            _pendingDuelEnemy  = enemySam;
            _pendingDuelSinner = sinnerSam;
        }
        catch { }
    }


    [HarmonyPatch(typeof(NewOperationSinSlot), nameof(NewOperationSinSlot.PointerExitForNormal))]
    [HarmonyPostfix]
    static void EnemySlot_ExitPending(NewOperationSinSlot __instance)
    {
        var sam = __instance._sinActionSlot?._sinAction;
        if (!IsEnemy(sam)) return;
        if (_pendingDuelEnemy?.Pointer == sam?.Pointer)
        {
            _pendingDuelEnemy  = null;
            _pendingDuelSinner = null;
        }
    }


    [HarmonyPatch(typeof(NewOperationController), nameof(NewOperationController.CheckCompleteForNormal))]
    [HarmonyPrefix]
    static void CheckComplete_CommitDuel(NewOperationController __instance)
    {
        if (!_cmdOpen || _pendingDuelEnemy == null || _pendingDuelSinner == null) return;
        try
        {
            if (!__instance.IsDrawing) return;
            var enemySam  = _pendingDuelEnemy;
            var sinnerSam = _pendingDuelSinner;
            _pendingDuelEnemy  = null;
            _pendingDuelSinner = null;

            _duelIntent[enemySam.Pointer] = (sinnerSam, ++_actionSeq);

            var unitSin = enemySam.currentSinList?.Count > 0 ? enemySam.currentSinList[0] : null;
            if (unitSin != null)
                try { enemySam.SelectSin(unitSin, sinnerSam); } catch { }

            try { SingletonBehavior<BattleUIRoot>.Instance?.ShowAllCharacterTargetArrows(); } catch { }
        }
        catch { }
    }


    [HarmonyPatch(typeof(SinActionModel), nameof(SinActionModel.DeSelectSin))]
    [HarmonyPostfix]
    static void SinnerDeSelected(SinActionModel __instance)
    {
        if (!_cmdOpen || __instance == null || IsEnemy(__instance)) return;
        try
        {
            bool removed = false;
            foreach (var key in new List<IntPtr>(_duelIntent.Keys))
            {
                if (_duelIntent.TryGetValue(key, out var intent) &&
                    intent.playerSam?.Pointer == __instance.Pointer)
                {
                    _duelIntent.Remove(key);
                    removed = true;
                }
            }
            if (removed)
                try { SingletonBehavior<BattleUIRoot>.Instance?.ShowAllCharacterTargetArrows(); } catch { }
        }
        catch { }
    }

    [HarmonyPatch(typeof(NewOperationSinActionSlot), nameof(NewOperationSinActionSlot.SetData))]
    [HarmonyPrefix]
    static bool SafeSetDataForEnemy(NewOperationSinActionSlot __instance,
        SinActionModel sinAction, BattleUnitView view, bool isRoundStart)
    {
        if (!IsEnemy(sinAction)) return true;
        try
        {
            __instance._sinAction = sinAction;
            __instance._portraitSlot?.SetData(view, isRoundStart);
            __instance._firstSinSlot?.Init(__instance);
            __instance._secondSinSlot?.Init(__instance);

            Il2CppSystem.Collections.Generic.List<UnitSinModel> sinList = sinAction.currentSinList;

            if (__instance._portraitSlot != null) _portraitSam[__instance._portraitSlot.Pointer] = sinAction;

            if (sinList != null && sinList.Count > 0)
            {
                SetEnemySinSlot(__instance._firstSinSlot,  sinList.Count > 0 ? sinList[0] : null);
                SetEnemySinSlot(__instance._secondSinSlot, sinList.Count > 1 ? sinList[1] : null);
            }
            else
            {
                __instance._firstSinSlot?.SetActiveSlot(false);
                __instance._secondSinSlot?.SetActiveSlot(false);
            }
            __instance._readySinSlot?.SetActiveSlot(false);
            if (__instance.rect_pivot != null)
                __instance.rect_pivot.localScale = Vector3.one;
        }
        catch { }
        return false;
    }

    public static void SetEnemySinSlot(NewOperationSinSlot? slot, UnitSinModel? sin)
    {
        if (slot == null) return;
        if (sin?.GetSkill() != null)
        {
            slot._unitSin = sin;
            slot._skillSlot?.SetSkill(sin, false);
            slot.SetActiveSlot(true);
        }
        else
        {
            slot._unitSin = null;
            slot.SetActiveSlot(false);
        }
    }

    [HarmonyPatch(typeof(SinActionModel), nameof(SinActionModel.SelectSin),
        new[] { typeof(UnitSinModel) })]
    [HarmonyPrefix]
    static bool CancelAISkill(SinActionModel __instance, UnitSinModel sin)
    {
        if (IsEnemy(__instance)) return false;
        return true;
    }


    [HarmonyPatch(typeof(SinActionModel), nameof(SinActionModel.SelectSin),
        new[] { typeof(UnitSinModel) })]
    [HarmonyPostfix]
    static void CaptureAISkill(SinActionModel __instance, UnitSinModel sin)
    {
        if (!IsEnemy(__instance)) return;
        if (sin?.GetSkill() == null) return;
        try
        {
            var sinList = __instance.currentSinList;
            if (sinList == null)
            {
                sinList = new Il2CppSystem.Collections.Generic.List<UnitSinModel>();
                __instance.currentSinList = sinList;
            }
            else { sinList.Clear(); }
            sinList.Add(sin);

            try
            {
                var root = SingletonBehavior<BattleUIRoot>.Instance;
                root?.NewOperationController?.UpdateAllSlotForNormal();
                root?.ShowAllCharacterTargetArrows();
            }
            catch { }
        }
        catch { }
    }

    [HarmonyPatch(typeof(SinActionModel), nameof(SinActionModel.SelectSin),
        new[] { typeof(UnitSinModel), typeof(SinActionModel) })]
    [HarmonyPostfix]
    static void CapturePlayerTarget(SinActionModel __instance,
        UnitSinModel sin, SinActionModel targetSinAction)
    {
        if (!IsEnemy(__instance)) return;
        if (!_cmdOpen) return;
        if (targetSinAction == null) return;
        if (targetSinAction.Pointer == __instance.Pointer) return;
        if (IsEnemy(targetSinAction)) return;
        _targets[__instance.Pointer] = (targetSinAction, sin);

        long ptr = __instance.UnitModel.Pointer.ToInt64();

        if (EnemyCtrlPlugin._skillBagStates.TryGetValue(ptr, out var state)) state.SkillIdUsedLastTurn = sin.GetSkill()?.GetID();

        try { SingletonBehavior<BattleUIRoot>.Instance?.ShowAllCharacterTargetArrows(); }
        catch { }
    }

    [HarmonyPatch(typeof(NewOperationSinActionSlot),
        nameof(NewOperationSinActionSlot.SetActiveAllSinSlot))]
    [HarmonyPrefix]
    static bool HideEnemySubSlots(NewOperationSinActionSlot __instance, bool on)
    {
        if (!IsEnemy(__instance.SinAction)) return true;
        if (!on) return true;
        try
        {
            var sinList = __instance.SinAction?.currentSinList;
            bool hasSecond = sinList != null && sinList.Count >= 2 && sinList[1]?.GetSkill() != null;
            __instance._readySinSlot?.SetActiveSlot(false);
            __instance._firstSinSlot?.SetActiveSlot(true);
            __instance._secondSinSlot?.SetActiveSlot(hasSecond);
        }
        catch { }
        return false;
    }

    [HarmonyPatch(typeof(UnitSinModel), nameof(UnitSinModel.GetSkill))]
    [HarmonyPostfix]
    static void GetSkill_Fallback(UnitSinModel __instance, ref SkillModel __result)
    {
        if (__result != null) return;
        try { __result = __instance.GetNewSkill(); } catch { }
    }

    [HarmonyPatch(typeof(OperationSkillSlot), nameof(OperationSkillSlot.SetSkill))]
    [HarmonyPrefix]
    static bool SetSkill_Guard(UnitSinModel sin)
    {
        try { return sin?.GetSkill() != null; }
        catch { return false; }
    }

    [HarmonyPatch(typeof(NewOperationPortraitSlot), nameof(NewOperationPortraitSlot.SetData))]
    [HarmonyPrefix]
    static bool Portrait_Guard(BattleUnitView view) => view != null;

    [HarmonyPatch(typeof(SinActionModel), "IsTargetable")]
    [HarmonyPostfix]
    static void SAM_IsTargetable(SinActionModel __instance, ref bool __result)
    {
        if (__instance?.GetFaction() == UNIT_FACTION.PLAYER) __result = true;
    }

    [HarmonyPatch(typeof(SinActionModel), nameof(SinActionModel.SelectSin),
        new[] { typeof(UnitSinModel), typeof(SinActionModel) })]
    [HarmonyPostfix]
    static void SinnerTargetsEnemy_Gate(SinActionModel __instance,
        UnitSinModel sin, SinActionModel targetSinAction)
    {
        if (!_cmdOpen) return;
        if (__instance == null || targetSinAction == null) return;
        if (IsEnemy(__instance)) return;
        if (!IsEnemy(targetSinAction)) return;
        try
        {
            var enemyAction  = targetSinAction.CurrentBattleAction;
            var playerAction = __instance.CurrentBattleAction;
            if (enemyAction  != null) _pinned.Remove(enemyAction.Pointer);
            if (playerAction != null) _pinned.Remove(playerAction.Pointer);
            _targets.Remove(targetSinAction.Pointer);
            _duelIntent.Remove(targetSinAction.Pointer);

            bool redirected = false;
            try
            {
                var mainTarget = enemyAction?.GetMainTarget();
                redirected = mainTarget != null && __instance.UnitModel != null
                          && mainTarget.Pointer == __instance.UnitModel.Pointer;
            }
            catch { }

            if (redirected)
            {
                _duelIntent[targetSinAction.Pointer] = (__instance, ++_actionSeq);
                TryFormDuel(targetSinAction, __instance);
            }
            try { SingletonBehavior<BattleUIRoot>.Instance?.ShowAllCharacterTargetArrows(); } catch { }
        }
        catch { }
    }

    [HarmonyPatch(typeof(BattleUnitModel), "IsTargetable")]
    [HarmonyPostfix]
    static void Unit_IsTargetable(BattleUnitModel __instance, ref bool __result)
    {
        if (__instance?.IsFaction(UNIT_FACTION.PLAYER) == true) __result = true;
    }

    [HarmonyPatch(typeof(BattleActionModelManager), nameof(BattleActionModelManager.Run),
        new Type[] { })]
    [HarmonyPrefix]
    static void ApplyTargets()
    {
        _cmdOpen = false;
        if (_targets.Count == 0) return;

        try
        {
            var sinMgr = Singleton<SinManager>.Instance;
            if (sinMgr == null) return;

            var enemies = sinMgr.GetActionListByFaction(UNIT_FACTION.ENEMY);
            if (enemies == null) return;

            var applied = new List<(SinActionModel sam, BattleActionModel action, SinActionModel targetSam)>();
            foreach (var sam in enemies)
            {
                if (sam == null) continue;
                if (!_targets.TryGetValue(sam.Pointer, out var entry)) continue;
                var (targetSam, intendedSin) = entry;
                if (targetSam == null) continue;

                BattleActionModel action = null;
                try { action = intendedSin?.GetBattleActionModel(); } catch { }
                if (action == null) action = sam._currentBattleAction;
                if (action == null) continue;

                try
                {
                    action.ChangeMainTargetSinAction(targetSam, null, true);
                    _pinned[action.Pointer] = targetSam;
                    applied.Add((sam, action, targetSam));
                }
                catch { }
            }

            var actionMgr = Singleton<BattleActionModelManager>.Instance;
            if (actionMgr == null) return;
            var dueledPlayers = new HashSet<IntPtr>();
            var dueledEnemies = new HashSet<IntPtr>();

            var sorted = new List<(SinActionModel sam, BattleActionModel action, SinActionModel duelPartner, int seq)>();
            foreach (var (sam, enemyAction, targetSam) in applied)
            {
                int seq = 0;
                SinActionModel? duelPartner = null;
                if (_duelIntent.TryGetValue(sam.Pointer, out var intent))
                {
                    duelPartner = intent.playerSam;
                    seq = intent.seq;
                }
                sorted.Add((sam, enemyAction, duelPartner!, seq));
            }
            sorted.Sort((a, b) => b.seq.CompareTo(a.seq));

            foreach (var (sam, enemyAction, duelPartner, seq) in sorted)
            {
                if (duelPartner == null) continue;
                try
                {
                    var playerAction = duelPartner.CurrentBattleAction;
                    if (playerAction == null) continue;
                    if (dueledPlayers.Contains(playerAction.Pointer)) continue;
                    if (dueledEnemies.Contains(enemyAction.Pointer)) continue;

                    try { playerAction.ChangeMainTargetSinAction(sam, enemyAction, true); } catch { }
                    try { enemyAction.ChangeMainTargetSinAction(duelPartner, playerAction, true); } catch { }
                    _pinned[enemyAction.Pointer] = duelPartner;

                    actionMgr.RemoveDuel(enemyAction);
                    actionMgr.RemoveDuel(playerAction);
                    actionMgr.AddDuel(playerAction, enemyAction);
                    _pinned[playerAction.Pointer] = sam;
                    dueledPlayers.Add(playerAction.Pointer);
                    dueledEnemies.Add(enemyAction.Pointer);
                }
                catch { }
            }
        }
        catch { }
    }

    [HarmonyPatch(typeof(BattleActionModel), nameof(BattleActionModel.RecheckTargetList))]
    [HarmonyPostfix]
    static void RecheckTarget_Postfix(BattleActionModel __instance)
    {
        if (__instance == null) return;
        try
        {
            if (!_pinned.TryGetValue(__instance.Pointer, out var pinned) || pinned == null) return;
            var unit = pinned.UnitModel;
            if (unit == null || !unit.IsTargetable(__instance.Model)) return;
            var cur = __instance.GetMainTarget();
            if (cur?.Pointer == unit.Pointer) return;
            __instance.ChangeMainTargetSinAction(pinned, null, true);
        }
        catch { }
    }

    [HarmonyPatch(typeof(BattleActionModel), nameof(BattleActionModel.ChangeMainTargetSinAction))]
    [HarmonyPrefix]
    static bool ChangeTarget_Prefix(BattleActionModel __instance,
        SinActionModel changeTargetSinAction, BattleActionModel oppositeAction, bool forcely)
    {
        if (__instance == null || forcely) return true;
        try
        {
            if (!_pinned.TryGetValue(__instance.Pointer, out var pinned) || pinned == null) return true;
            if (changeTargetSinAction?.Pointer == pinned.Pointer) return true;
            var unit = pinned.UnitModel;
            if (unit == null || !unit.IsTargetable(__instance.Model))
            { _pinned.Remove(__instance.Pointer); return true; }
            return false;
        }
        catch { return true; }
    }

    [HarmonyPatch(typeof(BattleUnitModel), "OnRoundStart_After_Event")]
    [HarmonyPostfix]
    static void AttachTargetTriggers(BattleUnitModel __instance)
    {
        if (!__instance.IsFaction(UNIT_FACTION.PLAYER)) return;
        try
        {
            var objMgr = SingletonBehavior<BattleObjectManager>.Instance;
            if (objMgr == null) return;
            var view = objMgr.GetView(__instance);
            if (view == null) return;
            var uiMgr = view.UIManager;
            if (uiMgr == null) return;
            var slots = uiMgr.UnitActionUI?._actionSlotUIList;
            if (slots == null) return;

            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot == null) continue;
                int id = ((UnityEngine.Object)(object)slot).GetInstanceID();
                if (_triggeredSlots.Contains(id)) continue;
                _triggeredSlots.Add(id);

                var capturedSlot = slot;
                capturedSlot._trigger.AttachEntry((EventTriggerType)0,
                    (Il2CppSystem.Action)((System.Action)(() =>
                    {
                        if (_drag == null) return;
                        var slotSam = capturedSlot._sinAction;
                        if (slotSam == null || IsEnemy(slotSam)) return;
                        _hoverSam = slotSam;
                    })));

                capturedSlot._trigger.AttachEntry((EventTriggerType)1,
                    (Il2CppSystem.Action)((System.Action)(() =>
                    {
                        var slotSam = capturedSlot._sinAction;
                        if (_hoverSam?.Pointer == slotSam?.Pointer) _hoverSam = null;
                    })));

                capturedSlot._trigger.AttachEntry((EventTriggerType)3,
                    (Il2CppSystem.Action)((System.Action)(() =>
                    {
                        if (_drag == null) return;
                        var slotSam = capturedSlot._sinAction;
                        if (slotSam == null || IsEnemy(slotSam)) return;
                        var drag = _drag;
                        var unitSin = _dragSin ?? (drag.currentSinList?.Count > 0 ? drag.currentSinList[0] : null);
                        _drag     = null;
                        _dragSin  = null;
                        _hoverSam = null;
                        if (unitSin != null)
                        {
                            try { drag.DeSelectSin(); } catch { }
                            try { drag.SelectSin(unitSin, slotSam); } catch { }
                            _duelIntent[drag.Pointer] = (slotSam, ++_actionSeq);
                            TryFormDuel(drag, slotSam);
                            try
                            {
                                var root = SingletonBehavior<BattleUIRoot>.Instance;
                                var ctrl = root?.NewOperationController;
                                ctrl?.EndDrag(unitSin);
                                root?.ShowAllCharacterTargetArrows();
                                ctrl?.UpdateAllSlotForNormal();
                            }
                            catch { }
                        }
                    })));
            }
        }
        catch { }
    }

    [HarmonyPatch(typeof(NewOperationController), nameof(NewOperationController.LateUpdate))]
    [HarmonyPostfix]
    static void CheckRightClickCancel()
    {
        if (_drag != null && Input.GetMouseButtonDown(1))
            CancelDrag();
    }

    [HarmonyPatch(typeof(NewOperationPortraitSlot), nameof(NewOperationPortraitSlot.OnPointerDown))]
    [HarmonyPrefix]
    static bool Portrait_PointerDown(NewOperationPortraitSlot __instance, PointerEventData eventData)
    {
        if (!_portraitSam.TryGetValue(__instance.Pointer, out var sam) || sam == null) return true;
        if (!IsEnemy(sam)) return true;

        if (ModularSkillScripts.Patches.UniquePatches.RunSpecialAction(sam)) return false;

        if (eventData.button == PointerEventData.InputButton.Left)
        {
            try
            {
                // if (!_portraitSam.TryGetValue(__instance.Pointer, out var sam) || sam == null) return true;
                // if (!IsEnemy(sam)) return true;  

                BattleUnitModel unit = sam.UnitModel;
                Il2CppSystem.Collections.Generic.List<UnitSinModel> currentSinList = sam.currentSinList;

                if (unit == null || currentSinList == null || currentSinList.Count == 0) return false;

                UnitSinModel bottomSin = currentSinList[0];
                bool alreadyIsDefense = bottomSin.GetSkill().IsDefense();
                bool isEgo = bottomSin.GetSkill().IsEgoSkill();
                bool isEgoOverclock = bottomSin.GetSkill().IsEgoOverclock();

                Il2CppSystem.Collections.Generic.List<int> defSkillIdList = unit.GetDefenseSkillIDList();

                if (defSkillIdList == null || defSkillIdList.Count == 0) return false;

                int defaultDefSkillId = defSkillIdList[0];

                if (alreadyIsDefense)
                {
                    if (_defenseSkillSwapPreserve.TryGetValue(sam.Pointer, out int preservedSkillId)) currentSinList[0] = new UnitSinModel(preservedSkillId, unit, sam);
                }
                else
                {
                    if ((!isEgo && !isEgoOverclock) || !_defenseSkillSwapPreserve.ContainsKey(sam.Pointer))
                    _defenseSkillSwapPreserve[sam.Pointer] = bottomSin.GetSkill().GetID();

                    currentSinList[0] = new UnitSinModel(defaultDefSkillId, unit, sam);
                }

                var controller = SingletonBehavior<BattleUIRoot>.Instance.NewOperationController;

                if (controller != null)
                {
                    NewOperationSinActionSlot newOperationSinActionSlot = null;

                    foreach(NewOperationSinActionSlot slot in controller._sinActionSlotList)
                    {
                        if (slot?.SinAction?.Pointer == sam.Pointer && slot.SinAction != null)
                        {
                            newOperationSinActionSlot = slot;
                            break;
                        }
                    }

                    if (newOperationSinActionSlot != null) SetEnemySinSlot(newOperationSinActionSlot._firstSinSlot, currentSinList[0]);
                    controller.UpdateAllSlotForNormal();
                }

                
            }
            catch (Exception ex)
            {
                EnemyCtrlPlugin.Logger.LogError($"Failed to toggle defense skill: {ex}");
            }
            return false;
        }

        if (eventData.button == PointerEventData.InputButton.Right)
        {
            try
            {
                // if (!_portraitSam.TryGetValue(__instance.Pointer, out var sam) || sam == null) return true;
                // if (!IsEnemy(sam)) return true;
                
                EnemyCtrlPlugin.EgoSelectionUI.Instance.OpenPanel(sam);
            }
            catch (Exception ex)
            {
                EnemyCtrlPlugin.Logger.LogError($"Failed to open custom ego ui: {ex}");
            }
            return false;
        }

        return true;
    }

    [HarmonyPatch(typeof(NewOperationSinSlot), nameof(NewOperationSinSlot.OnPointerDown))]
    [HarmonyPrefix]
    static bool SinSlot_PointerDown_Pre(NewOperationSinSlot __instance, PointerEventData eventData)
    {
        if (eventData?.button != PointerEventData.InputButton.Left) return true;

        var sam = __instance._sinActionSlot?._sinAction;
        if (!IsEnemy(sam)) return true;

        try
        {
            _drag    = sam;
            _dragSin = __instance._unitSin;

            var ctrl = SingletonBehavior<BattleUIRoot>.Instance?.NewOperationController;
            try { ctrl?.StartDragForAb(__instance); }
            catch { }
        }
        catch { }
        return false;
    }

    [HarmonyPatch(typeof(SkillModel), nameof(SkillModel.OnStartTurn_BeforeLog))]
	[HarmonyPostfix]
	static void Postfix_SkillModel_WhenUseTIMING(BattleActionModel action, List<BattleUnitModel> targets, BATTLE_EVENT_TIMING timing, SkillModel __instance)
	{ 
        var sam = action.SinAction;
        if (sam == null) return;
		if (!IsEnemy(sam)) return;

        Singleton<SinManager>.Instance._egoStockMangaer.AddSinStock(UNIT_FACTION.ENEMY, __instance.GetAttributeType(), 1, 0);
	}

    static void CancelDrag()
    {
        if (_drag == null) return;
        var unitSin = _dragSin ?? (_drag.currentSinList?.Count > 0 ? _drag.currentSinList[0] : null);
        _drag    = null;
        _dragSin = null;
        _hoverSam = null;
        try
        {
            var root = SingletonBehavior<BattleUIRoot>.Instance;
            var ctrl = root?.NewOperationController;
            if (unitSin != null) ctrl?.EndDrag(unitSin);
            root?.ShowAllCharacterTargetArrows();
            ctrl?.UpdateAllSlotForNormal();
        }
        catch { }
    }

    static void TryFormDuel(SinActionModel enemySam, SinActionModel playerSam)
    {
        try
        {
            var actionMgr   = Singleton<BattleActionModelManager>.Instance;
            if (actionMgr == null) return;
            var enemyAction  = enemySam.CurrentBattleAction;
            var playerAction = playerSam.CurrentBattleAction;
            if (enemyAction == null || playerAction == null) return;

            var playerTargetUnit = playerAction.GetMainTarget();
            var enemyUnit        = enemySam.UnitModel;
            if (playerTargetUnit == null || enemyUnit == null) return;
            if (playerTargetUnit.Pointer != enemyUnit.Pointer) return;

           
            actionMgr.RemoveDuel(enemyAction);
            actionMgr.RemoveDuel(playerAction);
            actionMgr.AddDuel(playerAction, enemyAction);
        }
        catch { }
    }

    static bool IsEnemy(SinActionModel? sam)
    {
        if (sam == null || sam.Pointer == IntPtr.Zero) return false;
        try { return sam.GetFaction() == UNIT_FACTION.ENEMY; }
        catch { return false; }
    }

    static SkillModel? SkillFor(UnitSinModel sin)
    {
        try { return sin.GetSkill(); } catch { }
        try { return sin.GetNewSkill(); } catch { }
        return null;
    }
}
