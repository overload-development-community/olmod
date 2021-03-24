using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Harmony;
using Newtonsoft.Json.Linq;
using Overload;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Networking.NetworkSystem;

namespace GameMod {
    public static class ExtMatchMode
    {
        public const MatchMode ANARCHY = MatchMode.ANARCHY;
        public const MatchMode TEAM_ANARCHY = MatchMode.TEAM_ANARCHY;
        public const MatchMode MONSTERBALL = MatchMode.MONSTERBALL;
        public const MatchMode CTF = (MatchMode)3;
        public const MatchMode RACE = (MatchMode)4;
        //public const MatchMode ARENA = (MatchMode)5;
        //public const MatchMode TEAM_ARENA = (MatchMode)6;
        public const MatchMode NUM = (MatchMode)((int)RACE + 1);

        private static readonly string[] Names = new string[] {
            "ANARCHY", "TEAM ANARCHY", "MONSTERBALL", "CTF", "RACE" };
        public static string ToString(MatchMode mode)
        {
            if ((int)mode < 0 || (int)mode >= Names.Length)
                return "UNEXPECTED MODE: " + (int)mode;
            return Names[(int)mode];
        }
    }

    static class MPModPrivateData
    {

        public static int TeamCount
        {
            get { return MPTeams.NetworkMatchTeamCount; }
            set { MPTeams.NetworkMatchTeamCount = value; }
        }
        public static bool RearViewEnabled
        {
            get { return RearView.MPNetworkMatchEnabled; }
            set { RearView.MPNetworkMatchEnabled = value; }
        }
        public static bool JIPEnabled
        {
            get { return MPJoinInProgress.NetworkMatchEnabled; }
            set { MPJoinInProgress.NetworkMatchEnabled = value; }
        }
        public static bool SniperPacketsEnabled
        {
            get { return MPSniperPackets.enabled; }
            set { MPSniperPackets.enabled = value; }
        }
        public static MatchMode MatchMode
        {
            get { return NetworkMatch.GetMode(); }
            set { NetworkMatch.SetMode(value); }
        }
        public static bool SuddenDeathEnabled
        {
            get { return MPSuddenDeath.SuddenDeathMatchEnabled; }
            set { MPSuddenDeath.SuddenDeathMatchEnabled = value; }
        }
        public static int LapLimit;
        public static string MatchNotes { get; set; }
        public static bool HasPassword { get; set; }
        public static bool ScaleRespawnTime { get; set; }
        public static int ModifierFilterMask;
        public static bool ClassicSpawnsEnabled
        {
            get { return MPClassic.matchEnabled; }
            set { MPClassic.matchEnabled = value; }
        }
        public static bool CtfCarrierBoostEnabled
        {
            get { return CTF.CarrierBoostEnabled; }
            set { CTF.CarrierBoostEnabled = value; }
        }
        public static bool AlwaysCloaked {
            get { return MPAlwaysCloaked.Enabled; }
            set { MPAlwaysCloaked.Enabled = value; }
        }
        public static string CustomProjdata { get; set; }

        public static bool AllowSmash {
            get { return MPSmash.Enabled; }
            set { MPSmash.Enabled = value; }
        }

        public static JObject Serialize()
        {
            JObject jobject = new JObject();
            jobject["teamcount"] = TeamCount;
            jobject["rearviewenabled"] = RearViewEnabled;
            jobject["jipenabled"] = JIPEnabled;
            jobject["sniperpacketsenabled"] = SniperPacketsEnabled;
            jobject["matchmode"] = (int)MatchMode;
            jobject["suddendeathenabled"] = SuddenDeathEnabled;
            jobject["laplimit"] = LapLimit;
            jobject["matchnotes"] = MatchNotes;
            jobject["haspassword"] = HasPassword;
            jobject["scalerespawntime"] = ScaleRespawnTime;
            jobject["modifierfiltermask"] = ModifierFilterMask;
            jobject["classicspawnsenabled"] = ClassicSpawnsEnabled;
            jobject["ctfcarrierboostenabled"] = CtfCarrierBoostEnabled;
            jobject["alwayscloaked"] = AlwaysCloaked;
            jobject["allowsmash"] = AllowSmash;
            jobject["customprojdata"] = CustomProjdata;
            return jobject;
        }

        public static void Deserialize(JToken root)
        {
            TeamCount = root["teamcount"].GetInt(MPTeams.Min);
            RearViewEnabled = root["rearviewenabled"].GetBool(false);
            JIPEnabled = root["jipenabled"].GetBool(false);
            SniperPacketsEnabled = root["sniperpacketsenabled"].GetBool(false);
            MatchMode = (MatchMode)root["matchmode"].GetInt(0);
            SuddenDeathEnabled = root["suddendeathenabled"].GetBool(false);
            LapLimit = root["laplimit"].GetInt(0);
            MatchNotes = root["matchnotes"].GetString(String.Empty);
            HasPassword = root["haspassword"].GetBool(false);
            ScaleRespawnTime = root["scalerespawntime"].GetBool(false);
            ModifierFilterMask = root["modifierfiltermask"].GetInt(255);
            ClassicSpawnsEnabled = root["classicspawnsenabled"].GetBool(false);
            CtfCarrierBoostEnabled = root["ctfcarrierboostenabled"].GetBool(false);
            AlwaysCloaked = root["alwayscloaked"].GetBool(false);
            AllowSmash = root["allowsmash"].GetBool(false);
            CustomProjdata = root["customprojdata"].GetString(string.Empty);
        }

        public static string GetModeString(MatchMode mode)
        {
            return Loc.LS(ExtMatchMode.ToString(mode));
        }

    }

    public class MPModPrivateDataTransfer
    {
        public static void SendTo(int connId)
        {
            var mmpdMsg = new StringMessage(MPModPrivateData.Serialize().ToString(Newtonsoft.Json.Formatting.None));
            NetworkServer.SendToClient(connId, MessageTypes.MsgModPrivateData, mmpdMsg);
        }
        public static void OnReceived(string data)
        {
            Debug.LogFormat("MPModPrivateData: received {0}", data);
            MPModPrivateData.Deserialize(JToken.Parse(data));
        }
    }

    /*
    public class MPModPrivateDataMessage : MessageBase
    {
        public int TeamCount { get; set; }
        public bool RearViewEnabled { get; set; }
        public bool JIPEnabled { get; set; }
        public MatchMode MatchMode { get; set; }
        public int LapLimit { get; set; }

        public override void Serialize(NetworkWriter writer)
        {
            writer.Write((byte)0); // version
            writer.WritePackedUInt32((uint)TeamCount);
            writer.Write(RearViewEnabled);
            writer.Write(JIPEnabled);
            writer.WritePackedUInt32((uint)MatchMode);
            writer.WritePackedUInt32((uint)LapLimit);
        }

        public override void Deserialize(NetworkReader reader)
        {
            var version = reader.ReadByte();
            TeamCount = (int)reader.ReadPackedUInt32();
            RearViewEnabled = reader.ReadBoolean();
            JIPEnabled = reader.ReadBoolean();
            MatchMode = (MatchMode)reader.ReadPackedUInt32();
            LapLimit = (int)reader.ReadPackedUInt32();
        }
    }
    */

    [HarmonyPatch(typeof(MenuManager), "GetMMSGameMode")]
    class MPModPrivateData_MenuManager_GetMMSGameMode
    {
        static bool Prefix(ref string __result)
        {
            __result = MPModPrivateData.GetModeString(MenuManager.mms_mode);

            return false;
        }
    }

    [HarmonyPatch(typeof(UIElement), "DrawMpMatchSetup")]
    class MPModPrivateData_UIElement_DrawMpMatchSetup
    {
        static void Postfix(UIElement __instance)
        {
            if (MenuManager.m_menu_micro_state == 2 && MenuManager.mms_mode == ExtMatchMode.RACE)
            {
                Vector2 position = Vector2.zero;
                position.y = -279f + 62f * 6;
                var text = ExtMenuManager.mms_ext_lap_limit == 0 ? "NONE" : ExtMenuManager.mms_ext_lap_limit.ToString();
                __instance.SelectAndDrawStringOptionItem("LAP LIMIT", position, 10, text, string.Empty, 1.5f, false);
            }
        }
    }

    [HarmonyPatch(typeof(MenuManager), "MpMatchSetup")]
    class MPModPrivateData_MenuManager_MpMatchSetup
    {
        static void MatchModeSlider()
        {
            MenuManager.mms_mode = (MatchMode)(((int)MenuManager.mms_mode + (int)ExtMatchMode.NUM + UIManager.m_select_dir) % (int)ExtMatchMode.NUM);
            return;
        }

        static void HandleLapLimit()
        {
            if (MenuManager.m_menu_sub_state == MenuSubState.ACTIVE &&
                MenuManager.m_menu_micro_state == 2 &&
                UIManager.m_menu_selection == 10)
            {
                //ExtMenuManager.mms_ext_lap_limit = (ExtMenuManager.mms_ext_lap_limit + 21 + UIManager.m_select_dir) % 21;
                ExtMenuManager.mms_ext_lap_limit = Math.Max(0, Math.Min(50, ExtMenuManager.mms_ext_lap_limit + UIManager.m_select_dir * 5));
                MenuManager.PlayCycleSound(1f, (float)UIManager.m_select_dir);
            }
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            bool remove = false;
            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Call && ((MethodInfo)code.operand).Name == "MaybeReverseOption")
                {
                    yield return code;
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPModPrivateData_MenuManager_MpMatchSetup), "HandleLapLimit"));
                    continue;
                }

                if (code.opcode == OpCodes.Ldsfld && (code.operand as FieldInfo).Name == "mms_mode")
                {
                    remove = true;
                    code.opcode = OpCodes.Call;
                    code.operand = AccessTools.Method(typeof(MPModPrivateData_MenuManager_MpMatchSetup), "MatchModeSlider");
                    yield return code;
                }

                if (code.opcode == OpCodes.Stsfld && (code.operand as FieldInfo).Name == "mms_mode")
                {
                    remove = false;
                    continue;
                }

                if (remove)
                    continue;

                yield return code;
            }
        }
    }

    public class ExtMenuManager
    {
        public static int mms_ext_lap_limit = 10;
    }

    [HarmonyPatch(typeof(NetworkMatch), "GetModeString")]
    class MPModPrivateData_NetworkMatch_GetModeString
    {
        // there's a mode argument but in actual usage this is always NetworkMatch.GetMode()
        // so ignore it here, since the default value MatchMode.NUM means CTF now :(
        static bool Prefix(MatchMode mode, ref string __result)
        {
            __result = MPModPrivateData.GetModeString(NetworkMatch.GetMode());
            return false;
        }
    }

    [HarmonyPatch(typeof(Client), "RegisterHandlers")]
    class MPModPrivateData_Client_RegisterHandlers
    {
        private static void OnModPrivateData(NetworkMessage msg)
        {
            /*
            MPModPrivateDataMessage mpdm = msg.ReadMessage<MPModPrivateDataMessage>();
            MPModPrivateData.MatchMode = mpdm.MatchMode;
            MPModPrivateData.JIPEnabled = mpdm.JIPEnabled;
            MPModPrivateData.RearViewEnabled = mpdm.RearViewEnabled;
            MPModPrivateData.TeamCount = mpdm.TeamCount;
            MPModPrivateData.LapLimit = mpdm.LapLimit;
            */
            MPModPrivateDataTransfer.OnReceived(msg.ReadMessage<StringMessage>().value);
        }

        static void Postfix()
        {
            if (Client.GetClient() == null)
                return;
            Client.GetClient().RegisterHandler(MessageTypes.MsgModPrivateData, OnModPrivateData);
        }
    }

    /*
    [HarmonyPatch(typeof(Server), "SendAcceptedToLobby")]
    class MPModPrivateData_Server_SendAcceptedToLobby
    {
        static void Postfix(NetworkConnection conn)
        {
            var server = NetworkMatch.m_client_server_location;
            if (!server.StartsWith("OLMOD ") || server == "OLMOD 0.2.8" || server.StartsWith("OLMOD 0.2.8."))
                return;
            /-*
            var msg = new MPModPrivateDataMessage
            {
                JIPEnabled = MPModPrivateData.JIPEnabled,
                MatchMode = MPModPrivateData.MatchMode,
                RearViewEnabled = MPModPrivateData.RearViewEnabled,
                TeamCount = MPModPrivateData.TeamCount,
                LapLimit = MPModPrivateData.LapLimit
            };
            *-/
            var msg = new StringMessage(MPModPrivateData.Serialize().ToString(Newtonsoft.Json.Formatting.None));

            NetworkServer.SendToClient(conn.connectionId, ModCustomMsg.MsgModPrivateData, msg);
        }
    }
    */

    [HarmonyPatch(typeof(NetworkMatch), "NetSystemOnGameSessionStart")]
    class NetworkMatch_NetSystemOnGameSessionStart
    {
        public static string ModNetSystemOnGameSessionStart(Dictionary<string, object> attributes)
        {
            return attributes.ContainsKey("mod_private_data") ? (string)attributes["mod_private_data"] : "";
        }

        public static void ModNetSystemOnGameSessionStart2(string mpd)
        {
            if (mpd != "")
                MPModPrivateDataTransfer.OnReceived(mpd);
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator ilGen)
        {
            LocalBuilder a = ilGen.DeclareLocal(typeof(string));
            var codes = instructions.ToList();
            int startIdx = -1;
            int startIdx2 = -1;
            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Ldstr && (string)codes[i].operand == "private_match_data")
                {
                    startIdx = i - 3;
                }

                //if (codes[i].opcode == OpCodes.Ldstr && (string)codes[i].operand == "Unpacked private match data for: {0}")
                if (codes[i].opcode == OpCodes.Ldsfld && ((MemberInfo)codes[i].operand).Name == "m_max_players_for_match" && startIdx2 == -1)
                {
                    startIdx2 = i;
                }
            }

            // insert backwards to preserve indexes

            if (startIdx2 > -1 && startIdx > -1)
            {
                var labels = codes[startIdx2].labels;
                codes[startIdx2].labels = new List<Label>();
                codes.InsertRange(startIdx2, new[]
                {
                    new CodeInstruction(OpCodes.Ldloc_S, a) { labels = labels },
                    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(NetworkMatch_NetSystemOnGameSessionStart), "ModNetSystemOnGameSessionStart2"))
                });
            }

            if (startIdx > -1)
            {
                List<CodeInstruction> newCodes = new List<CodeInstruction>();
                for (int i = startIdx; i < startIdx + 3; i++) // copy loads for attributes dict
                {
                    newCodes.Add(new CodeInstruction(codes[i].opcode, codes[i].operand));
                }
                newCodes.Add(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(NetworkMatch_NetSystemOnGameSessionStart), "ModNetSystemOnGameSessionStart")));
                newCodes.Add(new CodeInstruction(OpCodes.Stloc_S, a));
                codes.InsertRange(startIdx, newCodes);
            }


            return codes;
        }

    }

    [HarmonyPatch(typeof(NetworkMatch), "StartMatchMakerRequest")]
    class MPModPrivateData_NetworkMatch_StartMatchMakerRequest
    {

        public static void PatchModPrivateData(MatchmakerPlayerRequest matchmakerPlayerRequest)
        {
            if (!MenuManager.m_mp_lan_match) // LAN includes internet match
                return;
            MPModPrivateData.MatchMode = MenuManager.mms_mode;
            MPModPrivateData.RearViewEnabled = RearView.MPMenuManagerEnabled;
            MPModPrivateData.JIPEnabled = MPJoinInProgress.MenuManagerEnabled || MPJoinInProgress.SingleMatchEnable;
            MPModPrivateData.TeamCount = MPTeams.MenuManagerTeamCount;
            MPModPrivateData.LapLimit = ExtMenuManager.mms_ext_lap_limit;
            MPModPrivateData.MatchNotes = MPServerBrowser.mms_match_notes;
            MPModPrivateData.SniperPacketsEnabled = true;
            MPModPrivateData.ScaleRespawnTime = Menus.mms_scale_respawn_time;
            MPModPrivateData.ModifierFilterMask = RUtility.BoolArrayToBitmask(MPModifiers.mms_modifier_filter);
            MPModPrivateData.ClassicSpawnsEnabled = Menus.mms_classic_spawns;
            MPModPrivateData.CtfCarrierBoostEnabled = Menus.mms_ctf_boost;
            MPModPrivateData.AlwaysCloaked = Menus.mms_always_cloaked;
            MPModPrivateData.AllowSmash = Menus.mms_allow_smash;
            if (Menus.mms_mp_projdata_fn != "STOCK")
            {
                try
                {
                    MPModPrivateData.CustomProjdata = System.IO.File.ReadAllText(Menus.mms_mp_projdata_fn);
                    
                }
                catch (Exception ex)
                {
                    Debug.Log("Unable to read custom projdata file: " + Menus.mms_mp_projdata_fn);
                    MPModPrivateData.CustomProjdata = String.Empty;
                }
            }
            else
            {
                MPModPrivateData.CustomProjdata = String.Empty;
            }

            var mpd = (PrivateMatchDataMessage)AccessTools.Field(typeof(NetworkMatch), "m_private_data").GetValue(null);
            MPModPrivateData.HasPassword = mpd.m_password.Contains('_');
            matchmakerPlayerRequest.PlayerAttributes["mod_private_data"] = MPModPrivateData.Serialize().ToString(Newtonsoft.Json.Formatting.None);
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            int i = 0;
            CodeInstruction last = null;
            CodeInstruction mmprAttributes = null;
            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Ldstr && (string)code.operand == "players")
                    mmprAttributes = last;

                if (mmprAttributes == null)
                    last = code;

                if (mmprAttributes != null && code.opcode == OpCodes.Ldsfld && code.operand == AccessTools.Field(typeof(NetworkMatch), "m_private_data"))
                {
                    i++;

                    if (i == 3)
                    {
                        CodeInstruction ci1 = new CodeInstruction(OpCodes.Ldloc_S, mmprAttributes.operand);
                        CodeInstruction ci2 = new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPModPrivateData_NetworkMatch_StartMatchMakerRequest), "PatchModPrivateData"));
                        yield return ci1;
                        yield return ci2;
                    }
                }

                yield return code;
            }
        }
    }
}
