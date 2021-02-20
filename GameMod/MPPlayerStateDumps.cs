using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.IO;
using System.Threading;
using Harmony;
using Overload;
using UnityEngine;
using UnityEngine.Networking;

namespace GameMod {
	public class MPPlayerStateDump {
		public enum Command : uint {
			NONE = 0,
			ENQUEUE,
			UPDATE_BEGIN,
			UPDATE_END,
			INTERPOLATE_BEGIN,
			INTERPOLATE_END,
			LERP_BEGIN,
			LERP_END,
			FINISH
		}	
		public class Buffer {
			private FileStream fs;
			private MemoryStream ms;
			private BinaryWriter bw;
			private Mutex mtx;
			private bool go;
			private int matchCount;
			private const long maxMemBuffer = 256 * 1024;

			public Buffer() {
				mtx = new Mutex();
				ms = new MemoryStream();
				bw = new BinaryWriter(ms);
				go = false;
				matchCount=0;
			}

			~Buffer() {
				Stop();
			}

			public void Start()
			{
				if (go) {
					Stop();
				}
				try {
        				string basePath = Path.Combine(Application.persistentDataPath, "playerstatedump");
					String name = basePath + matchCount + ".olmd";
					Debug.Log("MPPlayerStateDump: dump started to " + name);
					fs = File.Create(name);
					ms.Position = 0;
					bw.Write((uint)1); // file format version
					bw.Write((uint)0); // size of extra header, reserved for later versions
					matchCount++;
					go = true;
				} catch (Exception e) {
					Debug.Log("MPPlayerStateDump: failed to initialize buffer file:" + e);
				}
			}

			public void Stop()
			{
				if (!go) {
					return;
				}
				try {
					bw.Write((uint)Command.FINISH);
					Flush(true);
					fs.Close();
					Debug.Log("MPPlayerStateDump: dump finished");
				} catch (Exception e) {
					Debug.Log("MPPlayerStateDump: failed to stop: " + e);
				} finally {
					go = false;
				}
			}

			private void Flush(bool force)
			{
				if (force || ms.Position > maxMemBuffer) {
					Debug.Log("MPPlayerStateDump: dumping " + ms.Position + " bytes");
					ms.SetLength(ms.Position);
					ms.WriteTo(fs);
					fs.Flush();
					ms.Position=0;
				}
			}

			private void WritePlayerSnapshot(ref PlayerSnapshot s)
			{
				bw.Write(s.m_net_id.Value);
				bw.Write(s.m_pos.x);
				bw.Write(s.m_pos.y);
				bw.Write(s.m_pos.z);
				bw.Write(s.m_rot.x);
				bw.Write(s.m_rot.y);
				bw.Write(s.m_rot.z);
				bw.Write(s.m_rot.w);
			}

			public void AddCommand(uint cmd) {
				if (!go) {
					return;
				}
				mtx.WaitOne();
				try {
					bw.Write(cmd);
					Flush(false);
				} catch (Exception e) {
					Debug.Log("MPPlayerStateDump: failed to dump command: " + e);
				} finally {
					mtx.ReleaseMutex();
				}
			}

			public void AddCommand(uint cmd, float timestamp) {
				if (!go) {
					return;
				}
				mtx.WaitOne();
				try {
					bw.Write(cmd);
					bw.Write(timestamp);
					Flush(false);
				} catch (Exception e) {
					Debug.Log("MPPlayerStateDump: failed to dump command: " + e);
				} finally {
					mtx.ReleaseMutex();
				}
			}

			public void AddUpdateBegin(float timestamp, float interpolTime) {
				if (!go) {
					return;
				}
				mtx.WaitOne();
				try {
					bw.Write((uint)Command.UPDATE_BEGIN);
					bw.Write(timestamp);
					bw.Write(interpolTime);
					Flush(false);
				} catch (Exception e) {
					Debug.Log("MPPlayerStateDump: failed to dump update begin: " + e);
				} finally {
					mtx.ReleaseMutex();
				}
			}

			public void AddUpdateEnd(float interpolTime) {
				if (!go) {
					return;
				}
				mtx.WaitOne();
				try {
					bw.Write((uint)Command.UPDATE_END);
					bw.Write(interpolTime);
					Flush(false);
				} catch (Exception e) {
					Debug.Log("MPPlayerStateDump: failed to dump update end: " + e);
				} finally {
					mtx.ReleaseMutex();
				}
			}


			public void AddSnapshot(ref PlayerSnapshotToClientMessage msg)
			{
				if (!go) {
					return;
				}
				mtx.WaitOne();
				try {
					bw.Write((uint)Command.ENQUEUE);
					bw.Write(Time.time);
					bw.Write(msg.m_num_snapshots);
					for (int i = 0; i<msg.m_num_snapshots; i++) {
						WritePlayerSnapshot(ref msg.m_snapshots[i]);
					}
					Flush(false);
				} catch (Exception e) {
					Debug.Log("MPPlayerStateDump: failed to dump snapshot: " + e);
				} finally {
					mtx.ReleaseMutex();
				}
			}

			public void AddInterpolateBegin(float timestamp, int ping) {
				if (!go) {
					return;
				}
				mtx.WaitOne();
				try {
					bw.Write((uint)Command.INTERPOLATE_BEGIN);
					bw.Write(timestamp);
					bw.Write(ping);

					Flush(false);
				} catch (Exception e) {
					Debug.Log("MPPlayerStateDump: failed to dump command: " + e);
				} finally {
					mtx.ReleaseMutex();
				}
			}

			public void AddLerpBegin(bool wait_for_respawn, ref PlayerSnapshot A, ref PlayerSnapshot B, float t)
			{
				if (!go) {
					return;
				}
				mtx.WaitOne();
				try {
					bw.Write((uint)Command.LERP_BEGIN);
					int w = (wait_for_respawn)?1:0;
					bw.Write(w);
					// dumping the whole states again is redundant, but the amout of data is not that high... 
					WritePlayerSnapshot(ref A);
					WritePlayerSnapshot(ref B);
					bw.Write(t);
					Flush(false);
				} catch (Exception e) {
					Debug.Log("MPPlayerStateDump: failed to dump lerp begin: " + e);
				} finally {
					mtx.ReleaseMutex();
				}
			}

			public void AddLerpEnd(bool wait_for_respawn)
			{
				if (!go) {
					return;
				}
				mtx.WaitOne();
				try {
					bw.Write((uint)Command.LERP_END);
					int w = (wait_for_respawn)?1:0;
					bw.Write(w);
					Flush(false);
				} catch (Exception e) {
					Debug.Log("MPPlayerStateDump: failed to dump lerp begin: " + e);
				} finally {
					mtx.ReleaseMutex();
				}
			}


		}

		private static Buffer buf = new Buffer();

    
		/* these are not working as I had hoped...
		[HarmonyPatch(typeof(NetworkMatch), "InitBeforeEachMatch")]
		class MPPlayerStateDump_InitBeforeEachMatch {
			private static void Postfix() {
				buf.Start();
			}
		}

		[HarmonyPatch(typeof(NetworkMatch), "ExitMatch")]
		class MPPlayerStateDump_ExitMatch {
			private static void Prefix() {
				Debug.Log("EXIT!!!!!!!!!!!!!!!!");
				buf.Stop();
			}
		}
		*/

		[HarmonyPatch(typeof(Client), "Connect")]
		class MPPlayerStateDump_Connect {
			private static void Postfix() {
				buf.Start();
			}
		}
		[HarmonyPatch(typeof(Client), "Disconnect")]
		class MPPlayerStateDump_Disconnect {
			private static void Prefix() {
				buf.Stop();
			}
		}

		[HarmonyPatch(typeof(Client), "OnPlayerSnapshotToClient")]
		class MPPlayerStateDump_Enqueue {
			private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes) {
				foreach (var code in codes) {
					// After the enqueue, call our own method.
					if (code.opcode == OpCodes.Callvirt && ((MethodInfo)code.operand).Name == "Enqueue") {
						yield return code;
						yield return new CodeInstruction(OpCodes.Ldloc_0);
						yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPPlayerStateDump), "EnqueueBuffer"));
						Debug.Log("Patched OnPlayerSnapshotToClient for MPPlayerStateDump");
						continue;

					}
					yield return code;
				}
			}
		}

        	public static void EnqueueBuffer(PlayerSnapshotToClientMessage msg) {
			buf.AddSnapshot(ref msg);
		}


		[HarmonyPatch(typeof(Client), "UpdateInterpolationBuffer")]
		class MPPlayerStateDump_UpdateInterpolationBuffer {
		        static void Prefix() {
				buf.AddUpdateBegin(Time.time,Client.m_InterpolationStartTime);
			}
		        static void Postfix() {
				buf.AddUpdateEnd(Client.m_InterpolationStartTime);
			}
		}

		[HarmonyPatch(typeof(Client), "InterpolateRemotePlayers")]
		class MPPlayerStateDump_InterpolateRemotePlayers {
		        static void Prefix() {
            			int ping = GameManager.m_local_player.m_avg_ping_ms;
				buf.AddInterpolateBegin(Time.time, ping);
			}
		        static void Postfix() {
				buf.AddCommand((uint)Command.INTERPOLATE_END);
			}
        	}

		[HarmonyPatch(typeof(Player), "LerpRemotePlayer")]
		class MPPlayerStateDump_LerpRemotePlayer {
		        static void Prefix(Player __instance, ref PlayerSnapshot A, ref PlayerSnapshot B, float t) {
				buf.AddLerpBegin(__instance.m_lerp_wait_for_respawn_pos,ref A,ref B,t);
			}
		        static void Postfix(Player __instance) {
				buf.AddLerpEnd(__instance.m_lerp_wait_for_respawn_pos);
			}
        	}
	}
}
