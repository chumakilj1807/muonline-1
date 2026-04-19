using Client.Data.BMD;
using Client.Main.Controllers;
using Client.Main.Controls.UI;
using Client.Main.Core.Client;
using Client.Main.Core.Utilities;
using Client.Main.Scenes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input.Touch;
using System.Collections.Generic;
using System.Linq;

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
        private bool _slotsAutoPopulated;

        public AndroidHUD()
        {
            Interactive = false;
            Visible = true;
            Current = this;
        }

        internal void SetSkillController(Scenes.GameSceneSkillController controller)
        {
            _skillController = controller;
        }

        public override async System.Threading.Tasks.Task Load()
        {
            if (_joystick != null) return; // already loaded

            _areaSelector = new AreaTargetSelector();
            Controls.Add(_areaSelector);
            await _areaSelector.Load();

            _skillMenu = new AndroidSkillMenu();
            Controls.Add(_skillMenu);
            await _skillMenu.Load();
            _skillMenu.SkillAssigned = (skill, slot) =>
            {
                _skillBar?.AssignSkill(skill, slot);
                _slotsAutoPopulated = true; // user overrode; don't re-auto-fill
            };

            _skillBar = new AndroidSkillBar(_skillMenu);
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

            // Action bar and skill bar always on top of HUD
            _actionBar.BringToFront();
            _skillBar.BringToFront();
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
                if (s.SkillId == 6) return s;
            return null;
        }

        private static void SimulateKey(Microsoft.Xna.Framework.Input.Keys key)
        {
            MuGame.Instance?.QueueSyntheticKey(key);
        }

        /// <summary>
        /// Auto-fills skill slots with the first 4 skills from CharacterState.
        /// Priority: offensive skills first (Area/Target), then Self buffs.
        /// Called once after character skills arrive from server.
        /// </summary>
        private void TryAutoPopulateSkillSlots()
        {
            if (_slotsAutoPopulated || _skillBar == null) return;

            var state = MuGame.Network?.GetCharacterState();
            if (state == null) return;

            var skills = state.GetSkills().ToList();
            if (skills.Count == 0) return;

            // Sort: Area/Target first (offensive), then Self (buffs)
            var ordered = skills
                .OrderBy(s => SkillDefinitions.GetSkillType(s.SkillId) == SkillType.Self ? 1 : 0)
                .ThenBy(s => s.SkillId)
                .ToList();

            for (int i = 0; i < 4 && i < ordered.Count; i++)
                _skillBar.AssignSkill(ordered[i], i);

            _slotsAutoPopulated = true;
        }

        public override void Update(GameTime gameTime)
        {
            try
            {
                ConsumedTouchIds.Clear();

                // Try to auto-fill slots once skills arrive from server
                if (!_slotsAutoPopulated)
                    TryAutoPopulateSkillSlots();

                if (_areaSelector != null && _areaSelector.IsActive)
                {
                    _areaSelector.Update(gameTime);
                    _joystick?.Update(gameTime);
                    _hpBar?.Update(gameTime);
                    return;
                }

                if (_skillMenu != null && _skillMenu.Visible)
                {
                    _skillMenu.Update(gameTime);
                    _hpBar?.Update(gameTime);
                    return;
                }

                _joystick?.Update(gameTime);
                _skillBar?.Update(gameTime);
                _actionBar?.Update(gameTime);
                _hpBar?.Update(gameTime);
            }
            catch { }
        }

        public override void Draw(GameTime gameTime)
        {
            if (!Visible) return;
            _hpBar?.Draw(gameTime);
            _joystick?.Draw(gameTime);
            _actionBar?.Draw(gameTime);
            _skillBar?.Draw(gameTime);

            if (_areaSelector?.IsActive == true)
                _areaSelector.Draw(gameTime);

            if (_skillMenu?.Visible == true)
                _skillMenu.Draw(gameTime);
        }

        // ──────────────── Skill invocation (called by sub-controls) ────────────────

        public void InvokeAreaSkill(SkillEntryState skill, Vector2 targetTile)
        {
            if (_skillController == null) return;
            // For area skills fired from the joystick direction, project the target ahead of hero
            if (MuGame.Instance.ActiveScene is Scenes.GameScene gs && gs.Hero != null)
            {
                var hero = gs.Hero;
                // Aim one tile ahead in facing direction so skill hits in front
                var iso = new Vector2(
                    (float)System.Math.Cos(hero.Angle.Z),
                    (float)System.Math.Sin(hero.Angle.Z));
                targetTile = hero.Location + iso * 3f; // 3 tiles ahead
            }
            _skillController.AndroidUseAreaSkill(skill, targetTile);
        }

        public void InvokeDirectSkill(SkillEntryState skill)
        {
            if (_skillController == null) return;
            _skillController.AndroidUseSkillOnNearestTarget(skill);
        }

        public void InvokeSelfSkill(SkillEntryState skill)
        {
            if (_skillController == null) return;
            _skillController.AndroidUseSelfSkill(skill);
        }

        public override void Dispose()
        {
            if (Current == this) Current = null;
            base.Dispose();
        }
    }
}
