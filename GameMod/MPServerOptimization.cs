using HarmonyLib;
using Overload;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using UnityEngine;
using UnityEngine.Networking;

namespace GameMod
{
	/*
	//TEMPORARY
	[HarmonyPatch(typeof(GameManager), "Start")]
	public static class GraphManager_GameManager_Start
	{
		public static void Postfix()
		{
			uConsole.RegisterCommand("rollfix", "Enables or disables fixed rolling while mouse is moving", new uConsole.DebugCommand(CmdRollFix));
			uConsole.RegisterCommand("odturning", "Enables or disables overdrive-boosted turn speed", new uConsole.DebugCommand(CmdODTurning));
			uConsole.RegisterCommand("buffer", "Sets the server input buffer length to the given int (between 1 and 30)", new uConsole.DebugCommand(CmdBUFFERLENGTH));
		}

		public static void CmdRollFix()
        {
			MPServerOptimization.RollFix = uConsole.GetBool();
			Debug.Log("-------------------------------");
			Debug.Log("CCF fixed roll speed is enabled: " + MPServerOptimization.RollFix);
			Debug.Log("-------------------------------");
		}

		public static void CmdODTurning()
        {
			MPServerOptimization.ODTurning = uConsole.GetBool();
			Debug.Log("-------------------------------");
			Debug.Log("CCF ODTurning boost is enabled: " + MPServerOptimization.ODTurning);
			Debug.Log("-------------------------------");
		}

		public static void CmdBUFFERLENGTH()
        {
			if (Client.IsConnected())
			{
				MPServerOptimization.InputBufferLength = Mathf.Clamp(uConsole.GetInt(), 1, 30);
				var msg = new UnityEngine.Networking.NetworkSystem.IntegerMessage(MPServerOptimization.InputBufferLength);
				Client.GetClient().Send(MessageTypes.MsgINPUTBUFFER, msg);
				Debug.Log("-------------------------------");
				Debug.Log("CCF Sending message to set server buffer length set to: " + MPServerOptimization.InputBufferLength);
				Debug.Log("-------------------------------");
			}
			else
            {
				Debug.Log("-------------------------------");
				Debug.Log("CCF Client is not currently connected, no server to send to.");
				Debug.Log("-------------------------------");
			}
		}
	}
	// END TEMPORARY
	*/

	[HarmonyPatch]
	public static class MPServerOptimization
	{
		public static bool prefEnabled = true; // the toggle used by the menus - sent to the server on match creation and saved in player settings, but otherwise not used
		public static bool enabled = true; // The toggle used in-match to determine whether or not to use the client-side physics optimizations

		public static int InputBufferLength = 2; // 3 ticks is stock (50ms), 2 seems universally smooth (33ms), 1 feels great but is very network-sensitive (16.7ms) -- sticking with 2 ticks for now
		public static float CatchUpFactor = 0.1f; // what percentage of backlogged packets to process in a single frame (minimum of 1 frame processed, if there are any)

		public static bool ODTurning = true; // allows OD to boost turning speed if true
		public static bool RollFix = false; // allows roll speed to be unaffected by mouse movement if true

		public static PlayerEncodedPhysics current;
		public static PlayerPhysicsMessage message;

		public static PlayerState[] m_player_state_history;

		// grab a permanent reference to the private m_player_state_history array so we don't have to keep Reflecting it
		[HarmonyPatch(typeof(Client), "Init")]
		public static class MPServerOptimization_ClientInit
		{
			public static void Postfix(PlayerState[] ___m_player_state_history)
			{
				m_player_state_history = ___m_player_state_history;
			}
		}

		/* Contains a bunch of different patches, not just server stuff -- we have too many parallel patches as it is.
		 * 1. (Prefix & Transpiler) Moves the player rigidbody turn limiting to the front of the method, since the AddForce/AddTorque stuff doesn't actually affect the rigidbody values until *after* the physics step so it didn't matter where it was, which enables:
		 * 2. (Prefix) On the server (and on clients during state reconciliation when the server supports it) bypass the control replay and just apply the final AddForce/AddTorque values calculated by the client.
		 * 3. (Postfix) On the client, after the original method, sends a replacement packet type containing these physics values when the server supports it.
		 * 4. (Transpiler) Records the accumulated physics calls from the client for use in the packet from #3
		 * 5. (Transpiler) Puts an option in to allow the overdrive-induced turn boost to be disabled if desired
		 * 6. (Transpiler) Fixes the roll-speed bug for mouse players (provided they've enabled it, since it *does* change the handling feel)
		 */
		[HarmonyPatch(typeof(PlayerShip), "FixedUpdateProcessControlsInternal")]
		public static class MPServerOptimization_FixedUpdateProcessControlsInternal
		{
			public static Vector3 force = Vector3.zero;
			public static Vector3 torque = Vector3.zero;

			public static bool Prefix(PlayerShip __instance)
			{
				int TurnLimitIdx = __instance.c_player.m_player_control_options.opt_turn_speed_limit;
				if (GameplayManager.IsMultiplayerActive)
				{
					TurnLimitIdx = Mathf.Min(__instance.m_turn_speed_mp, TurnLimitIdx);
				}

				float TurnLimitRigidBody = PlayerShip.m_turn_speed_limit_rb[TurnLimitIdx];
				float TurnMagnitude = __instance.c_rigidbody.angularVelocity.magnitude;
				if (TurnMagnitude > TurnLimitRigidBody)
				{
					__instance.c_rigidbody.angularVelocity *= TurnLimitRigidBody / TurnMagnitude;
				}

				if (enabled && (NetworkSim.m_resimulating || (GameplayManager.IsDedicatedServer() && MPTweaks.ClientHasTweak(__instance.connectionToClient.connectionId, "cphysics")))) // supports client-side physics, also simplify resim
				{
					// BEGIN MOVEMENT

					if (current.m_boosting)
					{
						if (!NetworkSim.m_resimulating) { __instance.c_player.UseEnergy(RUtility.FRAMETIME_FIXED * 0.5f); }
						__instance.m_boosting = true;
					}
					else if (__instance.m_boosting) // server ship was boosting, incoming says we aren't anymore
					{
						if (!NetworkSim.m_resimulating) { __instance.BoostStopped(); }
						__instance.m_boosting = false;
					}

					__instance.c_rigidbody.AddForce(current.move_dir);

					// END MOVEMENT
					// BEGIN ROTATION

					__instance.c_rigidbody.AddTorque(current.rot_dir);

					if (GameplayManager.IsMultiplayerActive && __instance.c_mesh_collider_trans != null && __instance.c_transform != null)
					{
						__instance.c_mesh_collider_trans.localPosition = __instance.c_transform.localPosition; // rotation is taken care of in the Postfix for all cases
					}

					return false; // skip the original flow
				}
				else // older server, use the normal flow
				{
					return true;
				}
			}

			public static void Postfix(PlayerShip __instance)
			{
				NetworkClient client = Client.GetClient();
				if (enabled && client != null && client.isConnected && GameplayManager.IsMultiplayerActive && !GameplayManager.IsDedicatedServer() && __instance.c_player.NeedToSendFixedUpdateMessages() && __instance.isLocalPlayer && !NetworkSim.m_resimulating)
				{
					current.m_boosting = __instance.m_boosting;
					current.move_dir = force;
					current.rot_dir = torque;

					client.SendByChannel(MessageTypes.MsgPlayerPhysics, message, 2); // *now* we send the new packet type including the pre-calculated physics moves
				}
				force = Vector3.zero;
				torque = Vector3.zero;
			}

			public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
			{
				List<CodeInstruction> hold = new List<CodeInstruction>();
				List<CodeInstruction> jump = new List<CodeInstruction>();

				int skip = 0;
				int splice = 0;
				bool od = false;

				foreach (var code in codes)
				{
					if (skip != 1 && splice < 1)
					{
						// Force vector
						if (code.opcode == OpCodes.Callvirt && code.operand == AccessTools.Method(typeof(Rigidbody), "AddForce", new Type[] { typeof(Vector3) }))
						{
							yield return new CodeInstruction(OpCodes.Dup);
							yield return new CodeInstruction(OpCodes.Stsfld, AccessTools.Field(typeof(MPServerOptimization_FixedUpdateProcessControlsInternal), "force")); // store the AddForce() value for sending in the MsgPlayerPhysics packet
						}
						// Torque vector
						else if (code.opcode == OpCodes.Callvirt && code.operand == AccessTools.Method(typeof(Rigidbody), "AddTorque", new Type[] { typeof(Vector3) }))
						{
							skip++; // starts skipping instructions used for the angular velocity limiter after the first occurrence of AddTorque
							yield return new CodeInstruction(OpCodes.Dup);
							yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPServerOptimization_FixedUpdateProcessControlsInternal), "AccumulateTorque")); // accumulate the various AddTorque() calls for one single call in the MsgPlayerPhysics packet
						}
						// Landmarking for the roll fix
						else if (code.opcode == OpCodes.Stloc_S && ((LocalBuilder)code.operand).LocalIndex == 29 && splice != -1) // first occurence of AimRoll for fixing the mouse movement roll nerf bug
						{
							splice++;
						}
						// Landmarking for the OD turning toggle
						else if (code.opcode == OpCodes.Ldc_R4 && (float)code.operand == 5.8f) // joystick overdrive turning toggle insertion
						{
							od = true;
						}
						// Method swap for the OD turning toggle
						else if (od && code.opcode == OpCodes.Ldfld && code.operand == AccessTools.Field(typeof(Player), "m_overdrive"))
						{
							code.opcode = OpCodes.Call;
							code.operand = AccessTools.Method(typeof(MPServerOptimization_FixedUpdateProcessControlsInternal), "OverdriveCheck");
							od = false;
						}
						yield return code;
					}
					// Chop out the in-method angular velocity limiter. It didn't matter where it was getting executed since the torques didn't get applied until after the physics step, so now it's just out front of everything (see the Prefix).
					else if (code.opcode == OpCodes.Callvirt && code.operand == AccessTools.Method(typeof(Rigidbody), "set_angularVelocity"))
					{
						skip++;
					}
					// Stores the instructions that handle rolling bound to the actual mouse X-axis for reinsertion later
					else if (splice == 1)
					{
						hold.Add(code);
						if (code.opcode == OpCodes.Stloc_S && ((LocalBuilder)code.operand).LocalIndex == 29) // second occurence of AimRoll
						{
							splice++;
						}
					}
					// Stores the instructions that apply the joy/key roll speed multiplier
					else if (splice == 2)
					{
						jump.Add(code);
						// Landmarking for the end of the roll multiplier instructions
						if (code.opcode == OpCodes.Stloc_S && ((LocalBuilder)code.operand).LocalIndex == 29) // third occurence of AimRoll
						{
							splice++;
						}
						// The array index for the roll speed multiplier is now fed into a method (along with the mouse aim bool) that figures out which actual index value to spit out
						else if (code.opcode == OpCodes.Ldfld && code.operand == AccessTools.Field(typeof(PlayerControlsOptions), "opt_joy_speed_roll"))
						{
							jump.Add(new CodeInstruction(OpCodes.Ldloc_S, 31)); // bool MouseHasBeenAimed
							jump.Add(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPServerOptimization_FixedUpdateProcessControlsInternal), "RollFixCheck")));
						}
					}
					// Perform the reinsertion of the two stored blocks in the reverse order -- we need to trim it a little, and we also need to swap a label
					else if (splice == 3)
					{
						splice = -1;

						code.labels.Clear();
						jump[0].MoveLabelsTo(code);
						jump.RemoveRange(0, 2); // we don't need the if statement anymore since we're doing the multiplier -before- any potential mouse axis roll is added

						foreach (CodeInstruction c in Enumerable.Concat(jump, hold))
						{
							yield return c;
						}
						yield return code;
					}
				}
			}

			public static void AccumulateTorque(Vector3 t)
			{
				torque += t;
			}

			public static bool OverdriveCheck(Player p)
			{
				return p.m_overdrive && (ODTurning || !enabled);
			}

			public static int RollFixCheck(int rollSpeed, bool mouseAimed)
			{
				if ((GameplayManager.IsDedicatedServer() || !RollFix || !enabled) && mouseAimed) // only way we hit this as a server is if the client doesn't support the server optimizations, thus the roll fix
				{
					return 3; // neutral position in the array, = 1f
				}
				return rollSpeed;
			}
		}

		// We're going to piggyback on the stored inputs through inheritance in a couple of places so the fields can be used to store both packet types.
		public class PlayerPhysicsMessage : PlayerInputMessage
		{
			public PlayerPhysicsMessage() { }

			public override void Serialize(NetworkWriter writer)
			{
				if (enabled)
				{
					writer.WritePackedUInt32((uint)m_tick);
					writer.Write((ushort)m_inputs.Length);

					foreach (PlayerEncodedPhysics p in m_inputs)
					{
						writer.WritePackedUInt32(p.m_encoded_bits);
						writer.Write(p.m_boosting);
						writer.Write(p.move_dir.x);
						writer.Write(p.move_dir.y);
						writer.Write(p.move_dir.z);
						writer.Write(p.rot_dir.x);
						writer.Write(p.rot_dir.y);
						writer.Write(p.rot_dir.z);
					}
				}
				else
				{
					base.Serialize(writer); // send the original packet information
				}
			}

			public override void Deserialize(NetworkReader reader)
			{
				m_tick = (int)reader.ReadPackedUInt32();
				int num = reader.ReadUInt16();
				m_inputs = new PlayerEncodedPhysics[num];

				for (int i = 0; i < num; i++)
				{
					PlayerEncodedPhysics p = new PlayerEncodedPhysics();
					p.m_encoded_bits = reader.ReadPackedUInt32();
					//phys.m_encoded_input = p.m_encoded_input; // no need for these anymore
					p.m_boosting = reader.ReadBoolean();
					p.move_dir.x = reader.ReadSingle();
					p.move_dir.y = reader.ReadSingle();
					p.move_dir.z = reader.ReadSingle();
					p.rot_dir.x = reader.ReadSingle();
					p.rot_dir.y = reader.ReadSingle();
					p.rot_dir.z = reader.ReadSingle();
					m_inputs[i] = p;
				}
			}
		}

		// More inheritance. Gets carried around in the existing fields and methods.
		public class PlayerEncodedPhysics : PlayerEncodedInput
		{
			public bool m_boosting;
			public Vector3 move_dir;
			public Vector3 rot_dir;
		}

		[HarmonyPatch(typeof(PlayerShip), "FixedUpdateReadControls")]
		public static class MPServerOptimization_FixedUpdateReadControls
		{
			public static PlayerEncodedInput SendPlayerPhysicsToServer(Player p)
			{
				NetworkClient client = Client.GetClient();
				if (client == null || !client.isConnected)
				{
					Debug.LogErrorFormat("Null client when trying to send fixed update state to server");
					return null;
				}
				PlayerPhysicsMessage playerPhysicsMessage = new PlayerPhysicsMessage();
				if (Client.m_last_acknowledged_tick >= Client.m_tick)
				{
					Debug.LogWarningFormat("Last achnowledged tick {0} greater than or equal to tick {1}", Client.m_last_acknowledged_tick, Client.m_tick);
					Client.m_last_acknowledged_tick = -1;
				}
				int ticks = Mathf.Clamp(Client.m_tick - Client.m_last_acknowledged_tick - 1, 0, 20); // Player.MAX_UNACKNOWLEDGED_TICKS was normally 25, not 20
				playerPhysicsMessage.m_tick = Client.m_tick - ticks;
				playerPhysicsMessage.m_inputs = new PlayerEncodedInput[ticks + 1];
				for (int i = 0; i < ticks; i++)
				{
					playerPhysicsMessage.m_inputs[i] = Client.GetEncodedPlayerInputFromHistory((playerPhysicsMessage.m_tick + i) % 1024);
				}
				playerPhysicsMessage.m_inputs[ticks] = new PlayerEncodedPhysics();
				playerPhysicsMessage.m_inputs[ticks].m_encoded_bits = p.EncodePlayerButtonPresses();
				playerPhysicsMessage.m_inputs[ticks].m_encoded_bits |= p.GetEncodedInputFilter() << 16;
				playerPhysicsMessage.m_inputs[ticks].m_encoded_input = p.EncodeInput();
				// physics info added during FUPCI
				current = (PlayerEncodedPhysics)playerPhysicsMessage.m_inputs[ticks];
				message = playerPhysicsMessage;

				if (!enabled)
				{
					client.SendByChannel(62, playerPhysicsMessage, 2); // This will send the original input packet to legacy servers. Otherwise, wait until after FUPCI and send the new one.
				}

				// We're also now -NOT- returning null anymore in the case of a send failure. There's already a retry mechanic built-in to this with it sending multiple frames of input in each packet. The way it was originally had the possibility to drop inputs.
				return playerPhysicsMessage.m_inputs[ticks];
			}

			public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
			{
				foreach (var code in codes)
				{
					if (code.opcode == OpCodes.Callvirt && code.operand == AccessTools.Method(typeof(Player), "SendPlayerControlsToServer"))
					{
						code.opcode = OpCodes.Call;
						code.operand = AccessTools.Method(typeof(MPServerOptimization_FixedUpdateReadControls), "SendPlayerPhysicsToServer");
					}
					yield return code;
				}
			}
		}

		[HarmonyPatch(typeof(Server), "RegisterHandlers")]
		public static class MPServerOptimization_RegisterHandlers
		{
			public static void Postfix()
			{
				NetworkServer.RegisterHandler(MessageTypes.MsgPlayerPhysics, OnPlayerPhysicsToServer);
				//NetworkServer.RegisterHandler(MessageTypes.MsgINPUTBUFFER, OnINPUTBUFFER);
			}
		}

		/*
		public static void OnINPUTBUFFER(NetworkMessage msg)
		{
			var message = msg.ReadMessage<UnityEngine.Networking.NetworkSystem.IntegerMessage>();
			InputBufferLength = message.value;
			Debug.Log("-------------------------------");
			Debug.Log("CCF Message received to set server buffer length to: " + InputBufferLength);
			Debug.Log("-------------------------------");
		}
		*/

		public static void OnPlayerPhysicsToServer(NetworkMessage msg)
		{
			Player playerSendingMessage = Server.FindPlayerByConnectionId(msg.conn.connectionId);
			if (playerSendingMessage == null)
			{
				Debug.LogErrorFormat("Unable to find Player for connection ID: {0}", msg.conn.connectionId);
			}

			if ((bool)playerSendingMessage && !playerSendingMessage.isLocalPlayer)
			{
				PlayerPhysicsMessage msg2 = msg.ReadMessage<PlayerPhysicsMessage>();
				OL_Server.QueueNewInputsForProcessingOnServer(playerSendingMessage, msg2);
			}
			//PlayerInputMessage msg2 = msg.ReadMessage<PlayerInputMessage>();
		}

		[HarmonyPatch(typeof(Player), "ApplyFixedUpdateInputMessage")]
		public static class MPServerOptimization_ApplyFixedUpdateInputMessage
		{
			//private static Dictionary<Player, int> states = new Dictionary<Player, int>();

			public static bool Prefix(Player __instance, PlayerEncodedInput input)
			{
				if (enabled && (NetworkSim.m_resimulating || (GameplayManager.IsDedicatedServer() && MPTweaks.ClientHasTweak(__instance.connectionToClient.connectionId, "cphysics"))))
				{
					__instance.ClearCachedInput();
					__instance.DecodePlayerButtonPresses(input.m_encoded_bits & 0xFFFFu);
					return false;
				}
				return true;
			}

			public static void Postfix(Player __instance, PlayerEncodedInput input)
			{
				if (enabled && (NetworkSim.m_resimulating || (GameplayManager.IsDedicatedServer() && MPTweaks.ClientHasTweak(__instance.connectionToClient.connectionId, "cphysics"))))
				{
					current = (PlayerEncodedPhysics)input; // always keep a record of the current physics state regardless of where in the physics step it came from
				}
			}
		}

		// Several variables no longer have any effect on the server and so should just be overwritten from the client history if a correction needs to happen.
		[HarmonyPatch(typeof(Player), "RewindStateToMessage")]
		public static class MPServerOptimization_RewindStateToMessage
		{
			public static void Postfix(PlayerShip ___c_player_ship, PlayerStateToClientMessage msg)
			{
				if (enabled)
				{
					PlayerState playerState = m_player_state_history[msg.m_tick & 1023];

					___c_player_ship.m_turning_bank = playerState.m_turning_bank;
					___c_player_ship.m_turn_overage_pitch = playerState.m_turn_overage_pitch;
					___c_player_ship.m_turn_overage_yaw = playerState.m_turn_overage_yaw;
					___c_player_ship.m_roll90_remaining = playerState.m_roll90_remaining;
					___c_player_ship.m_boost_overheat_timer = playerState.m_boost_overheat_timer;
					___c_player_ship.m_boost_heat = playerState.m_boost_heat;
					___c_player_ship.m_prev_mouse_move = playerState.m_prev_mouse_move;
					___c_player_ship.m_prev_mouse_aim = playerState.m_prev_mouse_aim;
					___c_player_ship.m_turn_slope = playerState.m_turn_slope;
					___c_player_ship.m_ramp_turn = playerState.m_ramp_turn;
				}
			}
		}

		// Changes the input buffer length on the server to lower movement latency
		[HarmonyPatch(typeof(Server), "ProcessCachedControlsRemote")]
		public static class MPServerOptimization_ProcessCachedControlsRemote
		{
			public static void Prefix(Player player)
            {
				player.m_send_updated_state = false; // reset this at the start of the method instead of later in the process
            }

			public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
			{
				foreach (var code in codes)
				{
					if (code.opcode == OpCodes.Ldc_I4_3) // this is the number of packets that need to be collected before input is considered "primed"
					{
						code.opcode = OpCodes.Ldsfld;
						code.operand = AccessTools.Field(typeof(MPServerOptimization), "InputBufferLength");
					}
					yield return code;
				}
			}
		}

		// Replaces AccelerateInputs completely to change the input buffer length on the server to lower movement latency -- also inserts some missing calls from ProcessCachedControlsRemote that should have been included here as well
		[HarmonyPatch(typeof(Server), "AccelerateInputs")]
		public static class MP_ServerAccelerate
		{
			public static bool Prefix()
			{
				int num = 0;

				// figures out who needs to be caught up and by how much
				foreach (Player player in Overload.NetworkManager.m_Players)
				{
					if (player != null && !player.m_spectator)
					{
						player.m_input_deficit = 60; // moved from MPClientExtrapolation

						if (player.m_InputToProcessOnServer.Count > InputBufferLength) // controls how far to "catch up" to keep the buffer size low
						{
							player.m_num_inputs_to_accelerate = Mathf.Max(1, Mathf.FloorToInt((float)player.m_InputToProcessOnServer.Count * CatchUpFactor));
						}
						else
						{
							player.m_num_inputs_to_accelerate = 0;
						}
						if (!player.c_player_ship.m_dying && !player.c_player_ship.m_dead)
						{
							player.m_num_inputs_to_accelerate = Mathf.Clamp(player.m_num_inputs_to_accelerate, 0, player.m_input_deficit);
						}
						player.m_input_deficit -= player.m_num_inputs_to_accelerate;
						player.m_input_deficit = Mathf.Clamp(player.m_input_deficit, 0, int.MaxValue);
						if (player.m_num_inputs_to_accelerate > num)
						{
							num = player.m_num_inputs_to_accelerate;
						}
					}
				}

				if (num == 0)
				{
					return false;
				}
				NetworkSim.PauseAllRigidBodiesExceptPlayers();

				// performs the actual catch-up operations
				for (int i = 0; i < num; i++)
				{
					foreach (Player player2 in Overload.NetworkManager.m_Players)
					{
						if (!(player2 == null) && !player2.m_spectator)
						{
							if (player2.m_num_inputs_to_accelerate == 0)
							{
								NetworkSim.PauseRigidBody(player2.c_player_ship.c_rigidbody);
								continue;
							}
							PlayerEncodedInputWithTick playerEncodedInputWithTick = player2.m_InputToProcessOnServer.Dequeue();

							player2.m_updated_state.m_tick = playerEncodedInputWithTick.m_tick; // This is in ProcessCachedControlsRemote() -- I don't see a good reason why it shouldn't be here too
							player2.m_send_updated_state = true; // same here ^^

							player2.ApplyFixedUpdateInputMessage(playerEncodedInputWithTick.m_input);
							OL_Server.SendJustPressedOrJustReleasedMessage(player2, CCInput.FIRE_WEAPON);
							OL_Server.SendJustPressedOrJustReleasedMessage(player2, CCInput.FIRE_MISSILE);
							player2.c_player_ship.FixedUpdateProcessControls();

							player2.ProcessRemotePlayerFiringControlsPost(); // and same here. Button states should be properly updated even if we're fast-fowarding (even with client-side physics).

							player2.m_num_inputs_to_accelerate--;
						}
					}
					Physics.Simulate(Time.fixedDeltaTime);
				}
				NetworkSim.ResumeAllPausedRigidBodies();

				return false;
			}
		}
	}
}
