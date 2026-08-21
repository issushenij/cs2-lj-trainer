using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace LJTrainer.Core
{
    public class GitHubReleaseInfo
    {
        public string TagName { get; set; } = "";
        public string Name { get; set; } = "";
        public string Body { get; set; } = "";
        public string HtmlUrl { get; set; } = "";
        public string DownloadUrl { get; set; } = "";
        public string ExeDownloadUrl { get; set; } = "";
        public long SizeBytes { get; set; } = 0;
    }

    public static class UpdateManager
    {
        public static string CurrentVersion { get; } = GetCurrentVersion();
        private const string RepoOwner = "issushenij";
        private const string RepoName = "cs2-lj-trainer";
        private const string ApiUrl = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";

        private static string GetCurrentVersion()
        {
            try
            {
                var asm = System.Reflection.Assembly.GetExecutingAssembly();
                var infoVer = asm.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>()?.InformationalVersion;
                if (!string.IsNullOrEmpty(infoVer))
                {
                    string clean = infoVer.Split('+')[0].Trim();
                    return clean.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? clean : "v" + clean;
                }
                var ver = asm.GetName().Version;
                if (ver != null)
                {
                    return $"v{ver.Major}.{ver.Minor}.{ver.Build}";
                }
            }
            catch { }
            return "v1.1.7";
        }

        public static bool IsChecking { get; private set; } = false;
        public static bool UpdateAvailable { get; private set; } = false;
        public static bool ShowUpdatePrompt { get; set; } = false;
        public static bool IsDownloading { get; private set; } = false;
        public static float DownloadProgress { get; private set; } = 0f;
        public static string StatusMessage { get; private set; } = "";
        public static GitHubReleaseInfo? LatestRelease { get; private set; } = null;

        public static async Task CheckForUpdatesAsync(bool silent = true)
        {
            if (IsChecking) return;
            IsChecking = true;
            if (!silent) StatusMessage = "Проверка обновлений...";

            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "CS2-LJTrainer-App");
                client.Timeout = TimeSpan.FromSeconds(8);

                var response = await client.GetAsync(ApiUrl);
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    string tag = root.GetProperty("tag_name").GetString() ?? "";
                    string name = root.TryGetProperty("name", out var nEl) ? (nEl.GetString() ?? tag) : tag;
                    string body = root.TryGetProperty("body", out var bEl) ? (bEl.GetString() ?? "") : "";
                    string htmlUrl = root.TryGetProperty("html_url", out var hEl) ? (hEl.GetString() ?? "") : "";

                    string downloadUrl = "";
                    string exeDownloadUrl = "";
                    long size = 0;

                    if (root.TryGetProperty("assets", out var assetsEl) && assetsEl.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var asset in assetsEl.EnumerateArray())
                        {
                            string aName = asset.GetProperty("name").GetString() ?? "";
                            string aUrl = asset.GetProperty("browser_download_url").GetString() ?? "";
                            long aSize = asset.TryGetProperty("size", out var sEl) ? sEl.GetInt64() : 0;

                            if (aName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                            {
                                downloadUrl = aUrl;
                                size = aSize;
                            }
                            else if (aName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                            {
                                exeDownloadUrl = aUrl;
                                if (string.IsNullOrEmpty(downloadUrl)) size = aSize;
                            }
                        }
                    }

                    if (string.IsNullOrEmpty(downloadUrl) && !string.IsNullOrEmpty(exeDownloadUrl))
                    {
                        downloadUrl = exeDownloadUrl;
                    }

                    LatestRelease = new GitHubReleaseInfo
                    {
                        TagName = tag,
                        Name = name,
                        Body = body,
                        HtmlUrl = htmlUrl,
                        DownloadUrl = downloadUrl,
                        ExeDownloadUrl = exeDownloadUrl,
                        SizeBytes = size
                    };

                    if (IsNewerVersion(tag, CurrentVersion))
                    {
                        UpdateAvailable = true;
                        ShowUpdatePrompt = true;
                        StatusMessage = $"Доступна новая версия: {tag}";
                    }
                    else
                    {
                        UpdateAvailable = false;
                        if (!silent) StatusMessage = "У вас установлена последняя версия!";
                    }
                }
                else
                {
                    if (!silent) StatusMessage = "Не удалось проверить обновления.";
                }
            }
            catch (Exception ex)
            {
                if (!silent) StatusMessage = "Ошибка соединения при проверке.";
                Debug.WriteLine($"[UpdateManager] Check failed: {ex.Message}");
            }
            finally
            {
                IsChecking = false;
            }
        }

        public static bool IsNewerVersion(string remoteTag, string currentTag)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(remoteTag) || string.IsNullOrWhiteSpace(currentTag)) return false;

                string r = remoteTag.Trim().TrimStart('v', 'V').Trim();
                string c = currentTag.Trim().TrimStart('v', 'V').Trim();

                if (string.Equals(r, c, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                var rParts = r.Split(new[] { '.', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
                var cParts = c.Split(new[] { '.', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
                int maxLen = Math.Max(rParts.Length, cParts.Length);

                for (int i = 0; i < maxLen; i++)
                {
                    int rNum = (i < rParts.Length && int.TryParse(rParts[i], out int rp)) ? rp : 0;
                    int cNum = (i < cParts.Length && int.TryParse(cParts[i], out int cp)) ? cp : 0;

                    if (rNum > cNum) return true;
                    if (rNum < cNum) return false;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        public static async Task PerformInAppUpdateAsync()
        {
            if (LatestRelease == null || string.IsNullOrEmpty(LatestRelease.DownloadUrl) || IsDownloading) return;

            IsDownloading = true;
            DownloadProgress = 0.05f;
            StatusMessage = "Скачивание обновления...";

            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            string tempDir = Path.Combine(Path.GetTempPath(), "LJTrainer_Update_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(tempDir);

            string downloadPath = Path.Combine(tempDir, "update_package" + (LatestRelease.DownloadUrl.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ? ".zip" : ".exe"));

            try
            {
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "CS2-LJTrainer-App");
                    using var response = await client.GetAsync(LatestRelease.DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
                    response.EnsureSuccessStatusCode();

                    long totalBytes = response.Content.Headers.ContentLength ?? (LatestRelease.SizeBytes > 0 ? LatestRelease.SizeBytes : -1);

                    using var stream = await response.Content.ReadAsStreamAsync();
                    using var fileStream = new FileStream(downloadPath, FileMode.Create, FileAccess.Write, FileShare.None);

                    byte[] buffer = new byte[81920];
                    long totalRead = 0;
                    int bytesRead;

                    while ((bytesRead = await stream.ReadAsync(buffer)) > 0)
                    {
                        await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                        totalRead += bytesRead;
                        if (totalBytes > 0)
                        {
                            DownloadProgress = Math.Clamp((float)totalRead / totalBytes, 0.05f, 0.95f);
                        }
                    }
                }

                DownloadProgress = 0.98f;
                StatusMessage = "Подготовка к перезапуску...";

                // Ensure user profile is completely flushed and saved before updating
                UserProfile.Save();
                AppConfig.Save();

                string currentExe = Process.GetCurrentProcess().MainModule?.FileName ?? Path.Combine(appDir, "LJTrainer.exe");
                int currentPid = Process.GetCurrentProcess().Id;

                // Create self-contained updater script in temp directory
                string updaterPsPath = Path.Combine(tempDir, "apply_update.ps1");
                string psScript = $@"
$pidToWait = {currentPid}
$targetExe = '{currentExe.Replace("'", "''")}'
$sourceExe = '{downloadPath.Replace("'", "''")}'

try {{
    $p = Get-Process -Id $pidToWait -ErrorAction SilentlyContinue
    if ($p) {{
        $p.WaitForExit(8000)
    }}
}} catch {{}}

Start-Sleep -Milliseconds 600

$copied = $false
for ($i = 0; $i -lt 10; $i++) {{
    try {{
        Copy-Item -Path $sourceExe -Destination $targetExe -Force -ErrorAction Stop
        $copied = $true
        break
    }} catch {{
        Start-Sleep -Milliseconds 500
    }}
}}

if ($copied) {{
    Start-Process -FilePath $targetExe -WorkingDirectory (Split-Path -Path $targetExe -Parent)
}}
";
                File.WriteAllText(updaterPsPath, psScript, System.Text.Encoding.UTF8);

                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{updaterPsPath}\"",
                    UseShellExecute = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                Process.Start(psi);
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                IsDownloading = false;
                StatusMessage = "Ошибка установки обновления: " + ex.Message;
                Debug.WriteLine($"[UpdateManager] Update failed: {ex}");
            }
        }
    }
}
