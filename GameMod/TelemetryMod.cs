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
    private static TelemetryMod.Telemetry telemetryComponent;
    private static bool initialized = false;
    private static GameObject udpSenderObject;
    private static Vector3 previousVelocity = Vector3.zero;

    [HarmonyPatch(typeof (PlayerShip), "FixedUpdateProcessControlsInternal")]
    private class TelemetryMod_PlayerShip_FixedUpdateProcessControlsInternal
    {
      private static void Prefix()
      {
        if (!TelemetryMod.telemetry_enabled)
          return;
        TelemetryMod.event_primary_fire = GameManager.m_local_player.IsPressed((CCInput) 14) ? 1f : 0.0f;
        TelemetryMod.event_secondary_fire = GameManager.m_local_player.IsPressed((CCInput) 15) ? 1f : 0.0f;
      }
    }

    [HarmonyPatch(typeof (Item), "PlayItemPickupFX")]
    private class TelemetryMod_Item_PlayItemPickupFX
    {
      private static void Postfix(Player player)
      {
        if (!TelemetryMod.telemetry_enabled || (Object) player == null || !((NetworkBehaviour) player).isLocalPlayer)
          return;
        TelemetryMod.event_secondary_fire = 1f;
      }
    }

    [HarmonyPatch(typeof (PlayerShip), "ApplyDamage")]
    private class TelemetryMod_PlayerShip_ApplyDamage
    {
      private static void Postfix(DamageInfo di, PlayerShip __instance)
      {
        if (!TelemetryMod.telemetry_enabled || __instance ==  null || !((NetworkBehaviour) __instance).isLocalPlayer)
          return;
        TelemetryMod.event_damage_taken += di.damage;
      }
    }

    [HarmonyPatch(typeof (GameManager), "FixedUpdate")]
    private class TelemetryMod_GameManager_FixedUpdate
    {
      private static void Postfix()
      {
        if (!TelemetryMod.initialized & (Object) GameManager.m_local_player != null)
        {
          TelemetryMod.initialized = true;
          TelemetryMod.udpSenderObject = new GameObject("UdpTelemetrySender");
          TelemetryMod.telemetryComponent = TelemetryMod.udpSenderObject.AddComponent<TelemetryMod.Telemetry>();
          TelemetryMod.telemetryComponent.IP = "127.0.0.1";
          TelemetryMod.telemetryComponent.port = 4123;
        }
        else
        {
          if (!TelemetryMod.initialized)
            return;
          TelemetryMod.event_boosting = GameManager.m_local_player.c_player_ship.m_boosting ? 1f : 0.0f;
          if (GameplayManager.m_gameplay_state == 0)
          {
            Rigidbody cRigidbody = GameManager.m_local_player.c_player_ship.c_rigidbody;
            Quaternion rotation = cRigidbody.rotation;
            Vector3 eulerAngles = ((Quaternion) rotation).eulerAngles;
            Vector3 angularVelocity = cRigidbody.angularVelocity;
            Vector3 vector3 = (cRigidbody.velocity - TelemetryMod.previousVelocity) / Time.fixedDeltaTime / 9.81f;
            TelemetryMod.previousVelocity = cRigidbody.velocity;
            TelemetryMod.Telemetry.Telemetry_SendTelemetry((double)eulerAngles.z > 180.0 ? eulerAngles.z - 360f : eulerAngles.z,
                                                           (double)eulerAngles.x > 180.0 ? eulerAngles.x - 360f : eulerAngles.x,
                                                           (double)eulerAngles.y > 180.0 ? eulerAngles.y - 360f : eulerAngles.y,
                                                           angularVelocity.z,
                                                           angularVelocity.x,
                                                           angularVelocity.y,
                                                           vector3.x,
                                                           vector3.y,
                                                           vector3.z,
                                                           TelemetryMod.event_boosting,
                                                           TelemetryMod.event_primary_fire,
                                                           TelemetryMod.event_secondary_fire,
                                                           TelemetryMod.event_picked_up_item,
                                                           TelemetryMod.event_damage_taken);
          }
          else
            TelemetryMod.Telemetry.Telemetry_SendTelemetry(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f);
          TelemetryMod.event_boosting = 0.0f;
          TelemetryMod.event_primary_fire = 0.0f;
          TelemetryMod.event_secondary_fire = 0.0f;
          TelemetryMod.event_picked_up_item = 0.0f;
          TelemetryMod.event_damage_taken = 0.0f;
        }
      }
    }

    private class PlayerData
    {
      public float Roll;
      public float Pitch;
      public float Yaw;
      public float Heave;
      public float Sway;
      public float Surge;
      public float Extra1;
      public float Extra2;
      public float Extra3;
      public float EventBoosting;
      public float EventPrimaryFire;
      public float EventSecondaryFire;
      public float EventItemPickup;
      public float EventDamageTaken;

      public PlayerData()
      {
      }

      public PlayerData(
        float Roll,
        float Pitch,
        float Yaw,
        float Heave,
        float Sway,
        float Surge,
        float Extra1,
        float Extra2,
        float Extra3,
        float Boosting,
        float PrimaryFire,
        float SecondaryFire,
        float ItemPickup,
        float DamageTaken)
      {
        this.Roll = Roll;
        this.Pitch = Pitch;
        this.Yaw = Yaw;
        this.Heave = Heave;
        this.Sway = Sway;
        this.Surge = Surge;
        this.Extra1 = Extra1;
        this.Extra2 = Extra2;
        this.Extra3 = Extra3;
        this.EventBoosting = Boosting;
        this.EventPrimaryFire = PrimaryFire;
        this.EventSecondaryFire = SecondaryFire;
        this.EventItemPickup = ItemPickup;
        this.EventDamageTaken = DamageTaken;
      }
    }

    public class Telemetry : MonoBehaviour
    {
      public string IP = "127.0.0.1";
      public int port = 4123;
      private IPEndPoint remoteEndPoint;
      private static UdpClient client;
      private static TelemetryMod.PlayerData local_player_data;

      private void Start()
      {
        Object.DontDestroyOnLoad((Object) ((Component) this).gameObject);
        this.remoteEndPoint = new IPEndPoint(IPAddress.Parse(this.IP), this.port);
        TelemetryMod.Telemetry.client = new UdpClient();
        TelemetryMod.Telemetry.local_player_data = new TelemetryMod.PlayerData();
        this.StartCoroutine("Telemetry_Start");
      }

      public static void Telemetry_SendTelemetry(
        float Roll,
        float Pitch,
        float Yaw,
        float Heave,
        float Sway,
        float Surge,
        float Extra1,
        float Extra2,
        float Extra3,
        float Boosting,
        float PrimaryFire,
        float SecondaryFire,
        float ItemPickup,
        float DamageTaken)
      {
        TelemetryMod.Telemetry.local_player_data = new TelemetryMod.PlayerData(Roll, Pitch, Yaw, Heave, Sway, Surge, Extra1, Extra2, Extra3, Boosting, PrimaryFire, SecondaryFire, ItemPickup, DamageTaken);
      }

      private IEnumerator Telemetry_Start()
      {
        while (true)
        {
          string info = string.Format("{0};{1};{2};{3};{4};{5};{6};{7};{8};{9};{10};{11};{12};{13}", (object) TelemetryMod.Telemetry.local_player_data.Roll, (object) TelemetryMod.Telemetry.local_player_data.Pitch, (object) TelemetryMod.Telemetry.local_player_data.Yaw, (object) TelemetryMod.Telemetry.local_player_data.Heave, (object) TelemetryMod.Telemetry.local_player_data.Sway, (object) TelemetryMod.Telemetry.local_player_data.Surge, (object) TelemetryMod.Telemetry.local_player_data.Extra1, (object) TelemetryMod.Telemetry.local_player_data.Extra2, (object) TelemetryMod.Telemetry.local_player_data.Extra3, (object) TelemetryMod.Telemetry.local_player_data.EventBoosting, (object) TelemetryMod.Telemetry.local_player_data.EventPrimaryFire, (object) TelemetryMod.Telemetry.local_player_data.EventSecondaryFire, (object) TelemetryMod.Telemetry.local_player_data.EventItemPickup, (object) TelemetryMod.Telemetry.local_player_data.EventDamageTaken);
          byte[] data = Encoding.Default.GetBytes(info);
          TelemetryMod.Telemetry.client.Send(data, data.Length, this.remoteEndPoint);
          yield return (object) null;
          info = (string) null;
          data = (byte[]) null;
        }
      }
    }
  }
}
