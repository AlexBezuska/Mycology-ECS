using System;
using System.Collections.Generic;
using Mycology_ECS.Utils;
using Mycology_ECS.adaptors_for_engines.unity.core_systems;
using Mycology_ECS.translation_layers.unity.entity_helpers;
using UnityEngine;

namespace Mycology_ECS.translation_layers.unity
{
    internal sealed class MycoEntityComponentApplier
    {
        private readonly MycoComponentCatalog _componentCatalog;

        public MycoEntityComponentApplier(MycoComponentCatalog componentCatalog)
        {
            _componentCatalog = componentCatalog;
        }

        public void AnalyzeUiFromComponents(EntityDef entityDefinition, out bool isUi, out int uiOrder)
        {
            isUi = false;
            uiOrder = 0;

            if (entityDefinition == null) return;

            if (!string.IsNullOrWhiteSpace(entityDefinition.type) &&
                (string.Equals(entityDefinition.type, "UI_text", StringComparison.OrdinalIgnoreCase) || string.Equals(entityDefinition.type, "UI_image", StringComparison.OrdinalIgnoreCase)))
            {
                isUi = true;
                uiOrder = 0;
            }

            if (entityDefinition.components == null || entityDefinition.components.Length == 0) return;

            for (var componentIndex = 0; componentIndex < entityDefinition.components.Length; componentIndex++)
            {
                var componentId = entityDefinition.components[componentIndex];
                if (string.IsNullOrWhiteSpace(componentId)) continue;

                if (_componentCatalog == null || !_componentCatalog.TryGet(componentId, out var componentDefinition) || componentDefinition == null) continue;

                var componentType = TypeConversionUtils.GetString(componentDefinition, "type") ?? string.Empty;
                if (!string.Equals(componentType, "RenderLayer", StringComparison.Ordinal)) continue;

                var renderLayerName = TypeConversionUtils.GetString(componentDefinition, "layer") ?? string.Empty;
                if (string.Equals(renderLayerName, "ui", StringComparison.OrdinalIgnoreCase))
                {
                    isUi = true;
                    uiOrder = TypeConversionUtils.GetInt(componentDefinition, "order", 0);
                    return;
                }
            }
        }

        public void ApplyComponentsToGameObject(EntityDef entityDefinition, GameObject entityGameObject, ref Canvas mycoCanvas)
        {
            if (entityDefinition == null || entityGameObject == null) return;
            if (entityDefinition.components == null || entityDefinition.components.Length == 0) return;

            var hasTransform = false;
            var transformPos = Vector2.zero;
            var transformRot = 0f;

            var hasHealth = false;
            var healthMax = 0f;
            var healthCurrent = 0f;

            var hasAi = false;
            string aiBehavior = null;

            var hasInput = false;
            string inputScheme = null;

            var hasTag = false;
            string tagValue = null;

            string primitive = null;
            string materialKind = null;
            string materialColor = null;

            string uiAnchor = null;
            var uiAnchoredPos = Vector2.zero;
            var uiSize = Vector2.zero;
            var hasUiTransform = false;

            string textValue = null;
            float fontSize = 36;
            float letterSpacing = 0;
            Color textColor = Color.white;
            var hasText = false;

            var hasRenderLayerUi = false;
            var renderLayerOrder = 0;

            for (var componentIndex = 0; componentIndex < entityDefinition.components.Length; componentIndex++)
            {
                var componentId = entityDefinition.components[componentIndex];
                if (string.IsNullOrWhiteSpace(componentId)) continue;

                if (_componentCatalog == null || !_componentCatalog.TryGet(componentId, out var componentDefinition) || componentDefinition == null)
                {
                    Debug.LogWarning($"[Myco] Unknown component id '{componentId}' on entity '{entityDefinition.id}'.");
                    continue;
                }

                var componentType = TypeConversionUtils.GetString(componentDefinition, "type") ?? string.Empty;
                switch (componentType)
                {
                    case "Tag":
                        tagValue = TypeConversionUtils.GetString(componentDefinition, "value") ?? tagValue;
                        hasTag = !string.IsNullOrWhiteSpace(tagValue);
                        break;

                    case "RenderLayer":
                        {
                            var renderLayerName = TypeConversionUtils.GetString(componentDefinition, "layer") ?? string.Empty;
                            if (string.Equals(renderLayerName, "ui", StringComparison.OrdinalIgnoreCase))
                            {
                                hasRenderLayerUi = true;
                                renderLayerOrder = TypeConversionUtils.GetInt(componentDefinition, "order", renderLayerOrder);
                            }
                            break;
                        }

                    case "Transform":
                        transformPos = TypeConversionUtils.GetVector2(componentDefinition, "position", transformPos);
                        transformRot = TypeConversionUtils.GetFloat(componentDefinition, "rotation", transformRot);
                        hasTransform = true;
                        break;

                    case "Health":
                        healthMax = TypeConversionUtils.GetFloat(componentDefinition, "max", healthMax);
                        healthCurrent = TypeConversionUtils.GetFloat(componentDefinition, "current", healthCurrent);
                        hasHealth = true;
                        break;

                    case "AI":
                        aiBehavior = TypeConversionUtils.GetString(componentDefinition, "behavior") ?? aiBehavior;
                        hasAi = true;
                        break;

                    case "Input":
                        inputScheme = TypeConversionUtils.GetString(componentDefinition, "scheme") ?? inputScheme;
                        hasInput = true;
                        break;

                    case "UITransform":
                        uiAnchor = TypeConversionUtils.GetString(componentDefinition, "anchor") ?? uiAnchor;
                        uiAnchoredPos = TypeConversionUtils.GetVector2(componentDefinition, "anchored_position", uiAnchoredPos);
                        uiSize = TypeConversionUtils.GetVector2(componentDefinition, "size", uiSize);
                        hasUiTransform = true;
                        break;

                    case "TextContent":
                        textValue = TypeConversionUtils.GetString(componentDefinition, "value") ?? textValue;
                        hasText = true;
                        break;

                    case "TextStyle":
                        fontSize = TypeConversionUtils.GetFloat(componentDefinition, "font_size", fontSize);
                        letterSpacing = TypeConversionUtils.GetFloat(componentDefinition, "letter_spacing", letterSpacing);
                        hasText = true;
                        break;

                    case "TextColor":
                        textColor = UI.ParseColor(TypeConversionUtils.GetString(componentDefinition, "value"), fallbackColor: textColor);
                        hasText = true;
                        break;
                }

                if (string.IsNullOrEmpty(componentType))
                {
                    if (componentDefinition.TryGetValue("primitive", out var primitiveObj) && primitiveObj != null)
                    {
                        primitive = primitiveObj.ToString();
                    }

                    if (componentDefinition.TryGetValue("material", out var materialObj) && materialObj != null)
                    {
                        materialKind = materialObj.ToString();
                    }

                    if (componentDefinition.TryGetValue("color", out var colorObj) && colorObj != null)
                    {
                        materialColor = colorObj.ToString();
                    }
                }
            }

            if (hasTransform)
            {
                var unityPos = new Vector3(transformPos.x, transformPos.y, 0f);
                entityGameObject.transform.localPosition = unityPos;
                entityGameObject.transform.localRotation = Quaternion.Euler(0f, 0f, transformRot);

                var transformComponent = entityGameObject.GetComponent<TransformComponent>();
                if (transformComponent == null) transformComponent = entityGameObject.AddComponent<TransformComponent>();
                transformComponent.position = unityPos;
                transformComponent.rotation = transformRot;
            }

            if (hasHealth)
            {
                var healthComponent = entityGameObject.GetComponent<HealthComponent>();
                if (healthComponent == null) healthComponent = entityGameObject.AddComponent<HealthComponent>();
                healthComponent.max = healthMax;
                healthComponent.current = healthCurrent;
            }

            if (hasAi)
            {
                var aiComponent = entityGameObject.GetComponent<AIComponent>();
                if (aiComponent == null) aiComponent = entityGameObject.AddComponent<AIComponent>();
                aiComponent.behavior = aiBehavior;
            }

            if (hasInput)
            {
                var inputComponent = entityGameObject.GetComponent<InputComponent>();
                if (inputComponent == null) inputComponent = entityGameObject.AddComponent<InputComponent>();
                inputComponent.scheme = inputScheme;
            }

            if (hasTag)
            {
                UnityTagUtility.EnsureProjectTagsExist(new[] { tagValue }, logAdded: false);
                try
                {
                    entityGameObject.tag = tagValue;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Myco] Failed to assign Unity tag '{tagValue}' to '{entityGameObject.name}': {e.Message}");
                }
            }

            if (!string.IsNullOrWhiteSpace(primitive))
            {
                EnsurePrimitive(entityGameObject, primitive);
            }

            if (!string.IsNullOrWhiteSpace(materialKind) || !string.IsNullOrWhiteSpace(materialColor))
            {
                ApplyBasicMaterial(entityGameObject, materialColor);
            }

            if (hasRenderLayerUi || hasUiTransform || hasText)
            {
                if (!hasRenderLayerUi)
                {
                    Debug.LogWarning($"[Myco] Entity '{entityDefinition.id}' has UI components but no RenderLayer(ui). Treating it as UI.");
                }

                mycoCanvas = mycoCanvas != null ? mycoCanvas : UI.EnsureCanvas();
                entityGameObject.transform.SetParent(mycoCanvas.transform, false);

                if (hasUiTransform)
                {
                    var rectTransform = UI.EnsureRectTransform(entityGameObject);
                    UI.ApplyUITransform(rectTransform, uiAnchor, uiAnchoredPos, uiSize, fallbackSizeDelta: new Vector2(600, 120));
                }

                if (hasText)
                {
                    var rectTransform = UI.EnsureRectTransform(entityGameObject);
                    if (!hasUiTransform)
                    {
                        UI.ApplyUITransform(rectTransform, "middle_center", Vector2.zero, Vector2.zero, fallbackSizeDelta: new Vector2(600, 120));
                    }

                    var textMeshProText = UI.EnsureText(entityGameObject);
                    textMeshProText.text = textValue ?? textMeshProText.text ?? string.Empty;
                    textMeshProText.fontSize = fontSize > 0 ? fontSize : 36;
                    textMeshProText.color = textColor;
                    textMeshProText.characterSpacing = letterSpacing;
                }

                if (hasRenderLayerUi)
                {
                    var uiParentTransform = entityGameObject.transform.parent;
                    if (uiParentTransform != null)
                    {
                        var maxSiblingIndex = Mathf.Max(0, uiParentTransform.childCount - 1);
                        entityGameObject.transform.SetSiblingIndex(Mathf.Clamp(renderLayerOrder, 0, maxSiblingIndex));
                    }
                }
            }
        }

        private static void EnsurePrimitive(GameObject entityGameObject, string primitive)
        {
            if (entityGameObject == null) return;
            if (string.IsNullOrWhiteSpace(primitive)) return;

            var primitiveType = PrimitiveType.Cube;
            var p = primitive.Trim().ToLowerInvariant();
            switch (p)
            {
                case "sphere": primitiveType = PrimitiveType.Sphere; break;
                case "capsule": primitiveType = PrimitiveType.Capsule; break;
                case "cylinder": primitiveType = PrimitiveType.Cylinder; break;
                case "plane": primitiveType = PrimitiveType.Plane; break;
                case "quad": primitiveType = PrimitiveType.Quad; break;
                case "cube":
                default:
                    primitiveType = PrimitiveType.Cube;
                    break;
            }

            var existing = entityGameObject.transform.Find("Model");
            if (existing != null) return;

            var model = GameObject.CreatePrimitive(primitiveType);
            model.name = "Model";
            model.transform.SetParent(entityGameObject.transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;
        }

        private static void ApplyBasicMaterial(GameObject entityGameObject, string color)
        {
            if (entityGameObject == null) return;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) return;

            var mat = new Material(shader);
            mat.color = ParseNamedColor(color, mat.color);

            var renderers = entityGameObject.GetComponentsInChildren<Renderer>(includeInactive: true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null) continue;
                r.material = mat;
            }
        }

        private static Color ParseNamedColor(string color, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(color)) return fallback;

            var c = color.Trim();
            if (c.StartsWith("#", StringComparison.Ordinal))
            {
                if (ColorUtility.TryParseHtmlString(c, out var parsed)) return parsed;
                return fallback;
            }

            switch (c.ToLowerInvariant())
            {
                case "red": return Color.red;
                case "purple": return new Color(0.6f, 0.2f, 0.8f, 1f);
                case "magenta": return Color.magenta;
                case "white": return Color.white;
                case "black": return Color.black;
                case "blue": return Color.blue;
                case "green": return Color.green;
                case "yellow": return Color.yellow;
            }

            if (ColorUtility.TryParseHtmlString("#" + c, out var parsedHex)) return parsedHex;
            return fallback;
        }

        public void ApplyLegacyUi(EntityDef entityDefinition, GameObject entityGameObject, ref Canvas mycoCanvas)
        {
            if (entityDefinition == null || entityGameObject == null) return;
            if (string.IsNullOrWhiteSpace(entityDefinition.type)) return;

            if (string.Equals(entityDefinition.type, "UI_text", StringComparison.OrdinalIgnoreCase))
            {
                mycoCanvas = mycoCanvas != null ? mycoCanvas : UI.EnsureCanvas();
                entityGameObject.transform.SetParent(mycoCanvas.transform, false);

                var rectTransform = UI.EnsureRectTransform(entityGameObject);
                var anchoredPos = entityDefinition.UI_text != null ? new Vector2(entityDefinition.UI_text.anchored_position?[0] ?? 0, entityDefinition.UI_text.anchored_position?[1] ?? 0) : Vector2.zero;
                UI.ApplyUITransform(rectTransform, entityDefinition.UI_text?.anchor, anchoredPos, Vector2.zero, fallbackSizeDelta: new Vector2(600, 120));

                var textMeshProText = UI.EnsureText(entityGameObject);
                textMeshProText.text = entityDefinition.UI_text?.text ?? textMeshProText.text ?? string.Empty;
                textMeshProText.fontSize = entityDefinition.UI_text != null && entityDefinition.UI_text.font_size > 0 ? entityDefinition.UI_text.font_size : 36;
                textMeshProText.color = UI.ParseColor(entityDefinition.UI_text?.color, fallbackColor: Color.white);
                return;
            }

            if (string.Equals(entityDefinition.type, "UI_image", StringComparison.OrdinalIgnoreCase))
            {
                mycoCanvas = mycoCanvas != null ? mycoCanvas : UI.EnsureCanvas();
                entityGameObject.transform.SetParent(mycoCanvas.transform, false);

                var rectTransform = UI.EnsureRectTransform(entityGameObject);
                var anchoredPos = entityDefinition.UI_image != null ? new Vector2(entityDefinition.UI_image.anchored_position?[0] ?? 0, entityDefinition.UI_image.anchored_position?[1] ?? 0) : Vector2.zero;
                var size = entityDefinition.UI_image != null ? new Vector2(entityDefinition.UI_image.size?[0] ?? 128, entityDefinition.UI_image.size?[1] ?? 128) : new Vector2(128, 128);
                UI.ApplyUITransform(rectTransform, entityDefinition.UI_image?.anchor, anchoredPos, size, fallbackSizeDelta: new Vector2(128, 128));

                var imageComponent = UI.EnsureImage(entityGameObject);
                imageComponent.color = UI.ParseColor(entityDefinition.UI_image?.color, fallbackColor: Color.white);
                return;
            }
        }
    }
}
