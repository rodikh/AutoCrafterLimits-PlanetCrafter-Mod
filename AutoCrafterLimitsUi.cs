using System;
using System.Collections.Generic;
using HarmonyLib;
using SpaceCraft;
using UnityEngine;
using UnityEngine.UI;

namespace AutoCrafterLimits
{
    internal sealed class AutoCrafterLimitsUi : MonoBehaviour
    {
        private readonly Dictionary<int, string> _numberInputBuffers = new Dictionary<int, string>();
        private readonly Dictionary<string, string> _thresholdInputBuffers = new Dictionary<string, string>();
        private readonly Dictionary<int, UiWidgets> _widgetsByWindowId = new Dictionary<int, UiWidgets>();

        private bool _showConfigWindow;
        private Rect _windowRect = new Rect(90f, 120f, 420f, 420f);
        private UiWindowGroupSelector _currentWindow;
        private MachineAutoCrafter _currentCrafter;
        private GUIStyle _blockedMessageStyle;

        private static readonly FieldInfoWrapper<UiWindowGroupSelector, MachineAutoCrafter> AutoCrafterField =
            new FieldInfoWrapper<UiWindowGroupSelector, MachineAutoCrafter>("_autoCrafter");

        public void AttachWindow(UiWindowGroupSelector window)
        {
            if (window == null)
            {
                return;
            }

            int key = window.GetInstanceID();
            if (_widgetsByWindowId.ContainsKey(key))
            {
                _currentWindow = window;
                _currentCrafter = AutoCrafterField.Get(window);
                return;
            }

            UiWidgets widgets = CreateWidgets(window);
            _widgetsByWindowId[key] = widgets;
            _currentWindow = window;
            _currentCrafter = AutoCrafterField.Get(window);
        }

        public void OnWindowClose(UiWindowGroupSelector window)
        {
            if (_currentWindow == window)
            {
                _showConfigWindow = false;
                _currentCrafter = null;
                _currentWindow = null;
            }
        }

        public void UpdateWindowStatus(UiWindowGroupSelector window)
        {
            if (window == null)
            {
                return;
            }

            int key = window.GetInstanceID();
            if (!_widgetsByWindowId.TryGetValue(key, out UiWidgets widgets))
            {
                return;
            }

            MachineAutoCrafter crafter = AutoCrafterField.Get(window);
            if (crafter == null)
            {
                return;
            }

            TryPositionButtonNearSelectedRecipeImage(window, widgets.ButtonRect);
        }

        private void OnGUI()
        {
            if (_showConfigWindow && _currentCrafter != null)
            {
                _windowRect = GUILayout.Window(712345, _windowRect, DrawWindow, "AutoCrafter Limits");
            }

            if (_currentCrafter == null || _currentWindow == null)
            {
                return;
            }

            DrawBlockedMessage();
        }

        private void DrawWindow(int windowId)
        {
            if (!ModRuntime.TryGetAutoCrafterData(_currentCrafter, out int worldObjectId, out Group outputGroup) || outputGroup == null)
            {
                GUILayout.Label("No output recipe selected.");
                GUI.DragWindow();
                return;
            }

            AutoCrafterLimitConfig config = ModRuntime.Store.GetOrCreate(worldObjectId);
            Dictionary<string, int> countSnapshot = ModRuntime.GetCurrentCounts(_currentCrafter);
            int outputCurrent = ModRuntime.GetCountFromSnapshot(countSnapshot, outputGroup.GetId());
            GUILayout.Space(6f);

            bool outputEnabled = GUILayout.Toggle(config.EnableOutputLimit, "Enable Output Limit");
            if (outputEnabled != config.EnableOutputLimit)
            {
                config.EnableOutputLimit = outputEnabled;
                ModRuntime.Store.Save();
            }

            if (config.EnableOutputLimit)
            {
                DrawNumberField(
                    "Target Output Amount (0 = unlimited) | Current: " + outputCurrent,
                    worldObjectId,
                    config.TargetOutputAmount,
                    delegate(int value)
                    {
                        config.TargetOutputAmount = Mathf.Max(0, value);
                        ModRuntime.Store.Save();
                    });
            }

            GUILayout.Space(10f);

            bool inputEnabled = GUILayout.Toggle(config.EnableInputThreshold, "Enable Input Thresholds");
            if (inputEnabled != config.EnableInputThreshold)
            {
                config.EnableInputThreshold = inputEnabled;
                ModRuntime.Store.Save();
            }

            List<Group> ingredients = BuildUniqueIngredientKinds(outputGroup.GetRecipe().GetIngredientsGroupInRecipe());
            bool changed = config.AdaptToRecipe(ingredients);
            if (changed)
            {
                ModRuntime.Store.Save();
            }

            if (config.EnableInputThreshold)
            {
                GUILayout.Label("Recipe Ingredients (0 = no threshold):");
                for (int i = 0; i < ingredients.Count; i++)
                {
                    Group ingredient = ingredients[i];
                    int current = ModRuntime.GetCountFromSnapshot(countSnapshot, ingredient.GetId());
                    DrawThresholdField(config, ingredient, current);
                }
            }

            GUILayout.Space(10f);
            if (GUILayout.Button("Close"))
            {
                _showConfigWindow = false;
            }

            GUI.DragWindow(new Rect(0f, 0f, 10000f, 25f));
        }

        private void DrawNumberField(string label, int key, int currentValue, Action<int> onApply)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(220f));

            if (!_numberInputBuffers.TryGetValue(key, out string text))
            {
                text = currentValue.ToString();
            }

            string updated = GUILayout.TextField(text, GUILayout.Width(70f));
            _numberInputBuffers[key] = updated;

            if (GUILayout.Button("Set", GUILayout.Width(50f)))
            {
                if (int.TryParse(updated, out int parsed))
                {
                    onApply(parsed);
                    _numberInputBuffers[key] = parsed.ToString();
                }
            }

            GUILayout.EndHorizontal();
        }

        private void DrawThresholdField(AutoCrafterLimitConfig config, Group ingredient, int currentAmount)
        {
            string ingredientId = ingredient.GetId();
            string fieldKey = config.OwnerId + "::" + ingredientId;
            int currentThreshold = config.GetThreshold(ingredientId);

            GUILayout.BeginHorizontal();
            GUILayout.Label(ingredientId + " (Current: " + currentAmount + ")", GUILayout.Width(220f));

            if (!_thresholdInputBuffers.TryGetValue(fieldKey, out string text))
            {
                text = currentThreshold.ToString();
            }

            string updated = GUILayout.TextField(text, GUILayout.Width(70f));
            _thresholdInputBuffers[fieldKey] = updated;

            if (GUILayout.Button("Set", GUILayout.Width(50f)))
            {
                if (int.TryParse(updated, out int parsed))
                {
                    config.SetThreshold(ingredientId, Mathf.Max(0, parsed));
                    _thresholdInputBuffers[fieldKey] = Mathf.Max(0, parsed).ToString();
                    ModRuntime.Store.Save();
                }
            }

            GUILayout.EndHorizontal();
        }

        private static List<Group> BuildUniqueIngredientKinds(List<Group> ingredients)
        {
            List<Group> unique = new List<Group>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < ingredients.Count; i++)
            {
                Group ingredient = ingredients[i];
                if (ingredient == null)
                {
                    continue;
                }

                string id = ingredient.GetId();
                if (string.IsNullOrEmpty(id) || seen.Contains(id))
                {
                    continue;
                }

                seen.Add(id);
                unique.Add(ingredient);
            }

            return unique;
        }

        private void DrawBlockedMessage()
        {
            string reason = ModRuntime.GetBlockReason(_currentCrafter);
            if (string.IsNullOrEmpty(reason))
            {
                return;
            }

            if (_blockedMessageStyle == null)
            {
                _blockedMessageStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 16,
                    wordWrap = true
                };
                _blockedMessageStyle.normal.textColor = new Color(1f, 0.55f, 0.35f, 1f);
            }

            float messageWidth = Mathf.Min(900f, Screen.width * 0.85f);
            float x = (Screen.width - messageWidth) * 0.5f;
            float y = Screen.height * 0.78f;
            GUI.Label(new Rect(x, y, messageWidth, 48f), reason, _blockedMessageStyle);
        }

        private UiWidgets CreateWidgets(UiWindowGroupSelector window)
        {
            UiWidgets widgets = new UiWidgets();

            GameObject buttonGo = new GameObject("AutoCrafterLimitsButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            buttonGo.transform.SetParent(window.textAutoCrafterContainer.transform, false);

            RectTransform buttonRect = buttonGo.GetComponent<RectTransform>();
            buttonRect.sizeDelta = new Vector2(120f, 30f);

            Image buttonImage = buttonGo.GetComponent<Image>();
            buttonImage.color = new Color(0.14f, 0.24f, 0.36f, 0.95f);

            Button button = buttonGo.GetComponent<Button>();
            button.onClick.AddListener(delegate
            {
                _currentWindow = window;
                _currentCrafter = AutoCrafterField.Get(window);
                _showConfigWindow = !_showConfigWindow;
            });

            GameObject buttonTextGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            buttonTextGo.transform.SetParent(buttonGo.transform, false);
            RectTransform textRect = buttonTextGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            Text buttonText = buttonTextGo.GetComponent<Text>();
            buttonText.text = "Limits";
            buttonText.alignment = TextAnchor.MiddleCenter;
            buttonText.color = Color.white;
            buttonText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            buttonText.resizeTextForBestFit = true;

            widgets.Button = button;
            widgets.ButtonRect = buttonRect;
            TryPositionButtonNearSelectedRecipeImage(window, buttonRect);
            return widgets;
        }

        private static void TryPositionButtonNearSelectedRecipeImage(UiWindowGroupSelector window, RectTransform buttonRect)
        {
            if (window == null || buttonRect == null || window.loadingAutoCrafterImage == null)
            {
                return;
            }

            RectTransform parentRect = buttonRect.parent as RectTransform;
            RectTransform imageRect = window.loadingAutoCrafterImage.rectTransform;
            if (parentRect == null || imageRect == null)
            {
                return;
            }

            Vector3[] corners = new Vector3[4];
            imageRect.GetWorldCorners(corners);
            for (int i = 0; i < corners.Length; i++)
            {
                corners[i] = parentRect.InverseTransformPoint(corners[i]);
            }

            float imageRight = Mathf.Max(corners[2].x, corners[3].x);
            float imageTop = Mathf.Max(corners[1].y, corners[2].y);

            buttonRect.anchorMin = new Vector2(0f, 1f);
            buttonRect.anchorMax = new Vector2(0f, 1f);
            buttonRect.pivot = new Vector2(0f, 1f);
            buttonRect.sizeDelta = new Vector2(120f, 30f);
            float anchorX = imageRight - parentRect.rect.xMin + 8f;
            float anchorY = imageTop - parentRect.rect.yMax + 2f;
            buttonRect.anchoredPosition = new Vector2(anchorX, anchorY);
        }

        private sealed class UiWidgets
        {
            public Button Button;
            public RectTransform ButtonRect;
        }
    }
}
