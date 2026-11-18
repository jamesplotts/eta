using Railroader.Modding;
using UnityEngine;

namespace ETA
{
    // Entry point for Railroader's built-in mod loader (0.6+).
    public sealed class ModEntry : IModEntryPoint, IWorldLifecycle
    {
        internal static IModLogger Log;

        private static GameObject _host;

        public void OnModLoad(IModContext context)
        {
            Log = context.Log;
        }

        public void OnModUnload()
        {
            OnWorldUnload();
            Log = null;
        }

        public void OnWorldLoad()
        {
            if (_host != null)
                return;
            _host = new GameObject("ETA");
            _host.AddComponent<EtaLine>();
        }

        public void OnWorldUnload()
        {
            if (_host != null)
                Object.Destroy(_host);
            _host = null;
        }
    }
}
