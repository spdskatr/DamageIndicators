using Verse;
using UnityEngine;
using RimWorld;
using System;
using Harmony;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Linq;

namespace DamageMotes
{
    [StaticConstructorOnStartup, HarmonyPatch(typeof(Thing), nameof(Thing.TakeDamage))]
    public static class DamageMotes_Patch
    {
        static DamageMotes_Patch()
        {
            HarmonyInstance.Create("com.spdskatr.DamageMotes.Patch").PatchAll(System.Reflection.Assembly.GetExecutingAssembly());
            Log.Message("SS Damage Motes initialized.\n " + 
                "Patched pre + postfix non-destructive: " + typeof(DamageWorker).FullName + "." + nameof(DamageWorker.Apply) + 
                "\n Patched Postfix non-destructive: " + typeof(ShieldBelt) + "." + nameof(ShieldBelt.CheckPreAbsorbDamage));
        }
        [HarmonyPriority(600)]
        static void Prefix(Thing __instance, DamageInfo dinfo)
        {
            var val = Mathf.Min(dinfo.Amount, __instance.HitPoints);
            if (__instance is Pawn)
            {
                val = dinfo.Amount;
            }
            if (val > 0.01f && __instance.Map != null && __instance.ShouldDisplayDamage(dinfo.Instigator)) ThrowDamageMote(val, __instance.Map, __instance.DrawPos, val.ToString());
        }
        public static void ThrowDamageMote(float damage, Map map, Vector3 loc, string text)
        {
            Color color = Color.white;
            //Determine colour
            if (damage >= 90f)
                color = Color.cyan;
            else if (damage >= 70f)
                color = Color.magenta;
            else if (damage >= 50f)
                color = Color.red;
            else if (damage >= 30f)
                color = Color.Lerp(Color.red, Color.yellow, 0.5f);//orange
            else if (damage >= 10f)
                color = Color.yellow;

            MoteMaker.ThrowText(loc, map, text, color, 3.65f);
        }
    }
    [HarmonyPatch(typeof(ShieldBelt), nameof(ShieldBelt.CheckPreAbsorbDamage))]
    public static class ShieldBelt_Patch
    {
        static void Postfix(DamageInfo dinfo, bool __result, ShieldBelt __instance)
        {
            if (__result && __instance.Wearer != null && __instance.Wearer.Map != null)
            {
                if (dinfo.Def != DamageDefOf.EMP)
                {
                    var amount = dinfo.Amount * Traverse.Create(__instance).Field("EnergyLossPerDamage").GetValue<float>() * 100;
                    MoteMaker.ThrowText(__instance.Wearer.DrawPos, __instance.Wearer.Map, ShieldBeltOutputString(__instance, amount), 3.65f);
                }
                if (__instance.ShieldState == ShieldState.Resetting)
                {
                    MoteMaker.ThrowText(__instance.Wearer.DrawPos, __instance.Wearer.Map, "PERSONALSHIELD_BROKEN".Translate(), 3.65f);
                }
            }
        }
        public static string ShieldBeltOutputString(Thing __instance, float amount)
        {
            return "(- " + amount.ToString("F0") + "/ " + (__instance.GetStatValue(StatDefOf.EnergyShieldEnergyMax, true) * 100) + ")";
        }
    }
    [HarmonyPatch(typeof(Verb_MeleeAttack), "TryCastShot")]
    public static class Verb_MeleeAttack_Patch
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var instrList = instructions.ToList();
            for (int i = 0; i < instrList.Count; i++)
            {
                var instr = instrList[i];
                if (instr.opcode == OpCodes.Bge_Un && instrList[i-1].operand == typeof(Verb_MeleeAttack).GetMethod("GetNonMissChance", AccessTools.all))
                {
                    yield return new CodeInstruction(OpCodes.Clt_Un);
                    yield return new CodeInstruction(OpCodes.Ldloc_2);
                    yield return new CodeInstruction(OpCodes.Call, typeof(DamageMotesUtil).GetMethod(nameof(DamageMotesUtil.TranspilerUtility_NotifyMiss), AccessTools.all));
                    yield return new CodeInstruction(OpCodes.Brfalse, instr.operand);
                    continue;
                }
                yield return instr;
            }
        }
    }
    static class DamageMotesUtil
    {
        /// <summary>
        /// Used on both the instigator and the target.
        /// </summary>
        internal static bool ShouldDisplayDamage(this Thing t, Thing instigator = null)
        {
            return (LoadedModManager.GetMod<DMMod>().settings.EnableIndicatorNeutralFaction || t?.Faction != null) || (instigator?.ShouldDisplayDamage() ?? false);
        }
        public static bool TranspilerUtility_NotifyMiss(bool b, Thing t)
        {
            if (!b && t.Map != null)
                MoteMaker.ThrowText(t.DrawPos, t.Map, "DM_MISS".Translate(), 3.65f);
            return b;
        }
    }
}
