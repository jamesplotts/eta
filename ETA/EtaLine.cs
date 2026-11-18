using System.Reflection;
using Game;
using Game.Messages;
using Model;
using Model.AI;
using TMPro;
using UI.EngineControls;
using UI.Formatting;
using UI.Tooltips;
using UnityEngine;
using UnityEngine.UI;

namespace ETA
{
    // Adds a line under the locomotive window's "cars, tons, ft" text with the ETA at the
    // Auto Engineer waypoint and the distance left to it.
    internal sealed class EtaLine : MonoBehaviour
    {
        private const float Interval = 1f;

        // Speed is averaged over about this many seconds, so the ETA doesn't jump with
        // every little change in speed.
        private const float SmoothingSeconds = 20f;

        // Below this (m/s, about 1 mph) the train counts as stopped and there's no ETA.
        private const float StoppedSpeed = 0.45f;

        private static readonly FieldInfo InfoALabelField =
            typeof(LocomotiveControlsUIAdapter).GetField("infoALabel", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo NameLabelField =
            typeof(LocomotiveControlsUIAdapter).GetField("nameLabel", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo InfoBLabelField =
            typeof(LocomotiveControlsUIAdapter).GetField("infoBLabel", BindingFlags.Instance | BindingFlags.NonPublic);

        private LocomotiveControlsUIAdapter _adapter;
        private TMP_Text _source;
        private TMP_Text _crew;
        private LocoHover _hover;
        private TMP_Text _label;
        private float _nextUpdate;
        private string _locomotiveId;
        private float _speed;
        private bool _logged;

        private void Update()
        {
            if (Time.unscaledTime < _nextUpdate)
                return;
            _nextUpdate = Time.unscaledTime + Interval;
            if (!EnsureLabel())
                return;
            _label.text = LineFor(TrainController.Shared?.SelectedLocomotive);
        }

        private void OnDestroy()
        {
            if (_crew != null)
                _crew.gameObject.SetActive(true);
            if (_hover != null)
                Destroy(_hover);
            LocoTooltip.Shutdown();
            if (_label != null)
                Destroy(_label.gameObject);
        }

        private bool EnsureLabel()
        {
            if (_label != null && _adapter != null)
                return true;
            _adapter = FindFirstObjectByType<LocomotiveControlsUIAdapter>(FindObjectsInactive.Include);
            if (_adapter == null || InfoALabelField == null)
                return false;
            _source = InfoALabelField.GetValue(_adapter) as TMP_Text;
            if (_source == null)
                return false;
            LogLayout();
            _crew = InfoBLabelField?.GetValue(_adapter) as TMP_Text;
            _label = MakeLabel(_source, _crew);
            // The ETA line replaces the crew line; the crew is in the name's hover callout.
            if (_crew != null)
                _crew.gameObject.SetActive(false);
            if (NameLabelField?.GetValue(_adapter) is TMP_Text name)
            {
                name.raycastTarget = true;
                _hover = name.gameObject.GetComponent<LocoHover>() ?? name.gameObject.AddComponent<LocoHover>();
            }
            return true;
        }

        // A copy of the "cars, tons, ft" label, in the crew line's place just below it.
        private static TMP_Text MakeLabel(TMP_Text source, TMP_Text crew)
        {
            GameObject copy = Instantiate(source.gameObject, source.transform.parent);
            copy.name = "ETA Label";
            foreach (Transform child in copy.transform)
                Destroy(child.gameObject);
            UITooltipProvider tooltip = copy.GetComponent<UITooltipProvider>();
            if (tooltip != null)
                Destroy(tooltip);
            // Keep any layout group on the parent from moving the original or the copy.
            LayoutElement element = copy.GetComponent<LayoutElement>() ?? copy.AddComponent<LayoutElement>();
            element.ignoreLayout = true;

            TMP_Text label = copy.GetComponent<TMP_Text>();
            label.text = "";
            label.raycastTarget = false;
            RectTransform to = label.rectTransform;
            if (crew != null)
            {
                RectTransform from = crew.rectTransform;
                to.anchorMin = from.anchorMin;
                to.anchorMax = from.anchorMax;
                to.pivot = from.pivot;
                to.sizeDelta = from.sizeDelta;
                to.anchoredPosition = from.anchoredPosition;
                label.alignment = crew.alignment;
            }
            else
            {
                RectTransform from = source.rectTransform;
                to.anchoredPosition = from.anchoredPosition - new Vector2(0f, from.rect.height);
            }
            copy.transform.SetSiblingIndex(source.transform.GetSiblingIndex() + 1);
            return label;
        }

        private string LineFor(BaseLocomotive locomotive)
        {
            if (locomotive == null)
                return "";
            if (locomotive.id != _locomotiveId)
            {
                _locomotiveId = locomotive.id;
                _speed = Mathf.Abs(locomotive.velocity);
            }
            else
            {
                _speed += (Mathf.Abs(locomotive.velocity) - _speed) * (1f - Mathf.Exp(-Interval / SmoothingSeconds));
            }

            // The planner only exists on the host, so a multiplayer client shows nothing.
            AutoEngineerPlanner planner = locomotive.AutoEngineerPlanner;
            if (planner == null)
                return "";
            Orders orders = new AutoEngineerPersistence(locomotive.KeyValueObject).Orders;
            if (orders.Mode != AutoEngineerMode.Waypoint)
                return "";
            float? remaining = planner.RemainingWaypointDistance;
            if (!remaining.HasValue)
                return "";

            // Speed set to 0: the train won't get there until it's told to move.
            string eta = "--:--";
            if (orders.MaxSpeedMph == 0)
                eta = "\u221E";
            else if (_speed >= StoppedSpeed)
            {
                // Physics runs in real seconds; the clock runs TimeMultiplier times faster.
                float gameSeconds = remaining.Value / _speed * TimeWeather.TimeMultiplier;
                GameDateTime arrival = TimeWeather.Now.AddingSeconds(gameSeconds);
                int minutes = Mathf.FloorToInt(arrival.Minutes);
                eta = $"{minutes / 60:D2}:{minutes % 60:D2}";
            }
            return $"ETA: {eta}, {Units.DistanceText(remaining.Value)} to waypoint";
        }

        // One-time note of where the game's labels sit, for adjusting the line's position.
        private void LogLayout()
        {
            if (_logged)
                return;
            _logged = true;
            Transform parent = _source.transform.parent;
            ModEntry.Log?.Info($"ETA: info label parent '{parent.name}', layout {parent.GetComponent<LayoutGroup>()?.GetType().Name ?? "none"}");
            foreach (Transform child in parent)
            {
                RectTransform rect = child as RectTransform;
                TMP_Text text = child.GetComponent<TMP_Text>();
                ModEntry.Log?.Info($"ETA:   '{child.name}' pos {rect?.anchoredPosition} size {rect?.sizeDelta} anchors {rect?.anchorMin}-{rect?.anchorMax} pivot {rect?.pivot}"
                    + (text != null ? $" font {text.fontSize} align {text.alignment} text '{text.text}'" : ""));
            }
        }
    }
}
