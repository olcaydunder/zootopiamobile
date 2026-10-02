using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Holds a UI element down (used for the fire buttons).</summary>
public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public bool Held { get; private set; }

    public void OnPointerDown(PointerEventData eventData) { Held = true; }
    public void OnPointerUp(PointerEventData eventData) { Held = false; }
    private void OnDisable() { Held = false; }
}
