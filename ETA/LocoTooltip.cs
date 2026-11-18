using System.Collections.Generic;
using Game.Settings;
using System.Reflection;
using Helpers;
using Model;
using UI;
using UI.Tags;
using UI.Tooltips;
using UnityEngine;
using UnityEngine.UI;

namespace ETA
{
    // One callout for the hovered locomotive name, built from the Tab tag prefab so it has the same
    // colored title bar and background, drawn on the game's tooltip canvas.
    internal sealed class LocoTooltip : MonoBehaviour
    {
        private const float RefreshSeconds = 0.5f;

        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo TagPrefabField = typeof(TagController).GetField("tagCalloutPrefab", Private);
        private static readonly FieldInfo TooltipCanvasField = typeof(UITooltipController).GetField("canvas", Private);
        private static readonly FieldInfo TooltipPrefabField = typeof(UITooltipController).GetField("calloutPrefab", Private);

        private static TagCallout TagPrefab(TagController tags) => TagPrefabField?.GetValue(tags) as TagCallout;
        private static Canvas TooltipCanvas(UITooltipController controller) => TooltipCanvasField?.GetValue(controller) as Canvas;
        private static Callout TooltipPrefab(UITooltipController controller) => TooltipPrefabField?.GetValue(controller) as Callout;

        private static LocoTooltip _shared;

        private Canvas _canvas;
        private RectTransform _canvasRect;
        private Callout _callout;
        private CanvasGroup _group;
        private readonly List<Image> _colorImages = new List<Image>();
        private ITipSource _hover;
        private float _nextRefresh;

        public static void Show(ITipSource hover)
        {
            LocoTooltip tooltip = GetOrCreate();
            if (tooltip == null)
                return;
            tooltip._hover = hover;
            tooltip._nextRefresh = 0f;
            tooltip.Refresh();
        }

        public static void Hide(ITipSource hover)
        {
            if (_shared != null && _shared._hover == hover)
                _shared.HideNow();
        }

        public static void Shutdown()
        {
            if (_shared != null)
                Destroy(_shared._callout != null ? _shared._callout.gameObject : _shared.gameObject);
            _shared = null;
        }

        private static LocoTooltip GetOrCreate()
        {
            if (_shared != null)
                return _shared;
            UITooltipController controller = UITooltipController.Shared;
            Canvas canvas = controller != null ? TooltipCanvas(controller) : null;
            if (canvas == null)
                return null;

            // Prefer the Tab tag callout; fall back to the game's plain tooltip callout.
            TagController tags = TagController.Shared;
            TagCallout tagPrefab = tags != null ? TagPrefab(tags) : null;
            Callout prefab = tagPrefab != null ? tagPrefab.callout : TooltipPrefab(controller);
            if (prefab == null)
                return null;

            Callout callout = Instantiate(prefab, canvas.transform);
            callout.gameObject.name = "ETA Callout";
            callout.transform.localRotation = Quaternion.identity;
            callout.transform.localScale = Vector3.one;
            LocoTooltip tooltip = callout.gameObject.AddComponent<LocoTooltip>();
            tooltip._canvas = canvas;
            tooltip._canvasRect = canvas.GetComponent<RectTransform>();
            tooltip._callout = callout;
            tooltip._group = callout.GetComponent<CanvasGroup>() ?? callout.gameObject.AddComponent<CanvasGroup>();
            tooltip._group.blocksRaycasts = false;
            tooltip._group.interactable = false;
            foreach (Graphic graphic in callout.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = false;
            if (tagPrefab != null)
                tooltip.FindColorImages(tagPrefab);
            ModEntry.Log?.Info($"ETA: callout from {(tagPrefab != null ? "tag" : "tooltip")} prefab, {tooltip._colorImages.Count} color images.");
            tooltip.HideNow();
            _shared = tooltip;
            return tooltip;
        }

        // The tag's colorImages are references into the prefab; find the same objects in our copy.
        private void FindColorImages(TagCallout tagPrefab)
        {
            Transform prefabRoot = tagPrefab.callout.transform;
            foreach (Image image in tagPrefab.colorImages)
            {
                if (image == null || !image.transform.IsChildOf(prefabRoot))
                    continue;
                Transform copy = image.transform == prefabRoot ? _callout.transform : _callout.transform.Find(PathFrom(prefabRoot, image.transform));
                Image copyImage = copy != null ? copy.GetComponent<Image>() : null;
                if (copyImage != null)
                    _colorImages.Add(copyImage);
            }
        }

        private static string PathFrom(Transform root, Transform target)
        {
            string path = target.name;
            for (Transform t = target.parent; t != null && t != root; t = t.parent)
                path = t.name + "/" + path;
            return path;
        }

        private void HideNow()
        {
            _hover = null;
            if (_group != null)
                _group.alpha = 0f;
        }

        private void Update()
        {
            if (_hover == null)
                return;
            if ((_hover as Object) == null || !_hover.IsLive || UI.ContextMenu.ContextMenu.IsShown)
            {
                HideNow();
                return;
            }
            if (Time.unscaledTime >= _nextRefresh)
                Refresh();
            Place();
        }

        private void Refresh()
        {
            Car car = (_hover as Object) != null ? _hover.Car : null;
            if (car == null)
            {
                HideNow();
                return;
            }
            _nextRefresh = Time.unscaledTime + RefreshSeconds;
            _callout.Title = LocoInfo.Title(car);
            _callout.Text = LocoInfo.Text(car);
            _callout.Layout();
            Color color = LocoInfo.TagColor(car);
            foreach (Image image in _colorImages)
                image.color = color;
            _callout.transform.SetAsLastSibling();
            _group.alpha = 1f;
            Place();
        }

        // Left edge just above the mouse (below it near the top of the screen), kept on screen.
        // The tag callout's visible box isn't laid out from its root's pivot (tags center over
        // their car), so this measures what's drawn and moves the root by the difference.
        private void Place()
        {
            RectTransform rect = _callout.RectTransform != null ? _callout.RectTransform : (RectTransform)_callout.transform;
            Camera camera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, Input.mousePosition, camera, out Vector2 mouse))
                return;
            if (!TryGetDrawnBounds(out Vector2 min, out Vector2 max))
                return;
            Vector2 size = max - min;
            Rect canvasRect = _canvasRect.rect;
            Vector2 target = mouse + new Vector2(0f, 16f);
            if (target.y + size.y > canvasRect.yMax)
                target.y = mouse.y - 16f - size.y;   // no room above: hang it below the mouse
            target.x = Mathf.Clamp(target.x, canvasRect.xMin, Mathf.Max(canvasRect.xMin, canvasRect.xMax - size.x));
            target.y = Mathf.Clamp(target.y, canvasRect.yMin, Mathf.Max(canvasRect.yMin, canvasRect.yMax - size.y));
            Vector2 position = rect.anchoredPosition + (target - min);
            float scale = CanvasSettingsApplicator.EffectiveCanvasScale;
            rect.anchoredPosition = new Vector2(Mathf.Round(position.x * scale) / scale, Mathf.Round(position.y * scale) / scale);
        }

        private static readonly Vector3[] Corners = new Vector3[4];

        // The box around everything the callout draws, in the canvas's local space.
        private bool TryGetDrawnBounds(out Vector2 min, out Vector2 max)
        {
            min = new Vector2(float.MaxValue, float.MaxValue);
            max = new Vector2(float.MinValue, float.MinValue);
            bool any = false;
            foreach (Graphic graphic in _callout.GetComponentsInChildren<Graphic>())
            {
                if (!graphic.enabled)
                    continue;
                graphic.rectTransform.GetWorldCorners(Corners);
                foreach (Vector3 corner in Corners)
                {
                    Vector2 local = _canvasRect.InverseTransformPoint(corner);
                    min = Vector2.Min(min, local);
                    max = Vector2.Max(max, local);
                }
                any = true;
            }
            return any;
        }
    }
}
