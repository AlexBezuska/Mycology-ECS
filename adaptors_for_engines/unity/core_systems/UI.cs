using System;
using UnityEngine;
using UnityEngine.UI;

using TMPro;

namespace Mycology_ECS.translation_layers.unity.entity_helpers
{
    public static class UI
    {
        private static Canvas _cachedCanvas;

        // These mirror the legacy JSON defs in Myco_Unity_Entities.cs.
        [Serializable]
        public sealed class UiImageDef
        {
            public float[] size;
            public float[] anchored_position;
            public string anchor;
            public string color;
            public string sprite;
        }

        [Serializable]
        public sealed class UiTextDef
        {
            public string text;
            public float font_size;
            public float[] anchored_position;
            public string anchor;
            public string color;
        }

        /// <summary>
        /// Ensures the single Mycology-managed canvas exists.
        /// Mycology uses a single canvas for now (created as "MycoCanvas").
        /// </summary>
        public static Canvas EnsureCanvas()
        {
            if (_cachedCanvas != null)
            {
                return _cachedCanvas;
            }

            var existingMycoCanvasGameObject = GameObject.Find("MycoCanvas");
            if (existingMycoCanvasGameObject != null)
            {
                var existingMycoCanvas = existingMycoCanvasGameObject.GetComponent<Canvas>();
                if (existingMycoCanvas != null)
                {
                    _cachedCanvas = existingMycoCanvas;
                    return _cachedCanvas;
                }
            }

            var mycoCanvasGameObject = new GameObject("MycoCanvas");
            var mycoCanvasComponent = mycoCanvasGameObject.AddComponent<Canvas>();
            mycoCanvasComponent.renderMode = RenderMode.ScreenSpaceOverlay;
            mycoCanvasComponent.sortingOrder = 0;

            mycoCanvasGameObject.AddComponent<CanvasScaler>();
            mycoCanvasGameObject.AddComponent<GraphicRaycaster>();

            _cachedCanvas = mycoCanvasComponent;
            return _cachedCanvas;
        }

        public static RectTransform EnsureRectTransform(GameObject targetGameObject)
        {
            if (targetGameObject == null) return null;
            var rectTransform = targetGameObject.GetComponent<RectTransform>();
            if (rectTransform != null) return rectTransform;
            return targetGameObject.AddComponent<RectTransform>();
        }

        public static void ApplyUITransform(RectTransform rectTransform, string anchor, Vector2 anchoredPosition, Vector2 requestedSizeDelta, Vector2? fallbackSizeDelta = null)
        {
            if (rectTransform == null) return;

            ApplyAnchor(rectTransform, anchor);
            rectTransform.anchoredPosition = anchoredPosition;

            // If size is [0,0] (common "auto" convention), use fallback.
            if (Mathf.Approximately(requestedSizeDelta.x, 0) && Mathf.Approximately(requestedSizeDelta.y, 0))
            {
                rectTransform.sizeDelta = fallbackSizeDelta ?? rectTransform.sizeDelta;
            }
            else
            {
                rectTransform.sizeDelta = requestedSizeDelta;
            }
        }

        public static TextMeshProUGUI EnsureText(GameObject targetGameObject)
        {
            if (targetGameObject == null) return null;
            var textMeshProText = targetGameObject.GetComponent<TextMeshProUGUI>();
            if (textMeshProText == null)
            {
                textMeshProText = targetGameObject.AddComponent<TextMeshProUGUI>();
            }

            // Reasonable defaults.
            textMeshProText.alignment = TextAlignmentOptions.Center;
            textMeshProText.raycastTarget = false;
            return textMeshProText;
        }

        public static Image EnsureImage(GameObject targetGameObject)
        {
            if (targetGameObject == null) return null;
            var imageComponent = targetGameObject.GetComponent<Image>();
            if (imageComponent == null)
            {
                imageComponent = targetGameObject.AddComponent<Image>();
            }
            return imageComponent;
        }

        private static void ApplyAnchor(RectTransform rectTransform, string anchor)
        {
            anchor = (anchor ?? string.Empty).Trim().ToLowerInvariant();

            // Default: middle center.
            var anchorMin = new Vector2(0.5f, 0.5f);
            var anchorMax = new Vector2(0.5f, 0.5f);
            var pivot = new Vector2(0.5f, 0.5f);

            switch (anchor)
            {
                case "top_left":
                    anchorMin = anchorMax = new Vector2(0, 1);
                    pivot = new Vector2(0, 1);
                    break;
                case "top_center":
                    anchorMin = anchorMax = new Vector2(0.5f, 1);
                    pivot = new Vector2(0.5f, 1);
                    break;
                case "top_right":
                    anchorMin = anchorMax = new Vector2(1, 1);
                    pivot = new Vector2(1, 1);
                    break;
                case "middle_left":
                    anchorMin = anchorMax = new Vector2(0, 0.5f);
                    pivot = new Vector2(0, 0.5f);
                    break;
                case "middle_center":
                    anchorMin = anchorMax = new Vector2(0.5f, 0.5f);
                    pivot = new Vector2(0.5f, 0.5f);
                    break;
                case "middle_right":
                    anchorMin = anchorMax = new Vector2(1, 0.5f);
                    pivot = new Vector2(1, 0.5f);
                    break;
                case "bottom_left":
                    anchorMin = anchorMax = new Vector2(0, 0);
                    pivot = new Vector2(0, 0);
                    break;
                case "bottom_center":
                    anchorMin = anchorMax = new Vector2(0.5f, 0);
                    pivot = new Vector2(0.5f, 0);
                    break;
                case "bottom_right":
                    anchorMin = anchorMax = new Vector2(1, 0);
                    pivot = new Vector2(1, 0);
                    break;
            }

            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.pivot = pivot;
        }

        private static Vector2 ToVector2(float[] array, Vector2? fallback = null)
        {
            if (array == null || array.Length < 2)
            {
                return fallback ?? Vector2.zero;
            }

            return new Vector2(array[0], array[1]);
        }


        public static Color ParseColor(string htmlColorString, Color fallbackColor)
        {
            if (string.IsNullOrWhiteSpace(htmlColorString)) return fallbackColor;

            htmlColorString = htmlColorString.Trim();
            if (!htmlColorString.StartsWith("#", StringComparison.Ordinal))
            {
                htmlColorString = "#" + htmlColorString;
            }

            if (ColorUtility.TryParseHtmlString(htmlColorString, out var parsedColor))
            {
                return parsedColor;
            }

            return fallbackColor;
        }
    }
}
