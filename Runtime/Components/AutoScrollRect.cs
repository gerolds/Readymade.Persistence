﻿// by Travis_Singleton

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent ( typeof ( ScrollRect ) )]
public class AutoScrollRect : MonoBehaviour {
    // Sets the speed to move the scrollbar
    public float scrollSpeed = 10f;

    // Set as Template Object via (Your Dropdown Button > Template)
    public ScrollRect m_templateScrollRect;

    // Set as Template Viewport Object via (Your Dropdown Button > Template > Viewport)
    public RectTransform m_templateViewportTransform;

    // Set as Template Content Object via (Your Dropdown Button > Template > Viewport > Content)
    public RectTransform m_ContentRectTransform;

    private RectTransform m_SelectedRectTransform;

    /// <summary>
    /// Event function.
    /// </summary>
    private void Update () {
        UpdateScrollToSelected ( m_templateScrollRect, m_ContentRectTransform, m_templateViewportTransform );
    }

    /// <summary>
    /// Update the scroll position to ensure the selected option remains visible.
    /// </summary>
    /// <param name="scrollRect">The <see cref="ScrollRect"/> component.</param>
    /// <param name="contentRectTransform">The <see cref="RectTransform"/> of the scroll content.</param>
    /// <param name="viewportRectTransform">The <see cref="RectTransform"/> of the scroll viewport.</param>
    private void UpdateScrollToSelected (
        ScrollRect scrollRect,
        RectTransform contentRectTransform,
        RectTransform viewportRectTransform
    ) {
        // Get the current selected option from the EventSystem.
        GameObject selected = EventSystem.current.currentSelectedGameObject;

        if ( selected == null ) {
            return;
        }

        if ( selected.transform.parent != contentRectTransform.transform ) {
            return;
        }

        m_SelectedRectTransform = selected.GetComponent<RectTransform> ();

        // Math stuff
        Vector3 selectedDifference = viewportRectTransform.localPosition - m_SelectedRectTransform.localPosition;
        float contentHeightDifference = ( contentRectTransform.rect.height - viewportRectTransform.rect.height );

        float selectedPosition = ( contentRectTransform.rect.height - selectedDifference.y );
        float currentScrollRectPosition = scrollRect.normalizedPosition.y * contentHeightDifference;
        float above = currentScrollRectPosition - ( m_SelectedRectTransform.rect.height / 2 ) + viewportRectTransform.rect.height;
        float below = currentScrollRectPosition + ( m_SelectedRectTransform.rect.height / 2 );

        // Check if selected option is out of bounds.
        if ( selectedPosition > above ) {
            float step = selectedPosition - above;
            float newY = currentScrollRectPosition + step;
            float newNormalizedY = newY / contentHeightDifference;
            scrollRect.normalizedPosition = Vector2.Lerp ( scrollRect.normalizedPosition, new Vector2 ( 0, newNormalizedY ),
                scrollSpeed * Time.deltaTime );
        } else if ( selectedPosition < below ) {
            float step = selectedPosition - below;
            float newY = currentScrollRectPosition + step;
            float newNormalizedY = newY / contentHeightDifference;
            scrollRect.normalizedPosition = Vector2.Lerp ( scrollRect.normalizedPosition, new Vector2 ( 0, newNormalizedY ),
                scrollSpeed * Time.deltaTime );
        }
    }
}