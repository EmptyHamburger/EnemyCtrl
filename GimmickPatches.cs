using System;
using System.Collections.Generic;
using System.Reflection;
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
using BattleUI.UIRoot;
using System.Data;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Unity.Collections;
using Il2CppSystem.Linq;

namespace EnvyPeccatulumPVP;
internal static class GimmickPatches
{
    // //Custom Mental Conditions
    //Clear base mental conditions
    [HarmonyPatch(typeof(BattleUnitModel), "OnRoundStart_After_Event")]
    [HarmonyPostfix]
    public static void Postfix_BattleUnitModel_OnRoundStart_After_Event(BattleUnitModel __instance)
    {
        if (!EnvyPeccatulumPVP.ConfigCustom.Active) return;
        if (EnvyPeccatulumPVP._doneMental.Contains(__instance.Pointer)) return;
        __instance._mentalDetail._mentalConditions.Clear();
        
        EnvyPeccatulumPVP._doneMental.Add(__instance.Pointer);
    }

    //Gate SP
    [HarmonyPatch(typeof(BattleUnitModel), nameof(BattleUnitModel.OnChangeMp))]
    [HarmonyPostfix]
    public static void Postfix_BattleUnitModel_OnChangeMP(int oldMp, int newMp, BattleUnitModel __instance)
    {
        if (!EnvyPeccatulumPVP.ConfigCustom.Active) return;
        if (newMp > EnvyPeccatulumPVP.ConfigCustom.GateSPMax) __instance._changeStat.SetMp(EnvyPeccatulumPVP.ConfigCustom.GateSPMax, out int _);
        if (newMp < EnvyPeccatulumPVP.ConfigCustom.GateSPMin) __instance._changeStat.SetMp(EnvyPeccatulumPVP.ConfigCustom.GateSPMin, out int _);
    }

    //OnWinDuelAsParryingCountMultiplyXAndPlusYPercent
    [HarmonyPatch(typeof(PassiveDetail), nameof(PassiveDetail.OnWinDuel))]
	[HarmonyPostfix]
	public static void Mental_Postfix_PassiveDetail_OnWinDuel(BattleActionModel selfAction, BattleActionModel oppoAction, int parryingCount, BATTLE_EVENT_TIMING timing, PassiveDetail __instance)
	{
        if (!EnvyPeccatulumPVP.ConfigCustom.Active) return;
        if (selfAction == null || selfAction.Skill == null) return;
        if (__instance._owner.GetUnitID().ToString().StartsWith("20000")) return;
        __instance._owner.HealTargetMp(__instance._owner, EnvyPeccatulumPVP.ConfigCustom.ClashWin * (1 + (parryingCount - 1) * EnvyPeccatulumPVP.ConfigCustom.ClashWinMultiplier), ABILITY_SOURCE_TYPE.UNIT, timing);
        EnvyPeccatulumPVP.Logger.LogFatal("Win Duel Self");
    }

    //OnLoseDuelAsParryingX
    [HarmonyPatch(typeof(PassiveDetail), nameof(PassiveDetail.OnLoseDuel))]
	[HarmonyPostfix]
	public static void Mental_Postfix_PassiveDetail_OnLoseDuel(BattleActionModel selfAction, BattleActionModel oppoAction, BATTLE_EVENT_TIMING timing, PassiveDetail __instance)
	{
        if (!EnvyPeccatulumPVP.ConfigCustom.Active) return;
        if (selfAction == null || selfAction.Skill == null) return;
        if (__instance._owner.GetUnitID().ToString().StartsWith("20000")) return;

        if (oppoAction._model.Mp < __instance._owner.Mp)
        {
            __instance._owner.HealTargetMp(__instance._owner, EnvyPeccatulumPVP.ConfigCustom.ClashLoseToLowerSPEnemy, ABILITY_SOURCE_TYPE.UNIT, timing);
            EnvyPeccatulumPVP.Logger.LogFatal("Lose Duel LowerSP Self");
        }

        __instance._owner.HealTargetMp(__instance._owner, EnvyPeccatulumPVP.ConfigCustom.ClashLose, ABILITY_SOURCE_TYPE.UNIT, timing);

        EnvyPeccatulumPVP.Logger.LogFatal("Lose Duel Self");
    }

    //OnKillEnemyMpX + OnAllyKillEnemy
    [HarmonyPatch(typeof(PassiveDetail), nameof(PassiveDetail.OnKillTarget))]
	[HarmonyPostfix]
	public static void Mental_Postfix_PassiveDetail_OnKillTarget(BattleActionModel actionOrNull, BattleUnitModel target, DAMAGE_SOURCE_TYPE dmgSrcType, BATTLE_EVENT_TIMING timing, PassiveDetail __instance)
	{
        if (!EnvyPeccatulumPVP.ConfigCustom.Active) return;
        if (actionOrNull == null || actionOrNull.Skill == null) return;
        if (__instance._owner.GetUnitID().ToString().StartsWith("20000")) return;
        __instance._owner.HealTargetMp(__instance._owner, EnvyPeccatulumPVP.ConfigCustom.EnemyKilled, ABILITY_SOURCE_TYPE.UNIT, timing);
        EnvyPeccatulumPVP.Logger.LogFatal("Kill Enemy Self");

        BattleObjectManager _battleobjectmanager = SingletonBehavior<BattleObjectManager>.Instance;
        var aliveList = _battleobjectmanager.GetAliveUnitModels(false, __instance._owner._faction);

        for (int i = 0; i < aliveList.Count(); i++)
        {
            if (aliveList.ElementAt(i) == __instance._owner || aliveList.ElementAt(i).GetUnitID().ToString().StartsWith("20000")) continue;
            aliveList.ElementAt(i).HealTargetMp(aliveList.ElementAt(i), EnvyPeccatulumPVP.ConfigCustom.EnemyKilledByAlly, ABILITY_SOURCE_TYPE.UNIT, timing);
            EnvyPeccatulumPVP.Logger.LogFatal("Kill Enemy Ally");
        }
    }

    //OnDieAlly
    [HarmonyPatch(typeof(PassiveDetail), nameof(PassiveDetail.OnDie))]
	[HarmonyPostfix]
	public static void Mental_Postfix_PassiveDetail_OnDie(BattleUnitModel killer, BattleActionModel actionOrNull, DAMAGE_SOURCE_TYPE dmgSrcType, BUFF_UNIQUE_KEYWORD keyword, BATTLE_EVENT_TIMING timing, PassiveDetail __instance)
	{
        if (!EnvyPeccatulumPVP.ConfigCustom.Active) return;
        if (__instance._owner.GetUnitID().ToString().StartsWith("20000")) return;
        BattleObjectManager _battleobjectmanager = SingletonBehavior<BattleObjectManager>.Instance;
        var aliveList = _battleobjectmanager.GetAliveUnitModels(false, __instance._owner._faction);

        for (int i = 0; i < aliveList.Count(); i++)
        {
            if (aliveList.ElementAt(i).GetUnitID().ToString().StartsWith("20000")) continue;
            aliveList.ElementAt(i).HealTargetMp(aliveList.ElementAt(i), EnvyPeccatulumPVP.ConfigCustom.AllyKilled, ABILITY_SOURCE_TYPE.UNIT, timing);
            EnvyPeccatulumPVP.Logger.LogFatal("On Die Ally");
        }
    }

    // Sub Units SP Gain
    [HarmonyPatch(typeof(BattleUnitModel), nameof(BattleUnitModel.OnAddUnit_After))]
	[HarmonyPostfix]
	public static void Mental_Postfix_PassiveDetail_OnAddUnit_After(BATTLE_EVENT_TIMING timing, BattleUnitModel __instance)
	{
        if (!EnvyPeccatulumPVP.ConfigCustom.Active) return;
        EnvyPeccatulumPVP._pendingSubInstID.Add(__instance.InstanceID);
    }

    [HarmonyPatch(typeof(StageController), nameof(StageController.FixedUpdate))]
    [HarmonyPrefix]
    public static void Prefix_StageController_FixedUpdate(StageController __instance)
    {
        if (!EnvyPeccatulumPVP.ConfigCustom.Active) return;
        switch (__instance._phase)
        {
            case STAGE_PHASE.WAIT_COMMAND_BEFORE:
                foreach(int instID in EnvyPeccatulumPVP._pendingSubInstID)
                {
                    BattleUnitModel unit = SingletonBehavior<BattleObjectManager>.Instance.GetModel(instID);
                    if (EnvyPeccatulumPVP.SpGainedUnitIds.Contains(unit.GetUnitID())) continue;
                    if (unit.IsAbnormalityOrPart)
                    {
                        unit._changeStat.SetMp(EnvyPeccatulumPVP.ConfigCustom.SpValue[EnvyPeccatulumPVP._currentEnvyPeccIndex - 1], out int _);
                        EnvyPeccatulumPVP._currentEnvyPeccIndex++;
                    }
                    else unit._changeStat.SetMp(EnvyPeccatulumPVP.ConfigCustom.SpValue[unit._participateOrder - 1], out int _);

                    EnvyPeccatulumPVP.SpGainedUnitIds.Add(unit.GetUnitID());
                }
                EnvyPeccatulumPVP._pendingSubInstID.Clear();
                return;
            default: return;
        }
    }

    [HarmonyPatch(typeof(SkillModel), nameof(SkillModel.OnStartTurn_AfterLog))]
	[HarmonyPostfix]
    public static void Postfix_SkillModel_OnStartTurn_AfterLog(BattleActionModel action, Il2CppSystem.Collections.Generic.List<BattleUnitModel> targets, BATTLE_EVENT_TIMING timing, SkillModel __instance)
    {
        EnvyPeccatulumPVP._unopposedDict[__instance.Pointer] = -1; //-1
    }

    [HarmonyPatch(typeof(SkillModel), nameof(SkillModel.OnStartDuel))]
	[HarmonyPostfix]
    public static void Postfix_SkillModel_OnStartDuel(BattleActionModel selfAction, BattleActionModel oppoAction, BATTLE_EVENT_TIMING timing, SkillModel __instance)
    {
        EnvyPeccatulumPVP._unopposedDict[__instance.Pointer] = 0; //0
    }

    //Potential to be better
    // [HarmonyPatch(typeof(BattleActionModel), nameof(BattleActionModel.OnAttackConfirmed))]
	// [HarmonyPostfix]
	// public static void Postfix_BattleActionModel_OnAttackConfirmed(CoinModel coin, BattleUnitModel target, BATTLE_EVENT_TIMING timing, bool isCritical, BattleActionModel __instance)
	// {
    //     if (__instance._model.GetUnitID().ToString().StartsWith("20000")) return;
    //     if (EnvyPeccatulumPVP._unopposedDict[__instance.Skill.Pointer] != -1) return;
        
    //     EnvyPeccatulumPVP._unopposedDict[__instance.Skill.Pointer] = 1;
    // }

    //This worked
    [HarmonyPatch(typeof(BattleUnitModel), nameof(BattleUnitModel.OnTakeAttackDamage))]
	[HarmonyPostfix]
	public static void Postfix_BattleUnitModel_OnTakeAttackDamage(BattleActionModel action, CoinModel coin, int realDmg, int hpDamage, BATTLE_EVENT_TIMING timing, bool isCritical, BattleUnitModel __instance)
	{
        if (__instance.TryCast<BattleUnitModel_Abnormality>() != null) return;
        if (__instance.IsDead()) return;
        if (action._model.GetUnitID().ToString().StartsWith("20000")) return;
        if (EnvyPeccatulumPVP._unopposedDict[action.Skill.Pointer] != -1) return;
        
        EnvyPeccatulumPVP._unopposedDict[action.Skill.Pointer] = 1;
    }

    [HarmonyPatch(typeof(SkillModel), nameof(SkillModel.OnEndBehaviour))]
	[HarmonyPostfix]
    public static void Postfix_SkillModel_OnEndBehaviour(BattleActionModel action, BATTLE_EVENT_TIMING timing, SkillModel __instance)
    {
        if (EnvyPeccatulumPVP._unopposedDict[__instance.Pointer] != 1) return;
        action._model.HealTargetMp(action._model, EnvyPeccatulumPVP.ConfigCustom.Unopposed, ABILITY_SOURCE_TYPE.UNIT, timing);
        EnvyPeccatulumPVP.Logger.LogFatal("Unopposed");
    }
    
    //The House of Spiders: The Thumb Nursefather Rodion
    [HarmonyPatch(typeof(PassiveDetail), nameof(PassiveDetail.OnKillTarget))]
	[HarmonyPostfix]
	public static void Postfix_BattleUnitModel_OnKillTarget(BattleActionModel actionOrNull, BattleUnitModel target, DAMAGE_SOURCE_TYPE dmgSrcType, BATTLE_EVENT_TIMING timing)
	{
        if (actionOrNull == null || actionOrNull.Skill == null) return;

        if (actionOrNull.Model.GetUnitID() == 2010010916 &&
        actionOrNull.GetSkillID() == 1091607 && 
        target._buffDetail.GetActivatedBuffStack(BUFF_UNIQUE_KEYWORD.TeachersPreyRodion, false) != 0 &&
        !EnvyPeccatulumPVP.TheHouseOfSpidersTheThumbNursefatherRodion_IsAddedDisposal)
        EnvyPeccatulumPVP.TheHouseOfSpidersTheThumbNursefatherRodion_DisposalKillCheck = true;
    }

    [HarmonyPatch(typeof(BattleUnitModel), nameof(BattleUnitModel.RightAfterLosingBuff))]
    [HarmonyPostfix]
    public static void Postfix_BattleUnitModel_RightAfterLosingBuff(int loseStack, int loseTurn, BuffInfo loseBuffInfo, BATTLE_EVENT_TIMING timing, BattleUnitModel __instance)
    {
        if (__instance.GetUnitID() == 2010010916 && loseBuffInfo._mainKeyword == BUFF_UNIQUE_KEYWORD.FutureEyeOnRodion && loseBuffInfo._stack == 0)
        EnvyPeccatulumPVP.TheHouseOfSpidersTheThumbNursefatherRodion_CheckEye = true;
    }

    [HarmonyPatch(typeof(PassiveDetail), nameof(PassiveDetail.OnKillTarget))]
	[HarmonyPostfix]
	public static void Postfix_BattleUnitModel_OnKillTargetOnKillTarget(BattleActionModel actionOrNull, BattleUnitModel target, DAMAGE_SOURCE_TYPE dmgSrcType, BATTLE_EVENT_TIMING timing)
	{
        if (actionOrNull == null || actionOrNull.Skill == null) return;

        if (actionOrNull.Model.GetUnitID() == 2010010916 &&
        actionOrNull.GetSkillID() == 1091607 && 
        target._buffDetail.GetActivatedBuffStack(BUFF_UNIQUE_KEYWORD.TeachersPreyRodion, false) != 0 &&
        !EnvyPeccatulumPVP.TheHouseOfSpidersTheThumbNursefatherRodion_IsAddedDisposal)
        EnvyPeccatulumPVP.TheHouseOfSpidersTheThumbNursefatherRodion_DisposalKillCheck = true;
    }

    //The Index
    // [HarmonyPatch(typeof(PassiveDetail), nameof(PassiveDetail.OnStartTurn_BeforeLog))]
	// [HarmonyPostfix]
	// public static void Postfix_PassiveDetail_OnStartTurn_BeforeLog(BattleActionModel action, BATTLE_EVENT_TIMING timing, PassiveDetail __instance)
	// {
    //     if (action == null || action.Skill == null) return;
    //     SinActionModel sam = action._sinAction;
    //     NewOperationController ctrl = SingletonBehavior<BattleUIRoot>.Instance.NewOperationController;
    //     NewOperationSinActionSlot nosas = ctrl.GetSinActionSlot(action._sinAction);
    //     NewOperationSinSlot? noss = null;
    //     if (nosas.FirstSinSlot.SinAction == sam) noss = nosas.FirstSinSlot;
    //     if (nosas.SecondSinSlot.SinAction == sam) noss = nosas.SecondSinSlot;
    //     switch (action.Model.GetUnitID())
    //     {
    //         case 2010010115: //The House of Spiders: The Index Nursefather Yi Sang
    //             switch (EnvyPeccatulumPVP.TheHouseOfSpidersTheIndexNursefatherYiSang_CurrentUnlockRank)
    //             {
    //                 case 0:
    //                     if (action.GetMainTarget()._buffDetail.GetActivatedBuffStack(BUFF_UNIQUE_KEYWORD.IndexPrescriptTargetToEnemy, false) != 0)
    //                     {
    //                         EnvyPeccatulumPVP.TheHouseOfSpidersTheIndexNursefatherYiSang_PrescriptExecuted = true;
    //                         EnvyPeccatulumPVP.TheHouseOfSpidersTheIndexNursefatherYiSang_ExecutedOnPrescriptTarget = true;
    //                         return;
    //                     }
    //                     if (noss == null) return;

    //                     if (noss._effectManager._skillEffectList.ContainsKey(OPERATION_SKILL_EFFECT_TYPE.INDEX_FINGER))
    //                     EnvyPeccatulumPVP.TheHouseOfSpidersTheIndexNursefatherYiSang_PrescriptExecuted = true;

    //                     return;
    //                 case 1:
    //                     if (noss == null) return;

    //                     if (action.GetMainTarget()._buffDetail.GetActivatedBuffStack(BUFF_UNIQUE_KEYWORD.IndexPrescriptTargetToEnemy, false) != 0)
    //                     EnvyPeccatulumPVP.TheHouseOfSpidersTheIndexNursefatherYiSang_ExecutedOnPrescriptTarget = true;

    //                     if (noss._effectManager._skillEffectList.ContainsKey(OPERATION_SKILL_EFFECT_TYPE.INDEX_FINGER))
    //                     EnvyPeccatulumPVP.TheHouseOfSpidersTheIndexNursefatherYiSang_PrescriptExecuted = true;

    //                     return;
    //                 default: return;
    //             }
    //         default: return;
    //     }
	// }

    // public static List<BUFF_UNIQUE_KEYWORD> prescriptList = new List<BUFF_UNIQUE_KEYWORD> {BUFF_UNIQUE_KEYWORD.IndexPrescriptYi_1, BUFF_UNIQUE_KEYWORD.IndexPrescriptYi_2, BUFF_UNIQUE_KEYWORD.IndexPrescriptYi_3};
    // //The House of Spiders: The Index Nursefather Yi Sang
    // [HarmonyPatch(typeof(PassiveDetail), nameof(PassiveDetail.OnBattleEnd))]
	// [HarmonyPrefix]
	// public static void Prefix_PassiveDetail_OnBattleEnd(BATTLE_EVENT_TIMING timing, PassiveDetail __instance)
	// {
    //     BattleUnitModel unit = __instance._owner;
    //     if (unit.GetUnitID() == 2010010115)
    //     {
    //         bool hasMaxProcuration = unit._buffDetail.GetActivatedBuffStack(BUFF_UNIQUE_KEYWORD.StackYisangSpecialSkill, false) == 9;
            
    //         if (EnvyPeccatulumPVP.TheHouseOfSpidersTheIndexNursefatherYiSang_PrescriptExecuted)
    //         {
    //             unit._buffDetail.LoseBuffStack(unit, BUFF_UNIQUE_KEYWORD.KarmaOfIndexRien, 5, 0, timing);
    //             Il2CppSystem.Collections.Generic.List<BuffHistory> buffHistories = new();

    //             if (EnvyPeccatulumPVP.TheHouseOfSpidersTheIndexNursefatherYiSang_CurrentUnlockRank < 2)
    //             {
    //                 if (EnvyPeccatulumPVP.TheHouseOfSpidersTheIndexNursefatherYiSang_ExecutedOnPrescriptTarget)
    //                 {
    //                     unit.HealTargetMp(unit, 8, ABILITY_SOURCE_TYPE.PASSIVE, timing);
    //                     buffHistories.Add(new BuffHistory(unit, 3, 0, ABILITY_SOURCE_TYPE.PASSIVE));
    //                     unit._buffDetail.AddBuff(unit, BUFF_UNIQUE_KEYWORD.BlessingOfIndexPrescriptAlly, buffHistories, 0, ABILITY_SOURCE_TYPE.PASSIVE, timing, null, out _, out _, out _, out _);
    //                 }
    //                 else
    //                 {
    //                     unit.HealTargetMp(unit, 4, ABILITY_SOURCE_TYPE.PASSIVE, timing);
    //                     buffHistories.Add(new BuffHistory(unit, 1, 0, ABILITY_SOURCE_TYPE.PASSIVE));
    //                     unit._buffDetail.AddBuff(unit, BUFF_UNIQUE_KEYWORD.BlessingOfIndexPrescriptAlly, buffHistories, 0, ABILITY_SOURCE_TYPE.PASSIVE, timing, null, out _, out _, out _, out _);
    //                 }
    //             }
    //             else if (EnvyPeccatulumPVP.TheHouseOfSpidersTheIndexNursefatherYiSang_CurrentUnlockRank == 2)
    //             {
    //                 if (hasMaxProcuration)
    //                 {
    //                     unit.HealTargetMp(unit, 8, ABILITY_SOURCE_TYPE.PASSIVE, timing);
    //                     buffHistories.Add(new BuffHistory(unit, 3, 0, ABILITY_SOURCE_TYPE.PASSIVE));
    //                     unit._buffDetail.AddBuff(unit, BUFF_UNIQUE_KEYWORD.BlessingOfIndexPrescriptAlly, buffHistories, 0, ABILITY_SOURCE_TYPE.PASSIVE, timing, null, out _, out _, out _, out _);
    //                 }
    //                 else
    //                 {
    //                     unit.HealTargetMp(unit, 4, ABILITY_SOURCE_TYPE.PASSIVE, timing);
    //                 }
    //             }
                
    //             int currentGraceOfPrescipt = unit._buffDetail.GetActivatedBuffStack(BUFF_UNIQUE_KEYWORD.BlessingOfIndexPrescriptAlly, false);
    //             if (currentGraceOfPrescipt == 9)
    //             {
    //                 EnvyPeccatulumPVP.TheHouseOfSpidersTheIndexNursefatherYiSang_CurrentUnlockRank = 3;
    //             }
    //             else if (currentGraceOfPrescipt >= 6)
    //             {
    //                 EnvyPeccatulumPVP.TheHouseOfSpidersTheIndexNursefatherYiSang_CurrentUnlockRank = 2;
    //             }
    //             else if (currentGraceOfPrescipt >= 3)
    //             {
    //                 EnvyPeccatulumPVP.TheHouseOfSpidersTheIndexNursefatherYiSang_CurrentUnlockRank = 1;
    //             }
    //         }
    //     }
	// }

    // [HarmonyPatch(typeof(PassiveDetail), nameof(PassiveDetail.OnBattleEnd))]
	// [HarmonyPostfix]
	// public static void Postfix_PassiveDetail_OnBattleEnd(BATTLE_EVENT_TIMING timing, PassiveDetail __instance)
    // {
    //     BattleUnitModel unit = __instance._owner;
    //     if (unit.GetUnitID() == 2010010115)
    //     {
    //         if (EnvyPeccatulumPVP.TheHouseOfSpidersTheIndexNursefatherYiSang_PrescriptExecuted)
    //         {
    //             unit._buffDetail.LoseBuffStack(unit, BUFF_UNIQUE_KEYWORD.KarmaOfIndexRien, 5, 0, timing);
    //             EnvyPeccatulumPVP.TheHouseOfSpidersTheIndexNursefatherYiSang_PrescriptExecuted = false;
    //             EnvyPeccatulumPVP.TheHouseOfSpidersTheIndexNursefatherYiSang_ExecutedOnPrescriptTarget = false;
    //         }
    //     }
    // }
}