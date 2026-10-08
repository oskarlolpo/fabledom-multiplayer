using System;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FabledomMultiplayer
{
    [HarmonyPatch(typeof(MainMenuSceneManager))]
    public static class MainMenuPatch
    {
        private static GameObject _mpButtonObj;

        [HarmonyPostfix]
        [HarmonyPatch("Start")]
        public static void Start_Postfix(MainMenuSceneManager __instance)
        {
            try
            {
                MultiplayerPlugin.Logger.LogInfo("MainMenuSceneManager.Start intercepted. Creating Coop button...");

                // Find loadButton via reflection
                var loadButtonField = typeof(MainMenuSceneManager).GetField("loadButton", BindingFlags.Instance | BindingFlags.NonPublic);
                Button loadButton = loadButtonField?.GetValue(__instance) as Button;

                Transform buttonsParent = null;
                if (loadButton != null)
                {
                    buttonsParent = loadButton.transform.parent;
                }
                else if (__instance.mainMenuContainer != null)
                {
                    buttonsParent = __instance.mainMenuContainer.transform;
                    loadButton = buttonsParent.GetComponentInChildren<Button>();
                }

                if (loadButton == null || buttonsParent == null)
                {
                    MultiplayerPlugin.Logger.LogWarning("Could not find loadButton or menu container to clone!");
                    return;
                }

                if (_mpButtonObj != null)
                {
                    GameObject.Destroy(_mpButtonObj);
                }

                // Clone loadButton to get the exact layout, font and animations
                _mpButtonObj = GameObject.Instantiate(loadButton.gameObject, buttonsParent);
                _mpButtonObj.name = "CoopButton";

                // Match exact RectTransform dimensions
                var origRect = loadButton.GetComponent<RectTransform>();
                var myRect = _mpButtonObj.GetComponent<RectTransform>();
                if (origRect != null && myRect != null)
                {
                    myRect.sizeDelta = origRect.sizeDelta;
                    myRect.anchorMin = origRect.anchorMin;
                    myRect.anchorMax = origRect.anchorMax;
                    myRect.pivot = origRect.pivot;
                }

                // Position right after loadButton
                int loadIndex = loadButton.transform.GetSiblingIndex();
                _mpButtonObj.transform.SetSiblingIndex(loadIndex + 1);

                // Strip any Localization scripts so Unity Localization never overwrites our text
                var allComps = _mpButtonObj.GetComponentsInChildren<MonoBehaviour>(true);
                foreach (var c in allComps)
                {
                    if (c != null && c.GetType().Name.IndexOf("Localiz", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        UnityEngine.Object.DestroyImmediate(c);
                    }
                }

                // Set text to "Кооператив" with EXACT same font and size as loadButton (do not reduce size!)
                var origTmp = loadButton.GetComponentInChildren<TMP_Text>();
                var tmpText = _mpButtonObj.GetComponentInChildren<TMP_Text>();
                if (tmpText != null)
                {
                    if (origTmp != null)
                    {
                        tmpText.font = origTmp.font;
                        tmpText.fontSize = origTmp.fontSize;
                        tmpText.fontSizeMin = origTmp.fontSizeMin;
                        tmpText.fontSizeMax = origTmp.fontSizeMax;
                        tmpText.color = origTmp.color;
                    }
                    tmpText.text = "Кооператив";
                    tmpText.enableWordWrapping = false;
                    tmpText.alignment = TextAlignmentOptions.Center;
                }

                // Clean Button component without inspector persistent callbacks
                var oldBtn = _mpButtonObj.GetComponent<Button>();
                var targetGraphic = oldBtn.targetGraphic;
                var colors = oldBtn.colors;
                var spriteState = oldBtn.spriteState;
                var transition = oldBtn.transition;

                GameObject.DestroyImmediate(oldBtn);

                var newBtn = _mpButtonObj.AddComponent<Button>();
                newBtn.targetGraphic = targetGraphic;
                newBtn.colors = colors;
                newBtn.spriteState = spriteState;
                newBtn.transition = transition;

                newBtn.onClick.AddListener(() =>
                {
                    MultiplayerPlugin.Logger.LogInfo("Coop button clicked! Opening Coop Menu...");
                    MultiplayerUI.Instance?.OpenMenu(__instance);
                });

                MultiplayerPlugin.Logger.LogInfo("Coop button successfully created matching exact font size!");
            }
            catch (Exception ex)
            {
                MultiplayerPlugin.Logger.LogError($"Error creating Coop button: {ex}");
            }
        }
    }
}
