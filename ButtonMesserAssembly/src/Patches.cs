﻿using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using log4net;
using HarmonyLib;

public static class Patcher
{
    private static bool _patched = false;

    public static qkButtonMesser GetMeser(BombComponent __instance)
    {
        if (__instance == null) return null;
        var messers = __instance.transform.parent.GetComponentsInChildren<qkButtonMesser>(true);
        if (messers.Length > 0) return messers.OrderByDescending(x => x.moduleID).ToArray()[0];
        return null;
    }

    public static Selectable GetParent(Selectable parented)
    {
        while (parented.Parent != null && parented.Parent.GetComponent<BombComponent>() == null) parented = parented.Parent;
        return parented;
    }

    public static void Patch()
    {
        if (!_patched)
        {
            _patched = true;
            new Harmony("qkrisi.buttonmesser").PatchAll();
        }
    }
}

[HarmonyPatch(typeof(BombComponent), "HandlePass", MethodType.Normal)]
public class SolvePatch
{
    public static void Prefix(BombComponent __instance)
    {
        __instance.GetComponent<qkButtonMesser>()?.ResetAll();
    }
}

[HarmonyPatch(typeof(BombComponent), "HandleStrike", MethodType.Normal)]
public class StrikePatch
{
    public static Selectable striked = null;
    public static List<BombComponent> CoroutineStrikes = new List<BombComponent>();

    public static bool IsEnabled(BombComponent instance)
    {
        var messer = Patcher.GetMeser(instance);
        return messer == null || messer.EnabledButtons.Contains(striked) || striked.GetComponent<Messed>() == null;
    }

    public static bool Prefix(BombComponent __instance)
    {
        if(CoroutineStrikes.Contains(__instance))
        {
            CoroutineStrikes.Remove(__instance);
            return false;
        }
        return striked==null || IsEnabled(__instance);
    }
}

[HarmonyPatch(typeof(BombComponent), "Activate", MethodType.Normal)]
public class ActivatePatch
{
    public static void Prefix(BombComponent __instance)
    {
        var messer = Patcher.GetMeser(__instance);
        if (messer != null) messer._done += 1;
    }
}

[HarmonyPatch(typeof(Selectable), "HandleInteract", MethodType.Normal)]
public class PressPatch
{
    public static bool Prefix(Selectable __instance, ILog ___logger, out qkButtonMesser __state, ref bool __result)
    {
        Selectable parented = Patcher.GetParent(__instance);
        var messer = Patcher.GetMeser((parented.Parent == null ? parented : parented.Parent).GetComponent<BombComponent>());
        if (messer != null && messer._enable)
        {    
            if (messer._forced)
            {
                if(__instance.GetComponent<Messed>()!=null) messer.DestroyObject(__instance.GetComponent<Messed>());
                __state = null;
                return true;
            }
            if(messer.AvoidStrike.Contains(__instance))
            {
                StrikePatch.striked = __instance;
                __state = messer;
                return true;
            }
            if (messer.AvoidVanilla.Contains(__instance))
            {
                __state = null;
                bool flag = true;
                ___logger.DebugFormat("OnInteract: {0}", __instance.name);
                BombComponent component = __instance.GetComponent<BombComponent>();
                if (component != null)
                {
                    component.Focused();
                    if (__instance.OnFocus != null) __instance.OnFocus();
                }
                if (__instance.OnInteract != null) flag &= __instance.OnInteract();
                __result = flag;
                return false;
            }
        }
        __state = null;
        return true;
    }

    public static void Postfix(Selectable __instance, qkButtonMesser __state)
    {
        if (__state != null)
        {
            StrikePatch.striked = null;
            __state.SubmitButton(__instance);
            __state.AvoidStrike.Remove(__instance);
        }
    }
}

[HarmonyPatch(typeof(MonoBehaviour), "StartCoroutine", typeof(IEnumerator))]
public class CoroutinePatch
{
    private static IEnumerator PatchedRoutine(IEnumerator BaseRoutine, BombComponent AvoidStrike)
    {
        StrikePatch.CoroutineStrikes.Add(AvoidStrike);
        yield return BaseRoutine;
        StrikePatch.CoroutineStrikes.Remove(AvoidStrike);
    }

    public static void Prefix(MonoBehaviour __instance, ref IEnumerator routine)
    {
        if (StrikePatch.striked == null) return;
        var component = __instance.GetComponent<BombComponent>();
        if (StrikePatch.IsEnabled(component)) return;
        routine = PatchedRoutine(routine, component);
    }
}

[HarmonyPatch(typeof(Selectable), "OnInteractEnded", MethodType.Normal)]
public class EndPatch
{
    public static bool Prefix(Selectable __instance)
    {
        Selectable parented = Patcher.GetParent(__instance);
        var messer = Patcher.GetMeser((parented.Parent == null ? parented : parented.Parent).GetComponent<BombComponent>());
        return messer == null || messer.UnlockedSelectables.Contains(__instance);
    }
}