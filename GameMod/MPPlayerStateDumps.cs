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
			UPDATE,
			INTERPOLATE
		}	
		public class Buffer {
			private FileStream fs;
			private MemoryStream ms;
			private BinaryWriter bw;
			private Mutex mtx;
			private bool go;
			private int matchCount;
			//private const long maxMemBuffer = 128 * 1024;  // 128kiB
			private const long maxMemBuffer = 1;  // 128kiB
			private const String path = "/tmp/playerstate_";

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
				go = true;
				String name = path + matchCount + ".olmd";
				Debug.Log("MPPlayerStateDump: dump started to " + name);
				fs = File.Create(name);
				matchCount++;
			}

			public void Stop()
			{
				if (!go) {
					return;
				}
				Flush(true);
				fs.Close();
				Debug.Log("MPPlayerStateDump: dump finished");
				go = false;
			}

			private void Flush(bool force)
			{
				if (force || ms.Position > maxMemBuffer) {
					Debug.Log("MPPlayerStateDump: dumping " + ms.Position + " bytes");
					ms.WriteTo(fs);
					ms.Position=0;
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
				} finally {
					mtx.ReleaseMutex();
				}
			}

		}

		private static Buffer buf = new Buffer();

    
		[HarmonyPatch(typeof(NetworkMatch), "InitBeforeEachMatch")]
		class MPPlayerStateDump_InitBeforeEachMatch {
			private static void Postfix() {
				buf.Start();
			}
		}

		[HarmonyPatch(typeof(NetworkMatch), "ExitMatch")]
		class MPPlayerStateDump_ExitMatch {
			private static void Prefix() {
				buf.Stop();
			}
		}

		/*
		[HarmonyPatch(typeof(Client), "OnPlayerSnapshotToClient")]
		class MPPlayerStateDump_UpdateInterpolationBuffer {
		        static void Prefix() {
				buf.AddCommand((uint)Command.UPDATE,Time.time);
			}
		        static void Postfix() {
				buf.AddCommand((uint)Command.UPDATE_END,Time.time);
			}
		}
		*/
		[HarmonyPatch(typeof(Client), "UpdateInterpolationBuffer")]
		class MPPlayerStateDump_UpdateInterpolationBuffer {
		        static void Prefix() {
				buf.AddCommand((uint)Command.UPDATE,Time.time);
			}
			/*
		        static void Postfix() {
				buf.AddCommand((uint)Command.UPDATE_END,Time.time);
			}
			*/
		}

		[HarmonyPatch(typeof(Client), "InterpolateRemotePlayers")]
		class MPPlayerStateDump_InterpolateRemotePlayers {
		        static void Prefix() {
				buf.AddCommand((uint)Command.INTERPOLATE,Time.time);
			}
        	}
	}
}
