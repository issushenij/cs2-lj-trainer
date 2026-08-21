using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Raylib_cs;

namespace LJTrainer.Core
{
    public static class MapImageCache
    {
        private static readonly string CacheDir = Path.Combine(AppContext.BaseDirectory, "cache", "maps");
        private static readonly ConcurrentDictionary<string, Texture2D?> _textureCache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, bool> _pendingDownloads = new(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentQueue<(string MapName, string FilePath)> _texturesToLoadOnMainThread = new();
        private static readonly HttpClient _httpClient;

        static MapImageCache()
        {
            try
            {
                if (!Directory.Exists(CacheDir))
                {
                    Directory.CreateDirectory(CacheDir);
                }
            }
            catch { }

            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (_, _, _, _) => true
            };
            _httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(8)
            };
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        }

        public static void UpdateMainThread()
        {
            // Load up to 4 pending textures per frame on the main thread
            int loadedThisFrame = 0;
            while (_texturesToLoadOnMainThread.TryDequeue(out var item) && loadedThisFrame < 4)
            {
                if (_textureCache.ContainsKey(item.MapName)) continue;

                if (File.Exists(item.FilePath))
                {
                    try
                    {
                        var img = Raylib.LoadImage(item.FilePath);
                        if (img.Width > 0 && img.Height > 0)
                        {
                            var tex = Raylib.LoadTextureFromImage(img);
                            Raylib.SetTextureFilter(tex, TextureFilter.Bilinear);
                            Raylib.UnloadImage(img);
                            _textureCache[item.MapName] = tex;
                            loadedThisFrame++;
                        }
                        else
                        {
                            _textureCache[item.MapName] = null;
                        }
                    }
                    catch
                    {
                        _textureCache[item.MapName] = null;
                    }
                }
            }
        }

        public static Texture2D? GetMapTexture(string mapName)
        {
            if (string.IsNullOrWhiteSpace(mapName)) return null;

            string cleanName = mapName.Trim().ToLowerInvariant();
            if (_textureCache.TryGetValue(cleanName, out var tex))
            {
                return tex;
            }

            string filePath = Path.Combine(CacheDir, $"{cleanName}.jpg");
            if (File.Exists(filePath))
            {
                _texturesToLoadOnMainThread.Enqueue((cleanName, filePath));
                return null;
            }

            // Trigger async multi-tier background resolver
            if (_pendingDownloads.TryAdd(cleanName, true))
            {
                Task.Run(async () =>
                {
                    try
                    {
                        // Tier 1: Query kztimerglobal API for workshop URL
                        try
                        {
                            string apiUrl = $"https://kztimerglobal.com/api/v2/maps/name/{cleanName}";
                            var apiResp = await _httpClient.GetAsync(apiUrl);
                            if (apiResp.IsSuccessStatusCode)
                            {
                                string jsonStr = await apiResp.Content.ReadAsStringAsync();
                                using var doc = JsonDocument.Parse(jsonStr);
                                if (doc.RootElement.TryGetProperty("workshop_url", out var wsProp))
                                {
                                    string? wsUrl = wsProp.GetString();
                                    if (!string.IsNullOrEmpty(wsUrl) && wsUrl.Contains("id="))
                                    {
                                        var mMatch = Regex.Match(wsUrl, @"id=(\d+)");
                                        if (mMatch.Success)
                                        {
                                            string wsPageUrl = $"https://steamcommunity.com/sharedfiles/filedetails/?id={mMatch.Groups[1].Value}";
                                            var pageResp = await _httpClient.GetAsync(wsPageUrl);
                                            if (pageResp.IsSuccessStatusCode)
                                            {
                                                string html = await pageResp.Content.ReadAsStringAsync();
                                                var imgMatch = Regex.Match(html, @"id=""previewImageMain""[^>]*src=""([^""]+)""");
                                                if (!imgMatch.Success)
                                                    imgMatch = Regex.Match(html, @"id=""previewImage""[^>]*src=""([^""]+)""");
                                                if (!imgMatch.Success)
                                                    imgMatch = Regex.Match(html, @"(https://steamuserimages-[^""]+)");

                                                if (imgMatch.Success)
                                                {
                                                    string imgUrl = imgMatch.Groups[1].Value.Replace("&amp;", "&");
                                                    if (await TryDownloadImageAsync(imgUrl, filePath))
                                                    {
                                                        _texturesToLoadOnMainThread.Enqueue((cleanName, filePath));
                                                        return;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        catch { }

                        // Tier 2: Search Steam Workshop browse page directly
                        try
                        {
                            string searchUrl = $"https://steamcommunity.com/workshop/browse/?appid=730&searchtext={cleanName}";
                            var sResp = await _httpClient.GetAsync(searchUrl);
                            if (sResp.IsSuccessStatusCode)
                            {
                                string html = await sResp.Content.ReadAsStringAsync();
                                var match = Regex.Match(html, @"class=""workshopItemPreviewImage ""[^>]*src=""([^""]+)""");
                                if (!match.Success)
                                    match = Regex.Match(html, @"(https://steamuserimages-[^""]+)");

                                if (match.Success)
                                {
                                    string imgUrl = match.Groups[1].Value.Replace("&amp;", "&");
                                    if (await TryDownloadImageAsync(imgUrl, filePath))
                                    {
                                        _texturesToLoadOnMainThread.Enqueue((cleanName, filePath));
                                        return;
                                    }
                                }
                            }
                        }
                        catch { }

                        // Tier 3: Direct CDN Fallbacks
                        string[] candidateUrls = new[]
                        {
                            $"https://raw.githubusercontent.com/KZGlobalTeam/cs2kz-images/master/images/medium/{cleanName}.jpg",
                            $"https://raw.githubusercontent.com/KZGlobalTeam/cs2kz-images/master/images/large/{cleanName}.jpg",
                            $"https://raw.githubusercontent.com/KZGlobalTeam/map-images/master/images/medium/{cleanName}.jpg",
                            $"https://raw.githubusercontent.com/KZGlobalTeam/map-images/master/images/large/{cleanName}.jpg"
                        };

                        foreach (var url in candidateUrls)
                        {
                            if (await TryDownloadImageAsync(url, filePath))
                            {
                                _texturesToLoadOnMainThread.Enqueue((cleanName, filePath));
                                return;
                            }
                        }
                    }
                    catch { }
                    finally
                    {
                        _pendingDownloads.TryRemove(cleanName, out _);
                    }
                });
            }

            return null;
        }

        private static async Task<bool> TryDownloadImageAsync(string url, string targetPath)
        {
            try
            {
                var resp = await _httpClient.GetAsync(url);
                if (resp.IsSuccessStatusCode)
                {
                    var bytes = await resp.Content.ReadAsByteArrayAsync();
                    if (bytes.Length > 100)
                    {
                        bool isJpg = bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;
                        bool isPng = bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47;
                        if (isJpg || isPng)
                        {
                            await File.WriteAllBytesAsync(targetPath, bytes);
                            return true;
                        }
                    }
                }
            }
            catch { }
            return false;
        }

        public static void Cleanup()
        {
            foreach (var kvp in _textureCache)
            {
                if (kvp.Value.HasValue && kvp.Value.Value.Id > 0)
                {
                    Raylib.UnloadTexture(kvp.Value.Value);
                }
            }
            _textureCache.Clear();
        }
    }
}
