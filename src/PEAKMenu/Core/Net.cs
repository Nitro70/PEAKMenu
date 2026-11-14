using Photon.Pun;

namespace PEAKMenu.Core
{
    // Sends the game's own network messages. Fails quietly (and once into the log) when the view is
    // gone or we're not connected, so a stale pick can never throw into the menu.
    internal static class Net
    {
        public static bool Ready => PhotonNetwork.IsConnected || PhotonNetwork.OfflineMode;

        public static void Rpc(PhotonView view, string method, RpcTarget target, params object[] args)
        {
            if (!view || !Ready)
            {
                return;
            }

            view.RPC(method, target, args);
        }

        // to one player only, for messages only their own game should act on
        public static void RpcTo(PhotonView view, string method, Photon.Realtime.Player player, params object[] args)
        {
            if (!view || !Ready || player == null)
            {
                return;
            }

            view.RPC(method, player, args);
        }
    }
}
