using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace WinSpotlight
{
    public class SuggestionItem
    {
        public string Title { get; set; }
        public string Path { get; set; }
        public string Description { get; set; }
        public bool IsApp { get; set; }
        public System.Windows.Media.ImageSource Icon { get; set; }
    }

    public static class SearchEngine
    {
        private static List<SuggestionItem> _apps = new List<SuggestionItem>();

        public static void Init()
        {
            var userStartMenu = Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);
            var commonStartMenu = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu);

            LoadShortcuts(userStartMenu);
            LoadShortcuts(commonStartMenu);
            
            // Deduplicate
            _apps = _apps.GroupBy(a => a.Title).Select(g => g.First()).ToList();
        }

        private static void LoadShortcuts(string dir)
        {
            if (!Directory.Exists(dir)) return;

            try
            {
                var files = Directory.GetFiles(dir, "*.lnk", SearchOption.AllDirectories);
                foreach (var f in files)
                {
                    var name = Path.GetFileNameWithoutExtension(f);
                    
                    if (name.Contains("uninstall", StringComparison.OrdinalIgnoreCase)) continue;

                    System.Windows.Media.ImageSource icon = null;
                    try
                    {
                        using (var sysIcon = System.Drawing.Icon.ExtractAssociatedIcon(f))
                        {
                            if (sysIcon != null)
                            {
                                icon = Imaging.CreateBitmapSourceFromHIcon(
                                    sysIcon.Handle,
                                    Int32Rect.Empty,
                                    BitmapSizeOptions.FromEmptyOptions());
                                icon.Freeze(); // important for cross-thread access
                            }
                        }
                    }
                    catch { }

                    _apps.Add(new SuggestionItem
                    {
                        Title = name,
                        Path = f,
                        Description = "App",
                        IsApp = true,
                        Icon = icon
                    });
                }
            }
            catch 
            { 
            }
        }

        public static List<SuggestionItem> GetSuggestions(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return new List<SuggestionItem>();

            var results = new List<SuggestionItem>();
            var q = query.ToLowerInvariant();

            // 1. Math eval
            if (query.Any(char.IsDigit) && !query.Any(char.IsLetter))
            {
                try
                {
                    var res = new System.Data.DataTable().Compute(query, null);
                    results.Add(new SuggestionItem { Title = res.ToString(), Description = "Calculator result", Path = query, IsApp = false });
                }
                catch { }
            }

            // 2. Apps
            var matchedApps = _apps
                .Where(a => a.Title.ToLowerInvariant().Contains(q))
                .OrderBy(a => a.Title.StartsWith(q, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .Take(6);
                
            results.AddRange(matchedApps);

            // 3. Web Search Fallback
            results.Add(new SuggestionItem 
            { 
                Title = $"Search Google for '{query}'", 
                Path = $"https://www.google.com/search?q={Uri.EscapeDataString(query)}", 
                Description = "Web",
                IsApp = false 
            });

            return results;
        }
    }
}
