using Client.Main.Controls.UI;
using Client.Main.Controls.UI.Common;
using Client.Main.Controls.UI.Game;
using Client.Main.Controls.UI.SelectCharacter;
using Client.Main.Controllers;
using Client.Main.Core.Client;
using Client.Main.Core.Utilities;
using Client.Main.Graphics;
using Client.Main.Helpers;
using Client.Main.Models;
using Client.Main.Networking;
using Client.Main.Objects.Player;
using Client.Main.Worlds;
using Client.Main.Scenes.SelectCharacter;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MUnique.OpenMU.Network.Packets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Client.Main.Scenes
{
    public class SelectCharacterScene : BaseScene
    {
        private static class Theme
        {
            // MU Online dark gothic backgrounds
            public static readonly Color BgDarkest = new(4, 4, 8, 255);
            public static readonly Color BgDark = new(8, 8, 16, 245);
            public static readonly Color BgMid = new(14, 12, 22, 240);
            public static readonly Color BgLight = new(22, 18, 32, 235);
            public static readonly Color BgLighter = new(32, 26, 44, 230);
            public static readonly Color BgCard = new(10, 8, 18, 220);
            public static readonly Color BgCardHover = new(20, 16, 30, 235);
            public static readonly Color BgCardSelected = new(28, 22, 42, 245);

            // MU Online signature gold
            public static readonly Color Gold = new(200, 160, 60);
            public static readonly Color GoldBright = new(240, 200, 90);
            public static readonly Color GoldDim = new(130, 100, 35);
            public static readonly Color GoldGlow = new(255, 210, 80, 30);

            // Accent - deep crimson (MU Online signature red)
            public static readonly Color Crimson = new(160, 30, 30);
            public static readonly Color CrimsonBright = new(200, 50, 50);
            public static readonly Color CrimsonDim = new(90, 18, 18);

            // Borders - classic MU double-border style
            public static readonly Color BorderOuter = new(180, 140, 50);   // outer gold line
            public static readonly Color BorderInner = new(60, 48, 16);     // inner dark line
            public static readonly Color BorderHighlight = new(240, 200, 80, 100);

            // Text
            public static readonly Color TextWhite = new(230, 225, 210);
            public static readonly Color TextGold = new(220, 185, 80);
            public static readonly Color TextGoldBright = new(255, 220, 110);
            public static readonly Color TextGray = new(140, 130, 110);
            public static readonly Color TextDark = new(80, 72, 58);
            public static readonly Color TextRed = new(200, 80, 60);

            // Button colors (MU Online style)
            public static readonly Color BtnNormal = new(10, 8, 18);
            public static readonly Color BtnHover = new(22, 18, 34);
            public static readonly Color BtnDisabled = new(8, 6, 14);
            public static readonly Color BtnEnter = new(12, 28, 12);        // green tint
            public static readonly Color BtnDelete = new(28, 8, 8);         // red tint
            public static readonly Color BtnCreate = new(10, 14, 28);       // blue tint
        }

        private const int PANEL_WIDTH = 340;
        private const int PANEL_MARGIN = 30;
        private const int HEADER_HEIGHT = 45;
        private const int BUTTON_HEIGHT = 36;
        private const int BUTTON_SPACING = 8;
        private const int INNER_PADDING = 12;
        private const int CHAR_CARD_HEIGHT = 65;
        private const int CHAR_CARD_SPACING = 6;

        // Fields
        private readonly List<(string Name, CharacterClassNumber Class, ushort Level, byte[] Appearance)> _characters;
        private SelectWorld _selectWorld;
        private CharacterSelectionController _characterController;
        private readonly NetworkManager _networkManager;
        private ILogger<SelectCharacterScene> _logger;
        private (string Name, CharacterClassNumber Class, ushort Level, byte[] Appearance)? _selectedCharacterInfo = null;
        private LoadingScreenControl _loadingScreen;
        private bool _initialLoadComplete = false;
        private ButtonControl _previousCharacterButton;
        private ButtonControl _nextCharacterButton;
        private int _currentCharacterIndex = -1;
        private bool _isSelectionInProgress = false;
        private Texture2D _backgroundTexture;
        private ProgressBarControl _progressBar;
        private bool _previousDayNightEnabled;
        private Vector3 _previousSunDirection;
        private bool _dayNightPatched;
        private ButtonControl _createCharacterButton;
        private ButtonControl _deleteCharacterButton;
        private ButtonControl _enterGameButton;
        private ButtonControl _exitButton;
        private CharacterCreationDialog _characterCreationDialog;
        private string _currentlySelectedCharacterName = null;
        private bool _isIntentionalLogout = false;

        // UI Panel rendering
        private Rectangle _characterPanelRect;
        private Rectangle _buttonSectionRect;
        private Rectangle _characterListRect;
        private List<Rectangle> _characterCardRects = new List<Rectangle>();
        private int _hoveredCardIndex = -1;
        private bool _previousMousePressed = false;

        // Constructors
        public SelectCharacterScene(List<(string Name, CharacterClassNumber Class, ushort Level, byte[] Appearance)> characters, NetworkManager networkManager)
        {
            _characters = characters ?? new List<(string Name, CharacterClassNumber Class, ushort Level, byte[] Appearance)>();
            _networkManager = networkManager ?? throw new ArgumentNullException(nameof(networkManager));
            _logger = MuGame.AppLoggerFactory.CreateLogger<SelectCharacterScene>();

            _loadingScreen = new LoadingScreenControl { Visible = true, Message = "Loading Characters..." };
            Controls.Add(_loadingScreen);
            _loadingScreen.BringToFront();

            InitializeModernUI();

            try
            {
                _backgroundTexture = MuGame.Instance.Content.Load<Texture2D>("Background");
            }
            catch (Exception ex)
            {
                _logger?.LogDebug($"[SelectCharacterScene] Background load failed: {ex.Message}");
            }

            _progressBar = new ProgressBarControl();
            Controls.Add(_progressBar);

            SubscribeToNetworkEvents();
        }

        private void DisableDayNightCycleForScene()
        {
            if (_dayNightPatched) return;

            _dayNightPatched = true;
            _previousDayNightEnabled = Constants.ENABLE_DAY_NIGHT_CYCLE;
            _previousSunDirection = Constants.SUN_DIRECTION;
            Constants.ENABLE_DAY_NIGHT_CYCLE = false;
            SunCycleManager.ResetToDefault();
        }

        private void RestoreDayNightCycle()
        {
            if (!_dayNightPatched) return;

            Constants.ENABLE_DAY_NIGHT_CYCLE = _previousDayNightEnabled;
            Constants.SUN_DIRECTION = _previousSunDirection;
            _dayNightPatched = false;
        }

        private void UpdateLoadProgress(string message, float progress)
        {
            MuGame.ScheduleOnMainThread(() =>
            {
                if (_loadingScreen != null && _loadingScreen.Visible)
                {
                    _loadingScreen.Message = message;
                    _loadingScreen.Progress = progress;
                }
            });
        }

        private void InitializeModernUI()
        {
            // Previous/Next character arrows (disabled)
            _previousCharacterButton = CreateModernNavigationButton("<");
            _previousCharacterButton.Click += (s, e) => MoveSelection(-1);
            _previousCharacterButton.Enabled = false;
            _previousCharacterButton.Visible = false;
            Controls.Add(_previousCharacterButton);

            _nextCharacterButton = CreateModernNavigationButton(">");
            _nextCharacterButton.Click += (s, e) => MoveSelection(1);
            _nextCharacterButton.Enabled = false;
            _nextCharacterButton.Visible = false;
            Controls.Add(_nextCharacterButton);

            // Action buttons — MU Online style
            _enterGameButton = CreateModernButton("ENTER GAME", Theme.BtnEnter);
            _enterGameButton.Click += OnEnterGameButtonClick;
            Controls.Add(_enterGameButton);

            _createCharacterButton = CreateModernButton("CREATE CHARACTER", Theme.BtnCreate);
            _createCharacterButton.Click += OnCreateCharacterButtonClick;
            Controls.Add(_createCharacterButton);

            _deleteCharacterButton = CreateModernButton("DELETE CHARACTER", Theme.BtnDelete);
            _deleteCharacterButton.Click += OnDeleteCharacterButtonClick;
            Controls.Add(_deleteCharacterButton);

            _exitButton = CreateModernButton("EXIT", Theme.BtnNormal);
            _exitButton.Click += OnExitButtonClick;
            Controls.Add(_exitButton);

            CalculatePanelLayout();
        }

        private ButtonControl CreateModernNavigationButton(string arrow)
        {
            return new ButtonControl
            {
                Text = arrow,
                FontSize = 48f,
                AutoViewSize = false,
                ViewSize = new Point(70, 70),
                BackgroundColor = Theme.BgMid,
                HoverBackgroundColor = Theme.BgLight,
                PressedBackgroundColor = Theme.BgDark,
                TextColor = Theme.Gold,
                HoverTextColor = Theme.GoldBright,
                DisabledTextColor = Theme.TextDark,
                Interactive = true,
                Visible = false,
                Enabled = false,
                BorderThickness = 2,
                BorderColor = Theme.BorderInner
            };
        }

        private ButtonControl CreateModernButton(string text, Color baseColor)
        {
            return new ButtonControl
            {
                Text = text,
                FontSize = 12f,
                AutoViewSize = false,
                ViewSize = new Point(PANEL_WIDTH - INNER_PADDING * 2, BUTTON_HEIGHT),
                BackgroundColor = baseColor,
                HoverBackgroundColor = Color.Lerp(baseColor, new Color(60, 50, 20), 0.5f),
                PressedBackgroundColor = Color.Lerp(baseColor, Color.Black, 0.4f),
                TextColor = Theme.TextGold,
                HoverTextColor = Theme.TextGoldBright,
                DisabledTextColor = Theme.TextDark,
                Interactive = true,
                Visible = false,
                Enabled = false,
                BorderThickness = 1,
                BorderColor = Theme.GoldDim
            };
        }

        private void CalculatePanelLayout()
        {
            int screenWidth = ViewSize.X;
            int screenHeight = ViewSize.Y;

            // Calculate panel height based on content
            int buttonSectionHeight = (BUTTON_HEIGHT + BUTTON_SPACING) * 4 + INNER_PADDING * 2; // Buttons only, no header
            int maxCharCards = Math.Min(_characters.Count, 5);
            int characterListHeight = maxCharCards * (CHAR_CARD_HEIGHT + CHAR_CARD_SPACING) + INNER_PADDING * 2;
            int totalPanelHeight = HEADER_HEIGHT + characterListHeight + buttonSectionHeight;

            // Character panel (right side)
            int panelX = screenWidth - PANEL_WIDTH - PANEL_MARGIN;
            int panelY = (screenHeight - totalPanelHeight) / 2;
            _characterPanelRect = new Rectangle(panelX, panelY, PANEL_WIDTH, totalPanelHeight);

            // Character list section (top, below header)
            int listY = panelY + HEADER_HEIGHT;
            _characterListRect = new Rectangle(panelX, listY, PANEL_WIDTH, characterListHeight);

            // Button section (bottom of panel, below character list)
            int buttonY = listY + characterListHeight;
            _buttonSectionRect = new Rectangle(panelX, buttonY, PANEL_WIDTH, buttonSectionHeight);

            // Calculate character card rectangles
            _characterCardRects.Clear();
            int cardY = listY + INNER_PADDING;
            for (int i = 0; i < _characters.Count && i < 5; i++)
            {
                _characterCardRects.Add(new Rectangle(
                    panelX + INNER_PADDING,
                    cardY,
                    PANEL_WIDTH - INNER_PADDING * 2,
                    CHAR_CARD_HEIGHT
                ));
                cardY += CHAR_CARD_HEIGHT + CHAR_CARD_SPACING;
            }
        }

        private void PositionNavigationButtons()
        {
            // Early exit if buttons not created yet (called during construction)
            if (_previousCharacterButton == null && _nextCharacterButton == null && 
                _enterGameButton == null && _createCharacterButton == null && 
                _deleteCharacterButton == null && _exitButton == null)
            {
                return;
            }

            CalculatePanelLayout();
            
            bool ready = _initialLoadComplete && (_loadingScreen == null || !_loadingScreen.Visible) && !_isSelectionInProgress;
            bool hasCharacters = _characters.Count > 0;
            bool hasSelection = !string.IsNullOrEmpty(_currentlySelectedCharacterName);
            bool canCreate = _characters.Count < 5;

            // Position navigation arrows
            if (_previousCharacterButton != null)
            {
                _previousCharacterButton.X = (ViewSize.X / 2) - 250;
                _previousCharacterButton.Y = (ViewSize.Y - _previousCharacterButton.ViewSize.Y) / 2;
            }

            if (_nextCharacterButton != null)
            {
                _nextCharacterButton.X = (ViewSize.X / 2) + 180;
                _nextCharacterButton.Y = (ViewSize.Y - _nextCharacterButton.ViewSize.Y) / 2;
            }

            // Position action buttons in button section (bottom of panel)
            int panelX = _characterPanelRect.X;
            int buttonY = _buttonSectionRect.Y + INNER_PADDING;

            // ENTER GAME button (top of button section)
            if (_enterGameButton != null)
            {
                _enterGameButton.X = panelX + INNER_PADDING;
                _enterGameButton.Y = buttonY;
                _enterGameButton.Enabled = ready && hasCharacters && hasSelection;
                _enterGameButton.Visible = ready && hasCharacters && hasSelection;
            }

            buttonY += (BUTTON_HEIGHT + BUTTON_SPACING);

            // DELETE CHARACTER button (shows when character selected)
            if (_deleteCharacterButton != null)
            {
                _deleteCharacterButton.X = panelX + INNER_PADDING;
                _deleteCharacterButton.Y = buttonY;
                _deleteCharacterButton.Enabled = ready && hasSelection;
                _deleteCharacterButton.Visible = ready && hasSelection;
                
                _logger?.LogDebug("Delete button - Ready: {Ready}, HasSelection: {HasSel}, CharName: '{Name}', Visible: {Vis}", 
                    ready, hasSelection, _currentlySelectedCharacterName, _deleteCharacterButton.Visible);
            }

            buttonY += (BUTTON_HEIGHT + BUTTON_SPACING);

            // CREATE CHARACTER button
            if (_createCharacterButton != null)
            {
                _createCharacterButton.X = panelX + INNER_PADDING;
                _createCharacterButton.Y = buttonY;
                _createCharacterButton.Enabled = ready && canCreate;
                _createCharacterButton.Visible = ready;
            }

            buttonY += (BUTTON_HEIGHT + BUTTON_SPACING);

            // EXIT button (very bottom)
            if (_exitButton != null)
            {
                _exitButton.X = panelX + INNER_PADDING;
                _exitButton.Y = buttonY;
                _exitButton.Enabled = ready && !_isSelectionInProgress;
                _exitButton.Visible = ready;
            }

        }

        private void UpdateNavigationButtonState()
        {
            // Navigation buttons are permanently disabled
            if (_previousCharacterButton != null)
            {
                _previousCharacterButton.Enabled = false;
                _previousCharacterButton.Visible = false;
            }

            if (_nextCharacterButton != null)
            {
                _nextCharacterButton.Enabled = false;
                _nextCharacterButton.Visible = false;
            }
        }

        private void MoveSelection(int direction)
        {
            if (_characters.Count == 0 || _characterController == null)
            {
                return;
            }

            if (!_initialLoadComplete || (_loadingScreen != null && _loadingScreen.Visible) || _isSelectionInProgress)
            {
                return;
            }

            int currentIndex = _currentCharacterIndex;
            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            if (_characters.Count == 1)
            {
                return;
            }

            int nextIndex = (currentIndex + direction) % _characters.Count;
            if (nextIndex < 0)
            {
                nextIndex += _characters.Count;
            }

            if (nextIndex == _currentCharacterIndex)
            {
                return;
            }

            _currentCharacterIndex = nextIndex;
            _characterController.SetActiveCharacter(_currentCharacterIndex);

            if (_currentCharacterIndex >= 0 && _currentCharacterIndex < _characters.Count)
            {
                _currentlySelectedCharacterName = _characters[_currentCharacterIndex].Name;
                PositionNavigationButtons();
                UpdateNavigationButtonState();
            }
            else
            {
                _currentlySelectedCharacterName = null;
                UpdateNavigationButtonState();
            }

        }

        protected override async Task LoadSceneContentWithProgress(Action<string, float> progressCallback)
        {
            DisableDayNightCycleForScene();
            UpdateLoadProgress("Initializing Character Selection...", 0.0f);
            _logger.LogInformation(">>> SelectCharacterScene LoadSceneContentWithProgress starting...");

            try
            {
                UpdateLoadProgress("Creating Select World...", 0.05f);
                _selectWorld = new SelectWorld();
                Controls.Add(_selectWorld);

                UpdateLoadProgress("Initializing Select World (Graphics)...", 0.1f);
                await _selectWorld.Initialize();
                World = _selectWorld;
                UpdateLoadProgress("Select World Initialized.", 0.35f);
                _logger.LogInformation("--- SelectCharacterScene: SelectWorld initialized and set.");

                if (_selectWorld.Terrain != null)
                {
                    _selectWorld.Terrain.AmbientLight = 0.6f;
                }

                // Create controller
                _characterController = new CharacterSelectionController(
                    MuGame.AppLoggerFactory.CreateLogger<CharacterSelectionController>());

                // Subscribe to events
                _characterController.CharacterClicked += OnControllerCharacterClicked;
                _characterController.CharacterDoubleClicked += OnControllerCharacterDoubleClicked;

                // Connect to world
                _selectWorld.SetController(_characterController);

                if (_characters.Any())
                {
                    UpdateLoadProgress("Preparing Character Data...", 0.40f);
                    await _characterController.CreateCharactersAsync(
                        _characters,
                        _selectWorld,
                        this,
                        _selectWorld.CharacterDisplayPosition,
                        _selectWorld.CharacterDisplayAngle);

                    if (_characters.Count > 0)
                    {
                        _currentCharacterIndex = 0;
                        _currentlySelectedCharacterName = _characters[0].Name;
                    }
                    else
                    {
                        _currentCharacterIndex = -1;
                    }

                    PositionNavigationButtons();
                    UpdateNavigationButtonState();

                    float characterCreationStartProgress = 0.45f;
                    float characterCreationEndProgress = 0.85f;
                    float totalCharacterProgressSpan = characterCreationEndProgress - characterCreationStartProgress;

                    if (_characters.Count > 0)
                    {
                        float progressPerCharacter = totalCharacterProgressSpan / _characters.Count;
                        for (int i = 0; i < _characters.Count; i++)
                        {
                            UpdateLoadProgress($"Configuring character {i + 1}/{_characters.Count}...", characterCreationStartProgress + (i + 1) * progressPerCharacter);
                        }
                    }
                    else
                    {
                        UpdateLoadProgress("No characters to configure.", characterCreationEndProgress);
                    }

                    UpdateLoadProgress("Character Objects Ready.", 0.90f);
                    _logger.LogInformation("--- SelectCharacterScene: Character creation finished.");
                }
                else
                {
                    _currentCharacterIndex = -1;
                    string message = "No characters found on this account.";
                    _logger.LogWarning("--- SelectCharacterScene: {Message}", message);
                    UpdateLoadProgress(message, 0.90f);
                    UpdateNavigationButtonState();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "!!! SelectCharacterScene: Error during world initialization or character creation.");
                UpdateLoadProgress("Error loading character selection.", 1.0f);
                UpdateNavigationButtonState();
            }
            finally
            {
                _initialLoadComplete = true;
                UpdateNavigationButtonState();
                UpdateLoadProgress("Character Selection Ready.", 1.0f);
                _logger.LogInformation("<<< SelectCharacterScene LoadSceneContentWithProgress finished.");
            }
        }

        public override void AfterLoad()
        {
            base.AfterLoad();
            _logger.LogInformation("SelectCharacterScene.AfterLoad() called.");
            if (_loadingScreen != null)
            {
                MuGame.ScheduleOnMainThread(() =>
                {
                    if (_loadingScreen != null)
                    {
                        Controls.Remove(_loadingScreen);
                        _loadingScreen.Dispose();
                        _loadingScreen = null;
                        if (_progressBar != null)
                        {
                            _progressBar.Visible = false;
                        }
                        PositionNavigationButtons();
                        UpdateNavigationButtonState();
                        _previousCharacterButton?.BringToFront();
                        _nextCharacterButton?.BringToFront();
                        _deleteCharacterButton?.BringToFront();
                        _createCharacterButton?.BringToFront();
                        _enterGameButton?.BringToFront();
                        _exitButton?.BringToFront();
                        Cursor?.BringToFront();
                        DebugPanel?.BringToFront();
                    }
                });
            }
        }

        protected override void OnScreenSizeChanged()
        {
            base.OnScreenSizeChanged();
            PositionNavigationButtons();
        }

        public override async Task Load()
        {
            if (Status == GameControlStatus.Initializing)
            {
                await LoadSceneContentWithProgress(UpdateLoadProgress);
            }
            else
            {
                _logger.LogDebug("SelectCharacterScene.Load() called outside of InitializeWithProgressReporting flow. Re-routing to progressive load.");
                await LoadSceneContentWithProgress(UpdateLoadProgress);
            }
        }


        public void CharacterSelected(string characterName)
        {
            if (_loadingScreen != null && _loadingScreen.Visible)
            {
                _logger.LogInformation("Character selection attempted while loading screen is visible. Ignoring.");
                return;
            }

            int matchedIndex = -1;
            for (int i = 0; i < _characters.Count; i++)
            {
                if (string.Equals(_characters[i].Name, characterName, StringComparison.Ordinal))
                {
                    matchedIndex = i;
                    break;
                }
            }

            if (matchedIndex < 0)
            {
                _logger.LogError("Character '{CharacterName}' selected, but not found in the character list.", characterName);
                MessageWindow.Show($"Error selecting character '{characterName}'.");
                return;
            }

            _selectedCharacterInfo = _characters[matchedIndex];
            _currentCharacterIndex = matchedIndex;
            _characterController?.SetActiveCharacter(_currentCharacterIndex);

            ClientConnectionState currentState = _networkManager.CurrentState;
            bool canSelect = currentState == ClientConnectionState.ConnectedToGameServer ||
                             currentState == ClientConnectionState.SelectingCharacter;

            if (!canSelect)
            {
                _logger.LogWarning("Character selection attempted but NetworkManager state is not ConnectedToGameServer or SelectingCharacter. State: {State}", currentState);
                MessageWindow.Show($"Cannot select character. Invalid network state: {currentState}");
                _selectedCharacterInfo = null;
                return;
            }

            _logger.LogInformation("Character '{CharacterName}' (Class: {Class}) selected in scene. Sending request...",
                                   _selectedCharacterInfo.Value.Name, _selectedCharacterInfo.Value.Class);

            DisableInteractionDuringSelection(characterName);
            _ = _networkManager.SendSelectCharacterRequestAsync(characterName);
        }

        public override void Dispose()
        {
            _logger.LogDebug("Disposing SelectCharacterScene.");
            UnsubscribeFromNetworkEvents();

            if (_characterController != null)
            {
                _characterController.CharacterClicked -= OnControllerCharacterClicked;
                _characterController.CharacterDoubleClicked -= OnControllerCharacterDoubleClicked;
                _characterController.Dispose();
                _characterController = null;
            }

            CloseCharacterCreationDialog();
            if (_loadingScreen != null)
            {
                Controls.Remove(_loadingScreen);
                _loadingScreen.Dispose();
                _loadingScreen = null;
            }
            RestoreDayNightCycle();
            base.Dispose();
        }

        private void SubscribeToNetworkEvents()
        {
            if (_networkManager != null)
            {
                _networkManager.EnteredGame += HandleEnteredGame;
                _networkManager.ErrorOccurred += HandleNetworkError;
                _networkManager.ConnectionStateChanged += HandleConnectionStateChange;
                _networkManager.CharacterListReceived += HandleCharacterListReceived;
                _networkManager.LogoutResponseReceived += HandleLogoutResponseReceived;
                _logger.LogDebug("SelectCharacterScene subscribed to NetworkManager events (including LogoutResponseReceived).");
            }
        }

        private void UnsubscribeFromNetworkEvents()
        {
            if (_networkManager != null)
            {
                _networkManager.EnteredGame -= HandleEnteredGame;
                _networkManager.ErrorOccurred -= HandleNetworkError;
                _networkManager.ConnectionStateChanged -= HandleConnectionStateChange;
                _networkManager.CharacterListReceived -= HandleCharacterListReceived;
                _networkManager.LogoutResponseReceived -= HandleLogoutResponseReceived;
                _logger.LogDebug("SelectCharacterScene unsubscribed from NetworkManager events.");
            }
        }

        private void HandleLogoutResponseReceived(object sender, LogOutType logoutType)
        {
            _logger.LogInformation("SelectCharacterScene.HandleLogoutResponseReceived: Type={Type}", logoutType);
            // Intentional logout handling is now done in HandleConnectionStateChange
            // which reacts to the Disconnected state after logout
        }

        private void HandleCharacterListReceived(object sender,
            List<(string Name, CharacterClassNumber Class, ushort Level, byte[] Appearance)> characters)
        {
            _logger.LogInformation("SelectCharacterScene.HandleCharacterListReceived: Received {Count} characters", characters?.Count ?? 0);
            
            MuGame.ScheduleOnMainThread(() =>
            {
                if (MuGame.Instance.ActiveScene != this)
                {
                    _logger.LogWarning("Scene changed, aborting character list refresh.");
                    return;
                }

                if (characters == null || characters.Count == 0)
                {
                    _logger.LogError("Received null or empty character list.");
                    return;
                }

                try
                {
                    _logger.LogInformation("Reloading SelectCharacterScene with updated list...");
                    var newScene = new SelectCharacterScene(characters, _networkManager);
                    MuGame.Instance.ChangeScene(newScene);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error reloading SelectCharacterScene.");
                }
            });
        }

        private void HandleEnteredGame(object sender, EventArgs e)
        {
            _logger.LogInformation(">>> SelectCharacterScene.HandleEnteredGame: Event received.");

            if (!_selectedCharacterInfo.HasValue)
            {
                _logger.LogError("!!! SelectCharacterScene.HandleEnteredGame: EnteredGame event received, but _selectedCharacterInfo is null. Cannot change to GameScene.");
                if (_loadingScreen != null)
                {
                    MuGame.ScheduleOnMainThread(() =>
                    {
                        Controls.Remove(_loadingScreen);
                        _loadingScreen.Dispose();
                        _loadingScreen = null;
                        EnableInteractionAfterSelection();
                    });
                }
                return;
            }

            var characterInfo = _selectedCharacterInfo.Value;
            _logger.LogInformation("--- SelectCharacterScene.HandleEnteredGame: Scheduling scene change to GameScene for character: {Name} ({Class})",
                characterInfo.Name, characterInfo.Class);

            MuGame.ScheduleOnMainThread(() =>
            {
                _logger.LogInformation("--- SelectCharacterScene.HandleEnteredGame (UI Thread): Executing scheduled scene change...");
                if (MuGame.Instance.ActiveScene == this)
                {
                    try
                    {
                        MuGame.Instance.ChangeScene(new GameScene(characterInfo, _networkManager));
                        _logger.LogInformation("<<< SelectCharacterScene.HandleEnteredGame (UI Thread): ChangeScene to GameScene call completed.");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "!!! SelectCharacterScene.HandleEnteredGame (UI Thread): Exception during ChangeScene to GameScene.");
                        EnableInteractionAfterSelection();
                    }
                }
                else
                {
                    _logger.LogWarning("<<< SelectCharacterScene.HandleEnteredGame (UI Thread): Scene changed before execution. Aborting change to GameScene.");
                }
            });
        }

        private void HandleNetworkError(object sender, string errorMessage)
        {
            MuGame.ScheduleOnMainThread(() =>
            {
                _logger.LogError("SelectCharacterScene received NetworkError: {Error}", errorMessage);
                MessageWindow.Show($"Network Error: {errorMessage}");
                EnableInteractionAfterSelection();
                if (MuGame.Instance.ActiveScene == this)
                {
                    MuGame.Instance.ChangeScene<LoginScene>();
                }
            });
        }

        private void HandleConnectionStateChange(object sender, ClientConnectionState newState)
        {
            MuGame.ScheduleOnMainThread(() =>
            {
                _logger.LogDebug("SelectCharacterScene received ConnectionStateChanged: {NewState}", newState);
                if (newState == ClientConnectionState.Disconnected)
                {
                    if (_isIntentionalLogout)
                    {
                        _logger.LogInformation("Intentional logout - returning to LoginScene.");
                    }
                    else
                    {
                        _logger.LogWarning("Disconnected while in character selection. Returning to LoginScene.");
                        MessageWindow.Show("Connection lost.");
                    }

                    if (MuGame.Instance.ActiveScene == this)
                    {
                        MuGame.Instance.ChangeScene<LoginScene>();
                    }
                }
            });
        }

        private void DisableInteractionDuringSelection(string characterName)
        {
            _isSelectionInProgress = true;
            if (_selectWorld != null)
            {
                _selectWorld.Interactive = false;
            }
            if (_characterController != null)
            {
                foreach (var player in _characterController.Characters)
                {
                    player.Interactive = false;
                }
                foreach (var label in _characterController.Labels.Values)
                {
                    label.Visible = false;
                }
            }
            if (_loadingScreen == null)
            {
                _loadingScreen = new LoadingScreenControl { Visible = true };
                Controls.Add(_loadingScreen);
            }
            _loadingScreen.Message = $"Entering game as {characterName}...";
            _loadingScreen.Progress = 0f;
            _loadingScreen.Visible = true;
            _loadingScreen.BringToFront();
            Cursor?.BringToFront();
            UpdateNavigationButtonState();
        }

        private void EnableInteractionAfterSelection()
        {
            _isSelectionInProgress = false;
            if (_selectWorld != null)
            {
                _selectWorld.Interactive = true;
            }
            if (_characterController != null)
            {
                foreach (var player in _characterController.Characters)
                {
                    player.Interactive = true;
                }
                // Labels visibility will be restored by controller's active character logic
                if (_characterController.ActiveCharacter != null)
                {
                    var activePlayer = _characterController.ActiveCharacter;
                    if (_characterController.Labels.TryGetValue(activePlayer, out var label))
                    {
                        label.Visible = true;
                    }
                }
            }
            _selectedCharacterInfo = null;

            if (_loadingScreen != null)
            {
                Controls.Remove(_loadingScreen);
                _loadingScreen.Dispose();
                _loadingScreen = null;
            }

            UpdateNavigationButtonState();
        }

        private void OnCreateCharacterButtonClick(object sender, EventArgs e)
        {
            if (_characterCreationDialog != null)
            {
                // Dialog already open
                return;
            }

            _logger.LogInformation("Opening character creation dialog...");

            // Create and show dialog
            _characterCreationDialog = new CharacterCreationDialog();
            _characterCreationDialog.CharacterCreateRequested += OnCharacterCreateRequested;
            _characterCreationDialog.CancelRequested += OnCharacterCreationCancelled;
            
            Controls.Add(_characterCreationDialog);
            _characterCreationDialog.BringToFront();
            Cursor?.BringToFront();

            // Disable interactions with world
            if (_selectWorld != null)
            {
                _selectWorld.Interactive = false;
            }
            if (_createCharacterButton != null)
            {
                _createCharacterButton.Enabled = false;
            }
        }

        private void OnCharacterCreateRequested(object sender, (string Name, CharacterClassNumber Class) data)
        {
            _logger.LogInformation("Character creation requested: Name={Name}, Class={Class}", data.Name, data.Class);

            // Close dialog
            CloseCharacterCreationDialog();

            // Send create character request
            var characterService = _networkManager?.GetCharacterService();
            if (characterService != null)
            {
                _ = characterService.SendCreateCharacterRequestAsync(data.Name, data.Class);
                MessageWindow.Show($"Creating character '{data.Name}'...\nPlease wait for server response.");
                
                // Request updated character list after a short delay
                _ = RefreshCharacterListAfterDelay();
            }
            else
            {
                _logger.LogError("CharacterService not available - cannot create character.");
                MessageWindow.Show("Error: Cannot create character at this time.");
            }
        }

        private async Task RefreshCharacterListAfterDelay()
        {
            // Wait for server to process creation
            await Task.Delay(2000);
            
            _logger.LogInformation("Requesting updated character list after creation...");
            var characterService = _networkManager?.GetCharacterService();
            if (characterService != null)
            {
                await characterService.RequestCharacterListAsync();
                // Note: The character list handler will update the scene
            }
        }

        private void OnCharacterCreationCancelled(object sender, EventArgs e)
        {
            _logger.LogInformation("Character creation cancelled.");
            CloseCharacterCreationDialog();
        }
        
        private void OnControllerCharacterClicked(object sender, string characterName)
        {
            _logger.LogInformation("Controller: Character '{Name}' clicked.", characterName);
            _currentlySelectedCharacterName = characterName;

            // Find index
            for (int i = 0; i < _characters.Count; i++)
            {
                if (_characters[i].Name == characterName)
                {
                    _currentCharacterIndex = i;
                    break;
                }
            }

            PositionNavigationButtons();
            UpdateNavigationButtonState();
        }

        private void OnControllerCharacterDoubleClicked(object sender, string characterName)
        {
            _logger.LogInformation("Controller: Character '{Name}' double-clicked.", characterName);
            CharacterSelected(characterName);
        }
        
        private void OnDeleteCharacterButtonClick(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_currentlySelectedCharacterName))
            {
                _logger.LogWarning("Delete button clicked but no character selected.");
                return;
            }
            
            string characterToDelete = _currentlySelectedCharacterName;
            _logger.LogInformation("Delete button clicked for character '{Name}'.", characterToDelete);
            
            // Create security code input dialog
            var securityCodeDialog = new CharacterDeletionDialog(characterToDelete);
            securityCodeDialog.DeleteConfirmed += (s, securityCode) =>
            {
                _logger.LogInformation("User confirmed deletion of '{Name}' with security code.", characterToDelete);
                var characterService = _networkManager?.GetCharacterService();
                if (characterService != null)
                {
                    _ = characterService.SendDeleteCharacterRequestAsync(characterToDelete, securityCode);
                    MessageWindow.Show($"Deleting character '{characterToDelete}'...\nPlease wait for server response.");
                    
                    // Clear selection
                    _currentlySelectedCharacterName = null;
                    UpdateNavigationButtonState();
                        }
                else
                {
                    _logger.LogError("CharacterService not available - cannot delete character.");
                    MessageWindow.Show("Error: Cannot delete character at this time.");
                }
                
                // Clean up dialog
                Controls.Remove(securityCodeDialog);
                securityCodeDialog.Dispose();
                
                // Re-enable world interaction
                if (_selectWorld != null)
                {
                    _selectWorld.Interactive = true;
                }
            };
            
            securityCodeDialog.CancelRequested += (s, args) =>
            {
                _logger.LogInformation("User cancelled deletion of '{Name}'.", characterToDelete);
                
                // Clean up dialog
                Controls.Remove(securityCodeDialog);
                securityCodeDialog.Dispose();
                
                // Re-enable world interaction
                if (_selectWorld != null)
                {
                    _selectWorld.Interactive = true;
                }
            };
            
            // Show dialog
            Controls.Add(securityCodeDialog);
            securityCodeDialog.BringToFront();
            Cursor?.BringToFront();
            
            // Disable world interaction while dialog is open
            if (_selectWorld != null)
            {
                _selectWorld.Interactive = false;
            }
        }

        private void CloseCharacterCreationDialog()
        {
            if (_characterCreationDialog != null)
            {
                _characterCreationDialog.CharacterCreateRequested -= OnCharacterCreateRequested;
                _characterCreationDialog.CancelRequested -= OnCharacterCreationCancelled;
                Controls.Remove(_characterCreationDialog);
                _characterCreationDialog.Dispose();
                _characterCreationDialog = null;
            }

            // Re-enable interactions
            if (_selectWorld != null)
            {
                _selectWorld.Interactive = true;
            }
            UpdateNavigationButtonState();
        }

        public override void Update(GameTime gameTime)
        {
            if (_loadingScreen != null && _loadingScreen.Visible)
            {
                _loadingScreen.Update(gameTime);
                Cursor?.Update(gameTime);
                DebugPanel?.Update(gameTime);
                return;
            }
            if (!_initialLoadComplete && Status == GameControlStatus.Initializing)
            {
                Cursor?.Update(gameTime);
                DebugPanel?.Update(gameTime);
                return;
            }

            // Handle character card mouse interaction
            UpdateCharacterCardInteraction();

            base.Update(gameTime);
        }

        private void UpdateCharacterCardInteraction()
        {
            if (_characterCardRects.Count == 0 || !_initialLoadComplete || Cursor == null)
                return;

            Point mousePos = new Point((int)Cursor.X, (int)Cursor.Y);
            int previousHovered = _hoveredCardIndex;
            _hoveredCardIndex = -1;

            var mouseState = MuGame.Instance.UiMouseState;
            bool mousePressed = mouseState.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed;
            bool mouseClicked = mousePressed && !_previousMousePressed;
            _previousMousePressed = mousePressed;

            // Only check cards if mouse is in the character list area
            if (!_characterListRect.Contains(mousePos))
                return;

            // Check if mouse is over any character card
            for (int i = 0; i < _characterCardRects.Count; i++)
            {
                if (_characterCardRects[i].Contains(mousePos))
                {
                    _hoveredCardIndex = i;
                    
                    if (previousHovered != _hoveredCardIndex)
                    {
                                }

                    // Handle click (on mouse release)
                    if (mouseClicked)
                    {
                        SelectCharacterByIndex(i);
                        _logger.LogInformation("Character card {Index} clicked: {Name}", i, _characters[i].Name);
                    }
                    break;
                }
            }

            if (previousHovered != _hoveredCardIndex && previousHovered != -1)
            {
                }
        }

        private void SelectCharacterByIndex(int index)
        {
            if (index < 0 || index >= _characters.Count || _characterController == null)
                return;

            _currentCharacterIndex = index;
            var character = _characters[index];
            _currentlySelectedCharacterName = character.Name;
            _characterController.SetActiveCharacter(_currentCharacterIndex);
            PositionNavigationButtons();
            UpdateNavigationButtonState();

            _logger.LogInformation("Character '{Name}' selected via card click.", character.Name);
        }

        public override void Draw(GameTime gameTime)
        {
            if (_loadingScreen != null && _loadingScreen.Visible)
            {
                GraphicsDevice.Clear(new Color(12, 12, 20));
                DrawBackground();
                _progressBar.Progress = _loadingScreen.Progress;
                _progressBar.StatusText = _loadingScreen.Message;
                _progressBar.Visible = true;
                _progressBar.Draw(gameTime);
                return;
            }

            // Draw 3D world first
            base.Draw(gameTime);

            // Draw modern UI overlay
            DrawModernUI(gameTime);
        }

        private void DrawModernUI(GameTime gameTime)
        {
            // Draw character info panel
            using (var scope = new SpriteBatchScope(
                GraphicsManager.Instance.Sprite, SpriteSortMode.Deferred,
                BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone,
                null, UiScaler.SpriteTransform))
            {
                var sb = GraphicsManager.Instance.Sprite;
                DrawCharacterPanel(sb);
            }

            // Draw cursor and debug panel on top of everything
            using (var scope = new SpriteBatchScope(
                GraphicsManager.Instance.Sprite, SpriteSortMode.Deferred,
                BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone,
                null, UiScaler.SpriteTransform))
            {
                Cursor?.Draw(gameTime);
                DebugPanel?.Draw(gameTime);
            }
        }

        private void DrawCharacterPanel(SpriteBatch sb)
        {
            var pixel = GraphicsManager.Instance.Pixel;
            var font = GraphicsManager.Instance.Font;
            if (pixel == null || font == null) return;

            int px = _characterPanelRect.X;
            int py = _characterPanelRect.Y;
            int pw = _characterPanelRect.Width;
            int ph = _characterPanelRect.Height;

            // === Panel background: only header + character list (NOT over buttons) ===
            int contentH = ph - _buttonSectionRect.Height;
            var contentRect = new Rectangle(px, py, pw, contentH);
            sb.Draw(pixel, contentRect, Theme.BgDark);
            UiDrawHelper.DrawVerticalGradient(sb,
                new Rectangle(px, py, pw, contentH / 2),
                Theme.BgMid, Theme.BgDark);

            // === Double-border: outer gold, inner dark (MU Online classic style) ===
            DrawDoubleBorder(sb, pixel, _characterPanelRect, Theme.BorderOuter, Theme.BorderInner);

            // === HEADER: dark crimson with gold text ===
            var headerRect = new Rectangle(px + 2, py + 2, pw - 4, HEADER_HEIGHT);
            UiDrawHelper.DrawHorizontalGradient(sb, headerRect, Theme.CrimsonDim, Theme.BgDark);
            // Gold separator line under header
            sb.Draw(pixel, new Rectangle(px + 2, py + HEADER_HEIGHT + 1, pw - 4, 1), Theme.GoldDim);
            sb.Draw(pixel, new Rectangle(px + 2, py + HEADER_HEIGHT + 2, pw - 4, 1), Theme.BorderOuter * 0.5f);

            // Corner ornaments in header
            DrawCornerOrnaments(sb, pixel, headerRect, Theme.Gold);

            // Header text: "CHARACTERS" centered
            const string headerText = "CHARACTERS";
            float hScale = 0.72f;
            Vector2 hSize = font.MeasureString(headerText) * hScale;
            var hPos = new Vector2(px + (pw - hSize.X) / 2f, py + (HEADER_HEIGHT - hSize.Y) / 2f);
            sb.DrawString(font, headerText, hPos + new Vector2(0, 1), Color.Black * 0.8f, 0f, Vector2.Zero, hScale, SpriteEffects.None, 0f);
            sb.DrawString(font, headerText, hPos, Theme.GoldBright, 0f, Vector2.Zero, hScale, SpriteEffects.None, 0f);

            // === CHARACTER LIST AREA ===
            // Subtle inner rect background
            sb.Draw(pixel, new Rectangle(_characterListRect.X + 2, _characterListRect.Y, _characterListRect.Width - 4, _characterListRect.Height), Theme.BgDarkest * 0.7f);

            // Draw character cards
            for (int i = 0; i < _characters.Count && i < _characterCardRects.Count; i++)
                DrawCharacterCard(sb, pixel, font, i, _characterCardRects[i], _characters[i]);

            // === BUTTON SECTION: thin gold separator top ===
            sb.Draw(pixel, new Rectangle(px + 2, _buttonSectionRect.Y, pw - 4, 1), Theme.GoldDim);

            // === Draw MU-style gold borders on top of buttons ===
            DrawButtonBorders(sb, pixel);
        }

        /// <summary>Draws MU Online classic double-border: outer gold line, 1px gap, inner dark line.</summary>
        private static void DrawDoubleBorder(SpriteBatch sb, Texture2D px, Rectangle r, Color outer, Color inner)
        {
            // Outer gold border (1px)
            sb.Draw(px, new Rectangle(r.X, r.Y, r.Width, 1), outer);
            sb.Draw(px, new Rectangle(r.X, r.Bottom - 1, r.Width, 1), outer);
            sb.Draw(px, new Rectangle(r.X, r.Y, 1, r.Height), outer);
            sb.Draw(px, new Rectangle(r.Right - 1, r.Y, 1, r.Height), outer);
            // Inner dark border (1px, inset by 2)
            sb.Draw(px, new Rectangle(r.X + 2, r.Y + 2, r.Width - 4, 1), inner);
            sb.Draw(px, new Rectangle(r.X + 2, r.Bottom - 3, r.Width - 4, 1), inner);
            sb.Draw(px, new Rectangle(r.X + 2, r.Y + 2, 1, r.Height - 4), inner);
            sb.Draw(px, new Rectangle(r.Right - 3, r.Y + 2, 1, r.Height - 4), inner);
        }

        /// <summary>Draws small corner ornament marks (4px L-shapes in gold).</summary>
        private static void DrawCornerOrnaments(SpriteBatch sb, Texture2D px, Rectangle r, Color c)
        {
            int s = 5; // size of ornament arm
            // Top-left
            sb.Draw(px, new Rectangle(r.X, r.Y, s, 1), c);
            sb.Draw(px, new Rectangle(r.X, r.Y, 1, s), c);
            // Top-right
            sb.Draw(px, new Rectangle(r.Right - s, r.Y, s, 1), c);
            sb.Draw(px, new Rectangle(r.Right - 1, r.Y, 1, s), c);
            // Bottom-left
            sb.Draw(px, new Rectangle(r.X, r.Bottom - 1, s, 1), c);
            sb.Draw(px, new Rectangle(r.X, r.Bottom - s, 1, s), c);
            // Bottom-right
            sb.Draw(px, new Rectangle(r.Right - s, r.Bottom - 1, s, 1), c);
            sb.Draw(px, new Rectangle(r.Right - 1, r.Bottom - s, 1, s), c);
        }

        private void DrawCharacterCard(SpriteBatch sb, Texture2D pixel, SpriteFont font, int index, Rectangle cardRect, (string Name, CharacterClassNumber Class, ushort Level, byte[] Appearance) character)
        {
            bool isSelected = _currentCharacterIndex == index;
            bool isHovered = _hoveredCardIndex == index;

            // Card background
            Color bgColor = isSelected ? Theme.BgCardSelected : (isHovered ? Theme.BgCardHover : Theme.BgCard);
            sb.Draw(pixel, cardRect, bgColor);

            // Left accent bar (gold when selected, dim when hovered, invisible otherwise)
            if (isSelected)
            {
                sb.Draw(pixel, new Rectangle(cardRect.X, cardRect.Y, 3, cardRect.Height), Theme.Gold);
                // Inner glow next to bar
                sb.Draw(pixel, new Rectangle(cardRect.X + 3, cardRect.Y, 1, cardRect.Height), Theme.GoldGlow);
            }
            else if (isHovered)
            {
                sb.Draw(pixel, new Rectangle(cardRect.X, cardRect.Y, 2, cardRect.Height), Theme.GoldDim);
            }

            // Card border (thin gold when selected, very dark otherwise)
            Color borderColor = isSelected ? Theme.GoldDim : new Color(30, 25, 40);
            sb.Draw(pixel, new Rectangle(cardRect.X, cardRect.Y, cardRect.Width, 1), borderColor);
            sb.Draw(pixel, new Rectangle(cardRect.X, cardRect.Bottom - 1, cardRect.Width, 1), borderColor);
            sb.Draw(pixel, new Rectangle(cardRect.X, cardRect.Y, 1, cardRect.Height), borderColor);
            sb.Draw(pixel, new Rectangle(cardRect.Right - 1, cardRect.Y, 1, cardRect.Height), borderColor);

            // Text content
            int textX = cardRect.X + 12;
            int textY = cardRect.Y + 8;

            // Name
            Color nameColor = isSelected ? Theme.TextGoldBright : (isHovered ? Theme.TextGold : Theme.TextWhite);
            const float nameScale = 0.68f;
            sb.DrawString(font, character.Name, new Vector2(textX + 1, textY + 1), Color.Black * 0.8f, 0f, Vector2.Zero, nameScale, SpriteEffects.None, 0f);
            sb.DrawString(font, character.Name, new Vector2(textX, textY), nameColor, 0f, Vector2.Zero, nameScale, SpriteEffects.None, 0f);

            // Class • Level
            string classText = $"{CharacterClassDatabase.GetClassName(character.Class)}   Lv.{character.Level}";
            Color infoColor = isSelected ? Theme.Gold : Theme.TextGray;
            const float infoScale = 0.58f;
            int infoY = textY + 20;
            sb.DrawString(font, classText, new Vector2(textX + 1, infoY + 1), Color.Black * 0.7f, 0f, Vector2.Zero, infoScale, SpriteEffects.None, 0f);
            sb.DrawString(font, classText, new Vector2(textX, infoY), infoColor, 0f, Vector2.Zero, infoScale, SpriteEffects.None, 0f);

            // Small dot separator between name and class
            if (isSelected)
            {
                // Right-side level badge: "Lv.XX" box
                string lvBadge = $"Lv.{character.Level}";
                Vector2 lvSize = font.MeasureString(lvBadge) * 0.52f;
                int badgeX = cardRect.Right - (int)lvSize.X - 10;
                int badgeY = cardRect.Y + (cardRect.Height - (int)lvSize.Y) / 2;
                sb.Draw(pixel, new Rectangle(badgeX - 3, badgeY - 2, (int)lvSize.X + 6, (int)lvSize.Y + 4), Theme.CrimsonDim);
                sb.Draw(pixel, new Rectangle(badgeX - 3, badgeY - 2, (int)lvSize.X + 6, 1), Theme.Gold * 0.6f);
                sb.Draw(pixel, new Rectangle(badgeX - 3, badgeY + (int)lvSize.Y + 2, (int)lvSize.X + 6, 1), Theme.Gold * 0.6f);
                sb.DrawString(font, lvBadge, new Vector2(badgeX, badgeY), Theme.GoldBright, 0f, Vector2.Zero, 0.52f, SpriteEffects.None, 0f);
            }
        }

        private new void DrawBackground()
        {
            if (_backgroundTexture == null) return;

            using var scope = new SpriteBatchScope(
                GraphicsManager.Instance.Sprite, SpriteSortMode.Deferred,
                BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone,
                null, UiScaler.SpriteTransform);

            GraphicsManager.Instance.Sprite.Draw(_backgroundTexture,
                new Rectangle(0, 0, UiScaler.VirtualSize.X, UiScaler.VirtualSize.Y), Color.White);
        }

        private void DrawButtonBorders(SpriteBatch sb, Texture2D pixel)
        {
            // Draw gold ornament borders on top of each visible button
            DrawMuButtonBorder(sb, pixel, _enterGameButton);
            DrawMuButtonBorder(sb, pixel, _createCharacterButton);
            DrawMuButtonBorder(sb, pixel, _deleteCharacterButton);
            DrawMuButtonBorder(sb, pixel, _exitButton);
        }

        private static void DrawMuButtonBorder(SpriteBatch sb, Texture2D pixel, ButtonControl btn)
        {
            if (btn == null || !btn.Visible) return;
            var r = new Rectangle(btn.X, btn.Y, btn.ViewSize.X, btn.ViewSize.Y);

            // Outer gold border
            Color borderColor = btn.IsMouseOver ? Theme.GoldBright : Theme.GoldDim;
            sb.Draw(pixel, new Rectangle(r.X, r.Y, r.Width, 1), borderColor);
            sb.Draw(pixel, new Rectangle(r.X, r.Bottom - 1, r.Width, 1), borderColor);
            sb.Draw(pixel, new Rectangle(r.X, r.Y, 1, r.Height), borderColor);
            sb.Draw(pixel, new Rectangle(r.Right - 1, r.Y, 1, r.Height), borderColor);
            // Corner ornaments
            DrawCornerOrnaments(sb, pixel, r, borderColor);
        }

        private void OnEnterGameButtonClick(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_currentlySelectedCharacterName))
            {
                MessageWindow.Show("Please select a character first.");
                return;
            }

            // Enter game with selected character
            var matchedIndex = _characters.FindIndex(c => c.Name.Equals(_currentlySelectedCharacterName, StringComparison.OrdinalIgnoreCase));
            if (matchedIndex < 0)
            {
                _logger.LogWarning("Character '{Name}' not found in character list.", _currentlySelectedCharacterName);
                MessageWindow.Show($"Error: Character '{_currentlySelectedCharacterName}' not found.");
                return;
            }

            _selectedCharacterInfo = _characters[matchedIndex];
            _currentCharacterIndex = matchedIndex;
            _characterController?.SetActiveCharacter(_currentCharacterIndex);

            ClientConnectionState currentState = _networkManager.CurrentState;
            bool canSelect = currentState == ClientConnectionState.ConnectedToGameServer ||
                             currentState == ClientConnectionState.SelectingCharacter;

            if (!canSelect)
            {
                _logger.LogWarning("Character selection attempted but NetworkManager state is not ConnectedToGameServer or SelectingCharacter. State: {State}", currentState);
                MessageWindow.Show($"Cannot select character. Invalid network state: {currentState}");
                _selectedCharacterInfo = null;
                return;
            }

            _logger.LogInformation("Character '{CharacterName}' (Class: {Class}) selected in scene. Sending request...",
                                   _selectedCharacterInfo.Value.Name, _selectedCharacterInfo.Value.Class);

            DisableInteractionDuringSelection(_currentlySelectedCharacterName);
            _ = _networkManager.SendSelectCharacterRequestAsync(_currentlySelectedCharacterName);
        }

        private void OnExitButtonClick(object sender, EventArgs e)
        {
            _logger.LogInformation("Exit button clicked - returning to login.");
            _isIntentionalLogout = true;
            _ = _networkManager.GetCharacterService().SendLogoutRequestAsync(LogOutType.BackToServerSelection);
        }
    }
}
