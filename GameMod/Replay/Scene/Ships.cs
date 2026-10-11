using System.Collections.Generic;
using Overload;
using UnityEngine;

namespace GameMod.Replay
{
    public static class Ships
    {
        private static Recording recording;
        private static GameObject[] clones;
        private static ShipRenderState[] renderStates;

        private static ShipState[] currentStates;

        private class ShipRenderState
        {
            public MeshRenderer[] renderers;
            public bool? visible;
            public bool cloaked;

            public Material[][] hullMaterials;

            public GameObject[][] weaponMounts;
            public int shownWeapon = -1;
        }

        public static int Count { get { return clones != null ? clones.Length : 0; } }

        public static ShipState StateOf(int rosterIndex)
        {
            return (currentStates != null && rosterIndex >= 0 && rosterIndex < currentStates.Length)
                ? currentStates[rosterIndex] : default(ShipState);
        }

        public static Transform TransformOf(int rosterIndex)
        {
            return (clones != null && rosterIndex >= 0 && rosterIndex < clones.Length &&
                    clones[rosterIndex] != null) ? clones[rosterIndex].transform : null;
        }

        public static void Create(Recording replay, Transform shipMesh)
        {
            recording = replay;
            int rosterSize = replay.players.Count;
            clones = new GameObject[rosterSize];
            renderStates = new ShipRenderState[rosterSize];
            currentStates = new ShipState[rosterSize];
            wasAlive = new bool[rosterSize];
            scatterCooldown = new float[rosterSize];
            deathClock = new float[rosterSize];
            finalBlastDone = new bool[rosterSize];
            for (int i = 0; i < rosterSize; i++)
                deathClock[i] = NO_DEATH_RECORDED;

            CaptureWeaponMountPaths(shipMesh);
            int created = 0;
            for (int i = 0; i < rosterSize; i++)
            {
                clones[i] = CloneShipMesh(shipMesh.gameObject, replay.players[i].name);
                if (clones[i] == null)
                    continue;
                created++;
                renderStates[i] = new ShipRenderState
                {
                    renderers = clones[i].GetComponentsInChildren<MeshRenderer>(true),
                    weaponMounts = ResolveMounts(clones[i])
                };
            }
            Debug.LogFormat("REPLAY: {0}/{1} replay ships created", created, rosterSize);
        }

        public static void Destroy()
        {
            if (clones != null)
                foreach (var clone in clones)
                    if (clone != null)
                        UnityEngine.Object.Destroy(clone);
            clones = null;
            renderStates = null;
            currentStates = null;
            wasAlive = null;
            scatterCooldown = null;
            deathClock = null;
            finalBlastDone = null;
            weaponMountPaths = null;
            recording = null;
        }

        public static void HideAll()
        {
            if (clones == null)
                return;
            for (int i = 0; i < clones.Length; i++)
                SetVisible(i, visible: false);
        }

        private static GameObject CloneShipMesh(GameObject shipMesh, string playerName)
        {
            var clone = UnityEngine.Object.Instantiate(shipMesh);
            clone.name = "ReplayShip_" + playerName;
            clone.transform.parent = null;
            foreach (var body in clone.GetComponentsInChildren<Rigidbody>())
                body.isKinematic = true;
            foreach (var collider in clone.GetComponentsInChildren<Collider>())
                collider.enabled = false;

            foreach (var camera in clone.GetComponentsInChildren<Camera>(true))
                if (camera != null)
                    UnityEngine.Object.Destroy(camera.gameObject);
            foreach (var renderLayer in clone.GetComponentsInChildren<UnityRenderLayer>(true))
                if (renderLayer != null)
                    UnityEngine.Object.Destroy(renderLayer.gameObject);
            foreach (var listener in clone.GetComponentsInChildren<AudioListener>(true))
                if (listener != null)
                    UnityEngine.Object.Destroy(listener);

            foreach (var source in clone.GetComponentsInChildren<AudioSource>(true))
            {
                if (source == null)
                    continue;
                source.Stop();
                if (source.gameObject != clone)
                    UnityEngine.Object.Destroy(source.gameObject);
                else
                    UnityEngine.Object.Destroy(source);
            }
            clone.SetActive(true);
            return clone;
        }

        private const int WEAPON_MOUNT_SLOTS = 8;
        private static string[][] weaponMountPaths;

        private static void CaptureWeaponMountPaths(Transform shipMesh)
        {
            weaponMountPaths = null;
            var ship = GameManager.m_player_ship;
            if (ship == null || shipMesh == null)
                return;
            var paths = new string[WEAPON_MOUNT_SLOTS][];
            int found = 0;
            for (int slot = 0; slot < WEAPON_MOUNT_SLOTS; slot++)
            {
                var slotPaths = new List<string>();
                AddMountPath(slotPaths, ship.m_weapon_mounts1, slot, shipMesh);
                if (slot != 1 && slot != 4) // skip lower mounted weapons: cyclone (1) and driller (4)
                    AddMountPath(slotPaths, ship.m_weapon_mounts2, slot, shipMesh);
                paths[slot] = slotPaths.ToArray();
                found += slotPaths.Count;
            }
            if (found == 0)
            {
                Debug.LogWarning("REPLAY: no weapon mounts found on the ship mesh; replayed ships will fly without weapon models");
                return;
            }
            weaponMountPaths = paths;
        }

        private static void AddMountPath(List<string> slotPaths, GameObject[] mounts, int slot, Transform root)
        {
            if (mounts == null || slot >= mounts.Length || mounts[slot] == null)
                return;
            string path = RelativePath(mounts[slot].transform, root);
            if (path != null)
                slotPaths.Add(path);
        }

        private static string RelativePath(Transform node, Transform root)
        {
            string path = null;
            for (var step = node; step != null; step = step.parent)
            {
                if (step == root)
                    return path;
                path = (path == null) ? step.name : step.name + "/" + path;
            }
            return null;
        }

        private static GameObject[][] ResolveMounts(GameObject clone)
        {
            if (weaponMountPaths == null)
                return null;
            var mounts = new GameObject[WEAPON_MOUNT_SLOTS][];
            for (int slot = 0; slot < WEAPON_MOUNT_SLOTS; slot++)
            {
                var slotMounts = new List<GameObject>();
                foreach (var path in weaponMountPaths[slot])
                {
                    var mount = clone.transform.Find(path);
                    if (mount != null)
                        slotMounts.Add(mount.gameObject);
                }
                mounts[slot] = slotMounts.ToArray();
            }
            return mounts;
        }

        private static void SetVisible(int rosterIndex, bool visible)
        {
            var renderState = renderStates != null ? renderStates[rosterIndex] : null;
            if (renderState == null || renderState.renderers == null || renderState.visible == visible)
                return;
            renderState.visible = visible;
            foreach (var renderer in renderState.renderers)
            {
                if (renderer == null)
                    continue;
                renderer.enabled = visible;

                if (visible)
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
        }

        private static void SetCloaked(int rosterIndex, bool cloaked)
        {
            var renderState = renderStates != null ? renderStates[rosterIndex] : null;
            if (renderState == null || renderState.renderers == null || renderState.cloaked == cloaked)
                return;
            Material cloakMaterial = UIManager.gm != null ? UIManager.gm.m_cloak_material : null;
            if (cloaked && cloakMaterial == null)
                return;
            if (!cloaked && renderState.hullMaterials == null)
                return;

            if (cloaked && renderState.hullMaterials == null)
            {
                renderState.hullMaterials = new Material[renderState.renderers.Length][];
                for (int i = 0; i < renderState.renderers.Length; i++)
                    renderState.hullMaterials[i] = renderState.renderers[i] != null
                        ? renderState.renderers[i].materials : null;
            }

            renderState.cloaked = cloaked;
            for (int i = 0; i < renderState.renderers.Length; i++)
            {
                var renderer = renderState.renderers[i];
                if (renderer == null)
                    continue;
                if (!cloaked)
                {
                    if (renderState.hullMaterials[i] != null)
                        renderer.materials = renderState.hullMaterials[i];
                    continue;
                }
                var cloakSlots = new Material[renderer.materials.Length];
                for (int slot = 0; slot < cloakSlots.Length; slot++)
                    cloakSlots[slot] = cloakMaterial;
                renderer.materials = cloakSlots;
            }
        }

        public static void Drive()
        {
            int before = Playback.FrameBefore, after = Playback.FrameAfter;
            if (before < 0 || clones == null)
                return;
            float blend = Playback.FrameBlend;
            float intervalSeconds = recording.frames[after].t - recording.frames[before].t;

            for (int rosterIndex = 0; rosterIndex < recording.players.Count; rosterIndex++)
            {
                var stateBefore = recording.frames[before].ships[rosterIndex];
                var stateAfter = recording.frames[after].ships[rosterIndex];
                bool present = stateBefore.present || stateAfter.present;
                bool alive = present && (stateAfter.present ? stateAfter.alive : stateBefore.alive);

                Vector3 pos;
                Quaternion rot;
                if (!stateBefore.present || !stateAfter.present)
                {
                    var onlySample = stateAfter.present ? stateAfter : stateBefore;
                    pos = onlySample.pos;
                    rot = onlySample.rot;
                }
                else
                {
                    rot = Quaternion.SlerpUnclamped(stateBefore.rot, stateAfter.rot, blend);
                    pos = Motion.Interpolate(stateBefore.pos, stateBefore.vel,
                                             stateAfter.pos, stateAfter.vel, intervalSeconds, blend);
                }

                var current = stateBefore.present ? stateBefore : stateAfter;
                current.present = present;
                current.alive = alive;
                current.pos = pos;
                current.rot = rot;
                current.health = stateAfter.present ? stateAfter.health : stateBefore.health;
                currentStates[rosterIndex] = current;

                AdvanceDeathRoll(rosterIndex, present, alive, pos);
                ApplyAppearance(rosterIndex, present, alive, pos, rot, current);
                wasAlive[rosterIndex] = alive;
            }
        }

        private static void ApplyAppearance(int rosterIndex, bool present, bool alive,
                                            Vector3 pos, Quaternion rot, ShipState state)
        {
            bool insideDeathRoll = present && !alive && IsRollingAfterDeath(rosterIndex);
            bool watchedFromInside = rosterIndex == Viewpoint.Watching &&
                                     Viewpoint.Mode == CameraMode.FirstPerson;
            bool showMesh = present && !watchedFromInside && (alive || insideDeathRoll);
            SetVisible(rosterIndex, showMesh);
            SetCloaked(rosterIndex, showMesh && alive && state.IsCloaked);

            var renderState = present ? renderStates[rosterIndex] : null;
            if (renderState != null && renderState.weaponMounts != null &&
                renderState.shownWeapon != state.weapon)
            {
                for (int slot = 0; slot < renderState.weaponMounts.Length; slot++)
                {
                    bool show = slot == state.weapon;
                    foreach (var mount in renderState.weaponMounts[slot])
                        if (mount != null)
                            mount.SetActive(show);
                }
                renderState.shownWeapon = state.weapon;
            }

            if (present && clones[rosterIndex] != null)
            {
                clones[rosterIndex].transform.localPosition = pos;
                clones[rosterIndex].transform.rotation = rot;
            }
        }

        private static bool[] wasAlive;
        private static float[] scatterCooldown;
        private static float[] deathClock;
        private static bool[] finalBlastDone;

        private const float DEATH_ROLL_SECONDS = 3.5f;

        private const float NO_DEATH_RECORDED = -1f;

        private static readonly int[] DEATH_ROLL_CUES = { 256, 255, 254 };

        private static bool IsRollingAfterDeath(int rosterIndex)
        {
            return deathClock[rosterIndex] >= 0f &&
                   (Playback.Clock - deathClock[rosterIndex]) <= DEATH_ROLL_SECONDS;
        }

        private static void AdvanceDeathRoll(int rosterIndex, bool present, bool alive, Vector3 pos)
        {
            bool playFx = !Playback.Paused && !Playback.JustSeeked;

            if (present && !alive && wasAlive[rosterIndex])
            {
                deathClock[rosterIndex] = Playback.Clock;
                scatterCooldown[rosterIndex] = 0f;
                finalBlastDone[rosterIndex] = false;
                if (playFx)
                {
                    if (clones[rosterIndex] != null)
                        ExplosionManager.CreateRandomExplosionSimple(clones[rosterIndex].transform, 1.2f, 0.9f);
                    else
                        ExplosionManager.CreateRandomExplosionCore(pos);
                }
            }

            bool dead = present && !alive;
            bool rolling = dead && IsRollingAfterDeath(rosterIndex);

            if (dead && !finalBlastDone[rosterIndex] && !rolling && deathClock[rosterIndex] >= 0f)
            {
                finalBlastDone[rosterIndex] = true;
                if (playFx)
                {
                    if (GameManager.m_audio != null)
                    {
                        GameManager.m_audio.PlayCuePos(256, pos, 1f, Random.Range(-0.2f, -0.1f));
                        GameManager.m_audio.PlayCuePos(255, pos, 1f, Random.Range(-0.3f, -0.2f));
                    }
                    ExplosionManager.CreateRandomExplosionCore(pos);
                }
            }

            if (!playFx || !rolling)
                return;
            scatterCooldown[rosterIndex] -= Time.unscaledDeltaTime;
            if (scatterCooldown[rosterIndex] > 0f)
                return;
            scatterCooldown[rosterIndex] = Random.Range(0.1f, 0.16f);
            PlayScatterExplosion(pos);
        }

        private static void PlayScatterExplosion(Vector3 pos)
        {
            Vector3 scatterAt = pos + Random.onUnitSphere * Random.Range(0.6f, 1.1f);
            if (Random.value < 0.5f)
                ExplosionManager.CreateSimpleExplosion(scatterAt);
            else
                ExplosionManager.CreateMinorExplosion(scatterAt);

            if (GameManager.m_audio == null)
                return;
            int cueIndex = Random.Range(0, DEATH_ROLL_CUES.Length);
            bool quietest = cueIndex == 2; // cue 254, the quietest of the three
            GameManager.m_audio.PlayCuePos(DEATH_ROLL_CUES[cueIndex], pos,
                Random.Range(0.3f, quietest ? 0.5f : 0.6f),
                Random.Range(0f, quietest ? 0.2f : 0.3f));
        }

        public static void ClearDeathRoll(int rosterIndex)
        {
            if (deathClock != null && rosterIndex >= 0 && rosterIndex < deathClock.Length)
                deathClock[rosterIndex] = NO_DEATH_RECORDED;
        }

        public static void ResetDeathRollAt(float time)
        {
            if (wasAlive == null || recording == null)
                return;
            int frameIndex = Playback.FrameIndexAt(time);
            for (int rosterIndex = 0; rosterIndex < recording.players.Count; rosterIndex++)
            {
                var state = recording.frames[frameIndex].ships[rosterIndex];
                wasAlive[rosterIndex] = state.present && state.alive;
                scatterCooldown[rosterIndex] = 999f; // park the scatter FX; a roll found below restarts it
                deathClock[rosterIndex] = NO_DEATH_RECORDED;

                finalBlastDone[rosterIndex] = true;
                if (!state.present || state.alive)
                    continue;
                float edgeTime = FindDeathEdgeTime(rosterIndex, frameIndex, time);
                if (edgeTime >= 0f && (time - edgeTime) <= DEATH_ROLL_SECONDS)
                {
                    deathClock[rosterIndex] = edgeTime;
                    finalBlastDone[rosterIndex] = false;
                }
            }
        }

        private static float FindDeathEdgeTime(int rosterIndex, int frameIndex, float time)
        {
            int edgeIndex = frameIndex;
            while (edgeIndex > 0)
            {
                if (time - recording.frames[edgeIndex].t > DEATH_ROLL_SECONDS)
                    return -1f;
                var previous = recording.frames[edgeIndex - 1].ships[rosterIndex];
                if (!(previous.present && !previous.alive))
                    break;
                edgeIndex--;
            }
            var edge = recording.frames[edgeIndex].ships[rosterIndex];
            return (edge.present && !edge.alive) ? recording.frames[edgeIndex].t : -1f;
        }
    }

    public static class Motion
    {
        private const float MIN_MOVING_SQR_SPEED = 0.01f;

        public static Vector3 Interpolate(Vector3 fromPos, Vector3 fromVel,
                                          Vector3 toPos, Vector3 toVel,
                                          float intervalSeconds, float blend)
        {
            bool moving = fromVel.sqrMagnitude > MIN_MOVING_SQR_SPEED ||
                          toVel.sqrMagnitude > MIN_MOVING_SQR_SPEED;
            if (intervalSeconds <= 1e-4f || !moving)
                return Vector3.LerpUnclamped(fromPos, toPos, blend);

            Vector3 m0 = fromVel * intervalSeconds;
            Vector3 m1 = toVel * intervalSeconds;
            float f2 = blend * blend;
            float f3 = f2 * blend;
            float h00 = 2f * f3 - 3f * f2 + 1f;
            float h10 = f3 - 2f * f2 + blend;
            float h01 = -2f * f3 + 3f * f2;
            float h11 = f3 - f2;
            return h00 * fromPos + h10 * m0 + h01 * toPos + h11 * m1;
        }
    }
}
