using Overload;

namespace GameMod
{
    class MPHUDMessage
    {
        public static void SendToAll(string message, int id = -1, bool priority = false)
        {
            foreach (Player player in Overload.NetworkManager.m_Players)
                player.CallTargetAddHUDMessage(player.connectionToClient, message, id, priority);
        }
    }
}
