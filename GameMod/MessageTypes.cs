using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GameMod
{
    public class MessageTypes
    {
        public const short MsgSetMatchState = 101;
        public const short MsgAddMpStatus = 102;
        public const short MsgModPrivateData = 103;
        public const short MsgJIPJustJoined = 104;

        public const short MsgClientCapabilities = 119;
        public const short MsgMPTweaksSet = 120;
        public const short MsgCTFPickup = 121;
        public const short MsgCTFLose = 122;
        public const short MsgCTFNotifyOld = 123;
        public const short MsgCTFFlagUpdate = 124;
        public const short MsgCTFNotify = 125;
        public const short MsgLapCompleted = 126;
        public const short MsgSetFullMatchState = 127;
        public const short MsgCTFJoinUpdate = 128;

        public const short MsgCreeperSync = 130;
        public const short MsgExplode = 131;
        // public const short MsgSetAlternatingMissleFire = 132; // No longer used.
        public const short MsgSniperPacket = 133;
        public const short MsgPlayerWeaponSynchronization = 134;
        public const short MsgPlayerAddResource = 135;
        public const short MsgPlayerSyncResource = 136;
        public const short MsgPlayerSyncAllMissiles = 137;
        public const short MsgDetonate = 138;
        public const short MsgSetDisconnectedMatchState = 139;
        public const short MsgNewPlayerSnapshotToClient = 140;
        public const short MsgCTFPlayerStats = 141;
        public const short MsgMonsterballPlayerStats = 142;
        public const short MsgDeathReview = 143;

        public const short MsgSetTurnRampMode = 145;
        public const short MsgChangeTeam = 146;
        public const short MsgCustomLoadouts = 147;
        public const short MsgSetCustomLoadout = 148;

        public const short MsgSendDamage = 149;

        public const short MsgShareAudioTauntIdentifiers = 150;
        public const short MsgRequestAudioTaunt = 151;
        public const short MsgPlayAudioTaunt = 152;
        public const short MsgAudioTauntPacket = 153;

        // Do not use 400, it is in use by Mod-Projdata.dll.
    }
}
