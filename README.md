## olmod - Overload mod

**Community mods for Overload**

[Overload](https://playoverload.com) is a registered trademark of [Revival Productions, LLC](https://www.revivalprod.com).
This is an unaffiliated, unsupported tool. Use at your own risk.

#### How to run

- If you installed Overload using Steam or GOG Galaxy you can extract olmod
  anywhere and it will find Overload automatically.

- If you installed Overload yourself or on linux, extract olmod in the Overload main
  directory (where `Overload.exe` / `Overload.x86_64` is also located).
  
- Run `olmod.exe` / `olmod.sh` (linux) instead of `Overload.exe`

- Use `olmodserver.bat` / `olmodserver.sh` (linux) to start a server

#### What does it do

- Allows access to the unfinished Monsterball multiplayer mode (with tweaks
  by terminal).
  The Monsterball multiplayer mode only works when both server and clients run olmod.

- Puts a MP player in observer mode if it uses a pilot name starting with
  OBSERVER.
  The observer mode only works when the server and the observer client run olmod.

- Reads projdata.txt / robotdata.txt with custom projectile (weapon) and
  robot settings. You can extract the stock data from the game with the
  included tool `olgetdata`. The txt files must be in the same directory as
  olmod.exe. You can run olgetdata on linux with `mono olgetdata.exe`.

- Adds `frametime` non-cheat code and `rearview` cheat code

- Allows MP with up to 16 players / 8 teams (server and all clients must run olmod)

- Writes match log files on server (by luponix)

- Fixes MP client homing behaviour (by terminal)

- Allows shoot to open doors and disables door open when nearby in MP

- Allows pasting in the MP password field

- Allows joining in progress matches when enabled for the match

- Adds option to enable console with ` key

- Adds console commands: xp, reload_missions, mipmap_bias, ui_color

- Allows custom music in custom levels

- Allows powerup spawn data for custom MP levels

- Adds Internet match option with built in olproxy

- Adds -frametime command line option

- Adds custom mod loading, add Mod-xxx.dll assembly to olmod directory

- Automatically downloads MP levels from overloadmaps.com

- Adds MP level select screen, sorts MP level list

- Disables weapon speed/lifetime randomization in LAN/Internet MP

- Adds MP Capture The Flag mode

- MP Prev weapon switch fix from Tobias

- Adds support for some missing textures (crystal) and props (fans, monitors) in custom levels

#### How to build

- Open solution in Visual Studio 2017

- Fix references in GameMod project

  If your Overload copy is not in
  `C:\Program Files (x86)\Steam\steamapps\common\Overload` you need to
  adjust the references to `UnityEngine.CoreModule.dll` and
  `Assembly-CSharp.dll`.
  They are in `Overload_Data\Managed`.

- Run

#### How does it work

The regular `Overload.exe` just runs `UnityMain` from `UnityPlayer.dll`.
The replacement `olmod.exe` also runs `UnityMain`, but intercepts calls from Unity to the mono C# engine
to also load `GameMod.dll`. The file `GameMod.dll` contains a C# class that
uses [Harmony](https://github.com/pardeike/Harmony) to modify the game
scripting code in memory.
