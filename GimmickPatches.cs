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
using EnvyPeccatulumPVP;

namespace EnvyPeccatulumPVP.Patches;
internal static class GimmickPatches
{
    // //Custom Mental Conditions
    // public static List<IntPtr> doneMental = new();
    // //Clear 
    // [HarmonyPatch(typeof(BattleUnitModel), "OnRoundStart_After_Event")]
    // [HarmonyPostfix]
    // public static void Postfix_BattleUnitModel_OnRoundStart_After_Event(BattleUnitModel __instance)
    // {
    //     if (doneMental.Contains(__instance.Pointer)) return;
    //     __instance._mentalDetail._mentalConditions.Clear();
    //     doneMental.Add(__instance.Pointer);
    // }

    // //Gate SP
    // [HarmonyPatch(typeof(BattleUnitModel), nameof(BattleUnitModel.OnChangeMp))]
    // [HarmonyPostfix]
    // public static void Postfix_BattleUnitModel_OnChangeMP(int oldMp, int newMp, BattleUnitModel __instance)
    // {
    //     if (newMp > 20) __instance._changeStat.SetMp(20, out int _);
    //     if (newMp < -15) __instance._changeStat.SetMp(-15, out int _);
    // }

    // //OnKillEnemyMp5
    // [HarmonyPatch(typeof(PassiveDetail), nameof(PassiveDetail.OnKillTarget))]
	// [HarmonyPostfix]
	// public static void Mental_Postfix_BattleUnitModel_OnKillTarget(BattleActionModel actionOrNull, BattleUnitModel target, DAMAGE_SOURCE_TYPE dmgSrcType, BATTLE_EVENT_TIMING timing, PassiveDetail __instance)
	// {
    //     __instance._owner.HealTargetMp(__instance._owner, 5, ABILITY_SOURCE_TYPE.UNIT, timing);
    // }

    // //OnWinDuelAsParryingCountMultiply5AndPlus20Percent
    // [HarmonyPatch(typeof(PassiveDetail), nameof(PassiveDetail.OnWinDuel))]
	// [HarmonyPostfix]
	// public static void Mental_Postfix_PassiveDetail_OnWinDuel(BattleActionModel selfAction, BattleActionModel oppoAction, int parryingCount, BATTLE_EVENT_TIMING timing, PassiveDetail __instance)
	// {
    //     __instance._owner.HealTargetMp(__instance._owner, 5 + (parryingCount - 1), ABILITY_SOURCE_TYPE.UNIT, timing);
    // }
    
    // //OnLoseDuelAsParrying3
    // [HarmonyPatch(typeof(PassiveDetail), nameof(PassiveDetail.OnLoseDuel))]
	// [HarmonyPostfix]
	// public static void Mental_Postfix_PassiveDetail_OnLoseDuel(BattleActionModel selfAction, BattleActionModel oppoAction, BATTLE_EVENT_TIMING timing, PassiveDetail __instance)
	// {
    //     __instance._owner.HealTargetMp(__instance._owner, -3, ABILITY_SOURCE_TYPE.UNIT, timing);
    // }    
    // Sub Units SP Gain
    [HarmonyPatch(typeof(PassiveDetail), nameof(PassiveDetail.OnAddUnit_After))]
	[HarmonyPostfix]
	public static void Mental_Postfix_PassiveDetail_OnAddUnit_After(BATTLE_EVENT_TIMING timing, PassiveDetail __instance)
	{
        if (__instance._owner.IsAbnormalityOrPart)
        {
            __instance._owner._changeStat.SetMp(EnvyPeccatulumPVP.SpBackUp.SpValue[EnvyPeccatulumPVP._currentEnvyPeccIndex - 1], out int _);
            EnvyPeccatulumPVP._currentEnvyPeccIndex++;
        }
        else __instance._owner._changeStat.SetMp(EnvyPeccatulumPVP.SpBackUp.SpValue[__instance._owner._participateOrder - 1], out int _);
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

    public static List<BUFF_UNIQUE_KEYWORD> prescriptList = new List<BUFF_UNIQUE_KEYWORD> {BUFF_UNIQUE_KEYWORD.IndexPrescriptYi_1, BUFF_UNIQUE_KEYWORD.IndexPrescriptYi_2, BUFF_UNIQUE_KEYWORD.IndexPrescriptYi_3};
    //The House of Spiders: The Index Nursefather Yi Sang
    [HarmonyPatch(typeof(PassiveDetail), nameof(PassiveDetail.OnBattleEnd))]
	[HarmonyPrefix]
	public static void Prefix_PassiveDetail_OnBattleEnd(BATTLE_EVENT_TIMING timing, PassiveDetail __instance)
	{
        BattleUnitModel unit = __instance._owner;
        if (unit.GetUnitID() == 2010010115)
        {
            bool hasMaxProcuration = unit._buffDetail.GetActivatedBuffStack(BUFF_UNIQUE_KEYWORD.StackYisangSpecialSkill, false) == 9;
            
            if (EnvyPeccatulumPVP.TheHouseOfSpidersTheIndexNursefatherYiSang_PrescriptExecuted)
            {
                unit._buffDetail.LoseBuffStack(unit, BUFF_UNIQUE_KEYWORD.KarmaOfIndexRien, 5, 0, timing);
                Il2CppSystem.Collections.Generic.List<BuffHistory> buffHistories = new();

                if (EnvyPeccatulumPVP.TheHouseOfSpidersTheIndexNursefatherYiSang_CurrentUnlockRank < 2)
                {
                    if (EnvyPeccatulumPVP.TheHouseOfSpidersTheIndexNursefatherYiSang_ExecutedOnPrescriptTarget)
                    {
                        unit.HealTargetMp(unit, 8, ABILITY_SOURCE_TYPE.PASSIVE, timing);
                        buffHistories.Add(new BuffHistory(unit, 3, 0, ABILITY_SOURCE_TYPE.PASSIVE));
                        unit._buffDetail.AddBuff(unit, BUFF_UNIQUE_KEYWORD.BlessingOfIndexPrescriptAlly, buffHistories, 0, ABILITY_SOURCE_TYPE.PASSIVE, timing, null, out _, out _, out _, out _);
                    }
                    else
                    {
                        unit.HealTargetMp(unit, 4, ABILITY_SOURCE_TYPE.PASSIVE, timing);
                        buffHistories.Add(new BuffHistory(unit, 1, 0, ABILITY_SOURCE_TYPE.PASSIVE));
                        unit._buffDetail.AddBuff(unit, BUFF_UNIQUE_KEYWORD.BlessingOfIndexPrescriptAlly, buffHistories, 0, ABILITY_SOURCE_TYPE.PASSIVE, timing, null, out _, out _, out _, out _);
                    }
                }
                else if (EnvyPeccatulumPVP.TheHouseOfSpidersTheIndexNursefatherYiSang_CurrentUnlockRank == 2)
                {
                    if (hasMaxProcuration)
                    {
                        unit.HealTargetMp(unit, 8, ABILITY_SOURCE_TYPE.PASSIVE, timing);
                        buffHistories.Add(new BuffHistory(unit, 3, 0, ABILITY_SOURCE_TYPE.PASSIVE));
                        unit._buffDetail.AddBuff(unit, BUFF_UNIQUE_KEYWORD.BlessingOfIndexPrescriptAlly, buffHistories, 0, ABILITY_SOURCE_TYPE.PASSIVE, timing, null, out _, out _, out _, out _);
                    }
                    else
                    {
                        unit.HealTargetMp(unit, 4, ABILITY_SOURCE_TYPE.PASSIVE, timing);
                    }
                }
                
                int currentGraceOfPrescipt = unit._buffDetail.GetActivatedBuffStack(BUFF_UNIQUE_KEYWORD.BlessingOfIndexPrescriptAlly, false);
                if (currentGraceOfPrescipt == 9)
                {
                    EnvyPeccatulumPVP.TheHouseOfSpidersTheIndexNursefatherYiSang_CurrentUnlockRank = 3;
                }
                else if (currentGraceOfPrescipt >= 6)
                {
                    EnvyPeccatulumPVP.TheHouseOfSpidersTheIndexNursefatherYiSang_CurrentUnlockRank = 2;
                }
                else if (currentGraceOfPrescipt >= 3)
                {
                    EnvyPeccatulumPVP.TheHouseOfSpidersTheIndexNursefatherYiSang_CurrentUnlockRank = 1;
                }
            }
        }
	}

    [HarmonyPatch(typeof(PassiveDetail), nameof(PassiveDetail.OnBattleEnd))]
	[HarmonyPostfix]
	public static void Postfix_PassiveDetail_OnBattleEnd(BATTLE_EVENT_TIMING timing, PassiveDetail __instance)
    {
        BattleUnitModel unit = __instance._owner;
        if (unit.GetUnitID() == 2010010115)
        {
            if (EnvyPeccatulumPVP.TheHouseOfSpidersTheIndexNursefatherYiSang_PrescriptExecuted)
            {
                unit._buffDetail.LoseBuffStack(unit, BUFF_UNIQUE_KEYWORD.KarmaOfIndexRien, 5, 0, timing);
                EnvyPeccatulumPVP.TheHouseOfSpidersTheIndexNursefatherYiSang_PrescriptExecuted = false;
                EnvyPeccatulumPVP.TheHouseOfSpidersTheIndexNursefatherYiSang_ExecutedOnPrescriptTarget = false;
            }
        }
    }
}