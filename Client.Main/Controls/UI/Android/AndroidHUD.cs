using Client.Data.BMD;
using Client.Main.Controllers;
using Client.Main.Controls.UI;
using Client.Main.Core.Client;
using Client.Main.Core.Utilities;
using Client.Main.Graphics;
using Client.Main.Helpers;
using Client.Main.Objects;
using Client.Main.Scenes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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

        private SkillEntryState _pendingTargetSkill;
        private int _targetTouchConsumedId = -1; // touch ID to keep consuming after target tap
        private Texture2D _pixel;

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
            if (_joystick != null) return;

            _pixel = new Texture2D(MuGame.Instance.GraphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });

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
        // Warrior skills injected for DL
        private static readonly ushort[] DarkLordWarriorSkills = { 21, 22, 19, 20, 18 };

        private static bool IsDarkLordSkillSet(System.Collections.Generic.List<SkillEntryState> skills,
            MUnique.OpenMU.Network.Packets.CharacterClassNumber cls)
        {
            if (cls == MUnique.OpenMU.Network.Packets.CharacterClassNumber.DarkLord ||
                cls == MUnique.OpenMU.Network.Packets.CharacterClassNumber.LordEmperor)
                return true;
            // Fallback: detect by having DL-specific skill IDs (class may arrive after skills)
            return skills.Any(s => s.SkillId >= 55 && s.SkillId <= 79);
        }

        private void TryAutoPopulateSkillSlots()
        {
            if (_slotsAutoPopulated || _skillBar == null) return;

            var state = MuGame.Network?.GetCharacterState();
            if (state == null) return;

            var skills = state.GetSkills().ToList();

            // Inject warrior skills for DL; do BEFORE count check so DL gets slots even with 0 server skills
            if (IsDarkLordSkillSet(skills, state.Class))
            {
                var existingIds = new System.Collections.Generic.HashSet<ushort>(skills.Select(s => s.SkillId));
                foreach (var sid in DarkLordWarriorSkills)
                    if (!existingIds.Contains(sid))
                        skills.Add(new SkillEntryState { SkillId = sid, SkillLevel = 1 });
            }

            if (skills.Count == 0) return;

            // For DL: preferred slot order (slot 0 = Fire Burst, then Area skills, then Target, then Self)
            bool isDLForSlots = IsDarkLordSkillSet(skills, state.Class);
            if (isDLForSlots)
            {
                // Preferred DL slot order: Fire Burst → Fire Slash → Earthshake → Electric Spike → rest
                ushort[] dlPreferred = { 61, 55, 62, 65, 56, 78, 238, 66, 57, 79, 230, 237, 21, 22, 19, 20 };
                int slot = 0;
                var remaining = new System.Collections.Generic.List<SkillEntryState>(skills);
                foreach (var wantedId in dlPreferred)
                {
                    if (slot >= 4) break;
                    var s = remaining.FirstOrDefault(x => x.SkillId == wantedId);
                    if (s != null) { _skillBar.AssignSkill(s, slot++); remaining.Remove(s); }
                }
                // Fill remaining slots with whatever's left (offensive first)
                var rest = remaining
                    .OrderBy(s => SkillDefinitions.GetSkillType(s.SkillId) == SkillType.Self ? 1 : 0)
                    .ThenBy(s => s.SkillId);
                foreach (var s in rest)
                {
                    if (slot >= 4) break;
                    _skillBar.AssignSkill(s, slot++);
                }
            }
            else
            {
                var ordered = skills
                    .OrderBy(s => SkillDefinitions.GetSkillType(s.SkillId) == SkillType.Self ? 1 : 0)
                    .ThenBy(s => s.SkillId)
                    .ToList();
                for (int i = 0; i < 4 && i < ordered.Count; i++)
                    _skillBar.AssignSkill(ordered[i], i);
            }

            _slotsAutoPopulated = true;
        }

        /// <summary>Enter tap-to-target mode for a Target-type skill.</summary>
        public void BeginTargetSelection(SkillEntryState skill)
        {
            _pendingTargetSkill = skill;
        }

        private void CancelTargetSelection()
        {
            _pendingTargetSkill = null;
        }

        private void HandleTargetSelectionTouch(GameTime gameTime)
        {
            var touches = MuGame.Instance.Touch;
            foreach (var touch in touches)
            {
                if (AndroidHUD.ConsumedTouchIds.Contains(touch.Id)) continue;
                if (touch.State != TouchLocationState.Pressed) continue;

                AndroidHUD.ConsumedTouchIds.Add(touch.Id);
                _targetTouchConsumedId = touch.Id;
                _skillController?.ConsumeMouseInput();

                // Tap on the cancel banner at top = cancel without firing
                if (touch.Position.Y < 80)
                {
                    _pendingTargetSkill = null;
                    return;
                }

                // Use skill controller's path — same as AndroidUseSkillOnNearestTarget which works
                var skillId = _pendingTargetSkill?.SkillId ?? 0;
                var monster = _skillController?.GetNearestMonsterToHero();
                Console.WriteLine($"[HUD] TargetTap: skillId={skillId} ctrl={_skillController != null} monster={monster?.NetworkId.ToString() ?? "NULL"}");
                if (monster != null)
                {
                    var result = false;
                    try { result = _skillController.AndroidUseSkillOnMonster(_pendingTargetSkill, monster); } catch (Exception ex) { Console.WriteLine($"[HUD] SkillError: {ex.Message}"); }
                    Console.WriteLine($"[HUD] UseSkillResult={result}");
                }

                _pendingTargetSkill = null;
                return;
            }
        }

        public override void Update(GameTime gameTime)
        {
            try
            {
                ConsumedTouchIds.Clear();

                // Keep consuming the touch that triggered target-mode exit until finger lifts
                if (_targetTouchConsumedId >= 0)
                {
                    bool stillDown = MuGame.Instance.Touch.Any(
                        t => t.Id == _targetTouchConsumedId && t.State != TouchLocationState.Released);
                    if (stillDown)
                    {
                        ConsumedTouchIds.Add(_targetTouchConsumedId);
                        _skillController?.ConsumeMouseInput();
                    }
                    else
                        _targetTouchConsumedId = -1;
                }

                // Try to auto-fill slots once skills arrive from server
                if (!_slotsAutoPopulated)
                    TryAutoPopulateSkillSlots();

                if (_pendingTargetSkill != null)
                {
                    HandleTargetSelectionTouch(gameTime);
                    _joystick?.Update(gameTime);
                    _hpBar?.Update(gameTime);
                    return;
                }

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

            if (_pendingTargetSkill != null)
                DrawTargetSelectionOverlay(gameTime);
        }

        private void DrawTargetSelectionOverlay(GameTime gameTime)
        {
            if (_pixel == null) return;
            var vp = MuGame.Instance.GraphicsDevice.Viewport;
            var sb = GraphicsManager.Instance.Sprite;
            var font = GraphicsManager.Instance.Font;

            using (new SpriteBatchScope(sb, SpriteSortMode.Deferred, BlendState.NonPremultiplied,
                SamplerState.LinearClamp, DepthStencilState.None))
            {
                // Semi-transparent banner at top
                sb.Draw(_pixel, new Rectangle(0, 0, vp.Width, 70), new Color(0, 0, 0, 160));
                if (font != null)
                {
                    string skillName = SkillDatabase.GetSkillName(_pendingTargetSkill.SkillId);
                    string msg = $"TAP TARGET: {skillName}";
                    var size = font.MeasureString(msg) * 0.8f;
                    var pos = new Vector2((vp.Width - size.X) / 2f, (70 - size.Y) / 2f);
                    sb.DrawString(font, msg, pos + Vector2.One, Color.Black * 0.7f, 0, Vector2.Zero, 0.8f, SpriteEffects.None, 0);
                    sb.DrawString(font, msg, pos, Color.Yellow, 0, Vector2.Zero, 0.8f, SpriteEffects.None, 0);

                    // Cancel hint
                    string cancel = "[TAP EMPTY AREA TO CANCEL]";
                    var cancelSize = font.MeasureString(cancel) * 0.45f;
                    var cancelPos = new Vector2((vp.Width - cancelSize.X) / 2f, 45f);
                    sb.DrawString(font, cancel, cancelPos, Color.Gray, 0, Vector2.Zero, 0.45f, SpriteEffects.None, 0);
                }

                // Draw circles around visible monsters
                if (MuGame.Instance.ActiveScene is Scenes.GameScene gs && gs.World is Controls.WorldControl world)
                {
                    var cam = Camera.Instance;
                    foreach (var monster in world.Monsters)
                    {
                        if (monster == null || monster.IsDead) continue;
                        var screen = vp.Project(monster.Position, cam.Projection, cam.View, Matrix.Identity);
                        if (screen.Z < 0 || screen.Z > 1) continue;
                        DrawScreenCircle(sb, new Vector2(screen.X, screen.Y), 50, new Color(255, 80, 30, 180));
                    }
                }
            }
        }

        private void DrawScreenCircle(SpriteBatch sb, Vector2 center, int radius, Color color)
        {
            int thickness = 3;
            // Draw as 4 lines forming a cross-hair square around target
            sb.Draw(_pixel, new Rectangle((int)center.X - radius, (int)center.Y - radius, radius * 2, thickness), color);
            sb.Draw(_pixel, new Rectangle((int)center.X - radius, (int)center.Y + radius, radius * 2, thickness), color);
            sb.Draw(_pixel, new Rectangle((int)center.X - radius, (int)center.Y - radius, thickness, radius * 2), color);
            sb.Draw(_pixel, new Rectangle((int)center.X + radius, (int)center.Y - radius, thickness, radius * 2 + thickness), color);
        }

        // ──────────────── Skill invocation (called by sub-controls) ────────────────

        public void InvokeDirectSkillOnMonster(SkillEntryState skill, MonsterObject monster)
        {
            if (_skillController == null) return;
            _skillController.AndroidUseSkillOnMonster(skill, monster);
        }

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
            if (_pixel != null) { _pixel.Dispose(); _pixel = null; }
            base.Dispose();
        }
    }
}
