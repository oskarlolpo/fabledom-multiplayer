using System;
using System.Reflection;
using FabledomMultiplayer.Networking;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FabledomMultiplayer
{
    public class MultiplayerUI : MonoBehaviour
    {
        public static MultiplayerUI Instance { get; private set; }

        private MainMenuSceneManager _currentMainMenu;
        private GameObject _overlay;
        private TMP_Text _titleText;

        private GameObject _mainView;
        private GameObject _hostView;
        private GameObject _connectView;

        private TMP_InputField _ipInput;
        private TMP_Text _statusLabel;

        private Button _refButton;
        private TMP_FontAsset _gameFont;

        private void Awake()
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void OpenMenu(MainMenuSceneManager mainMenu)
        {
            _currentMainMenu = mainMenu;

            // Close other menus if open
            try
            {
                var saveField = typeof(MainMenuSceneManager).GetField("saveLoadMenu", BindingFlags.Instance | BindingFlags.NonPublic);
                var saveMenu = saveField?.GetValue(mainMenu) as SaveLoadMenuContainer;
                saveMenu?.gameObject.SetActive(false);

                var settingsField = typeof(MainMenuSceneManager).GetField("settingsMenu", BindingFlags.Instance | BindingFlags.NonPublic);
                var settingsMenu = settingsField?.GetValue(mainMenu) as SettingsMenu;
                settingsMenu?.gameObject.SetActive(false);

                var creditsField = typeof(MainMenuSceneManager).GetField("creditsPanel", BindingFlags.Instance | BindingFlags.NonPublic);
                var credits = creditsField?.GetValue(mainMenu) as GameObject;
                credits?.SetActive(false);
            }
            catch { }

            if (_overlay == null)
            {
                BuildCoopDialog();
            }

            if (_overlay != null)
            {
                _overlay.SetActive(true);
                ShowMainView();
            }
        }

        public void CloseMenu()
        {
            if (_overlay != null)
            {
                _overlay.SetActive(false);
            }
        }

        private void BuildCoopDialog()
        {
            try
            {
                MultiplayerPlugin.Logger.LogInfo("Building compact fairy-tale Co-op Dialog window...");

                Canvas parentCanvas = null;
                if (_currentMainMenu != null && _currentMainMenu.mainMenuContainer != null)
                {
                    parentCanvas = _currentMainMenu.mainMenuContainer.GetComponentInParent<Canvas>();
                }
                if (parentCanvas == null) parentCanvas = FindObjectOfType<Canvas>();
                if (parentCanvas == null)
                {
                    MultiplayerPlugin.Logger.LogError("Canvas not found for Co-op UI!");
                    return;
                }

                // Reference button and font
                if (_currentMainMenu != null && _currentMainMenu.mainMenuContainer != null)
                {
                    _refButton = _currentMainMenu.mainMenuContainer.GetComponentInChildren<Button>();
                    var tmp = _refButton?.GetComponentInChildren<TMP_Text>();
                    _gameFont = tmp?.font;
                }

                // Reference background sprite from game's menus
                Sprite bgSprite = null;
                try
                {
                    var saveField = typeof(MainMenuSceneManager).GetField("saveLoadMenu", BindingFlags.Instance | BindingFlags.NonPublic);
                    var saveMenu = saveField?.GetValue(_currentMainMenu) as SaveLoadMenuContainer;
                    if (saveMenu != null)
                    {
                        var img = saveMenu.GetComponent<Image>() ?? saveMenu.GetComponentInChildren<Image>();
                        if (img != null) bgSprite = img.sprite;
                    }
                }
                catch { }

                // 1. Overlay (dims background)
                _overlay = new GameObject("CoopModalOverlay", typeof(RectTransform), typeof(Image));
                _overlay.transform.SetParent(parentCanvas.transform, false);
                var overlayRect = _overlay.GetComponent<RectTransform>();
                overlayRect.anchorMin = Vector2.zero;
                overlayRect.anchorMax = Vector2.one;
                overlayRect.offsetMin = Vector2.zero;
                overlayRect.offsetMax = Vector2.zero;

                var overlayImg = _overlay.GetComponent<Image>();
                overlayImg.color = new Color(0.02f, 0.02f, 0.02f, 0.82f);

                // 2. Window (Compact: 500x380)
                var window = new GameObject("CoopWindow", typeof(RectTransform), typeof(Image), typeof(Outline));
                window.transform.SetParent(_overlay.transform, false);
                var winRect = window.GetComponent<RectTransform>();
                winRect.sizeDelta = new Vector2(500, 380);
                winRect.anchoredPosition = Vector2.zero;

                var winImg = window.GetComponent<Image>();
                if (bgSprite != null)
                {
                    winImg.sprite = bgSprite;
                    winImg.type = Image.Type.Sliced;
                    winImg.color = Color.white;
                }
                else
                {
                    winImg.color = new Color(0.14f, 0.11f, 0.08f, 0.98f);
                    var winOutline = window.GetComponent<Outline>();
                    winOutline.effectColor = new Color(0.85f, 0.72f, 0.45f, 0.9f);
                    winOutline.effectDistance = new Vector2(2, -2);
                }

                // 3. Title Text
                var titleObj = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
                titleObj.transform.SetParent(window.transform, false);
                var titleRect = titleObj.GetComponent<RectTransform>();
                titleRect.sizeDelta = new Vector2(460, 50);
                titleRect.anchoredPosition = new Vector2(0, 140);

                _titleText = titleObj.GetComponent<TextMeshProUGUI>();
                if (_gameFont != null) _titleText.font = _gameFont;
                _titleText.text = "Кооператив";
                _titleText.fontSize = 30;
                _titleText.color = new Color(1f, 0.90f, 0.65f);
                _titleText.alignment = TextAlignmentOptions.Center;

                // ==================== VIEW 1: MAIN CHOICE ====================
                _mainView = CreateViewContainer(window.transform, "MainView");
                CreateMenuButton(_mainView.transform, "Создать игру (Хост)", ShowHostView);
                CreateMenuButton(_mainView.transform, "Подключиться к другу", ShowConnectView);
                CreateMenuButton(_mainView.transform, "Назад", CloseMenu);

                // ==================== VIEW 2: HOST VIEW ====================
                _hostView = CreateViewContainer(window.transform, "HostView");
                CreateMenuButton(_hostView.transform, "Новая игра", () =>
                {
                    CloseMenu();
                    StartHostAndRun(() => _currentMainMenu?.StartNewGame());
                });
                CreateMenuButton(_hostView.transform, "Загрузить сохранение", () =>
                {
                    CloseMenu();
                    StartHostAndRun(() => _currentMainMenu?.OpenLoadMenu());
                });
                CreateMenuButton(_hostView.transform, "Назад", ShowMainView);

                // ==================== VIEW 3: CONNECT VIEW ====================
                _connectView = CreateViewContainer(window.transform, "ConnectView");

                _ipInput = CreateCleanInput(_connectView.transform, "127.0.0.1:7777");
                CreateMenuButton(_connectView.transform, "Подключиться", () =>
                {
                    string target = _ipInput != null && !string.IsNullOrEmpty(_ipInput.text)
                        ? _ipInput.text.Trim()
                        : "127.0.0.1:7777";
                    DoConnect(target);
                });

                // Status
                var statusObj = new GameObject("Status", typeof(RectTransform), typeof(TextMeshProUGUI));
                statusObj.transform.SetParent(_connectView.transform, false);
                var sRect = statusObj.GetComponent<RectTransform>();
                sRect.sizeDelta = new Vector2(420, 30);
                _statusLabel = statusObj.GetComponent<TextMeshProUGUI>();
                if (_gameFont != null) _statusLabel.font = _gameFont;
                _statusLabel.fontSize = 17;
                _statusLabel.color = new Color(0.75f, 0.9f, 1f);
                _statusLabel.alignment = TextAlignmentOptions.Center;
                _statusLabel.text = "Введите адрес напарника из Porthole";

                CreateMenuButton(_connectView.transform, "Назад", ShowMainView);

                _overlay.SetActive(false);
                MultiplayerPlugin.Logger.LogInfo("Compact Co-op Dialog built successfully!");
            }
            catch (Exception ex)
            {
                MultiplayerPlugin.Logger.LogError($"Error building Co-op Dialog: {ex}");
            }
        }

        private GameObject CreateViewContainer(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(440, 240);
            rect.anchoredPosition = new Vector2(0, -30);

            var vlg = go.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 16;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlWidth = false;
            vlg.childControlHeight = false;

            return go;
        }

        private void ShowMainView()
        {
            if (_titleText != null) _titleText.text = "Кооператив";
            if (_mainView != null) _mainView.SetActive(true);
            if (_hostView != null) _hostView.SetActive(false);
            if (_connectView != null) _connectView.SetActive(false);
        }

        private void ShowHostView()
        {
            if (_titleText != null) _titleText.text = "Создание игры";
            if (_mainView != null) _mainView.SetActive(false);
            if (_hostView != null) _hostView.SetActive(true);
            if (_connectView != null) _connectView.SetActive(false);
        }

        private void ShowConnectView()
        {
            if (_titleText != null) _titleText.text = "Подключение";
            if (_mainView != null) _mainView.SetActive(false);
            if (_hostView != null) _hostView.SetActive(false);
            if (_connectView != null) _connectView.SetActive(true);
            if (_statusLabel != null)
            {
                _statusLabel.text = "Введите адрес напарника из Porthole";
                _statusLabel.color = new Color(0.75f, 0.9f, 1f);
            }
        }

        private void StartHostAndRun(Action startAction)
        {
            MultiplayerPlugin.Logger.LogInfo("Launching Host on port 7777...");
            NetworkManager.Instance?.StartHost(7777);
            startAction?.Invoke();
        }

        private void DoConnect(string target)
        {
            string host = target;
            int port = 7777;

            if (target.Contains(":"))
            {
                var parts = target.Split(':');
                host = parts[0];
                if (int.TryParse(parts[1], out int p)) port = p;
            }

            if (_statusLabel != null)
            {
                _statusLabel.text = $"Подключение к {host}:{port}...";
                _statusLabel.color = new Color(1f, 0.85f, 0.4f);
            }

            NetworkManager.Instance?.ConnectToHost(host, port);
        }

        private Button CreateMenuButton(Transform parent, string label, Action onClick)
        {
            GameObject btnObj;
            if (_refButton != null)
            {
                btnObj = GameObject.Instantiate(_refButton.gameObject, parent);

                // Strip localization scripts so text is NOT overwritten!
                var comps = btnObj.GetComponentsInChildren<MonoBehaviour>(true);
                foreach (var c in comps)
                {
                    if (c != null && c.GetType().Name.IndexOf("Localiz", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        DestroyImmediate(c);
                    }
                }

                // Strip old button component and add clean one
                var oldB = btnObj.GetComponent<Button>();
                var targetG = oldB.targetGraphic;
                var cols = oldB.colors;
                var trans = oldB.transition;
                DestroyImmediate(oldB);

                var b = btnObj.AddComponent<Button>();
                b.targetGraphic = targetG;
                b.colors = cols;
                b.transition = trans;
                b.onClick.AddListener(() => onClick?.Invoke());
            }
            else
            {
                btnObj = new GameObject("Btn_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
                btnObj.transform.SetParent(parent, false);
                var b = btnObj.GetComponent<Button>();
                b.onClick.AddListener(() => onClick?.Invoke());
            }

            var rect = btnObj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(340, 48);

            var text = btnObj.GetComponentInChildren<TMP_Text>();
            if (text != null)
            {
                if (_gameFont != null) text.font = _gameFont;
                text.text = label;
                text.fontSize = 24;
                text.enableWordWrapping = false;
                text.alignment = TextAlignmentOptions.Center;
            }

            return btnObj.GetComponent<Button>();
        }

        private TMP_InputField CreateCleanInput(Transform parent, string placeholder)
        {
            var go = new GameObject("InputField", typeof(RectTransform), typeof(Image), typeof(Outline), typeof(TMP_InputField));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(340, 46);

            var img = go.GetComponent<Image>();
            img.color = new Color(0.08f, 0.07f, 0.05f, 0.95f);

            var outline = go.GetComponent<Outline>();
            outline.effectColor = new Color(0.8f, 0.65f, 0.4f, 0.8f);
            outline.effectDistance = new Vector2(1, -1);

            var textObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObj.transform.SetParent(go.transform, false);
            var textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10, 0);
            textRect.offsetMax = new Vector2(-10, 0);

            var text = textObj.GetComponent<TextMeshProUGUI>();
            if (_gameFont != null) text.font = _gameFont;
            text.fontSize = 20;
            text.color = new Color(1f, 0.95f, 0.85f);
            text.alignment = TextAlignmentOptions.Center;

            var input = go.GetComponent<TMP_InputField>();
            input.textComponent = text;
            input.text = placeholder;

            return input;
        }

        private void Update()
        {
            if (NetworkManager.Instance != null && _statusLabel != null && _connectView != null && _connectView.activeSelf)
            {
                if (NetworkManager.Instance.IsConnected)
                {
                    _statusLabel.text = "Успешно подключено к лобби!";
                    _statusLabel.color = new Color(0.4f, 1f, 0.4f);
                }
                else if (NetworkManager.Instance.State == ConnectionState.Connecting)
                {
                    _statusLabel.text = "Подключение...";
                    _statusLabel.color = new Color(1f, 0.85f, 0.4f);
                }
            }
        }
    }
}
