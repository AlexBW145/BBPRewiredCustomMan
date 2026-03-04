using HarmonyLib;
using Rewired;
using Rewired.Data;
using System.Collections.Generic;
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
    /*[HarmonyPatch(typeof(UserDataStore_PlayerPrefs), "GetControllerMapPlayerPrefsKeyCommonSuffix"), HarmonyPrefix]
    private static void Set256(Player player, ControllerIdentifier controllerIdentifier, int categoryId, ref int layoutId, int ppKeyVersion, out int __state)
    {
        __state = layoutId;
        if (layoutId > ReInput.UserData.actions.Count) // DO NOT ADD IN INPUTS TO THIS LIST, THESE ARE LEFT BY DEFAULT.
            layoutId = 256;
    }
    [HarmonyPatch(typeof(UserDataStore_PlayerPrefs), "GetControllerMapPlayerPrefsKeyCommonSuffix"), HarmonyPostfix]
    private static void Is256(Player player, ControllerIdentifier controllerIdentifier, int categoryId, int layoutId, int ppKeyVersion, ref string __result, int __state)
    {
        if (layoutId == 256)
            __result += "|actualModdedString=" + ReInput.UserData.GetActionById(__state).name;
    }
    [HarmonyPatch(typeof(ControllerMap), nameof(ControllerMap.uYSdnhMxPwUaPWNtLyieoaHsarrc)), HarmonyPostfix]
    private static void Grab256(object[] __args, ControllerMap __instance)
    {
        SerializedObject obj = (SerializedObject)__args[0];
        if (__instance._layoutId == 256)
        {
            string thevalue = "";
            obj.TryGetDeserializedValueByRef("actualModdedString", ref thevalue);
            if (RewiredPlusManager.Actions.ContainsKey(thevalue))
                __instance.layoutId = RewiredPlusManager.Actions[thevalue].id;
        }
    }
    [HarmonyPatch(typeof(ControllerMap), nameof(ControllerMap.uzaeGcbFOWCisiMZhXvlchZxqgLuB)), HarmonyPrefix]
    static void Save256(out int __state, ControllerMap __instance)
    {
        __state = __instance._layoutId;
        if (__instance._layoutId > ReInput.UserData.actions.Count)
            __instance._layoutId = 256;
    }
    [HarmonyPatch(typeof(ControllerMap), nameof(ControllerMap.uzaeGcbFOWCisiMZhXvlchZxqgLuB)), HarmonyPostfix]
    static void Reset256(object[] __args, int __state, ControllerMap __instance)
    {
        SerializedObject obj = (SerializedObject)__args[0];
        if (__instance._layoutId == 256)
        {
            obj.Add("actualModdedString", ReInput.UserData.GetActionById(__state).name);
            __instance._layoutId = __state;
        }
    }*/
    [HarmonyPatch(typeof(UserDataStore_PlayerPrefs), "Save"), HarmonyPostfix]
    private static void Save() => RewiredPlusManager.Save();
    [HarmonyPatch(typeof(PlayerFileManager), "Load"), HarmonyPostfix]
    private static void Load() => RewiredPlusManager.Load();
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
        RewiredPlusManager.CreateNewInput("StudentYaySfx", "Yay!!", InputActionType.Button, RewiredPlusManager.InputBehaviorID.Snap, RewiredPlusManager.InputMapCategory.Actions, key: KeyCode.L, joystickElementId: 6, mouseElementId: 3);
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