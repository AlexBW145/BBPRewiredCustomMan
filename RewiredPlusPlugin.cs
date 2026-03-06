using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Newtonsoft.Json;
using Rewired;
using Rewired.Data;
using Steamworks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace BBPRewiredCompat;

[BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
public class RewiredPlusPlugin : BaseUnityPlugin
{
    private const string 
        PLUGIN_GUID = "alexbw145.bbplus.rewiredcompat",
        PLUGIN_NAME = "Rewired Compat API",
        PLUGIN_VERSION = "1.1.1.0";
    public static string GUID => PLUGIN_GUID;
    internal static new ManualLogSource Logger = new ManualLogSource("Rewired Compat API");

    private void Awake()
    {
        Logger = base.Logger;
        new Harmony(PLUGIN_GUID).PatchAll();
    }
}

[Serializable]
internal class RewiredPlusData
{
    public string actionName;
    public ControllerType controllerType;
    public int controllerId;
    public bool isSecondInput;

    public KeyCode keyCode;
    public ModifierKeyFlags modifierKeys;
    public int elementIdentifier;
    public Pole axisContribution;
    public ControllerElementType elementType;
    public AxisRange axisRange;
    public bool invert;

    public RewiredPlusData() { }

    public RewiredPlusData(ActionElementMap elementMap) : this()
    {
        actionName = ReInput.UserData.GetActionById(elementMap.actionId).name;
        controllerType = elementMap.controllerMap.controllerType;
        controllerId = elementMap.controllerMap.controllerId;
        isSecondInput = elementMap._actionCategoryId == -1;

        keyCode = elementMap.keyCode;
        modifierKeys = elementMap.modifierKeyFlags;
        elementIdentifier = elementMap.elementIdentifierId;
        axisContribution = elementMap.axisContribution;
        elementType = elementMap.elementType;
        axisRange = elementMap.axisRange;
        invert = elementMap.invert;
    }
}

public static partial class RewiredPlusManager
{
    internal static List<ActionElementMap> GetElements()
    {
        var player = ReInput.players.GetPlayer(0);
        var list = new List<ActionElementMap>();
        foreach (var map in player.controllers.maps.GetAllMaps())
            map.AllMaps.DoIf(x => x.actionId > ReInput.UserData.actions.Count, list.Add); // DO NOT ADD IN INPUTS TO THIS LIST, THESE ARE LEFT BY DEFAULT.
        return list;
    }
    internal static void Save()
    {
        var path = Path.Combine(Application.persistentDataPath, "Modded");
        List<RewiredPlusData> inputs;
        if (File.Exists(Path.Combine(path, "customRewiredInput.json")))
            inputs = JsonConvert.DeserializeObject<List<RewiredPlusData>>(File.ReadAllText(Path.Combine(path, "customRewiredInput.json")));
        else
            inputs = new List<RewiredPlusData>();
        var list = GetElements();
        foreach (var action in actions)
        {
            foreach (var act in list.FindAll(x => x.actionId == action.Value.id))
            {
                inputs.RemoveAll(x => x.actionName == action.Value.name && x.controllerId == act.controllerMap.controllerId && x.controllerType == act.controllerMap.controllerType && x.axisRange == act.axisRange && x.axisContribution == act.axisContribution && x.isSecondInput == (act._actionCategoryId == -1 ? true : false));
                inputs.Add(new(act));
            }
            for (int i = 0; i < inputs.Count; i++) { // Makes unassigned inputs "unassigned" so that it will not reload to default bindings again.
                if (inputs[i].actionName == action.Value.name && !list.Exists(j => j.actionId == action.Value.id && inputs[i].controllerId == j.controllerMap.controllerId && inputs[i].controllerType == j.controllerMap.controllerType && j.axisRange == inputs[i].axisRange && j.axisContribution == inputs[i].axisContribution && inputs[i].isSecondInput == (j._actionCategoryId == -1 ? true : false)))
                {
                    inputs[i] = new RewiredPlusData()
                    {
                        actionName = action.Value.name,
                        controllerType = inputs[i].controllerType,
                        isSecondInput = inputs[i].isSecondInput,
                        elementType = inputs[i].elementType,
                        controllerId = inputs[i].controllerId,
                        axisRange = inputs[i].axisRange,
                        axisContribution = inputs[i].axisContribution,

                        elementIdentifier = -1,
                    };
                }
            }
        }
        var json = JsonConvert.SerializeObject(inputs, Formatting.Indented);
        File.WriteAllText(Path.Combine(path, "customRewiredInput.json"), json);
    }
    private static void SetBind(Player player, Rewired.InputAction action, KeyCode keycode, Pole pole = Pole.Positive)
    {
        player.controllers.maps.GetMap(ControllerType.Keyboard, 0, GetCategoryID(action), 0).CreateElementMap(action.id, pole, keycode, ModifierKeyFlags.None);
    }
    private static void SetBind(Player player, Rewired.InputAction action, int elementIdent, ControllerType type, Pole pole = Pole.Positive, bool fullRange = true)
    {
        if ((type == ControllerType.Joystick && player.controllers.joystickCount > 0) || type == ControllerType.Mouse)
            player.controllers.maps.GetMap(type, 0, GetCategoryID(action), 0).CreateElementMap(action.id, pole, elementIdent, (ControllerElementType)action.type, fullRange ? AxisRange.Full : (AxisRange)(pole + 1), false);
    }
    internal static void RestoreDefaults()
    {
        var save = (UserDataStore_PlayerPrefs)ReInput.userDataStore;
        var player = ReInput.players.GetPlayer(0);
        bool saveNow = false;
        foreach (var action in actions)
        {
            saveNow = true;
            if (string.IsNullOrEmpty(action.Value.key)) // positiveKey and negativeKey are not null because they have been autoassigned with key and key + "/negative"
            {
                KeyCode
                    positiveKey = (KeyCode)Enum.Parse(typeof(KeyCode), action.Value.positiveKey),
                    negativeKey = (KeyCode)Enum.Parse(typeof(KeyCode), action.Value.negativeKey);
                if (positiveKey != KeyCode.None)
                    SetBind(player, action.Value, positiveKey, Pole.Positive);
                if (negativeKey != KeyCode.None)
                    SetBind(player, action.Value, negativeKey, Pole.Negative);
            }
            else
            {
                KeyCode keycode = (KeyCode)Enum.Parse(typeof(KeyCode), action.Value.key);
                if (keycode != KeyCode.None)
                    SetBind(player, action.Value, keycode);
            }
            if (defaultJoystickBinds.ContainsKey(action.Value))
            {
                if (string.IsNullOrEmpty(action.Value.key))
                {
                    if (defaultJoystickBinds[action.Value].Item1 != -1)
                        SetBind(player, action.Value, defaultJoystickBinds[action.Value].Item1, ControllerType.Joystick, Pole.Positive, false);
                    if (defaultJoystickBinds[action.Value].Item2 != -1)
                        SetBind(player, action.Value, defaultJoystickBinds[action.Value].Item2, ControllerType.Joystick, Pole.Negative, false);
                }
                else
                    SetBind(player, action.Value, defaultJoystickBinds[action.Value].Item1, ControllerType.Joystick);
            }
            if (defaultMouseBinds.ContainsKey(action.Value))
            {
                if (string.IsNullOrEmpty(action.Value.key))
                {
                    if (defaultMouseBinds[action.Value].Item1 != -1)
                        SetBind(player, action.Value, defaultMouseBinds[action.Value].Item1, ControllerType.Mouse, Pole.Positive, false);
                    if (defaultMouseBinds[action.Value].Item2 != -1)
                        SetBind(player, action.Value, defaultMouseBinds[action.Value].Item2, ControllerType.Mouse, Pole.Negative, false);
                }
                else
                    SetBind(player, action.Value, defaultMouseBinds[action.Value].Item1, ControllerType.Mouse);
            }
        }
        if (saveNow)
            Save();
    }
    internal static void Load()
    {
        var path = Path.Combine(Application.persistentDataPath, "Modded");
        if (!File.Exists(Path.Combine(path, "customRewiredInput.json")))
        {
            RestoreDefaults();
            return;
        }
        var save = (UserDataStore_PlayerPrefs)ReInput.userDataStore;
        var player = ReInput.players.GetPlayer(0);
        bool saveNow = false;
        List<RewiredPlusData> inputs = JsonConvert.DeserializeObject<List<RewiredPlusData>>(File.ReadAllText(Path.Combine(path, "customRewiredInput.json")));
        foreach (var input in inputs.Where(x => x.elementIdentifier != -1))
        {
            if (actions.ContainsKey(input.actionName))
            {
                ActionElementMap map = null;
                player.controllers.maps.GetMap(input.controllerType, input.controllerId, GetCategoryID(actions[input.actionName]), 0)?.ReplaceOrCreateElementMap(new(input.controllerType, input.elementType, input.elementIdentifier, input.axisRange, input.keyCode, input.modifierKeys, actions[input.actionName].id, input.axisContribution, input.invert), out map);
                if (map != null)
                    map._actionCategoryId = input.isSecondInput ? -1 : 0;
            }
        }
        foreach (var action in actions.Where(x => !inputs.Exists(j => x.Key == j.actionName && j.controllerType == ControllerType.Keyboard)))
        {
            if (string.IsNullOrEmpty(action.Value.key))
            {
                KeyCode 
                    positiveKey = (KeyCode)Enum.Parse(typeof(KeyCode), action.Value.positiveKey),
                    negativeKey = (KeyCode)Enum.Parse(typeof(KeyCode), action.Value.negativeKey);
                if (positiveKey != KeyCode.None)
                {
                    saveNow = true;
                    SetBind(player, action.Value, positiveKey, Pole.Positive);
                }
                if (negativeKey != KeyCode.None)
                {
                    saveNow = true;
                    SetBind(player, action.Value, negativeKey, Pole.Negative);
                }
            }
            else
            {
                KeyCode keycode = (KeyCode)Enum.Parse(typeof(KeyCode), action.Value.key);
                if (keycode != KeyCode.None)
                {
                    saveNow = true;
                    SetBind(player, action.Value, keycode);
                }
            }
        }
        foreach (var action in actions.Where(x => defaultJoystickBinds.ContainsKey(x.Value)))
        {
            if (player.controllers.joystickCount > 0 && !inputs.Exists(j => action.Key == j.actionName && j.controllerType == ControllerType.Joystick))
            {
                saveNow = true;
                if (string.IsNullOrEmpty(action.Value.key))
                {
                    if (defaultJoystickBinds[action.Value].Item1 != -1)
                        SetBind(player, action.Value, defaultJoystickBinds[action.Value].Item1, ControllerType.Joystick, Pole.Positive, false);
                    if (defaultJoystickBinds[action.Value].Item2 != -1)
                        SetBind(player, action.Value, defaultJoystickBinds[action.Value].Item2, ControllerType.Joystick, Pole.Negative, false);
                }
                else
                    SetBind(player, action.Value, defaultJoystickBinds[action.Value].Item1, ControllerType.Joystick);
            }
        }
        foreach (var action in actions.Where(x => defaultMouseBinds.ContainsKey(x.Value)))
        {
            if (!inputs.Exists(j => action.Key == j.actionName && j.controllerType == ControllerType.Mouse))
            {
                saveNow = true;
                if (string.IsNullOrEmpty(action.Value.key))
                {
                    if (defaultMouseBinds[action.Value].Item1 != -1)
                        SetBind(player, action.Value, defaultMouseBinds[action.Value].Item1, ControllerType.Mouse, Pole.Positive, false);
                    if (defaultMouseBinds[action.Value].Item2 != -1)
                        SetBind(player, action.Value, defaultMouseBinds[action.Value].Item2, ControllerType.Mouse, Pole.Negative, false);
                }
                else
                    SetBind(player, action.Value, defaultMouseBinds[action.Value].Item1, ControllerType.Mouse);
            }
        }
        if (saveNow)
            Save();
    }
    private static readonly Dictionary<string, Rewired.InputAction> actions = new Dictionary<string, Rewired.InputAction>();
    private static readonly Dictionary<Rewired.InputAction, (int, int)> 
        defaultJoystickBinds = new Dictionary<Rewired.InputAction, (int, int)>(),
        defaultMouseBinds = new Dictionary<Rewired.InputAction, (int, int)>();
    internal static Dictionary<string, Rewired.InputAction> Actions => actions;
    internal static Dictionary<string, bool> actionIsDigital;
    public enum InputMapCategory
    {
        Default = 0,
        Movement = 1,
        Actions = 2,
        Menu = 3,
    }
    public enum InputBehaviorID
    {
        Default = 0,
        Snap = 1
    }
    private static void DoInsertsToRewired(Rewired.InputAction action, Rewired.InputBehavior behavior)
    {
        ReInput.IxvfGFEozkuytPyuvkhMreLoTHfHA.stleLScZDZJQrUJoNEgwOLcXdfCxA = ReInput.IxvfGFEozkuytPyuvkhMreLoTHfHA.stleLScZDZJQrUJoNEgwOLcXdfCxA.AddToArray(action);
        ReInput.IxvfGFEozkuytPyuvkhMreLoTHfHA.nTPwEUHXNHjmnrGoauPbkCHYvygn = ReInput.IxvfGFEozkuytPyuvkhMreLoTHfHA.stleLScZDZJQrUJoNEgwOLcXdfCxA.Length;
        if (action.id > ReInput.IxvfGFEozkuytPyuvkhMreLoTHfHA.RfRZHKnbfXNHAcPDjmPJpVrNvPLG)
            ReInput.IxvfGFEozkuytPyuvkhMreLoTHfHA.RfRZHKnbfXNHAcPDjmPJpVrNvPLG = action.id;
        var data = new xZCXUGDcjYPvQJkSmaUEEFZnKAiA.UUvuovmAQKWejENefGhqqmdLXItC(action, ReInput.IxvfGFEozkuytPyuvkhMreLoTHfHA.nTPwEUHXNHjmnrGoauPbkCHYvygn - 1);
        ReInput.IxvfGFEozkuytPyuvkhMreLoTHfHA.BbEwHQgueMMNhZtmbTIbqYaCDhes = ReInput.IxvfGFEozkuytPyuvkhMreLoTHfHA.BbEwHQgueMMNhZtmbTIbqYaCDhes.AddToArray(data);
        ReInput.IxvfGFEozkuytPyuvkhMreLoTHfHA.LVTXUUKVZiREbEJfrZMOEqpLzbbf.Add(action.name, data);
        ReInput.IxvfGFEozkuytPyuvkhMreLoTHfHA.NmdkjXCanZSlXdZefgYrbYzhElYm = new System.Collections.ObjectModel.ReadOnlyCollection<Rewired.InputAction>(ReInput.IxvfGFEozkuytPyuvkhMreLoTHfHA.stleLScZDZJQrUJoNEgwOLcXdfCxA);

        ReInput.RkhXZiawgZIAYuRDboepGdPvKqDL.skUcRTJPUkscVGYhzTxsQdNLnjrhb = ReInput.IxvfGFEozkuytPyuvkhMreLoTHfHA.nTPwEUHXNHjmnrGoauPbkCHYvygn;
        CXjDeHHBYTLUiyUxJsOcTBGTUZYJA inputdata = new CXjDeHHBYTLUiyUxJsOcTBGTUZYJA(9999999, action, behavior, ReInput.configVars);
        ReInput.RkhXZiawgZIAYuRDboepGdPvKqDL.fCShKwOohAeVUXnbeMlNdzEAyDJF = ReInput.RkhXZiawgZIAYuRDboepGdPvKqDL.fCShKwOohAeVUXnbeMlNdzEAyDJF.AddToArray(inputdata);
        var listofactiondatas = ReInput.RkhXZiawgZIAYuRDboepGdPvKqDL.kHBIfvsaRbIMIzJDSPxexnuTOClW.ToList();
        listofactiondatas.Insert(ReInput.RkhXZiawgZIAYuRDboepGdPvKqDL.skUcRTJPUkscVGYhzTxsQdNLnjrhb - 1, inputdata);
        if (ReInput.RkhXZiawgZIAYuRDboepGdPvKqDL.OBRivntvrCemHCkTRUDyDCQIMWOKA.GetLength(1) < ReInput.RkhXZiawgZIAYuRDboepGdPvKqDL.skUcRTJPUkscVGYhzTxsQdNLnjrhb)
        {
            var twodarray = new CXjDeHHBYTLUiyUxJsOcTBGTUZYJA[ReInput.players.playerCount, ReInput.RkhXZiawgZIAYuRDboepGdPvKqDL.skUcRTJPUkscVGYhzTxsQdNLnjrhb];
            int exactlength = ReInput.RkhXZiawgZIAYuRDboepGdPvKqDL.OBRivntvrCemHCkTRUDyDCQIMWOKA.GetLength(1) - 1;
            for (int i = 0; i < ReInput.players.playerCount; i++)
            {
                for (int j = 0; j < ReInput.RkhXZiawgZIAYuRDboepGdPvKqDL.skUcRTJPUkscVGYhzTxsQdNLnjrhb; j++)
                {
                    if (j < exactlength)
                        twodarray[i, j] = ReInput.RkhXZiawgZIAYuRDboepGdPvKqDL.OBRivntvrCemHCkTRUDyDCQIMWOKA[i, j];
                    else
                    {
                        inputdata = new CXjDeHHBYTLUiyUxJsOcTBGTUZYJA(i, action, behavior, ReInput.configVars);
                        twodarray[i, j] = inputdata;
                        listofactiondatas.Insert((i + 2) * j, inputdata);
                    }
                }
            }
            ReInput.RkhXZiawgZIAYuRDboepGdPvKqDL.OBRivntvrCemHCkTRUDyDCQIMWOKA = twodarray;
        }
        ReInput.RkhXZiawgZIAYuRDboepGdPvKqDL.kHBIfvsaRbIMIzJDSPxexnuTOClW = listofactiondatas.ToArray();
    }
    /// <summary>
    /// Creates a brand new button Rewired input.
    /// </summary>
    /// <param name="name">The object-like name of this input</param>
    /// <param name="descriptionName">The localization-like name of this input</param>
    /// <param name="behaviorID">The behavior ID for this input<para>Already defined ids are 0 (default) and 1 (snap, which all base game inputs uses)</para></param>
    /// <param name="categoryID">The category ID for this input</param>
    /// <param name="key">The default input for this key</param>
    /// <param name="joystickElementId">The default input id for this joystick input</param>
    /// <param name="mouseElementId">The default input id for this mouse input</param>
    /// <returns></returns>
    public static bool CreateNewInput(string name, string descriptionName, InputBehaviorID behaviorID, InputMapCategory categoryID,
        KeyCode key = KeyCode.None, int joystickElementId = -1, int mouseElementId = -1)
    {
        var type = InputActionType.Button;
        if (string.IsNullOrWhiteSpace(name)) return false;
        var userData = ReInput.UserData;
        if (actions.ContainsKey(name) || userData.NUOyBxwHYBZqYgECChWsabGDMOVS.Exists(x => x.name == name)) return false;
        try
        {
            if (behaviorID > InputBehaviorID.Snap)
            {
                RewiredPlusPlugin.Logger.LogWarning("Behavior ID has exceeded past 1, binding to 1! (Snap behavior)");
                behaviorID = InputBehaviorID.Snap;
            }
            var action = new Rewired.InputAction()
            {
                id = userData.GetNewActionId(),
                name = name,
                descriptiveName = descriptionName,
                type = type,
                userAssignable = true,
                behaviorId = (int)behaviorID,
                categoryId = (int)categoryID,
            };
            action.DJYOTBmJjtfSTvwDqbRIgptIirJuA();
            action.key = Keyboard.GetKeyName(key);
            actions.Add(name, action);
            userData.NUOyBxwHYBZqYgECChWsabGDMOVS.Add(action);
            var behavior = userData.GetInputBehaviorById((int)behaviorID); // Just do not go above 0 to 1, those are the already defined ones. (Especially when most uses the number 1)
            if (behavior == null) 
                throw new NullReferenceException("Behavior is null");
            userData.actionCategoryMap.list.Add(new Rewired.Data.Mapping.ActionCategoryMap.Entry((int)categoryID));
            userData.actionCategoryMap.AddAction((int)categoryID, action.id);
            DoInsertsToRewired(action, behavior);
            actionIsDigital.Add(name, type == InputActionType.Button);
            InputManager.Instance.rewiredInputNameToSteamInputName.Add(name, name);
            if (SteamManager.Initialized)
                InputManager.Instance.steamDigitalInputs.Add(name, new DigitalInputData(SteamInput.GetDigitalActionHandle(name)));
            if (joystickElementId != -1)
                defaultJoystickBinds.Add(action, (joystickElementId, -1));
            if (mouseElementId != -1)
                defaultMouseBinds.Add(action, (mouseElementId, -1));
            return true;
        }
        catch (Exception ex)
        {
            RewiredPlusPlugin.Logger.LogError(ex);
            return false;
        }
    }
    /// <summary>
    /// Creates a brand new axis Rewired input.
    /// </summary>
    /// <param name="name">The object-like name of this input</param>
    /// <param name="descriptionName">The localization-like name of this input</param>
    /// <param name="behaviorID">The behavior ID for this input<para>Already defined ids are 0 (default) and 1 (snap, which all base game inputs uses)</para></param>
    /// <param name="categoryID">The category ID for this input</param>
    /// <param name="key">The default inputs for this key<para>As in order: PosX, NegX, PosY, NegY</para></param>
    /// <param name="joystickElementId">The default inputs id for this joystick input<para>As in order: PosX, NegX, PosY, NegY</para></param>
    /// <param name="mouseElementId">The default inputs id for this mouse input<para>As in order: PosX, NegX, PosY, NegY</para></param>
    /// <returns></returns>
    public static bool CreateNewInput(string name, string descriptionName, InputBehaviorID behaviorID, InputMapCategory categoryID,
        (KeyCode, KeyCode, KeyCode, KeyCode)? key = null, (int, int, int, int)? joystickElementId = null, (int, int, int, int)? mouseElementId = null)
    {
        var type = InputActionType.Axis;
        if (string.IsNullOrWhiteSpace(name)) return false;
        var userData = ReInput.UserData;
        if (actions.ContainsKey(name + "X") || actions.ContainsKey(name + "Y") 
            || userData.NUOyBxwHYBZqYgECChWsabGDMOVS.Exists(x => x.name == name + "X") || userData.NUOyBxwHYBZqYgECChWsabGDMOVS.Exists(x => x.name == name + "Y")) return false;
        if (key == null) key = new(KeyCode.None, KeyCode.None, KeyCode.None, KeyCode.None);
        if (joystickElementId == null) joystickElementId = new(-1, -1, -1, -1);
        if (mouseElementId == null) mouseElementId = new(-1, -1, -1, -1);
        try
        {
            if (behaviorID > InputBehaviorID.Snap)
            {
                RewiredPlusPlugin.Logger.LogWarning("Behavior ID has exceeded past 1, binding to 1! (Snap behavior)");
                behaviorID = InputBehaviorID.Snap;
            }
            for (int step = 0; step < 2; step++)
            {
                string xy = step == 0 ? "X" : "Y";
                var action = new Rewired.InputAction()
                {
                    id = userData.GetNewActionId(),
                    name = name + xy,
                    descriptiveName = descriptionName + " " + xy,
                    positiveDescriptiveName = descriptionName + " " + xy + "+",
                    negativeDescriptiveName = descriptionName + " " + xy + "-",
                    type = type,
                    userAssignable = true,
                    behaviorId = (int)behaviorID,
                    categoryId = (int)categoryID,
                };
                action.DJYOTBmJjtfSTvwDqbRIgptIirJuA();
                action.positiveKey = Keyboard.GetKeyName(step == 0 ? key.Value.Item1 : key.Value.Item3);
                action.negativeKey = Keyboard.GetKeyName(step == 0 ? key.Value.Item2 : key.Value.Item4);
                actions.Add(name + xy, action);
                userData.NUOyBxwHYBZqYgECChWsabGDMOVS.Add(action);
                var behavior = userData.GetInputBehaviorById((int)behaviorID); // Just do not go above 0 to 1, those are the already defined ones. (Especially when most uses the number 1)
                if (behavior == null)
                    throw new NullReferenceException("Behavior is null");
                userData.actionCategoryMap.list.Add(new Rewired.Data.Mapping.ActionCategoryMap.Entry((int)categoryID));
                userData.actionCategoryMap.AddAction((int)categoryID, action.id);
                DoInsertsToRewired(action, behavior);
                actionIsDigital.Add(name + xy, type == InputActionType.Button);
                InputManager.Instance.rewiredInputNameToSteamInputName.Add(name + xy, name);
                if ((step == 0 ? (joystickElementId.Value.Item1, joystickElementId.Value.Item3) : (joystickElementId.Value.Item2, joystickElementId.Value.Item4)) != (-1, -1))
                    defaultJoystickBinds.Add(action, (step == 0 ? (joystickElementId.Value.Item1, joystickElementId.Value.Item3) : (joystickElementId.Value.Item2, joystickElementId.Value.Item4)));
                if ((step == 0 ? (mouseElementId.Value.Item1, mouseElementId.Value.Item3) : (mouseElementId.Value.Item2, mouseElementId.Value.Item4)) != (-1, -1))
                    defaultMouseBinds.Add(action, (step == 0 ? (mouseElementId.Value.Item1, mouseElementId.Value.Item3) : (mouseElementId.Value.Item2, mouseElementId.Value.Item4)));
            }
            if (SteamManager.Initialized)
                InputManager.Instance.steamAnalogInputs.Add(name, SteamInput.GetAnalogActionHandle(name));
            return true;
        }
        catch (Exception ex)
        {
            RewiredPlusPlugin.Logger.LogError(ex);
            return false;
        }
    }
    /// <summary>
    /// Grabs the input ID for this custom input
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public static int GetInputID(string name)
    {
        if (!actions.ContainsKey(name)) return -1;
        return actions[name].id;
    }
    public enum InputMapPage
    {
        Gameplay = 0,
        Menu = 1
    }
    internal static readonly Dictionary<InputMapCategory, InputMapPage> newPages = new Dictionary<InputMapCategory, InputMapPage>();
    /// <summary>
    /// Creates a brand new category to an existing Rewired Mapper Page
    /// </summary>
    /// <param name="name">The object-like name of this category</param>
    /// <param name="descriptionName">The localization-like name of this category</param>
    /// <param name="page">The existing page where this mapping category goes to</param>
    /// <returns></returns>
    public static InputMapCategory CreateNewCategory(string name, string descriptionName, InputMapPage page)
    {
        var userData = ReInput.UserData;
        try
        {
            InputActionCategory inputActionCategory = new InputActionCategory()
            {
                id = userData.GetNewActionCategoryId(),
                name = name,
            };
            inputActionCategory.descriptiveName = descriptionName;
            inputActionCategory.userAssignable = true;
            userData.actionCategories.Add(inputActionCategory);
            userData.actionCategoryMap.AddCategory(inputActionCategory.id);
            newPages.Add((InputMapCategory)inputActionCategory.id, page);
            return (InputMapCategory)inputActionCategory.id;
        }
        catch (Exception ex) 
        {
            RewiredPlusPlugin.Logger.LogError(ex);
            return (InputMapCategory)(-1);
        }
    }
    private static int GetCategoryID(Rewired.InputAction action)
    {
        if (newPages.ContainsKey((InputMapCategory)action.categoryId))
            return (int)newPages[(InputMapCategory)action.categoryId];
        return (InputMapCategory)action.categoryId switch
        {
            InputMapCategory.Default => -1, // Not implemented category
            InputMapCategory.Movement => 0,
            InputMapCategory.Actions => 0,
            InputMapCategory.Menu => 1,
            _ => throw new Exception("Out of bound vanilla categories!!")
        };
    }
}