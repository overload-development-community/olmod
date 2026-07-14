using System;
using System.Collections.Generic;
using HarmonyLib;
using Overload;
using UnityEngine;

namespace GameMod
{

    // global hotkeys, editable through the console with hotkey_list, hotkey_bind, hotkey_unbind
    // persists in the pilot.extendedconfig
    // to add new hotkeys just Register the key and point it towards a function
    public class Hotkeys
    {
        static Hotkeys()
        {
            Register("test", "p+o+s", "For testing different scenarios with clear and harmless output", Test);
            Register("fullscreen", "alt+f", "Toggles between fullscreen and windowed mode", ToggleFullscreen);
        }

        ///////////////////////////////////////////
        //      Hotkey edge case test command
        ///////////////////////////////////////////
        private static int hks_testCounter = 0;

        private static void Test()
        {
            hks_testCounter++;
            string message = "HOTKEY TEST " + hks_testCounter;
            if (GameManager.m_game_state == GameManager.GameState.GAMEPLAY)
            {
                GameplayManager.AddHUDMessage(message, -1, true);
            }
            else
            {
                MenuManager.PopupMessage(message, "HOTKEY", 0f, 3f);
            }
            SFXCueManager.PlayRawSoundEffect2D(SoundEffect.hud_notify_message1, 0.8f);
        }

        ///////////////////////////////////////////
        //      Fullscreen toggle
        ///////////////////////////////////////////
        private static int hks_windowedWidth = -1;
        private static int hks_windowedHeight = -1;

        private static void ToggleFullscreen()
        {
            if (!Screen.fullScreen)
            {
                hks_windowedWidth = Screen.width;
                hks_windowedHeight = Screen.height;
                var native = Screen.currentResolution;
                Screen.SetResolution(native.width, native.height, true);
                MenuManager.m_resolution_width = native.width;
                MenuManager.m_resolution_height = native.height;
            }
            else
            {
                int w = hks_windowedWidth > 0 ? hks_windowedWidth : MenuManager.m_resolution_width;
                int h = hks_windowedHeight > 0 ? hks_windowedHeight : MenuManager.m_resolution_height;
                Screen.SetResolution(w, h, false);
                MenuManager.m_resolution_width = w;
                MenuManager.m_resolution_height = h;
            }
        }




        public const int MAX_COMBO_KEYS = 3; // null/empty = unbound/ignored.
        
        public static List<Hotkey> hotkeys = new List<Hotkey>();
        
        private static Dictionary<string, Hotkey> by_name = new Dictionary<string, Hotkey>();
        
        public static readonly Modifier[] Modifiers = {
            new Modifier("ctrl", KeyCode.LeftControl, KeyCode.RightControl),
            new Modifier("alt", KeyCode.LeftAlt, KeyCode.RightAlt),
            new Modifier("shift", KeyCode.LeftShift, KeyCode.RightShift),
        };

        public class Hotkey
        {
            public string name;
            public string description;
            public string defaultBinding;
            public KeyCode[] keys;
            public Action action;
        }

        // simplifies usage by hiding the difference between left/right alt/shift/control
        public struct Modifier
        {
            public string alias;
            public KeyCode left, right;
            public Modifier(string alias, KeyCode left, KeyCode right)
            {
                this.alias = alias;
                this.left = left;
                this.right = right;
            }
        }

        public static void Register(string name, string defaultBinding, string description, Action action)
        {
            Hotkey h = new Hotkey();
            h.name = name.ToLowerInvariant();
            h.description = description;
            h.defaultBinding = defaultBinding;
            h.action = action;
            TryParseBinding(defaultBinding, out h.keys);
            hotkeys.Add(h);
            by_name.Add(h.name, h);
        }

        public static Hotkey Find(string name)
        {
            Hotkey h;
            by_name.TryGetValue(name.ToLowerInvariant(), out h);
            return h;
        }

        public static bool SetBinding(string name, string bindingText)
        {
            Hotkey h = Find(name);
            KeyCode[] keys;
            if (h == null || !TryParseBinding(bindingText, out keys))
            {
                return false;
            }
            h.keys = keys;
            return true;
        }

        // Accepts "none" or up to MAX_COMBO_KEYS key names joined with '+'
        // ("f", "alt+f", "g+h", "tab+q+e"). Any KeyCode name is valid,
        // plus the aliases ctrl/alt/shift and single digits ("1" => alpha1).
        public static bool TryParseBinding(string text, out KeyCode[] keys)
        {
            keys = null;
            if (string.IsNullOrEmpty(text) || text.ToLowerInvariant() == "none")
            {
                return true;
            }

            string[] tokens = text.ToLowerInvariant().Split('+');
            if (tokens.Length > MAX_COMBO_KEYS)
            {
                return false;
            }
            KeyCode[] parsed = new KeyCode[tokens.Length];
            for (int i = 0; i < tokens.Length; i++)
            {
                if (!TryParseKey(tokens[i], out parsed[i]))
                {
                    return false;
                }
                for (int j = 0; j < i; j++)
                {
                    if (parsed[j] == parsed[i])
                    {
                        return false;
                    }
                }
            }
            keys = parsed;
            return true;
        }

        private static bool TryParseKey(string token, out KeyCode key)
        {
            key = KeyCode.None;
            foreach (Modifier m in Modifiers)
            {
                if (token == m.alias)
                {
                    key = m.left;
                    return true;
                }
            }
            if (token.Length == 1 && token[0] >= '0' && token[0] <= '9')
            {
                token = "alpha" + token;
            }
            try
            {
                key = (KeyCode)Enum.Parse(typeof(KeyCode), token, true);
            }
            catch (ArgumentException)
            {
                return false;
            }
            return key != KeyCode.None;
        }

        // the ctrl/alt/shift aliases match either side of the keyboard
        public static bool IsKeyHeld(KeyCode key)
        {
            foreach (Modifier m in Modifiers)
            {
                if (key == m.left)
                {
                    return Input.GetKey(m.left) || Input.GetKey(m.right);
                }
            }
            return Input.GetKey(key);
        }

        public static bool IsKeyDown(KeyCode key)
        {
            foreach (Modifier m in Modifiers)
            {
                if (key == m.left)
                {
                    return Input.GetKeyDown(m.left) || Input.GetKeyDown(m.right);
                }
            }
            return Input.GetKeyDown(key);
        }

        public static string FormatBinding(KeyCode[] keys)
        {
            if (keys == null || keys.Length == 0)
            {
                return "none";
            }
            string text = "";
            for (int i = 0; i < keys.Length; i++)
            {
                if (i > 0)
                {
                    text += "+";
                }
                text += FormatKey(keys[i]);
            }
            return text;
        }

        private static string FormatKey(KeyCode key)
        {
            foreach (Modifier m in Modifiers)
            {
                if (key == m.left)
                {
                    return m.alias;
                }
            }
            return key.ToString().ToLowerInvariant();
        }


    }

    ///////////////////////////////////////////
    //      Update
    ///////////////////////////////////////////

    [HarmonyPatch(typeof(GameManager), "Update")]
    class Hotkeys_GameManager_Update
    {
        static void Postfix()
        {
            if (!Input.anyKeyDown || GameplayManager.IsDedicatedServer() || uConsole.IsOn() || PlayerShip.m_typing_in_chat)
                return;

            foreach (Hotkeys.Hotkey h in Hotkeys.hotkeys)
            {
                KeyCode[] keys = h.keys;
                if (keys == null || keys.Length == 0)
                    continue;
                // cheapest reject first: the trigger (last) key must have gone down this frame
                if (!Hotkeys.IsKeyDown(keys[keys.Length - 1]))
                    continue;

                bool held = true;
                for (int i = 0; i < keys.Length - 1 && held; i++)
                {
                    held = Hotkeys.IsKeyHeld(keys[i]);
                }
                // a held standard modifier that is not part of the combo blocks it,
                // so "f" and "alt+f" stay distinct bindings
                if (held && !BlockedByModifier(keys))
                    h.action();
            }
        }

        private static bool BlockedByModifier(KeyCode[] keys)
        {
            foreach (Hotkeys.Modifier m in Hotkeys.Modifiers)
            {
                if (Hotkeys.IsKeyHeld(m.left) && !Contains(keys, m.left, m.right))
                    return true;
            }
            return false;
        }

        private static bool Contains(KeyCode[] keys, KeyCode a, KeyCode b)
        {
            foreach (KeyCode k in keys)
            {
                if (k == a || k == b)
                    return true;
            }
            return false;
        }
    }



    ///////////////////////////////////////////
    //      Console Commands
    ///////////////////////////////////////////
    
    [HarmonyPatch(typeof(GameManager), "Start")]
    class Hotkeys_GameManager_Start
    {
        static void Postfix()
        {
            uConsole.RegisterCommand("hotkey_list", "Lists all hotkeys and their current bindings", new uConsole.DebugCommand(CmdList));
            uConsole.RegisterCommand("hotkey_bind", "Binds a hotkey -- \"hotkey_bind <name> <combo>\" (combo of up to 3 keys like f, alt+f, tab+q+e)", new uConsole.DebugCommand(CmdBind));
            uConsole.RegisterCommand("hotkey_unbind", "Removes the binding of a hotkey -- \"hotkey_unbind <name>\"", new uConsole.DebugCommand(CmdUnbind));
        }

        private static void CmdList()
        {
            foreach (Hotkeys.Hotkey h in Hotkeys.hotkeys)
            {
                uConsole.Log(h.name + ": " + Hotkeys.FormatBinding(h.keys) + "  -  " + h.description);
            }
        }

        private static void CmdBind()
        {
            if (uConsole.GetNumParameters() != 2)
            {
                uConsole.Log("[HOTKEYS] usage example: hotkey_bind fullscreen alt+f    (allows up to 3 keys)");
                return;
            }
            Bind(uConsole.GetString(), uConsole.GetString());
        }

        private static void CmdUnbind()
        {
            if (uConsole.GetNumParameters() != 1)
            {
                uConsole.Log("[HOTKEYS] usage: hotkey_unbind <name>");
                return;
            }
            Bind(uConsole.GetString(), "none");
        }

        private static void Bind(string name, string combo)
        {
            Hotkeys.Hotkey hotkey = Hotkeys.Find(name);
            if (hotkey == null)
            {
                uConsole.Log("[HOTKEYS] unknown hotkey '" + name + "'. Use hotkey_list to see all hotkeys");
                return;
            }
            KeyCode[] keys;
            if (!Hotkeys.TryParseBinding(combo, out keys))
            {
                uConsole.Log("[HOTKEYS] could not parse '" + combo + "'. Up to " + Hotkeys.MAX_COMBO_KEYS + " keys, examples: f, alt+f, g+h, tab+q+e, none");
                return;
            }
            foreach (Hotkeys.Hotkey other in Hotkeys.hotkeys)
            {
                // losely enforce 1:1 relation between key triggers and functions (if somebody really wanted to they could edit their .extendedconfig to still do this)
                if (other != hotkey && keys != null && Hotkeys.FormatBinding(other.keys) == Hotkeys.FormatBinding(keys))
                {
                    uConsole.Log("[HOTKEYS] could not set binding: '" + combo + "' is also bound to '" + other.name + "'");
                    return;
                }
            }
            hotkey.keys = keys;
            uConsole.Log("[HOTKEYS] " + hotkey.name + " bound to " + Hotkeys.FormatBinding(keys));
            ExtendedConfig.Section_Hotkeys.Set(true);
            ExtendedConfig.SaveActivePilot();
        }
    }
}
