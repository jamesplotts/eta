using Model;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ETA
{
    // Added to the locomotive window's name label: hover shows the engine's callout.
    internal sealed class LocoHover : MonoBehaviour, ITipSource, IPointerEnterHandler, IPointerExitHandler
    {
        public Car Car => TrainController.Shared?.SelectedLocomotive;

        public bool IsLive => isActiveAndEnabled;

        public void OnPointerEnter(PointerEventData eventData) => LocoTooltip.Show(this);

        public void OnPointerExit(PointerEventData eventData) => LocoTooltip.Hide(this);

        private void OnDisable() => LocoTooltip.Hide(this);
    }
}
