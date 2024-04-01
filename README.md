## olmod 0.5.13 - Overload mod

**Community mods for Overload**

[Overload](https://playoverload.com) is a registered trademark of [Revival Productions, LLC](https://www.revivalprod.com).
This is an unaffiliated, unsupported tool. Use at your own risk.

#### How to run

- Download the latest release from [olmod.overloadmaps.com](https://olmod.overloadmaps.com)

- Extract olmod in the Overload main directory
  (where `Overload.exe` / `Overload.x86_64` / `Overload.app` is also located).
  
- On linux / mac, execute `chmod +x olmod.sh` from the terminal after changing to the correct directory.
  For example after `cd "~/Library/Application Support/Steam/steamapps/common/Overload"`

- Run `olmod.exe` / `olmod.sh` (linux / mac) instead of `Overload.exe`

  On mac you might get a warning about `olmod.dylib`, you can add an exception in
  System Preferences, Security & Privacy, General.

- Use `olmodserver.bat` / `olmodserver.sh` (linux) to start a LAN server,
  use `olmodserverinet.bat` / `olmodserverinet.sh` (linux) to start an
  internet server. For the internet server you need to open UDP port range
  7000-8001

#### What does it do

- Allows access to the unfinished Monsterball multiplayer mode (with tweaks by terminal). The Monsterball multiplayer mode only works when both server and clients run olmod.

- Puts a MP player in observer mode if it uses a pilot name starting with OBSERVER. The observer mode only works when the server and the observer client run olmod.

- Reads `<level name>-projdata.txt` / `<level name>-robotdata.txt` for levels, `<mission name>-projdata.txt` / `<level name>-robotdata.txt` for missions, and `projdata.txt` / `robotdata.txt` in the olmod directory for testing.  These files can have custom projectile (weapon) and robot settings. You can extract the stock data from the game with the included tool `olgetdata`. The txt files must be in the same directory as olmod.exe. You can run olgetdata on linux with `mono olgetdata.exe`. They can either go in the olmod directory, or map makers can add them to their map .zip files.

- Adds a rearview option for all game modes, with an option to allow it in a multiplayer game

- Allows MP with up to 16 players / 8 teams (server and all clients must run olmod)

- Writes match log files on server (by luponix)

- Fixes MP client homing behaviour (by terminal)

- Allows shoot to open doors and disables door open when nearby in MP

- Allows pasting in the MP password field

- Allows joining in progress matches when enabled for the match

- Allows switching teams for team games with join in progress enabled, by Tobias

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

- Adds Capture The Flag and Race match mode (Race mode by Tobias)

- MP Prev weapon switch fix from Tobias

- Adds support for some missing textures (crystal) and props (fans, monitors) in custom levels

- MP server writes kill/damage log and optionally send it to tracker

- Always makes all MP loadouts available and uses Impulse instead of Reflex for Warrior

- Fixes missing doors and teleporters in MP levels

- Less strict item pickup for inverted segment MP levels, originally by terminal

- Adds MP presets by Tobias

- Allows more simultaneous sounds for large MP matches

- Adds server browser by Tobias

- Replaces default networking model with a sniper packets style, resulting in more consistent network play.  Older clients can still play with the newer clients, but won't experience the new networking model.

- Better synchronizes energy, ammo, weapon choice, missile counts, and devastator firing/triggering when using sniper packets.

- Shows you in the version whether you are running a modded version of olmod or not.

- Improves the primary spawning algorithm.

- Adds weapon and missile autoselect, by luponix.

- Adds a multiplayer game option to scale respawn time by team size, by Tobias.

- Adds a Classic Spawns game mode, which makes the game play like other 6DoF multiplayer games such as Descent 3, by Tobias.

- Adds an option to allow or disallow boosting when running the flag in CTF, by Tobias.

- Adds an option to allow or disallow specific modifiers for games, by Tobias.

- Adds lag compensation to make games feel more LAN-like at the expense of ships jumping around when ships change direction.  Highly customizable.  By Whollycow (ship lag compensation), roncli (weapon lag compensation), derhass (detailed analysis, code review, and improvements), and Tobias (menuing).

- Adds automap to multiplayer using the quick save key, by Tobias.

- Prevents quantization of ship positions on maps that stray too far from 0, 0, 0, by roncli & Whollycow

- Adds numerical sliders to some controller options, by Tobias.

- Fixes seizure-inducing flashing in VR, by roncli (thanks to Arne for finding the issue!).

- Makes player scoreboard sorting in team anarchy make more sense, by Tobias.

- Allows games where all ships can always be cloaked, by roncli.

- Allows projdata.txt files to be loaded by the player starting a multiplayer game.  You can have custom weapon balance for a game.  Check under advanced settings when creating a game.  By Tobias.

- Doubles the allotted time to enter a map at the beginning of a multiplayer match in an attempt to allow players with slower computers to start at the same time as everyone else, by roncli.

- Removes issue with servers failing to catch up on player inputs in some circumstances, by roncli.

- Smashmouth Overload!

- Updated weapon balance for multiplayer, by zero, roncli, & Tobias.

- Nerf to reduced shader cloaks down to 30% opacity, by Tobias.

- Options to reduce damage blur and damage color intensity, by Tobias.

- Improved spawn algorithm for respawning in anarchy and team anarchy, by roncli.

- Multiplayer match time limit is now selectable in one minute intervals, by Tobias.

- Main menu notification for when there is a new version of olmod, by marlowe.

- Observer mode health bars and damage indicators for pilots, by Tobias.

- In team anarchy, players now glow their team's color when taking damage, by Tobias.

- A joystick curve editor, by luponix.

- In anarchy or team anarchy, a multiplayer game option to turn off assist scoring, by Sirius.

- Custom team colors, by Tobias.

- Disables use of audio for servers, by derhass.

- Fixes jitteriness of network error correction, by derhass.

- Controller ramping options, by luponix.

- Death summary that shows what killed you, by Tobias.

- Increased UI opacity while cloaked, by Tobias.

- Option to reset audio engine, by Tobias.

- Option to move your missions to a directory of your choosing using the `-missionpath` parameter, by roncli.

- Option to have bigger and more colorful enemy names in anarchy, by luponix.

- Multiplayer chat commands, by derhass.

- Corpse desync fix, by Tobias.

- Option to disable profanity filter, by luponix.

- Options for framerate and velocity on the HUD, by Tobias.

- Ability to join games by LAN hostname, by derhass.

- Objects teleported by warpers are now oriented as expected, by kevin.

- 4 customizable multiplayer loadouts, with a reflex sidearm, by Tobias.

- Multiplayer option for team health bars, by roncli.

- Match option for thunderbolt ship penetration, by luponix.

- Match option for floating damage numbers, by roncli.

- Audio occlusion and stereo homing alerts, by Fireball.

- vr_scale console command to set the VR camera size, by roncli.

- New matcen HP cap calculation for level designers, by Kevin.

- Boss 2B available for use in single player campaigns, by Kevin.

- Audio taunts, by luponix.  See https://github.com/overload-development-community/olmod/wiki/Audio-taunts for details.

- Packet loss monitor, by Fireball.

- Colored creepers in team games, by Fireball.

- Options to use a different mesh collider in multiplayer, by Fireball.

- Frame limiting, by luponix.

- Multiplayer options shown in the lobby, by Fireball.

- Configurable cyclone spinup time, by roncli.

- Spawnpoint injection, by Fireball. See https://github.com/overload-development-community/olmod/wiki/Spawnpoint-Injection for details.

##### Linux

Build the shared object library:

```
$ cd linux
$ make olmod.so
```

Build the GameMod.dll file containing the game logic (requires the `mcs` compiler, usually found in packages `mono-dev` or `mono-mcs`):

```
$ cd linux
$ export OLPATH=/path/to/your/Overload/installation
$ make GameMod.dll
```

Please note that you will need the Mono C# compiler for this to work.

##### Mac OS

Build the dynamic library:

```
$ cd linux
$ make olmod.dylib
```

Build the GameMod.dll file containing the game logic (untested):

```
$ cd linux
$ export OLPATH=/path/to/your/Overload/installation
$ make GameMod.dll
```

Please note that you will need the Mono C# compiler for this to work.


#### How does it work

The regular `Overload.exe` just runs `UnityMain` from `UnityPlayer.dll`.
The replacement `olmod.exe` also runs `UnityMain`, but intercepts calls from Unity to the mono C# engine
to also load `GameMod.dll`. The file `GameMod.dll` contains a C# class that
uses [Harmony](https://github.com/pardeike/Harmony) to modify the game
scripting code in memory.
