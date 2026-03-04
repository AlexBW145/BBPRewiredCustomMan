using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Newtonsoft.Json;
using Rewired;
using Rewired.Data;
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
        PLUGIN_VERSION = "1.0.0.1";
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

    public KeyCode keyCode;
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

        keyCode = elementMap.keyCode;
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
        var path = Path.Combine(Application.persistentDataPath, "Modded", PlayerFileManager.Instance.fileName);
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
                inputs.RemoveAll(x => x.actionName == action.Value.name && x.controllerId == act.controllerMap.controllerId && x.controllerType == act.controllerMap.controllerType);
                inputs.Add(new(act));
            }
            foreach (var act in list.FindAll(x => x.actionId == action.Value.id))
            {
                inputs.RemoveAll(x => x.actionName == action.Value.name && x.controllerId == act.controllerMap.controllerId && x.controllerType == act.controllerMap.controllerType);
                inputs.Add(new(act));
            }
            for (int i = 0; i < inputs.Count; i++) { // Makes unassigned inputs "unassigned" so that it will not reload to default bindings again.
                if (inputs[i].actionName == action.Value.name && !list.Exists(j => j.actionId == action.Value.id && inputs[i].controllerId == j.controllerMap.controllerId && inputs[i].controllerType == j.controllerMap.controllerType))
                {
                    inputs[i] = new RewiredPlusData()
                    {
                        actionName = action.Value.name,
                        controllerType = inputs[i].controllerType,
                        controllerId = inputs[i].controllerId,

                        elementIdentifier = -1,
                    };
                }
            }
        }
        var json = JsonConvert.SerializeObject(inputs, Formatting.Indented);
        File.WriteAllText(Path.Combine(path, "customRewiredInput.json"), json);
    }
    internal static void Load()
    {
        bool saveNow = false;
        var save = (UserDataStore_PlayerPrefs)ReInput.userDataStore;
        var player = ReInput.players.GetPlayer(0);
        var path = Path.Combine(Application.persistentDataPath, "Modded", PlayerFileManager.Instance.fileName);
        if (!File.Exists(Path.Combine(path, "customRewiredInput.json")))
        {
            foreach (var action in actions)
            {
                saveNow = true;
                player.controllers.maps.GetMap(ControllerType.Keyboard, 0, 0, 0).CreateElementMap(action.Value.id, Pole.Positive, (KeyCode)Enum.Parse(typeof(KeyCode), action.Value.key), ModifierKeyFlags.None);
                if (enqueuedJoystickBinds.ContainsKey(action.Value))
                {
                    if (player.controllers.joystickCount > 0)
                        player.controllers.maps.GetMap(ControllerType.Joystick, 0, 0, 0).CreateElementMap(action.Value.id, Pole.Positive, enqueuedJoystickBinds[action.Value], ControllerElementType.Button, AxisRange.Full, false);
                    enqueuedJoystickBinds.Remove(action.Value);
                }
            }
            if (saveNow)
                Save();
            return;
        }
        List<RewiredPlusData> inputs = JsonConvert.DeserializeObject<List<RewiredPlusData>>(File.ReadAllText(Path.Combine(path, "customRewiredInput.json")));
        foreach (var input in inputs.Where(x => x.elementIdentifier != -1))
        {
            if (actions.ContainsKey(input.actionName))
            {
                player.controllers.maps.GetMap(input.controllerType, input.controllerId, 0, 0).ReplaceOrCreateElementMap(new(input.controllerType, input.elementType, input.elementIdentifier, input.axisRange, input.keyCode, ModifierKeyFlags.None, actions[input.actionName].id, input.axisContribution, input.invert));
                if (input.controllerType == ControllerType.Joystick && enqueuedJoystickBinds.ContainsKey(actions[input.actionName]))
                    enqueuedJoystickBinds.Remove(actions[input.actionName]);
            }
        }
        foreach (var action in actions.Where(x => !inputs.Exists(j => actions.ContainsKey(j.actionName))))
        {
            saveNow = true;
            player.controllers.maps.GetMap(ControllerType.Keyboard, 0, 0, 0).CreateElementMap(action.Value.id, Pole.Positive, (KeyCode)Enum.Parse(typeof(KeyCode), action.Value.key), ModifierKeyFlags.None);
        }
        foreach (var action in actions.Where(x => enqueuedJoystickBinds.ContainsKey(x.Value)))
        {
            if (player.controllers.joystickCount > 0 && !inputs.Exists(j => actions.ContainsKey(j.actionName) && j.controllerType == ControllerType.Joystick))
            {
                saveNow = true;
                player.controllers.maps.GetMap(ControllerType.Joystick, 0, 0, 0).CreateElementMap(action.Value.id, Pole.Positive, enqueuedJoystickBinds[action.Value], ControllerElementType.Button, AxisRange.Full, false);
            }
            enqueuedJoystickBinds.Remove(action.Value);
        }
        if (saveNow)
            Save();
    }
    private static readonly Dictionary<string, Rewired.InputAction> actions = new Dictionary<string, Rewired.InputAction>();
    private static readonly Dictionary<Rewired.InputAction, int> enqueuedJoystickBinds = new Dictionary<Rewired.InputAction, int>();
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
    /// <summary>
    /// Used for creating a brand new Rewired input.
    /// </summary>
    /// <param name="name">The object-like name of this input</param>
    /// <param name="descriptionName">The localization-like name of this input</param>
    /// <param name="type">If this input is considered to be a button (keyboard and controller buttons) or an axis (mouse and controller joystick)</param>
    /// <param name="behaviorID">The behavior ID for this input<para>Already defined ids are 0 (default) and 1 (snap, which all base game inputs uses)</para></param>
    /// <param name="categoryID">The category ID for this input</param>
    /// <param name="key">The default input for this key</param>
    /// <param name="joystickElementId">The default input id for this joystick input</param>
    /// <returns></returns>
    public static bool CreateNewInput(string name, string descriptionName, InputActionType type, InputBehaviorID behaviorID, InputMapCategory categoryID,
        KeyCode key = KeyCode.None, int joystickElementId = -1)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        var userData = ReInput.UserData;
        if (actions.ContainsKey(name) || userData.actions.Exists(x => x.name == name)) return false;
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
                positiveDescriptiveName = descriptionName + " +",
                negativeDescriptiveName = descriptionName + " -",
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
            actionIsDigital.Add(name, type == InputActionType.Button);
            InputManager.Instance.rewiredInputNameToSteamInputName.Add(name, name);
            if (joystickElementId != -1)
                enqueuedJoystickBinds.Add(action, joystickElementId);
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
}