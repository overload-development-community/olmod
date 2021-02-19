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
		public class MPPlayerStateDumpBuffer {
			public enum Command : uint {
				NONE = 0,
				ENQUEUE,
				UPDATE,
				INTERPOLATE
			}	
			public class BufHeader {
				public Command cmd;
				public float timestamp;

				public BufHeader(Command c, float ts) {
					cmd = c;
					timestamp = ts;
				}
			}

			private FileStream fs;
			private MemoryStream ms;
			private BinaryWriter bw;
			private Mutex mtx;
			//private const long maxMemBuffer = 128 * 1024;  // 128kiB
			private const long maxMemBuffer = 1;  // 128kiB
			private const String path = "/tmp/playerstate.dump";

			public MPPlayerStateDumpBuffer() {
				mtx = new Mutex();
				ms = new MemoryStream();
				bw = new BinaryWriter(ms);

			}

			private void Flush()
			{
				if (ms.Position > maxMemBuffer) {
					if (fs == null) {
						fs = File.Create(path);
					}
					ms.WriteTo(fs);
					ms.Position=0;
				}
			}

			public void AddCommand(uint cmd, float timestamp) {
				mtx.WaitOne();
				bw.Write(cmd);
				bw.Write(timestamp);
				Flush();
				mtx.ReleaseMutex();
			}
		}

		private static MPPlayerStateDumpBuffer buf = new MPPlayerStateDumpBuffer();

    
		[HarmonyPatch(typeof(Client), "InterpolateRemotePlayers")]
		class MPPlayerStateDump_InterpolateRemotePlayers {
		        static void Prefix() {
				buf.AddCommand(1,Time.time);
			}
        	}
	}
}
