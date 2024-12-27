using HarmonyLib;

using Overload;

using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Text;

using UnityEngine;
using UnityEngine.Networking;

namespace GameMod
{

    // commandline arguments:
    //      telemetry   = enables the telemetry output
    //      telemetry-ip = ip to send udp packets towards (optional)
    //      telemetry-port = port to send udp packets towards (optional)

    /// <summary>
    /// v 1.1.1
    /// </summary>
    internal class TelemetryMod
    {
        public static bool telemetry_enabled = false;
        public static string telemetry_ip = "127.0.0.1";
        public static int telemetry_port = 4123;
        private static float event_boosting = 0.0f;
        private static float event_primary_fire = 0.0f;
        private static float event_secondary_fire = 0.0f;
        private static float event_picked_up_item = 0.0f;
        private static float event_damage_taken = 0.0f;
        private static Telemetry telemetryComponent;
        private static bool initialized = false;
        private static GameObject udpSenderObject;
        private static Vector3 previousVelocity = Vector3.zero; 
        private static Vector3 previousLocalVelocity = Vector3.zero;


        [HarmonyPatch(typeof(PlayerShip), "FixedUpdateProcessControlsInternal")]
        private class TelemetryMod_PlayerShip_FixedUpdateProcessControlsInternal
        {
            private static void Prefix()
            {
                if (!telemetry_enabled)
                    return;
                event_primary_fire = GameManager.m_local_player.IsPressed((CCInput)14) ? 1f : 0.0f;
                event_secondary_fire = GameManager.m_local_player.IsPressed((CCInput)15) ? 1f : 0.0f;
            }
        }

        [HarmonyPatch(typeof(Item), "PlayItemPickupFX")]
        private class TelemetryMod_Item_PlayItemPickupFX
        {
            private static void Postfix(Player player)
            {
                if (!telemetry_enabled || player == null || !player.isLocalPlayer)
                    return;
                event_secondary_fire = 1f;
            }
        }

        [HarmonyPatch(typeof(PlayerShip), "ApplyDamage")]
        private class TelemetryMod_PlayerShip_ApplyDamage
        {
            private static void Postfix(DamageInfo di, PlayerShip __instance)
            {
                if (!telemetry_enabled || __instance == null || !__instance.isLocalPlayer)
                    return;
                event_damage_taken += di.damage;
            }
        }

        [HarmonyPatch(typeof(GameManager), "FixedUpdate")]
        private class TelemetryMod_GameManager_FixedUpdate
        {
            private static void Postfix()
            {
                if (!initialized & GameManager.m_local_player != null)
                {
                    initialized = true;
                    udpSenderObject = new GameObject("UdpTelemetrySender");
                    telemetryComponent = udpSenderObject.AddComponent<Telemetry>();
                    telemetryComponent.IP = telemetry_ip;
                    telemetryComponent.port = telemetry_port;
                }
                else
                {
                    if (!initialized)
                        return;
                    event_boosting = GameManager.m_local_player.c_player_ship.m_boosting ? 1f : 0.0f;
                    if (GameplayManager.m_gameplay_state == 0)
                    {
                        Rigidbody cRigidbody = GameManager.m_local_player.c_player_ship.c_rigidbody;
                        Quaternion rotation = cRigidbody.rotation;
                        Vector3 eulerAngles = rotation.eulerAngles;
                        Vector3 angularVelocity = cRigidbody.angularVelocity;

                        // angular velocity relative to object
                        Vector3 localAngularVelocity = cRigidbody.transform.InverseTransformDirection(cRigidbody.angularVelocity);

                        // velocity relative to object
                        Vector3 localVelocity = cRigidbody.transform.InverseTransformDirection(cRigidbody.velocity);

                        Vector3 gforce = (cRigidbody.velocity - previousVelocity) / Time.fixedDeltaTime / 9.81f;
                        previousVelocity = cRigidbody.velocity;

                        Vector3 lgforce = (localVelocity - previousLocalVelocity) / Time.fixedDeltaTime / 9.81f;
                        previousLocalVelocity = localVelocity;


                        Telemetry.Telemetry_SendTelemetry(  new Vector3(
                            eulerAngles.x > 180.0 ? eulerAngles.x - 360f : eulerAngles.x, 
                            eulerAngles.y > 180.0 ? eulerAngles.y - 360f : eulerAngles.y, 
                            eulerAngles.z > 180.0 ? eulerAngles.z - 360f : eulerAngles.z),
                            angularVelocity,
                            gforce,
                            event_boosting,
                            event_primary_fire,
                            event_secondary_fire,
                            event_picked_up_item,
                            event_damage_taken,
                            lgforce,
                            localAngularVelocity,
                            localVelocity);
                    }
                    else
                        Telemetry.Telemetry_SendTelemetry(Vector3.zero, Vector3.zero, Vector3.zero, 0f, 0f, 0f, 0f, 0f, Vector3.zero, Vector3.zero, Vector3.zero);

                    event_boosting = 0.0f;
                    event_primary_fire = 0.0f;
                    event_secondary_fire = 0.0f;
                    event_picked_up_item = 0.0f;
                    event_damage_taken = 0.0f;
                }
            }
        }

        private class PlayerData
        {
            /// <summary>
            /// pitch (x), yaw (y), roll (z)
            /// </summary>
            public Vector3 Rotation;

            /// <summary>
            /// pitch (x), yaw (y), roll (z)
            /// </summary>
            public Vector3 AngularVelocity;

            /// <summary>
            /// sway (x), heave (y), surge (z)
            /// </summary>
            public Vector3 GForce;

            /// <summary>
            /// pitch (x), yaw (y), roll (z)
            /// </summary>
            public Vector3 LocalAngularVelocity;

            /// <summary>
            /// sway (x), heave (y), surge (z)
            /// </summary>
            public Vector3 LocalVelocity;

            /// <summary>
            /// sway (x), heave (y), surge (z)
            /// </summary>
            public Vector3 LocalGForce;

            public float EventBoosting;
            public float EventPrimaryFire;
            public float EventSecondaryFire;
            public float EventItemPickup;
            public float EventDamageTaken;

            

            public PlayerData()
            {
            }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="rotation">pitch(x), yaw(y), roll(z)</param>
            /// <param name="angularVelocity">pitch (x), yaw (y), roll (z)</param>
            /// <param name="gforce">sway (x), heave (y), surge (z)</param>
            /// <param name="boosting"></param>
            /// <param name="primaryFire"></param>
            /// <param name="secondaryFire"></param>
            /// <param name="itemPickup"></param>
            /// <param name="damageTaken"></param>
            /// <param name="localgForce">sway (x), heave (y), surge (z)</param>
            /// <param name="localAngularVelocity">pitch (x), yaw (y), roll (z)</param>
            /// <param name="localVelocity">sway (x), heave (y), surge (z)</param>
            public PlayerData(
              Vector3 rotation,
              Vector3 angularVelocity,
              Vector3 gforce,
              float boosting,
              float primaryFire,
              float secondaryFire,
              float itemPickup,
              float damageTaken,
              Vector3 localgForce,
              Vector3 localAngularVelocity,
              Vector3 localVelocity
              )
            {
                Rotation = rotation;
                AngularVelocity = angularVelocity;
                GForce = gforce;
                
                EventBoosting = boosting;
                EventPrimaryFire = primaryFire;
                EventSecondaryFire = secondaryFire;
                EventItemPickup = itemPickup;
                EventDamageTaken = damageTaken;

                LocalGForce = localgForce;
                LocalAngularVelocity = localAngularVelocity;
                LocalVelocity = localVelocity;

            }
        }

        public class Telemetry : MonoBehaviour
        {
            public string IP = "127.0.0.1";
            public int port = 4123;
            private IPEndPoint remoteEndPoint;
            private static UdpClient client;
            private static PlayerData local_player_data;

            private void Start()
            {
                DontDestroyOnLoad(gameObject);
                this.remoteEndPoint = new IPEndPoint(IPAddress.Parse(IP), port);
                client = new UdpClient();
                local_player_data = new PlayerData();
                this.StartCoroutine("Telemetry_Start");
            }

            public static void Telemetry_SendTelemetry(
              Vector3 rotation,
              Vector3 angularVelocity,
              Vector3 gforce,
              float boosting,
              float primaryFire, float secondaryFire,
              float itemPickup,
              float damageTaken,
              Vector3 localgForce,
              Vector3 localAngularVelocity,
              Vector3 localVelocity)
            {
                local_player_data = new PlayerData(rotation,
                                                   angularVelocity,
                                                   gforce,
                                                   boosting,
                                                   primaryFire, secondaryFire,
                                                   itemPickup,
                                                   damageTaken,
                                                   localgForce,
                                                   localAngularVelocity,
                                                   localVelocity);
            }

            private IEnumerator Telemetry_Start()
            {
                while (true)
                {
                    string info = string.Format("{0};{1};{2};{3};{4};{5};{6};{7};{8};{9};{10};{11};{12};{13};{14};{15};{16};{17};{18};{19};{20};{21};{22}", 
                        local_player_data.Rotation.z, local_player_data.Rotation.x, local_player_data.Rotation.y,                       // out of order for backwards compatibility with simtools
                        local_player_data.AngularVelocity.z, local_player_data.AngularVelocity.x, local_player_data.AngularVelocity.y,  // out of order for backwards compatibility with simtools
                        local_player_data.GForce.x, local_player_data.GForce.y, local_player_data.GForce.z, 
                        local_player_data.EventBoosting, 
                        local_player_data.EventPrimaryFire, local_player_data.EventSecondaryFire, 
                        local_player_data.EventItemPickup, local_player_data.EventDamageTaken,
                        local_player_data.LocalGForce.x, local_player_data.LocalGForce.y, local_player_data.LocalGForce.z,
                        local_player_data.LocalAngularVelocity.x, local_player_data.LocalAngularVelocity.y, local_player_data.LocalAngularVelocity.z,
                        local_player_data.LocalVelocity.x, local_player_data.LocalVelocity.y, local_player_data.LocalVelocity.z);
                    
                    byte[] data = Encoding.Default.GetBytes(info);
                    client.Send(data, data.Length, this.remoteEndPoint);
                    yield return null;
                    info = null;
                    data = null;
                }
            }
        }
    }
}
