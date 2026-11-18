using Model;

namespace ETA
{
    // Something the callout can be shown for: the locomotive name.
    internal interface ITipSource
    {
        Car Car { get; }

        // Still on screen and enabled.
        bool IsLive { get; }
    }
}
