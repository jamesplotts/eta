using System.Collections.Generic;
using Game;
using Game.Messages;
using Model;
using Model.AI;
using UI.Formatting;
using UnityEngine;

namespace ETA
{
    // "ETA: 14:32, 2.3 mi to waypoint" for an engine running in AE Waypoint mode.
    // The same file is in ETA and RosterTip, so the two show the same figures.
    internal static class WaypointEta
    {
        // Speed is averaged over about this many seconds, so the ETA doesn't jump with
        // every little change in speed.
        private const float SmoothingSeconds = 20f;

        // Below this (m/s, about 1 mph) the train counts as stopped and there's no ETA.
        private const float StoppedSpeed = 0.45f;

        private static readonly Dictionary<string, float> Speeds = new Dictionary<string, float>();
        private static readonly List<string> Gone = new List<string>();

        // Averages the speed of every engine running to a waypoint. Called about once a second.
        public static void Sample(float seconds)
        {
            TrainController trains = TrainController.Shared;
            if (trains == null)
                return;
            float blend = 1f - Mathf.Exp(-seconds / SmoothingSeconds);
            Gone.Clear();
            Gone.AddRange(Speeds.Keys);
            foreach (Car car in trains.Cars)
            {
                if (!(car is BaseLocomotive locomotive) || !IsRunningToWaypoint(locomotive))
                    continue;
                float speed = Mathf.Abs(locomotive.velocity);
                Speeds[locomotive.id] = Speeds.TryGetValue(locomotive.id, out float average)
                    ? average + (speed - average) * blend
                    : speed;
                Gone.Remove(locomotive.id);
            }
            foreach (string id in Gone)
                Speeds.Remove(id);
        }

        public static void Clear() => Speeds.Clear();

        // Empty unless the engine has a waypoint route.
        public static string Line(BaseLocomotive locomotive)
        {
            if (locomotive == null || !IsRunningToWaypoint(locomotive))
                return "";
            float? remaining = locomotive.AutoEngineerPlanner.RemainingWaypointDistance;
            if (!remaining.HasValue)
                return "";

            string eta = "--:--";
            float speed = Speeds.TryGetValue(locomotive.id, out float average) ? average : Mathf.Abs(locomotive.velocity);
            // Speed set to 0: the train won't get there until it's told to move.
            if (new AutoEngineerPersistence(locomotive.KeyValueObject).Orders.MaxSpeedMph == 0)
                eta = "∞";
            else if (speed >= StoppedSpeed)
            {
                // Physics runs in real seconds; the clock runs TimeMultiplier times faster.
                float gameSeconds = remaining.Value / speed * TimeWeather.TimeMultiplier;
                GameDateTime arrival = TimeWeather.Now.AddingSeconds(gameSeconds);
                int minutes = Mathf.FloorToInt(arrival.Minutes);
                eta = $"{minutes / 60:D2}:{minutes % 60:D2}";
            }
            return $"ETA: {eta}, {Units.DistanceText(remaining.Value)} to waypoint";
        }

        // The planner only exists on the host, so a multiplayer client never has an ETA.
        private static bool IsRunningToWaypoint(BaseLocomotive locomotive)
        {
            return locomotive.AutoEngineerPlanner != null
                && new AutoEngineerPersistence(locomotive.KeyValueObject).Orders.Mode == AutoEngineerMode.Waypoint;
        }
    }

    // Keeps WaypointEta's speed averages up to date.
    internal sealed class EtaSampler : MonoBehaviour
    {
        private const float Interval = 1f;

        private float _nextSample;

        private void Update()
        {
            if (Time.unscaledTime < _nextSample)
                return;
            _nextSample = Time.unscaledTime + Interval;
            WaypointEta.Sample(Interval);
        }

        private void OnDestroy() => WaypointEta.Clear();
    }
}
