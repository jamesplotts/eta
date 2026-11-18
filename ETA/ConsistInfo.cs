using System.Collections.Generic;
using System.Linq;
using Core;
using Model;
using Model.Ops;
using Model.Physics;
using UnityEngine;

namespace ETA
{
    // Whole-train facts: size, loads, how steep a grade the engines can take it up, and
    // where the cars are going.
    internal static class ConsistInfo
    {
        // The speed the grade is worked out at: the slowest column of the game's tonnage chart.
        private const float RatingMph = 10f;
        private const int MaxDestinationLines = 6;

        public static List<Car> Train(Car car) => car.EnumerateCoupled().ToList();

        public static List<string> Summary(List<Car> cars)
        {
            List<string> lines = new List<string>();
            float weightLbs = cars.Sum(c => c.Weight);
            int feet = Mathf.CeilToInt(ConsistLength.OfCars(cars) * 3.28084f);
            lines.Add($"{cars.Count.Pluralize("car")}, {Mathf.CeilToInt(weightLbs / 2000f):N0} tons, {feet:N0} ft");

            List<Car> freight = cars.Where(c => c.CarType.IsFreight).ToList();
            if (freight.Count > 0)
            {
                int loaded = freight.Count(IsLoaded);
                lines.Add($"{loaded} loaded, {freight.Count - loaded} empty");
            }

            string grade = MaxGradeText(cars, weightLbs);
            if (grade != null)
                lines.Add(grade);

            List<(string name, Color color, int count)> destinations = Destinations(cars);
            foreach ((string name, Color color, int count) in destinations.Take(MaxDestinationLines))
                lines.Add($"{count} to <color=#{ColorUtility.ToHtmlStringRGB(color)}>{name}</color>");
            if (destinations.Count > MaxDestinationLines)
                lines.Add($"{destinations.Skip(MaxDestinationLines).Sum(d => d.count)} to other places");
            return lines;
        }

        // The steepest grade the train's engines can lift it up at RatingMph, using the game's
        // own tonnage rating: 3 lb/ton rolling, 20 lb/ton per 1% grade, plus a 15° curve.
        private static string MaxGradeText(List<Car> cars, float weightLbs)
        {
            float tractiveEffort = cars.Where(c => c.IsLocomotive).Sum(c => TractiveEffortRating.AvailableTE(c.Definition, RatingMph));
            if (tractiveEffort <= 0f || weightLbs <= 0f)
                return null;
            float tons = weightLbs / 2000f;
            float lbsPerTonPerPercent = TractiveEffortRating.RequiredTE(2000f, 1f) - TractiveEffortRating.RequiredTE(2000f, 0f);
            float levelLbsPerTon = TractiveEffortRating.RequiredTE(2000f, 0f) + TractiveEffortRating.CurveResistance(2000f, TractiveEffortRating.AssumedCurveDegrees);
            float grade = (tractiveEffort / tons - levelLbsPerTon) / lbsPerTonPerPercent;
            if (grade < 0f)
                return $"<color=#FF6A5C>Over tonnage even on level track</color>";
            return $"Max grade {grade:0.0}% at {RatingMph:0} mph";
        }

        private static bool IsLoaded(Car car)
        {
            for (int i = 0; i < car.Definition.LoadSlots.Count; i++)
            {
                CarLoadInfo? info = car.GetLoadInfo(i);
                if (info.HasValue && info.Value.Quantity > 0f)
                    return true;
            }
            return false;
        }

        private static List<(string name, Color color, int count)> Destinations(List<Car> cars)
        {
            Dictionary<string, (Color color, int count)> byName = new Dictionary<string, (Color, int)>();
            foreach (Car car in cars)
            {
                if (!TryGetDestination(car, out string name, out Color color, out _))
                    continue;
                byName[name] = byName.TryGetValue(name, out (Color color, int count) seen) ? (seen.color, seen.count + 1) : (color, 1);
            }
            return byName.Select(kv => (kv.Key, kv.Value.color, kv.Value.count)).OrderByDescending(d => d.Item3).ToList();
        }

        // The destination's area color, as Map Enhancer colors its car icons: freight cars only,
        // shifted brighter or darker depending on whether the car has arrived.
        public static bool TryGetDestination(Car car, out string name, out Color color, out bool isAtDestination)
        {
            name = null;
            color = Color.white;
            isAtDestination = false;
            OpsController ops = OpsController.Shared;
            if (ops == null || !car.CarType.IsFreight)
                return false;
            if (!ops.TryGetDestinationInfo(car, out name, out isAtDestination, out _, out OpsCarPosition destination) || string.IsNullOrEmpty(name))
                return false;
            Area area = ops.AreaForCarPosition(destination);
            if (area != null)
                color = area.tagColor;
            float max = color.maxColorComponent;
            if (max >= 0.99f ? isAtDestination : !isAtDestination)
            {
                if (max < 0.99f)
                {
                    color *= 1f / Mathf.Max(max, 0.0001f);
                }
                else
                {
                    Color.RGBToHSV(color, out float h, out float s, out float v);
                    color = Color.HSVToRGB(h, s, v * 0.6f);
                }
                color.a = 1f;
            }
            return true;
        }
    }
}
