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

using Il2CppList = Il2CppSystem.Collections.Generic.List<SinActionModel>;

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
    public override void Load()
    {
        new Harmony(PluginInfo.PLUGIN_GUID).PatchAll(typeof(EnemyCtrlPatches));
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
        _drag     = null;
        _dragSin  = null;
        _hoverSam = null;
    }

    [HarmonyPatch(typeof(StageModel), nameof(StageModel.Init))]
    [HarmonyPrefix]
    static void OnStageInit()
    {
        _injectedEnemies.Clear();
        _targets.Clear();
        _duelIntent.Clear();
        _actionSeq = 0;
        _drag          = null;
        _dragSin       = null;
        _hoverSam      = null;
        _triggeredSlots.Clear();
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
        __result = false;
        return false;
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

            var sinList = sinAction.currentSinList;
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

    static void SetEnemySinSlot(NewOperationSinSlot? slot, UnitSinModel? sin)
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
                var ctrl = SingletonBehavior<BattleUIRoot>.Instance?.NewOperationController;
                ctrl?.UpdateAllSlotForNormal();
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

    [HarmonyPatch(typeof(SinActionModel), nameof(SinActionModel.OnTargetedAsMain))]
    [HarmonyPostfix]
    static void TrackSinnerTargetsEnemy(SinActionModel __instance, BattleActionModel otherAction)
    {
        if (!_cmdOpen || otherAction == null) return;
        if (!IsEnemy(__instance)) return;
        try
        {
            var sinnerSam = otherAction.SinAction;
            if (sinnerSam == null || IsEnemy(sinnerSam)) return;
            _duelIntent[__instance.Pointer] = (sinnerSam, ++_actionSeq);
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

            var sorted = new List<(SinActionModel sam, BattleActionModel action, SinActionModel targetSam, int seq)>();
            foreach (var (sam, enemyAction, targetSam) in applied)
            {
                int seq = 0;
                SinActionModel duelPartner = targetSam;
                if (_duelIntent.TryGetValue(sam.Pointer, out var intent))
                {
                    duelPartner = intent.playerSam;
                    seq = intent.seq;
                }
                sorted.Add((sam, enemyAction, duelPartner, seq));
            }
            sorted.Sort((a, b) => b.seq.CompareTo(a.seq));

            foreach (var (sam, enemyAction, duelPartner, seq) in sorted)
            {
                try
                {
                    var playerAction = duelPartner.CurrentBattleAction;
                    if (playerAction == null) continue;
                    if (dueledPlayers.Contains(playerAction.Pointer)) continue;
                    if (dueledEnemies.Contains(enemyAction.Pointer)) continue;

                    if (!BattleActionModel.CanDuelBoth(enemyAction, playerAction)) continue;

                    actionMgr.RemoveDuel(enemyAction);
                    actionMgr.RemoveDuel(playerAction);
                    actionMgr.AddDuel(playerAction, enemyAction);
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

            if (!BattleActionModel.CanDuelBoth(enemyAction, playerAction)) return;

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
