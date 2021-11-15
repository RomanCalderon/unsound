﻿/*
 * HFPS_GameManager.cs - script written by ThunderWire Games
 * ver. 1.4
*/

using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;
using ThunderWire.Input;
using ThunderWire.Helpers;
using HFPS.Player;
using HFPS.UI;

#if TW_LOCALIZATION_PRESENT
using ThunderWire.Localization;
#endif

namespace HFPS.Systems
{
    public sealed class HelpButton
    {
        public string Name;
        public string BindingPath;

        public HelpButton(string name, string bindingPath)
        {
            Name = name;
            BindingPath = bindingPath;
        }
    }

    public class HFPS_GameManager : Singleton<HFPS_GameManager>
    {
#region Structures
        [Serializable]
        public struct GamePanels
        {
            public GameObject PauseGamePanel;
            public GameObject MainGamePanel;
            public GameObject TabButtonPanel;
            public GameObject LightPanel;
            public GameObject AmmoPanel;
            public GameObject PaperReadPanel;
            public GameObject MiscPanel;
            public GameObject ExaminePanel;
            public GameObject HintTipsPanel;
            public GameObject HintMessagePanel;
            public GameObject InteractPanel;
            public GameObject HelpKeysPanel;
            [Space(5)]
            public Selectable DeadFirstButton;
        }

        [Serializable]
        public struct Popups
        {
            public Transform HintTipsContent;
            public GameObject HintTipsPrefab;
            [Space(5)]
            public Transform QuickMessageContent;
            public GameObject QuickMessagePrefab;
        }

        [Serializable]
        public struct UserInterface
        {
            public CanvasGroup SaveIcon;
            public Image Crosshair;
            public Image ConsoleCursor;
            [Space(5)]
            public Slider LightSlider;
            public Slider ValveSlider;
            public Slider StaminaSlider;
            [Space(5)]
            public Text HintText;
            public Text ExamineText;
            public Text PaperReadText;
            public Text HealthText;
            public Text BulletsText;
            public Text MagazinesText;
        }

        [Serializable]
        public struct InteractUI
        {
            public GameObject InteractInfoText;
            public GameObject KeyboardButton1;
            public GameObject KeyboardButton2;
        }

        [Serializable]
        public struct HelpPanelUI
        {
            public GameObject HelpButton1;
            public GameObject HelpButton2;
            public GameObject HelpButton3;
            public GameObject HelpButton4;
        }
        #endregion

        #region Public Variables
        public GameObject PlayerObj;
        public string sceneLoaderName = "SceneLoader";
        public bool isCursorShown = false;
        public bool pauseTime = false;

        [Tooltip("Enable global localization.")]
        public bool enableLocalization = false;

        /// <summary>
        /// Check if localization exists and is enabled globally.
        /// </summary>
        public static bool LocalizationEnabled
        {
            get
            {
#if TW_LOCALIZATION_PRESENT
                if (HasReference && Instance.enableLocalization)
                {
                    return LocalizationSystem.HasReference;
                }
#endif

                return false;
            }
        }

        [Header("Settings")]
        public bool useGreyscale = true;
        public float greyscaleFadeSpeed = 5f;
        public float saveFadeSpeed = 1.5f;
        public float saveShowTime = 3f;
        public float consoleCursorSpeed = 7;

        [Header("Sprites")]
        public Sprite QMWarningSprite;
        public Sprite DefaultLightIcon;

        [Header("UI References")]
        public GamePanels gamePanels = new GamePanels();
        public Popups popups = new Popups();
        public UserInterface userInterface = new UserInterface();
        public InteractUI interactUI = new InteractUI();
        public HelpPanelUI helpUI = new HelpPanelUI();
        #endregion

#region Hidden Variables
        [HideInInspector] public Scene currentScene;
        [HideInInspector] public bool isPaused;
        [HideInInspector] public bool isInventoryShown;
        [HideInInspector] public bool isHeld;
        [HideInInspector] public bool canGrab;
        [HideInInspector] public bool isGrabbed;
        [HideInInspector] public bool isExamining;
        [HideInInspector] public bool isLocked;
        [HideInInspector] public bool isWeaponZooming;
        #endregion

        #region Private Variables
        //private PostProcessVolume postProcessing;
        //private ColorGrading colorGrading;
        private GameObject playerObj;
        private SaveGameHandler saveHandler;
        private MenuController menuUI;
        private CutsceneManager cutscene;
        private ScriptManager ScriptManager
        {
            get
            {
                if ( scriptManager == null )
                {
                    return scriptManager = ScriptManager.HasReference ? ScriptManager.Instance : null;
                }
                return scriptManager;
            }
            set
            {
                scriptManager = value;
            }
        }
        private ScriptManager scriptManager;
        private HealthManager HealthManager
        {
            get
            {
                if ( _healthManager == null )
                {
                    _healthManager = PlayerObj?.GetComponent<HealthManager> ();
                }
                return _healthManager;
            }
            set
            {
                _healthManager = value;
            }
        }
        private HealthManager _healthManager;

        protected List<IPauseEvent> PauseEvents = new List<IPauseEvent>();
        protected List<GameObject> Notifications = new List<GameObject>();
        protected List<GameObject> HintTipsList = new List<GameObject>();

        private string bindPath_Use;
        private string bindPath_Grab;
        private string bindPath_Throw;
        private string bindPath_Rotate;
        private string bindPath_Cursor;

        private bool greyscaleState;
        private bool playerLocked;
        private bool isGamepad;

        private int oldBlurLevel;
#endregion

#region Texts
        private string ExamineText;
        private string TakeText;
        private string PutAwayText;
        private string RotateText;
        private string ThrowText;
        private string ShowCursorText;
#endregion

        private void Awake()
        {
            InputHandler.OnInputsUpdated += OnInputsUpdated;
            TextsSource.Subscribe(OnInitTexts);

            ScriptManager = ScriptManager.HasReference ? ScriptManager.Instance : null;
            menuUI = GetComponent<MenuController>();
            saveHandler = GetComponent<SaveGameHandler>();
            cutscene = GetComponent<CutsceneManager>();
            HealthManager = PlayerObj?.GetComponent<HealthManager>();

            currentScene = SceneManager.GetActiveScene();
            SetupUIControls();
        }

        private void Start ()
        {
            SetupUI ();
            Unpause ();

            /*if (useGreyscale && colorGrading)
            {
                colorGrading.enabled.Override(true);
                colorGrading.saturation.Override(0);
            }*/

            if ( pauseTime )
            {
                foreach ( var Instance in FindObjectsOfType<MonoBehaviour> ().Where ( x => typeof ( IPauseEvent ).IsAssignableFrom ( x.GetType () ) ).Cast<IPauseEvent> () )
                {
                    PauseEvents.Add ( Instance );
                }
            }
        }

        void OnDestroy()
        {
            if ( InputHandler.GetInputAction ( "Pause" ) != null ) InputHandler.GetInputAction("Pause").performed -= OnPause;
            InputHandler.GetInputAction("Inventory").performed -= OnInventory;
            InputHandler.OnInputsUpdated -= OnInputsUpdated;
            //colorGrading.saturation.Override(0);
        }

        private void OnInitTexts ()
        {
            ExamineText = TextsSource.GetText ( "Interact.Examine", "Examine" );
            TakeText = TextsSource.GetText ( "Interact.Take", "Take" );
            PutAwayText = TextsSource.GetText ( "Examine.PutAway", "Put Away" );
            RotateText = TextsSource.GetText ( "Examine.Rotate", "Rotate" );
            ThrowText = TextsSource.GetText ( "Examine.Throw", "Throw" );
            ShowCursorText = TextsSource.GetText ( "Examine.ShowCursor", "Show Cursor" );
        }

        void SetupUIControls ()
        {
            InputHandler.GetInputAction ( "Pause" ).performed += OnPause;
            InputHandler.GetInputAction ( "Inventory" ).performed += OnInventory;
        }

        void SetupUI()
        {
            gamePanels.TabButtonPanel.SetActive(false);
            if (userInterface.SaveIcon)
            {
                userInterface.SaveIcon.alpha = 0f;
                userInterface.SaveIcon.gameObject.SetActive(false);
            }

            HideSprites(0);
            HideSprites(1);

            gamePanels.HintMessagePanel.SetActive(false);
            gamePanels.ExaminePanel.SetActive(false);
        }

        private void OnInputsUpdated ( InputHandler.Device device, ActionBinding [] bindings )
        {
            isGamepad = device != InputHandler.Device.MouseKeyboard;

            if ( isGamepad )
            {
                Cursor.visible = false;
                Cursor.lockState = CursorLockMode.Locked;
            }
            else
            {
                if ( menuUI.optionsShown )
                {
                    Cursor.visible = true;
                    Cursor.lockState = CursorLockMode.None;
                }
                else
                {
                    Cursor.visible = false;
                    Cursor.lockState = CursorLockMode.Locked;
                }
            }

            bindPath_Use = InputHandler.CompositeOf ( "Use" ).bindingPath;
            bindPath_Grab = InputHandler.CompositeOf ( "Examine" ).bindingPath;
            bindPath_Throw = InputHandler.CompositeOf ( "Zoom" ).bindingPath;
            bindPath_Rotate = InputHandler.CompositeOf ( "Fire" ).bindingPath;
            bindPath_Cursor = InputHandler.CompositeOf ( "Zoom" ).bindingPath;
        }

        private void OnInventory(UnityEngine.InputSystem.InputAction.CallbackContext ctx)
        {
            if (!isPaused && ( HealthManager != null && !HealthManager.isDead ) && !cutscene.cutsceneRunning && ctx.ReadValueAsButton())
            {
                gamePanels.TabButtonPanel.SetActive(!gamePanels.TabButtonPanel.activeSelf);
                gamePanels.MiscPanel.SetActive(!gamePanels.TabButtonPanel.activeSelf);
                LockScript<ExamineManager>(!gamePanels.TabButtonPanel.activeSelf);

                if (gamePanels.TabButtonPanel.activeSelf)
                {
                    isInventoryShown = true;
                    userInterface.Crosshair.enabled = false;
                    GetComponent<FloatingIconManager>().SetAllIconsVisible(false);
                    LockPlayerControls(false, false, true, 3, true);
                    HideSprites(0);
                    HideSprites(1);
                }
                else
                {
                    isInventoryShown = false;
                    userInterface.Crosshair.enabled = true;
                    LockPlayerControls(true, true, false, 3, false);
                    GetComponent<FloatingIconManager>().SetAllIconsVisible(true);
                }
            }
        }

        private void OnPause(UnityEngine.InputSystem.InputAction.CallbackContext ctx)
        {
            if ( ( HealthManager != null && !HealthManager.isDead ) && !cutscene.cutsceneRunning && ctx.ReadValueAsButton())
            {
                gamePanels.PauseGamePanel.SetActive(!gamePanels.PauseGamePanel.activeSelf);
                gamePanels.MainGamePanel.SetActive(!gamePanels.MainGamePanel.activeSelf);

                if (greyscaleState)
                    greyscaleState = !greyscaleState;

                if (!isPaused)
                    menuUI.ShowGeneralMenu();
                else
                    menuUI.ResetPanels();

                isPaused = !isPaused;

                if (isPaused)
                {
                    userInterface.Crosshair.enabled = false;
                    LockPlayerControls(false, false, true, 3, true);
                    ScriptManager.Get<PlayerFunctions>().enabled = false;
                    GetComponent<FloatingIconManager>().SetAllIconsVisible(false);

                    if (pauseTime)
                    {
                        foreach (var PauseEvent in PauseEvents)
                        {
                            PauseEvent.OnPauseEvent(true);
                        }

                        Time.timeScale = 0;
                    }
                }
                else
                {
                    userInterface.Crosshair.enabled = true;
                    LockPlayerControls(true, true, false, 3, false);
                    ScriptManager.Get<PlayerFunctions>().enabled = true;
                    GetComponent<FloatingIconManager>().SetAllIconsVisible(true);

                    if (gamePanels.TabButtonPanel.activeSelf)
                    {
                        gamePanels.TabButtonPanel.SetActive(false);
                        gamePanels.MiscPanel.SetActive(true);
                        LockScript<ExamineManager>(true);
                        isInventoryShown = false;
                    }

                    if (pauseTime)
                    {
                        foreach (var PauseEvent in PauseEvents)
                        {
                            PauseEvent.OnPauseEvent(false);
                        }

                        Time.timeScale = 1;
                    }
                }
            }
        }

        void FixedUpdate()
        {
            if (Notifications.Count > 3)
            {
                Destroy(Notifications[0]);
            }

            if (Notifications.Any(x => x == null))
            {
                Notifications.RemoveAll(x => x == null);
            }
        }

        /*void Update()
        {
            if (useGreyscale && colorGrading != null)
            {
                if (greyscaleState)
                {
                    if (colorGrading.saturation.value > -99)
                    {
                        colorGrading.saturation.value -= Time.unscaledDeltaTime * (greyscaleFadeSpeed * 20);
                    }
                    else if (colorGrading.saturation <= -99)
                    {
                        colorGrading.saturation.Override(-100);
                    }
                }
                else
                {
                    if (colorGrading.saturation.value < -1)
                    {
                        colorGrading.saturation.value += Time.unscaledDeltaTime * (greyscaleFadeSpeed * 20);
                    }
                    else if (colorGrading.saturation >= -1)
                    {
                        colorGrading.saturation.Override(0);
                    }
                }
            }
        }*/

        /// <summary>
        /// Function to show/hide Inventory UI Panel
        /// </summary>
        public void ShowInventory(bool show)
        {
            isInventoryShown = show;

            if (show)
            {
                gamePanels.TabButtonPanel.SetActive(true);
                userInterface.Crosshair.enabled = false;
                GetComponent<FloatingIconManager>().SetAllIconsVisible(false);
                LockPlayerControls(false, false, true, 3, true);
                HideSprites(0);
                HideSprites(1);
            }
            else
            {
                gamePanels.TabButtonPanel.SetActive(false);
                userInterface.Crosshair.enabled = true;
                LockPlayerControls(true, true, false, 3, false);
                GetComponent<FloatingIconManager>().SetAllIconsVisible(true);
            }
        }

        /// <summary>
        /// Function to Unpause Game
        /// </summary>
        public void Unpause()
        {
            GetComponent<FloatingIconManager>().SetAllIconsVisible(true);

            if (gamePanels.TabButtonPanel.activeSelf)
            {
                gamePanels.TabButtonPanel.SetActive(false);
                isInventoryShown = false;
            }

            if (useGreyscale)
            {
                greyscaleState = false;
            }

            userInterface.Crosshair.enabled = true;
            LockPlayerControls(true, true, false, 3, false);
            gamePanels.PauseGamePanel.SetActive(false);
            gamePanels.MainGamePanel.SetActive(true);
            isPaused = false;

            if (pauseTime)
            {
                foreach (var PauseEvent in PauseEvents)
                {
                    PauseEvent.OnPauseEvent(false);
                }

                Time.timeScale = 1;
            }
        }

        /// <summary>
        /// Function to Lock Player Controller or Features
        /// </summary>
        /// <param name="Controller">Should we enable player controller? (true = enabled, false = disabled)</param>
        /// <param name="Interact">Should we enable interact feature? (true = enabled, false = disabled)</param>
        /// <param name="CursorVisible">Should we show/hide cursor? (true = show, false = hide)</param>
        /// <param name="BlurLevel">0 - None, 1 - MainCam Blur, 2 - ArmsCam Blur, 3 - Both Blur</param>
        /// <param name="BlurEnable">Should we enable blur effect? (true = enabled, false = disabled)</param>
        /// <param name="ResetBlur">Should we reset blur effect? (true = reset)</param>
        /// <param name="ForceLockLevel">0 - None, 1 = Enable, 2 - Disable</param>
        public void LockPlayerControls(bool Controller, bool Interact, bool CursorVisible, int BlurLevel = 0, bool BlurEnable = false, bool ResetBlur = false, int ForceLockLevel = 0)
        {
            if (ForceLockLevel == 2)
            {
                playerLocked = false;
            }

            if (!playerLocked && PlayerObj )
            {
                //Controller Lock
                PlayerObj.GetComponent<PlayerController>().isControllable = Controller;
                ScriptManager.Get<PlayerFunctions>().enabled = Controller;
                ScriptManager.ScriptGlobalState = Controller;
                LockScript<MouseLook>(Controller);

                //Interact Lock
                ScriptManager.Get<InteractManager>().inUse = !Interact;
            }

            //Show Cursor
            ShowCursor(CursorVisible && !isGamepad);

            //Blur Levels
            if (BlurLevel > 0)
            {
                if (BlurEnable)
                {
                    SetBlur(true, BlurLevel, ResetBlur);
                }
                else
                {
                    if (playerLocked)
                    {
                        SetBlur(true, oldBlurLevel, true);
                    }
                    else
                    {
                        SetBlur(false, BlurLevel);
                    }
                }
            }

            if (ForceLockLevel == 1)
            {
                playerLocked = true;
                oldBlurLevel = BlurLevel;
            }
        }

        void SetBlur(bool Enable, int BlurLevel, bool Reset = false)
        {
            Debug.Log ( "SetBlur()" );

            /*PostProcessVolume mainPostProcess = scriptManager.MainCamera.GetComponent<PostProcessVolume>();
            PostProcessVolume armsPostProcess = scriptManager.ArmsCamera.GetComponent<PostProcessVolume>();

            if (!mainPostProcess.profile.HasSettings<Blur>())
            {
                throw new NullReferenceException($"[PostProcessing] { mainPostProcess.gameObject.name } does not have Blur PostProcessing Script.");
            }

            if (!armsPostProcess.profile.HasSettings<Blur>())
            {
                throw new NullReferenceException($"[PostProcessing] {armsPostProcess.gameObject.name} does not have Blur PostProcessing script.");
            }

            if (Reset)
            {
                mainPostProcess.profile.GetSetting<Blur>().enabled.Override(false);
                armsPostProcess.profile.GetSetting<Blur>().enabled.Override(false);
            }

            if (BlurLevel == 1) { mainPostProcess.profile.GetSetting<Blur>().enabled.Override(Enable); }
            if (BlurLevel == 2) { armsPostProcess.profile.GetSetting<Blur>().enabled.Override(Enable); }
            if (BlurLevel == 3)
            {
                mainPostProcess.profile.GetSetting<Blur>().enabled.Override(Enable);
                armsPostProcess.profile.GetSetting<Blur>().enabled.Override(Enable);
            }*/
        }

        /// <summary>
        /// Should we lock script in Script Manager children?
        /// </summary>
        /// <typeparam name="T">Script Type (MonoBehaviour)</typeparam>
        /// <param name="enabled">true = enabled, false = disabled</param>
        public void LockScript<T>(bool enabled) where T : MonoBehaviour
        {
            if (ScriptManager.gameObject.GetComponent<T>())
            {
                ScriptManager.gameObject.GetComponent<T>().enabled = enabled;
                return;
            }

            throw new NullReferenceException($"[LockScript] Could not find \"{typeof(T).Name}\" script!");
        }

        /// <summary>
        /// Function to show/hide cursor
        /// </summary>
        /// <param name="state">true = show, false = hide</param>
        public void ShowCursor(bool state)
        {
            switch (state)
            {
                case true:
                    Cursor.visible = true;
                    Cursor.lockState = CursorLockMode.None;
                    break;
                case false:
                    Cursor.visible = false;
                    Cursor.lockState = CursorLockMode.Locked;
                    break;
            }
        }

        /// <summary>
        /// Function to show/hide console cursor
        /// </summary>
        /// <param name="state">true = show, false = hide</param>
        public void ShowConsoleCursor(bool state)
        {
            if (userInterface.ConsoleCursor)
            {
                userInterface.ConsoleCursor.gameObject.SetActive(state);
                userInterface.ConsoleCursor.GetComponent<RectTransform>().localPosition = Vector3.zero;
            }
        }

        /// <summary>
        /// Move with console cursor
        /// </summary>
        public void MoveConsoleCursor(Vector2 movement)
        {
            if (userInterface.ConsoleCursor && userInterface.ConsoleCursor.gameObject.activeSelf)
            {
                Vector3 consoleCursorPos = userInterface.ConsoleCursor.transform.position;
                consoleCursorPos.x += movement.x * consoleCursorSpeed;
                consoleCursorPos.y += movement.y * consoleCursorSpeed;
                userInterface.ConsoleCursor.transform.position = consoleCursorPos;
            }
        }

        /// <summary>
        /// Show small quick message popup
        /// </summary>
        /// <param name="message">Message Text</param>
        /// <param name="id">Unique ID of the popup</param>
        /// <param name="isWarning">Is the message warning?</param>
        public void ShowQuickMessage(string message, string id, bool isWarning = false)
        {
            if (Notifications.Count == 0 || Notifications.Count(x => x.GetComponent<Notification>().id == id) == 0 || string.IsNullOrEmpty(id))
            {
                GameObject Message = Instantiate(popups.QuickMessagePrefab, popups.QuickMessageContent);
                Message.GetComponent<Notification>().id = id;
                Notifications.Add(Message);

                if (!isWarning)
                {
                    Message.GetComponent<Notification>().SetMessage(message);
                }
                else
                {
                    Message.GetComponent<Notification>().SetMessage(message, 3f, QMWarningSprite);
                }
            }
        }

        /// <summary>
        /// Show hint popup at middle top of the screen
        /// </summary>
        /// <param name="text">Hint Text</param>
        /// <param name="time">How long will be popup shown</param>
        /// <param name="messageTips">Tips that will be shown under popup</param>
        public void ShowHintPopup(string text, float time = 3f, InteractiveItem.MessageTip[] messageTips = null)
        {
            userInterface.HintText.text = text;

            if (HintTipsList.Count > 0)
            {
                foreach (var item in HintTipsList)
                {
                    Destroy(item);
                }
            }

            HintTipsList.Clear();

            if (messageTips != null && messageTips.Length > 0)
            {
                bool showTipsPanel = false;

                foreach (var item in messageTips)
                {
                    if (string.IsNullOrEmpty(item.InputAction) || item.InputAction == "?")
                        continue;

                    GameObject obj = Instantiate(popups.HintTipsPrefab, popups.HintTipsContent.transform);
                    ActionBinding.CompositePart composite = InputHandler.CompositeOf(item.InputAction);
                    HintTipsList.Add(obj);

                    if (composite != null)
                    {
                        SetKey(obj.transform, composite.bindingPath, item.Message);
                    }

                    showTipsPanel = true;
                }

                gamePanels.HintTipsPanel.SetActive(showTipsPanel);
            }
            else
            {
                gamePanels.HintTipsPanel.SetActive(false);
            }

            UIFade uIFade = UIFade.Instance(gamePanels.HintMessagePanel, "[UIFader] HintNotification");
            uIFade.ResetGraphicsColor();
            uIFade.ImageTextAlpha(0.8f, 1f);
            uIFade.FadeInOut(fadeOutTime: time, fadeOutAfter: UIFade.FadeOutAfter.Time);
            uIFade.OnFadeOutEvent += delegate
            {
                foreach (var item in HintTipsList)
                {
                    Destroy(item);
                }
            };
            isExamining = false;

            foreach (GameObject tip in HintTipsList)
            {
                HorizontalLayoutGroup horizontalLayoutGroup = tip.GetComponent<HorizontalLayoutGroup>();
                horizontalLayoutGroup.enabled = false;
                horizontalLayoutGroup.enabled = true;
            }
        }

        /// <summary>
        /// Set Control Key Sprite depending on the Input Binding Path
        /// </summary>
        /// <param name="ControlObj">Control Transform</param>
        /// <param name="BindingPath">Input Binding Path of the Control</param>
        /// <param name="ControlName">Name of the Control</param>
        private void SetKey(Transform ControlObj, string BindingPath, string ControlName = "Null")
        {
            ControlObj.GetChild(1).GetComponent<Text>().text = ControlName;

            if (!string.IsNullOrEmpty(BindingPath))
            {
                CrossPlatformSprites sprites = InputHandler.GetSprites();

                if (sprites != null)
                {
                    if (sprites.GetSprite(BindingPath) is var sprite && sprite != null)
                    {
                        ControlObj.GetChild(0).GetComponent<Image>().sprite = sprite;
                    }
                    else
                    {
                        Debug.LogError("[Control Sprite] The specified sprite was not found!");
                    }
                }

                ControlObj.gameObject.SetActive(true);
            }
            else
            {
                ControlObj.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Show/Hide name of the examined item
        /// </summary>
        /// <param name="show">Show/Hide state</param>
        /// <param name="text">Name of the Examined Item</param>
        public void ShowExamineName(bool show, string name)
        {
            UIFade examineFader = UIFade.Instance(gamePanels.ExaminePanel, "[UIFader] ExamineNotification");

            if (examineFader != null)
            {
                if (show)
                {
                    userInterface.ExamineText.text = name;
                    gamePanels.ExaminePanel.SetActive(true);

                    examineFader.ResetGraphicsColor();
                    examineFader.ImageTextAlpha(0.8f, 1f);
                    examineFader.fadeOut = false;
                    examineFader.FadeInOut(fadeOutSpeed: 3f, fadeOutAfter: UIFade.FadeOutAfter.Bool);

                    isExamining = false;
                }
                else
                {
                    examineFader.fadeOut = true;
                }
            }
        }

        /// <summary>
        /// Show Equipped Light UI
        /// </summary>
        /// <param name="start">Light starting slider value</param>
        /// <param name="fadeIn">Should we fade in value?</param>
        /// <param name="icon">Icon of the equipped light</param>
        public void ShowLightUI(float start = 0f, bool fadeIn = true, Sprite icon = null)
        {
            userInterface.LightSlider.value = start / 100;

            if (icon != null)
            {
                gamePanels.LightPanel.transform.GetChild(0).GetComponent<Image>().sprite = icon;
            }
            else
            {
                gamePanels.LightPanel.transform.GetChild(0).GetComponent<Image>().sprite = DefaultLightIcon;
            }

            if (gamePanels.LightPanel.GetComponent<CanvasGroup>())
            {
                Fader fade = Fader.Instance(gamePanels.LightPanel, "[Fader] LightPercentagle");

                if (fade != null)
                {
                    fade.OnFade += value =>
                    {
                        gamePanels.LightPanel.GetComponent<CanvasGroup>().alpha = value;
                    };

                    if (fadeIn)
                    {
                        fade.Fade(new Fader.FadeSettings()
                        {
                            startValue = 0f,
                            endValue = 1f,
                            fadeInSpeed = 1.5f,
                            fadeOutSpeed = 3f,
                            fadeOutAfterSignal = true,
                            actionObj = gamePanels.LightPanel,
                            fadedOutAction = Fader.DestroyType.Disable
                        });
                    }
                    else
                    {
                        fade.FadeOutSignal();
                    }
                }
            }
            else
            {
                gamePanels.LightPanel.SetActive(fadeIn);
            }
        }

        /// <summary>
        /// Show Stamina UI
        /// </summary>
        /// <param name="start">Stamina starting slider value</param>
        /// <param name="fadeIn">Should we fade in value?</param>
        public void ShowStaminaUI(float start, bool fadeIn = true)
        {
            userInterface.StaminaSlider.value = start;

            if (userInterface.StaminaSlider.GetComponent<CanvasGroup>())
            {
                Fader fade = Fader.Instance(userInterface.StaminaSlider.gameObject, "[Fader] Stamina");

                if (fade != null)
                {
                    fade.OnFade += value =>
                    {
                        userInterface.StaminaSlider.GetComponent<CanvasGroup>().alpha = value;
                    };

                    fade.OnFadeCompleted += delegate
                    {
                        userInterface.StaminaSlider.gameObject.SetActive(false);
                    };

                    if (fadeIn)
                    {
                        userInterface.StaminaSlider.gameObject.SetActive(true);

                        fade.Fade(new Fader.FadeSettings()
                        {
                            startValue = 0f,
                            endValue = 1f,
                            fadeInSpeed = 1.5f,
                            fadeOutSpeed = 3f,
                            fadeOutAfterSignal = true,
                            actionObj = userInterface.StaminaSlider.gameObject,
                            fadedOutAction = Fader.DestroyType.Disable
                        });
                    }
                    else
                    {

                        fade.FadeOutSignal();
                    }
                }
            }
            else
            {
                userInterface.StaminaSlider.gameObject.SetActive(fadeIn);
            }
        }

        /// <summary>
        /// Update slider value for specific type
        /// </summary>
        /// <param name="type">
        /// 0 = Light Slider
        /// 1 = Stamina Slider
        /// </param>
        public void UpdateSliderValue(int type, float value)
        {
            if (type == 0)
            {
                userInterface.LightSlider.value = Mathf.MoveTowards(userInterface.LightSlider.value, value / 100, Time.deltaTime);
            }
            else if (type == 1)
            {
                userInterface.StaminaSlider.value = Mathf.MoveTowards(userInterface.StaminaSlider.value, value, Time.deltaTime);
            }
        }

        /// <summary>
        /// Show Saving icon
        /// </summary>
        public void ShowSaveIcon()
        {
            if (userInterface.SaveIcon)
            {
                Fader fade = Fader.Instance(userInterface.SaveIcon.gameObject, "[Fader] SaveIcon");
                userInterface.SaveIcon.gameObject.SetActive(true);

                if (fade != null)
                {
                    fade.OnFade += value =>
                    {
                        userInterface.SaveIcon.alpha = value;
                    };

                    fade.OnFadeCompleted += () => userInterface.SaveIcon.gameObject.SetActive(false);

                    fade.Fade(new Fader.FadeSettings()
                    {
                        startValue = 0f,
                        endValue = 1f,
                        fadeInSpeed = 3f,
                        fadeOutSpeed = 3f,
                        fadeOutWait = saveShowTime,
                        fadedOutAction = Fader.DestroyType.Disable
                    });
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="Row">KB1 = 1 or KB2 = 2</param>
        /// <param name="KeyName">Name of the key</param>
        /// <param name="BindingPath">Binding Path of the Control</param>
        public void ShowInteractSprite(int Row, string KeyName, string BindingPath)
        {
            if (isHeld) return;
            gamePanels.InteractPanel.SetActive(true);

            switch (Row)
            {
                case 1:
                    SetKey(interactUI.KeyboardButton1.transform, BindingPath, KeyName);
                    break;
                case 2:
                    SetKey(interactUI.KeyboardButton2.transform, BindingPath, KeyName);
                    break;
            }
        }

        /// <summary>
        /// Show info of interacting object
        /// </summary>
        /// <param name="info"></param>
        public void ShowInteractInfo(string text)
        {
            interactUI.InteractInfoText.SetActive(true);
            interactUI.InteractInfoText.GetComponent<Text>().text = text;
        }

        /// <summary>
        /// Show Down Help Buttons
        /// </summary>
        public void ShowHelpButtons(HelpButton help1, HelpButton help2, HelpButton help3, HelpButton help4)
        {
            if (help1 != null) { SetKey(helpUI.HelpButton1.transform, help1.BindingPath, help1.Name); } else { helpUI.HelpButton1.SetActive(false); }
            if (help2 != null) { SetKey(helpUI.HelpButton2.transform, help2.BindingPath, help2.Name); } else { helpUI.HelpButton2.SetActive(false); }
            if (help3 != null) { SetKey(helpUI.HelpButton3.transform, help3.BindingPath, help3.Name); } else { helpUI.HelpButton3.SetActive(false); }
            if (help4 != null) { SetKey(helpUI.HelpButton4.transform, help4.BindingPath, help4.Name); } else { helpUI.HelpButton4.SetActive(false); }

            if (help1 != null || help2 != null || help3 != null || help4 != null)
            {
                gamePanels.HelpKeysPanel.SetActive(true);
            }
        }

        //
        /// <summary>
        /// Show Examine UI Buttons
        /// </summary>
        /// <param name="btn1">Put Away</param>
        /// <param name="btn2">Use</param>
        /// <param name="btn3">Rotate</param>
        /// <param name="btn4">Show Cursor</param>
        public void ShowExamineSprites(bool btn1 = true, bool btn2 = true, bool btn3 = true, bool btn4 = true, string PutAwayText = "", string UseText = "")
        {
            if (string.IsNullOrEmpty(PutAwayText)) PutAwayText = this.PutAwayText;
            if (string.IsNullOrEmpty(UseText)) UseText = TakeText;

            if (btn1) { SetKey(helpUI.HelpButton1.transform, bindPath_Grab, PutAwayText); } else { helpUI.HelpButton1.SetActive(false); }
            if (btn2) { SetKey(helpUI.HelpButton2.transform, bindPath_Use, UseText); } else { helpUI.HelpButton2.SetActive(false); }
            if (btn3) { SetKey(helpUI.HelpButton3.transform, bindPath_Rotate, RotateText); } else { helpUI.HelpButton3.SetActive(false); }
            if (btn4) { SetKey(helpUI.HelpButton4.transform, bindPath_Cursor, ShowCursorText); } else { helpUI.HelpButton4.SetActive(false); }
            gamePanels.HelpKeysPanel.SetActive(true);
        }

        /// <summary>
        /// Show Examine Sprites for Paper Object
        /// </summary>
        /// <param name="BindingPath">Binding Path of the Paper Examine Control</param>
        /// <param name="Rotate">Should we show Rotate Control?</param>
        /// <param name="ExamineText">Examine Text</param>
        public void ShowPaperExamineSprites(string BindingPath, bool Rotate, bool Read = true, string ExamineText = "")
        {
            if (string.IsNullOrEmpty(ExamineText)) ExamineText = this.ExamineText;

            SetKey(helpUI.HelpButton1.transform, bindPath_Grab, PutAwayText);

            if (Read)
                SetKey(helpUI.HelpButton2.transform, BindingPath, ExamineText);
            else
                helpUI.HelpButton2.SetActive(false);

            if (Rotate)
                SetKey(helpUI.HelpButton3.transform, bindPath_Rotate, RotateText);
            else
                helpUI.HelpButton3.SetActive(false);

            helpUI.HelpButton4.SetActive(false);
            gamePanels.HelpKeysPanel.SetActive(true);
        }

        /// <summary>
        /// Show Sprites for Draggable Object
        /// </summary>
        public void ShowGrabSprites()
        {
            SetKey(helpUI.HelpButton1.transform, bindPath_Grab, PutAwayText);
            SetKey(helpUI.HelpButton2.transform, bindPath_Rotate, RotateText);
            SetKey(helpUI.HelpButton3.transform, bindPath_Throw, ThrowText);
            helpUI.HelpButton4.SetActive(false);
            gamePanels.HelpKeysPanel.SetActive(true);
        }

        /// <summary>
        /// Hide screen interaction sprites
        /// </summary>
        /// <param name="type">
        /// 0 = Interact Sprites
        /// 1 = Help Sprites
        /// </param>
        public void HideSprites(int type)
        {
            if (type == 0)
            {
                interactUI.KeyboardButton1.SetActive(false);
                interactUI.KeyboardButton2.SetActive(false);
                interactUI.InteractInfoText.SetActive(false);
                gamePanels.InteractPanel.SetActive(false);
            }
            else
            {
                gamePanels.HelpKeysPanel.SetActive(false);
            }
        }

        /// <summary>
        /// Show Death Screen Panel
        /// </summary>
        public void ShowDeadPanel()
        {
            LockPlayerControls(false, false, true);
            ScriptManager.Get<ItemSwitcher>().DisableItems();
            ScriptManager.Get<ItemSwitcher>().enabled = false;

            GetComponent<MenuController>().ShowPanel("Dead"); //Show Dead UI and Buttons
            MenuController.FirstOrAltButton(gamePanels.DeadFirstButton, null);

            gamePanels.PauseGamePanel.SetActive(false);
            gamePanels.MainGamePanel.SetActive(false);
        }

        /// <summary>
        /// Change current scene using default unity method
        /// </summary>
        public void ChangeScene(string SceneName)
        {
            SceneManager.LoadScene(SceneName);
        }

        /// <summary>
        /// If CrossSceneSaving is enabled, save and load next scene using Scene Loader
        /// </summary>
        public void LoadNextScene(string scene)
        {
            if (saveHandler)
            {
                if (saveHandler.crossSceneSaving)
                {
                    saveHandler.SaveNextSceneData(scene);

                    if (!isPaused)
                    {
                        LockPlayerControls(false, false, false);
                    }

                    if (saveHandler.fadeControl)
                    {
                        saveHandler.fadeControl.FadeIn(false);
                    }

                    StartCoroutine(LoadScene(scene, 2));
                }
            }
        }

        /// <summary>
        /// Load last saved scene
        /// </summary>
        public void Retry()
        {
            if (saveHandler.fadeControl)
            {
                saveHandler.fadeControl.FadeIn(false);
            }

            StartCoroutine(LoadScene(SceneManager.GetActiveScene().name, 1));
        }

        private IEnumerator LoadScene(string scene, int loadstate)
        {
            yield return new WaitForEndOfFrame();
            yield return new WaitUntil(() => saveHandler.fadeControl.IsFadedIn);

            Prefs.Game_SaveName(saveHandler.lastSave);
            Prefs.Game_LoadState(loadstate);
            Prefs.Game_LevelName(scene);

            SceneManager.LoadScene(sceneLoaderName);
        }
    }
}