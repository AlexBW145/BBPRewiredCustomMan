# BBPRewiredCompat
## For Modders
Just follow the example code in the debug class in `RewiredPatches.cs`, however the simple way of creating a digital input is always this:
```cs
RewiredPlusManager.CreateNewInput("MyOwnInput", "This Input Appears", InputActionType.Button, RewiredPlusManager.InputBehaviorID.Snap, RewiredPlusManager.InputMapCategory.Actions, key: KeyCode.Q);
```
## For Users
Defined inputs has a default binding on what the coder binds to, controller support is indeed available but full functionality will occur if **Steam Input** is disabled.

Saved custom bindings goes to specific name entries in your global save packed into a json, has been tested twice and works fine.

This manager is not dependent to any APIs, but it's only specifically dependent to Baldi's Basics Plus.