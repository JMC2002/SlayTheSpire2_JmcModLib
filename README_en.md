**🌐[ [中文](README.md) | English ]**

[📝 Changelog](CHANGELOG_en.md)

[📦 Releases](https://github.com/JMC2002/SlayTheSpire2_JmcModLib/releases)

# JmcModLib
## 0. Installation

### Installing the mod
For the Steam version, subscribe through the Steam Workshop once it is available (not public yet).

For other versions, you can build it yourself, or download the .zip from [📦 Releases](https://github.com/JMC2002/SlayTheSpire2_JmcModLib/releases) and extract it into the Mods folder under the game installation directory. Create the folder if it does not exist.

After installation, the directory should look like this:

```sh
-- Slay the Spire 2
    |-- SlayTheSpire2.exe
        |-- mods
             |-- JmcModLib
```

### Save migration
> When you install a MOD for the first time, the game separates modded saves from unmodded saves by default. You can migrate your saves with the steps below:

After installing the MOD, launch the game once. The game will ask whether to enable MODs. Enable them, launch the game one more time, then switch to the save location. Copy the corresponding save files from the numbered folder under `%appdata%\SlayTheSpire2\steam\` into that folder's `modded` folder, so the same saves can be used before and after enabling MODs.

After migration, the directory should look like this:

```sh
-- %appdata%\SlayTheSpire2
    |-- logs                                # Log folder
    |-- steam
        |-- <steamId>
             |-- profile1
             |-- profile2
             |-- profile3
             |-- modded
                  |-- profile1
                  |-- profile2
                  |-- profile3
```
---
## 🧠 1. Introduction
The core of this Mod comes from my [JmcModLib](https://github.com/JMC2002/JmcModLib) prerequisite library for Escape from Duckov. It mainly includes a configuration library, a reflection helper library, logging wrappers, and localization wrappers.

[Demo video (Bilibili) (not posted yet)]()

[Github repository](https://github.com/JMC2002/SlayTheSpire2_JmcModLib)
## ⚙️ 2. Features
- Provides a settings UI and configuration items that fit the game's native style, including rich-text tooltips, with native controller support.
![](./pic/配置.png)
![](./pic/配置2.png)
- Automatically scans and builds localized configuration items from a `setting_ui.json` file.
![](./pic/配置3.png)
- After a key binding is marked as controller-compatible, a Steam Input event is registered automatically. A restart is required the first time. The related localization text follows the same rules as configuration items.
![](./pic/Steam输入2.png)
![](./pic/Steam输入3.png)
- Provides several game-native prefabs.
![](./pic/确认框.png)
- A reflection helper library with built-in caching.
- Logging wrappers.
- Localization wrappers.

## 🔔 3. Other
- If you want to use this Mod, start with the [Quick Start](./docs/JML_QuickStart_en.md) and [API Reference](./docs/JML_API_Reference_en.md), and use them together with the [Demo](https://github.com/JMC2002/SlayTheSpire2_JmcModLibDemo).
- This Mod is still under active construction. If you want to use it, joining the [Discord server](https://discord.gg/peRD8SUxXg) or QQ group (617674584) is recommended.
- The core of this Mod comes from [JmcModLib](https://github.com/JMC2002/JmcModLib), with development assistance from CodeX.
- This Mod's documentation and localization text rely on AI-generated content. If you find any translation that feels off, suggestions are welcome.

## 🧩 4. Compatibility and Dependencies
- This Mod depends on `Newtonsoft.Json 13.0.4` and `Harmony`. The former has been published in the release menu.
- Since the game is still in EA, this Mod may stop working after game updates.

## 🧭 5. TODO
- Documentation is in progress.

**If you like this Mod, I would appreciate a star.**

If you are genuinely very rich, you can consider sponsoring me. Sponsoring me will not get you anything, but it might give me a good scare.

![图片描述](pic/wechat_qrcode.png)
