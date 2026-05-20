using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Linq;
using Microsoft.Win32;
using System.Windows.Media.Animation;


namespace WinSpotlight
{
    public partial class MainWindow : Window
    {
        private GlobalHotKey _hotKey;
        private Storyboard? _showStoryboard;
        private Storyboard? _hideStoryboard;
        private bool _isHiding = false;

        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += MainWindow_Loaded;
            this.Closing += MainWindow_Closing;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var handle = new WindowInteropHelper(this).Handle;
            _hotKey = new GlobalHotKey(handle, 9000, GlobalHotKey.MOD_ALT, 0x43, OnHotKeyPressed);
            
            _showStoryboard = this.Resources["ShowStoryboard"] as Storyboard;
            _hideStoryboard = this.Resources["HideStoryboard"] as Storyboard;
            
            SetStartup();
            
            System.Threading.Tasks.Task.Run(() => SearchEngine.Init());
            this.Hide();
        }

        private void SetStartup()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true))
                {
                    string appName = "WinSpotlight";
                    string currentPath = Process.GetCurrentProcess().MainModule?.FileName;
                    if (!string.IsNullOrEmpty(currentPath) && key != null)
                    {
                        if (key.GetValue(appName) as string != currentPath)
                        {
                            key.SetValue(appName, currentPath);
                        }
                    }
                }
            }
            catch
            {
                // Fail silently if unable to write to registry
            }
        }

        private void OnHotKeyPressed()
        {
            if (this.Visibility == Visibility.Visible && !_isHiding)
                HideWindow();
            else
                ShowWindow();
        }

        private void ShowWindow()
        {
            _isHiding = false;
            this.Opacity = 0;
            this.Show();
            this.Activate();
            SearchBox.SelectAll();
            SearchBox.Focus();

            _showStoryboard?.Begin(this, true);
        }

        private void HideWindow()
        {
            if (_isHiding) return;
            _isHiding = true;

            if (_hideStoryboard != null)
            {
                _hideStoryboard.Completed -= HideStoryboard_Completed;
                _hideStoryboard.Completed += HideStoryboard_Completed;
                _hideStoryboard.Begin(this, true);
            }
            else
            {
                ActualHide();
            }
        }

        private void HideStoryboard_Completed(object? sender, EventArgs e)
        {
            if (_isHiding)
            {
                ActualHide();
            }
        }

        private void ActualHide()
        {
            _isHiding = false;
            this.Hide();
            SearchBox.Text = "";
            SuggestionsList.Visibility = Visibility.Collapsed;
            this.Height = 90;
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            HideWindow();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var query = SearchBox.Text.Trim();
            if (string.IsNullOrEmpty(query))
            {
                SuggestionsList.Visibility = Visibility.Collapsed;
                this.Height = 90;
                return;
            }

            var suggestions = SearchEngine.GetSuggestions(query);
            SuggestionsList.ItemsSource = suggestions;
            
            if (suggestions.Any())
            {
                SuggestionsList.Visibility = Visibility.Visible;
                SuggestionsList.SelectedIndex = 0;
                
                var targetHeight = 90 + (suggestions.Count * 45) + 20; 
                this.Height = Math.Min(targetHeight, 600);
            }
            else
            {
                SuggestionsList.Visibility = Visibility.Collapsed;
                this.Height = 90;
            }
        }

        private void SearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                HideWindow();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Enter && SearchBox.Text.Trim().Equals("/exit", StringComparison.OrdinalIgnoreCase))
            {
                Application.Current.Shutdown();
                e.Handled = true;
                return;
            }

            if (SuggestionsList.Visibility != Visibility.Visible || SuggestionsList.Items.Count == 0) 
                return;

            int count = SuggestionsList.Items.Count;
            int current = SuggestionsList.SelectedIndex;

            if (e.Key == Key.Down)
            {
                SuggestionsList.SelectedIndex = (current + 1) % count;
                SuggestionsList.ScrollIntoView(SuggestionsList.SelectedItem);
                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                SuggestionsList.SelectedIndex = (current - 1 < 0) ? count - 1 : current - 1;
                SuggestionsList.ScrollIntoView(SuggestionsList.SelectedItem);
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                ExecuteSelected();
                e.Handled = true;
            }
        }

        private void SuggestionsList_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (SuggestionsList.SelectedItem != null)
            {
                ExecuteSelected();
            }
        }

        private void ExecuteSelected()
        {
            if (!(SuggestionsList.SelectedItem is SuggestionItem item)) return;

            try
            {
                if (item.Description == "Calculator result")
                {
                    Clipboard.SetText(item.Title);
                }
                else
                {
                    Process.Start(new ProcessStartInfo { FileName = item.Path, UseShellExecute = true });
                }
            }
            catch { }

            HideWindow();
        }

        private void Thumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            var w = this.Width + e.HorizontalChange;
            var h = this.Height + e.VerticalChange;
            if (w >= this.MinWidth) this.Width = w;
            if (h >= this.MinHeight) this.Height = h;
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _hotKey?.Dispose();
        }
    }
}