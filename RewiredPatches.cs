using HarmonyLib;
using Rewired;
using Rewired.Data;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEngine;

namespace BBPRewiredCompat;

[HarmonyPatch]
internal class RewiredPatches
{
    [HarmonyPatch(typeof(InputManager_Base), "Start"), HarmonyPostfix]
    private static void SetUserData(ref UserData ____userData) => ____userData = ReInput.UserData;
    [HarmonyPatch(typeof(InputManager_Base), nameof(InputManager_Base.userData), MethodType.Getter), HarmonyPostfix]
    private static void GetStaticUserData(ref UserData __result) => __result = ReInput.UserData;
    [HarmonyPatch(typeof(InputManager), "Start"), HarmonyPostfix]
    private static void GetDigitalDictionary(Dictionary<string, bool> ___actionIsDigital) => RewiredPlusManager.actionIsDigital = ___actionIsDigital;
    [HarmonyPatch(typeof(UserDataStore_PlayerPrefs), "Save"), HarmonyPostfix]
    private static void SaveOLD() => RewiredPlusManager.Save();
    [HarmonyPatch(typeof(UserDataStore_KeyValue), "Save"), HarmonyPostfix]
    private static void Save() => RewiredPlusManager.Save();
    [HarmonyPatch(typeof(PlayerFileManager), "Load"), HarmonyPostfix]
    private static void Load() => RewiredPlusManager.Load();
    [HarmonyPatch(typeof(Rewired.UI.ControlMapper.ControlMapper), "Initialize"), HarmonyPrefix]
    private static void LoadPages(Rewired.UI.ControlMapper.ControlMapper __instance)
    {
        foreach (var page in RewiredPlusManager.newPages)
        {
            __instance._mappingSets[(int)page.Value]._actionCategoryIds = __instance._mappingSets[(int)page.Value]._actionCategoryIds.AddToArray((int)page.Key);
            __instance._mappingSets[(int)page.Value]._actionCategoryIdsReadOnly = new ReadOnlyCollection<int>(__instance._mappingSets[(int)page.Value]._actionCategoryIds);
        }
    }
    [HarmonyPatch(typeof(Rewired.UI.ControlMapper.ControlMapper), "OnRestoreDefaultsConfirmed")]
    [HarmonyPatch(typeof(InputManager), nameof(InputManager.ResetControlMaps))]
    [HarmonyPostfix]
    private static void RestoreDefaults(object __instance)
    {
        RewiredPlusManager.RestoreDefaults();
        if (__instance is Rewired.UI.ControlMapper.ControlMapper)
        {
            var mapper = (Rewired.UI.ControlMapper.ControlMapper)__instance;
            mapper.Redraw(false, false);
        }
    }
}
#if DEBUG
[HarmonyPatch]
internal class DebugPatches
{
    [HarmonyPatch(typeof(NameManager), "Awake"), HarmonyPriority(Priority.Low), HarmonyPrefix]
    private static void DebugVariables()
    {
        yay = Resources.FindObjectsOfTypeAll<SoundObject>().Last(x => x.name == "Student_Yay");
    }
    [HarmonyPatch(typeof(WarningScreen), "Start"), HarmonyPostfix]
    private static void DebugInsert()
    {
        var cat = RewiredPlusManager.CreateNewCategory("bbplustest", "Testing Grounds", RewiredPlusManager.InputMapPage.Gameplay);
        RewiredPlusManager.CreateNewInput("StudentYaySfx", "Yay!!", RewiredPlusManager.InputBehaviorID.Snap, cat, key: KeyCode.L, joystickElementId: 6, mouseElementId: 3);
        RewiredPlusManager.CreateNewInput("AxisTest", "TestAxis", RewiredPlusManager.InputBehaviorID.Snap, cat, key: (KeyCode.D, KeyCode.A, KeyCode.W, KeyCode.S));
    }
    private static SoundObject yay;
    [HarmonyPatch(typeof(PlayerManager), "Update"), HarmonyPostfix]
    private static void Yay()
    {
        if (InputManager.Instance.GetDigitalInput("StudentYaySfx", true))
            CoreGameManager.Instance.audMan.PlaySingle(yay);
    }
}
#endif