using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace LJTrainer.Core
{
    public class CS2ConfigImportResult
    {
        public bool Success { get; set; }
        public float Sensitivity { get; set; } = 1.0f;
        public float YawFactor { get; set; } = 0.022f;
        public string SourceFilePath { get; set; } = "";
        public string Message { get; set; } = "";
    }

    public static class CS2ConfigImporter
    {
        public static CS2ConfigImportResult TryAutoImport()
        {
            var candidatePaths = FindPotentialCS2ConfigFiles();

            foreach (var path in candidatePaths)
            {
                if (File.Exists(path))
                {
                    var result = ParseConfigFile(path);
                    if (result.Success)
                    {
                        return result;
                    }
                }
            }

            return new CS2ConfigImportResult
            {
                Success = false,
                Message = "Конфиг CS2 не найден автоматически. Укажите путь к cs2_user_convars.vcfg или autoexec.cfg вручную."
            };
        }

        public static List<string> FindPotentialCS2ConfigFiles()
        {
            var list = new List<string>();
            var steamRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. Check Windows Registry for Steam Path
            if (OperatingSystem.IsWindows())
            {
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
                    if (key?.GetValue("SteamPath") is string regPath && !string.IsNullOrEmpty(regPath))
                    {
                        steamRoots.Add(regPath.Replace('/', '\\'));
                    }
                }
                catch { }
            }

            // 2. Common Steam Drives & Directories
            string[] standardDrives = { "C", "D", "E", "F", "G" };
            foreach (var d in standardDrives)
            {
                steamRoots.Add($@"{d}:\Program Files (x86)\Steam");
                steamRoots.Add($@"{d}:\Program Files\Steam");
                steamRoots.Add($@"{d}:\Steam");
                steamRoots.Add($@"{d}:\SteamLibrary");
                steamRoots.Add($@"{d}:\Games\Steam");
            }

            // 3. Search userdata folders for CS2 AppID (730)
            foreach (var root in steamRoots)
            {
                string userdata = Path.Combine(root, "userdata");
                if (Directory.Exists(userdata))
                {
                    try
                    {
                        var userDirs = Directory.GetDirectories(userdata);
                        foreach (var uDir in userDirs)
                        {
                            string cs2CfgDir = Path.Combine(uDir, "730", "local", "cfg");
                            if (Directory.Exists(cs2CfgDir))
                            {
                                list.Add(Path.Combine(cs2CfgDir, "cs2_user_convars.vcfg"));
                                list.Add(Path.Combine(cs2CfgDir, "cs2_machine_convars.vcfg"));
                                list.Add(Path.Combine(cs2CfgDir, "cs2_user_keys.vcfg"));
                            }
                        }
                    }
                    catch { }
                }

                // Search game common cfg
                string gameCfgDir = Path.Combine(root, "steamapps", "common", "Counter-Strike Global Offensive", "game", "csgo", "cfg");
                if (Directory.Exists(gameCfgDir))
                {
                    list.Add(Path.Combine(gameCfgDir, "autoexec.cfg"));
                    list.Add(Path.Combine(gameCfgDir, "cs2_user_convars.vcfg"));
                    list.Add(Path.Combine(gameCfgDir, "config.cfg"));
                }
            }

            // Sort by LastWriteTime descending (most recent first)
            return list.Where(File.Exists)
                       .OrderByDescending(f => File.GetLastWriteTime(f))
                       .ToList();
        }

        public static CS2ConfigImportResult ParseConfigFile(string filePath)
        {
            try
            {
                string content = File.ReadAllText(filePath);

                // Look for "sensitivity" "<value>" or sensitivity <value>
                var sensMatch = Regex.Match(content, @"[""']?sensitivity[""']?\s+[""']?([0-9]+(?:\.[0-9]+)?)[""']?", RegexOptions.IgnoreCase);
                var yawMatch = Regex.Match(content, @"[""']?m_yaw[""']?\s+[""']?([0-9]+(?:\.[0-9]+)?)[""']?", RegexOptions.IgnoreCase);

                if (sensMatch.Success && float.TryParse(sensMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float sens))
                {
                    float yaw = 0.022f;
                    if (yawMatch.Success && float.TryParse(yawMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedYaw))
                    {
                        yaw = parsedYaw;
                    }

                    return new CS2ConfigImportResult
                    {
                        Success = true,
                        Sensitivity = sens,
                        YawFactor = yaw,
                        SourceFilePath = filePath,
                        Message = $"Успешно импортировано из {Path.GetFileName(filePath)}: Sens {sens:F2}, m_yaw {yaw:F3}"
                    };
                }
            }
            catch (Exception ex)
            {
                return new CS2ConfigImportResult
                {
                    Success = false,
                    Message = $"Ошибка чтения файла: {ex.Message}"
                };
            }

            return new CS2ConfigImportResult
            {
                Success = false,
                Message = "Параметр sensitivity не найден в файле."
            };
        }

        public static string? DetectLocalSteamId64(string? preferredNick = null)
        {
            var steamRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (OperatingSystem.IsWindows())
            {
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
                    if (key?.GetValue("SteamPath") is string regPath && !string.IsNullOrEmpty(regPath))
                    {
                        steamRoots.Add(regPath.Replace('/', '\\'));
                    }
                }
                catch { }
            }

            string[] standardDrives = { "C", "D", "E", "F", "G" };
            foreach (var d in standardDrives)
            {
                steamRoots.Add($@"{d}:\Program Files (x86)\Steam");
                steamRoots.Add($@"{d}:\Program Files\Steam");
                steamRoots.Add($@"{d}:\Steam");
                steamRoots.Add($@"{d}:\SteamLibrary");
            }

            // 1. Check Steam config\loginusers.vdf for exact PersonaName match or MostRecent=1 / AutoLogin=1
            foreach (var root in steamRoots)
            {
                string loginUsersPath = Path.Combine(root, "config", "loginusers.vdf");
                if (File.Exists(loginUsersPath))
                {
                    try
                    {
                        string content = File.ReadAllText(loginUsersPath);
                        // Regex match all user blocks
                        var matches = Regex.Matches(content, @"""(765611\d+)""\s*\{([^}]+)\}", RegexOptions.Singleline);
                        
                        string? mostRecentSid = null;
                        string? personaMatchedSid = null;

                        foreach (Match m in matches)
                        {
                            string sid = m.Groups[1].Value;
                            string block = m.Groups[2].Value;

                            var pMatch = Regex.Match(block, @"""PersonaName""\s*""([^""]+)""", RegexOptions.IgnoreCase);
                            string persona = pMatch.Success ? pMatch.Groups[1].Value : "";

                            // If preferredNick is given, match exact PersonaName
                            if (!string.IsNullOrEmpty(preferredNick) && !string.IsNullOrEmpty(persona))
                            {
                                if (persona.Equals(preferredNick, StringComparison.OrdinalIgnoreCase) ||
                                    preferredNick.Contains(persona, StringComparison.OrdinalIgnoreCase) ||
                                    persona.Contains(preferredNick, StringComparison.OrdinalIgnoreCase))
                                {
                                    personaMatchedSid = sid;
                                }
                            }

                            if (block.Contains("\"MostRecent\"\t\t\"1\"") || block.Contains("\"MostRecent\" \"1\"") ||
                                block.Contains("\"AutoLogin\"\t\t\"1\"") || block.Contains("\"AutoLogin\" \"1\""))
                            {
                                mostRecentSid = sid;
                            }
                        }

                        if (!string.IsNullOrEmpty(personaMatchedSid))
                            return personaMatchedSid;

                        if (!string.IsNullOrEmpty(mostRecentSid))
                            return mostRecentSid;
                    }
                    catch { }
                }
            }

            // 2. Fallback to userdata folders ordered by last modification
            foreach (var root in steamRoots)
            {
                string userdata = Path.Combine(root, "userdata");
                if (Directory.Exists(userdata))
                {
                    try
                    {
                        var userDirs = Directory.GetDirectories(userdata);
                        var orderedDirs = userDirs
                            .Select(d => new DirectoryInfo(d))
                            .Where(d => uint.TryParse(d.Name, out uint aid) && aid > 0)
                            .OrderByDescending(d => d.LastWriteTime)
                            .ToList();

                        foreach (var dir in orderedDirs)
                        {
                            if (uint.TryParse(dir.Name, out uint accountId32))
                            {
                                ulong steamId64 = 76561197960265728UL + accountId32;
                                return steamId64.ToString();
                            }
                        }
                    }
                    catch { }
                }
            }

            return null;
        }
    }
}
