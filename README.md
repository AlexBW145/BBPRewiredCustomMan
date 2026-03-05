# BBPRewiredCompat
## For Modders
Just follow the example code in the debug class in `RewiredPatches.cs`, however the simple way of creating a digital input is always this:
```c#
RewiredPlusManager.CreateNewInput(string, string, RewiredPlusManager.InputBehaviorID, RewiredPlusManager.InputMapCategory, [key: KeyCode, joystickElementId: int, mouseElementId: int]);
```
With an example being:
```c#
RewiredPlusManager.CreateNewInput("MyOwnInput", "This Input Appears", RewiredPlusManager.InputBehaviorID.Snap, RewiredPlusManager.InputMapCategory.Actions, key: KeyCode.Q, joystickElementId: 6);
```
Afterwards, you can just perform regular input checks such as:
```c#
InputManager.Instance.GetDigitalInput(string, bool);
```
Make sure that the custom inputs are created before the save data has been loaded!
## For Users
Defined inputs has a default binding on what the coder binds to, controller support is indeed available but full functionality will occur if **Steam Input** is disabled.

Saved custom bindings in Baldi's Basics Plus is part of your global save and your defined custom bindings are packed into a json, the save system for custom bindings has been tested twice and works fine.

This manager is not dependent to any APIs, but it's only specifically dependent to Baldi's Basics Plus.