**🌐[ [中文](JML_API_Reference.md) | English ]**

# JmcModLib STS2 API Reference

Source baseline: JML `1.1.0`. This document is reorganized from the source and does not treat older documentation as authoritative. Common namespace imports:

```csharp
using JmcModLib.Core;
using JmcModLib.Config;
using JmcModLib.Config.UI;
using JmcModLib.Config.Storage;
using JmcModLib.Reflection;
using JmcModLib.Utils;
using JmcModLib.Prefabs;
```

`ExprHelper` is now in the `JmcModLib.Utils` namespace and is usually brought in automatically by `global using JmcModLib.Utils;`.

---

## 1. Overall JML Lifecycle

```mermaid
flowchart TD
    A[Game loads JmcModLib.dll] --> B[Bootstrap reads runtime.config]
    B --> C[Resolve dependencies and load Runtime]
    C --> D[JmcModLib.MainFile.Initialize]
    D --> E[Register JML itself / Harmony Patch]
    E --> F[Child MOD Initialize]
    F --> G[ModRegistry.Register]
    G --> H[ModContext created or updated]
    H --> I[Default services enabled: Logger + ConfigManager]
    I --> J[ConfigManager initializes AttributeRouter and handlers]
    J --> K[Register immediately Done or Builder.Done]
    K --> L[ModRegistry.OnRegistered]
    L --> M[AttributeRouter.ScanAssembly]
    M --> N1[Config / UI / Button entries]
    M --> N2[Hotkey registrations]
    N1 --> O[Storage sync and settings UI generation]
    N2 --> P[Input Relay / Steam Backend]
    O --> Q[User changes settings]
    P --> R[User triggers hotkey]
    Q --> S[Write back field/property + persist + callback]
    R --> T[Execute MOD action]
    S --> U[Unregister/exit Flush and cleanup]
    T --> U
```

---

## 2. Core: Registration, Context, Runtime Information

### 2.1 Lifecycle Diagram

```mermaid
flowchart TD
    A[Child MOD calls Register] --> B[ResolveAssembly]
    B --> C[Infer ModId / DisplayName / Version]
    C --> D[Contexts AddOrUpdate]
    D --> E[EnsureDefaultServices]
    E --> F[ModLogger.RegisterAssembly]
    E --> G[ConfigManager.Init]
    G --> H{deferred?}
    H -- no --> I[Builder.Done]
    H -- yes --> J[WithDisplayName / WithVersion / WithConfigStorage / RegisterButton]
    J --> I
    I --> K[context.IsCompleted = true]
    K --> L[OnRegistered]
    L --> M[AttributeRouter scans]
    M --> N[Runtime GetContext/GetTag/Unregister]
```

### 2.2 `VersionInfo`

Namespace: `JmcModLib.Core`

| Member | Description |
|---|---|
| `const string Name = "JmcModLib"` | JML name |
| `const string Version = "1.1.0"` | JML version |
| `string Tag` | `"[JmcModLib v1.1.0]"` |
| `GetName(Assembly? assembly = null)` | Gets the specified assembly name; JML itself returns the fixed name |
| `GetVersion(Assembly? assembly = null)` | Gets the specified assembly version; JML itself returns the fixed version |
| `GetTag(Assembly? assembly = null)` | Builds a log tag |

### 2.3 `ModContext`

Namespace: `JmcModLib.Core`

| Property | Type | Description |
|---|---|---|
| `Assembly` | `Assembly` | Current MOD managed assembly |
| `ModId` | `string` | Stable ID, usually equal to manifest `id` |
| `DisplayName` | `string` | Display name used by UI and logs |
| `Version` | `string` | Current registered version |
| `IsCompleted` | `bool` | Whether `Done()` has been triggered to complete registration |
| `LoggerContext` | `string` | Context passed to STS2 logging |
| `Tag` | `string` | Formatted like `[DisplayName v1.0.0]` |

### 2.4 `ModRegistry`

Namespace: `JmcModLib.Core`

| Member | Signature / Default Parameters | Description |
|---|---|---|
| `OnRegistered` | `event Action<ModContext>?` | Fired after a MOD completes registration; AttributeRouter depends on this event |
| `OnUnregistered` | `event Action<ModContext>?` | Fired after a MOD is unregistered |
| `Register` | `Register(string modId, string? displayName = null, string? version = null, Assembly? assembly = null)` | Registers with a manual ID and returns a builder |
| `Register` | `Register(bool deferredCompletion, string modId, string? displayName = null, string? version = null, Assembly? assembly = null)` | Uses a bool to control whether `Done` is deferred |
| `Register` | `Register(bool deferredCompletion, object? modInfo, string? displayName = null, string? version = null, Assembly? assembly = null)` | Reads id/name/version from an anonymous object or metadata object |
| `Register<T>` | `void Register<T>()` | Recommended entry point; completes registration immediately |
| `Register<T>` | `RegistryBuilder? Register<T>(bool deferredCompletion)` | Generic entry point with deferred builder support |
| `Register<T>` | `RegistryBuilder Register<T>(string modId, string? displayName = null, string? version = null)` | Generic entry point with an explicit ID |
| `Register<T>` | `RegistryBuilder? Register<T>(bool deferredCompletion, string modId, string? displayName = null, string? version = null)` | Explicit ID with optional deferral |
| `IsRegistered` | `bool IsRegistered(Assembly? assembly = null)` | Checks whether the Assembly is registered |
| `TryGetContext` | `bool TryGetContext(out ModContext? context, Assembly? assembly = null)` | Tries to get the context |
| `GetContext` | `ModContext? GetContext(Assembly? assembly = null)` | Gets the context |
| `GetModId` | `string GetModId(Assembly? assembly = null)` | Gets the ID; falls back to the Assembly when unregistered |
| `GetDisplayName` | `string GetDisplayName(Assembly? assembly = null)` | Gets the display name |
| `GetVersion` | `string GetVersion(Assembly? assembly = null)` | Gets the version |
| `GetTag` | `string GetTag(Assembly? assembly = null)` | Gets the log tag |
| `Unregister` | `bool Unregister(Assembly? assembly = null)` | Unregisters the context and triggers cleanup |

Recommendation: ordinary child MODs should use `Register<MainFile>()`; shared helpers or cross-Assembly operations should pass `Assembly` explicitly.

### 2.5 `RegistryBuilder`

Namespace: `JmcModLib.Core`

| Method | Default Parameters | Description |
|---|---|---|
| `WithDisplayName(string displayName)` | None | Overrides the display name |
| `WithVersion(string version)` | None | Overrides the version |
| `WithConfigStorage(IConfigStorage storage)` | None | Sets custom storage before scanning |
| `RegisterButton(out string key, string description, Action action, string buttonText = "按钮", string group = ConfigAttribute.DefaultGroup, string? storageKey = null, string? helpText = null, string? locTable = null, string? displayNameKey = null, string? helpTextKey = null, string? buttonTextKey = null, string? groupKey = null, int order = 0, UIButtonColor color = UIButtonColor.Default)` | See signature | Registers a manual button and returns the key |
| `RegisterButton(string description, Action action, ...)` | Same as above | Registers a manual button without retrieving the key |
| `Done()` | None | Completes registration and triggers Attribute scanning |

`Done()` may be called repeatedly. The first call triggers the lifecycle; later calls only return the existing context.

### 2.6 `ModRuntime`

Namespace: `JmcModLib.Core`

| Method | Description |
|---|---|
| `TryGetLoadedMod(Assembly? assembly = null)` | Finds the loaded STS2 `Mod` |
| `TryGetManifest(Assembly? assembly = null)` | Finds the manifest corresponding to the current Assembly |
| `GetManifestId(Assembly? assembly = null)` | Manifest `id` |
| `GetPckName(Assembly? assembly = null)` | pck name; falls back to Assembly name on failure |
| `GetDisplayName(Assembly? assembly = null)` | Manifest name or Assembly name |
| `GetLoadedVersion(Assembly? assembly = null)` | Manifest version or Assembly version |
| `FindModById(string modId)` | Finds by exact manifest id |
| `FindLoadedMod(string modId)` | Finds by id/pck/name/assembly name |

---

## 3. Bootstrap / Runtime Loading

### 3.1 Lifecycle Diagram

```mermaid
flowchart TD
    A[STS2 loads JmcModLib.dll] --> B[BootstrapMain.Initialize]
    B --> C[Locate mod directory]
    C --> D[Read JmcModLib.runtime.config]
    D --> E[Normalize runtimeAssembly / initializer / probe dirs]
    E --> F[Install Assembly Resolver]
    F --> G[Load dependencies]
    G --> H[Load JmcModLib.Runtime.dll]
    H --> I[Reflect initializerType + initializerMethod]
    I --> J[Call JmcModLib.MainFile.Initialize]
```

### 3.2 `BootstrapMain`

Namespace: an independent assembly in the Bootstrap project. Ordinary child MODs do not need to call this directly.

| Method | Description |
|---|---|
| `Initialize()` | Called after the game loads the JML Bootstrap; responsible for loading the Runtime |

Key files in the publish directory:

```text
JmcModLib.dll                 # Bootstrap, the dll named by the game manifest
JmcModLib.Runtime.dll         # Runtime referenced by child MODs
JmcModLib.Runtime.xml         # IntelliSense XML
JmcModLib.Sts2.props          # MSBuild reference entry point for child MODs
JmcModLib.runtime.config      # Runtime loading descriptor
Newtonsoft.Json.dll           # Dependency
JmcModLib.pck                 # Godot resource package
JmcModLib.json                # JML manifest
```

Current core fields in `JmcModLib.runtime.config`:

```json
{
  "runtimeAssembly": "JmcModLib.Runtime.dll",
  "initializerType": "JmcModLib.MainFile",
  "initializerMethod": "Initialize",
  "probeDirectories": [".", "lib", "libs"],
  "dependencies": ["Newtonsoft.Json.dll"],
  "probeAllDlls": true
}
```

---

## 4. AttributeRouter: Attribute Scanning and Extension

### 4.1 Lifecycle Diagram

```mermaid
flowchart TD
    A[ConfigManager.Init or manual Init] --> B[AttributeRouter.Init]
    B --> C[Subscribe to ModRegistry.OnRegistered/OnUnregistered]
    C --> D[Register IAttributeHandler]
    D --> E[OnRegistered after MOD Done]
    E --> F[ScanAssembly]
    F --> G[Iterate TypeAccessor]
    F --> H[Iterate MethodAccessor]
    F --> I[Iterate MemberAccessor]
    G --> J[Read Attributes]
    H --> J
    I --> J
    J --> K[Find handlers by exact Attribute type]
    K --> L[handler.Handle]
    L --> M[Record handler/accessor]
    M --> N[AssemblyScanned]
    C --> O[OnUnregistered]
    O --> P[UnscanAssembly]
    P --> Q[Call handler.Unregister]
    Q --> R[AssemblyUnscanned]
```

### 4.2 `AttributeRouter`

Namespace: `JmcModLib.Core.AttributeRouter`

| Member | Description |
|---|---|
| `AssemblyScanned` | Scan-completed event |
| `AssemblyUnscanned` | Unscan/unregister-completed event |
| `IsInitialized` | Whether it has been initialized |
| `Init()` | Subscribes to the registration lifecycle |
| `Dispose()` | Unsubscribes events, unscans scanned Assemblies, and clears handlers |
| `RegisterHandler<TAttribute>(IAttributeHandler handler)` | Registers a handler |
| `RegisterHandler<TAttribute>(Action<Assembly, ReflectionAccessorBase, TAttribute> action)` | Registers a simple action handler |
| `UnregisterHandler(IAttributeHandler handler)` | Removes a handler, but does not automatically unscan existing records |
| `ScanAssembly(Assembly assembly)` | Scans an Assembly |
| `UnscanAssembly(Assembly assembly)` | Runs handler unregister callbacks and clears records |

### 4.3 `IAttributeHandler`

| Member | Description |
|---|---|
| `Handle(Assembly assembly, ReflectionAccessorBase accessor, Attribute attribute)` | Handles a discovered Attribute |
| `Unregister` | Optional cleanup callback; parameters are the Assembly and the list of accessors handled by this handler |

### 4.4 `SimpleAttributeHandler<TAttribute>`

Constructor:

```csharp
new SimpleAttributeHandler<TAttribute>(Action<Assembly, ReflectionAccessorBase, TAttribute> action)
```

It only handles the registered type `TAttribute`, and `Unregister` is currently `null`. It is suitable for lightweight extensions, not for extensions that need unload cleanup.

---

## 5. Config: Configuration, Storage, Entries

### 5.1 Lifecycle Diagram

```mermaid
flowchart TD
    A[ConfigManager.Init] --> B[Register Config/UIButton/JmcHotkey/UIHotkey handlers]
    B --> C[AttributeRouter scans members]
    C --> D{Attribute type}
    D -- Config --> E[BuildConfigEntry]
    D -- UIButton --> F[BuildButtonEntry]
    D -- UIHotkey --> G[RegisterConfig + RegisterHotkey]
    E --> H[RegisterEntry]
    F --> H
    G --> H
    H --> I[SyncFromStorage]
    I --> J{Config file already has value?}
    J -- yes --> K[Convert and write back to field/property]
    J -- no --> L[Save default value]
    K --> M[Register UI / input action]
    L --> M
    M --> N[User changes UI or calls SetValue]
    N --> O[ConfigEntry.SetValue]
    O --> P[setter writes back to source member]
    P --> Q[Persist/Flush]
    Q --> R[onChanged + ValueChanged]
    R --> S[Flush/Unregister on exit or unregister]
```

### 5.2 `ConfigAttribute`

Namespace: `JmcModLib.Config`

Constructor:

```csharp
[Config(string displayName, string? onChanged = null, string group = ConfigAttribute.DefaultGroup)]
```

| Member | Default | Description |
|---|---:|---|
| `DefaultGroup` | `"DefaultGroup"` | Default group constant |
| `DisplayName` | Constructor parameter | UI fallback display name |
| `OnChanged` | `null` | Static callback method name |
| `Group` | `DefaultGroup` | Group |
| `Key` | `null` | Storage key; when empty, Attribute registration uses `DeclaringType.FullName.MemberName` |
| `Description` | `null` | Description fallback text |
| `LocTable` | `null` | Localization table; defaults to `settings_ui` when used by the UI layer |
| `DisplayNameKey` | `null` | Display-name localization key |
| `DescriptionKey` | `null` | Description localization key |
| `GroupKey` | `null` | Group localization key |
| `Order` | `0` | Sort order; smaller values come first |
| `RestartRequired` | `false` | Whether to hint that a restart or re-entry flow is needed |
| `IsValidMethod(MethodInfo method, Type valueType, out LogLevel? level, out string? errorMessage)` | Static method | Validates the onChanged callback |

### 5.3 `ConfigManager`

Namespace: `JmcModLib.Config`

| Member | Default / Signature | Description |
|---|---|---|
| `FlushOnSet` | `true` | Flushes immediately after each write |
| `AssemblyRegistered` | event | Event fired after Assembly config entries are registered |
| `AssemblyUnregistered` | event | Assembly config cleanup event |
| `EntryRegistered` | event | Single config entry registration event |
| `ValueChanged` | event | Config value changed event |
| `IsInitialized` | bool | Whether it has been initialized |
| `Init()` | None | Initializes AttributeRouter and default handlers |
| `Dispose()` | None | Clears all config registrations |
| `SetStorage(IConfigStorage storage, Assembly? assembly = null)` | assembly inferred automatically | Sets Assembly storage |
| `GetStorage(Assembly? assembly = null)` | assembly inferred automatically | Gets storage; defaults to Newtonsoft when unset |
| `CreateStorageKey(Type declaringType, string memberName)` | None | Generates `FullName.Member` |
| `CreateKey(string storageKey, string group = ConfigAttribute.DefaultGroup)` | Default group | Generates `group.storageKey` |
| `Flush(Assembly? assembly = null)` | assembly inferred automatically | Flushes to disk |
| `GetEntries(Assembly? assembly = null)` | assembly inferred automatically | Gets entries sorted by order/displayName |
| `GetEntries(string group, Assembly? assembly = null)` | group required | Gets config entries in the specified group |
| `GetGroups(Assembly? assembly = null)` | assembly inferred automatically | Gets group names |
| `TryGetEntry(string key, out ConfigEntry? entry, Assembly? assembly = null)` | assembly inferred automatically | Looks up a config entry |
| `GetValue(string key, Assembly? assembly = null)` | assembly inferred automatically | Gets a value; returns null when not found |
| `SetValue(string key, object? value, Assembly? assembly = null)` | assembly inferred automatically | Sets a value and returns whether it succeeded |
| `ResetAssembly(Assembly? assembly = null)` | assembly inferred automatically | Resets all Assembly config to default values |
| `RegisterConfig<TValue>(...)` | See below | Manually registers config |
| `RegisterButton(...)` | See the button section | Manually registers a button |
| `Unregister(Assembly? assembly = null)` | assembly inferred automatically | Cleans up Assembly config, input actions, and storage |

Full manual config signature:

```csharp
string RegisterConfig<TValue>(
    string displayName,
    Func<TValue> getter,
    Action<TValue> setter,
    string group = ConfigAttribute.DefaultGroup,
    Action<TValue>? onChanged = null,
    UIConfigAttribute? uiAttribute = null,
    string? storageKey = null,
    string? locTable = null,
    string? displayNameKey = null,
    string? groupKey = null,
    string? description = null,
    string? descriptionKey = null,
    int order = 0,
    bool restartRequired = false,
    Assembly? assembly = null)
```

Manual registration is suitable for dynamic config. Production MODs should pass `storageKey` explicitly.

### 5.4 `ConfigEntry` / `ConfigEntry<TValue>`

Namespace: `JmcModLib.Config.Entry`

| Member | Description |
|---|---|
| `Assembly` | Owning Assembly |
| `StorageKey` | Persistence key, without group |
| `Group` | Group |
| `DisplayName` | Fallback display name |
| `Key` | `CreateKey(StorageKey, Group)` |
| `Attribute` | Config metadata |
| `UIAttribute` | UI metadata, may be null |
| `SourceDeclaringType` | Attribute source type, may be null |
| `SourceMemberName` | Attribute source member name, may be null |
| `ValueType` | Value type |
| `DefaultValue` | Default value |
| `GetValue()` | Reads the current source value |
| `SetValue(object? value)` | Converts and sets the value |
| `Reset()` | Resets to the default value |
| `ValueChanged` | Entry-level change event |
| `CreateStorageKey(Type declaringType, string memberName)` | Static key generation |
| `CreateKey(string storageKey, string group = DefaultGroup)` | Full key generation |

Additional members on `ConfigEntry<TValue>`:

| Member | Description |
|---|---|
| `DefaultValueTyped` | Strongly typed default value |
| `GetTypedValue()` | Strongly typed read |
| `SetTypedValue(TValue value)` | Strongly typed set |

### 5.5 `IConfigStorage`

Namespace: `JmcModLib.Config.Storage`

| Method | Description |
|---|---|
| `GetFileName(Assembly? assembly = null)` | Gets the file name |
| `GetFilePath(Assembly? assembly = null)` | Gets the full path |
| `Exists(Assembly? assembly = null)` | Whether the file exists |
| `Save(string key, string group, object? value, Assembly? assembly = null)` | Saves to cache and marks dirty |
| `TryLoad(string key, string group, Type valueType, out object? value, Assembly? assembly = null)` | Tries to read and deserialize |
| `Flush(Assembly? assembly = null)` | Writes to disk |

### 5.6 `NewtonsoftConfigStorage` / `JsonConfigStorage`

Constructors:

```csharp
new NewtonsoftConfigStorage(string? rootDirectory = null)
new JsonConfigStorage(string? rootDirectory = null)
```

Both implement `IConfigStorage`. When the default root is empty, `OS.GetUserDataDir()/mods/config` is used. The default storage is `NewtonsoftConfigStorage`, which is more tolerant of complex types; `JsonConfigStorage` is lighter, but compatibility with complex types needs extra validation.

---

## 6. Config UI: Settings UI Attributes

### 6.1 Lifecycle Diagram

```mermaid
flowchart TD
    A[Field/property has Config + UIConfigAttribute] --> B[AttributeRouter scans]
    B --> C[ConfigManager.BuildConfigEntry]
    C --> D[ValidateUiAttribute]
    D --> E[ConfigEntry registration]
    E --> F[ModSettingsTabBridge injects Settings Tab]
    F --> G[ModSettingsPanel reads entries/groups]
    G --> H{UIAttribute type}
    H -- UIToggle --> I[Tickbox]
    H -- UIInput --> J[Input]
    H -- UISlider --> K[Slider]
    H -- UIDropdown --> L[Dropdown]
    H -- UIKeybind --> M[Keybind Button]
    H -- UIColor --> N[Color Picker]
    I --> O[User changes value]
    J --> O
    K --> O
    L --> O
    M --> O
    N --> O
    O --> P[ConfigManager.SetValue]
    P --> Q[Write back + persist + refresh UI]
```

### 6.2 `UIButtonAttribute`

Constructor:

```csharp
[UIButton(string description, string buttonText = "按钮", string group = ConfigAttribute.DefaultGroup)]
```

| Property | Default | Description |
|---|---:|---|
| `Description` | Constructor parameter | Row display name/description |
| `ButtonText` | `"按钮"` | Button text |
| `Group` | `DefaultGroup` | Group |
| `Key` | `null` | Storage key, inferred from method name when empty |
| `LocTable` | `null` | Localization table |
| `DisplayNameKey` | `null` | Display name key |
| `DescriptionKey` | `null` | Help text key |
| `ButtonTextKey` | `null` | Button text key |
| `GroupKey` | `null` | Group key |
| `Color` | `UIButtonColor.Default` | Button color |
| `Order` | `0` | Sort order |
| `HelpText` | `null` | Hover help text |
| `IsValidMethod` | Static | Requires static parameterless method; non-void return values warn |

### 6.3 UI Attribute Overview

| Attribute | Constructor / Default | Supported Types | Description |
|---|---|---|---|
| `UIConfigAttribute` | Abstract base class | Any | Base class for UI metadata |
| `UIConfigAttribute<TValue>` | Abstract generic base class | Exact `TValue` | Automatically validates value type |
| `UIToggleAttribute` | None | `bool` | Checkbox |
| `UIKeybindAttribute` | `(bool allowController = false, bool allowKeyboard = true)` | `Godot.Key` or `JmcKeyBinding` | Key binding; controller support requires `JmcKeyBinding`; use `JKB` unless there is a reason not to |
| `UIInputAttribute` | `(int characterLimit = 0, bool multiline = false)` | `string` | Text input |
| `UIColorAttribute` | `(params string[] presets)` | `Godot.Color` | Color selection; defaults to `Palette=Game`, `AllowCustom=true`, `AllowAlpha=true` |
| `UISliderAttribute` | `(double min, double max, double step = 1.0)` | Numeric types | General numeric slider |
| `UIIntSliderAttribute` | `(int min, int max, int characterLimit = 5)` | `int` | int slider |
| `UIDropdownAttribute` | `(params string[]? exclude)` | `string` or enum | strings are used as options; enum values are used as exclusions |

Enums:

```csharp
public enum UIButtonColor { Default, Green, Red, Gold, Blue, Reset }
public enum UIColorPalette { None, Basic, Game, CardRarity, Rainbow }
```

Interface:

```csharp
public interface ISliderConfigAttribute
{
    double Min { get; }
    double Max { get; }
    double Step { get; }
}
```

---

## 7. Hotkey / Input: Hotkeys and Input

### 7.1 Lifecycle Diagram

```mermaid
flowchart TD
    A[ConfigManager.Init registers Hotkey handlers] --> B[AttributeRouter scans methods]
    B --> C{Attribute}
    C -- JmcHotkey --> D[Find bindingMember field/property]
    D --> E[Create bindingGetter]
    E --> F[JmcHotkeyManager.Register]
    C -- UIHotkey --> G[Create JmcKeyBinding config entry]
    G --> H[Register ConfigEntry]
    H --> F
    F --> I[JmcHotkeyManager.Init]
    I --> J[JmcInputManager.Initialize]
    J --> K[Install JmcHotkeyInputRelay]
    K --> L[Godot _Input/_UnhandledInput]
    J --> M[Steam/Godot backend Process each frame]
    L --> N[HandleInput]
    M --> N
    N --> O[Match JmcKeyBinding]
    O --> P[Debounce/ConsumeInput]
    P --> Q[action.Invoke]
```

### 7.2 `JmcKeyModifiers`

Namespace: `JmcModLib.Config.UI`

```csharp
[Flags]
public enum JmcKeyModifiers
{
    None = 0,
    Ctrl = 1,
    Shift = 2,
    Alt = 4,
    Meta = 8
}
```

### 7.3 `JmcKeyBinding`

Namespace: `JmcModLib.Config.UI`

Constructors:

```csharp
new JmcKeyBinding()
new JmcKeyBinding(Key keyboard)
new JmcKeyBinding(Key keyboard = Key.None, string controller = "", JmcKeyModifiers modifiers = JmcKeyModifiers.None, bool enabled = true)
new JmcKeyBinding(Key keyboard, string controller, JmcKeyModifiers modifiers)
new JmcKeyBinding(Key keyboard, JmcKeyModifiers modifiers, bool enabled = true)
```

| Member | Description |
|---|---|
| `Keyboard` | Keyboard key; `Key.None` means there is no keyboard binding |
| `Controller` | Controller action name |
| `Modifiers` | Modifier key combination |
| `Enabled` | Whether enabled; the default struct is also treated as enabled |
| `HasKeyboard` | Whether it has a keyboard binding |
| `HasModifiers` | Whether it has modifier keys |
| `HasController` | Whether it has a controller action |
| `WithKeyboard(Key keyboard)` | Replaces the keyboard key and clears modifiers |
| `WithKeyboard(Key keyboard, JmcKeyModifiers modifiers)` | Replaces the keyboard key and modifiers |
| `WithController(string? controller)` | Replaces the controller action |
| `WithEnabled(bool enabled)` | Changes enabled state |
| `IsPressed(InputEvent inputEvent, bool allowEcho = false, bool exactModifiers = true)` | Checks whether the input event triggers the binding |
| `IsReleased(InputEvent inputEvent)` | Checks for release |
| `IsDown(bool exactModifiers = true)` | Whether it is currently down |
| `implicit operator JmcKeyBinding(Key keyboard)` | Implicitly creates from `Key` |
| `static IsPressed(Key keyboard, InputEvent inputEvent, bool allowEcho = false)` | Static convenience method |
| `static IsReleased(Key keyboard, InputEvent inputEvent)` | Static convenience method |
| `ToKeyboardText()` | Human-readable keyboard binding text |
| `ToString()` | Keyboard/controller combined text |
| `ReadModifiers(InputEventKey keyEvent)` | Reads modifiers from an event |
| `ReadCurrentModifiers()` | Reads currently pressed modifiers |
| `IsModifierKey(Key key)` | Whether the key is a modifier key |
| `ReadKey(InputEventKey keyEvent)` | Reads the actual keycode |

### 7.4 `JmcHotkeyAttribute`

```csharp
[JmcHotkey(string bindingMember)]
```

| Property | Default | Description |
|---|---:|---|
| `BindingMember` | Constructor parameter | Static field/property name that stores `Key` or `JmcKeyBinding` |
| `Key` | `null` | Hotkey registration key; inferred from method name when empty |
| `ConsumeInput` | `true` | Consumes input after triggering |
| `ExactModifiers` | `true` | Whether to disallow extra modifiers; `true` means modifiers must match exactly, while `false` only requires the configured modifiers to be included |
| `AllowEcho` | `false` | Whether repeated echo input from holding a keyboard key can trigger again |
| `DebounceMs` | `150` | Debounce in milliseconds |

Methods must be static and parameterless; return values are ignored.

### 7.5 `UIHotkeyAttribute`

```csharp
[UIHotkey(string displayName, string group = ConfigAttribute.DefaultGroup)]
```

| Property | Default | Description |
|---|---:|---|
| `DisplayName` | Constructor parameter | Display name in the settings UI |
| `Group` | `DefaultGroup` | Group |
| `Key` | `null` | Base config key / hotkey key |
| `Description` | `null` | Description |
| `LocTable` | `null` | Localization table |
| `DisplayNameKey` | `null` | Display name key |
| `DescriptionKey` | `null` | Description key |
| `GroupKey` | `null` | Group key |
| `Order` | `0` | Sort order |
| `RestartRequired` | `false` | Whether to indicate a restart is required |
| `DefaultKeyboard` | `Key.None` | Default keyboard key |
| `DefaultModifiers` | `None` | Default modifiers |
| `DefaultController` | `""` | Default controller action |
| `AllowKeyboard` | `true` | Allows keyboard binding |
| `AllowController` | `false` | Allows controller binding |
| `ConsumeInput` | `true` | Consumes input after triggering |
| `ExactModifiers` | `true` | Whether to disallow extra modifiers; `true` means modifiers must match exactly, while `false` only requires the configured modifiers to be included |
| `AllowEcho` | `false` | Whether repeated echo input from holding a keyboard key can trigger again |
| `DebounceMs` | `150` | Debounce |

### 7.6 `JmcHotkeyManager`

| Member | Description |
|---|---|
| `IsInitialized` | Whether the hotkey system is initialized |
| `Init()` | Initializes the input backend and unregister events |
| `Register(string key, Func<JmcKeyBinding> bindingGetter, Action action, bool consumeInput = true, bool exactModifiers = true, bool allowEcho = false, ulong debounceMs = 150, Assembly? assembly = null)` | Registers a dynamically bound hotkey |
| `Register(string key, Func<Key> keyGetter, Action action, bool consumeInput = true, bool exactModifiers = true, bool allowEcho = false, ulong debounceMs = 150, Assembly? assembly = null)` | Registers a keyboard hotkey |
| `Unregister(string key, Assembly? assembly = null)` | Unregisters a single hotkey |
| `UnregisterAssembly(Assembly? assembly = null)` | Unregisters all hotkeys under an Assembly |

### 7.7 `HotkeyOptions`

```csharp
public readonly record struct HotkeyOptions(
    bool ConsumeInput = true,
    bool ExactModifiers = true,
    bool AllowEcho = false,
    ulong DebounceMs = 150);
```

---

## 8. Steam Input Integration

### 8.1 Lifecycle Diagram

```mermaid
flowchart TD
    A[Config entry uses UIKeybind AllowController=true] --> B[JmcInputActionRegistry.RegisterConfigEntry]
    B --> C[Hotkey handler binds hotkeyKey and entryKey/sourceMember]
    C --> D[ActionsChanged]
    D --> E[SteamInputManifestInstaller / Patches]
    E --> F[Read original Steam Input manifest]
    F --> G[SteamInputManifestMerger merges JML actions]
    G --> H[Generate/install merged manifest]
    H --> I[SteamInputBackend initializes action handles]
    I --> J[Poll digital action each frame]
    J --> K[Convert to JmcKeyBinding controller press/release]
    K --> L[JmcHotkeyManager.HandleInput]
```

Most Steam Input-related types are internal. The public API is exposed indirectly mainly through `UIKeybind(allowController: true)`, `JmcKeyBinding.Controller`, and `UIHotkey.AllowController`. Child MODs should not depend directly on the internal installer/merger.

---

## 9. Logger: Logging

### 9.1 Lifecycle Diagram

```mermaid
flowchart TD
    A[ModRegistry.Register] --> B[ModLogger.RegisterAssembly]
    B --> C[Create AssemblyLogConfiguration]
    C --> D[Read ModRegistry LoggerContext]
    D --> E[MOD calls Debug/Info/Warn/Error/Fatal]
    E --> F[Add Prefix / Exception Details]
    F --> G[Forward to STS2 Logger]
    G --> H[Display controlled by native STS2 level]
    H --> I{Fatal && ThrowOnFatal?}
    I -- yes --> J[Throw exception]
    I -- no --> K[End]
    B --> M[Cleanup on UnregisterAssembly]
```

JML does not maintain a minimum display level. To adjust log display, use native STS2 developer console commands, such as `log Debug` or `log Generic Debug`.

### 9.2 Types and Members

Namespace: `JmcModLib.Utils`

```csharp
[Flags]
public enum LogPrefixFlags
{
    None = 0,
    Timestamp = 1,
    Default = Timestamp
}
```

`AssemblyLogConfiguration`:

| Property | Default |
|---|---:|
| `LogType` | `LogType.Generic` |
| `PrefixFlags` | `LogPrefixFlags.Default` |
| `ThrowOnFatal` | `true` |
| `IncludeExceptionDetails` | `true` |

`LoggerSnapshot`:

```csharp
public readonly record struct LoggerSnapshot(
    LogType LogType,
    LogPrefixFlags PrefixFlags,
    bool ThrowOnFatal,
    bool IncludeExceptionDetails,
    string Context);
```

`ModLogger`:

| Member | Description |
|---|---|
| `DefaultLogType` | Default Generic |
| `DefaultPrefixFlags` | Default Timestamp |
| `DefaultThrowOnFatal` | Default true |
| `DefaultIncludeExceptionDetails` | Default true |
| `RegisterAssembly(Assembly? assembly = null, LogPrefixFlags prefixFlags = LogPrefixFlags.Default, bool throwOnFatal = true, LogType logType = LogType.Generic, bool includeExceptionDetails = true)` | Registers Assembly log configuration |
| `UnregisterAssembly(Assembly? assembly = null)` | Clears log configuration |
| `GetLogType/SetLogType` | Reads/sets STS2 log type |
| `GetPrefixFlags/SetPrefixFlags` | Reads/sets prefix |
| `HasPrefixFlag/TogglePrefixFlag` | Checks/toggles a prefix flag |
| `GetSnapshot` | Gets the current configuration snapshot |
| `Load/Trace/Debug/Info/Warn/Error/Fatal` | Log output methods |
| `Warn(string message, Exception exception, Assembly? assembly = null)` | warn with exception |
| `Error(string message, Exception exception, Assembly? assembly = null)` | error with exception |
| `Fatal(Exception exception, string? message = null, Assembly? assembly = null)` | fatal, may throw |

---

## 10. L10n: Localization

### 10.1 Lifecycle Diagram

```mermaid
flowchart TD
    A[Call L10n.Resolve / ConfigLocalization] --> B[Determine table/key]
    B --> C{Does key include table/key?}
    C -- yes --> D[Split table and key]
    C -- no --> E[Use DefaultTable]
    D --> F[LocString.Exists]
    E --> F
    F -- false --> G[Return fallback]
    F -- true --> H[LocString.GetFormattedText]
    H --> I[Return text]
    A --> J[TryResolveForLanguage]
    J --> K[Read res://<pck>/localization/<lang>/<table>.json]
    K --> L[Fallback to eng if current language fails]
```

### 10.2 `L10n`

Namespace: `JmcModLib.Utils`

| Member | Description |
|---|---|
| `FallbackLanguage = "eng"` | Fallback language |
| `DefaultTable = "settings_ui"` | Default table |
| `SupportedLanguages` | STS2 supported language list |
| `CurrentLanguage` | Current language; falls back to `eng` on failure |
| `GetModLocalizationRoot(Assembly? assembly = null)` | `res://<pck>/localization` |
| `GetModLocalizationDirectory(string? language = null, Assembly? assembly = null)` | Directory for the specified language |
| `GetModTablePath(string fileName, string? language = null, Assembly? assembly = null)` | Table file path, automatically appending `.json` |
| `HasModTable(string fileName, string? language = null, Assembly? assembly = null)` | Whether the resource exists |
| `EnumerateExistingModTablePaths(string fileName, Assembly? assembly = null)` | Current-language and fallback paths |
| `Create(string table, string key, Action<LocString>? configure = null)` | Creates a `LocString` |
| `CreateIfExists(string table, string key, Action<LocString>? configure = null)` | Creates one only if it exists |
| `Exists(string table, string key)` | Whether the key exists |
| `TryGetFormattedText(string table, string key, out string? text, Action<LocString>? configure = null, Assembly? assembly = null)` | Tries to format |
| `Resolve(string? key, string? fallback = null, string? table = null, Assembly? assembly = null, Action<LocString>? configure = null)` | Resolves text; returns fallback/empty string on failure |
| `ResolveAny(IEnumerable<string?> keys, string? fallback = null, string? table = null, Assembly? assembly = null, Action<LocString>? configure = null)` | Multi-key fallback |
| `ResolvePath(string? path, string? fallback = null, Assembly? assembly = null, Action<LocString>? configure = null)` | Resolves using the default table |
| `TryResolve(string? key, out string text, string? table = null, Assembly? assembly = null, Action<LocString>? configure = null)` | Tries to resolve |
| `GetFormattedText(string table, string key, Action<LocString>? configure = null)` | Direct formatting |
| `GetRawText(string table, string key)` | Raw text |
| `SubscribeToLocaleChange(LocManager.LocaleChangeCallback callback)` | Subscribes to locale changes |
| `UnsubscribeToLocaleChange(LocManager.LocaleChangeCallback callback)` | Unsubscribes from locale changes |

---

## 11. Reflection: Reflection Accessors

### 11.1 Lifecycle Diagram

```mermaid
flowchart TD
    A[Call Type/Member/MethodAccessor.Get] --> B[Check cache]
    B -- hit --> C[Return accessor]
    B -- miss --> D[Reflect MemberInfo]
    D --> E[Validate supported type]
    E --> F[Build dynamic method/expression/delegate]
    F --> G[Cache accessor]
    G --> H[GetValue/SetValue/Invoke]
    H --> I[AttributeRouter uses GetAll to scan]
```

### 11.2 `ReflectionAccessorBase`

Namespace: `JmcModLib.Reflection`

| Member | Description |
|---|---|
| `DefaultFlags` | `Instance | Static | Public | NonPublic` |
| `Name` | Member name |
| `DeclaringType` | Declaring type |
| `IsStatic` | Whether static |
| `IsSaveOwner(Type? declaringType)` | Determines whether the type is suitable as an owner |
| `GetAttribute<T>()` | Gets a single Attribute |
| `HasAttribute<T>()` | Whether it has an Attribute |
| `GetAttributes(Type? type = null)` | Gets Attributes |
| `GetAllAttributes()` | Gets all Attributes |

Generic base class `ReflectionAccessorBase<TMemberInfo,TAccessor>`:

| Member | Description |
|---|---|
| `CacheCount` | Cache count |
| `ClearCache()` | Clears cache |
| `MemberInfo` | Original `MemberInfo` |

### 11.3 `TypeAccessor`

| Member | Description |
|---|---|
| `Type` | Original `Type` |
| `new TypeAccessor(Type type)` | Constructs and detects static classes |
| `Get(Type type)` | Gets from cache |
| `Get<T>()` | Generic get |
| `GetAll(Assembly asm)` | Gets all safe types in an Assembly |
| `CreateInstance()` | Creates an instance with a parameterless constructor; returns null and logs on failure |
| `CreateInstance(params object?[] args)` | Constructor with arguments |
| `CreateInstance<T>() where T : class` | Generic create |

### 11.4 `MemberAccessor`

| Member | Description |
|---|---|
| `CanRead` / `CanWrite` | Whether readable/writable |
| `ValueType` | Field/property type |
| `MemberType` | Field/Property |
| `TypedGetter` / `TypedSetter` | Strongly typed delegates; may be null for indexers/ref-like cases |
| `GetValue(object? target)` | Reads a non-indexed member |
| `SetValue(object? target, object? value)` | Writes a non-indexed member |
| `GetValue(object? target, params object?[] indexArgs)` | Reads an indexer |
| `SetValue(object? target, object? value, params object?[] indexArgs)` | Writes an indexer |
| `GetValue<TTarget,TValue>(TTarget target)` | Generic instance read |
| `SetValue<TTarget,TValue>(TTarget target, TValue value)` | Generic instance write |
| `GetValue<TValue>()` | Static read |
| `SetValue<TValue>(TValue value)` | Static write |
| `Get(Type type, string memberName)` | Finds a field/property by name |
| `GetIndexer(Type type, params Type[] parameterTypes)` | Finds an indexer by index parameter types |
| `Get(MemberInfo member)` | Gets by MemberInfo |
| `GetAll(Type type, BindingFlags flags = DefaultFlags)` | Gets all fields/properties |
| `GetAll<T>(BindingFlags flags = DefaultFlags)` | Generic get-all |

### 11.5 `MethodAccessor`

| Member | Description |
|---|---|
| `IsStatic` | Whether static |
| `TypedDelegate` | Strongly typed delegate; may be null for some complex methods |
| `Get(MethodInfo method)` | Gets an accessor |
| `GetTypedDelegate(MethodInfo method)` | Gets a strongly typed delegate |
| `GetAll(Type type, BindingFlags flags = DefaultFlags)` | Gets all methods |
| `GetAll<T>(BindingFlags flags = DefaultFlags)` | Generic get-all methods |
| `Get(Type type, string methodName, Type[]? parameterTypes = null)` | Finds a method by name/parameters |
| `MakeGeneric(params Type[] genericTypes)` | Constructs a generic method accessor |
| `Invoke(object? instance, params object?[] args)` | General invocation |
| `Invoke(object? instance)` / `Invoke(instance, a0/a1/a2)` | 0-3 argument convenience invocation |
| `Invoke<TTarget,TResult>(TTarget instance)` | Generic instance invocation |
| `Invoke<TTarget,T1,TResult>(...)` | 1-argument generic instance invocation |
| `Invoke<TTarget,T1,T2,TResult>(...)` | 2-argument generic instance invocation |
| `Invoke<TTarget,T1,T2,T3,TResult>(...)` | 3-argument generic instance invocation |
| `InvokeVoid<TTarget>(...)` | void instance invocation |
| `InvokeVoid<TTarget,T1/T2/T3>(...)` | 1-3 argument void instance invocation |
| `InvokeStatic<TResult>()` | Static return-value invocation |
| `InvokeStatic<T1,TResult>(...)` | 1-argument static return-value invocation |
| `InvokeStatic<T1,T2,TResult>(...)` | 2-argument static return-value invocation |
| `InvokeStatic<T1,T2,T3,TResult>(...)` | 3-argument static return-value invocation |
| `InvokeStaticVoid()` | Static void invocation |
| `InvokeStaticVoid<T1/T2/T3>(...)` | 1-3 argument static void invocation |

### 11.6 `ExprHelper`

Namespace: `JmcModLib.Utils`. The source file is `Utils/ExprHelper.cs`; functionally, it still belongs to reflection access helpers.

| Member | Description |
|---|---|
| `EnableCache` | Whether cache is enabled; default true |
| `AccessMode` | Accessor generation mode; default `MemberAccessMode.Default` |
| `MemberAccessMode` | `Reflection` / `ExpressionTree` / `Emit` / `Default=Emit` |
| `MemberAccessors(Delegate Getter, Delegate Setter)` | Accessor record |
| `GetOrCreateAccessors<T>(Expression<Func<T>> expr, Assembly? assembly = null)` | Gets getter/setter from an expression |
| `GetOrCreateAccessors<T>(Expression<Func<T>> expr, out bool cacheHit, Assembly? assembly = null)` | Includes cache-hit output |
| `ClearAssemblyCache(Assembly? assembly = null)` | Clears cache for the specified Assembly |

---

## 12. Prefabs: Popups

### 12.1 Lifecycle Diagram

```mermaid
flowchart TD
    A[Call ShowConfirmationAsync / ShowMessageAsync] --> B{NModalContainer available?}
    B -- no --> C[Return default/failure result or log]
    B -- yes --> D[Construct Modal content]
    D --> E[Resolve string or LocString]
    E --> F[OpenModal]
    F --> G[User clicks Confirm/Cancel/OK]
    G --> H[TaskCompletionSource completes]
    H --> I[Caller await returns]
```

### 12.2 `JmcConfirmationPopup`

Namespace: `JmcModLib.Prefabs`. Source files are under `Prefabs/`.

| Member | Description |
|---|---|
| `IsAvailable` | Whether a modal can currently be shown |
| `ShowConfirmationAsync(string title, string body, string? confirmText = null, string? cancelText = null, bool showBackstop = true, Assembly? assembly = null)` | Confirm/cancel popup, returns bool |
| `ShowMessageAsync(string title, string body, string? okText = null, bool showBackstop = true, Assembly? assembly = null)` | OK message popup |
| `LocString` overloads | Supports localized string parameters |

---

## 13. Build / Deploy and Child MOD Props

### 13.1 Lifecycle Diagram

```mermaid
flowchart TD
    A[dotnet build JmcModLib] --> B[EnsureGodotProjectFiles]
    B --> C[Generate/check project.godot / export_presets / manifest]
    C --> D[BuildAndDeploy]
    D --> E[Build Bootstrap]
    E --> F[Copy Runtime as JmcModLib.Runtime.dll]
    F --> G[Copy Bootstrap as JmcModLib.dll]
    G --> H[Copy XML / Newtonsoft / references]
    H --> I[Godot export-pack generates pck]
    I --> J[robocopy to STS2 mods/JmcModLib]
    J --> K{LaunchGamePrompt?}
    K -- true --> L[PowerShell popup starts game]
    K -- false --> M[End]
```

### 13.2 Current MSBuild Key Points

JML main project:

- `TargetFramework=net9.0`
- `GenerateDocumentationFile=true`
- The default local paths are Windows personal paths and should preferably be localized into user props.
- `BuildAndDeploy` builds both Bootstrap and Runtime.
- Runtime is copied as `JmcModLib.Runtime.dll`; Bootstrap is copied as `JmcModLib.dll`.

Child MOD props:

```xml
<Project>
  <PropertyGroup>
    <JmcModLibPublishDir Condition="'$(JmcModLibPublishDir)' == ''">$(MSBuildThisFileDirectory)</JmcModLibPublishDir>
    <JmcModLibRoot Condition="'$(JmcModLibRoot)' == ''">$(JmcModLibPublishDir)</JmcModLibRoot>
    <JmcModLibRuntimePath Condition="'$(JmcModLibRuntimePath)' == ''">$(JmcModLibPublishDir)JmcModLib.Runtime.dll</JmcModLibRuntimePath>
  </PropertyGroup>

  <ItemGroup>
    <Reference Include="JmcModLib">
      <HintPath>$(JmcModLibRuntimePath)</HintPath>
      <Private>false</Private>
    </Reference>
  </ItemGroup>
</Project>
```

---

## 14. Default Parameter Semantics Index

| Default Parameter | API | Semantics | Documentation Guidance |
|---|---|---|---|
| `assembly = null` | Most APIs | Resolves the caller Assembly through the call stack | Can be omitted at entry points; pass explicitly in helpers |
| `displayName/version = null` | `ModRegistry.Register` | Falls back from manifest / Assembly | Omit for ordinary MODs |
| `deferredCompletion = bool` | `Register<T>(bool)` | true returns builder, false immediately calls Done | Prefer adding semantic overloads |
| `group = DefaultGroup` | Config/UI/Button | Default group | UI layer should preferably localize it as a regular group |
| `storageKey = null` | Manual config/button | Derived from display text | Production MODs should not omit it |
| `Key = null` | Attribute config/button/hotkey | Inferred from type/member/method | Prefer explicit stable keys after release |
| `buttonText = "按钮"` | Button | Button fallback text | Prefer localization or a neutral English default |
| `FlushOnSet = true` | ConfigManager | Writes to disk on every SetValue | Consider debounce for high-frequency sliders |
| `allowKeyboard = true` | UIKeybind/UIHotkey | Keyboard binding enabled by default | Reasonable |
| `allowController = false` | UIKeybind/UIHotkey | Controller binding disabled by default | Reasonable |
| `ConsumeInput = true` | Hotkey | Consumes input after triggering | Reasonable for action hotkeys; set false for debug hotkeys |
| `ExactModifiers = true` | Hotkey | Disallows extra modifiers | Avoids `Ctrl + F8` accidentally triggering `F8` |
| `AllowEcho = false` | Hotkey | Does not react to repeated hold input | Reasonable for ordinary action hotkeys; repeated actions may set true |
| `DebounceMs = 150` | Hotkey | 150ms debounce | Reasonable |
| `FallbackLanguage = eng` | L10n | English fallback | Reasonable |
| `DefaultTable = settings_ui` | L10n | Default settings table | Reasonable |

---

## 15. Inter-Module Dependency Overview

```mermaid
flowchart LR
    Core[Core: ModRegistry/Runtime] --> Config[ConfigManager]
    Core --> Logger[ModLogger]
    Config --> Router[AttributeRouter]
    Router --> Reflection[Reflection Accessors]
    Config --> Storage[Config Storage]
    Config --> UI[Settings UI]
    Config --> Input[Input/Hotkeys]
    Input --> Steam[Steam Input]
    UI --> L10n[L10n]
    UI --> Prefabs[Prefabs]
    Logger --> Core
    Reflection --> Logger
```

The most important architectural direction is to keep `Core` light, `Config` stable, and `Input/UI` replaceable. Game UI and Steam Input are both externally volatile areas, so hard-coded selectors and manifest merging logic should be isolated in internal layers.
