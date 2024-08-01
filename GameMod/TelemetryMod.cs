// Decompiled with JetBrains decompiler
// Type: GameMod.TelemetryMod
// Assembly: GameMod, Version=0.5.13.0, Culture=neutral, PublicKeyToken=null
// MVID: 07E71B55-E588-4E1E-892E-01FD6C135707
// Assembly location: H:\SteamLibrary\steamapps\common\Overload\GameMod.dll

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
    internal class TelemetryMod
    {
        private static bool telemetry_enabled = true;
        private static float event_boosting = 0.0f;
        private static float event_primary_fire = 0.0f;
        private static float event_secondary_fire = 0.0f;
        private static float event_picked_up_item = 0.0f;
        private static float event_damage_taken = 0.0f;
        private static Telemetry telemetryComponent;
        private static bool initialized = false;
        private static GameObject udpSenderObject;
        private static Vector3 previousVelocity = Vector3.zero;


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
                    telemetryComponent.IP = "127.0.0.1";
                    telemetryComponent.port = 4123;
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
                        Vector3 gforce = (cRigidbody.velocity - previousVelocity) / Time.fixedDeltaTime / 9.81f;
                        previousVelocity = cRigidbody.velocity;


                        // angular velocity relative to object
                        Vector3 localAv = cRigidbody.transform.InverseTransformDirection(cRigidbody.angularVelocity);

                        // velocity relative to object
                        Vector3 lv = cRigidbody.transform.InverseTransformDirection(cRigidbody.velocity);

                        Telemetry.Telemetry_SendTelemetry(eulerAngles.z > 180.0 ? eulerAngles.z - 360f : eulerAngles.z,
                                                                       eulerAngles.x > 180.0 ? eulerAngles.x - 360f : eulerAngles.x,
                                                                       eulerAngles.y > 180.0 ? eulerAngles.y - 360f : eulerAngles.y,
                                                                       angularVelocity.x,
                                                                       angularVelocity.y,
                                                                       angularVelocity.z,
                                                                       gforce.x,
                                                                       gforce.y,
                                                                       gforce.z,
                                                                       event_boosting,
                                                                       event_primary_fire,
                                                                       event_secondary_fire,
                                                                       event_picked_up_item,
                                                                       event_damage_taken,
                                                                       localAv.x,
                                                                       localAv.y,
                                                                       localAv.z,
                                                                       lv.x,
                                                                       lv.y,
                                                                       lv.z);
                    }
                    else
                        Telemetry.Telemetry_SendTelemetry(0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f);

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
            public float Roll;
            public float Pitch;
            public float Yaw;
            
            public float AngularVelocityX;
            public float AngularVelocityY;
            public float AngularVelocityZ;

            public float GForceX;
            public float GForceY;
            public float GForceZ;
            public float EventBoosting;
            public float EventPrimaryFire;
            public float EventSecondaryFire;
            public float EventItemPickup;
            public float EventDamageTaken;

            public float LocalAngularVelocityX;
            public float LocalAngularVelocityY;
            public float LocalAngularVelocityZ;

            public float Sway;
            public float Heave;
            public float Surge;

            public PlayerData()
            {
            }

            public PlayerData(
              float roll,
              float pitch,
              float yaw,
              float angularVelocityX,
              float angularVelocityY,
              float angularVelocityZ,
              float gforcex,
              float geforcey,
              float geforcez,
              float boosting,
              float primaryFire,
              float secondaryFire,
              float itemPickup,
              float damageTaken,
              float localAngularVelocityX,
              float localAngularVelocityY,
              float localAngularVelocityZ,
              float sway,
              float heave,
              float surge
              )
            {
                Roll = roll;
                Pitch = pitch;
                Yaw = yaw;
                
                
                AngularVelocityX = angularVelocityX;
                AngularVelocityY = angularVelocityY;
                AngularVelocityZ = angularVelocityZ;

                GForceX = gforcex;
                GForceY = geforcey;
                GForceZ = geforcez;
                
                EventBoosting = boosting;
                EventPrimaryFire = primaryFire;
                EventSecondaryFire = secondaryFire;
                EventItemPickup = itemPickup;
                EventDamageTaken = damageTaken;

                
                LocalAngularVelocityX = localAngularVelocityX;
                LocalAngularVelocityY = localAngularVelocityY;
                LocalAngularVelocityZ = localAngularVelocityZ;


                Sway = sway;
                Heave = heave;
                Surge = surge;

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
                this.remoteEndPoint = new IPEndPoint(IPAddress.Parse(this.IP), this.port);
                client = new UdpClient();
                local_player_data = new PlayerData();
                this.StartCoroutine("Telemetry_Start");
            }

            public static void Telemetry_SendTelemetry(
              float roll, float pitch, float yaw,
              float angularVelocityX, float angularVelocityY, float angularVelocityZ,
              float gforcex, float gforcey, float gforcez,
              float boosting,
              float primaryFire, float secondaryFire,
              float itemPickup,
              float damageTaken,
              float localAngularVelocityX, float localAngularVelocityY, float localAngularVelocityZ,
              float sway, float heave, float surge)
            {
                local_player_data = new PlayerData(roll, pitch, yaw,
                                                   angularVelocityX, angularVelocityY, angularVelocityZ,
                                                   gforcex, gforcey, gforcez,
                                                   boosting,
                                                   primaryFire, secondaryFire,
                                                   itemPickup,
                                                   damageTaken,
                                                   localAngularVelocityX, localAngularVelocityY, localAngularVelocityZ,
                                                   sway, heave, surge);
            }

            private IEnumerator Telemetry_Start()
            {
                while (true)
                {
                    string info = string.Format("{0};{1};{2};{3};{4};{5};{6};{7};{8};{9};{10};{11};{12};{13};{14};{15};{16};{17};{18};{19}", 
                        local_player_data.Roll, local_player_data.Pitch, local_player_data.Yaw, 
                        local_player_data.AngularVelocityZ, local_player_data.AngularVelocityX, local_player_data.AngularVelocityY, 
                        local_player_data.GForceX, local_player_data.GForceY, local_player_data.GForceZ, 
                        local_player_data.EventBoosting, 
                        local_player_data.EventPrimaryFire, local_player_data.EventSecondaryFire, 
                        local_player_data.EventItemPickup, local_player_data.EventDamageTaken,
                        local_player_data.LocalAngularVelocityX, local_player_data.LocalAngularVelocityY, local_player_data.LocalAngularVelocityZ,
                        local_player_data.Sway, local_player_data.Heave, local_player_data.Surge);
                    
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
