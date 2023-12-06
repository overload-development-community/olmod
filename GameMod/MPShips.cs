using HarmonyLib;
using Overload;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using UnityEngine.Networking;

namespace GameMod
{
    // Handles asset replacement for different Ship types
    public static class MPShips
    {
        public const string MULTISHIP_VERSION = "MULTISHIP-0.5";

        public static Dictionary<NetworkInstanceId, Ship> SelectedShips = new Dictionary<NetworkInstanceId, Ship>(); // stores the PlayerShip's netId and the associated Ship reference
        public static Dictionary<int, int> LobbyShips = new Dictionary<int, int>(); // (lobby_id, idx) stores a lobby_id and the associated Ship index for translation at instantiation
        public static List<NetworkInstanceId> DeferredShips = new List<NetworkInstanceId>(); // stores any ships that are not found yet when their associated information is received from the server

        public static List<Ship> Ships = new List<Ship>();
        public static Ship sp_ship = new Kodachi(); // used in cases where there is no assigned ship type

        public static int allowed = 0; // which ships are allowed (0 is stock, 1 is all, the rest are locked ship types)
        public static bool FireWhileBoost = true; // ++++ this needs to have a menu option added

        private static int _idx = 0;
        public static int selected_idx // the player's preferred ship type
        {
            get { return _idx; }
            set
            {
                if (value < Ships.Count)
                {
                    _idx = value;
                }
                else
                {
                    Debug.Log("CCF Invalid ship selected, resetting to 0 (" + Ships[0].displayName + ")");
                    _idx = 0;
                }
            }
        }

        public static GameObject m_blank; // a single tiny untextured triangle for blanking out unused prefab components

        public static bool loading = false; // set to true during the loading process

        public static float masterscale = 1f; // applied in addition to any ship-defined scaling


        public static void AddShips()
        {
            // ship prefabs are explicitly added here
            //Debug.Log("Adding Kodachi ship definition");
            //Ships.Add(new Kodachi()); // Don't mess with this one or you're gonna break stuff.
            Debug.Log("Adding Kodachi85 ship definition");
            Ships.Add(new Kodachi85());
            //Debug.Log("Adding Kodachi75 ship definition");
            //Ships.Add(new Kodachi75());
            Debug.Log("Adding Pyro ship definition");
            Ships.Add(new PyroGX());
            Debug.Log("Adding Phoenix ship definition");
            Ships.Add(new Phoenix());
            Debug.Log("Adding Magnum ship definition");
            Ships.Add(new Magnum());
            //Debug.Log("Adding Pyro (Cosmetic) ship definition");
            //Ships.Add(new PyroGXCosmetic());

            //sp_ship = Ships[0];
            //sp_ship = new Kodachi(); done at field declaration

            //DEBUG
            //uConsole.RegisterCommand("accel", "Set ship accel", new uConsole.DebugCommand(SetAccelDebug));
            //uConsole.RegisterCommand("lvol", "Lancer extra sound volume", new uConsole.DebugCommand(MPWeapons.SetLancerVol));
            //uConsole.RegisterCommand("lrefire", "Lancer refire wait", new uConsole.DebugCommand(MPWeapons.SetLancerRefire));
        }

        // anything that needs to get toggled immediately when Multiship is enabled or disabled in the game options should get called here.
        public static void EnabledInMatch()
        {
            MPWeapons.UpdateWeaponList();
            MPWeapons.UpdateProjectileSync();
        }

        public static void SetScaleDebug()
        {
            string s = uConsole.GetString();

            if (GameplayManager.IsMultiplayerActive && float.TryParse(s, out float scale))
            {
                scale = Mathf.Clamp(scale, 0.2f, 2f);

                masterscale = scale;

                //GameManager.m_local_player.c_player_ship.c_main_ship_go.transform.localScale = new Vector3(scale, scale, scale);
                uConsole.Log("Changing ship scale for round to " + scale);
            }
            else
            {
                uConsole.Log("Not in a match or invalid scale, must be a number between 0.2 and 2.");
            }
        }


        public static void LoadResources()
        {
            loading = true;
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("GameMod.Resources.meshes"))
            {
                var ab = AssetBundle.LoadFromStream(stream);

                m_blank = Object.Instantiate(ab.LoadAsset<GameObject>("blankmesh")); // used for substituting sections of the ship that shouldn't be present but are expected to be in the effects array
                Object.DontDestroyOnLoad(m_blank); // they keep despawning and causing NullReferences unless this is here. Grrr.
                //Debug.Log("Blank replacement mesh loaded");

                foreach (Ship s in Ships)
                {
                    if (s.meshName != null) // if it's null, we're planning to use using the Kodachi default mesh
                    {
                        s.mesh = Object.Instantiate(ab.LoadAsset<GameObject>(s.meshName));
                        Object.DontDestroyOnLoad(s.mesh);
                        //Debug.Log("Prefab replacement mesh loaded: " + s.mesh.name);
                    }
                    for (int i = 0; i < 3; i++)
                    {
                        s.collider[i] = Object.Instantiate(ab.LoadAsset<GameObject>(s.colliderNames[i]));
                        Object.DontDestroyOnLoad(s.collider[i]);
                        //Debug.Log("MeshCollider loaded: " + s.collider[i].name);
                    }
                    for (int i = 0; i < s.extras.Length; i++)
                    {
                        s.extras[i] = Object.Instantiate(ab.LoadAsset<GameObject>(s.extraNames[i]));
                        Object.DontDestroyOnLoad(s.extras[i]);
                        //Debug.Log("Extra mesh loaded: " + s.extras[i].name);
                    }
                }
                ab.Unload(false);
            }
            loading = false;
        }


        // translates a lobby id into a netId once the object is instantiated on the server
        public static void AssignShip(int lobbyId, NetworkInstanceId netId)
        {
            int idx;

            if (!LobbyShips.TryGetValue(lobbyId, out idx))
            {
                Debug.Log("CCF did not find lobby_id " + lobbyId + " in AssignShip " + (GameplayManager.IsDedicatedServer() ? "on server" : "on client " + NetworkMatch.m_my_lobby_id));
                idx = -1;
                LobbyShips[lobbyId] = idx;
            }

            switch (allowed)
            {
                case 0:
                    idx = -1;
                    break;
                case 1:
                    break;
                default:
                    idx = allowed - 2;
                    break;
            }

            if (idx < 0)
            {
                SelectedShips[netId] = sp_ship.Copy();
            }
            else
            {
                SelectedShips[netId] = Ships[idx].Copy();
            }
        }


        // assigns the ship index to the lobby id of the current player (for conversion to a netId once it's instantiated)
        public static void AssignLobbyShip(int lobby_id, int idx)
        {
            //LobbyShips[lobby_id] = idx;
            if (idx >= Ships.Count)
            {
                Debug.Log("Client attempted to assign a non-existent ship value, using Kodachi instead.");
                idx = -1;
            }
            Debug.Log("CCF Adding to list " + lobby_id + " on " + (GameplayManager.IsDedicatedServer() ? "server" : "client"));
            LobbyShips[lobby_id] = idx;
        }


        // takes an instantiated PlayerShip and returns the appropriate Ship definition for that PlayerShip
        public static Ship GetShip(PlayerShip ps)
        {
            Ship s;

            NetworkInstanceId id = ps.netId;
            if (!SelectedShips.TryGetValue(id, out s))
            {
                Debug.Log("CCF No ship found in GetShip");
                s = sp_ship.Copy();
                SelectedShips[id] = s;
            }
            return s;
        }


        // finds the actual ship object on the client associated with a NetworkInstanceId
        public static PlayerShip GetPlayerShipFromNetId(NetworkInstanceId net_id)
        {
            GameObject gameObject = ClientScene.FindLocalObject(net_id);
            if (gameObject == null)
            {
                return null;
            }
            PlayerShip component = gameObject.GetComponent<PlayerShip>();
            if (component == null)
            {
                Debug.LogErrorFormat("Failed to find PlayerShip component on gameObject {0} with netId {1}", gameObject.name, net_id);
                return null;
            }
            return component;
        }


        // sends the ship selection and lobby_id from a client to the server
        public static void SendShipSelectionToServer()
        {
            if (Client.GetClient() == null)
            {
                Debug.LogErrorFormat("Null client in MPShips.SendShipSelectionToServer for player", new object[0]);
                return;
            }

            /*
            int ship_idx;
            switch (allowed)
            {
                case 0:
                    ship_idx = 0;
                    break;
                case 1:
                    ship_idx = selected_idx;
                    break;
                default:
                    ship_idx = allowed - 2;
                    break;
            }*/
            Debug.Log("CCF ships allowed: " + allowed + " selected: " + selected_idx);

            ShipDataToServerMessage message = new ShipDataToServerMessage()
            {
                lobbyId = NetworkMatch.m_my_lobby_id,
                selected_idx = selected_idx
            };

            Client.GetClient().Send(MessageTypes.MsgShipDataToServer, message);
        }

        /*
        // temporary solution for the weapon -- NOT NEEDED ANYMORE
        public static void TriTBFire(PlayerShip ps)
        {
            //Debug.Log("CCF TRIFIRE");
            if (GetShip(ps).triTB)
            {
                ProjectileManager.PlayerFire(ps.c_player, ProjPrefab.proj_thunderbolt, ps.m_muzzle_center.position, ps.c_transform.localRotation, ps.m_thunder_power, ps.c_player.m_weapon_level[(int)ps.c_player.m_weapon_type], no_sound: true, 2);
                ps.c_player.UseEnergy(2f + ps.m_thunder_power * 3f); // Wasteful trifusion uses twice the energy... tisk tisk.
            }
        }
        */

        // ====================================================================
        // Network Messages
        // ====================================================================


        // for sending selected ships and lobby IDs from client
        // to the server before the round starts
        public class ShipDataToServerMessage : MessageBase
        {
            public override void Serialize(NetworkWriter writer)
            {
                writer.WritePackedUInt32((uint)this.lobbyId);
                writer.WritePackedUInt32((uint)this.selected_idx);
            }

            public override void Deserialize(NetworkReader reader)
            {
                this.lobbyId = (int)reader.ReadPackedUInt32();
                this.selected_idx = (int)reader.ReadPackedUInt32();
            }

            public int lobbyId;
            public int selected_idx;
        }

        // for sending selected ships and net/lobby IDs from server
        // back to client once the object has been instantiated
        public class ShipDataToClientMessage : MessageBase
        {
            public override void Serialize(NetworkWriter writer)
            {
                writer.Write(this.netId);
                writer.WritePackedUInt32((uint)this.lobbyId);
                writer.WritePackedUInt32((uint)this.selected_idx);
            }

            public override void Deserialize(NetworkReader reader)
            {
                this.netId = reader.ReadNetworkId();
                this.lobbyId = (int)reader.ReadPackedUInt32();
                this.selected_idx = (int)reader.ReadPackedUInt32();
            }

            public NetworkInstanceId netId;
            public int lobbyId;
            public int selected_idx;
        }
    }


    // ====================================================================
    //
    //
    // ====================================================================
    // Ship Template
    // ====================================================================
    //
    //
    // ====================================================================


    public abstract class Ship
    {
        public PlayerShip ps;
        public Player player;

        // Substitute fields for the PlayerShip instance this ship is attached to -- don't override these
        public PrimaryWeapon[] primaries;

        public SecondaryWeapon[] secondaries; // not yet


        // ====================================================================
        // Ship Template Definitions
        // ====================================================================

        public string displayName; // what it will show up as in menus and whatnot
        public string name; // the GameObject's new name string (if needed)
        public string[] description = new string[3] // Flavour text for the menu. 3 lines available.
        {
            "- No description available -",
            "",
            ""
        };

        public string meshName; // the name of the mesh prefab -- if null, will be skipped (Kodachi comes to mind)
        public string[] colliderNames; // the 3 MeshCollider names
        public string[] extraNames; // names of extra meshes to load

        public GameObject mesh; // the replacement mesh (if needed)
        public GameObject[] collider = new GameObject[3];
        public GameObject[] extras = new GameObject[0]; // only used if needed

        public bool customizations; // whether or not to use the Kodachi's custom wing and body meshes.

        public Vector3[] FIRING_POINTS; // defines the LocalPosition of each primary weapon firepoint for this ship type. Must be 9 items in the array (see the Kodachi definition for an example)
        
        public Vector3 TRIFIRE_POINT; // temporary way to do the triTB firepoint
        public bool triTB = false;

        // quad impulse adjusted firepoints
        public float QdiffRightX;
        public float QdiffLeftX;
        public float QdiffY;
        public float QdiffZ;

        // ship scale factor (from Kodachi base of 1f)
        public float shipScale = 1f;

        // shield scaling factor -- inversely applied to shield pickups
        public float ShieldMultiplier;

        // movement speed restrictor
        public float[] m_slide_force_mp_nonscaled = new float[4] { 25f, 26.25f, 26.25f, 26.25f }; // We only care about the first 2 (1st is regular speed, 2nd is All-Way mod speed)
        public float[] m_slide_force_mp = new float[4]; // scaled movement values calculated at instantiation -- don't fill this directly

        // turn speed restrictors
        public float[] m_turn_speed_limit_acc = new float[5] { 2.3f, 3.2f, 4.5f, 6f, 100f }; // turning acceleration (I think)
        public float[] m_turn_speed_limit_rb = new float[5] { 2.5f, 3.2f, 4f, 5.2f, 100f }; // turning deceleration??? Not actually sure on this one.

        public float AccelMulti = 1f; // multiplies both the drag and the thruster force by this amount for more kick without affecting top speed
        public float MoveMulti = 1f; // movement multiplier
        public float TurnMulti = 1f; // turn speed multiplier
        public float boostMulti; // speed multiplier for regular boost
        public float boostMod; // speed multiplier for the Enhanced Boost mod

        public float boostBurst = 0f; // how much burst to apply at the start of a fresh boost (under 10% heat with normal boost) - as the extra percentage over 100%, expressed as decimal


        // makes a shallow copy of the Ship object for use with a specific player (for essentially "injecting" instance fields into a PlayerShip)
        public Ship Copy()
        {
            return (Ship)MemberwiseClone();
        }

        // Copies the match set of weapons to the Ship objects for use
        protected void SetWeapons()
        {
            primaries = new PrimaryWeapon[8];
            secondaries = new SecondaryWeapon[8];


            for (int i = 0; i < 8; i++)
            {
                primaries[i] = (PrimaryWeapon)MPWeapons.primaries[i].Copy();
                secondaries[i] = (SecondaryWeapon)MPWeapons.secondaries[i].Copy();
                primaries[i].SetShip(this);
                secondaries[i].SetShip(this);
            }
        }

        // should get called once with the Ship's constructor. If it's not called, the Ship will have Kodachi handling.
        protected void SetHandling()
        {
            int i;
            for (i = 0; i < 4; i++)
            {
                m_slide_force_mp_nonscaled[i] *= MoveMulti *= AccelMulti;
                m_turn_speed_limit_acc[i] *= TurnMulti; // the 5th turn array element should stay at 100f for unlocked turning
                m_turn_speed_limit_rb[i] *= TurnMulti; // the 5th turn array element should stay at 100f for unlocked turning
            }
        }

        // This is where the ship customizations in each definition get applied to the instantiated GameObject.
        // Override this in each Ship and call base.ApplyParameters() at the end of the method.
        public virtual void ApplyParameters(GameObject go)
        {
            // be sure to assign ps in implementation ApplyParameters() ==> ps = go.GetComponent<PlayerShip>();
            Debug.Log("Switching ship Prefabs to " + displayName);
            go.name = name;
            player = ps.c_player;

            ps.c_rigidbody.drag *= AccelMulti;
            Debug.Log("CCF setting drag to " + ps.c_rigidbody.drag);

            MPColliderSwap.SwapCollider(ps);
            SetScale();
            SetQuadFirepoints();
            SetWeapons();
        }

        // Scales the visible and shootable portion of the ship down or up by some amount.
        protected void SetScale()//PlayerShip ps)
        {
            /*if (MPShips.allowed != 0 && shipScale != 1f) // don't bother if multiship is off or if we're not scaling this particular ship
            {
                ps.c_main_ship_go.transform.localScale = new Vector3(shipScale * MPShips.masterscale, shipScale, shipScale); // only scales visible components, not the level collider. We don't want to squeeze places we shouldn't.

                //ps.c_main_ship_go.transform.localScale = new Vector3(MPShips.masterscale, MPShips.masterscale, MPShips.masterscale);

                for (int i = 0; i < 4; i++) // bring down the movement speeds too
                {
                    m_slide_force_mp[i] = shipScale * m_slide_force_mp_nonscaled[i];
                }
            }
            else
            {
                for (int i = 0; i < 4; i++)
                {
                    m_slide_force_mp[i] = m_slide_force_mp_nonscaled[i];
                }
            }
            */

            float sizeScale = shipScale * MPShips.masterscale;

            ps.c_main_ship_go.transform.localScale = new Vector3(sizeScale, sizeScale, sizeScale); // only scales visible components, not the level collider. We don't want to squeeze places we shouldn't.
            if (GameplayManager.IsMultiplayer)
            {
                ps.c_mesh_collider_trans.localScale = new Vector3(sizeScale, sizeScale, sizeScale); // however the mesh collider *should* be scaled, and it's not parented to the main ship in multiplayer
            }
            ps.c_flak_range_go.transform.localScale = new Vector3(3.333f / sizeScale, 3.333f / sizeScale, 3.333f / sizeScale); // flak rangefinder gets scaled too but shouldn't, its range isn't affected


            for (int i = 0; i < 4; i++) // bring down the movement speeds by half of the difference
            {
                m_slide_force_mp[i] = ((shipScale * m_slide_force_mp_nonscaled[i]) + (sizeScale * m_slide_force_mp_nonscaled[i])) / 2f;
            }
        }

        // translates the firepoint coordinates to world-scale adjustments like the game expects
        protected void SetQuadFirepoints()//PlayerShip ps)
        {
            float scale = ps.m_muzzle_right.parent.lossyScale.x; // changes if the ship has been scaled

            QdiffRightX = (FIRING_POINTS[8].x - FIRING_POINTS[0].x) * scale;
            QdiffLeftX = -1 * QdiffRightX;
            QdiffY = (FIRING_POINTS[8].y - FIRING_POINTS[0].y) * scale;
            QdiffZ = (FIRING_POINTS[8].z - FIRING_POINTS[0].z) * scale;
        }

        public float GetBoostMulti(float m_boost_heat)
        {
            float extraBoost = 0f;
            if (m_boost_heat < 0.5f)
            {
                extraBoost = boostBurst * 2f * (0.5f - m_boost_heat);
            }
            return boostMulti + extraBoost;
        }

        public float GetBoostMod(float m_boost_heat)
        {
            float extraBoost = 0f;
            if (m_boost_heat < 0.5f)
            {
                extraBoost = boostBurst * 2f * (0.5f - m_boost_heat);
            }
            return boostMod + extraBoost;
        }

        // ====================================================================
        // PlayerShip replacement instance methods/fields
        // ====================================================================

        public Vector3 c_forward;
        public Vector3 c_up;
        public Vector3 c_right;
        public int flak_fire_count;
    }


    // ====================================================================
    //
    //
    // ====================================================================
    // Utility Functions
    // ====================================================================
    //
    //
    // ====================================================================


    // Loads the new Prefabs at game launch
    //[HarmonyPatch(typeof(NetworkSpawnPlayer), "Init")]
    [HarmonyPriority(Priority.First)]
    [HarmonyPatch(typeof(PilotManager), "Initialize")]
    static class MPShips_NetworkSpawnPlayer_Init
    {
        public static void Prefix()
        //public static void Postfix()
        {
            Assets.LoadAssets();
            MPShips.AddShips();
            MPShips.LoadResources();
        }
    }


    // clears the dictionaries and lists before each match
    [HarmonyPatch(typeof(NetworkMatch), "InitBeforeEachMatch")]
    internal class MPShips_NetworkMatch_InitBeforeEachMatch
    {
        static void Postfix()
        {
            MPShips.LobbyShips.Clear();
            MPShips.SelectedShips.Clear();
            MPShips.DeferredShips.Clear();
        }
    }


    // Assigns the chosen Ship definition's properties to the ship object at instantiation on the server
    [HarmonyPatch(typeof(PlayerShip), "Start")]
    static class MPShips_PlayerShip_Start
    {
        static void Postfix(PlayerShip __instance)
        {
            if (GameplayManager.IsMultiplayerActive)
            {
                if (GameplayManager.IsDedicatedServer())
                {
                    NetworkConnection cn = __instance.connectionToClient;
                    int lobby_id = -1;

                    if (cn != null)
                    {
                        lobby_id = cn.connectionId;
                    }

                    NetworkInstanceId net_id = __instance.netId;

                    Debug.Log("CCF instantiate got net id " + net_id + " on server, lobby_id " + lobby_id);

                    MPShips.AssignShip(lobby_id, net_id);

                    if (lobby_id != -1)
                    {
                        foreach (int idx in NetworkMatch.m_players.Keys)
                        {
                            if (idx != 0 && MPTweaks.ClientHasTweak(idx, MPShips.MULTISHIP_VERSION))
                            {
                                var msg = new MPShips.ShipDataToClientMessage
                                {
                                    netId = net_id,
                                    lobbyId = lobby_id,
                                    selected_idx = MPShips.LobbyShips[lobby_id] // error checking for missing stuff needs to happen here
                                };
                                Debug.Log("CCF sending message to client id " + lobby_id + " selected_idx " + msg.selected_idx);
                                NetworkServer.SendToClient(idx, MessageTypes.MsgShipDataToClient, msg);
                            }
                        }
                    }
                    else
                    {
                        Debug.Log("CCF Message dropped in Start()");
                    }


                    Ship s = MPShips.SelectedShips[net_id];
                    s.ApplyParameters(__instance.gameObject);
                }
                else
                {
                    if (MPShips.DeferredShips.Contains(__instance.netId))
                    {
                        Ship s = MPShips.GetShip(__instance);
                        Debug.Log("CCF (deferred) Starting ship type " + s.displayName + " for netId " + __instance.netId + " name " + __instance.c_player.m_mp_name + " on client " + NetworkMatch.m_my_lobby_id);
                        s.ApplyParameters(__instance.gameObject);
                        MPShips.DeferredShips.Remove(__instance.netId);
                    }
                }

                System.Type t = typeof(PlayerShip);
                FieldInfo hl1Rot = t.GetField("_headlight1Rot", BindingFlags.NonPublic | BindingFlags.Instance);
                FieldInfo hl2Rot = t.GetField("_headlight2Rot", BindingFlags.NonPublic | BindingFlags.Instance);
                FieldInfo hlFRot = t.GetField("_headlightFarRot", BindingFlags.NonPublic | BindingFlags.Instance);

                // light rotation is screwy and needs to have references reassigned
                hl1Rot.SetValue(__instance, __instance.c_lights[0].transform.localRotation);
                hl2Rot.SetValue(__instance, __instance.c_lights[1].transform.localRotation);
                hlFRot.SetValue(__instance, __instance.c_lights[2].transform.localRotation);
            }
        }
    }

    // Fixes the ship drag values after respawn to keep the acceleration curve the same
    [HarmonyPatch(typeof(Player), "RestorePlayerShipDataAfterRespawn")]
    static class MPShips_PlayerShip_RestorePlayerShipDataAfterRespawn
    {
        static void Postfix(PlayerShip ___c_player_ship)
        {
            ___c_player_ship.c_rigidbody.drag *= MPShips.GetShip(___c_player_ship).AccelMulti;
            Debug.Log("CCF Restoring drag to " + ___c_player_ship.c_rigidbody.drag);
        }
    }


    // Hides the custom body panels if we're using a non-standard playership
    [HarmonyPatch(typeof(PlayerShip), "SetCustomBody")]
    static class MPShips_PlayerShip_SetCustomBody
    {
        static bool Prefix(PlayerShip __instance)
        {
            if (!MPShips.GetShip(__instance).customizations)
            {
                return false;
            }
            return true;
        }
    }


    // Hides the custom wings if we're using a non-standard playership
    [HarmonyPatch(typeof(PlayerShip), "SetCustomWings")]
    static class MPShips_PlayerShip_SetCustomWings
    {
        static bool Prefix(PlayerShip __instance)
        {
            if (!MPShips.GetShip(__instance).customizations)
            {
                return false;
            }
            return true;
        }
    }

    /* no longer needed

    // moves the quad shot positions in the PlayerShip firing code.
    // Currently this pulls from the selected ship definition. If multiples are to exist in the -same- round, this will need updating again.
    [HarmonyPatch(typeof(PlayerShip), "MaybeFireWeapon")]
    static class MPShips_PlayerShip_MaybeFireWeapon
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes, ILGenerator gen)
        {
            int state = 0;
            int tpcount = 0;
            int idx = gen.DeclareLocal(typeof(Ship)).LocalIndex;

            // stores the player's ship from the static methods -once- at the start to avoid the lookup headaches
            yield return new CodeInstruction(OpCodes.Ldarg_0);
            yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPShips), "GetShip"));
            yield return new CodeInstruction(OpCodes.Stloc_S, idx);

            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Ldc_R4 && state < 6) // changes the quad impulse firepoints
                {
                    switch ((float)code.operand)
                    {
                        case 0.25f: // right quad
                            yield return new CodeInstruction(OpCodes.Ldloc_S, idx);
                            yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(Ship), "QdiffRightX"));
                            state++;
                            break;
                        case -0.25f: // left quad
                            yield return new CodeInstruction(OpCodes.Ldloc_S, idx);
                            yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(Ship), "QdiffLeftX"));
                            state++;
                            break;
                        case -0.15f:
                            yield return new CodeInstruction(OpCodes.Ldloc_S, idx);
                            yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(Ship), "QdiffY"));
                            state++;
                            break;
                        case -0.3f:
                            yield return new CodeInstruction(OpCodes.Ldloc_S, idx);
                            yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(Ship), "QdiffZ"));
                            state++;
                            break;
                        default:
                            yield return code;
                            break;
                    }
                }
                // TEMPORARY WORKAROUND HOPEFULLY
                else if (code.opcode == OpCodes.Ldfld && code.operand == AccessTools.Field(typeof(PlayerShip), "m_thunder_power")) // Tri-TB check
                {
                    tpcount++;
                    yield return code;
                }
                else if (tpcount == 3 && code.opcode == OpCodes.Ldsfld && code.operand == AccessTools.Field(typeof(GameplayManager), "IsMultiplayerActive"))
                {
                    tpcount++;
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPShips), "TriTBFire"));
                    yield return code;
                }
                // END NONSENSE
                else
                {
                    yield return code;
                }
            }
        }
    }

    */

    // Moved from Debugging.cs and modified - replaces the SwitchVisibleWeapon method with one that uses Vector3 for positioning instead of Vector2.
    // I am unsure if the exception catching is necessary or not anymore.
    [HarmonyPatch(typeof(PlayerShip), "SwitchVisibleWeapon")]
    static class MPShips_PlayerShip_SwitchVisibleWeapon
    {
        static bool Prefix(PlayerShip __instance, bool force_visible = false, WeaponType wt = WeaponType.NUM)
        {
            Ship ship = MPShips.GetShip(__instance);

            __instance.m_refire_time = 0.5f; // NOT the right place to do this... but it works!

            if (wt == WeaponType.NUM)
            {
                wt = __instance.c_player.m_weapon_type;
            }
            if (__instance.IsCockpitVisible || force_visible || !__instance.isLocalPlayer)
            {
                for (int i = 0; i < __instance.m_weapon_mounts1.Length; i++) // Length *should* be 8 unless TriTB is on, and then it's 9 and we have a special case
                {
                    bool active = i == (int)wt || (ship.triTB && i == 8 && wt == WeaponType.THUNDERBOLT);
                    try
                    {
                        __instance.m_weapon_mounts1[i].SetActive(active);
                    }
                    catch (System.Exception e)
                    {
                        Debug.Log("Exception setting the first weapon mount's active state.");
                        Debug.LogException(e);
                    }
                    if (__instance.c_player.m_cloaked && active)
                    {
                        MeshRenderer[] componentsInChildren = null;
                        try
                        {
                            componentsInChildren = __instance.m_weapon_mounts1[i].GetComponentsInChildren<MeshRenderer>(includeInactive: true);
                        }
                        catch (System.Exception e)
                        {
                            Debug.Log("Exception getting the first weapon mount's components.");
                            Debug.LogException(e);
                        }
                        if (componentsInChildren != null)
                        {
                            foreach (MeshRenderer meshRenderer in componentsInChildren)
                            {
                                meshRenderer.enabled = false;
                            }
                        }
                    }
                    if (i == 4 || i == 1 || i == 8) // Cyclone or Driller or Tri-TB
                    {
                        continue;
                    }
                    try
                    {
                        __instance.m_weapon_mounts2[i].SetActive(i == (int)wt);
                    }
                    catch (System.Exception e)
                    {
                        Debug.Log("Exception setting the second weapon mount's active state.");
                        Debug.LogException(e);
                    }
                    if (__instance.c_player.m_cloaked && i == (int)wt)
                    {
                        MeshRenderer[] componentsInChildren2 = null;
                        try
                        {
                            componentsInChildren2 = __instance.m_weapon_mounts2[i].GetComponentsInChildren<MeshRenderer>(includeInactive: true);
                        }
                        catch (System.Exception e)
                        {
                            Debug.Log("Exception getting the second weapon mount's components.");
                            Debug.LogException(e);
                        }
                        if (componentsInChildren2 != null)
                        {
                            foreach (MeshRenderer meshRenderer2 in componentsInChildren2)
                            {
                                meshRenderer2.enabled = false;
                            }
                        }
                    }
                }
            }
            if (__instance.c_player.m_weapon_type == WeaponType.DRILLER || __instance.c_player.m_weapon_type == WeaponType.CYCLONE)
            {
                Vector3 localPosition = __instance.m_muzzle_center.localPosition;
                try
                {
                    localPosition.x = ship.FIRING_POINTS[(int)__instance.c_player.m_weapon_type].x;
                    localPosition.y = ship.FIRING_POINTS[(int)__instance.c_player.m_weapon_type].y;
                    localPosition.z = ship.FIRING_POINTS[(int)__instance.c_player.m_weapon_type].z;
                }
                catch (System.Exception e)
                {
                    Debug.Log("Exception getting the firing points for the driller or cyclone.");
                    Debug.LogException(e);
                }
                __instance.m_muzzle_center.localPosition = localPosition;
            }
            else
            {
                Vector3 localPosition2 = __instance.m_muzzle_right.localPosition;
                try
                {
                    localPosition2.x = ship.FIRING_POINTS[(int)__instance.c_player.m_weapon_type].x;
                    localPosition2.y = ship.FIRING_POINTS[(int)__instance.c_player.m_weapon_type].y;
                    localPosition2.z = ship.FIRING_POINTS[(int)__instance.c_player.m_weapon_type].z;
                }
                catch (System.Exception e)
                {
                    Debug.Log("Exception getting the firing points for other weapons.");
                    Debug.LogException(e);
                }
                __instance.m_muzzle_right.localPosition = localPosition2;
                localPosition2.x *= -1;
                __instance.m_muzzle_left.localPosition = localPosition2;
            }
            if (ship.triTB && __instance.c_player.m_weapon_type == WeaponType.THUNDERBOLT) // hacky way to do this but whatever
            {
                //Debug.Log("CCF setting trifire point");
                Vector3 localPosition = __instance.m_muzzle_center.localPosition;
                try
                {
                    localPosition.x = ship.TRIFIRE_POINT.x;
                    localPosition.y = ship.TRIFIRE_POINT.y;
                    localPosition.z = ship.TRIFIRE_POINT.z;
                }
                catch (System.Exception e)
                {
                    Debug.Log("Exception getting the firing points tri-Thunderbolt.");
                    Debug.LogException(e);
                }
                __instance.m_muzzle_center.localPosition = localPosition;
            }
            return false;
        }
    }


    // allows shooting while boost is pressed (if enabled) -- also present in MPAnticheat
    [HarmonyPatch(typeof(PlayerShip), "CanFire")]
    static class MPShips_PlayerShip_CanFire
    {
        static bool Prefix(ref bool __result, PlayerShip __instance)
        {
            __result = (MPShips.FireWhileBoost || !__instance.m_boosting) && __instance.m_wheel_select_state == WheelSelectState.NONE;
            return false;
        }
    }


    // kill the HUD dimming while boosting if shoot while boost is enabled
    [HarmonyPatch(typeof(PlayerShip), "Update")]
    static class MPShips_PlayerShip_Update
    {
        // disable the check if FireWhileBoost is on
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes, ILGenerator gen)
        {
            Label label1 = gen.DefineLabel();
            Label label2 = gen.DefineLabel();

            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Stfld && code.operand == AccessTools.Field(typeof(PlayerShip), "m_boost_zoom"))
                {
                    yield return code;
                    yield return new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(MPShips), "FireWhileBoost"));
                    yield return new CodeInstruction(OpCodes.Brtrue, label1);
                    
                }
                else if (code.opcode == OpCodes.Stfld && code.operand == AccessTools.Field(typeof(PlayerShip), "m_boost_ui_fade"))
                {
                    yield return new CodeInstruction(OpCodes.Br, label2); // jump over the HUD fading cancellation if it's not turned on
                    CodeInstruction jump = new CodeInstruction(OpCodes.Ldarg_0);
                    jump.labels.Add(label1);
                    yield return jump;
                    label1 = gen.DefineLabel(); // fresh label for the second set of calls
                    yield return new CodeInstruction(OpCodes.Ldc_R4, 0f);
                    code.labels.Add(label2);
                    yield return code;
                    label2 = gen.DefineLabel(); // fresh label for the second set of calls
                }
                else
                {
                    yield return code;
                }
            }
        }
    }


    // substitutes the PlayerShip's regular ship handling references with references to the Ship definition classes instead
    [HarmonyPatch(typeof(PlayerShip), "FixedUpdateProcessControlsInternal")]
    static class MPShips_PlayerShip_FixedUpdateProcessControlsInternal
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes, ILGenerator gen)
        {
            bool boost_done = false;
            bool turn_found = false;
            Label label1 = gen.DefineLabel();
            Label label2 = gen.DefineLabel();
            bool LabelNext = false;
            CodeInstruction ci;
            int CurrentShip = gen.DeclareLocal(typeof(Ship)).LocalIndex;

            // stores the player's ship from the static methods -once- at the start to avoid the lookup headaches
            yield return new CodeInstruction(OpCodes.Ldarg_0);
            yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPShips), "GetShip"));
            yield return new CodeInstruction(OpCodes.Stloc_S, CurrentShip);

            foreach (var code in codes)
            {
                // slide force
                if (code.opcode == OpCodes.Ldfld && code.operand == AccessTools.Field(typeof(PlayerShip), "m_slide_force_mp"))
                {
                    yield return new CodeInstruction(OpCodes.Pop); // there's a PlayerShip instance reference to clear out first
                    yield return new CodeInstruction(OpCodes.Ldloc_S, CurrentShip);
                    yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(Ship), "m_slide_force_mp"));
                }
                // turn speed acceleration
                else if (code.opcode == OpCodes.Ldsfld && code.operand == AccessTools.Field(typeof(PlayerShip), "m_turn_speed_limit_acc"))
                {
                    ci = new CodeInstruction(OpCodes.Ldloc_S, CurrentShip); // original had a label on it
                    ci.labels = code.labels;
                    yield return ci;
                    yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(Ship), "m_turn_speed_limit_acc"));
                }
                // turn speed rigidbody something something
                else if (code.opcode == OpCodes.Ldsfld && code.operand == AccessTools.Field(typeof(PlayerShip), "m_turn_speed_limit_rb"))
                {
                    yield return new CodeInstruction(OpCodes.Ldloc_S, CurrentShip);
                    yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(Ship), "m_turn_speed_limit_rb"));
                }
                // modded boost multiplier
                else if (code.opcode == OpCodes.Ldc_R4 && (float)code.operand == 1.85f && !boost_done)
                {
                    //yield return new CodeInstruction(OpCodes.Ldloc_S, CurrentShip);
                    //yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(Ship), "boostMod"));
                    yield return new CodeInstruction(OpCodes.Ldloc_S, CurrentShip);
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(PlayerShip), "m_boost_heat"));
                    //yield return new CodeInstruction(OpCodes.Ldloc_S, WasBoosting);
                    yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(Ship),"GetBoostMod"));
                }
                // regular boost multiplier
                else if (code.opcode == OpCodes.Ldc_R4 && (float)code.operand == 1.65f && !boost_done)
                {
                    //ci = new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(MPShips), "selected")); // more labels
                    //ci = new CodeInstruction(OpCodes.Ldloc_S, CurrentShip); // more labels
                    ci = new CodeInstruction(OpCodes.Ldloc_S, CurrentShip);
                    ci.labels = code.labels;
                    yield return ci;
                    //yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(Ship), "boostMulti"));
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(PlayerShip), "m_boost_heat"));
                    //yield return new CodeInstruction(OpCodes.Ldloc_S, WasBoosting);
                    yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(Ship), "GetBoostMulti"));
                    boost_done = true;
                }
                /*
                // allow disabling of the turn speed boost that comes with overdrive -- I *think* this needs to happen server-side as well unfortunately
                if (code.opcode == OpCodes.Ldc_R4 && (float)code.operand == 1.05f)
                {
                    //turn_found = true;
                    yield return new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(MPShips), "ODTurnUnrestricted"));
                    yield return new CodeInstruction(OpCodes.Brtrue, label1);
                    yield return new CodeInstruction(OpCodes.Ldc_R4, 1f);
                    yield return new CodeInstruction(OpCodes.Br, label2);
                    code.labels.Add(label1);
                    yield return code;
                    LabelNext = true;
                }
                else if (LabelNext)
                {
                    code.labels.Add(label2);
                    yield return code;
                    LabelNext = false;
                }*/
                else
                {
                    yield return code;
                }
            }
        }
    }
    

    // scale back up the flak rangefinder collider if the ship has been scaled
    [HarmonyPatch(typeof(PlayerShip), "UpdateFlakRangeFinder")]
    static class MPShips_PlayerShip_UpdateFlakRangeFinder
    {
        static bool Prefix(PlayerShip __instance, CapsuleCollider ___c_flak_range_collider)
        {
            float scale = __instance.c_main_ship_go.transform.localScale.x;
            Vector3 zero = Vector3.zero;
            if (GameplayManager.IsMultiplayerActive)
            {
                zero.z = 7f / scale;
                ___c_flak_range_collider.center = zero;
                ___c_flak_range_collider.height = 13f / scale;
            }
            else if (__instance.c_player.m_weapon_level[5] == WeaponUnlock.LEVEL_2B)
            {
                zero.z = 8f / scale;
                ___c_flak_range_collider.center = zero;
                ___c_flak_range_collider.height = 15f / scale;
            }
            else
            {
                zero.z = 6f / scale;
                ___c_flak_range_collider.center = zero;
                ___c_flak_range_collider.height = 11f / scale;
            }

            return false;
        }
    }


    // handles the additional Tri-TB mount effects stuff
    [HarmonyPatch(typeof(PlayerShip), "DrawSpawnMesh")]
    static class MPShips_PlayerShip_DrawSpawnMesh
    {
        static void Postfix(PlayerShip __instance, MaterialPropertyBlock ___m_spawn_mpb)
        {
            if (MPShips.GetShip(__instance).triTB && __instance.m_weapon_mounts1[8].activeSelf)
            {
                MeshFilter[] componentsInChildren = __instance.m_weapon_mounts1[8].GetComponentsInChildren<MeshFilter>();
                foreach (MeshFilter meshFilter in componentsInChildren)
                {
                    if (meshFilter.mesh != null)
                    {
                        Matrix4x4 localToWorldMatrix = meshFilter.transform.localToWorldMatrix;
                        Graphics.DrawMesh(meshFilter.mesh, localToWorldMatrix, UIManager.gm.m_teleport_material, 0, GameManager.m_local_player.c_player_ship.c_camera, 0, ___m_spawn_mpb, castShadows: false);
                    }
                }
            }
        }
    }


    // handles the additional Tri-TB mount effects stuff
    [HarmonyPatch(typeof(PlayerShip), "DrawEffectMesh")]
    static class MPShips_PlayerShip_DrawEffectMesh
    {
        static void Postfix(PlayerShip __instance, MaterialPropertyBlock ___m_spawn_mpb, Material mat)
        {
            if (MPShips.GetShip(__instance).triTB && __instance.m_weapon_mounts1[8].activeSelf)
            {
                MeshFilter[] componentsInChildren = __instance.m_weapon_mounts1[8].GetComponentsInChildren<MeshFilter>();
                foreach (MeshFilter meshFilter in componentsInChildren)
                {
                    if (meshFilter.mesh != null)
                    {
                        Matrix4x4 localToWorldMatrix = meshFilter.transform.localToWorldMatrix;
                        Graphics.DrawMesh(meshFilter.mesh, localToWorldMatrix, mat, 0, GameManager.m_local_player.c_player_ship.c_camera, 0, ___m_spawn_mpb, castShadows: false);
                    }
                }
            }
        }
    }


    // handles the additional Tri-TB mount effects stuff
    [HarmonyPatch(typeof(PlayerShip), "DrawEffectMeshWeapon")]
    static class MPShips_PlayerShip_DrawEffectMeshWeapon
    {
        static void Postfix(PlayerShip __instance, MaterialPropertyBlock ___m_spawn_mpb, Material mat)
        {
            if (MPShips.GetShip(__instance).triTB && __instance.m_weapon_mounts1[8].activeSelf)
            {
                MeshFilter[] componentsInChildren = __instance.m_weapon_mounts1[8].GetComponentsInChildren<MeshFilter>();
                foreach (MeshFilter meshFilter in componentsInChildren)
                {
                    if (meshFilter.mesh != null)
                    {
                        Matrix4x4 localToWorldMatrix = meshFilter.transform.localToWorldMatrix;
                        Graphics.DrawMesh(meshFilter.mesh, localToWorldMatrix, mat, 0, GameManager.m_local_player.c_player_ship.c_camera, 0, ___m_spawn_mpb, castShadows: false);
                    }
                }
            }
        }
    }


    // handles shield strength scaling at the damage side
    [HarmonyPatch(typeof(PlayerShip), "ApplyDamage")]
    static class MPShips_PlayerShip_ApplyDamage
    {
        static void Prefix(PlayerShip __instance, ref DamageInfo di)
        {
            di.damage /= MPShips.GetShip(__instance).ShieldMultiplier;
        }
    }


    // handles shield strength scaling at the pickup side
    [HarmonyPatch(typeof(Player), "AddArmor")]
    static class MPShips_Player_AddArmor
    {
        static void Prefix(Player __instance, ref float armor)
        {
            armor /= MPShips.GetShip(__instance.c_player_ship).ShieldMultiplier;
        }
    }

    
    // Ships that explode now deal damage to enemies.
    [HarmonyPatch(typeof(PlayerShip), "SpewItemsOnDeath")]
    class MPShips_PlayerShip_SpewItemsOnDeath
    {
        static void Prefix(PlayerShip __instance)
        {
            if (MPShips.allowed == 0)
                return;

            //GameManager.m_audio.PlayCuePos((int)NewSounds.MortarExplode, __instance.c_transform_position, 0.3f, -0.5f);
            GameManager.m_light_manager.CreateLightFlash(__instance.c_transform_position, Color.white, 10f, 10f, 0.2f, false);
        }

        static void Postfix(PlayerShip __instance)
        {
            if (!GameplayManager.IsDedicatedServer())
                return;

            Vector3 pos = __instance.c_transform_position;
            Collider[] colls = Physics.OverlapSphere(pos, 6f, 65536); // 6-unit is actually about ~2 ship lengths of space hull-to-hull

            foreach (Collider coll in colls)
            {
                PlayerShip ps = coll.GetComponent<PlayerMeshCollider>().c_player.c_player_ship;
                if (ps != __instance && !Physics.Linecast(pos, coll.transform.position, 67256320)) // make sure there is line-of-sight
                {
                    float dscale = 1f - (Vector3.Distance(pos, coll.transform.position) / 6f);
                    DamageInfo di = new DamageInfo()
                    {
                        owner = __instance.c_go,
                        stun_multiplier = 0f,
                        push_force = 10f * dscale,
                        push_torque = 0f,
                        damage = 45f * dscale, // actually comes out to ~20 with damage reduction on if you're literally touching the exploding ship
                        pos = pos,
                        push_dir = ps.c_transform_position - pos,
                        type = DamageType.EXPLOSIVE,
                        weapon = ProjPrefab.proj_melee,
                        force_death = false,
                        robot_owner = null
                    };
                    ps.ApplyDamage(di);
                }
            }
        }
    }
    

    // ====================================================================
    //
    //
    // ====================================================================
    // Network Functions
    // ====================================================================
    //
    //
    // ====================================================================



    // Server-side player ship selection handling
    [HarmonyPatch(typeof(Server), "RegisterHandlers")]
    public static class MPShips_Server_RegisterHandlers
    {
        static void Postfix()
        {
            NetworkServer.RegisterHandler(MessageTypes.MsgShipDataToServer, OnShipDataToServerMessage);
        }


        private static void OnShipDataToServerMessage(NetworkMessage rawMsg)
        {
            var msg = rawMsg.ReadMessage<MPShips.ShipDataToServerMessage>();

            int idx;
            switch (MPShips.allowed)
            {
                case 0:
                    idx = 0;
                    break;
                case 1:
                    idx = msg.selected_idx;
                    break;
                default:
                    idx = MPShips.allowed - 2;
                    break;
            }
            MPShips.AssignLobbyShip(msg.lobbyId, idx);
            Debug.Log("CCF received ship (server), lobby_id " + msg.lobbyId + ", ship idx " + msg.selected_idx);
        }
    }


    // Client-side player ship selection handling
    [HarmonyPatch(typeof(Client), "RegisterHandlers")]
    public static class MPShips_Client_RegisterHandlers
    {
        static void Postfix()
        {
            if (Client.GetClient() == null)
                return;

            Client.GetClient().RegisterHandler(MessageTypes.MsgShipDataToClient, OnShipDataToClientMessage);
        }

        private static void OnShipDataToClientMessage(NetworkMessage rawMsg)
        {
            var msg = rawMsg.ReadMessage<MPShips.ShipDataToClientMessage>();
            MPShips.AssignLobbyShip(msg.lobbyId, msg.selected_idx);
            MPShips.AssignShip(msg.lobbyId, msg.netId);
            Debug.Log("CCF received ship (client), lobby_id " + msg.lobbyId + ", netId " + msg.netId + ", ship idx " + msg.selected_idx);

            // find the ship referenced and attach the right parameters to it
            PlayerShip ps = MPShips.GetPlayerShipFromNetId(msg.netId);

            if (ps != null) // ship has already been instantiated
            {
                Ship s = MPShips.GetShip(ps);
                Debug.Log("CCF Starting ship type " + s.displayName + " for netId " + ps.netId + " name " + ps.c_player.m_mp_name + " on client " + NetworkMatch.m_my_lobby_id);
                s.ApplyParameters(ps.gameObject);
            }
            else // ship hasn't been instantiated yet, store it for the Start() stage
            {
                Debug.Log("CCF Ship not found for netId " + msg.netId + " name on client " + NetworkMatch.m_my_lobby_id + ", storing for Start()");
                MPShips.DeferredShips.Add(msg.netId);
            }
        }
    }


    [HarmonyPatch(typeof(Client), "SendPlayerLoadoutToServer")]
    internal class MPShips_Client_SendPlayerLoadoutToServer
    {
        static void Postfix()
        {
            Debug.Log("CCF sending ship to server from client " + NetworkMatch.m_my_lobby_id);
            MPShips.SendShipSelectionToServer();
        }
    }
}
