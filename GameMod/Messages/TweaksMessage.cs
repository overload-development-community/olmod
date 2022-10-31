using System.Collections.Generic;
using GameMod.Metadata;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Networking;

namespace GameMod.Messages {
    /// <summary>
    /// A message that includes the tweaks 
    /// </summary>
    [Mod(Mods.Tweaks)]
    public class TweaksMessage : MessageBase {
        public Dictionary<string, string> m_settings;

        public override void Serialize(NetworkWriter writer) {
            writer.Write((byte)1); // version
            writer.WritePackedUInt32((uint)m_settings.Count);
            foreach (var x in m_settings) {
                writer.Write(x.Key);
                writer.Write(x.Value);
            }
        }

        public override void Deserialize(NetworkReader reader) {
            var version = reader.ReadByte();
            int count = (int)reader.ReadPackedUInt32();
            if (m_settings == null)
                m_settings = new Dictionary<string, string>();
            m_settings.Clear();
            for (int i = 0; i < count; i++) {
                string key = reader.ReadString();
                string value = reader.ReadString();
                m_settings[key] = value;
            }
        }

        public static void ClientHandler(NetworkMessage rawMsg) {
            var msg = rawMsg.ReadMessage<TweaksMessage>();
            MPTweaks.Set(msg.m_settings);
        }

        public static void ServerHandler(NetworkMessage rawMsg) {
            var msg = rawMsg.ReadMessage<TweaksMessage>();
            Debug.Log($"MPTweaks: received client capabilities {rawMsg.conn.connectionId}: {msg.m_settings.Join()}");
            MPTweaks.ClientCapabilitiesSet(rawMsg.conn.connectionId, msg.m_settings);
        }
    }
}
