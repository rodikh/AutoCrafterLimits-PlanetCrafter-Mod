using System;
using System.Collections.Generic;
using HarmonyLib;
using SpaceCraft;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AutoCrafterLimits
{
    internal sealed class AutoCrafterLimitsUi : MonoBehaviour
    {
        private readonly Dictionary<int, string> _numberInputBuffers = new Dictionary<int, string>();
        private readonly Dictionary<string, string> _thresholdInputBuffers = new Dictionary<string, string>();
        private readonly Dictionary<int, UiWidgets> _widgetsByWindowId = new Dictionary<int, UiWidgets>();

        private const float FontScale = 1.5f;
        private const float IconScale = 2f;

        private bool _showConfigWindow;
        private Rect _windowRect = new Rect(100f, 100f, 840f, 400f);
        private Vector2 _scrollPosition;
        private GUIStyle _blockedMessageStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _multiLineLabelStyle;
        private GUIStyle _toggleStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _textFieldStyle;
        private GUIStyle _windowStyle;
        private GUIStyle _scrollViewStyle;
        private Texture2D _windowBgTexture;
        private Texture2D _checkboxOffTexture;
        private Texture2D _checkboxOnTexture;
        private UiWindowGroupSelector _currentWindow;
        private MachineAutoCrafter _currentCrafter;
        private GameObject _modalBlocker;
        private RectTransform _modalBlockerRect;

        private static readonly FieldInfoWrapper<UiWindowGroupSelector, MachineAutoCrafter> AutoCrafterField =
            new FieldInfoWrapper<UiWindowGroupSelector, MachineAutoCrafter>("_autoCrafter");

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            InvalidateModalStyles();
        }

        private void InvalidateModalStyles()
        {
            _labelStyle = null;
            _multiLineLabelStyle = null;
            _toggleStyle = null;
            _buttonStyle = null;
            _textFieldStyle = null;
            _windowStyle = null;
            _scrollViewStyle = null;
            _blockedMessageStyle = null;
            _windowBgTexture = null;
            _checkboxOffTexture = null;
            _checkboxOnTexture = null;
        }

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
                SetModalBlockerActive(false);
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
                EnsureModalStyles();
                _windowRect = GUILayout.Window(712345, _windowRect, DrawWindow, "AutoCrafter Limits", _windowStyle,
                    GUILayout.MinHeight(120f), GUILayout.MaxHeight(Screen.height - 40f));
                SetModalBlockerActive(true);
            }
            else
            {
                SetModalBlockerActive(false);
            }

            if (_currentCrafter != null && _currentWindow != null)
            {
                DrawBlockedMessage();
            }
        }

        private void SetModalBlockerActive(bool active)
        {
            if (active && _modalBlocker == null)
            {
                _modalBlocker = CreateModalBlocker();
            }
            if (_modalBlocker != null)
            {
                _modalBlocker.SetActive(active);
                if (active && _modalBlockerRect != null)
                {
                    UpdateModalBlockerRect();
                }
            }
        }

        private void ShrinkWindowForRelayout()
        {
            _windowRect.height = Mathf.Min(_windowRect.height, 200f);
        }

        private void UpdateModalBlockerRect()
        {
            if (_modalBlockerRect == null)
            {
                return;
            }
            float w = Screen.width;
            float h = Screen.height;
            Rect r = _windowRect;
            _modalBlockerRect.anchorMin = new Vector2(r.xMin / w, (h - r.yMax) / h);
            _modalBlockerRect.anchorMax = new Vector2(r.xMax / w, (h - r.yMin) / h);
            _modalBlockerRect.offsetMin = Vector2.zero;
            _modalBlockerRect.offsetMax = Vector2.zero;
        }

        private GameObject CreateModalBlocker()
        {
            GameObject blocker = new GameObject("AutoCrafterLimitsModalBlocker");

            Canvas canvas = blocker.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32767;
            blocker.AddComponent<GraphicRaycaster>();

            GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.transform.SetParent(blocker.transform, false);

            _modalBlockerRect = panel.GetComponent<RectTransform>();
            _modalBlockerRect.anchorMin = Vector2.zero;
            _modalBlockerRect.anchorMax = Vector2.one;
            _modalBlockerRect.offsetMin = Vector2.zero;
            _modalBlockerRect.offsetMax = Vector2.zero;

            Image image = panel.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.01f);
            image.raycastTarget = true;

            blocker.transform.SetParent(transform);
            return blocker;
        }

        private void DrawWindow(int windowId)
        {
            EnsureModalStyles();

            if (!ModRuntime.TryGetAutoCrafterData(_currentCrafter, out int worldObjectId, out Group outputGroup) || outputGroup == null)
            {
                GUILayout.Space(24f * FontScale);
                GUILayout.Label("No output recipe selected.", _labelStyle);
                GUILayout.Space(16f * FontScale);
                if (GUILayout.Button("Close", _buttonStyle))
                {
                    _showConfigWindow = false;
                }
                GUI.DragWindow(new Rect(0f, 0f, 10000f, 44f * FontScale));
                return;
            }

            AutoCrafterLimitConfig config = ModRuntime.Store.GetOrCreate(worldObjectId);
            if (config.ResetIfRecipeChanged(outputGroup.GetId()))
            {
                _numberInputBuffers.Remove(worldObjectId);
                ClearThresholdBuffersFor(config.OwnerId);
            }

            Dictionary<string, int> inRangeSnapshot = ModRuntime.GetCurrentCounts(_currentCrafter);
            Dictionary<string, int> outputCountSnapshot = config.OutputLimitCountsPlanetWide ? ModRuntime.GetCountsPlanetWide(outputGroup) : inRangeSnapshot;
            int outputCurrent = ModRuntime.GetCountFromSnapshot(outputCountSnapshot, outputGroup.GetId());

            GUILayout.Space(10f * FontScale);

            bool outputEnabled = DrawScaledToggle(config.EnableOutputLimit, "Craft until have X");
            if (outputEnabled != config.EnableOutputLimit)
            {
                config.EnableOutputLimit = outputEnabled;
                if (!outputEnabled)
                {
                    config.TargetOutputAmount = 0;
                    config.OutputLimitCountsPlanetWide = false;
                    _numberInputBuffers.Remove(worldObjectId);
                    ShrinkWindowForRelayout();
                }
            }

            if (config.EnableOutputLimit)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(20f * FontScale);
                GUILayout.BeginVertical();
                bool planetWide = DrawScaledToggle(config.OutputLimitCountsPlanetWide, "Count in containers planet-wide");
                if (planetWide != config.OutputLimitCountsPlanetWide)
                {
                    config.OutputLimitCountsPlanetWide = planetWide;
                }

                GUILayout.BeginHorizontal();
                GUILayout.Space(24f * FontScale);
                GUILayout.BeginVertical();
                string outputItemName = Readable.GetGroupName(outputGroup);
                DrawNumberField(
                    outputGroup,
                    outputItemName + " | In range: " + outputCurrent,
                    worldObjectId,
                    config.TargetOutputAmount,
                    value =>
                    {
                        config.TargetOutputAmount = Mathf.Max(0, value);
                    },
                    inputHint: "(0 = unlimited)");
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(12f * FontScale);

            bool inputEnabled = DrawScaledToggle(config.EnableInputThreshold, "When ingredients ≥ X");
            if (inputEnabled != config.EnableInputThreshold)
            {
                config.EnableInputThreshold = inputEnabled;
                if (!inputEnabled)
                {
                    config.InputThresholds.Clear();
                    config.InputThresholdCountsPlanetWide = false;
                    ClearThresholdBuffersFor(config.OwnerId);
                    ShrinkWindowForRelayout();
                }
            }

            List<Group> ingredients = BuildUniqueIngredientKinds(outputGroup.GetRecipe().GetIngredientsGroupInRecipe());
            config.AdaptToRecipe(ingredients);

            if (config.EnableInputThreshold)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(20f * FontScale);
                GUILayout.BeginVertical();
                bool inputPlanetWide = DrawScaledToggle(config.InputThresholdCountsPlanetWide, "Count in containers planet-wide");
                if (inputPlanetWide != config.InputThresholdCountsPlanetWide)
                {
                    config.InputThresholdCountsPlanetWide = inputPlanetWide;
                }

                GUILayout.BeginHorizontal();
                GUILayout.Space(24f * FontScale);
                GUILayout.BeginVertical();
                Dictionary<string, int> inputCountSnapshot = config.InputThresholdCountsPlanetWide ? ModRuntime.GetCountsPlanetWide(outputGroup) : inRangeSnapshot;
                GUILayout.Label("Recipe ingredients (0 = no threshold):", _labelStyle);
                _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, _scrollViewStyle, GUILayout.Height(220f * FontScale));
                for (int i = 0; i < ingredients.Count; i++)
                {
                    Group ingredient = ingredients[i];
                    int current = ModRuntime.GetCountFromSnapshot(inputCountSnapshot, ingredient.GetId());
                    DrawThresholdField(config, ingredient, current);
                }
                GUILayout.EndScrollView();
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(12f * FontScale);
            if (GUILayout.Button("Close", _buttonStyle))
            {
                _showConfigWindow = false;
            }

            GUI.DragWindow(new Rect(0f, 0f, 10000f, 44f * FontScale));
        }

        private static int IconSize => Mathf.RoundToInt(28f * IconScale);

        private static float ControlHeight => 30f * FontScale;

        private void DrawNumberField(Group outputGroup, string label, int key, int currentValue, Action<int> onApply, string inputHint = null)
        {
            float rowH = 36f * FontScale;
            GUILayout.BeginHorizontal(GUILayout.Height(rowH));

            GUILayout.BeginVertical(GUILayout.Width(IconSize), GUILayout.Height(rowH));
            GUILayout.FlexibleSpace();
            DrawGroupIcon(outputGroup, IconSize);
            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();

            GUILayout.Label(label, _multiLineLabelStyle, GUILayout.Width(300f * FontScale), GUILayout.Height(32f * FontScale));

            if (!string.IsNullOrEmpty(inputHint))
            {
                GUILayout.Label(inputHint, _labelStyle, GUILayout.Height(ControlHeight));
            }

            if (!_numberInputBuffers.TryGetValue(key, out string text))
            {
                text = currentValue.ToString();
            }
            string updated = GUILayout.TextField(text, _textFieldStyle, GUILayout.Width(90f * FontScale), GUILayout.Height(ControlHeight));
            _numberInputBuffers[key] = updated;

            if (GUILayout.Button("Set", _buttonStyle, GUILayout.Width(64f * FontScale), GUILayout.Height(ControlHeight)))
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
            string displayName = Readable.GetGroupName(ingredient);

            float rowH = 36f * FontScale;
            GUILayout.BeginHorizontal(GUILayout.Height(rowH));

            GUILayout.BeginVertical(GUILayout.Width(IconSize), GUILayout.Height(rowH));
            GUILayout.FlexibleSpace();
            DrawGroupIcon(ingredient, IconSize);
            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();

            GUILayout.Label(displayName + " (In range: " + currentAmount + ")", _multiLineLabelStyle, GUILayout.Width(300f * FontScale), GUILayout.Height(32f * FontScale));

            GUILayout.FlexibleSpace();

            if (!_thresholdInputBuffers.TryGetValue(fieldKey, out string text))
            {
                text = currentThreshold.ToString();
            }
            string updated = GUILayout.TextField(text, _textFieldStyle, GUILayout.Width(90f * FontScale), GUILayout.Height(ControlHeight));
            _thresholdInputBuffers[fieldKey] = updated;

            if (GUILayout.Button("Set", _buttonStyle, GUILayout.Width(64f * FontScale), GUILayout.Height(ControlHeight)))
            {
                if (int.TryParse(updated, out int parsed))
                {
                    config.SetThreshold(ingredientId, Mathf.Max(0, parsed));
                    _thresholdInputBuffers[fieldKey] = config.GetThreshold(ingredientId).ToString();
                }
            }
            GUILayout.EndHorizontal();
        }

        private static void DrawGroupIcon(Group group, int size)
        {
            if (group == null)
            {
                GUILayout.Space(size);
                return;
            }

            Sprite sprite = group.GetImage();
            if (sprite != null && sprite.texture != null)
            {
                Rect iconRect = GUILayoutUtility.GetRect(size, size, GUILayout.Width(size), GUILayout.Height(size));
                float s = Mathf.Min(iconRect.width, iconRect.height);
                Rect squareRect = new Rect(iconRect.x + (iconRect.width - s) * 0.5f, iconRect.y + (iconRect.height - s) * 0.5f, s, s);
                Rect texCoords = new Rect(
                    sprite.rect.x / sprite.texture.width,
                    sprite.rect.y / sprite.texture.height,
                    sprite.rect.width / sprite.texture.width,
                    sprite.rect.height / sprite.texture.height);
                GUI.DrawTextureWithTexCoords(squareRect, sprite.texture, texCoords);
            }
            else
            {
                GUILayout.Space(size);
            }
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
                    alignment = TextAnchor.MiddleRight,
                    fontSize = Mathf.RoundToInt(22f * FontScale),
                    wordWrap = true
                };
                _blockedMessageStyle.normal.textColor = new Color(1f, 0.55f, 0.35f, 1f);
            }

            Rect messageRect = new Rect(Screen.width * 0.5f + 30f * FontScale, Screen.height * 0.78f, Mathf.Min(700f * FontScale, Screen.width * 0.5f), 60f * FontScale);
            GUI.Label(messageRect, reason, _blockedMessageStyle);
        }

        private void EnsureModalStyles()
        {
            if (_labelStyle != null)
            {
                return;
            }

            int fontSize = Mathf.RoundToInt(18f * FontScale);

            var inputFieldBg = new Color(0.22f, 0.25f, 0.32f, 1f);
            var inputFieldHoverBg = new Color(0.28f, 0.31f, 0.40f, 1f);
            var setButtonBg = new Color(0.28f, 0.38f, 0.52f, 1f);
            var setButtonHoverBg = new Color(0.34f, 0.46f, 0.62f, 1f);

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                fixedHeight = 32f * FontScale
            };
            _labelStyle.normal.textColor = Color.white;

            _multiLineLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                wordWrap = true,
                fixedHeight = 32f * FontScale
            };
            _multiLineLabelStyle.normal.textColor = Color.white;

            float controlHeight = 30f * FontScale;
            int toggleBoxSize = Mathf.RoundToInt(20f * FontScale);
            _checkboxOffTexture = MakeCheckboxTexture(toggleBoxSize, false);
            _checkboxOnTexture = MakeCheckboxTexture(toggleBoxSize, true);
            _toggleStyle = new GUIStyle(GUI.skin.toggle)
            {
                fontSize = fontSize,
                fixedHeight = controlHeight,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(toggleBoxSize + 8, 4, 4, 4),
                imagePosition = ImagePosition.ImageLeft,
                border = new RectOffset(0, 0, 0, 0),
                overflow = new RectOffset(0, 0, 0, 0)
            };
            var transparent = MakeSolidTexture(1, 1, new Color(0f, 0f, 0f, 0f));
            _toggleStyle.normal.background = transparent;
            _toggleStyle.active.background = transparent;
            _toggleStyle.hover.background = transparent;
            _toggleStyle.onNormal.background = transparent;
            _toggleStyle.onActive.background = transparent;
            _toggleStyle.onHover.background = transparent;
            _toggleStyle.normal.textColor = Color.white;
            _toggleStyle.active.textColor = Color.white;
            _toggleStyle.hover.textColor = Color.white;
            _toggleStyle.onNormal.textColor = Color.white;
            _toggleStyle.onActive.textColor = Color.white;
            _toggleStyle.onHover.textColor = Color.white;

            int pad = Mathf.RoundToInt(6f * FontScale);
            var setButtonActiveBg = new Color(0.22f, 0.32f, 0.48f, 1f);
            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = fontSize,
                fixedHeight = controlHeight,
                border = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset(pad, pad / 2, pad / 2, pad / 2),
                overflow = new RectOffset(0, 0, 0, 0)
            };
            _buttonStyle.normal.background = MakeSolidTexture(1, 1, setButtonBg);
            _buttonStyle.normal.textColor = Color.white;
            _buttonStyle.hover.background = MakeSolidTexture(1, 1, setButtonHoverBg);
            _buttonStyle.hover.textColor = Color.white;
            _buttonStyle.active.background = MakeSolidTexture(1, 1, setButtonActiveBg);
            _buttonStyle.active.textColor = Color.white;
            _buttonStyle.focused.background = MakeSolidTexture(1, 1, setButtonBg);
            _buttonStyle.focused.textColor = Color.white;
            _buttonStyle.onNormal.background = MakeSolidTexture(1, 1, setButtonBg);
            _buttonStyle.onNormal.textColor = Color.white;
            _buttonStyle.onHover.background = MakeSolidTexture(1, 1, setButtonHoverBg);
            _buttonStyle.onHover.textColor = Color.white;
            _buttonStyle.onActive.background = MakeSolidTexture(1, 1, setButtonActiveBg);
            _buttonStyle.onActive.textColor = Color.white;

            _textFieldStyle = new GUIStyle(GUI.skin.textField)
            {
                fontSize = fontSize,
                fixedHeight = controlHeight,
                padding = new RectOffset(pad, pad, pad / 2, pad / 2),
                border = new RectOffset(0, 0, 0, 0),
                overflow = new RectOffset(0, 0, 0, 0)
            };
            _textFieldStyle.normal.background = MakeSolidTexture(1, 1, inputFieldBg);
            _textFieldStyle.normal.textColor = Color.white;
            _textFieldStyle.hover.background = MakeSolidTexture(1, 1, inputFieldHoverBg);
            _textFieldStyle.hover.textColor = Color.white;
            _textFieldStyle.focused.background = MakeSolidTexture(1, 1, inputFieldBg);
            _textFieldStyle.focused.textColor = Color.white;
            _textFieldStyle.active.background = MakeSolidTexture(1, 1, inputFieldBg);
            _textFieldStyle.active.textColor = Color.white;

            _windowBgTexture = MakeSolidTexture(1, 1, new Color(0.09f, 0.10f, 0.14f, 1f));

            _windowStyle = new GUIStyle(GUI.skin.window)
            {
                fontSize = Mathf.RoundToInt(20f * FontScale),
                fontStyle = FontStyle.Bold
            };
            _windowStyle.normal.background = _windowBgTexture;
            _windowStyle.normal.textColor = Color.white;
            _windowStyle.focused.background = _windowBgTexture;
            _windowStyle.focused.textColor = Color.white;
            _windowStyle.active.background = _windowBgTexture;
            _windowStyle.active.textColor = Color.white;
            _windowStyle.hover.background = _windowBgTexture;
            _windowStyle.hover.textColor = Color.white;
            _windowStyle.onNormal.background = _windowBgTexture;
            _windowStyle.onNormal.textColor = Color.white;
            _windowStyle.onFocused.background = _windowBgTexture;
            _windowStyle.onFocused.textColor = Color.white;
            _windowStyle.onActive.background = _windowBgTexture;
            _windowStyle.onActive.textColor = Color.white;
            _windowStyle.onHover.background = _windowBgTexture;
            _windowStyle.onHover.textColor = Color.white;
            int winPad = Mathf.RoundToInt(14f * FontScale);
            int winTitle = Mathf.RoundToInt(44f * FontScale);
            _windowStyle.padding = new RectOffset(winPad, winTitle, winPad + 2, winPad);
            _windowStyle.border = new RectOffset(8, 8, 8, 8);

            int scrollPad = Mathf.RoundToInt(6f * FontScale);
            _scrollViewStyle = new GUIStyle(GUI.skin.scrollView)
            {
                padding = new RectOffset(scrollPad, scrollPad, scrollPad, scrollPad)
            };
            _scrollViewStyle.normal.background = MakeSolidTexture(1, 1, new Color(0.06f, 0.07f, 0.10f, 1f));
        }

        private void ClearThresholdBuffersFor(int ownerId)
        {
            string prefix = ownerId + "::";
            var keysToRemove = new List<string>();
            foreach (string key in _thresholdInputBuffers.Keys)
            {
                if (key.StartsWith(prefix, StringComparison.Ordinal))
                {
                    keysToRemove.Add(key);
                }
            }
            for (int i = 0; i < keysToRemove.Count; i++)
            {
                _thresholdInputBuffers.Remove(keysToRemove[i]);
            }
        }

        private bool DrawScaledToggle(bool value, string label)
        {
            var content = new GUIContent(label, value ? _checkboxOnTexture : _checkboxOffTexture);
            if (GUILayout.Button(content, _toggleStyle))
            {
                return !value;
            }
            return value;
        }

        private static Texture2D MakeCheckboxTexture(int size, bool filled)
        {
            var tex = new Texture2D(size, size);
            var border = new Color(0.6f, 0.65f, 0.75f, 1f);
            var fill = new Color(0.35f, 0.5f, 0.7f, 1f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool edge = x == 0 || x == size - 1 || y == 0 || y == size - 1;
                    if (edge)
                    {
                        tex.SetPixel(x, y, border);
                    }
                    else if (filled)
                    {
                        tex.SetPixel(x, y, fill);
                    }
                    else
                    {
                        tex.SetPixel(x, y, new Color(0.15f, 0.16f, 0.2f, 1f));
                    }
                }
            }
            tex.Apply();
            return tex;
        }

        private static Texture2D MakeSolidTexture(int w, int h, Color color)
        {
            var tex = new Texture2D(w, h);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    tex.SetPixel(x, y, color);
                }
            }
            tex.Apply();
            return tex;
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
            button.onClick.AddListener(() =>
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

            const float gap = 8f;
            const float buttonWidth = 120f;
            const float buttonHeight = 30f;

            Vector3[] imageCorners = new Vector3[4];
            imageRect.GetWorldCorners(imageCorners);
            for (int i = 0; i < imageCorners.Length; i++)
            {
                imageCorners[i] = parentRect.InverseTransformPoint(imageCorners[i]);
            }
            float imageLeft = Mathf.Min(imageCorners[0].x, imageCorners[1].x);   // 0=BL, 1=TL
            float imageTop = Mathf.Max(imageCorners[1].y, imageCorners[2].y);   // 1=TL, 2=TR

            buttonRect.anchorMin = new Vector2(0f, 1f);
            buttonRect.anchorMax = new Vector2(0f, 1f);
            buttonRect.pivot = new Vector2(1f, 1f);
            buttonRect.sizeDelta = new Vector2(buttonWidth, buttonHeight);

            float anchorRefX = parentRect.rect.xMin;
            float anchorRefY = parentRect.rect.yMax;
            float buttonRightX = imageLeft - gap;
            float buttonTopY = imageTop;
            buttonRect.anchoredPosition = new Vector2(buttonRightX - anchorRefX, buttonTopY - anchorRefY);
        }

        private sealed class UiWidgets
        {
            public Button Button;
            public RectTransform ButtonRect;
        }
    }
}
