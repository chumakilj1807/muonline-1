using Client.Main.Controls.UI;
using Client.Main.Core.Client;
using Client.Main.Scenes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input.Touch;
using System.Collections.Generic;

namespace Client.Main.Controls.UI.Android
{
    /// <summary>
    /// Root container for all Android-specific HUD elements.
    /// Added to GameScene instead of the desktop PC UI panels.
    /// Also holds a static reference so sub-controls can call back into it.
    /// </summary>
    public class AndroidHUD : UIControl
    {
        public static AndroidHUD Current { get; private set; }

        /// <summary>Touch IDs already handled this frame; cleared each Update tick.</summary>
        public static readonly HashSet<int> ConsumedTouchIds = new();

        private VirtualJoystick _joystick;
        private AndroidSkillBar _skillBar;
        private AndroidSkillMenu _skillMenu;
        private AndroidActionBar _actionBar;
        private CharacterHPManaBar _hpBar;
        private AreaTargetSelector _areaSelector;

        private Scenes.GameSceneSkillController _skillController;

        public AndroidHUD()
        {
            Interactive = false;
            Visible = true;
            Current = this;
        }

        public void SetSkillController(Scenes.GameSceneSkillController controller)
        {
            _skillController = controller;
        }

        public override async System.Threading.Tasks.Task Load()
        {
            _areaSelector = new AreaTargetSelector();
            Controls.Add(_areaSelector);
            await _areaSelector.Load();

            _skillMenu = new AndroidSkillMenu();
            Controls.Add(_skillMenu);
            await _skillMenu.Load();
            _skillMenu.SkillAssigned = (skill, slot) => _skillBar?.AssignSkill(skill, slot);

            _skillBar = new AndroidSkillBar(_areaSelector, _skillMenu);
            Controls.Add(_skillBar);
            await _skillBar.Load();

            _actionBar = new AndroidActionBar();
            Controls.Add(_actionBar);
            await _actionBar.Load();
            WireActionBar();

            _joystick = new VirtualJoystick();
            Controls.Add(_joystick);
            await _joystick.Load();

            _hpBar = new CharacterHPManaBar();
            Controls.Add(_hpBar);
            await _hpBar.Load();

            await base.Load();
        }

        private void WireActionBar()
        {
            _actionBar.OnSkillsButton = () =>
            {
                if (_skillMenu.Visible) _skillMenu.Close();
                else _skillMenu.Open();
            };

            _actionBar.OnTeleportButton = () =>
            {
                // Simulate M key press to open map / trigger teleport
                var state = MuGame.Network?.GetCharacterState();
                var teleport = state != null ? FindTeleportSkill(state) : null;
                if (teleport != null)
                    InvokeAreaSkill(teleport, MuGame.Instance.ActiveScene is Scenes.GameScene gs
                        ? gs.Hero?.Location ?? Vector2.Zero : Vector2.Zero);
                else
                    SimulateKey(Microsoft.Xna.Framework.Input.Keys.M);
            };

            _actionBar.OnInventoryButton = () => SimulateKey(Microsoft.Xna.Framework.Input.Keys.I);
            _actionBar.OnStatsButton = () => SimulateKey(Microsoft.Xna.Framework.Input.Keys.C);
            _actionBar.OnGuildButton = () => SimulateKey(Microsoft.Xna.Framework.Input.Keys.G);
            _actionBar.OnPartyButton = () => SimulateKey(Microsoft.Xna.Framework.Input.Keys.P);
        }

        private SkillEntryState FindTeleportSkill(Core.Client.CharacterState state)
        {
            foreach (var s in state.GetSkills())
                if (s.SkillId == 6) return s; // TeleportSkillId = 6
            return null;
        }

        private static void SimulateKey(Microsoft.Xna.Framework.Input.Keys key)
        {
            // Queue a synthetic key event for GameSceneHotkeys to pick up
            MuGame.Instance?.QueueSyntheticKey(key);
        }

        public override void Update(GameTime gameTime)
        {
            // Clear consumed touch set each frame BEFORE child controls process touches
            ConsumedTouchIds.Clear();

            // Area selector takes priority (full-screen input capture)
            if (_areaSelector != null && _areaSelector.IsActive)
            {
                _areaSelector.Update(gameTime);
                // When area selector is active, joystick still works
                _joystick?.Update(gameTime);
                _hpBar?.Update(gameTime);
                return;
            }

            // Skill menu takes full input when open
            if (_skillMenu != null && _skillMenu.Visible)
            {
                _skillMenu.Update(gameTime);
                _hpBar?.Update(gameTime);
                return;
            }

            base.Update(gameTime); // updates all children
        }

        public override void Draw(GameTime gameTime)
        {
            if (!Visible) return;
            // HP bar is always drawn (world-space, no clip needed)
            _hpBar?.Draw(gameTime);

            // Joystick drawn on top of game but under menus
            _joystick?.Draw(gameTime);
            _actionBar?.Draw(gameTime);
            _skillBar?.Draw(gameTime);

            // Area selector overlay
            if (_areaSelector?.IsActive == true)
                _areaSelector.Draw(gameTime);

            // Skill menu (topmost)
            if (_skillMenu?.Visible == true)
                _skillMenu.Draw(gameTime);
        }

        // ──────────────── Skill invocation (called by sub-controls) ────────────────

        public void InvokeAreaSkill(SkillEntryState skill, Vector2 targetTile)
        {
            if (_skillController == null) return;
            _skillController.AndroidUseAreaSkill(skill, targetTile);
        }

        public void InvokeDirectSkill(SkillEntryState skill)
        {
            if (_skillController == null) return;
            _skillController.AndroidUseSkillOnNearestTarget(skill);
        }

        protected override void Dispose(bool disposing)
        {
            if (Current == this) Current = null;
            base.Dispose(disposing);
        }
    }
}
