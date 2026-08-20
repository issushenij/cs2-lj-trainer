using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Raylib_cs;
using LJTrainer.Core;

namespace LJTrainer.UI
{
    public class ProfileModal
    {
        public bool IsOpen { get; set; } = false;
        private int _activeTab = 1; // 1 = Jump PBs & CS2 Live, 2 = KZ Maps Table, 0 = Analytics & Training Plan

        // Steam Avatar Texture cache
        private Texture2D? _avatarTexture = null;
        private bool _avatarLoaded = false;
        private string _lastLoadedAvatarSid = "";

        // Rank & PB History Timeline Chain Popups
        private bool _showRankHistoryModal = false;
        private bool _showPBHistoryModal = false;
        private float _pbHistoryScrollY = 0f;
        private string _selectedPbHistoryJumpType = "All";

        // Raw Console Log Inspection Modal
        private bool _showLogModal = false;
        private string _selectedLogTitle = "";
        private string _selectedLogContent = "";
        private List<StrafeDetail>? _selectedLogBreakdown = null;

        // Nickname & manual sync popup state
        private bool _isEditingNick = false;
        private string _nickBuffer = "";
        private string _rankBuffer = "";
        private string _importStatusMsg = "";
        private double _importStatusTime = 0;
        private int _activeInputIndex = 0;

        // Scroll states
        private float _pbScrollY = 0f;
        private int _pbViewMode = 0; // 0 = Дистанция (All PBs), 1 = Рекорды Блоков (Block PBs)
        private int _pbDirFilter = 0; // 0 = ОБЫЧНЫЙ (FWD), 1 = БОКОМ (SW), 2 = СПИНОЙ (BW)
        private float _mapsScrollY = 0f;
        private float _analyticsScrollY = 0f;
        private float _rankHistoryScrollY = 0f;

        // Maps table search & filter state
        private string _mapSearchQuery = "";
        private bool _isSearchingMaps = false;
        private int _mapSortMode = 0; // 0 = Points desc, 1 = Rank asc, 2 = Time asc, 3 = Attempts desc, 4 = Name asc
        private bool _filterOnlyTop100 = false;

        // Analytics graph state (Per Jump Type!)
        private string _selectedGraphJumpType = "Long Jump"; // Filter graph strictly per jump type
        private int _graphMetric = 0; // 0 = Distance (Units), 1 = Sync (%), 2 = Pre-Speed (u/s), 3 = Overlap (ms)
        private int _graphSampleSize = 20; // 10, 20, 50, 100 jumps sample size
        private int _selectedTrajectoryJumpIndex = -2; // -2 = Auto-follow freshest jump, -1 = Average ghost only, 0..N = specific jump

        // Tab transition animation state
        private float _tabTransitionProgress = 1.0f;
        private float _tabIndicatorX = 16f;
        private float _targetTabIndicatorX = 16f;
        private float _tabIndicatorW = 250f;

        public void OnJumpCaptured(CS2ConsoleEvent evt)
        {
            if (!evt.IsJumpStat || evt.Distance <= 140f) return;
            string norm = CybershokeKzProfile.NormalizeJumpType(evt.JumpType);
            _selectedGraphJumpType = norm;
            _selectedTrajectoryJumpIndex = -2; // Follow freshest jump only inside trajectory tab
            // Stay on current tab (do not force switch if user is browsing PBs or Maps)
        }

        public Texture2D? AvatarTexture => _avatarTexture;

        public void EnsureAvatarLoaded(string steamId64, string? nick = null)
        {
            string? targetSid = !string.IsNullOrEmpty(steamId64) ? steamId64 : CS2ConfigImporter.DetectLocalSteamId64(nick);
            if (string.IsNullOrEmpty(targetSid)) return;

            if (_avatarLoaded && _lastLoadedAvatarSid == targetSid) return;
            
            if (_avatarTexture.HasValue && _avatarTexture.Value.Id > 0)
            {
                Raylib.UnloadTexture(_avatarTexture.Value);
                _avatarTexture = null;
            }

            _avatarLoaded = true;
            _lastLoadedAvatarSid = targetSid;

            try
            {
                string[] searchPaths = new[]
                {
                    $@"C:\Program Files (x86)\Steam\config\avatarcache\{targetSid}.png",
                    $@"C:\Program Files\Steam\config\avatarcache\{targetSid}.png",
                    $@"C:\Steam\config\avatarcache\{targetSid}.png",
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{targetSid}.png"),
                    Path.Combine(Directory.GetCurrentDirectory(), $"{targetSid}.png")
                };

                foreach (var p in searchPaths)
                {
                    if (File.Exists(p))
                    {
                        var img = Raylib.LoadImage(p);
                        if (img.Width > 0)
                        {
                            Raylib.ImageResize(ref img, 80, 80);
                            _avatarTexture = Raylib.LoadTextureFromImage(img);
                            Raylib.UnloadImage(img);
                            return;
                        }
                    }
                }
            }
            catch { }
        }

        public void DrawProfileAvatarIcon(int cx, int cy, int size, Color fallbackColor)
        {
            var cs = UserProfile.Instance.Cybershoke;
            string nick = !string.IsNullOrEmpty(cs.CybershokeNick) ? cs.CybershokeNick : "issushenij";
            EnsureAvatarLoaded(cs.SteamId64, nick);

            if (_avatarTexture.HasValue && _avatarTexture.Value.Id > 0)
            {
                var tex = _avatarTexture.Value;
                // Render avatar edge-to-edge across the entire button
                int fullSize = (int)(32 * AppConfig.Instance.UiScale);
                int x = cx - fullSize / 2;
                int y = cy - fullSize / 2;

                Raylib.DrawTexturePro(
                    tex,
                    new Rectangle(0, 0, tex.Width, tex.Height),
                    new Rectangle(x, y, fullSize, fullSize),
                    Vector2.Zero, 0f, Color.White);

                // Subtle inner border & Online indicator dot in bottom right
                Raylib.DrawRectangleLines(x, y, fullSize, fullSize, new Color(Theme.NeonCyan.R, Theme.NeonCyan.G, Theme.NeonCyan.B, (byte)160));
                Raylib.DrawCircle(x + fullSize - 4, y + fullSize - 4, 3.5f, Theme.NeonGreen);
            }
            else
            {
                Theme.DrawProfileIcon(cx, cy, size, fallbackColor);
            }
        }

        public void Draw(int screenWidth, int screenHeight)
        {
            if (!IsOpen) return;

            var cfg = AppConfig.Instance;
            var prof = UserProfile.Instance;
            var cs = prof.Cybershoke;
            float scale = cfg.UiScale;
            Vector2 mouse = Raylib.GetMousePosition();

            string myNick = !string.IsNullOrEmpty(cs.CybershokeNick) ? cs.CybershokeNick : "issushenij";
            string detectedSid = CS2ConfigImporter.DetectLocalSteamId64(myNick) ?? cs.SteamId64;
            if (!string.IsNullOrEmpty(detectedSid) && cs.SteamId64 != detectedSid)
            {
                cs.SteamId64 = detectedSid;
            }

            EnsureAvatarLoaded(cs.SteamId64, myNick);

            // Fullscreen solid high-tech dark background
            Raylib.DrawRectangle(0, 0, screenWidth, screenHeight, new Color(9, 12, 17, 255));

            // Subtle background grid
            Color gridCol = new Color(20, 30, 45, 40);
            for (int gx = 0; gx < screenWidth; gx += 40)
                Raylib.DrawLine(gx, 0, gx, screenHeight, gridCol);
            for (int gy = 0; gy < screenHeight; gy += 40)
                Raylib.DrawLine(0, gy, screenWidth, gy, gridCol);

            // =========================================================================
            // TOP FULLSCREEN NAVIGATION HEADER BAR (Height: 56px) - ZERO OVERLAPS
            // =========================================================================
            int headerH = (int)(56 * scale);
            Raylib.DrawRectangle(0, 0, screenWidth, headerH, Theme.BgPanelHeader);
            Raylib.DrawLine(0, headerH, screenWidth, headerH, Theme.Border);
            Raylib.DrawLine(0, 0, screenWidth, 0, new Color(0, 240, 255, 100));

            // 1. Left: Player Avatar (Real Steam Avatar Image) & Identity
            int avSize = (int)(40 * scale);
            int avX = 16;
            int avY = (headerH - avSize) / 2;

            if (_avatarTexture.HasValue && _avatarTexture.Value.Id > 0)
            {
                Raylib.DrawTexturePro(
                    _avatarTexture.Value,
                    new Rectangle(0, 0, _avatarTexture.Value.Width, _avatarTexture.Value.Height),
                    new Rectangle(avX, avY, avSize, avSize),
                    Vector2.Zero, 0f, Color.White);
                Raylib.DrawRectangleLines(avX, avY, avSize, avSize, Theme.NeonCyan);
            }
            else
            {
                Raylib.DrawCircle(avX + avSize / 2, avY + avSize / 2, avSize / 2f, new Color(15, 30, 45, 255));
                Raylib.DrawCircleLines(avX + avSize / 2, avY + avSize / 2, avSize / 2f, Theme.NeonCyan);
                string initial = !string.IsNullOrEmpty(cs.CybershokeNick) ? cs.CybershokeNick[..1].ToUpper() : "P";
                Theme.DrawText(initial, avX + (avSize - Theme.GetScaledFontSize(18)) / 2, avY + (avSize - Theme.GetScaledFontSize(18)) / 2, 18, Theme.NeonCyan);
            }

            int nameX = avX + avSize + 12;
            Theme.DrawText(myNick, nameX, headerH / 2 - (int)(12 * scale), 16, Theme.TextWhite);
            
            string sidStr = !string.IsNullOrEmpty(cs.SteamId64) ? $"STEAM: {cs.SteamId64}" : "STEAM: [НЕ ПРИВЯЗАН - НАЖМИТЕ ДЛЯ ВВОДА]";
            Theme.DrawText(sidStr, nameX, headerH / 2 + (int)(2 * scale), 9, !string.IsNullOrEmpty(cs.SteamId64) ? Theme.TextMuted : Theme.NeonOrange);

            int leftTotalW = nameX + (int)(160 * scale);

            // 2. Right: Action Buttons (Update App, Auto-Sync & Exit)
            int closeBtnW = (int)(105 * scale);
            int closeBtnH = (int)(34 * scale);
            int syncBtnW = (int)(135 * scale);
            int updateBtnW = (int)(135 * scale);
            int rightGap = 8;

            int closeBtnX = screenWidth - 16 - closeBtnW;
            int syncBtnX = closeBtnX - rightGap - syncBtnW;
            int updateBtnX = syncBtnX - rightGap - updateBtnW;
            int btnY = (headerH - closeBtnH) / 2;

            bool isModalActive = _showRankHistoryModal || _showPBHistoryModal || _showLogModal || _isEditingNick;

            // [НАЗАД / ESC]
            if (Theme.DrawButton(closeBtnX, btnY, closeBtnW, closeBtnH, "НАЗАД [Esc]", false, 11, enabled: !isModalActive) ||
                Raylib.IsKeyPressed(KeyboardKey.Escape) || Raylib.IsKeyPressed(KeyboardKey.P))
            {
                if (_showLogModal)
                {
                    _showLogModal = false;
                }
                else if (_showPBHistoryModal)
                {
                    _showPBHistoryModal = false;
                }
                else if (_showRankHistoryModal)
                {
                    _showRankHistoryModal = false;
                }
                else if (_isEditingNick)
                {
                    _isEditingNick = false;
                }
                else
                {
                    IsOpen = false;
                    UserProfile.Save();
                    return;
                }
            }

            // [АВТО-СИНХРОНИЗАЦИЯ С CYBERSHOKE]
            if (CybershokeWebSync.IsSyncing)
            {
                Raylib.DrawRectangle(syncBtnX, btnY, syncBtnW, closeBtnH, new Color(0, 45, 60, 220));
                Raylib.DrawRectangleLines(syncBtnX, btnY, syncBtnW, closeBtnH, Theme.NeonCyan);
                string animDots = new string('.', ((int)(Raylib.GetTime() * 3) % 4));
                Theme.DrawText($"Синхронизация{animDots}", syncBtnX + 10, (headerH - Theme.GetScaledFontSize(10)) / 2, 10, Theme.NeonCyan);
            }
            else
            {
                if (Theme.DrawButton(syncBtnX, btnY, syncBtnW, closeBtnH, "Авто-Синхр (Edge)", true, 11, enabled: !isModalActive))
                {
                    CybershokeWebSync.StartAutoSync(cs.SteamId64, (ok, msg) =>
                    {
                        _importStatusMsg = msg;
                        _importStatusTime = Raylib.GetTime();
                    });
                }
            }

            // [ОБНОВЛЕНИЕ ПРОГРАММЫ]
            if (UpdateManager.IsChecking || UpdateManager.IsDownloading)
            {
                Raylib.DrawRectangle(updateBtnX, btnY, updateBtnW, closeBtnH, new Color(30, 25, 10, 220));
                Raylib.DrawRectangleLines(updateBtnX, btnY, updateBtnW, closeBtnH, Theme.NeonGold);
                string upAnim = UpdateManager.IsDownloading ? $"Загрузка {UpdateManager.DownloadProgress * 100:F0}%" : "Проверка...";
                Theme.DrawText(upAnim, updateBtnX + 10, (headerH - Theme.GetScaledFontSize(10)) / 2, 10, Theme.NeonGold);
            }
            else
            {
                string upBtnText = UpdateManager.UpdateAvailable ? "⚡ ОБНОВИТЬ" : "Обновления";
                bool isUpActive = UpdateManager.UpdateAvailable;
                if (Theme.DrawButton(updateBtnX, btnY, updateBtnW, closeBtnH, upBtnText, isUpActive, 11, enabled: !isModalActive))
                {
                    if (UpdateManager.UpdateAvailable)
                    {
                        UpdateManager.ShowUpdatePrompt = true;
                    }
                    else
                    {
                        _ = System.Threading.Tasks.Task.Run(async () =>
                        {
                            await UpdateManager.CheckForUpdatesAsync(silent: false);
                            if (UpdateManager.UpdateAvailable)
                            {
                                UpdateManager.ShowUpdatePrompt = true;
                            }
                            else
                            {
                                _importStatusMsg = UpdateManager.StatusMessage;
                                _importStatusTime = Raylib.GetTime();
                            }
                        });
                    }
                }
            }

            // 3. Center: Dynamically Sized Quick Stats Badges (Clickable Rank Badge -> Timeline Chain!)
            int availMidW = updateBtnX - leftTotalW - 24;
            if (availMidW > 200)
            {
                int badgeH = (int)(34 * scale);
                int badgeY = (headerH - badgeH) / 2;

                int kzPos = cs.KzPosition;
                int kzPts = cs.KzPoints;
                int mapsCount = cs.CompletedMaps.Count > 0 ? cs.CompletedMaps.Count : cs.KzMapsCompleted;
                float mapsPct = cs.KzMapsPercent;
                int top100 = cs.KzTop100Count;

                string posStr = kzPos > 0 ? $"#{kzPos} ▶" : "-";
                string ptsStr = kzPts > 0 ? $"{kzPts:N0} PTS" : "-";
                string mapsStr = mapsCount > 0 ? $"{mapsCount} ({mapsPct:F1}%)" : "-";
                string topStr = top100 > 0 ? $"{top100} КАРТЫ" : "-";

                if (availMidW >= 460)
                {
                    int badgeW = Math.Min((int)(115 * scale), (availMidW - 24) / 4);
                    int curBx = leftTotalW + 12;

                    // KZ Rank Badge is CLICKABLE -> opens Rank Timeline Chain
                    if (DrawHeaderBadge(ref curBx, badgeY, badgeW, badgeH, "KZ РАНГ", posStr, Theme.NeonGold, clickable: !isModalActive && kzPos > 0))
                    {
                        _showRankHistoryModal = true;
                    }

                    DrawHeaderBadge(ref curBx, badgeY, badgeW, badgeH, "ОЧКИ КАРТ", ptsStr, Theme.NeonCyan, clickable: false);
                    DrawHeaderBadge(ref curBx, badgeY, badgeW, badgeH, "КАРТЫ KZ", mapsStr, Theme.NeonGreen, clickable: false);
                    DrawHeaderBadge(ref curBx, badgeY, badgeW, badgeH, "ТОП-100", topStr, Theme.NeonOrange, clickable: false);
                }
                else
                {
                    int badgeW = (availMidW - 12) / 2;
                    int curBx = leftTotalW + 6;
                    if (DrawHeaderBadge(ref curBx, badgeY, badgeW, badgeH, "KZ РАНГ", posStr, Theme.NeonGold, clickable: !isModalActive && kzPos > 0))
                    {
                        _showRankHistoryModal = true;
                    }
                    DrawHeaderBadge(ref curBx, badgeY, badgeW, badgeH, "ОЧКИ", ptsStr, Theme.NeonCyan, clickable: false);
                }
            }

            // =========================================================================
            // NAVIGATION SUB-HEADER (TABS BAR WITH SLIDING GLOW INDICATOR)
            // =========================================================================
            int tabsBarY = headerH;
            int tabsBarH = (int)(42 * scale);
            Raylib.DrawRectangle(0, tabsBarY, screenWidth, tabsBarH, new Color(13, 17, 24, 255));
            Raylib.DrawLine(0, tabsBarY + tabsBarH, screenWidth, tabsBarY + tabsBarH, Theme.Border);

            // Responsive tab button width: calculate based on screenWidth with max clamp
            int availTabsAreaW = screenWidth - 32;
            int tabGap = 8;
            int tabBtnW = Math.Clamp((availTabsAreaW - tabGap * 2) / 3, 180, (int)(320 * scale));
            int tabBtnH = (int)(32 * scale);
            int tabBtnY = tabsBarY + (tabsBarH - tabBtnH) / 2;

            // Animate sliding tab indicator
            int targetTabX = _activeTab switch
            {
                1 => 16,
                2 => 16 + tabBtnW + tabGap,
                _ => 16 + (tabBtnW + tabGap) * 2
            };
            _targetTabIndicatorX = targetTabX;
            _tabIndicatorX += (_targetTabIndicatorX - _tabIndicatorX) * 0.25f; // Smooth spring-like lerp
            _tabIndicatorW = tabBtnW;

            // Draw glowing sliding pill under active tab
            Raylib.DrawRectangle((int)_tabIndicatorX, tabBtnY + tabBtnH - 3, (int)_tabIndicatorW, 3, Theme.NeonCyan);
            Raylib.DrawRectangle((int)_tabIndicatorX, tabBtnY + tabBtnH - 5, (int)_tabIndicatorW, 2, new Color(0, 229, 255, 60));

            int curTabX = 16;

            if (Theme.DrawButton(curTabX, tabBtnY, tabBtnW, tabBtnH, "1. РЕКОРДЫ ПРЫЖКОВ (PB)", _activeTab == 1, 10, enabled: !isModalActive))
            {
                if (_activeTab != 1) { _activeTab = 1; _tabTransitionProgress = 0.0f; }
            }
            curTabX += tabBtnW + tabGap;

            int mCount = cs.CompletedMaps.Count > 0 ? cs.CompletedMaps.Count : 86;
            string mapsTabTitle = $"2. КАРТЫ KZ ({mCount} ПРОЙДЕНО)";
            if (Theme.DrawButton(curTabX, tabBtnY, tabBtnW, tabBtnH, mapsTabTitle, _activeTab == 2, 10, enabled: !isModalActive))
            {
                if (_activeTab != 2) { _activeTab = 2; _tabTransitionProgress = 0.0f; }
            }
            curTabX += tabBtnW + tabGap;

            if (Theme.DrawButton(curTabX, tabBtnY, tabBtnW, tabBtnH, "3. АНАЛИЗ И ПЛАН ТРЕНИРОВОК", _activeTab == 0, 10, enabled: !isModalActive))
            {
                if (_activeTab != 0) { _activeTab = 0; _tabTransitionProgress = 0.0f; }
            }

            // Animate tab content entrance
            if (_tabTransitionProgress < 1.0f)
            {
                _tabTransitionProgress += Raylib.GetFrameTime() * 4.5f;
                if (_tabTransitionProgress > 1.0f) _tabTransitionProgress = 1.0f;
            }

            string curStatus = CybershokeWebSync.IsSyncing ? CybershokeWebSync.SyncStatusMessage : _importStatusMsg;
            if (!string.IsNullOrEmpty(curStatus))
            {
                int statX = curTabX + tabBtnW + 16;
                bool isErr = curStatus.Contains("не") || curStatus.Contains("Ошибка") || curStatus.Contains("Таймаут");
                Color statCol = isErr ? Theme.NeonOrange : Theme.NeonGreen;
                Theme.DrawText($"[ {curStatus} ]", statX, tabsBarY + (tabsBarH - Theme.GetScaledFontSize(10)) / 2, 10, statCol);
            }

            // =========================================================================
            // ACTIVE TAB CONTENT RENDERER (BLOCKED WHEN MODAL IS OPEN)
            // =========================================================================
            int contentY = tabsBarY + tabsBarH + 12;
            int contentH = screenHeight - contentY - 12;
            int contentW = screenWidth - 32;
            int contentX = 16;

            bool inputActive = !isModalActive;

            // Content smooth slide-up offset
            int animSlideOffsetY = (int)((1.0f - _tabTransitionProgress) * 12.0f);
            contentY += animSlideOffsetY;

            if (_activeTab == 1)
            {
                DrawJumpPBsAndTelemetryTab(contentX, contentY, contentW, contentH, scale, prof, inputActive);
            }
            else if (_activeTab == 2)
            {
                DrawKzMapsLeaderboardTab(contentX, contentY, contentW, contentH, scale, prof, inputActive);
            }
            else
            {
                DrawDeepAnalyticsTab(contentX, contentY, contentW, contentH, scale, prof, inputActive);
            }

            // Rank History Timeline Chain Modal Overlay
            if (_showRankHistoryModal)
            {
                DrawRankHistoryChainModal(screenWidth, screenHeight, scale, prof);
            }

            // PB Progression History Timeline Chain Modal Overlay
            if (_showPBHistoryModal)
            {
                DrawPBHistoryModal(screenWidth, screenHeight, scale, prof);
            }

            // Raw Console Log Inspection Modal
            if (_showLogModal)
            {
                DrawConsoleLogModal(screenWidth, screenHeight, scale);
            }

            if (_isEditingNick)
            {
                DrawNickEditModal(screenWidth, screenHeight, scale, prof);
            }
        }

        private static bool DrawHeaderBadge(ref int x, int y, int w, int h, string label, string value, Color accent, bool clickable = false)
        {
            Vector2 mouse = Raylib.GetMousePosition();
            bool hover = mouse.X >= x && mouse.X <= x + w && mouse.Y >= y && mouse.Y <= y + h;

            Color bg = hover && clickable ? new Color(25, 36, 52, 240) : new Color(15, 20, 30, 220);
            Raylib.DrawRectangle(x, y, w, h, bg);
            Raylib.DrawRectangleLines(x, y, w, h, hover && clickable ? accent : new Color(accent.R, accent.G, accent.B, (byte)90));

            Theme.DrawText(label, x + 8, y + 4, 8, Theme.TextMuted);
            Theme.DrawText(value, x + 8, y + 16, 11, accent);

            bool clicked = clickable && hover && Raylib.IsMouseButtonPressed(MouseButton.Left);
            x += w + 8;
            return clicked;
        }

        // =========================================================================
        // PB TIMELINE CHAIN MODAL (ВРЕМЕННАЯ ЦЕПЬ ИЗМЕНЕНИЯ РЕКОРДОВ / PB HISTORY)
        // =========================================================================
        private void DrawPBHistoryModal(int screenWidth, int screenHeight, float scale, UserProfile prof)
        {
            var cs = prof.Cybershoke;
            Vector2 mouse = Raylib.GetMousePosition();

            // Dim backdrop (Solid dark blocking overlay)
            Raylib.DrawRectangle(0, 0, screenWidth, screenHeight, new Color(4, 7, 12, 240));

            int popW = Math.Min((int)(860 * scale), screenWidth - 30);
            int popH = Math.Min((int)(620 * scale), screenHeight - 40);
            int popX = (screenWidth - popW) / 2;
            int popY = (screenHeight - popH) / 2;

            Theme.DrawGlassPanel(popX, popY, popW, popH);

            int pHeaderH = (int)(44 * scale);
            Raylib.DrawRectangle(popX, popY, popW, pHeaderH, Theme.BgPanelHeader);
            Raylib.DrawLine(popX, popY + pHeaderH, popX + popW, popY + pHeaderH, Theme.Border);

            string titlePrefix = _selectedPbHistoryJumpType == "All" ? "ВСЕ ТИПЫ ПРЫЖКОВ" : _selectedPbHistoryJumpType.ToUpper();
            Theme.DrawText($"ИСТОРИЯ РЕКОРДОВ: {titlePrefix}", popX + 20, popY + (pHeaderH - Theme.GetScaledFontSize(13)) / 2, 13, Theme.NeonGold);

            // Back button
            int closeBtnW = (int)(80 * scale);
            int closeBtnH = (int)(28 * scale);
            int closeBtnX = popX + popW - closeBtnW - 12;
            int closeBtnY = popY + (pHeaderH - closeBtnH) / 2;
            if (Theme.DrawButton(closeBtnX, closeBtnY, closeBtnW, closeBtnH, "НАЗАД", false, 10))
            {
                _showPBHistoryModal = false;
                return;
            }

            // Jump Type Filter Pills Bar
            int filterBarY = popY + pHeaderH + 6;
            int filterBarH = (int)(28 * scale);
            int fBtnX = popX + 16;
            int fBtnW = (int)(55 * scale);
            int fBtnH = (int)(22 * scale);
            int fBy = filterBarY + (filterBarH - fBtnH) / 2;

            if (Theme.DrawButton(fBtnX, fBy, fBtnW, fBtnH, "ВСЕ", _selectedPbHistoryJumpType == "All", 8))
            {
                _selectedPbHistoryJumpType = "All";
                _pbHistoryScrollY = 0f;
            }
            fBtnX += fBtnW + 4;

            foreach (var jt in CybershokeKzProfile.StandardJumpTypes)
            {
                var (_, sc, _) = CybershokeKzProfile.GetJumpTypeMeta(jt);
                int jtBtnW = (int)(42 * scale);
                if (Theme.DrawButton(fBtnX, fBy, jtBtnW, fBtnH, sc, _selectedPbHistoryJumpType == jt, 8))
                {
                    _selectedPbHistoryJumpType = jt;
                    _pbHistoryScrollY = 0f;
                }
                fBtnX += jtBtnW + 4;
            }

            var fullHistory = cs.PBHistory;
            var history = fullHistory ?? new List<PBHistoryRecord>();
            if (_selectedPbHistoryJumpType != "All")
            {
                string normSel = CybershokeKzProfile.NormalizeJumpType(_selectedPbHistoryJumpType);
                history = history.Where(h => CybershokeKzProfile.NormalizeJumpType(h.JumpType) == normSel).ToList();
            }

            int chainAreaY = filterBarY + filterBarH + 8;
            int chainAreaH = popH - (chainAreaY - popY) - 16;

            if (history.Count == 0)
            {
                Theme.DrawText("История рекордов для данного типа пока пуста", popX + 30, chainAreaY + 40, 12, Theme.TextDim);
                Theme.DrawText("Устанавливайте новые рекорды на сервере — они будут сохраняться сюда!", popX + 30, chainAreaY + 65, 10, Theme.TextMuted);
                return;
            }

            // Chain parameters (Rich, fully utilized aesthetic cards)
            int nodeH = (int)(96 * scale);
            int nodeGap = (int)(14 * scale);
            int totalChainH = history.Count * (nodeH + nodeGap);
            float maxScroll = Math.Max(0, totalChainH - chainAreaH);

            if (mouse.X >= popX && mouse.X <= popX + popW && mouse.Y >= chainAreaY && mouse.Y <= chainAreaY + chainAreaH)
            {
                _pbHistoryScrollY -= Raylib.GetMouseWheelMove() * 40;
                _pbHistoryScrollY = Math.Clamp(_pbHistoryScrollY, 0, maxScroll);
            }

            Raylib.BeginScissorMode(popX + 10, chainAreaY, popW - 20, chainAreaH);

            int laserX = popX + (int)(48 * scale);

            // Draw vertical glowing laser connection line
            int lineStartY = chainAreaY + 24 - (int)_pbHistoryScrollY;
            int lineEndY = chainAreaY + (history.Count - 1) * (nodeH + nodeGap) + 24 - (int)_pbHistoryScrollY;
            Raylib.DrawLineEx(new Vector2(laserX, lineStartY), new Vector2(laserX, lineEndY), 3f, new Color(255, 215, 0, 120));
            Raylib.DrawLineEx(new Vector2(laserX, lineStartY), new Vector2(laserX, lineEndY), 1f, Theme.TextWhite);

            float timeAnim = (float)Raylib.GetTime();

            for (int i = 0; i < history.Count; i++)
            {
                var rec = history[i];
                int ny = chainAreaY + i * (nodeH + nodeGap) - (int)_pbHistoryScrollY;

                if (ny + nodeH < chainAreaY - 20 || ny > chainAreaY + chainAreaH + 20) continue;

                bool isLatest = (i == 0);
                var (tName, sCode, accCol) = CybershokeKzProfile.GetJumpTypeMeta(rec.JumpType);
                Color nodeAccent = isLatest ? Theme.NeonGold : accCol;

                // 1. Glowing Animated Circuit Node
                float pulse = isLatest ? (MathF.Sin(timeAnim * 4f) * 3f + 9f) : 7f;
                Raylib.DrawCircle(laserX, ny + nodeH / 2, pulse + 4f, new Color(nodeAccent.R, nodeAccent.G, nodeAccent.B, (byte)(isLatest ? 60 : 25)));
                Raylib.DrawCircle(laserX, ny + nodeH / 2, pulse, nodeAccent);
                Raylib.DrawCircle(laserX, ny + nodeH / 2, 3.5f, Theme.TextWhite);

                // 2. Node Milestone Card
                int cardX = laserX + (int)(26 * scale);
                int cardW = popW - (cardX - popX) - 22;
                Raylib.DrawRectangle(cardX, ny, cardW, nodeH, new Color(13, 18, 28, 240));
                Raylib.DrawRectangleLines(cardX, ny, cardW, nodeH, new Color(nodeAccent.R, nodeAccent.G, nodeAccent.B, (byte)(isLatest ? 190 : 75)));
                Raylib.DrawRectangle(cardX, ny, 4, nodeH, nodeAccent);

                // Top Subtle Separator
                Raylib.DrawLine(cardX + 16, ny + (int)(32 * scale), cardX + cardW - 16, ny + (int)(32 * scale), new Color(255, 255, 255, 18));

                // Row 1 (Y = ny + 8): [CODE] TYPE FULL NAME | TIMESTAMP | MAP PILL
                int bW = (int)(42 * scale);
                int bH = (int)(18 * scale);
                Raylib.DrawRectangle(cardX + 16, ny + 7, bW, bH, new Color(accCol.R, accCol.G, accCol.B, (byte)35));
                Raylib.DrawRectangleLines(cardX + 16, ny + 7, bW, bH, accCol);
                Theme.DrawText(sCode, cardX + 16 + (bW - Theme.MeasureText(sCode, 8)) / 2, ny + 7 + (bH - Theme.GetScaledFontSize(8)) / 2, 8, accCol);

                string fullTitle = tName.ToUpper();
                Theme.DrawText(fullTitle, cardX + 16 + bW + 8, ny + 9, 10, Theme.TextWhite);

                int timeX = cardX + 16 + bW + 8 + Theme.MeasureText(fullTitle, 10) + 14;
                Theme.DrawText($"•  {rec.TimestampStr}", timeX, ny + 10, 8, Theme.TextDim);

                // Map Pill on the right
                string mapText = !string.IsNullOrEmpty(rec.MapName) ? $"MAP: {rec.MapName}" : "MAP: kz_longjump";
                int mapW = Theme.MeasureText(mapText, 9) + 16;
                int mapX = cardX + cardW - mapW - 14;
                int mapH = (int)(18 * scale);
                Raylib.DrawRectangle(mapX, ny + 7, mapW, mapH, new Color(0, 229, 255, 20));
                Raylib.DrawRectangleLines(mapX, ny + 7, mapW, mapH, new Color(0, 229, 255, 70));
                Theme.DrawText(mapText, mapX + 8, ny + 9, 9, Theme.NeonCyan);

                int rightPillAnchor = mapX - 8;

                if (isLatest)
                {
                    string latestTag = "ТЕКУЩИЙ РЕКОРД";
                    int tagW = Theme.MeasureText(latestTag, 8) + 14;
                    int tagX = rightPillAnchor - tagW;
                    Raylib.DrawRectangle(tagX, ny + 7, tagW, mapH, new Color(255, 215, 0, 30));
                    Raylib.DrawRectangleLines(tagX, ny + 7, tagW, mapH, Theme.NeonGold);
                    Theme.DrawText(latestTag, tagX + 7, ny + 9, 8, Theme.NeonGold);
                    rightPillAnchor = tagX - 8;
                }

                // Open Raw Console Log Button (Relocated next to PB / Map pill)
                int logBtnW = (int)(95 * scale);
                int logBtnH = (int)(18 * scale);
                int logBtnX = rightPillAnchor - logBtnW;
                int logBtnY = ny + 7;

                string logBtnText = "ЛОГ КОНСОЛИ";
                if (Theme.DrawButton(logBtnX, logBtnY, logBtnW, logBtnH, logBtnText, false, 8, enabled: true))
                {
                    _showLogModal = true;
                    string blockTitle = rec.BlockDistance > 0 ? $" [БЛОК: {rec.BlockDistance:F0}]" : "";
                    _selectedLogTitle = $"{rec.JumpType.ToUpper()}{blockTitle} — {rec.Distance:F2}u ({rec.TimestampStr})";
                    _selectedLogContent = !string.IsNullOrEmpty(rec.RawConsoleLog) 
                        ? rec.RawConsoleLog 
                        : $"[CS2 Console Watcher] {rec.Distance:F4} units | {rec.Strafes} str | {rec.Sync:F1}% sync | {rec.PreSpeed:F1} pre | {rec.MaxSpeed:F1} max\nКарта: {rec.MapName}\nОтклонение (Deviation): {rec.Deviation:F2}\nAirpath: {rec.Airpath:F3}\nOverlap: {rec.AvgOverlap:F1}ms | Bad Angles: {rec.AvgBadAngles:F1}%";
                    _selectedLogBreakdown = rec.StrafeBreakdown;
                }

                // -------------------------------------------------------------
                // ROW 2 & 3: LEFT SIDE = DISTANCE & DELTA & BLOCK; RIGHT SIDE = 4 TELEMETRY TILES
                // -------------------------------------------------------------
                int mainContentY = ny + (int)(38 * scale);

                // Left: Big Distance
                string distNum = $"{rec.Distance:F2}";
                Theme.DrawText(distNum, cardX + 16, mainContentY, 20, isLatest ? Theme.NeonGold : Theme.NeonCyan);
                int numW = Theme.MeasureText(distNum, 20);
                Theme.DrawText("units", cardX + 16 + numW + 4, mainContentY + 8, 9, Theme.TextDim);

                // Block tag right next to units if present
                if (rec.BlockDistance > 0)
                {
                    var (bTier, _, bCol, _) = GetBlockTier(rec.JumpType, rec.BlockDistance);
                    string blkStr = $"БЛОК: {rec.BlockDistance:F0} [{bTier}]";
                    int blkW = Theme.MeasureText(blkStr, 8) + 12;
                    int blkH = (int)(16 * scale);
                    int blkX = cardX + 16 + numW + 36;
                    Raylib.DrawRectangle(blkX, mainContentY + 6, blkW, blkH, new Color(bCol.R, bCol.G, bCol.B, (byte)35));
                    Raylib.DrawRectangleLines(blkX, mainContentY + 6, blkW, blkH, bCol);
                    Theme.DrawText(blkStr, blkX + 6, mainContentY + 8, 8, bCol);
                }

                // Delta Badge right below distance
                int deltaY = mainContentY + (int)(27 * scale);
                if (rec.Delta > 0 && rec.PreviousDistance > 0)
                {
                    string deltaText = $"+{rec.Delta:F2}u (было {rec.PreviousDistance:F1}u)";
                    int delBadgeW = Theme.MeasureText(deltaText, 8) + 14;
                    int delBadgeH = (int)(18 * scale);
                    Raylib.DrawRectangle(cardX + 16, deltaY, delBadgeW, delBadgeH, new Color(0, 255, 128, 35));
                    Raylib.DrawRectangleLines(cardX + 16, deltaY, delBadgeW, delBadgeH, Theme.NeonGreen);
                    Theme.DrawText(deltaText, cardX + 23, deltaY + 3, 8, Theme.NeonGreen);
                }
                else if (i == history.Count - 1)
                {
                    string initText = "ПЕРВЫЙ РЕКОРД";
                    int delBadgeW = Theme.MeasureText(initText, 8) + 14;
                    int delBadgeH = (int)(18 * scale);
                    Raylib.DrawRectangle(cardX + 16, deltaY, delBadgeW, delBadgeH, new Color(0, 240, 255, 30));
                    Raylib.DrawRectangleLines(cardX + 16, deltaY, delBadgeW, delBadgeH, Theme.NeonCyan);
                    Theme.DrawText(initText, cardX + 23, deltaY + 3, 8, Theme.NeonCyan);
                }

                // Right Side: 4 Telemetry Mini-Badges spanning the right half of card
                int statsStartX = cardX + (int)(245 * scale);
                int statsW = (cardX + cardW - 16) - statsStartX;
                int tileCount = 4;
                int tileGap = 8;
                int singleTileW = (statsW - (tileCount - 1) * tileGap) / tileCount;
                int tileH = (int)(50 * scale);
                int tileY = mainContentY - 1;

                // Tile 1: Strafes
                DrawHistoryStatPill(statsStartX + 0 * (singleTileW + tileGap), tileY, singleTileW, tileH, "СТРЕЙФЫ", $"{rec.Strafes}", "strafes", Theme.NeonCyan);
                // Tile 2: Sync
                Color syncColor = rec.Sync >= 80 ? Theme.NeonGreen : (rec.Sync >= 60 ? Theme.NeonCyan : Theme.NeonOrange);
                DrawHistoryStatPill(statsStartX + 1 * (singleTileW + tileGap), tileY, singleTileW, tileH, "СИНХРА", $"{rec.Sync:F0}%", "avg sync", syncColor);
                // Tile 3: Pre-Speed
                DrawHistoryStatPill(statsStartX + 2 * (singleTileW + tileGap), tileY, singleTileW, tileH, "PRE-SPD", $"{rec.PreSpeed:F0}", "u/s pre", Theme.NeonGold);
                // Tile 4: Max-Speed
                DrawHistoryStatPill(statsStartX + 3 * (singleTileW + tileGap), tileY, singleTileW, tileH, "MAX-SPD", $"{rec.MaxSpeed:F0}", "u/s max", Theme.NeonPurple);
            }

            Raylib.EndScissorMode();

            if (maxScroll > 0)
            {
                int sbX = popX + popW - 16;
                int sbY = chainAreaY;
                int sbH = chainAreaH;
                float thumbPct = Math.Clamp(chainAreaH / (float)totalChainH, 0.15f, 1.0f);
                int thumbH = (int)(sbH * thumbPct);
                int thumbY = sbY + (int)((sbH - thumbH) * (_pbHistoryScrollY / maxScroll));
                Raylib.DrawRectangle(sbX, sbY, 4, sbH, new Color(255, 255, 255, 20));
                Raylib.DrawRectangle(sbX, thumbY, 4, thumbH, Theme.NeonGold);
            }
        }

        // =========================================================================
        // RANK TIMELINE CHAIN MODAL (ВРЕМЕННАЯ ЦЕПЬ ИЗМЕНЕНИЯ РАНГА)
        // =========================================================================
        private void DrawRankHistoryChainModal(int screenWidth, int screenHeight, float scale, UserProfile prof)
        {
            var cs = prof.Cybershoke;
            Vector2 mouse = Raylib.GetMousePosition();

            // Dim backdrop (Solid dark blocking overlay)
            Raylib.DrawRectangle(0, 0, screenWidth, screenHeight, new Color(4, 7, 12, 240));

            int popW = Math.Min((int)(800 * scale), screenWidth - 40);
            int popH = Math.Min((int)(580 * scale), screenHeight - 50);
            int popX = (screenWidth - popW) / 2;
            int popY = (screenHeight - popH) / 2;

            Theme.DrawGlassPanel(popX, popY, popW, popH);

            int pHeaderH = (int)(44 * scale);
            Raylib.DrawRectangle(popX, popY, popW, pHeaderH, Theme.BgPanelHeader);
            Raylib.DrawLine(popX, popY + pHeaderH, popX + popW, popY + pHeaderH, Theme.Border);

            Theme.DrawText("ВРЕМЕННАЯ ЦЕПЬ ИЗМЕНЕНИЯ РАНГА KZ (RANK TIMELINE CHAIN)", popX + 20, popY + (pHeaderH - Theme.GetScaledFontSize(13)) / 2, 13, Theme.NeonGold);

            // Back button
            int closeBtnW = (int)(80 * scale);
            int closeBtnH = (int)(28 * scale);
            int closeBtnX = popX + popW - closeBtnW - 12;
            int closeBtnY = popY + (pHeaderH - closeBtnH) / 2;
            if (Theme.DrawButton(closeBtnX, closeBtnY, closeBtnW, closeBtnH, "НАЗАД", false, 10))
            {
                _showRankHistoryModal = false;
                return;
            }

            var history = cs.RankHistory;
            if (history.Count == 0)
            {
                Theme.DrawText("История изменения ранга пуста", popX + 30, popY + pHeaderH + 40, 12, Theme.TextDim);
                return;
            }

            int chainAreaY = popY + pHeaderH + 16;
            int chainAreaH = popH - pHeaderH - 32;

            // Chain parameters (Spacious cards)
            int nodeH = (int)(90 * scale);
            int nodeGap = (int)(16 * scale);
            int totalChainH = history.Count * (nodeH + nodeGap);
            float maxScroll = Math.Max(0, totalChainH - chainAreaH);

            if (mouse.X >= popX && mouse.X <= popX + popW && mouse.Y >= chainAreaY && mouse.Y <= chainAreaY + chainAreaH)
            {
                _rankHistoryScrollY -= Raylib.GetMouseWheelMove() * 40;
                _rankHistoryScrollY = Math.Clamp(_rankHistoryScrollY, 0, maxScroll);
            }

            Raylib.BeginScissorMode(popX + 10, chainAreaY, popW - 20, chainAreaH);

            int laserX = popX + (int)(55 * scale);

            // Draw vertical glowing laser connection line
            int lineStartY = chainAreaY + 24 - (int)_rankHistoryScrollY;
            int lineEndY = chainAreaY + (history.Count - 1) * (nodeH + nodeGap) + 24 - (int)_rankHistoryScrollY;
            Raylib.DrawLineEx(new Vector2(laserX, lineStartY), new Vector2(laserX, lineEndY), 3f, new Color(0, 240, 255, 120));
            Raylib.DrawLineEx(new Vector2(laserX, lineStartY), new Vector2(laserX, lineEndY), 1f, Theme.TextWhite);

            float timeAnim = (float)Raylib.GetTime();

            for (int i = 0; i < history.Count; i++)
            {
                var rec = history[i];
                int ny = chainAreaY + i * (nodeH + nodeGap) - (int)_rankHistoryScrollY;

                if (ny + nodeH < chainAreaY - 20 || ny > chainAreaY + chainAreaH + 20) continue;

                bool isLatest = (i == history.Count - 1);
                Color nodeAccent = isLatest ? Theme.NeonGold : Theme.NeonCyan;

                // 1. Glowing Animated Circuit Node
                float pulse = isLatest ? (MathF.Sin(timeAnim * 4f) * 3f + 10f) : 8f;
                Raylib.DrawCircle(laserX, ny + nodeH / 2, pulse + 4f, new Color(nodeAccent.R, nodeAccent.G, nodeAccent.B, (byte)(isLatest ? 60 : 25)));
                Raylib.DrawCircle(laserX, ny + nodeH / 2, pulse, nodeAccent);
                Raylib.DrawCircle(laserX, ny + nodeH / 2, 4f, Theme.TextWhite);

                // 2. Node Milestone Card
                int cardX = laserX + (int)(30 * scale);
                int cardW = popW - (cardX - popX) - 26;
                Raylib.DrawRectangle(cardX, ny, cardW, nodeH, new Color(13, 18, 28, 235));
                Raylib.DrawRectangleLines(cardX, ny, cardW, nodeH, new Color(nodeAccent.R, nodeAccent.G, nodeAccent.B, (byte)(isLatest ? 180 : 80)));
                Raylib.DrawRectangle(cardX, ny, 4, nodeH, nodeAccent);

                // Row 1: Timestamp (Y = ny + 10)
                Theme.DrawText(rec.TimestampStr, cardX + 16, ny + 10, 9, Theme.TextDim);

                // Row 2: Rank Position + Delta Badge + Highlight Map (Y = ny + 34)
                string rkStr = $"#{rec.RankPosition}";
                Theme.DrawText(rkStr, cardX + 16, ny + 34, 18, nodeAccent);

                int rkW = Theme.MeasureText(rkStr, 18);
                int delBadgeX = cardX + 16 + rkW + 14;
                int delBadgeY = ny + 34;
                int delBadgeH = (int)(20 * scale);

                if (rec.RankDelta > 0)
                {
                    string delText = $"▲ +{rec.RankDelta} ПОЗИЦИЙ";
                    int delBadgeW = Theme.MeasureText(delText, 8) + 16;
                    Raylib.DrawRectangle(delBadgeX, delBadgeY, delBadgeW, delBadgeH, new Color(0, 255, 128, 35));
                    Raylib.DrawRectangleLines(delBadgeX, delBadgeY, delBadgeW, delBadgeH, Theme.NeonGreen);
                    Theme.DrawText(delText, delBadgeX + 8, delBadgeY + 4, 8, Theme.NeonGreen);
                }
                else if (i == 0)
                {
                    string startText = "ТОЧКА СТАРТА";
                    int delBadgeW = Theme.MeasureText(startText, 8) + 16;
                    Raylib.DrawRectangle(delBadgeX, delBadgeY, delBadgeW, delBadgeH, new Color(0, 240, 255, 30));
                    Raylib.DrawRectangleLines(delBadgeX, delBadgeY, delBadgeW, delBadgeH, Theme.NeonCyan);
                    Theme.DrawText(startText, delBadgeX + 8, delBadgeY + 4, 8, Theme.NeonCyan);
                }

                if (!string.IsNullOrEmpty(rec.HighlightMap))
                {
                    string hlMap = $"MAP: {rec.HighlightMap}";
                    int hlW = Theme.MeasureText(hlMap, 9) + 16;
                    int hlX = cardX + cardW - hlW - 14;
                    int hlY = ny + 10;
                    int hlH = (int)(20 * scale);
                    Raylib.DrawRectangle(hlX, hlY, hlW, hlH, new Color(0, 229, 255, 20));
                    Raylib.DrawRectangleLines(hlX, hlY, hlW, hlH, new Color(0, 229, 255, 70));
                    Theme.DrawText(hlMap, hlX + 8, hlY + 4, 9, isLatest ? Theme.NeonGold : Theme.NeonCyan);
                }

                // Row 3: Points & Maps info (Y = ny + 64)
                string ptsMaps = $"ОЧКИ: {rec.Points:N0} PTS  •  КАРТЫ: {rec.MapsCompleted} ПРОЙДЕНО";
                Theme.DrawText(ptsMaps, cardX + 16, ny + 64, 9, Theme.TextWhite);
            }

            Raylib.EndScissorMode();

            if (maxScroll > 0)
            {
                int sbX = popX + popW - 16;
                int sbY = chainAreaY;
                int sbH = chainAreaH;
                float thumbPct = Math.Clamp(chainAreaH / (float)totalChainH, 0.15f, 1.0f);
                int thumbH = (int)(sbH * thumbPct);
                int thumbY = sbY + (int)((sbH - thumbH) * (_rankHistoryScrollY / maxScroll));
                Raylib.DrawRectangle(sbX, sbY, 4, sbH, new Color(255, 255, 255, 20));
                Raylib.DrawRectangle(sbX, thumbY, 4, thumbH, Theme.NeonGold);
            }
        }

        private void DrawConsoleLogModal(int screenWidth, int screenHeight, float scale)
        {
            Vector2 mouse = Raylib.GetMousePosition();

            // Dark blocking backdrop
            Raylib.DrawRectangle(0, 0, screenWidth, screenHeight, new Color(4, 7, 12, 240));

            int popW = Math.Min((int)(820 * scale), screenWidth - 30);
            int popH = Math.Min((int)(540 * scale), screenHeight - 40);
            int popX = (screenWidth - popW) / 2;
            int popY = (screenHeight - popH) / 2;

            Theme.DrawGlassPanel(popX, popY, popW, popH);
            Raylib.DrawRectangleLines(popX, popY, popW, popH, Theme.NeonCyan);

            // Modal Header
            int headH = (int)(42 * scale);
            Raylib.DrawRectangle(popX, popY, popW, headH, Theme.BgPanelHeader);
            Raylib.DrawLine(popX, popY + headH, popX + popW, popY + headH, Theme.Border);

            Theme.DrawText("ЛОГ ТЕЛЕМЕТРИИ ПРЫЖКА (CS2 CONSOLE & CKZ)", popX + 16, popY + (headH - Theme.GetScaledFontSize(12)) / 2, 12, Theme.NeonCyan);

            // Close [X] Button
            int closeBtnSize = (int)(26 * scale);
            int closeBtnX = popX + popW - closeBtnSize - 10;
            int closeBtnY = popY + (headH - closeBtnSize) / 2;
            if (Theme.DrawButton(closeBtnX, closeBtnY, closeBtnSize, closeBtnSize, "[X]", false, 10, enabled: true))
            {
                _showLogModal = false;
            }

            int contentY = popY + headH + 12;
            int contentW = popW - 32;
            int contentX = popX + 16;

            // Header Jump Title Line
            Theme.DrawText(_selectedLogTitle, contentX, contentY, 13, Theme.NeonGold);
            contentY += (int)(24 * scale);

            // COMPACT STRUCTURED TABLE
            if (_selectedLogBreakdown != null && _selectedLogBreakdown.Count > 0)
            {
                int tableH = Math.Min((int)(190 * scale), (popY + popH - contentY) / 2);
                Raylib.DrawRectangle(contentX, contentY, contentW, tableH, new Color(9, 13, 20, 255));
                Raylib.DrawRectangleLines(contentX, contentY, contentW, tableH, Theme.Border);

                // Dynamically sized columns
                int col0W = (int)(42 * scale);  // #
                int col1W = (int)(68 * scale);  // Sync
                int col2W = (int)(80 * scale);  // Gain
                int col3W = (int)(80 * scale);  // Loss
                int col4W = (int)(85 * scale);  // MaxSpeed
                int col5W = (int)(75 * scale);  // Airtime
                int col6W = (int)(75 * scale);  // Overlap
                int col7W = (int)(75 * scale);  // BadAngle
                int col8W = contentW - (col0W + col1W + col2W + col3W + col4W + col5W + col6W + col7W); // Width / Status

                // Table Header Row
                int thH = (int)(22 * scale);
                Raylib.DrawRectangle(contentX, contentY, contentW, thH, new Color(16, 23, 34, 255));
                Raylib.DrawLine(contentX, contentY + thH, contentX + contentW, contentY + thH, Theme.Border);

                int hx = contentX + 8;
                Theme.DrawText("СТР", hx, contentY + 5, 8, Theme.NeonCyan); hx += col0W;
                Theme.DrawText("СИНХРА", hx, contentY + 5, 8, Theme.TextMuted); hx += col1W;
                Theme.DrawText("GAIN (+)", hx, contentY + 5, 8, Theme.NeonGreen); hx += col2W;
                Theme.DrawText("LOSS (-)", hx, contentY + 5, 8, Theme.NeonRed); hx += col3W;
                Theme.DrawText("MAX-SPD", hx, contentY + 5, 8, Theme.NeonGold); hx += col4W;
                Theme.DrawText("AIRTIME", hx, contentY + 5, 8, Theme.TextMuted); hx += col5W;
                Theme.DrawText("OVERLAP", hx, contentY + 5, 8, Theme.TextMuted); hx += col6W;
                Theme.DrawText("BAD-ANG", hx, contentY + 5, 8, Theme.TextMuted); hx += col7W;
                Theme.DrawText("WIDTH°", hx, contentY + 5, 8, Theme.TextMuted);

                // Table Rows
                int rowH = (int)(18 * scale);
                int rY = contentY + thH + 2;

                for (int s = 0; s < _selectedLogBreakdown.Count && rY + rowH <= contentY + tableH; s++)
                {
                    var st = _selectedLogBreakdown[s];
                    if (s % 2 == 1)
                    {
                        Raylib.DrawRectangle(contentX, rY, contentW, rowH, new Color(255, 255, 255, 5));
                    }

                    int rx = contentX + 8;
                    Theme.DrawText($"S{st.StrafeIndex + 1}", rx, rY + 3, 8, Theme.NeonCyan); rx += col0W;
                    
                    Color syncCol = st.Sync >= 80 ? Theme.NeonGreen : (st.Sync >= 65 ? Theme.TextWhite : Theme.NeonOrange);
                    Theme.DrawText($"{st.Sync:F1}%", rx, rY + 3, 8, syncCol); rx += col1W;

                    Theme.DrawText($"+{st.Gain:F1}", rx, rY + 3, 8, Theme.NeonGreen); rx += col2W;
                    
                    Color lossCol = st.Loss > 5f ? Theme.NeonRed : Theme.TextDim;
                    Theme.DrawText($"-{st.Loss:F1}", rx, rY + 3, 8, lossCol); rx += col3W;

                    Theme.DrawText($"{st.MaxSpeed:F0}", rx, rY + 3, 8, Theme.NeonGold); rx += col4W;
                    Theme.DrawText($"{st.AirtimePct:F0}%", rx, rY + 3, 8, Theme.TextWhite); rx += col5W;

                    Color overCol = st.Overlap > 18f ? Theme.NeonRed : Theme.TextDim;
                    Theme.DrawText($"{st.Overlap:F1}ms", rx, rY + 3, 8, overCol); rx += col6W;

                    Color badCol = st.BadAngles > 10f ? Theme.NeonOrange : Theme.TextDim;
                    Theme.DrawText($"{st.BadAngles:F1}%", rx, rY + 3, 8, badCol); rx += col7W;

                    Theme.DrawText($"{st.WidthDeg:F1}°", rx, rY + 3, 8, Theme.TextWhite);

                    rY += rowH;
                }

                contentY += tableH + 10;
            }

            // RAW CONSOLE OUTPUT / DETAILS
            Theme.DrawText("ОРИГИНАЛЬНЫЙ КОНСОЛЬНЫЙ ВЫВОД (RAW CS2 CONSOLE):", contentX, contentY, 9, Theme.TextMuted);
            contentY += (int)(16 * scale);

            int rawH = popY + popH - contentY - 14;
            Raylib.DrawRectangle(contentX, contentY, contentW, rawH, new Color(7, 10, 16, 255));
            Raylib.DrawRectangleLines(contentX, contentY, contentW, rawH, Theme.Border);

            string[] lines = _selectedLogContent.Split('\n');
            int textY = contentY + 6;
            int maxLines = rawH / (int)(15 * scale);
            for (int l = 0; l < lines.Length && l < maxLines; l++)
            {
                Theme.DrawText(lines[l], contentX + 10, textY, 8, Theme.TextWhite);
                textY += (int)(15 * scale);
            }
        }

        private static void DrawHistoryStatPill(int px, int py, int pw, int ph, string title, string val, string sub, Color accent)
        {
            // Sleek card container with left accent
            Raylib.DrawRectangle(px, py, pw, ph, new Color(15, 21, 32, 230));
            Raylib.DrawRectangleLines(px, py, pw, ph, new Color(accent.R, accent.G, accent.B, (byte)75));
            Raylib.DrawRectangle(px, py, 3, ph, accent);

            int padLeft = 10;

            // Top-Left: Title + Subtitle
            Theme.DrawText(title, px + padLeft, py + 5, 8, Theme.TextMuted);
            int titleW = Theme.MeasureText(title, 8);
            if (!string.IsNullOrEmpty(sub))
            {
                Theme.DrawText($"({sub})", px + padLeft + titleW + 6, py + 5, 7, Theme.TextDim);
            }

            // Bottom-Left: Large Bold Value
            Theme.DrawText(val, px + padLeft, py + 20, 18, accent);
        }

        // =========================================================================
        // TAB 1: JUMP PBS (7 CYBERSHOKE TYPES) + LIVE CS2 TELEMETRY & FEED
        // =========================================================================
        private void DrawJumpPBsAndTelemetryTab(int x, int y, int w, int h, float scale, UserProfile prof, bool inputActive = true)
        {
            var cs = prof.Cybershoke;
            Vector2 mouse = inputActive ? Raylib.GetMousePosition() : new Vector2(-99999, -99999);

            int colGap = 16;
            int col1W = (int)(w * 0.56f);
            int col2W = w - col1W - colGap;
            int col1X = x;
            int col2X = col1X + col1W + colGap;

            Theme.DrawGlassPanel(col1X, y, col1W, h);

            int pHeaderH = (int)(38 * scale);
            Raylib.DrawRectangle(col1X, y, col1W, pHeaderH, Theme.BgPanelHeader);
            Raylib.DrawLine(col1X, y + pHeaderH, col1X + col1W, y + pHeaderH, Theme.Border);

            // Responsive Header Title & Action Buttons
            int curHRight = col1X + col1W - 10;
            int pbHistBtnH = (int)(24 * scale);
            int pbHistBtnY = y + (pHeaderH - pbHistBtnH) / 2;

            // Compute available action bar space and dynamic button widths
            int availBtnAreaW = Math.Max(260, col1W - (col1W > 580 ? 210 : 20));
            int bHistW = Math.Clamp((int)(availBtnAreaW * 0.40f), 105, (int)(140 * scale));
            int bBlockW = Math.Clamp((int)(availBtnAreaW * 0.30f), 78, (int)(100 * scale));
            int bDistW = Math.Clamp((int)(availBtnAreaW * 0.30f), 74, (int)(95 * scale));

            // [ИСТОРИЯ РЕКОРДОВ]
            curHRight -= bHistW;
            string histLbl = bHistW < 120 ? "ИСТОРИЯ" : "ИСТОРИЯ РЕКОРДОВ";
            if (Theme.DrawButton(curHRight, pbHistBtnY, bHistW, pbHistBtnH, histLbl, false, 8, enabled: inputActive))
            {
                _selectedPbHistoryJumpType = "All";
                _showPBHistoryModal = true;
            }
            curHRight -= 6;

            // [РЕЖИМ: ТОЛЬКО БЛОКИ]
            curHRight -= bBlockW;
            string blkModeLbl = bBlockW < 85 ? "БЛОКИ" : "БЛОКИ (PB)";
            if (Theme.DrawButton(curHRight, pbHistBtnY, bBlockW, pbHistBtnH, blkModeLbl, _pbViewMode == 1, 8, enabled: inputActive))
            {
                _pbViewMode = 1;
            }
            curHRight -= 4;

            // [РЕЖИМ: ДИСТАНЦИЯ (ВСЕ)]
            curHRight -= bDistW;
            string distModeLbl = bDistW < 80 ? "ДИСТ." : "ДИСТАНЦИЯ";
            if (Theme.DrawButton(curHRight, pbHistBtnY, bDistW, pbHistBtnH, distModeLbl, _pbViewMode == 0, 8, enabled: inputActive))
            {
                _pbViewMode = 0;
            }

            // Left Title (shows only when space is ample)
            if (curHRight - col1X > 140)
            {
                string headerTitle = (curHRight - col1X > 200) ? "РЕКОРДЫ ПРЫЖКОВ (7 ТИПОВ)" : "РЕКОРДЫ";
                Theme.DrawText(headerTitle, col1X + 14, y + (pHeaderH - Theme.GetScaledFontSize(10)) / 2, 10, Theme.NeonCyan);
            }

            int pbAreaY = y + pHeaderH + 8;
            int pbAreaH = h - pHeaderH - 16;
            var jumpTypes = CybershokeKzProfile.StandardJumpTypes;

            int cardCols = 2;
            int cardGap = 8;
            int standardCardW = (col1W - 32 - cardGap) / cardCols;
            int cardH = (int)(118 * scale);
            int totalPbRows = (jumpTypes.Length + 1) / cardCols;
            int totalPbH = totalPbRows * (cardH + cardGap) + 10;

            float maxScroll = Math.Max(0, totalPbH - pbAreaH);
            if (inputActive && mouse.X >= col1X && mouse.X <= col1X + col1W && mouse.Y >= pbAreaY && mouse.Y <= pbAreaY + pbAreaH)
            {
                _pbScrollY -= Raylib.GetMouseWheelMove() * 35;
                _pbScrollY = Math.Clamp(_pbScrollY, 0, maxScroll);
            }

            Raylib.BeginScissorMode(col1X + 8, pbAreaY, col1W - 16, pbAreaH);
            for (int i = 0; i < jumpTypes.Length; i++)
            {
                string jType = jumpTypes[i];
                var pb = cs.GetOrCreate(jType);
                var (typeName, shortCode, accentColor) = CybershokeKzProfile.GetJumpTypeMeta(jType);

                int row = i / cardCols;
                int col = i % cardCols;
                int cardW = (i == jumpTypes.Length - 1 && jumpTypes.Length % 2 == 1) ? (col1W - 32) : standardCardW;
                int cx = col1X + 16 + (cardW == col1W - 32 ? 0 : col * (standardCardW + cardGap));
                int cy = pbAreaY + 4 + row * (cardH + cardGap) - (int)_pbScrollY;

                if (cy + cardH < pbAreaY - 20 || cy > pbAreaY + pbAreaH + 20) continue;

                bool isHover = inputActive && mouse.X >= cx && mouse.X <= cx + cardW && mouse.Y >= cy && mouse.Y <= cy + cardH;
                if (isHover && Raylib.IsMouseButtonPressed(MouseButton.Left) && mouse.Y < cy + (int)(32 * scale))
                {
                    _selectedPbHistoryJumpType = jType;
                    _showPBHistoryModal = true;
                }

                Color cardBg = isHover ? new Color(22, 32, 48, 245) : new Color(12, 17, 25, 200);
                Raylib.DrawRectangle(cx, cy, cardW, cardH, cardBg);
                Raylib.DrawRectangleLines(cx, cy, cardW, cardH, isHover ? accentColor : new Color(accentColor.R, accentColor.G, accentColor.B, (byte)70));
                Raylib.DrawRectangle(cx, cy, 4, cardH, accentColor);

                // Row 1: Left: [CODE] TYPE NAME. Right: Direction Selector Pills [ПРЯМО | БОКОМ | СПИНОЙ]
                int badgeW = (int)(36 * scale);
                int badgeH = (int)(18 * scale);
                Raylib.DrawRectangle(cx + 10, cy + 8, badgeW, badgeH, new Color(accentColor.R, accentColor.G, accentColor.B, (byte)35));
                Raylib.DrawRectangleLines(cx + 10, cy + 8, badgeW, badgeH, accentColor);
                Theme.DrawText(shortCode, cx + 10 + (badgeW - Theme.GetScaledFontSize(8)) / 2, cy + 8 + (badgeH - Theme.GetScaledFontSize(8)) / 2, 8, accentColor);

                Theme.DrawText(typeName.ToUpper(), cx + 10 + badgeW + 8, cy + 10, 10, Theme.TextWhite);

                // Direction Switcher Pills inside each card (FWD / SW / BW)
                int dirPillW = (int)(34 * scale);
                int dirPillH = (int)(17 * scale);
                int dirPillY = cy + 8;
                int dirPillX = cx + cardW - (dirPillW * 3 + 6) - 10;

                string[] dirLabels = { "FWD", "SW", "BW" };
                for (int d = 0; d < 3; d++)
                {
                    int px = dirPillX + d * (dirPillW + 3);
                    bool isCurDir = (_pbDirFilter == d);
                    if (Theme.DrawButton(px, dirPillY, dirPillW, dirPillH, dirLabels[d], isCurDir, 7, enabled: inputActive))
                    {
                        _pbDirFilter = d;
                    }
                }

                // Resolve values for the currently selected Direction (0 = FWD, 1 = SW, 2 = BW)
                float activeDist = _pbDirFilter switch
                {
                    1 => pb.SwPBDist,
                    2 => pb.BwPBDist,
                    _ => pb.PBDist
                };

                float activeBlock = _pbDirFilter switch
                {
                    1 => pb.SwBlockPB > 0 ? pb.SwBlockPB : pb.SwPBBlockDist,
                    2 => pb.BwBlockPB > 0 ? pb.BwBlockPB : pb.BwPBBlockDist,
                    _ => pb.BlockPB > 0 ? pb.BlockPB : pb.PBBlockDist
                };

                int activeStrafes = _pbDirFilter switch
                {
                    1 => pb.SwPBStrafes,
                    2 => pb.BwPBStrafes,
                    _ => pb.PBStrafes
                };

                float activeSync = _pbDirFilter switch
                {
                    1 => pb.SwPBSync,
                    2 => pb.BwPBSync,
                    _ => pb.PBSync
                };

                float activePre = _pbDirFilter switch
                {
                    1 => pb.SwPBPreSpeed,
                    2 => pb.BwPBPreSpeed,
                    _ => pb.PBPreSpeed
                };

                DateTime activeDate = _pbDirFilter switch
                {
                    1 => pb.SwPBDate,
                    2 => pb.BwPBDate,
                    _ => pb.PBDate
                };

                bool hasRecord = _pbViewMode == 1 ? (activeBlock > 0) : (activeDist > 0);

                if (hasRecord)
                {
                    if (_pbViewMode == 1) // БЛОКИ (BLOCK PBS MODE)
                    {
                        // Row 2: Block Distance as Main Headline
                        string blkMainStr = $"БЛОК {activeBlock:F0}";
                        var (bTierName, _, bTierCol, _) = GetBlockTier(jType, activeBlock);
                        Theme.DrawText(blkMainStr, cx + 12, cy + (int)(34 * scale), 18, bTierCol);

                        int blkTextW = Theme.MeasureText(blkMainStr, 18);
                        int curBadgeAnchorX = cx + 12 + blkTextW + 10;

                        // Tier Badge
                        if (bTierName != "NORMAL")
                        {
                            int tBadgeW = Theme.MeasureText(bTierName, 8) + 10;
                            int tBadgeH = (int)(17 * scale);
                            Raylib.DrawRectangle(curBadgeAnchorX, cy + (int)(35 * scale), tBadgeW, tBadgeH, new Color(bTierCol.R, bTierCol.G, bTierCol.B, (byte)35));
                            Raylib.DrawRectangleLines(curBadgeAnchorX, cy + (int)(35 * scale), tBadgeW, tBadgeH, bTierCol);
                            Theme.DrawText(bTierName, curBadgeAnchorX + 5, cy + (int)(38 * scale), 8, bTierCol);
                            curBadgeAnchorX += tBadgeW + 6;
                        }

                        // Row 3: Strafe metrics & Actual Jump Distance
                        string row1 = $"{activeDist:F2}u  •  {activeStrafes} str  •  {activeSync:F0}% sync";
                        Theme.DrawText(row1, cx + 12, cy + (int)(62 * scale), 9, Theme.TextDim);
                    }
                    else // ДИСТАНЦИЯ (DISTANCE PBS MODE)
                    {
                        // Row 2: Distance + units + Tier Badge + Block PB Badge
                        string distStr = $"{activeDist:F2}";
                        var (dTierName, _, dTierCol, _) = GetKzTier(jType, activeDist);
                        Theme.DrawText(distStr, cx + 12, cy + (int)(34 * scale), 18, dTierCol);

                        int distTextW = (int)(distStr.Length * 10.2f * scale);
                        int unitsX = cx + 12 + distTextW + 4;
                        Theme.DrawText("units", unitsX, cy + (int)(40 * scale), 8, Theme.TextDim);

                        int curBadgeAnchorX = unitsX + (int)(32 * scale);

                        // Dedicated Tier Badge (e.g. GODLIKE / OWNAGE / PERFECT / IMPRESSIVE)
                        if (dTierName != "NORMAL")
                        {
                            int tBadgeW = Theme.MeasureText(dTierName, 8) + 10;
                            int tBadgeH = (int)(17 * scale);
                            Raylib.DrawRectangle(curBadgeAnchorX, cy + (int)(35 * scale), tBadgeW, tBadgeH, new Color(dTierCol.R, dTierCol.G, dTierCol.B, (byte)35));
                            Raylib.DrawRectangleLines(curBadgeAnchorX, cy + (int)(35 * scale), tBadgeW, tBadgeH, dTierCol);
                            Theme.DrawText(dTierName, curBadgeAnchorX + 5, cy + (int)(38 * scale), 8, dTierCol);
                            curBadgeAnchorX += tBadgeW + 6;
                        }

                        // Block PB Badge with Tier Color (e.g. [БЛОК 270: OWNAGE])
                        if (activeBlock > 0)
                        {
                            var (bTierName, _, bTierCol, _) = GetBlockTier(jType, activeBlock);
                            string blkText = $"Блок {activeBlock:F0} [{bTierName}]";
                            int blkW = Theme.MeasureText(blkText, 8) + 10;
                            int blkH = (int)(17 * scale);
                            if (curBadgeAnchorX + blkW <= cx + cardW - 8)
                            {
                                Raylib.DrawRectangle(curBadgeAnchorX, cy + (int)(35 * scale), blkW, blkH, new Color(bTierCol.R, bTierCol.G, bTierCol.B, (byte)35));
                                Raylib.DrawRectangleLines(curBadgeAnchorX, cy + (int)(35 * scale), blkW, blkH, bTierCol);
                                Theme.DrawText(blkText, curBadgeAnchorX + 5, cy + (int)(38 * scale), 8, bTierCol);
                                curBadgeAnchorX += blkW + 6;
                            }
                        }

                        // Row 3: Strafe metrics & Block PB breakdown if present
                        string row1 = $"{activeStrafes} str  •  {activeSync:F0}% sync  •  {activePre:F0} pre";
                        if (activeBlock > 0)
                        {
                            row1 += $"  •  PB Блок: {activeBlock:F0}";
                        }
                        Theme.DrawText(row1, cx + 12, cy + (int)(62 * scale), 9, Theme.TextDim);
                    }

                    // Row 4: Date & averages
                    string dateStr = activeDate != DateTime.MinValue ? activeDate.ToString("dd.MM.yyyy HH:mm") : "";
                    string row2 = !string.IsNullOrEmpty(dateStr) ? $"Дата рекорда: {dateStr}" : $"Ср: {pb.AvgDist:F1}u ({pb.AvgSync:F0}% sync)";
                    Theme.DrawText(row2, cx + 12, cy + (int)(84 * scale), 8, Theme.TextMuted);
                }
                else
                {
                    string dirName = _pbDirFilter == 1 ? "БОКОМ (SW)" : (_pbDirFilter == 2 ? "СПИНОЙ (BW)" : "ПРЯМО (FWD)");
                    Theme.DrawText($"НЕТ ЗАПИСИ {dirName}", cx + 12, cy + (int)(42 * scale), 12, Theme.TextMuted);
                    Theme.DrawText($"Совершите прыжок {dirName} на сервере CS2...", cx + 12, cy + (int)(70 * scale), 8, Theme.TextDim);
                }
            }
            Raylib.EndScissorMode();

            if (maxScroll > 0)
            {
                int sbX = col1X + col1W - 8;
                int sbY = pbAreaY;
                int sbH = pbAreaH;
                float thumbPct = Math.Clamp(pbAreaH / (float)totalPbH, 0.2f, 1.0f);
                int thumbH = (int)(sbH * thumbPct);
                int thumbY = sbY + (int)((sbH - thumbH) * (_pbScrollY / maxScroll));
                Raylib.DrawRectangle(sbX, sbY, 3, sbH, new Color(255, 255, 255, 20));
                Raylib.DrawRectangle(sbX, thumbY, 3, thumbH, Theme.NeonCyan);
            }

            if (maxScroll > 0)
            {
                int sbX = col1X + col1W - 8;
                int sbY = pbAreaY;
                int sbH = pbAreaH;
                float thumbPct = Math.Clamp(pbAreaH / (float)totalPbH, 0.2f, 1.0f);
                int thumbH = (int)(sbH * thumbPct);
                int thumbY = sbY + (int)((sbH - thumbH) * (_pbScrollY / maxScroll));
                Raylib.DrawRectangle(sbX, sbY, 3, sbH, new Color(255, 255, 255, 20));
                Raylib.DrawRectangle(sbX, thumbY, 3, thumbH, Theme.NeonCyan);
            }

            Theme.DrawGlassPanel(col2X, y, col2W, h);

            Raylib.DrawRectangle(col2X, y, col2W, pHeaderH, Theme.BgPanelHeader);
            Raylib.DrawLine(col2X, y + pHeaderH, col2X + col2W, y + pHeaderH, Theme.Border);
            Theme.DrawText("ТЕЛЕМЕТРИЯ И ЛАЙВ-ФИД ПРЫЖКОВ CS2", col2X + 16, y + (pHeaderH - Theme.GetScaledFontSize(12)) / 2, 12, Theme.NeonCyan);

            int ry = y + pHeaderH + 12;

            int tileGap = 8;
            int tileW = (col2W - 32 - tileGap) / 2;
            int tileH = (int)(68 * scale);

            float avgSync = cs.OverallAvgSync > 0 ? cs.OverallAvgSync : (prof.TotalStrafes > 0 ? prof.LifetimeAvgSync : 0f);
            float avgPre = cs.OverallAvgPreSpeed > 0 ? cs.OverallAvgPreSpeed : 0f;
            float avgOvr = cs.OverallAvgOverlap > 0 ? cs.OverallAvgOverlap : 0f;
            float avgBad = cs.OverallAvgBadAngles > 0 ? cs.OverallAvgBadAngles : 0f;

            string syncStr = avgSync > 0 ? $"{avgSync:F1}%" : "-";
            string preStr = avgPre > 0 ? $"{avgPre:F1} u/s" : "-";
            string ovrStr = avgOvr > 0 ? $"{avgOvr:F1} ms" : "-";
            string badStr = avgBad > 0 ? $"{avgBad:F1}%" : "-";

            DrawQualityTile(col2X + 16, ry, tileW, tileH, "СРЕДНЯЯ СИНХРА", syncStr, "Качественные прыжки", avgSync > 0 ? Theme.NeonGreen : Theme.TextDim);
            DrawQualityTile(col2X + 16 + tileW + tileGap, ry, tileW, tileH, "СРЕДНИЙ PRE-SPEED", preStr, "Скорость на отрыве", avgPre > 0 ? Theme.NeonCyan : Theme.TextDim);
            DrawQualityTile(col2X + 16, ry + tileH + tileGap, tileW, tileH, "OVERLAP (ЗАЖАТИЕ A+D)", ovrStr, "Низкий = лучше", avgOvr > 0 ? (avgOvr < 25 ? Theme.NeonGreen : Theme.NeonOrange) : Theme.TextDim);
            DrawQualityTile(col2X + 16 + tileW + tileGap, ry + tileH + tileGap, tileW, tileH, "ОШИБКИ УГЛОВ (BAD ANGLES)", badStr, "Потери стрейфов", avgBad > 0 ? (avgBad < 10 ? Theme.NeonGreen : Theme.NeonOrange) : Theme.TextDim);

            ry += (tileH * 2 + tileGap * 2 + 10);

            Raylib.DrawLine(col2X + 16, ry, col2X + col2W - 16, ry, Theme.Border);
            ry += 8;
            Theme.DrawText("ПОСЛЕДНИЕ ПРЫЖКИ В CS2 (РЕАЛЬНОЕ ВРЕМЯ):", col2X + 16, ry, 10, Theme.NeonGold);
            ry += (int)(18 * scale);

            int feedH = h - (ry - y) - 16;
            Raylib.DrawRectangle(col2X + 16, ry, col2W - 32, feedH, Theme.BgDark);
            Raylib.DrawRectangleLines(col2X + 16, ry, col2W - 32, feedH, Theme.Border);

            var jumps = cs.RecentJumps;
            if (jumps.Count == 0)
            {
                int emptyY = ry + feedH / 2 - 10;
                Theme.DrawText("Ожидание прыжков на сервере...", col2X + 32, emptyY, 11, Theme.TextDim);
                Theme.DrawText("Прыгайте на сервере Cybershoke — данные отобразятся мгновенно!", col2X + 32, emptyY + 18, 9, Theme.TextMuted);
            }
            else
            {
                int rowH = (int)(26 * scale);
                int maxVisible = feedH / rowH;
                int count = Math.Min(jumps.Count, maxVisible);

                for (int j = 0; j < count; j++)
                {
                    var jmp = jumps[j];
                    int jRowY = ry + 4 + j * rowH;
                    bool isEven = j % 2 == 0;
                    if (isEven) Raylib.DrawRectangle(col2X + 18, jRowY, col2W - 36, rowH - 2, new Color(255, 255, 255, 4));

                    var (tName, sCode, accCol) = CybershokeKzProfile.GetJumpTypeMeta(jmp.JumpType);
                    var (distTierName, _, distTierCol, _) = GetKzTier(jmp.JumpType, jmp.Distance);

                    Theme.DrawText(sCode, col2X + 24, jRowY + 4, 10, accCol);
                    Theme.DrawText($"{jmp.Distance:F2}u", col2X + (int)(75 * scale), jRowY + 4, 11, distTierCol);

                    int detailX = col2X + (int)(160 * scale);
                    if (jmp.BlockDistance > 0)
                    {
                        var (bTierName, _, bTierCol, _) = GetBlockTier(jmp.JumpType, jmp.BlockDistance);
                        string blkTag = $"[Блок {jmp.BlockDistance:F0}: {bTierName}] ";
                        Theme.DrawText(blkTag, detailX, jRowY + 5, 9, bTierCol);
                        detailX += Theme.MeasureText(blkTag, 9) + 4;
                    }

                    string detailStr = $"{jmp.Strafes} str  •  {jmp.Sync:F0}% sync  •  {jmp.PreSpeed:F0} pre";
                    Theme.DrawText(detailStr, detailX, jRowY + 5, 9, Theme.TextDim);

                    if (jmp.IsPB)
                    {
                        int pbBadgeW = (int)(48 * scale);
                        Raylib.DrawRectangle(col2X + col2W - 32 - pbBadgeW - 8, jRowY + 2, pbBadgeW, rowH - 6, new Color(255, 215, 0, 40));
                        Raylib.DrawRectangleLines(col2X + col2W - 32 - pbBadgeW - 8, jRowY + 2, pbBadgeW, rowH - 6, Theme.NeonGold);
                        Theme.DrawText("PB!", col2X + col2W - 32 - pbBadgeW + 12, jRowY + 4, 9, Theme.NeonGold);
                    }
                }
            }
        }

        private static void DrawQualityTile(int tx, int ty, int tw, int th, string title, string val, string sub, Color accent)
        {
            Raylib.DrawRectangle(tx, ty, tw, th, new Color(13, 18, 27, 220));
            Raylib.DrawRectangleLines(tx, ty, tw, th, new Color(accent.R, accent.G, accent.B, (byte)70));
            Raylib.DrawRectangle(tx, ty, 3, th, accent);

            Theme.DrawText(title, tx + 10, ty + 6, 8, Theme.TextMuted);
            Theme.DrawText(val, tx + 10, ty + 18, 16, accent);
            Theme.DrawText(sub, tx + 10, ty + 46, 8, Theme.TextDim);
        }

        // =========================================================================
        // TAB 2: FULLY RESPONSIVE KZ MAPS LEADERBOARD TABLE (100% WIDTH ADAPTIVE)
        // =========================================================================
        private void DrawKzMapsLeaderboardTab(int x, int y, int w, int h, float scale, UserProfile prof, bool inputActive = true)
        {
            var cs = prof.Cybershoke;
            var maps = cs.CompletedMaps;
            Vector2 mouse = inputActive ? Raylib.GetMousePosition() : new Vector2(-99999, -99999);

            Theme.DrawGlassPanel(x, y, w, h);

            int pHeaderH = (int)(44 * scale);
            Raylib.DrawRectangle(x, y, w, pHeaderH, Theme.BgPanelHeader);
            Raylib.DrawLine(x, y + pHeaderH, x + w, y + pHeaderH, Theme.Border);

            int cx = x + 16;
            Theme.DrawText("ТАБЛИЦА ПРОЙДЕННЫХ КАРТ KZ (CYBERSHOKE LEADERBOARD)", cx, y + (pHeaderH - Theme.GetScaledFontSize(12)) / 2, 12, Theme.NeonCyan);

            int searchW = Math.Min((int)(260 * scale), w / 3);
            int searchH = (int)(28 * scale);
            int searchX = x + w - searchW - 16;
            int searchY = y + (pHeaderH - searchH) / 2;

            bool isHoverSearch = inputActive && mouse.X >= searchX && mouse.X <= searchX + searchW && mouse.Y >= searchY && mouse.Y <= searchY + searchH;
            if (inputActive && Raylib.IsMouseButtonPressed(MouseButton.Left))
            {
                _isSearchingMaps = isHoverSearch;
            }

            if (_isSearchingMaps && inputActive)
            {
                int key = Raylib.GetCharPressed();
                while (key > 0)
                {
                    if (key >= 32 && key <= 126 && _mapSearchQuery.Length < 30)
                        _mapSearchQuery += (char)key;
                    key = Raylib.GetCharPressed();
                }
                if (Raylib.IsKeyPressed(KeyboardKey.Backspace) && _mapSearchQuery.Length > 0)
                {
                    _mapSearchQuery = _mapSearchQuery[..^1];
                }
            }

            Raylib.DrawRectangle(searchX, searchY, searchW, searchH, new Color(10, 15, 22, 255));
            Raylib.DrawRectangleLines(searchX, searchY, searchW, searchH, _isSearchingMaps ? Theme.NeonCyan : Theme.Border);
            string searchDisplay = string.IsNullOrEmpty(_mapSearchQuery) ? (_isSearchingMaps ? "|" : "Поиск по названию карты...") : $"Поиск: {_mapSearchQuery}";
            Color searchColor = string.IsNullOrEmpty(_mapSearchQuery) ? Theme.TextDim : Theme.TextWhite;
            Theme.DrawText(searchDisplay, searchX + 8, searchY + (searchH - Theme.GetScaledFontSize(10)) / 2, 10, searchColor);

            if (!string.IsNullOrEmpty(_mapSearchQuery))
            {
                int clearBtnW = (int)(22 * scale);
                int clearBtnX = searchX + searchW - clearBtnW - 4;
                int clearBtnY = searchY + (searchH - clearBtnW) / 2;
                if (Theme.DrawButton(clearBtnX, clearBtnY, clearBtnW, clearBtnW, "X", false, 9, enabled: inputActive))
                {
                    _mapSearchQuery = "";
                }
            }

            int toolY = y + pHeaderH + 8;
            int toolH = (int)(32 * scale);
            Raylib.DrawRectangle(x + 16, toolY, w - 32, toolH, new Color(14, 20, 29, 200));
            Raylib.DrawRectangleLines(x + 16, toolY, w - 32, toolH, Theme.Border);

            int btnX = x + 24;
            int btnH = (int)(24 * scale);
            int by = toolY + (toolH - btnH) / 2;

            Theme.DrawText("СОРТИРОВКА:", btnX, by + (btnH - Theme.GetScaledFontSize(9)) / 2, 9, Theme.TextMuted);
            btnX += (int)(85 * scale);

            int sBtnW = (int)(85 * scale);
            if (Theme.DrawButton(btnX, by, sBtnW, btnH, "По очкам", _mapSortMode == 0, 9, enabled: inputActive)) _mapSortMode = 0;
            btnX += sBtnW + 6;
            if (Theme.DrawButton(btnX, by, sBtnW, btnH, "По топу", _mapSortMode == 1, 9, enabled: inputActive)) _mapSortMode = 1;
            btnX += sBtnW + 6;
            if (Theme.DrawButton(btnX, by, sBtnW, btnH, "По времени", _mapSortMode == 2, 9, enabled: inputActive)) _mapSortMode = 2;
            btnX += sBtnW + 6;
            if (Theme.DrawButton(btnX, by, sBtnW, btnH, "По попыткам", _mapSortMode == 3, 9, enabled: inputActive)) _mapSortMode = 3;
            btnX += sBtnW + 6;
            if (Theme.DrawButton(btnX, by, sBtnW, btnH, "По имени", _mapSortMode == 4, 9, enabled: inputActive)) _mapSortMode = 4;
            btnX += sBtnW + 10;

            int top100BtnW = (int)(105 * scale);
            if (Theme.DrawButton(btnX, by, top100BtnW, btnH, _filterOnlyTop100 ? "Топ-100: ВКЛ" : "Только Топ-100", _filterOnlyTop100, 9, enabled: inputActive))
            {
                _filterOnlyTop100 = !_filterOnlyTop100;
            }

            float totalMapPts = maps.Sum(m => m.Points);
            int displayCount = maps.Count;
            string summaryStr = $"Карт: {displayCount} • Сумма: {totalMapPts:F1} PTS";
            int sumW = (int)(210 * scale);
            if (x + w - 32 - sumW > btnX + top100BtnW + 10)
            {
                Theme.DrawText(summaryStr, x + w - 32 - sumW, by + (btnH - Theme.GetScaledFontSize(9)) / 2, 9, Theme.NeonCyan);
            }

            int tableY = toolY + toolH + 8;
            int colHeaderH = (int)(28 * scale);
            int tableAvailW = w - 32;

            Raylib.DrawRectangle(x + 16, tableY, tableAvailW, colHeaderH, new Color(18, 25, 36, 255));
            Raylib.DrawRectangleLines(x + 16, tableY, tableAvailW, colHeaderH, Theme.Border);

            int colIdxW = (int)(tableAvailW * 0.05f);
            int colNameW = (int)(tableAvailW * 0.28f);
            int colTimeW = (int)(tableAvailW * 0.16f);
            int colRankW = (int)(tableAvailW * 0.18f);
            int colAttW = (int)(tableAvailW * 0.11f);
            int colPtsW = (int)(tableAvailW * 0.11f);

            int thX = x + 24;
            Theme.DrawText("#", thX, tableY + 8, 9, Theme.TextMuted);
            thX += colIdxW;
            Theme.DrawText("НАЗВАНИЕ КАРТЫ", thX, tableY + 8, 9, Theme.TextMuted);
            thX += colNameW;
            Theme.DrawText("РЕКОРДНОЕ ВРЕМЯ (PB)", thX, tableY + 8, 9, Theme.TextMuted);
            thX += colTimeW;
            Theme.DrawText("МЕСТО В ТОПЕ КАРТЫ", thX, tableY + 8, 9, Theme.TextMuted);
            thX += colRankW;
            Theme.DrawText("ПОПЫТКИ", thX, tableY + 8, 9, Theme.TextMuted);
            thX += colAttW;
            Theme.DrawText("ОЧКИ (PTS)", thX, tableY + 8, 9, Theme.TextMuted);
            thX += colPtsW;
            Theme.DrawText("ДАТА РЕКОРДА", thX, tableY + 8, 9, Theme.TextMuted);

            int listY = tableY + colHeaderH + 2;
            int listH = h - (listY - y) - 12;

            IEnumerable<KzMapRecord> filtered = maps;
            if (!string.IsNullOrWhiteSpace(_mapSearchQuery))
            {
                string q = _mapSearchQuery.Trim().ToLower();
                filtered = filtered.Where(m => m.MapName.ToLower().Contains(q));
            }
            if (_filterOnlyTop100)
            {
                filtered = filtered.Where(m => m.RankOnMap > 0 && m.RankOnMap <= 100);
            }

            var sortedList = _mapSortMode switch
            {
                0 => filtered.OrderByDescending(m => m.Points).ToList(),
                1 => filtered.OrderBy(m => m.RankOnMap > 0 ? m.RankOnMap : 999999).ToList(),
                2 => filtered.OrderBy(m => m.TimeStr).ToList(),
                3 => filtered.OrderByDescending(m => m.Attempts).ToList(),
                4 => filtered.OrderBy(m => m.MapName).ToList(),
                _ => filtered.ToList()
            };

            int rowH = (int)(32 * scale);
            int totalContentH = sortedList.Count * rowH;
            float maxScroll = Math.Max(0, totalContentH - listH);

            if (inputActive && mouse.X >= x + 16 && mouse.X <= x + w - 16 && mouse.Y >= listY && mouse.Y <= listY + listH)
            {
                _mapsScrollY -= Raylib.GetMouseWheelMove() * 45;
                _mapsScrollY = Math.Clamp(_mapsScrollY, 0, maxScroll);
            }

            Raylib.BeginScissorMode(x + 16, listY, tableAvailW, listH);
            for (int i = 0; i < sortedList.Count; i++)
            {
                var map = sortedList[i];
                int ry = listY + i * rowH - (int)_mapsScrollY;

                if (ry + rowH < listY - 20 || ry > listY + listH + 20) continue;

                bool isHover = inputActive && mouse.X >= x + 16 && mouse.X <= x + w - 16 && mouse.Y >= ry && mouse.Y <= ry + rowH;
                Color rowBg = isHover ? new Color(22, 32, 48, 220) : (i % 2 == 0 ? new Color(13, 18, 27, 180) : new Color(10, 14, 21, 180));
                Raylib.DrawRectangle(x + 16, ry, tableAvailW, rowH - 2, rowBg);

                if (isHover)
                {
                    Raylib.DrawRectangleLines(x + 16, ry, tableAvailW, rowH - 2, new Color(0, 240, 255, 90));
                }

                int rx = x + 24;

                Theme.DrawText($"{i + 1}", rx, ry + 9, 9, Theme.TextDim);
                rx += colIdxW;

                Color mapCol = map.RankOnMap is > 0 and <= 100 ? Theme.NeonGold : Theme.TextWhite;
                Theme.DrawText(map.MapName, rx, ry + 8, 11, mapCol);
                rx += colNameW;

                Theme.DrawText(map.TimeStr, rx, ry + 8, 10, Theme.NeonCyan);
                rx += colTimeW;

                if (map.RankOnMap > 0)
                {
                    if (map.RankOnMap <= 50)
                    {
                        int pillW = Math.Min((int)(120 * scale), colRankW - 10);
                        Raylib.DrawRectangle(rx, ry + 4, pillW, rowH - 10, new Color(255, 215, 0, 45));
                        Raylib.DrawRectangleLines(rx, ry + 4, pillW, rowH - 10, Theme.NeonGold);
                        Theme.DrawText($"TOP-50 #{map.PositionStr}", rx + 6, ry + 8, 9, Theme.NeonGold);
                    }
                    else if (map.RankOnMap <= 100)
                    {
                        int pillW = Math.Min((int)(120 * scale), colRankW - 10);
                        Raylib.DrawRectangle(rx, ry + 4, pillW, rowH - 10, new Color(0, 255, 128, 35));
                        Raylib.DrawRectangleLines(rx, ry + 4, pillW, rowH - 10, Theme.NeonGreen);
                        Theme.DrawText($"TOP-100 #{map.PositionStr}", rx + 6, ry + 8, 9, Theme.NeonGreen);
                    }
                    else
                    {
                        Theme.DrawText($"#{map.PositionStr}", rx, ry + 8, 10, Theme.TextWhite);
                    }
                }
                else
                {
                    Theme.DrawText(map.PositionStr, rx, ry + 8, 10, Theme.TextDim);
                }
                rx += colRankW;

                Theme.DrawText($"{map.Attempts}", rx, ry + 8, 10, Theme.TextWhite);
                rx += colAttW;

                string ptsStr = map.Points > 0 ? $"+{map.Points:F2}" : "0.00";
                Theme.DrawText(ptsStr, rx, ry + 8, 11, Theme.NeonCyan);
                rx += colPtsW;

                Theme.DrawText(map.DateStr, rx, ry + 9, 9, Theme.TextDim);
            }
            Raylib.EndScissorMode();

            if (maxScroll > 0)
            {
                int sbX = x + w - 24;
                int sbY = listY;
                int sbH = listH;
                float thumbPct = Math.Clamp(listH / (float)totalContentH, 0.15f, 1.0f);
                int thumbH = (int)(sbH * thumbPct);
                int thumbY = sbY + (int)((sbH - thumbH) * (_mapsScrollY / maxScroll));
                Raylib.DrawRectangle(sbX, sbY, 4, sbH, new Color(255, 255, 255, 20));
                Raylib.DrawRectangle(sbX, thumbY, 4, thumbH, Theme.NeonCyan);
            }
        }

        // =========================================================================
        // TAB 3: DEEP AI & BIOMECHANICS ANALYTICS (PER-JUMP-TYPE FILTERED GRAPH & DRILLS)
        // =========================================================================
        private void DrawDeepAnalyticsTab(int x, int y, int w, int h, float scale, UserProfile prof, bool inputActive = true)
        {
            var cs = prof.Cybershoke;
            Vector2 mouse = inputActive ? Raylib.GetMousePosition() : new Vector2(-99999, -99999);

            int colGap = 14;
            // Adaptive 50/50 or 46/54 split based on available width
            float splitRatio = w > 1200 ? 0.48f : 0.45f;
            int col1W = (int)(w * splitRatio);
            int col2W = w - col1W - colGap;
            int col1X = x;
            int col2X = col1X + col1W + colGap;

            // =========================================================================
            // LEFT COLUMN: TIMELINE GRAPH & HAND BALANCE (A vs D)
            // =========================================================================
            Theme.DrawGlassPanel(col1X, y, col1W, h);

            int pHeaderH = (int)(38 * scale);
            Raylib.DrawRectangle(col1X, y, col1W, pHeaderH, Theme.BgPanelHeader);
            Raylib.DrawLine(col1X, y + pHeaderH, col1X + col1W, y + pHeaderH, Theme.Border);
            
            // Left Title (clipped/adapted if space is tight)
            string graphTitle = col1W > 450 ? "ГРАФИК ПРОГРЕССА" : "ПРОГРЕСС";
            Theme.DrawText(graphTitle, col1X + 14, y + (pHeaderH - Theme.GetScaledFontSize(11)) / 2, 11, Theme.NeonCyan);

            // Adaptive Metric Buttons on the Right Header
            string[] mNames = { "Дистанц.", "Синхра", "PreSpd", "Overlap" };
            int gBtnH = (int)(22 * scale);
            int gBtnW = Math.Clamp((col1W - Theme.MeasureText(graphTitle, 11) - 40) / 4, 45, (int)(68 * scale));
            int gTotalW = gBtnW * 4 + 9;
            int gBtnX = col1X + col1W - gTotalW - 10;
            int gBtnY = y + (pHeaderH - gBtnH) / 2;

            for (int m = 0; m < 4; m++)
            {
                if (Theme.DrawButton(gBtnX, gBtnY, gBtnW, gBtnH, mNames[m], _graphMetric == m, 8, enabled: inputActive))
                {
                    _graphMetric = m;
                }
                gBtnX += gBtnW + 3;
            }

            // Jump Type Selector Pills (Equal Width fitting col1W - 28)
            int jToolY = y + pHeaderH + 6;
            int jToolH = (int)(26 * scale);
            int availPillsW = col1W - 28;
            string[] quickTypes = { "Long Jump", "Bunnyhop", "Multi Bunnyhop", "Weird Jump", "Ladder Jump", "Sideways Jump", "Backwards Jump" };
            int jbW = Math.Max(38, (availPillsW - (quickTypes.Length - 1) * 3) / quickTypes.Length);
            int jbX = col1X + 14;

            foreach (var jt in quickTypes)
            {
                var (_, code, _) = CybershokeKzProfile.GetJumpTypeMeta(jt);
                if (Theme.DrawButton(jbX, jToolY, jbW, jToolH, code, _selectedGraphJumpType == jt, 8, enabled: inputActive))
                {
                    _selectedGraphJumpType = jt;
                }
                jbX += jbW + 3;
            }

            // Sample Size Selector Bar (Выборка: 10, 20, 50, 100 прыжков)
            int sampleToolY = jToolY + jToolH + 5;
            int sampleToolH = (int)(20 * scale);
            Theme.DrawText("ВЫБОРКА:", col1X + 14, sampleToolY + (sampleToolH - Theme.GetScaledFontSize(8)) / 2, 8, Theme.TextMuted);

            int[] sampleSizes = { 10, 20, 50, 100 };
            int smBtnW = (int)(42 * scale);
            int smBtnX = col1X + 14 + Theme.MeasureText("ВЫБОРКА:", 8) + 10;
            for (int sIdx = 0; sIdx < sampleSizes.Length; sIdx++)
            {
                int szVal = sampleSizes[sIdx];
                string szLabel = $"{szVal}";
                if (Theme.DrawButton(smBtnX, sampleToolY, smBtnW, sampleToolH, szLabel, _graphSampleSize == szVal, 8, enabled: inputActive))
                {
                    _graphSampleSize = szVal;
                }
                smBtnX += smBtnW + 4;
            }

            // Calculate adaptive heights so balance card and graph always fit without spilling
            int cardH = (int)(85 * scale);
            int graphY = sampleToolY + sampleToolH + 6;
            int py = y + h - cardH - 10;
            int graphH = Math.Max((int)(150 * scale), py - graphY - 8);
            int graphW = col1W - 28;

            DrawTimelineGraph(col1X + 14, graphY, graphW, graphH, _graphMetric, _selectedGraphJumpType, cs, prof, scale, mouse);

            DrawAdComparisonCard(col1X + 14, ref py, col1W - 28, scale, prof);

            // =========================================================================
            // RIGHT COLUMN: FULL-HEIGHT 2D TOP-DOWN TRAJECTORY VISUALIZER & ERROR MAP
            // =========================================================================
            Theme.DrawGlassPanel(col2X, y, col2W, h);
            Draw2DTrajectoryAnalyzer(col2X, y, col2W, h, _selectedGraphJumpType, cs, prof, scale, mouse);
        }

        private static void DrawAdComparisonCard(int cx, ref int cy, int cw, float scale, UserProfile prof)
        {
            int cardH = (int)(95 * scale);
            Raylib.DrawRectangle(cx, cy, cw, cardH, new Color(13, 18, 27, 230));
            Raylib.DrawRectangleLines(cx, cy, cw, cardH, Theme.Border);

            Theme.DrawText("БАЛАНС РУК: ЛЕВЫЕ СТРЕЙФЫ (A) vs ПРАВЫЕ (D)", cx + 12, cy + (int)(6 * scale), 10, Theme.NeonGold);

            int halfW = (cw - 24) / 2;
            int ly = cy + (int)(26 * scale);
            int boxH = cardH - (int)(32 * scale);

            Raylib.DrawRectangle(cx + 8, ly, halfW, boxH, new Color(0, 240, 255, 15));
            Raylib.DrawRectangleLines(cx + 8, ly, halfW, boxH, new Color(0, 240, 255, 60));
            Theme.DrawText("ЛЕВО (KEY_A):", cx + 14, ly + (int)(4 * scale), 10, Theme.NeonCyan);
            Theme.DrawText($"Синхра: {prof.LeftAvgSync:F0}% • Угол: {prof.LeftAvgAngle:F1}°", cx + 14, ly + (int)(22 * scale), 10, Theme.TextWhite);
            Theme.DrawText($"Зажатие A+D: {prof.LeftAvgOverlap:F1} мс", cx + 14, ly + (int)(40 * scale), 9, Theme.TextDim);

            int rx = cx + 8 + halfW + 8;
            Raylib.DrawRectangle(rx, ly, halfW, boxH, new Color(0, 255, 128, 15));
            Raylib.DrawRectangleLines(rx, ly, halfW, boxH, new Color(0, 255, 128, 60));
            Theme.DrawText("ПРАВО (KEY_D):", rx + 14, ly + (int)(4 * scale), 10, Theme.NeonGreen);
            Theme.DrawText($"Синхра: {prof.RightAvgSync:F0}% • Угол: {prof.RightAvgAngle:F1}°", rx + 14, ly + (int)(22 * scale), 10, Theme.TextWhite);
            Theme.DrawText($"Зажатие A+D: {prof.RightAvgOverlap:F1} мс", rx + 14, ly + (int)(40 * scale), 9, Theme.TextDim);
        }

        public static (string Name, string Badge, Color DotColor, Color GlowColor) GetKzTier(string jumpType, float dist)
        {
            string norm = CybershokeKzProfile.NormalizeJumpType(jumpType);

            float wreckerDist, ownageDist, godlikeDist, perfectDist, impressiveDist;

            switch (norm)
            {
                case "Bunnyhop":
                    // Cybershoke CS2 KZ BHOP exact thresholds
                    wreckerDist = 295.0f;
                    ownageDist = 292.0f;
                    godlikeDist = 286.0f;
                    perfectDist = 280.0f;
                    impressiveDist = 275.0f;
                    break;

                case "Multi Bunnyhop":
                    // Cybershoke CS2 KZ MBHOP
                    wreckerDist = 302.0f;
                    ownageDist = 298.0f;
                    godlikeDist = 292.0f;
                    perfectDist = 285.0f;
                    impressiveDist = 280.0f;
                    break;

                case "Weird Jump":
                    // Cybershoke CS2 KZ WJ exact thresholds:
                    wreckerDist = 286.0f;
                    ownageDist = 284.0f;
                    godlikeDist = 280.0f;
                    perfectDist = 275.0f;
                    impressiveDist = 265.0f;
                    break;

                case "Ladder Jump":
                    // Cybershoke CS2 KZ Ladder Jump exact thresholds:
                    wreckerDist = 195.0f;
                    ownageDist = 190.0f;
                    godlikeDist = 180.0f;
                    perfectDist = 170.0f;
                    impressiveDist = 160.0f;
                    break;

                case "Ladderhop":
                    // CS2 Ladderhop thresholds:
                    // Wrecker: 278+, Ownage: 274-277, Godlike: 270-273, Perfect: 265-269, Impressive: 255-264
                    wreckerDist = 278.0f;
                    ownageDist = 274.0f;
                    godlikeDist = 270.0f;
                    perfectDist = 265.0f;
                    impressiveDist = 255.0f;
                    break;

                case "Sideways Jump":
                case "Backwards Jump":
                case "Long Jump":
                case "Jumpbug":
                default:
                    // Cybershoke CS2 KZ LJ / Jumpbug / SW / BW exact distance thresholds:
                    // WRECKER: 284.0+ (Фиолетовый)
                    // OWNAGE: 280.0 - 283.99 (Желтый)
                    // GODLIKE: 275.0 - 279.99 (Красный)
                    // PERFECT: 270.0 - 274.99 (Зеленый)
                    // IMPRESSIVE: 265.0 - 269.99 (Голубой)
                    wreckerDist = 284.0f;
                    ownageDist = 280.0f;
                    godlikeDist = 275.0f;
                    perfectDist = 270.0f;
                    impressiveDist = 265.0f;
                    break;
            }

            if (dist >= wreckerDist)
                return ("WRECKER", "WRECKER", Theme.NeonPurple, new Color(213, 0, 249, 75));
            if (dist >= ownageDist)
                return ("OWNAGE", "OWNAGE", Theme.NeonGold, new Color(255, 215, 0, 75));
            if (dist >= godlikeDist)
                return ("GODLIKE", "GODLIKE", Theme.NeonRed, new Color(255, 23, 68, 75));
            if (dist >= perfectDist)
                return ("PERFECT", "PERFECT", Theme.NeonGreen, new Color(0, 230, 118, 45));
            if (dist >= impressiveDist)
                return ("IMPRESSIVE", "IMPRESSIVE", Theme.NeonCyan, new Color(0, 229, 255, 35));

            return ("NORMAL", "NORMAL", Theme.TextDim, Color.Blank);
        }

        public static (string Name, string Badge, Color DotColor, Color GlowColor) GetBlockTier(string jumpType, float blockDist)
        {
            string norm = CybershokeKzProfile.NormalizeJumpType(jumpType);

            float wreckerBlock, ownageBlock, godlikeBlock, perfectBlock, impressiveBlock;

            switch (norm)
            {
                case "Bunnyhop":
                    // CS2 BHOP Block Tiers
                    wreckerBlock = 286.0f;
                    ownageBlock = 282.0f;
                    godlikeBlock = 276.0f;
                    perfectBlock = 270.0f;
                    impressiveBlock = 264.0f;
                    break;

                case "Multi Bunnyhop":
                    // CS2 MBHOP Block Tiers
                    wreckerBlock = 292.0f;
                    ownageBlock = 288.0f;
                    godlikeBlock = 282.0f;
                    perfectBlock = 275.0f;
                    impressiveBlock = 268.0f;
                    break;

                case "Weird Jump":
                    // CS2 WJ Block Tiers
                    wreckerBlock = 278.0f;
                    ownageBlock = 274.0f;
                    godlikeBlock = 270.0f;
                    perfectBlock = 265.0f;
                    impressiveBlock = 258.0f;
                    break;

                case "Ladder Jump":
                    // CS2 Ladder Jump Block Tiers
                    wreckerBlock = 185.0f;
                    ownageBlock = 180.0f;
                    godlikeBlock = 170.0f;
                    perfectBlock = 160.0f;
                    impressiveBlock = 150.0f;
                    break;

                case "Ladderhop":
                    // CS2 Ladderhop Block Tiers
                    wreckerBlock = 274.0f;
                    ownageBlock = 270.0f;
                    godlikeBlock = 266.0f;
                    perfectBlock = 260.0f;
                    impressiveBlock = 252.0f;
                    break;

                case "Sideways Jump":
                case "Backwards Jump":
                case "Long Jump":
                case "Jumpbug":
                default:
                    // CS2 Block Tiers for LJ / SW / BW / JB:
                    // 275 блок - это Godlike
                    // WRECKER: 280+ (Purple)
                    // OWNAGE: 277 - 279 (Gold)
                    // GODLIKE: 274 - 276 (Red, т.е. 275 блок здесь)
                    // PERFECT: 268 - 273 (Green)
                    // IMPRESSIVE: 262 - 267 (Cyan)
                    wreckerBlock = 280.0f;
                    ownageBlock = 277.0f;
                    godlikeBlock = 274.0f;
                    perfectBlock = 268.0f;
                    impressiveBlock = 262.0f;
                    break;
            }

            if (blockDist >= wreckerBlock)
                return ("WRECKER", "WRECKER", Theme.NeonPurple, new Color(213, 0, 249, 75));
            if (blockDist >= ownageBlock)
                return ("OWNAGE", "OWNAGE", Theme.NeonGold, new Color(255, 215, 0, 75));
            if (blockDist >= godlikeBlock)
                return ("GODLIKE", "GODLIKE", Theme.NeonRed, new Color(255, 23, 68, 75));
            if (blockDist >= perfectBlock)
                return ("PERFECT", "PERFECT", Theme.NeonGreen, new Color(0, 230, 118, 45));
            if (blockDist >= impressiveBlock)
                return ("IMPRESSIVE", "IMPRESSIVE", Theme.NeonCyan, new Color(0, 229, 255, 35));

            return ("NORMAL", "NORMAL", Theme.TextDim, Color.Blank);
        }

        private static List<CS2ConsoleEvent> GetJumpHistoryForAnalytics(string normFilter, CybershokeKzProfile cs, int sampleSize = 30)
        {
            // 1. Check persistent per-type jump history (keeps up to 200 jumps per type)
            var typeJumps = cs.GetJumpsForType(normFilter);
            if (typeJumps.Count > 0)
            {
                return typeJumps.Take(sampleSize).Reverse().ToList();
            }

            // 2. Fallback to RecentJumps buffer
            var filtered = cs.RecentJumps
                .Where(j => CybershokeKzProfile.NormalizeJumpType(j.JumpType) == normFilter && j.Distance > 140f)
                .ToList();

            if (filtered.Count > 0)
            {
                return filtered.Take(sampleSize).Reverse().ToList();
            }

            // Fallback: if user has a PB recorded from Cybershoke/Console, seed with initial point
            var pb = cs.GetOrCreate(normFilter);
            if (pb.PBDist > 0)
            {
                return new List<CS2ConsoleEvent>
                {
                    new()
                    {
                        JumpType = normFilter,
                        Distance = pb.PBDist,
                        Strafes = pb.PBStrafes > 0 ? pb.PBStrafes : 8,
                        Sync = pb.PBSync > 0 ? pb.PBSync : 80f,
                        PreSpeed = pb.PBPreSpeed > 0 ? pb.PBPreSpeed : 275f,
                        MaxSpeed = pb.PBMaxSpeed > 0 ? pb.PBMaxSpeed : 340f,
                        AvgOverlap = pb.AvgOverlap,
                        AvgBadAngles = pb.AvgBadAngles,
                        IsPB = true
                    }
                };
            }

            return new List<CS2ConsoleEvent>();
        }

        private void DrawTimelineGraph(int gx, int gy, int gw, int gh, int metric, string filterType, CybershokeKzProfile cs, UserProfile prof, float scale, Vector2 mouse)
        {
            Raylib.DrawRectangle(gx, gy, gw, gh, new Color(11, 15, 22, 255));
            Raylib.DrawRectangleLines(gx, gy, gw, gh, Theme.Border);

            var normFilter = CybershokeKzProfile.NormalizeJumpType(filterType);
            var jumps = GetJumpHistoryForAnalytics(normFilter, cs, _graphSampleSize);

            var points = new List<float>();
            var labels = new List<string>();

            foreach (var jmp in jumps)
            {
                float val = metric switch
                {
                    0 => jmp.Distance,
                    1 => jmp.Sync,
                    2 => jmp.PreSpeed,
                    3 => jmp.AvgOverlap,
                    _ => jmp.Distance
                };
                points.Add(val);
                var jTier = GetKzTier(normFilter, jmp.Distance);
                labels.Add($"{jTier.Badge} {normFilter} #{jmp.Distance:F2}u ({jmp.Sync:F0}% sync, {jmp.Strafes} str)");
            }

            float minVal = points.Min();
            float maxVal = points.Max();
            float valRange = Math.Max(maxVal - minVal, metric == 0 ? 8f : 10f);
            minVal -= valRange * 0.1f;
            maxVal += valRange * 0.1f;

            int padL = (int)(45 * scale);
            int padR = (int)(15 * scale);
            int padT = (int)(15 * scale);
            int padB = (int)(25 * scale);
            int plotW = gw - padL - padR;
            int plotH = gh - padT - padB;

            for (int line = 0; line <= 4; line++)
            {
                float fract = line / 4f;
                int ly = gy + padT + (int)(plotH * (1f - fract));
                Raylib.DrawLine(gx + padL, ly, gx + gw - padR, ly, new Color(255, 255, 255, 15));

                float lineVal = minVal + (maxVal - minVal) * fract;
                string unitLabel = metric switch { 0 => "u", 1 => "%", 2 => "u/s", 3 => "ms", _ => "" };
                Theme.DrawText($"{lineVal:F0}{unitLabel}", gx + 6, ly - 5, 8, Theme.TextDim);
            }

            Vector2[] screenPoints = new Vector2[points.Count];
            float stepX = plotW / (float)Math.Max(1, points.Count - 1);

            for (int i = 0; i < points.Count; i++)
            {
                float px = gx + padL + i * stepX;
                float normY = (points[i] - minVal) / (maxVal - minVal);
                float py = gy + padT + plotH * (1f - normY);
                screenPoints[i] = new Vector2(px, py);
            }

            // Smooth Catmull-Rom Spline Curve Interpolation
            List<Vector2> splinePoints = new();
            if (screenPoints.Length >= 2)
            {
                int subdivisions = 8; // Smooth 8 intermediate steps between points
                for (int i = 0; i < screenPoints.Length - 1; i++)
                {
                    Vector2 p0 = i > 0 ? screenPoints[i - 1] : screenPoints[i];
                    Vector2 p1 = screenPoints[i];
                    Vector2 p2 = screenPoints[i + 1];
                    Vector2 p3 = i + 2 < screenPoints.Length ? screenPoints[i + 2] : p2;

                    for (int s = 0; s < subdivisions; s++)
                    {
                        float t = s / (float)subdivisions;
                        float t2 = t * t;
                        float t3 = t2 * t;

                        // Catmull-Rom formula: 0.5 * ((2*P1) + (-P0 + P2)*t + (2*P0 - 5*P1 + 4*P2 - P3)*t^2 + (-P0 + 3*P1 - 3*P2 + P3)*t^3)
                        Vector2 sp = 0.5f * (
                            (2f * p1) +
                            (-p0 + p2) * t +
                            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                            (-p0 + 3f * p1 - 3f * p2 + p3) * t3
                        );
                        splinePoints.Add(sp);
                    }
                }
                splinePoints.Add(screenPoints[^1]);
            }
            else if (screenPoints.Length == 1)
            {
                splinePoints.Add(screenPoints[0]);
            }

            // Fill area below smooth curve with soft glowing gradient
            float baseLineY = gy + padT + plotH;
            for (int i = 0; i < splinePoints.Count - 1; i++)
            {
                Vector2 p1 = splinePoints[i];
                Vector2 p2 = splinePoints[i + 1];
                Vector2 b1 = new Vector2(p1.X, baseLineY);
                Vector2 b2 = new Vector2(p2.X, baseLineY);

                Raylib.DrawTriangle(p1, b1, p2, new Color(0, 229, 255, 20));
                Raylib.DrawTriangle(p2, b1, b2, new Color(0, 229, 255, 20));
                
                // Outer neon glow
                Raylib.DrawLineEx(p1, p2, 4.5f, new Color(0, 229, 255, 50));
                // Core sharp line
                Raylib.DrawLineEx(p1, p2, 2.0f, Theme.NeonCyan);
            }

            string? hoverTooltip = null;
            Vector2 tooltipPos = Vector2.Zero;
            float timeAnim = (float)Raylib.GetTime();

            for (int i = 0; i < points.Count; i++)
            {
                Vector2 pt = screenPoints[i];
                bool isPtHover = Vector2.Distance(mouse, pt) < 12f;
                bool isSelected = (_selectedTrajectoryJumpIndex == i);

                float jDist = jumps[i].Distance;
                var tier = GetKzTier(normFilter, jDist);

                if (isPtHover)
                {
                    hoverTooltip = labels[i] + " [Клик: смотреть траекторию]";
                    tooltipPos = pt;

                    if (Raylib.IsMouseButtonPressed(MouseButton.Left))
                    {
                        _selectedTrajectoryJumpIndex = i;
                    }
                }

                // Smooth breathing animation for selected node
                float pulse = isSelected ? (MathF.Sin(timeAnim * 5f) * 2.5f + 4.5f) : (isPtHover ? 3.5f : 0f);
                if (pulse > 0)
                {
                    Raylib.DrawCircle((int)pt.X, (int)pt.Y, 7f + pulse, new Color(0, 229, 255, 60));
                }

                // Draw Tier Glows: Purple (Wrecker), Yellow (Ownage), Red (Godlike), Green (Perfect)
                if (tier.Name == "WRECKER")
                {
                    // Glowing Purple Halo
                    Raylib.DrawCircle((int)pt.X, (int)pt.Y, 9.5f, tier.GlowColor);
                    Raylib.DrawCircle((int)pt.X, (int)pt.Y, 6.5f, new Color(213, 0, 249, 180));
                    Raylib.DrawCircle((int)pt.X, (int)pt.Y, 3.5f, Theme.NeonPurple);
                    Raylib.DrawCircle((int)pt.X, (int)pt.Y, 1.5f, Theme.TextWhite);
                }
                else if (tier.Name == "OWNAGE")
                {
                    // Glowing Yellow/Gold Halo
                    Raylib.DrawCircle((int)pt.X, (int)pt.Y, 9.0f, tier.GlowColor);
                    Raylib.DrawCircle((int)pt.X, (int)pt.Y, 6.0f, new Color(255, 215, 0, 180));
                    Raylib.DrawCircle((int)pt.X, (int)pt.Y, 3.5f, Theme.NeonGold);
                    Raylib.DrawCircle((int)pt.X, (int)pt.Y, 1.5f, Theme.TextWhite);
                }
                else if (tier.Name == "GODLIKE")
                {
                    // Glowing Red Halo
                    Raylib.DrawCircle((int)pt.X, (int)pt.Y, 8.5f, tier.GlowColor);
                    Raylib.DrawCircle((int)pt.X, (int)pt.Y, 5.5f, new Color(255, 23, 68, 180));
                    Raylib.DrawCircle((int)pt.X, (int)pt.Y, 3.5f, Theme.NeonRed);
                    Raylib.DrawCircle((int)pt.X, (int)pt.Y, 1.5f, Theme.TextWhite);
                }
                else if (tier.Name == "PERFECT")
                {
                    // Green Dot
                    Raylib.DrawCircle((int)pt.X, (int)pt.Y, isPtHover ? 5.5f : 3.5f, Theme.NeonGreen);
                    Raylib.DrawCircle((int)pt.X, (int)pt.Y, isPtHover ? 2.5f : 1.5f, Theme.TextWhite);
                }
                else
                {
                    // Impressive / Normal Dot
                    Raylib.DrawCircle((int)pt.X, (int)pt.Y, isPtHover ? 5.5f : 3.5f, isPtHover ? Theme.NeonCyan : Theme.TextDim);
                    Raylib.DrawCircle((int)pt.X, (int)pt.Y, isPtHover ? 2.5f : 1.5f, Theme.TextWhite);
                }

                // Selected Jump Ring Indicator
                if (isSelected)
                {
                    Raylib.DrawCircleLines((int)pt.X, (int)pt.Y, 10.5f, Theme.NeonGold);
                    Raylib.DrawCircleLines((int)pt.X, (int)pt.Y, 9.5f, Theme.TextWhite);
                }
            }

            // Dynamic Legend at bottom of graph matching current jump type
            string legendStr = normFilter switch
            {
                "Bunnyhop" => "[Wrecker: 295+]  [Ownage: 292-294.9]  [Godlike: 286-291.9]  [Perfect: 280+]",
                "Multi Bunnyhop" => "[Wrecker: 302+]  [Ownage: 298-301.9]  [Godlike: 292-297.9]  [Perfect: 285+]",
                "Ladder Jump" => "[Wrecker: 195+]  [Ownage: 190-194.9]  [Godlike: 180-189.9]  [Perfect: 170+]",
                "Ladderhop" => "[Wrecker: 210+]  [Ownage: 200-209.9]  [Godlike: 190-199.9]  [Perfect: 180+]",
                "Weird Jump" => "[Wrecker: 286+]  [Ownage: 284-285.9]  [Godlike: 280-283.9]  [Perfect: 275+]",
                _ => "[Wrecker: 284+]  [Ownage: 280-283.9]  [Godlike: 275-279.9]  [Perfect: 270+]"
            };
            Theme.DrawText(legendStr, gx + padL, gy + gh - 14, 8, Theme.TextDim);

            if (hoverTooltip != null)
            {
                int tipW = (int)(250 * scale);
                int tipH = (int)(24 * scale);
                int tx = (int)tooltipPos.X - tipW / 2;
                int ty = (int)tooltipPos.Y - tipH - 8;
                tx = Math.Clamp(tx, gx + 4, gx + gw - tipW - 4);

                Raylib.DrawRectangle(tx, ty, tipW, tipH, new Color(5, 10, 18, 245));
                Raylib.DrawRectangleLines(tx, ty, tipW, tipH, Theme.NeonGold);
                Theme.DrawText(hoverTooltip, tx + 6, ty + 6, 8, Theme.NeonGold);
            }
        }

        private class TrajPoint
        {
            public Vector2 Pos;
            public int StrafeIndex;
            public float Speed;
            public float Sync;
            public bool IsOverlap;
            public bool IsBadAngle;
            public bool IsClean;
            public string Note = "";
        }

        private void Draw2DTrajectoryAnalyzer(int tx, int ty, int tw, int th, string filterType, CybershokeKzProfile cs, UserProfile prof, float scale, Vector2 mouse, bool inputActive = true)
        {
            Raylib.DrawRectangle(tx, ty, tw, th, Theme.BgDark);
            Raylib.DrawRectangleLines(tx, ty, tw, th, Theme.Border);

            var normFilter = CybershokeKzProfile.NormalizeJumpType(filterType);
            var jumps = GetJumpHistoryForAnalytics(normFilter, cs);

            // 1. Header with Jump Selection Tabs
            int headerH = (int)(32 * scale);
            Raylib.DrawRectangle(tx, ty, tw, headerH, Theme.BgPanelHeader);
            Raylib.DrawLine(tx, ty + headerH, tx + tw, ty + headerH, Theme.Border);

            Theme.DrawText("2D ТРАЕКТОРИЯ ПОЛЁТА", tx + 12, ty + (headerH - Theme.GetScaledFontSize(10)) / 2, 10, Theme.NeonGold);

            // 1. Selector buttons (ПОСЛЕДНИЙ ПРЫЖОК, РЕКОРД (PB), СРЕДНИЙ ТРЕК, ЛОГ)
            int selBtnH = (int)(22 * scale);
            int lastIdx = jumps.Count - 1;

            // Find absolute personal best jump across ALL recorded jumps for this type
            var allTypeJumps = cs.RecentJumps
                .Where(j => CybershokeKzProfile.NormalizeJumpType(j.JumpType) == normFilter && j.Distance > 140f)
                .ToList();

            CS2ConsoleEvent? absolutePbJump = null;
            float maxFoundDist = -1;
            foreach (var j in allTypeJumps)
            {
                if (j.Distance > maxFoundDist)
                {
                    maxFoundDist = j.Distance;
                    absolutePbJump = j;
                }
            }

            var pbMeta = cs.GetOrCreate(normFilter);
            if (pbMeta.PBDist > maxFoundDist)
            {
                maxFoundDist = pbMeta.PBDist;
                absolutePbJump = new CS2ConsoleEvent
                {
                    JumpType = normFilter,
                    Distance = pbMeta.PBDist,
                    Strafes = pbMeta.PBStrafes > 0 ? pbMeta.PBStrafes : 8,
                    Sync = pbMeta.PBSync > 0 ? pbMeta.PBSync : 80f,
                    PreSpeed = pbMeta.PBPreSpeed > 0 ? pbMeta.PBPreSpeed : 275f,
                    MaxSpeed = pbMeta.PBMaxSpeed > 0 ? pbMeta.PBMaxSpeed : 340f,
                    AvgOverlap = pbMeta.AvgOverlap,
                    AvgBadAngles = pbMeta.AvgBadAngles,
                    IsPB = true
                };
            }

            // If absolute PB is not in our recent sample window, ensure we can show it
            int pbJumpIdx = -1;
            if (absolutePbJump != null)
            {
                for (int k = 0; k < jumps.Count; k++)
                {
                    if (MathF.Abs(jumps[k].Distance - absolutePbJump.Distance) < 0.01f)
                    {
                        pbJumpIdx = k;
                        break;
                    }
                }
            }

            int rightReserved = (int)(12 * scale);
            int curBtnRight = tx + tw - rightReserved;

            // Adaptive button width based on available tw
            int btnCount = 4;
            int maxBtnBarW = tw - (int)(180 * scale);
            int dynamicBtnW = Math.Clamp((maxBtnBarW - (btnCount - 1) * 4) / btnCount, 60, (int)(115 * scale));

            // [ЛОГ ПРЫЖКА]
            curBtnRight -= dynamicBtnW;
            if (Theme.DrawButton(curBtnRight, ty + (headerH - selBtnH) / 2, dynamicBtnW, selBtnH, "ЛОГ ПРЫЖКА", false, 8, enabled: inputActive && jumps.Count > 0))
            {
                int curIdx = _selectedTrajectoryJumpIndex;
                if (curIdx == -2 || curIdx < 0 || curIdx >= jumps.Count) curIdx = lastIdx;
                if (curIdx >= 0 && curIdx < jumps.Count)
                {
                    var j = jumps[curIdx];
                    _showLogModal = true;
                    _selectedLogTitle = $"{j.JumpType.ToUpper()} — {j.Distance:F2}u";
                    _selectedLogContent = !string.IsNullOrEmpty(j.RawLine)
                        ? j.RawLine
                        : $"[CS2 Console Watcher] {j.Distance:F4} units | {j.Strafes} str | {j.Sync:F1}% sync | {j.PreSpeed:F1} pre | {j.MaxSpeed:F1} max\nКарта: {j.MapName}\nОтклонение: {j.Deviation:F2} | Airpath: {j.Airpath:F3}\nOverlap: {j.AvgOverlap:F1}ms | Bad Angles: {j.AvgBadAngles:F1}%";
                    _selectedLogBreakdown = j.StrafeBreakdown;
                }
            }
            curBtnRight -= 4;

            // [СРЕДНИЙ ТРЕК]
            curBtnRight -= dynamicBtnW;
            if (Theme.DrawButton(curBtnRight, ty + (headerH - selBtnH) / 2, dynamicBtnW, selBtnH, "СРЕДНИЙ", _selectedTrajectoryJumpIndex == -1, 8, enabled: inputActive))
            {
                _selectedTrajectoryJumpIndex = -1;
            }
            curBtnRight -= 4;

            // [РЕКОРД (PB)]
            if (absolutePbJump != null)
            {
                curBtnRight -= dynamicBtnW;
                string pbLbl = $"PB {absolutePbJump.Distance:F1}u";
                bool isPbActive = (_selectedTrajectoryJumpIndex == -3 || (_selectedTrajectoryJumpIndex >= 0 && _selectedTrajectoryJumpIndex == pbJumpIdx));
                if (Theme.DrawButton(curBtnRight, ty + (headerH - selBtnH) / 2, dynamicBtnW, selBtnH, pbLbl, isPbActive, 8, enabled: inputActive))
                {
                    _selectedTrajectoryJumpIndex = (pbJumpIdx >= 0) ? pbJumpIdx : -3; // -3 = Dedicated PB fallback
                }
                curBtnRight -= 4;
            }

            // [ПОСЛЕДНИЙ ПРЫЖОК]
            if (lastIdx >= 0)
            {
                curBtnRight -= dynamicBtnW;
                string freshLbl = $"СВЕЖИЙ {jumps[lastIdx].Distance:F1}u";
                bool isFreshActive = (_selectedTrajectoryJumpIndex == -2 || _selectedTrajectoryJumpIndex == lastIdx);
                if (Theme.DrawButton(curBtnRight, ty + (headerH - selBtnH) / 2, dynamicBtnW, selBtnH, freshLbl, isFreshActive, 8, enabled: inputActive))
                {
                    _selectedTrajectoryJumpIndex = -2; // Auto-follow fresh jump
                }
            }

            // 2. Active Jump Telemetry Resolution (Default -2 = Always show freshest latest jump)
            CS2ConsoleEvent activeJmp;
            int activeIdx = _selectedTrajectoryJumpIndex;
            if (activeIdx == -3 && absolutePbJump != null)
            {
                activeJmp = absolutePbJump;
            }
            else
            {
                if (activeIdx == -2 || activeIdx < 0 || activeIdx >= jumps.Count)
                {
                    activeIdx = lastIdx;
                }
                activeJmp = (lastIdx >= 0) ? jumps[activeIdx] : (absolutePbJump ?? new CS2ConsoleEvent { Distance = 275f, Strafes = 8 });
            }

            float jumpDist = activeJmp.Distance;
            int jumpStrafes = activeJmp.Strafes > 0 ? activeJmp.Strafes : 8;
            float jumpPre = activeJmp.PreSpeed > 0 ? activeJmp.PreSpeed : 274.6f;
            float jumpMax = activeJmp.MaxSpeed > 0 ? activeJmp.MaxSpeed : 337.7f;
            float jumpSync = activeJmp.Sync > 0 ? activeJmp.Sync : 77.0f;
            float jumpOverlap = activeJmp.AvgOverlap > 0 ? activeJmp.AvgOverlap : 19.4f;
            float jumpBad = activeJmp.AvgBadAngles > 0 ? activeJmp.AvgBadAngles : 12.5f;
            float jumpDev = activeJmp.Deviation;
            float jumpWidth = activeJmp.AvgWidth;

            // 3. Realistic Physical Trajectory from Authentic CS2 Console Breakdown
            float leftRightBias = (prof.RightAvgSync - prof.LeftAvgSync); // e.g. +11% right bias
            var pathPoints = GenerateRealisticFlightPath(activeJmp, leftRightBias, activeIdx, prof);

            // Compute actual final landing lateral drift from real path
            float finalLateralDrift = pathPoints.Count > 0 ? pathPoints[^1].Pos.X : 0f;

            // 4. Layout: FULL-WIDTH 2D FLIGHT CANVAS WITH PROPORTIONAL SCALING
            int bodyY = ty + headerH + 6;
            int bodyH = th - headerH - 12;
            int cvX = tx + 8;
            int cvY = bodyY;
            int cvW = tw - 16;
            int cvH = bodyH;

            // Canvas Background
            Raylib.DrawRectangle(cvX, cvY, cvW, cvH, new Color(7, 11, 16, 255));
            Raylib.DrawRectangleLines(cvX, cvY, cvW, cvH, new Color(Theme.Border.R, Theme.Border.G, Theme.Border.B, (byte)75));

            int originX = cvX + (int)(cvW * 0.38f);
            int originY = cvY + cvH - 26;
            int targetY = cvY + 30;

            // Dynamic scale: tightly fit the jump height with comfortable margins
            float maxViewDist = MathF.Max(jumpDist + 8f, 250f);
            float pxPerUnitY = (originY - targetY) / maxViewDist;
            
            // Strictly clamp pxPerUnitX so lateral strafes never escape canvas boundaries
            float maxCanvasHalfW = (cvW * 0.30f);
            float pxPerUnitX = Math.Clamp(pxPerUnitY * 1.5f, 0.7f, maxCanvasHalfW / 35.0f);

            // Proportional Distance Grid lines every 25u (50, 75, 100, 125, 150, 175, 200, 225, 250, 275...)
            for (float d = 25; d <= maxViewDist; d += 25)
            {
                int gy = originY - (int)(d * pxPerUnitY);
                if (gy < cvY + 12 || gy > originY) continue;

                bool isMajor = ((int)d % 50 == 0);
                Raylib.DrawLine(cvX + 12, gy, cvX + cvW - 12, gy, isMajor ? new Color(255, 255, 255, 18) : new Color(255, 255, 255, 8));
                Theme.DrawText($"{d:F0}u", cvX + 14, gy - 8, 8, isMajor ? Theme.TextWhite : Theme.TextDim);
            }

            // Ideal Centerline (X = 0)
            for (int cy = originY; cy >= targetY; cy -= 8)
            {
                Raylib.DrawLine(originX, cy, originX, Math.Max(targetY, cy - 4), new Color(0, 240, 255, 55));
            }
            Theme.DrawText("X=0", originX + 6, originY - 14, 8, new Color(0, 240, 255, 90));

            // Starting Platform (Takeoff Block)
            int startBlockW = (int)(60 * scale);
            Raylib.DrawRectangle(originX - startBlockW / 2, originY - 4, startBlockW, 8, new Color(0, 255, 128, 40));
            Raylib.DrawRectangleLines(originX - startBlockW / 2, originY - 4, startBlockW, 8, Theme.NeonGreen);
            Theme.DrawText("СТАРТ", originX - 16, originY + 6, 8, Theme.NeonGreen);

            // Landing Platform (Target Block)
            int landY = originY - (int)(jumpDist * pxPerUnitY);
            int landBlockW = (int)(70 * scale);
            Raylib.DrawRectangle(originX - landBlockW / 2, landY - 4, landBlockW, 8, new Color(255, 215, 0, 40));
            Raylib.DrawRectangleLines(originX - landBlockW / 2, landY - 4, landBlockW, 8, Theme.NeonGold);
            Theme.DrawText($"ФИНИШ ({jumpDist:F1}u)", originX - 28, landY - 16, 8, Theme.NeonGold);

            // SCISSOR CLIPPING: Guarantee trajectory geometry never leaves the 2D canvas rectangle
            Raylib.BeginScissorMode(cvX + 1, cvY + 1, cvW - 2, cvH - 2);

            // A. Draw Average Trajectory Ghost (Dotted Golden Curve)
            var ghostJump = new CS2ConsoleEvent
            {
                Distance = jumpDist,
                Strafes = 8,
                PreSpeed = 274.6f,
                MaxSpeed = 335.0f,
                Sync = 76.0f,
                Deviation = 0.5f,
                AvgWidth = 34.0f
            };
            var avgPath = GenerateRealisticFlightPath(ghostJump, leftRightBias * 0.4f, -1, prof);
            for (int i = 0; i < avgPath.Count - 1; i++)
            {
                var p1 = avgPath[i].Pos;
                var p2 = avgPath[i + 1].Pos;
                int s1X = originX + (int)(p1.X * pxPerUnitX);
                int s1Y = originY - (int)(p1.Y * pxPerUnitY);
                int s2X = originX + (int)(p2.X * pxPerUnitX);
                int s2Y = originY - (int)(p2.Y * pxPerUnitY);

                if (i % 2 == 0)
                {
                    Raylib.DrawLine(s1X, s1Y, s2X, s2Y, new Color(255, 215, 0, 95));
                }
            }

            // B. Draw Active Jump Trajectory with KZ Laser Beam Trail and Highlights
            TrajPoint? hoverPoint = null;
            Vector2 hoverScreenPos = Vector2.Zero;

            if (pathPoints.Count > 1)
            {
                // Interpolate smoothly through strafe points with Catmull-Rom spline
                var smoothPath = new List<(Vector2 Pos, TrajPoint Pt)>();
                for (int i = 0; i < pathPoints.Count - 1; i++)
                {
                    var pt0 = (i > 0) ? pathPoints[i - 1] : pathPoints[i];
                    var pt1 = pathPoints[i];
                    var pt2 = pathPoints[i + 1];
                    var pt3 = (i + 2 < pathPoints.Count) ? pathPoints[i + 2] : pt2;

                    Vector2 p0 = new(originX + pt0.Pos.X * pxPerUnitX, originY - pt0.Pos.Y * pxPerUnitY);
                    Vector2 p1 = new(originX + pt1.Pos.X * pxPerUnitX, originY - pt1.Pos.Y * pxPerUnitY);
                    Vector2 p2 = new(originX + pt2.Pos.X * pxPerUnitX, originY - pt2.Pos.Y * pxPerUnitY);
                    Vector2 p3 = new(originX + pt3.Pos.X * pxPerUnitX, originY - pt3.Pos.Y * pxPerUnitY);

                    int steps = 10;
                    for (int st = 0; st < steps; st++)
                    {
                        float t = st / (float)steps;
                        float t2 = t * t;
                        float t3 = t2 * t;

                        Vector2 sp = 0.5f * (
                            (2f * p1) +
                            (-p0 + p2) * t +
                            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                            (-p0 + 3f * p1 - 3f * p2 + p3) * t3
                        );
                        smoothPath.Add((sp, pt1));
                    }
                }
                var lastPt = pathPoints[^1];
                smoothPath.Add((new Vector2(originX + lastPt.Pos.X * pxPerUnitX, originY - lastPt.Pos.Y * pxPerUnitY), lastPt));

                // Render smooth glowing laser beam segments
                float trajAnim = (float)Raylib.GetTime();
                for (int i = 0; i < smoothPath.Count - 1; i++)
                {
                    var (sp1, pt1) = smoothPath[i];
                    var (sp2, _) = smoothPath[i + 1];

                    Color segCol = Theme.StrafeColors[pt1.StrafeIndex % Theme.StrafeColors.Length];

                    // Pulse animation along the curve
                    float pulseGlow = (MathF.Sin(trajAnim * 4f - i * 0.08f) * 0.5f + 0.5f);

                    if (pt1.IsOverlap)
                    {
                        segCol = Theme.NeonRed;
                        Raylib.DrawLineEx(sp1, sp2, 5.8f + pulseGlow * 1.5f, new Color((byte)255, (byte)40, (byte)40, (byte)(70 + pulseGlow * 40)));
                        Raylib.DrawLineEx(sp1, sp2, 3.2f, Theme.NeonRed);
                        Raylib.DrawLineEx(sp1, sp2, 1.2f, new Color(255, 255, 255, 180));
                    }
                    else if (pt1.IsBadAngle)
                    {
                        segCol = Theme.NeonOrange;
                        Raylib.DrawLineEx(sp1, sp2, 5.2f + pulseGlow * 1.2f, new Color((byte)255, (byte)140, (byte)0, (byte)(65 + pulseGlow * 35)));
                        Raylib.DrawLineEx(sp1, sp2, 3.0f, Theme.NeonOrange);
                        Raylib.DrawLineEx(sp1, sp2, 1.0f, new Color(255, 255, 255, 160));
                    }
                    else
                    {
                        Raylib.DrawLineEx(sp1, sp2, 5.0f + pulseGlow * 1.5f, new Color((byte)segCol.R, (byte)segCol.G, (byte)segCol.B, (byte)(60 + pulseGlow * 45)));
                        Raylib.DrawLineEx(sp1, sp2, 2.6f, segCol);
                        Raylib.DrawLineEx(sp1, sp2, 1.0f, new Color(255, 255, 255, 140));
                    }

                    // Check hover
                    if (Vector2.Distance(mouse, sp1) < 8.0f)
                    {
                        hoverPoint = pt1;
                        hoverScreenPos = sp1;
                    }
                }

                // Waypoint markers at strafe start (Large, crisp, glowing badges)
                for (int i = 1; i < pathPoints.Count; i++)
                {
                    var pt1 = pathPoints[i];
                    if (pt1.StrafeIndex != pathPoints[i - 1].StrafeIndex)
                    {
                        int s1X = originX + (int)(pt1.Pos.X * pxPerUnitX);
                        int s1Y = originY - (int)(pt1.Pos.Y * pxPerUnitY);
                        Color segCol = Theme.StrafeColors[pt1.StrafeIndex % Theme.StrafeColors.Length];

                        // 1. Large glowing point on trajectory
                        Raylib.DrawCircle(s1X, s1Y, 7.5f, new Color((byte)segCol.R, (byte)segCol.G, (byte)segCol.B, (byte)60));
                        Raylib.DrawCircle(s1X, s1Y, 5.0f, new Color((byte)segCol.R, (byte)segCol.G, (byte)segCol.B, (byte)180));
                        Raylib.DrawCircle(s1X, s1Y, 3.5f, segCol);
                        Raylib.DrawCircle(s1X, s1Y, 1.5f, Theme.TextWhite);

                        // 2. Clear floating Badge Pill for Strafe Number
                        string sLabel = $"S{pt1.StrafeIndex + 1}";
                        int labelW = Theme.MeasureText(sLabel, 9) + 8;
                        int labelH = (int)(16 * scale);

                        bool isLeft = (pt1.StrafeIndex % 2 == 0);
                        int labelX = isLeft ? (s1X - labelW - 6) : (s1X + 6);
                        int labelY = s1Y - labelH / 2;

                        // Badge background + border + glow
                        Raylib.DrawRectangle(labelX, labelY, labelW, labelH, new Color(12, 17, 26, 230));
                        Raylib.DrawRectangleLines(labelX, labelY, labelW, labelH, segCol);
                        Raylib.DrawRectangle(labelX, labelY, 3, labelH, segCol);

                        Theme.DrawText(sLabel, labelX + 5, labelY + 2, 9, Theme.TextWhite);
                    }
                }

                // Landing point & Deviation vector
                var landPt = pathPoints[^1].Pos;
                int finalLandX = originX + (int)(landPt.X * pxPerUnitX);
                int finalLandY = originY - (int)(landPt.Y * pxPerUnitY);

                // Landing Marker Halo
                Raylib.DrawCircle(finalLandX, finalLandY, 7f, new Color(255, 215, 0, 80));
                Raylib.DrawCircle(finalLandX, finalLandY, 4.5f, Theme.NeonGold);
                Raylib.DrawCircle(finalLandX, finalLandY, 2.0f, Theme.TextWhite);

                // Horizontal deviation vector from centerline to landing point
                if (MathF.Abs(finalLateralDrift) > 0.5f)
                {
                    Color devCol = MathF.Abs(finalLateralDrift) <= 6.0f ? Theme.NeonGreen : Theme.NeonOrange;
                    Raylib.DrawLineEx(new Vector2(originX, finalLandY), new Vector2(finalLandX, finalLandY), 1.5f, devCol);
                    string driftStr = finalLateralDrift >= 0 ? $"+{finalLateralDrift:F1}u" : $"-{MathF.Abs(finalLateralDrift):F1}u";
                    int dX = (originX + finalLandX) / 2 - 12;
                    Theme.DrawText(driftStr, dX, finalLandY + 5, 8, devCol);
                }
            }

            Raylib.EndScissorMode();

            DrawTrajectoryHudOverlays(cvX, cvY, cvW, cvH, scale, normFilter, activeJmp, jumpDist, jumpStrafes, jumpPre, jumpMax, jumpSync, jumpOverlap, jumpBad, finalLateralDrift, jumpWidth, prof);
        }

        private static void DrawTrajectoryHudOverlays(
            int cvX, int cvY, int cvW, int cvH, float scale, string normFilter, 
            CS2ConsoleEvent activeJmp, float jumpDist, int jumpStrafes, float jumpPre, float jumpMax, float jumpSync, 
            float jumpOverlap, float jumpBad, float finalLateralDrift, float jumpWidth, UserProfile prof)
        {
            string driftDir = finalLateralDrift > 0.5f ? "D" : (finalLateralDrift < -0.5f ? "A" : "Center");
            Color driftAccent = MathF.Abs(finalLateralDrift) <= 6.0f ? Theme.NeonGreen : Theme.NeonOrange;

            // 1. Direct Crisp Telemetry Text in Top-Right Corner (PINNED STRICTLY TO PANEL RIGHT EDGE)
            string blockSuffix = "";
            Color line1Col = Theme.NeonCyan;
            if (activeJmp.BlockDistance > 0)
            {
                var (bTier, _, bCol, _) = GetBlockTier(normFilter, activeJmp.BlockDistance);
                blockSuffix = $" • БЛОК: {activeJmp.BlockDistance:F0} [{bTier}]";
                line1Col = bCol;
            }
            else
            {
                var (dTier, _, dCol, _) = GetKzTier(normFilter, jumpDist);
                line1Col = dCol;
            }

            string line1 = $"{normFilter.ToUpper()}: {jumpDist:F2}u ({jumpStrafes} str){blockSuffix}";
            string line2 = $"Pre: {jumpPre:F0} u/s • Max: {jumpMax:F0} • Sync: {jumpSync:F0}%";
            string widthText = jumpWidth > 15f ? $" • Угол: {jumpWidth:F0}°" : "";
            string line3 = $"Снос dX: {MathF.Abs(finalLateralDrift):F1}u ({driftDir}){widthText}";

            int padRight = 16;
            int infoY = cvY + 10;

            Theme.DrawText(line1, cvX + cvW - Theme.MeasureText(line1, 9) - padRight, infoY, 9, line1Col);
            Theme.DrawText(line2, cvX + cvW - Theme.MeasureText(line2, 8) - padRight, infoY + 16, 8, Theme.TextWhite);
            Theme.DrawText(line3, cvX + cvW - Theme.MeasureText(line3, 8) - padRight, infoY + 32, 8, driftAccent);

            // 2. Bottom-Right Per-Strafe Status (PURE TEXT HUD PINNED TO THE RIGHT)
            int hud3W = (int)(155 * scale);
            int hud3H = (int)(100 * scale);
            int hud3X = cvX + cvW - hud3W - 6;
            int hud3Y = cvY + cvH - hud3H - 6;

            // Header shifted to right
            string titleH = "ПОСТРЕЙФОВЫЙ СТАТУС:";
            int titleW = Theme.MeasureText(titleH, 8);
            Theme.DrawText(titleH, hud3X + hud3W - titleW - 4, hud3Y, 8, Theme.NeonGold);

            int miniRowH = (int)(10 * scale);
            int countToDraw = Math.Min(8, jumpStrafes);
            for (int s = 0; s < countToDraw; s++)
            {
                int sRowY = hud3Y + (int)(13 * scale) + s * miniRowH;
                if (sRowY + miniRowH > hud3Y + hud3H) break;

                bool isLeft = (s % 2 == 0);
                string sideKey = isLeft ? "A" : "D";
                float sSync = isLeft ? Math.Clamp(prof.LeftAvgSync - (100f - jumpSync) * 0.3f, 45f, 95f) : Math.Clamp(prof.RightAvgSync - (100f - jumpSync) * 0.3f, 50f, 98f);
                if (s == 0) sSync = Math.Min(95f, sSync + 8f);

                string errTag = (s == 2 && jumpOverlap > 15f) ? "[OVERLAP]" :
                                ((s == 4 && jumpBad > 10f) ? "[BAD ANG]" :
                                ((s >= 6) ? "[RUSH]" : "[OK]"));
                Color errCol = errTag.Contains("OK") ? Theme.NeonGreen : (errTag.Contains("OVERLAP") ? Theme.NeonRed : Theme.NeonOrange);

                Color strCol = Theme.StrafeColors[s % Theme.StrafeColors.Length];

                // Right-aligned column layout:
                // [OK / RUSH / OVERLAP] pinned to the right edge (X = hud3X + hud3W - errW - 2)
                int errW = Theme.MeasureText(errTag, 8);
                int errX = hud3X + hud3W - errW - 2;

                // Sync % before error tag
                string syncText = $"{sSync:F0}%";
                int syncW = Theme.MeasureText(syncText, 8);
                int syncX = errX - syncW - 6;

                // Strafe label before sync %
                string strafeText = $"S{s + 1}({sideKey})";
                int strW = Theme.MeasureText(strafeText, 8);
                int strX = syncX - strW - 6;

                // Circle dot before strafe label
                int dotX = strX - 6;

                Raylib.DrawCircle(dotX, sRowY + 4, 2.5f, strCol);
                Theme.DrawText(strafeText, strX, sRowY, 8, Theme.TextWhite);
                Theme.DrawText(syncText, syncX, sRowY, 8, Theme.NeonCyan);
                Theme.DrawText(errTag, errX, sRowY, 8, errCol);
            }

            // 3. Bottom-Left Clean Legend (PINNED TO BOTTOM LEFT)
            int legY = cvY + cvH - 22;
            Raylib.DrawLine(cvX + 12, legY + 4, cvX + 26, legY + 4, Theme.NeonCyan);
            Theme.DrawText("Траектория S1..S8", cvX + 30, legY, 8, Theme.NeonCyan);

            Raylib.DrawCircle(cvX + 140, legY + 4, 3f, Theme.NeonRed);
            Theme.DrawText("Ошибки (Overlap / BadAngle)", cvX + 148, legY, 8, Theme.NeonRed);

            for (int dx = 0; dx < 14; dx += 4)
            {
                Raylib.DrawLine(cvX + 12 + dx, legY + 14, cvX + 14 + dx, legY + 14, Theme.NeonGold);
            }
            Theme.DrawText("Средняя траектория (Ghost)", cvX + 30, legY + 10, 8, Theme.NeonGold);
        }

        private static List<TrajPoint> GenerateRealisticFlightPath(
            CS2ConsoleEvent jmp, float rightBias, int jumpSeed, UserProfile prof)
        {
            var points = new List<TrajPoint>();
            float distance = Math.Max(jmp.Distance, 50f);
            int strafes = Math.Clamp(jmp.Strafes, 1, 16);
            float preSpeed = jmp.PreSpeed > 0 ? jmp.PreSpeed : 274.6f;
            float maxSpeed = jmp.MaxSpeed > 0 ? jmp.MaxSpeed : 337.0f;
            float sync = jmp.Sync > 0 ? jmp.Sync : 75.0f;
            float deviation = jmp.Deviation;
            float avgWidth = jmp.AvgWidth > 10f ? jmp.AvgWidth : 35f;

            // Determine starting key direction from sequence or default Right (D)
            bool startRight = true;
            if (!string.IsNullOrEmpty(jmp.LeftKeySequence) && !string.IsNullOrEmpty(jmp.RightKeySequence))
            {
                int firstL = jmp.LeftKeySequence.IndexOf('L');
                int firstR = jmp.RightKeySequence.IndexOf('R');
                if (firstL >= 0 && (firstR < 0 || firstL < firstR))
                {
                    startRight = false; // Player started with Key A (Left)
                }
            }

            // Extract per-strafe breakdown or fallback to standard airtime distribution
            bool hasBreakdown = jmp.StrafeBreakdown != null && jmp.StrafeBreakdown.Count >= strafes;
            float[] strafeAirtimePcts = new float[strafes];
            float[] strafeWidths = new float[strafes];
            float[] strafeSyncs = new float[strafes];
            float[] strafeBadAngles = new float[strafes];
            float[] strafeOverlaps = new float[strafes];
            float[] strafeDeadAirs = new float[strafes];

            if (hasBreakdown)
            {
                float totalPct = 0f;
                for (int s = 0; s < strafes; s++)
                {
                    var d = jmp.StrafeBreakdown![s];
                    strafeAirtimePcts[s] = d.AirtimePct > 0 ? d.AirtimePct : (100f / strafes);
                    totalPct += strafeAirtimePcts[s];
                    strafeWidths[s] = d.WidthDeg > 0 ? d.WidthDeg : avgWidth;
                    strafeSyncs[s] = d.Sync;
                    strafeBadAngles[s] = d.BadAngles;
                    strafeOverlaps[s] = d.Overlap;
                    strafeDeadAirs[s] = d.DeadAir;
                }
                if (totalPct > 10f)
                {
                    for (int s = 0; s < strafes; s++) strafeAirtimePcts[s] /= totalPct;
                }
            }
            else
            {
                float totalW = 0f;
                for (int s = 0; s < strafes; s++)
                {
                    float ratio = strafes > 1 ? (float)s / (strafes - 1) : 0f;
                    float w = 1.35f - 0.65f * ratio;
                    strafeAirtimePcts[s] = w;
                    totalW += w;
                    strafeWidths[s] = avgWidth * (1.15f - 0.30f * ratio);
                    strafeSyncs[s] = sync;
                    strafeBadAngles[s] = jmp.AvgBadAngles;
                    strafeOverlaps[s] = jmp.AvgOverlap;
                    strafeDeadAirs[s] = jmp.AvgDeadAir;
                }
                for (int s = 0; s < strafes; s++) strafeAirtimePcts[s] /= totalW;
            }

            // Cumulative start and end distance along Y for each strafe
            float[] strafeStartY = new float[strafes];
            float[] strafeEndY = new float[strafes];
            float accumY = 0f;
            for (int s = 0; s < strafes; s++)
            {
                strafeStartY[s] = accumY;
                float sLen = distance * strafeAirtimePcts[s];
                accumY += sLen;
                strafeEndY[s] = (s == strafes - 1) ? distance : accumY;
            }

            // Real physical landing drift (Deviation from CKZ is usually between -20u and +20u)
            float targetDrift = Math.Clamp(deviation, -25f, 25f);
            if (MathF.Abs(deviation) < 0.05f && MathF.Abs(rightBias) > 4.0f)
            {
                targetDrift = (rightBias > 0 ? 1f : -1f) * 3.5f;
            }

            float currentSpeed = preSpeed;
            points.Add(new TrajPoint
            {
                Pos = new Vector2(0, 0),
                StrafeIndex = 0,
                Speed = currentSpeed,
                Sync = sync,
                IsClean = true,
                Note = $"Старт прыжка (Pre: {preSpeed:F0} u/s)"
            });

            int ticksPerStrafe = 12; // High-resolution smooth curve ticks

            for (int s = 0; s < strafes; s++)
            {
                bool isRight = (s % 2 == 0) ? startRight : !startRight;
                float dir = isRight ? 1f : -1f;

                float sStartY = strafeStartY[s];
                float sLenY = strafeEndY[s] - sStartY;
                float sAngle = strafeWidths[s];
                float sSync = strafeSyncs[s];
                float sBad = strafeBadAngles[s];
                float sOver = strafeOverlaps[s];
                float sDead = strafeDeadAirs[s];

                // Hand asymmetry bias
                if (!isRight && rightBias > 0)
                {
                    sAngle *= MathF.Max(0.72f, 1.0f - (rightBias * 0.012f));
                }

                // Realistic physical lateral wave amplitude in CS2 (typical strafe width is 8u to 26u)
                float clampedAngle = Math.Clamp(sAngle, 15f, 90f);
                float rad = clampedAngle * (MathF.PI / 180f);
                float arcAmplitude = Math.Clamp(MathF.Sin(rad * 0.5f) * (sLenY * 0.38f), 6.0f, 24.0f);

                if (sBad > 20.0f) arcAmplitude *= (1.0f + (sBad - 20f) * 0.008f);

                for (int k = 1; k <= ticksPerStrafe; k++)
                {
                    float u = (float)k / ticksPerStrafe;
                    float curY = sStartY + sLenY * u;
                    float totalProgress = Math.Clamp(curY / distance, 0f, 1f);

                    float strafeWaveX = MathF.Sin(u * MathF.PI) * arcAmplitude * dir;

                    if (sOver > 15.0f && u < 0.30f)
                    {
                        strafeWaveX *= 0.35f;
                        currentSpeed = MathF.Max(255f, currentSpeed - 0.5f);
                    }
                    else
                    {
                        currentSpeed = MathF.Min(maxSpeed, currentSpeed + (0.95f * (sSync / 100.0f)));
                    }

                    float driftX = targetDrift * totalProgress;
                    float curX = strafeWaveX + driftX;

                    bool isOverlapTick = (sOver > 15.0f && u < 0.35f);
                    bool isBadAngleTick = (sBad > 25.0f && u > 0.35f && u < 0.75f);

                    string sideKey = isRight ? "Key D" : "Key A";
                    string note = isOverlapTick ? $"S{s + 1} ({sideKey}) Overlap ({sOver:F0}%): залипание A+D" :
                                  (isBadAngleTick ? $"S{s + 1} ({sideKey}) Bad Angle ({sBad:F0}%): срез угла {sAngle:F0}°" :
                                  $"S{s + 1} ({sideKey}): угол {sAngle:F0}°, {sSync:F0}% sync, {currentSpeed:F0} u/s, длина {sLenY:F1}u");

                    points.Add(new TrajPoint
                    {
                        Pos = new Vector2(curX, curY),
                        StrafeIndex = s,
                        Speed = currentSpeed,
                        Sync = sSync,
                        IsOverlap = isOverlapTick,
                        IsBadAngle = isBadAngleTick,
                        IsClean = !isOverlapTick && !isBadAngleTick,
                        Note = note
                    });
                }
            }

            return points;
        }

        private static void DrawWorkoutDrillCard(int dx, ref int dy, int dw, float scale, string title, string tag, Color tagCol, string goal, string instruction, string gain)
        {
            int cardH = (int)(110 * scale);
            Raylib.DrawRectangle(dx, dy, dw, cardH, new Color(13, 18, 27, 230));
            Raylib.DrawRectangleLines(dx, dy, dw, cardH, new Color(tagCol.R, tagCol.G, tagCol.B, (byte)70));
            Raylib.DrawRectangle(dx, dy, 4, cardH, tagCol);

            Theme.DrawText(title, dx + 12, dy + 8, 10, Theme.TextWhite);

            int tagW = (int)(95 * scale);
            int tagH = (int)(18 * scale);
            Raylib.DrawRectangle(dx + dw - tagW - 10, dy + 6, tagW, tagH, new Color(tagCol.R, tagCol.G, tagCol.B, (byte)35));
            Raylib.DrawRectangleLines(dx + dw - tagW - 10, dy + 6, tagW, tagH, tagCol);
            Theme.DrawText(tag, dx + dw - tagW - 4, dy + 9, 8, tagCol);

            Theme.DrawText(goal, dx + 12, dy + 30, 9, Theme.NeonCyan);
            Theme.DrawText(instruction, dx + 12, dy + 48, 8, Theme.TextDim);
            Theme.DrawText(gain, dx + 12, dy + 88, 9, Theme.NeonGreen);

            dy += cardH + 10;
        }

        // =========================================================================
        // MANUAL SETTINGS & OPTIONS POPUP (ONLY IF TRIGGERED)
        // =========================================================================
        private void DrawNickEditModal(int screenWidth, int screenHeight, float scale, UserProfile prof)
        {
            var cs = prof.Cybershoke;

            Raylib.DrawRectangle(0, 0, screenWidth, screenHeight, new Color(0, 0, 0, 220));

            int popW = (int)(540 * scale);
            int popH = (int)(320 * scale);
            int popX = (screenWidth - popW) / 2;
            int popY = (screenHeight - popH) / 2;

            Theme.DrawGlassPanel(popX, popY, popW, popH);

            int pHeaderH = (int)(38 * scale);
            Raylib.DrawRectangle(popX, popY, popW, pHeaderH, Theme.BgPanelHeader);
            Raylib.DrawLine(popX, popY + pHeaderH, popX + popW, popY + pHeaderH, Theme.Border);
            Theme.DrawText("НАСТРОЙКА НИКНЕЙМА И СИНХРОНИЗАЦИИ", popX + 16, popY + (pHeaderH - Theme.GetScaledFontSize(13)) / 2, 13, Theme.NeonCyan);

            int cy = popY + pHeaderH + 16;

            Theme.DrawText("НИКНЕЙМ В CS2 (ДЛЯ ФИЛЬТРАЦИИ ДРУГИХ ИГРОКОВ):", popX + 16, cy, 10, Theme.NeonCyan);
            cy += (int)(16 * scale);
            int inpW = popW - 32;
            int inpH = (int)(30 * scale);

            Raylib.DrawRectangle(popX + 16, cy, inpW, inpH, new Color(10, 15, 22, 255));
            Raylib.DrawRectangleLines(popX + 16, cy, inpW, inpH, _activeInputIndex == 0 ? Theme.NeonCyan : Theme.Border);
            Theme.DrawText(_nickBuffer, popX + 24, cy + (inpH - Theme.GetScaledFontSize(12)) / 2, 12, Theme.TextWhite);
            cy += inpH + (int)(14 * scale);

            Theme.DrawText("KZ РАНГ В ТОПЕ (#):", popX + 16, cy, 10, Theme.NeonGold);
            cy += (int)(16 * scale);
            Raylib.DrawRectangle(popX + 16, cy, inpW, inpH, new Color(10, 15, 22, 255));
            Raylib.DrawRectangleLines(popX + 16, cy, inpW, inpH, _activeInputIndex == 1 ? Theme.NeonGold : Theme.Border);
            Theme.DrawText(_rankBuffer, popX + 24, cy + (inpH - Theme.GetScaledFontSize(12)) / 2, 12, Theme.TextWhite);
            cy += inpH + (int)(20 * scale);

            int ch = Raylib.GetCharPressed();
            while (ch > 0)
            {
                if (_activeInputIndex == 0 && _nickBuffer.Length < 32) _nickBuffer += (char)ch;
                else if (_activeInputIndex == 1 && char.IsDigit((char)ch) && _rankBuffer.Length < 7) _rankBuffer += (char)ch;
                ch = Raylib.GetCharPressed();
            }
            if (Raylib.IsKeyPressed(KeyboardKey.Backspace))
            {
                if (_activeInputIndex == 0 && _nickBuffer.Length > 0) _nickBuffer = _nickBuffer[..^1];
                else if (_activeInputIndex == 1 && _rankBuffer.Length > 0) _rankBuffer = _rankBuffer[..^1];
            }
            if (Raylib.IsKeyPressed(KeyboardKey.Tab))
            {
                _activeInputIndex = (_activeInputIndex + 1) % 2;
            }

            int btnW = (popW - 40) / 2;
            int btnH = (int)(32 * scale);

            if (Theme.DrawButton(popX + 16, cy, btnW, btnH, "СОХРАНИТЬ [Enter]", true, 11) || Raylib.IsKeyPressed(KeyboardKey.Enter))
            {
                if (!string.IsNullOrWhiteSpace(_nickBuffer)) cs.CybershokeNick = _nickBuffer.Trim();
                if (int.TryParse(_rankBuffer, out int rk) && rk > 0)
                {
                    cs.KzPosition = rk;
                    cs.GlobalRankPosition = rk;
                }
                UserProfile.Save();
                _isEditingNick = false;
            }

            if (Theme.DrawButton(popX + 24 + btnW, cy, btnW, btnH, "ОТМЕНА [Esc]", false, 11))
            {
                _isEditingNick = false;
            }
        }
    }
}
