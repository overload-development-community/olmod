using System.Collections.Generic;
using Overload;
using UnityEngine;

namespace GameMod.Replay
{
    public static class World
    {
        private static Recording recording;

        private class ItemLifetime
        {
            public int id;
            public ItemType type;
            public bool super;
            public int index;
            public Vector3 spawnPos;
            public float spawnTime;
            public float pickupTime = float.MaxValue;
        }

        private static readonly List<ItemLifetime> lifetimes = new List<ItemLifetime>();
        private static readonly Dictionary<int, ItemLifetime> lifetimeById = new Dictionary<int, ItemLifetime>();
        private static readonly Dictionary<int, Item> liveItems = new Dictionary<int, Item>();
        private static readonly HashSet<Item> ownedItems = new HashSet<Item>();
        private static readonly List<int> expiredItemIds = new List<int>();

        private static GameObject ball;
        private static bool ballShown;

        private static GameObject[] carriedFlags;
        private static int[] carriedFlagCarriers;

        public static void Begin(Recording replay)
        {
            recording = replay;
            BuildItemTimeline();
            if (Item.m_ItemList != null)
                foreach (var item in Item.m_ItemList.ToArray())
                {
                    if (item == null || ownedItems.Contains(item))
                        continue;

                    if (Recorder.IsItemPrefabTemplate(item))
                        continue;
                    UnityEngine.Object.Destroy(item.gameObject);
                }
            SpawnBall();
            carriedFlags = new GameObject[CTF.TeamCount];
            carriedFlagCarriers = new int[CTF.TeamCount];
            for (int flag = 0; flag < carriedFlagCarriers.Length; flag++)
                carriedFlagCarriers[flag] = -1;
        }

        public static void End()
        {
            RemoveObjectiveProps();
            carriedFlags = null;
            carriedFlagCarriers = null;
            if (ball != null)
            {
                UnityEngine.Object.Destroy(ball);
                ball = null;
            }
            ballShown = false;
            ClearLiveItems();
            lifetimes.Clear();
            lifetimeById.Clear();
            recording = null;
        }

        public static void HideAll()
        {
            ClearLiveItems();
            RemoveObjectiveProps();
        }

        private static void RemoveObjectiveProps()
        {
            if (carriedFlags != null)
                for (int flag = 0; flag < carriedFlags.Length; flag++)
                    DestroyCarriedFlag(flag);
            ShowBall(shown: false);
        }

        public static void Drive()
        {
            DriveItems();
            DriveBall();
            DriveCarriedFlags();
        }

        private static void BuildItemTimeline()
        {
            lifetimes.Clear();
            lifetimeById.Clear();
            liveItems.Clear();
            ownedItems.Clear();
            foreach (var ev in recording.events)
            {
                if (ev.type == MatchEventType.ItemSpawn)
                {
                    if (lifetimeById.ContainsKey(ev.iA))
                        continue;
                    var lifetime = new ItemLifetime
                    {
                        id = ev.iA,
                        type = (ItemType)ev.iB,
                        super = ev.iC != 0,
                        index = (int)ev.fA,
                        spawnPos = ev.pos,
                        spawnTime = ev.t
                    };
                    lifetimes.Add(lifetime);
                    lifetimeById[ev.iA] = lifetime;
                }
                else if (ev.type == MatchEventType.ItemPickup)
                {
                    ItemLifetime lifetime;
                    if (lifetimeById.TryGetValue(ev.iA, out lifetime))
                        lifetime.pickupTime = ev.t;
                }
            }
        }

        public static void SpawnItem(int itemId)
        {
            ItemLifetime lifetime;
            if (lifetimeById.TryGetValue(itemId, out lifetime))
                Spawn(lifetime);
        }

        private static void Spawn(ItemLifetime lifetime)
        {
            if (liveItems.ContainsKey(lifetime.id))
                return;
            GameObject prefab;
            if (lifetime.type == ItemType.KEY_SECURITY &&
                lifetime.index >= 0 && lifetime.index < CTF.FlagObjs.Count)
            {
                prefab = CTF.FlagObjs[lifetime.index];
            }
            else
            {
                int prefabIndex = (int)Race.GetPrefabFromType(lifetime.type);
                prefab = PrefabManager.item_prefabs != null &&
                         prefabIndex >= 0 && prefabIndex < PrefabManager.item_prefabs.Length
                    ? PrefabManager.item_prefabs[prefabIndex] : null;
            }
            if (prefab == null)
                return;
            var spawned = UnityEngine.Object.Instantiate(prefab, lifetime.spawnPos, Quaternion.identity);
            var item = spawned != null ? spawned.GetComponent<Item>() : null;
            if (item == null)
            {
                if (spawned != null)
                    UnityEngine.Object.Destroy(spawned);
                return;
            }
            item.m_super = lifetime.super;
            MakeDisplayOnly(spawned);
            liveItems[lifetime.id] = item;
            ownedItems.Add(item);
        }

        private static void MakeDisplayOnly(GameObject prop)
        {
            prop.SetActive(true);
            foreach (var collider in prop.GetComponentsInChildren<Collider>(true))
                if (collider != null)
                    collider.enabled = false;
            var body = prop.GetComponent<Rigidbody>();
            if (body != null)
                body.isKinematic = true;
        }

        public static void DestroyItem(int itemId)
        {
            Item item;
            if (!liveItems.TryGetValue(itemId, out item))
                return;
            liveItems.Remove(itemId);
            if (item != null)
            {
                ownedItems.Remove(item);
                UnityEngine.Object.Destroy(item.gameObject);
            }
        }

        private static void ClearLiveItems()
        {
            foreach (var entry in liveItems)
                if (entry.Value != null)
                    UnityEngine.Object.Destroy(entry.Value.gameObject);
            liveItems.Clear();
            ownedItems.Clear();
        }

        public static void RebuildAt(float time)
        {
            expiredItemIds.Clear();
            foreach (var entry in liveItems)
            {
                ItemLifetime lifetime;
                bool stillAlive = entry.Value != null &&
                                  lifetimeById.TryGetValue(entry.Key, out lifetime) &&
                                  time >= lifetime.spawnTime && time < lifetime.pickupTime;
                if (!stillAlive)
                    expiredItemIds.Add(entry.Key);
            }
            for (int i = 0; i < expiredItemIds.Count; i++)
                DestroyItem(expiredItemIds[i]);
            foreach (var lifetime in lifetimes)
            {
                if (lifetime.spawnTime > time)
                    break;
                if (time < lifetime.pickupTime && !liveItems.ContainsKey(lifetime.id))
                    Spawn(lifetime);
            }
        }

        private static void DriveItems()
        {
            int before = Playback.FrameBefore, after = Playback.FrameAfter;
            if (liveItems.Count == 0 || before < 0)
                return;
            var samplesBefore = recording.frames[before].items;
            var samplesAfter = recording.frames[after].items;
            if (samplesBefore == null && samplesAfter == null)
                return;
            float blend = Playback.FrameBlend;
            foreach (var entry in liveItems)
            {
                var item = entry.Value;
                if (item == null || item.c_transform == null)
                    continue;
                Vector3 posBefore, posAfter;
                bool hasBefore = TryGetSample(samplesBefore, entry.Key, out posBefore);
                bool hasAfter = TryGetSample(samplesAfter, entry.Key, out posAfter);
                if (hasBefore && hasAfter)
                    item.c_transform.position = Vector3.LerpUnclamped(posBefore, posAfter, blend);
                else if (hasBefore)
                    item.c_transform.position = posBefore;
                else if (hasAfter)
                    item.c_transform.position = posAfter;
            }
        }

        private static bool TryGetSample(ItemSample[] samples, int itemId, out Vector3 pos)
        {
            if (samples != null)
                for (int i = 0; i < samples.Length; i++)
                    if (samples[i].id == itemId)
                    {
                        pos = samples[i].pos;
                        return true;
                    }
            pos = Vector3.zero;
            return false;
        }

        public static void PlayPickupFx(MatchEvent pickup)
        {
            int pickerIndex = pickup.iB;
            ItemLifetime lifetime;
            if (pickerIndex < 0 || !lifetimeById.TryGetValue(pickup.iA, out lifetime))
                return;
            int cue = (int)Item.GetPickupSoundEffect(lifetime.type, lifetime.super);
            if (pickerIndex == Viewpoint.Watching)
            {
                GameManager.m_audio.PlayCue2D(cue, 1f, 0f, 0f, true);
                if (GameManager.m_player_ship != null)
                    GameManager.m_player_ship.m_pickup_flash += 0.65f;
                return;
            }
            Vector3 pickedUpAt = pickup.pos != Vector3.zero ? pickup.pos : lifetime.spawnPos;
            GameManager.m_audio.PlayCuePos(cue, pickedUpAt);
        }

        private static void SpawnBall()
        {
            if (recording.matchMode != (int)MatchMode.MONSTERBALL)
                return;
            var prefab = GameManager.m_gm != null ? GameManager.m_gm.m_monsterball : null;
            if (prefab == null)
            {
                Debug.LogError("REPLAY: no monsterball prefab to clone");
                return;
            }
            ball = UnityEngine.Object.Instantiate(prefab, Vector3.zero, Quaternion.identity);
            if (ball == null)
                return;
            MakeDisplayOnly(ball);

            foreach (var networked in ball.GetComponentsInChildren<UnityEngine.Networking.NetworkBehaviour>(true))
                if (networked != null)
                    networked.enabled = false;
            ball.SetActive(false);
            ballShown = false;
        }

        private static void DriveBall()
        {
            int before = Playback.FrameBefore, after = Playback.FrameAfter;
            if (ball == null || before < 0)
                return;
            var sampleBefore = recording.frames[before].objective;
            var sampleAfter = recording.frames[after].objective;
            bool present = sampleBefore.ballPresent || sampleAfter.ballPresent;
            ShowBall(present);
            if (!present)
                return;

            if (!sampleBefore.ballPresent || !sampleAfter.ballPresent)
            {
                var onlySample = sampleAfter.ballPresent ? sampleAfter : sampleBefore;
                ball.transform.position = onlySample.ballPos;
                ball.transform.rotation = onlySample.ballRot;
                return;
            }
            float blend = Playback.FrameBlend;
            float intervalSeconds = recording.frames[after].t - recording.frames[before].t;
            ball.transform.position = Motion.Interpolate(
                sampleBefore.ballPos, sampleBefore.ballVel,
                sampleAfter.ballPos, sampleAfter.ballVel, intervalSeconds, blend);
            ball.transform.rotation = Quaternion.SlerpUnclamped(sampleBefore.ballRot,
                sampleAfter.ballRot, blend);
        }

        private static void ShowBall(bool shown)
        {
            if (ball == null || ballShown == shown)
                return;
            ballShown = shown;
            ball.SetActive(shown);
        }

        public static void PlayGoalFx(Vector3 pos, Quaternion rot)
        {
            ParticleManager.psm[3].StartParticleInstant(49, pos, rot); // sub-manager 3, particle 49: the goal burst
            SFXCueManager.PlayRawSoundEffect2D(SoundEffect.hud_notify_message1, 1f, 0f, 0f, true);
        }

        private static void DriveCarriedFlags()
        {
            if (carriedFlags == null || !recording.HasFlagState)
                return;
            for (int flag = 0; flag < carriedFlags.Length; flag++)
            {
                int carrier = recording.FlagStateAt(Playback.Clock, flag).carrierIndex;
                var carrierShip = Ships.TransformOf(carrier);
                if (carrierShip == null)
                {
                    DestroyCarriedFlag(flag);
                    continue;
                }
                if (carriedFlags[flag] == null || carriedFlagCarriers[flag] != carrier)
                {
                    DestroyCarriedFlag(flag);
                    carriedFlags[flag] = SpawnCarriedFlag(flag, carrierShip);
                    carriedFlagCarriers[flag] = carrier;
                }

                var carrierState = Ships.StateOf(carrier);
                bool shown = carrierState.present && carrierState.alive &&
                             !(carrier == Viewpoint.Watching &&
                               Viewpoint.Mode == CameraMode.FirstPerson);
                if (carriedFlags[flag] != null && carriedFlags[flag].activeSelf != shown)
                    carriedFlags[flag].SetActive(shown);
            }
        }

        private static GameObject SpawnCarriedFlag(int flag, Transform carrierShip)
        {
            if (flag < 0 || flag >= CTF.FlagObjs.Count || CTF.FlagObjs[flag] == null)
                return null;
            var spawned = UnityEngine.Object.Instantiate(CTF.FlagObjs[flag], carrierShip);
            if (spawned == null)
                return null;
            MakeDisplayOnly(spawned);
            spawned.transform.localPosition = new Vector3(0f, 1.1f, -0.9f); // above and behind the carrier
            spawned.transform.localRotation = Quaternion.identity;
            return spawned;
        }

        private static void DestroyCarriedFlag(int flag)
        {
            if (carriedFlags == null || carriedFlags[flag] == null)
                return;
            UnityEngine.Object.Destroy(carriedFlags[flag]);
            carriedFlags[flag] = null;
            carriedFlagCarriers[flag] = -1;
        }
    }
}
