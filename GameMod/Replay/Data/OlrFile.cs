using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Ionic.Zlib;
using UnityEngine;

namespace GameMod.Replay
{
    public static class OlrFile
    {
        public const byte VERSION = 13;

        public static string Directory
        {
            get { return Path.Combine(Application.persistentDataPath, "Replays"); }
        }

        private static string GameDirectory
        {
            get
            {
                try { return Path.GetDirectoryName(Application.dataPath); }
                catch (ArgumentException) { return null; }
            }
        }

        public static void EnsureDirectory()
        {
            try
            {
                if (!System.IO.Directory.Exists(Directory))
                    System.IO.Directory.CreateDirectory(Directory);
            }
            catch (Exception e)
            {
                Debug.LogErrorFormat("REPLAY: could not create replay dir: {0}", e);
            }
        }

        public static string Save(Recording replay)
        {
            EnsureDirectory();
            string fileName = "Replay_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" +
                              SanitizeFileName(replay.sceneName) + ".olr";
            return SaveTo(replay, Path.Combine(Directory, fileName));
        }

        public static string SaveTo(Recording replay, string path)
        {
            replay.frames.Sort((a, b) => a.t.CompareTo(b.t));
            replay.events.Sort((a, b) => a.t.CompareTo(b.t));
            replay.chat.Sort((a, b) => a.t.CompareTo(b.t));
            NormalizeTimeline(replay);
            if (replay.recordedUtcBinary == 0)
                replay.recordedUtcBinary = DateTime.UtcNow.ToBinary();

            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                stream.WriteByte(VERSION); // the version byte stays outside the zlib stream

                using (var writer = new BinaryWriter(
                    // Level3 is the measured knee: higher levels cost save time for little size
                    new ZlibStream(stream, CompressionMode.Compress, CompressionLevel.Level3)))
                {
                    WriteBody(writer, replay);
                }
            }
            Debug.LogFormat("REPLAY: saved {0} ({1} frames, {2} events, {3} chat)", path, replay.frames.Count, replay.events.Count, replay.chat.Count);
            return path;
        }

        private static void WriteBody(BinaryWriter writer, Recording replay)
        {
            WriteHeader(writer, replay);

            writer.Write(replay.frames.Count);
            foreach (var frame in replay.frames)
            {
                writer.Write(frame.t);
                for (int i = 0; i < replay.players.Count; i++)
                {
                    if (i >= frame.ships.Length)
                        writer.Write((byte)0);
                    else
                        WriteShipState(writer, ref frame.ships[i]);
                }
                WriteItemSamples(writer, frame.items);
                WriteObjectiveState(writer, ref frame.objective);
            }

            writer.Write(replay.events.Count);
            foreach (var ev in replay.events)
                WriteEvent(writer, ev);

            writer.Write(replay.chat.Count);
            foreach (var message in replay.chat)
                WriteChat(writer, message);
        }

        public static Recording Load(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
            using (var reader = OpenPayload(stream))
            {
                float storedDuration;
                var replay = ReadHeader(reader, out storedDuration);

                int frameCount = reader.ReadInt32();
                for (int i = 0; i < frameCount; i++)
                {
                    var frame = new Frame();
                    frame.t = reader.ReadSingle();
                    frame.ships = new ShipState[replay.players.Count];
                    for (int slot = 0; slot < replay.players.Count; slot++)
                        frame.ships[slot] = ReadShipState(reader);
                    frame.items = ReadItemSamples(reader);
                    frame.objective = ReadObjectiveState(reader);
                    replay.frames.Add(frame);
                }

                int eventCount = reader.ReadInt32();
                for (int i = 0; i < eventCount; i++)
                    replay.events.Add(ReadEvent(reader));

                int chatCount = reader.ReadInt32();
                for (int i = 0; i < chatCount; i++)
                    replay.chat.Add(ReadChat(reader));

                NormalizeTimeline(replay);
                return replay;
            }
        }

        private static BinaryReader OpenPayload(Stream stream)
        {
            int version = stream.ReadByte();
            if (version != VERSION)
                throw new IOException("unsupported .olr version " + version +
                                      " (expected " + VERSION + ")");
            return new BinaryReader(new ZlibStream(stream, CompressionMode.Decompress));
        }

        public static OlrInfo ReadInfo(string path)
        {
            var info = new OlrInfo
            {
                path = path,
                sceneName = "",
                durationSeconds = -1f,
                matchMode = -1,
                playerCount = 0,
                playerNames = new string[0],
                date = DateFromFileName(path),
                formatVersion = 0
            };
            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
                {
                    info.formatVersion = (byte)Math.Max(0, stream.ReadByte()); // ReadByte is -1 on an empty file
                    if (info.formatVersion != VERSION)
                        return info;
                    stream.Position = 0;

                    float duration;
                    var replay = ReadHeader(OpenPayload(stream), out duration);
                    info.sceneName = replay.sceneName;
                    info.matchMode = replay.matchMode;
                    info.durationSeconds = duration;
                    info.playerCount = replay.players.Count;
                    info.playerNames = new string[replay.players.Count];
                    for (int i = 0; i < replay.players.Count; i++)
                        info.playerNames[i] = replay.players[i].name;

                    if (replay.recordedUtcBinary != 0)
                        info.date = DateTime.FromBinary(replay.recordedUtcBinary).ToLocalTime();
                }
            }
            catch (Exception e)
            {
                Debug.LogErrorFormat("REPLAY: ReadInfo failed for {0}: {1}", path, e);
            }
            return info;
        }

        private static void WriteHeader(BinaryWriter writer, Recording replay)
        {
            writer.Write(replay.sceneName ?? "");
            writer.Write(replay.levelIdHash ?? "");
            writer.Write(replay.matchMode);
            writer.Write(replay.Duration);
            writer.Write(replay.maxHitpoints);
            writer.Write(replay.matchTimeLimit);
            writer.Write(replay.matchStartOffset);
            writer.Write(replay.recorderSlot);
            writer.Write(replay.recordedUtcBinary);
            writer.Write(replay.players.Count);
            foreach (var player in replay.players)
            {
                writer.Write(player.name ?? "");
                writer.Write(player.team);
                writer.Write(player.color.r);
                writer.Write(player.color.g);
                writer.Write(player.color.b);
            }
        }

        private static Recording ReadHeader(BinaryReader reader, out float storedDuration)
        {
            var replay = new Recording();
            replay.sceneName = reader.ReadString();
            replay.levelIdHash = reader.ReadString();
            replay.matchMode = reader.ReadInt32();
            storedDuration = reader.ReadSingle();
            replay.maxHitpoints = reader.ReadSingle();
            replay.matchTimeLimit = reader.ReadSingle();
            replay.matchStartOffset = reader.ReadSingle();
            replay.recorderSlot = reader.ReadInt32();
            replay.recordedUtcBinary = reader.ReadInt64();

            int playerCount = reader.ReadInt32();
            for (int i = 0; i < playerCount; i++)
            {
                replay.players.Add(new RosterEntry
                {
                    name = reader.ReadString(),
                    team = reader.ReadInt32(),
                    color = new Color(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), 1f)
                });
            }
            return replay;
        }

        private static void NormalizeTimeline(Recording replay)
        {
            float firstFrameTime = replay.frames.Count > 0 ? replay.frames[0].t : 0f;
            if (firstFrameTime == 0f)
                return;
            foreach (var frame in replay.frames) frame.t -= firstFrameTime;
            foreach (var ev in replay.events) ev.t = Mathf.Max(0f, ev.t - firstFrameTime);
            foreach (var message in replay.chat) message.t = Mathf.Max(0f, message.t - firstFrameTime);
        }

        private const int ROTATION_COMPONENT_BITS = 10;
        private const int ROTATION_COMPONENT_STEPS = (1 << ROTATION_COMPONENT_BITS) - 1;
        private const float MAX_SMALL_COMPONENT = 0.70710678f;

        private static uint PackRotation(Quaternion rotation)
        {
            float x = rotation.x, y = rotation.y, z = rotation.z, w = rotation.w;
            int largestIndex = 0;
            float largestMagnitude = Mathf.Abs(x);
            if (Mathf.Abs(y) > largestMagnitude) { largestIndex = 1; largestMagnitude = Mathf.Abs(y); }
            if (Mathf.Abs(z) > largestMagnitude) { largestIndex = 2; largestMagnitude = Mathf.Abs(z); }
            if (Mathf.Abs(w) > largestMagnitude) { largestIndex = 3; largestMagnitude = Mathf.Abs(w); }

            float sign = 1f;
            if ((largestIndex == 0 && x < 0f) || (largestIndex == 1 && y < 0f) ||
                (largestIndex == 2 && z < 0f) || (largestIndex == 3 && w < 0f))
                sign = -1f;
            x *= sign; y *= sign; z *= sign; w *= sign;

            float first, second, third;
            switch (largestIndex)
            {
                case 0: first = y; second = z; third = w; break;
                case 1: first = x; second = z; third = w; break;
                case 2: first = x; second = y; third = w; break;
                default: first = x; second = y; third = z; break;
            }
            return (uint)largestIndex |
                   ((uint)QuantizeComponent(first) << 2) |
                   ((uint)QuantizeComponent(second) << (2 + ROTATION_COMPONENT_BITS)) |
                   ((uint)QuantizeComponent(third) << (2 + ROTATION_COMPONENT_BITS * 2));
        }

        private static Quaternion UnpackRotation(uint packed)
        {
            int largestIndex = (int)(packed & 3);
            float first = DequantizeComponent((int)((packed >> 2) & ROTATION_COMPONENT_STEPS));
            float second = DequantizeComponent((int)((packed >> (2 + ROTATION_COMPONENT_BITS)) & ROTATION_COMPONENT_STEPS));
            float third = DequantizeComponent((int)((packed >> (2 + ROTATION_COMPONENT_BITS * 2)) & ROTATION_COMPONENT_STEPS));
            float dropped = Mathf.Sqrt(Mathf.Max(0f, 1f - first * first - second * second - third * third));
            switch (largestIndex)
            {
                case 0: return new Quaternion(dropped, first, second, third);
                case 1: return new Quaternion(first, dropped, second, third);
                case 2: return new Quaternion(first, second, dropped, third);
                default: return new Quaternion(first, second, third, dropped);
            }
        }

        private static int QuantizeComponent(float component)
        {
            float normalized = component / MAX_SMALL_COMPONENT * 0.5f + 0.5f;
            return Mathf.Clamp(Mathf.RoundToInt(normalized * ROTATION_COMPONENT_STEPS),
                               0, ROTATION_COMPONENT_STEPS);
        }

        private static float DequantizeComponent(int quantized)
        {
            return ((float)quantized / ROTATION_COMPONENT_STEPS * 2f - 1f) * MAX_SMALL_COMPONENT;
        }

        private const byte PRESENT_FLAG = 1;
        private const byte ALIVE_FLAG = 2;

        private static void WriteShipState(BinaryWriter writer, ref ShipState ship)
        {
            byte flags = (byte)((ship.present ? PRESENT_FLAG : 0) | (ship.alive ? ALIVE_FLAG : 0));
            writer.Write(flags);
            if (!ship.present)
                return;
            writer.Write(ship.pos.x); writer.Write(ship.pos.y); writer.Write(ship.pos.z);
            writer.Write(PackRotation(ship.rot));
            writer.Write(ship.weapon);
            writer.Write(ship.missile);
            writer.Write(ship.health);
            WriteHalfVector(writer, ship.vel);
            WriteHalfVector(writer, ship.vrot);
            writer.Write(ship.energy);
            writer.Write(ship.ammo);
            writer.Write(ship.weaponLevels);
            writer.Write(ship.missileLevels);
            writer.Write(ship.missileAmmo);
            writer.Write(ship.statusFlags);
        }

        private static ShipState ReadShipState(BinaryReader reader)
        {
            byte flags = reader.ReadByte();
            var ship = new ShipState();
            ship.present = (flags & PRESENT_FLAG) != 0;
            ship.alive = (flags & ALIVE_FLAG) != 0;
            if (!ship.present)
                return ship;
            ship.pos = ReadVector(reader);
            ship.rot = UnpackRotation(reader.ReadUInt32());
            ship.weapon = reader.ReadByte();
            ship.missile = reader.ReadByte();
            ship.health = reader.ReadByte();
            ship.vel = ReadHalfVector(reader);
            ship.vrot = ReadHalfVector(reader);
            ship.energy = reader.ReadByte();
            ship.ammo = reader.ReadUInt16();
            ship.weaponLevels = reader.ReadUInt32();
            ship.missileLevels = reader.ReadUInt32();
            ship.missileAmmo = reader.ReadUInt64();
            ship.statusFlags = reader.ReadByte();
            return ship;
        }

        private static void WriteItemSamples(BinaryWriter writer, ItemSample[] items)
        {
            int sampleCount = items != null ? items.Length : 0;
            writer.Write((ushort)sampleCount);
            for (int i = 0; i < sampleCount; i++)
            {
                writer.Write(items[i].id);
                WriteVector(writer, items[i].pos);
            }
        }

        private static ItemSample[] ReadItemSamples(BinaryReader reader)
        {
            int sampleCount = reader.ReadUInt16();
            if (sampleCount == 0)
                return null;
            var items = new ItemSample[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                items[i].id = reader.ReadUInt16();
                items[i].pos = ReadVector(reader);
            }
            return items;
        }

        private const byte BALL_PRESENT_FLAG = 1;

        private static void WriteObjectiveState(BinaryWriter writer, ref ObjectiveState objective)
        {
            writer.Write((byte)(objective.ballPresent ? BALL_PRESENT_FLAG : 0));
            if (!objective.ballPresent)
                return;
            WriteVector(writer, objective.ballPos);
            writer.Write(PackRotation(objective.ballRot));
            WriteHalfVector(writer, objective.ballVel);
        }

        private static ObjectiveState ReadObjectiveState(BinaryReader reader)
        {
            var objective = new ObjectiveState();
            objective.ballPresent = (reader.ReadByte() & BALL_PRESENT_FLAG) != 0;
            if (!objective.ballPresent)
                return objective;
            objective.ballPos = ReadVector(reader);
            objective.ballRot = UnpackRotation(reader.ReadUInt32());
            objective.ballVel = ReadHalfVector(reader);
            return objective;
        }

        private static void WriteEvent(BinaryWriter writer, MatchEvent ev)
        {
            writer.Write(ev.t);
            writer.Write((byte)ev.type);
            writer.Write(ev.iA);
            writer.Write(ev.iB);
            writer.Write(ev.iC);
            writer.Write(ev.fA);
            WriteVector(writer, ev.pos);
            writer.Write(ev.rot.x); writer.Write(ev.rot.y); writer.Write(ev.rot.z); writer.Write(ev.rot.w);
            writer.Write(ev.nameA ?? "");
            writer.Write(ev.nameB ?? "");
        }

        private static MatchEvent ReadEvent(BinaryReader reader)
        {
            var ev = new MatchEvent();
            ev.t = reader.ReadSingle();
            ev.type = (MatchEventType)reader.ReadByte();
            ev.iA = reader.ReadInt32();
            ev.iB = reader.ReadInt32();
            ev.iC = reader.ReadInt32();
            ev.fA = reader.ReadSingle();
            ev.pos = ReadVector(reader);
            ev.rot = new Quaternion(reader.ReadSingle(), reader.ReadSingle(),
                                    reader.ReadSingle(), reader.ReadSingle());
            ev.nameA = reader.ReadString();
            ev.nameB = reader.ReadString();
            return ev;
        }

        private static void WriteChat(BinaryWriter writer, ChatLine message)
        {
            writer.Write(message.t);
            writer.Write(message.sender);
            writer.Write(message.team);
            writer.Write(message.senderName ?? "");
            writer.Write(message.message ?? "");
        }

        private static ChatLine ReadChat(BinaryReader reader)
        {
            var message = new ChatLine();
            message.t = reader.ReadSingle();
            message.sender = reader.ReadInt32();
            message.team = reader.ReadInt32();
            message.senderName = reader.ReadString();
            message.message = reader.ReadString();
            return message;
        }

        private static void WriteVector(BinaryWriter writer, Vector3 v)
        {
            writer.Write(v.x); writer.Write(v.y); writer.Write(v.z);
        }

        private static Vector3 ReadVector(BinaryReader reader)
        {
            return new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        }

        private static void WriteHalfVector(BinaryWriter writer, Vector3 v)
        {
            writer.Write(HalfHelper.Compress(v.x));
            writer.Write(HalfHelper.Compress(v.y));
            writer.Write(HalfHelper.Compress(v.z));
        }

        private static Vector3 ReadHalfVector(BinaryReader reader)
        {
            return new Vector3(HalfHelper.Decompress(reader.ReadUInt16()),
                               HalfHelper.Decompress(reader.ReadUInt16()),
                               HalfHelper.Decompress(reader.ReadUInt16()));
        }

        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "level";
            foreach (var invalidChar in Path.GetInvalidFileNameChars())
                name = name.Replace(invalidChar, '_');
            return name;
        }

        public static List<string> ListPaths()
        {
            EnsureDirectory();
            var paths = new List<string>();
            try
            {
                foreach (var path in System.IO.Directory.GetFiles(Directory, "*.olr"))
                    paths.Add(path);
                AddGameDirectoryReplays(paths);
                paths.Sort();
                paths.Reverse();
            }
            catch (Exception e)
            {
                Debug.LogErrorFormat("REPLAY: list failed: {0}", e);
            }
            return paths;
        }

        public static List<OlrInfo> ListInfos()
        {
            var infos = new List<OlrInfo>();
            foreach (var path in ListPaths())
                infos.Add(ReadInfo(path));

            infos.Sort((a, b) => b.date.CompareTo(a.date));
            return infos;
        }

        private static void AddGameDirectoryReplays(List<string> paths)
        {
            string gameDirectory = GameDirectory;
            if (string.IsNullOrEmpty(gameDirectory))
                return;
            var alreadyListed = new HashSet<string>();
            foreach (var path in paths)
                alreadyListed.Add(Path.GetFileName(path).ToLowerInvariant());
            foreach (var directory in new[] { gameDirectory, Path.Combine(gameDirectory, "Replays") })
            {
                try
                {
                    if (!System.IO.Directory.Exists(directory))
                        continue;
                    foreach (var path in System.IO.Directory.GetFiles(directory, "*.olr"))
                        if (alreadyListed.Add(Path.GetFileName(path).ToLowerInvariant()))
                            paths.Add(path);
                }
                catch (Exception e)
                {
                    Debug.LogErrorFormat("REPLAY: game dir list failed: {0}", e);
                }
            }
        }

        private static DateTime DateFromFileName(string path)
        {
            string[] parts = Path.GetFileNameWithoutExtension(path).Split('_');
            DateTime recordedAt;
            if (parts.Length >= 3 && parts[0] == "Replay" &&
                DateTime.TryParseExact(parts[1] + parts[2], "yyyyMMddHHmmss",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out recordedAt))
                return recordedAt;
            try { return File.GetLastWriteTime(path); }
            catch (IOException) { return DateTime.MinValue; }
        }
    }
}
