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

namespace EnvyPeccatulumPVP.Patches;
internal static class GimmickPatches
{
    //The House of Spiders: The Thumb Nursefather Rodion
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
}