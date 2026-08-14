using MusicPlayer.Services;
using MusicPlayer.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace MusicPlayer
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private MainViewModel _viewModel;
        private bool _isDragging;
        private bool _isVolumeDragging;
        private string _currentStreamUrl = string.Empty;
        private bool _isStreamPlaying;
        private readonly HttpClient _httpClient = new HttpClient();
        private IcyMetadataService _metadataService;
        private System.Timers.Timer _metadataFallbackTimer;
        private string _currentStationName = string.Empty;
        private bool _isMetadataLoaded;

        public MainWindow()
        {
            InitializeComponent();

            _viewModel = (MainViewModel)DataContext;
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;

            Loaded += (sender, e) =>
            {
                var searchBox = FindVisualChild<TextBox>(this);
                if (searchBox != null && searchBox.ToolTip?.ToString() == "Поиск...")
                {
                    searchBox.Focus();
                }

                // Инициализация таймера для fallback-обновления метаданных
                _metadataFallbackTimer = new System.Timers.Timer(10000);
                _metadataFallbackTimer.Elapsed += async (timerSender, timerArgs) => await UpdateMetadataFallback();
                _metadataFallbackTimer.AutoReset = true;
            };
        }

        #region === Вспомогательные методы ===

        private T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild)
                {
                    return typedChild;
                }
                var result = FindVisualChild<T>(child);
                if (result != null)
                {
                    return result;
                }
            }
            return null;
        }

        #endregion

        #region === Обработчики событий ViewModel ===

        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.CurrentSong))
            {
                UpdateNowPlayingInfo();
            }
        }

        private void UpdateNowPlayingInfo()
        {
            if (_viewModel.CurrentSong != null)
            {
                Title = $"🎵 {_viewModel.CurrentSong.Title} — Music Player";
                StatusText.Text = $"🎵 {_viewModel.CurrentSong.Artist} — {_viewModel.CurrentSong.Title}";
            }
            else
            {
                Title = "🎵 Music Player";
                StatusText.Text = "🎵 Готов к воспроизведению";
            }
        }

        #endregion

        #region === Слайдер прогресса ===

        private void Slider_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            _isDragging = true;
        }

        private void Slider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            _isDragging = false;
            if (_viewModel != null && sender is Slider slider)
            {
                _viewModel.SeekCommand.Execute(slider.Value);
            }
        }

        #endregion

        #region === Горячие клавиши ===

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            var searchBox = FindVisualChild<TextBox>(this);
            bool isSearchFocused = searchBox != null && searchBox.IsFocused;

            switch (e.Key)
            {
                case Key.MediaPlayPause:
                    _viewModel.PlayPauseCommand.Execute(null);
                    e.Handled = true;
                    break;

                case Key.Space when !isSearchFocused:
                    _viewModel.PlayPauseCommand.Execute(null);
                    e.Handled = true;
                    break;

                case Key.MediaNextTrack:
                    _viewModel.NextCommand.Execute(null);
                    e.Handled = true;
                    break;

                case Key.MediaPreviousTrack:
                    _viewModel.PreviousCommand.Execute(null);
                    e.Handled = true;
                    break;

                case Key.MediaStop:
                    _viewModel.StopCommand.Execute(null);
                    e.Handled = true;
                    break;

                case Key.F5:
                    if (_viewModel.CurrentSong != null)
                    {
                        _viewModel.PlayCommand.Execute(null);
                    }
                    e.Handled = true;
                    break;
            }
        }

        #endregion

        #region === Обработчики событий окна ===

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _viewModel.StopCommand.Execute(null);
            StreamMediaElement.Stop();
            _metadataService?.Stop();
            _metadataService?.Dispose();
            _metadataFallbackTimer?.Stop();
            _metadataFallbackTimer?.Dispose();
            _httpClient.Dispose();
        }

        #endregion

        #region === Обработчики кнопок ===

        private void ClearSearchButton_Click(object sender, RoutedEventArgs e)
        {
            var searchBox = FindVisualChild<TextBox>(this);
            if (searchBox != null && searchBox.ToolTip?.ToString() == "Поиск...")
            {
                searchBox.Clear();
                searchBox.Focus();
            }
        }

        #endregion

        #region === Обработчики перетаскивания файлов ===

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    var playlist = _viewModel.CurrentPlaylist;
                    if (playlist != null)
                    {
                        foreach (var file in files)
                        {
                            if (file.EndsWith(".mp3") || file.EndsWith(".wav") ||
                                file.EndsWith(".flac") || file.EndsWith(".aac") ||
                                file.EndsWith(".ogg"))
                            {
                                var metadataService = new MetadataService();
                                var song = metadataService.ReadMetadata(file);
                                playlist.Songs.Add(song);
                            }
                        }
                        var playlistService = new PlaylistService();
                        playlistService.SavePlaylists(_viewModel.Playlists.ToList());
                        StatusText.Text = $"✅ Добавлено файлов: {files.Length}";
                    }
                }
            }
        }

        private void Window_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2a2a4a"));
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void Window_DragLeave(object sender, DragEventArgs e)
        {
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1a1a2e"));
            e.Handled = true;
        }

        #endregion

        #region === Двойной клик по треку ===

        private void SongListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListView listView && listView.SelectedItem != null)
            {
                _viewModel.PlaySongCommand.Execute(listView.SelectedItem);
            }
        }

        #endregion

        #region === Обновление метаданных ===

        /// <summary>
        /// Обновление метаданных через ICY-сервис
        /// </summary>
        private void UpdateMetadataFromIcy(string title, string artist, string fullTitle, string stationName)
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    _isMetadataLoaded = true;

                    if (!string.IsNullOrEmpty(fullTitle))
                    {
                        NowPlayingTitle.Text = $"🎵 {fullTitle}";
                        StatusText.Text = $"🎵 {fullTitle}";
                    }
                    else if (!string.IsNullOrEmpty(title))
                    {
                        NowPlayingTitle.Text = $"🎵 {title}";
                        StatusText.Text = $"🎵 {title}";
                    }

                    if (!string.IsNullOrEmpty(stationName))
                    {
                        NowPlayingStation.Text = $"📻 {stationName}";
                    }

                    StreamStatusIndicator.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27c93f"));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"UpdateMetadataFromIcy error: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Fallback-метод для получения метаданных через HTTP (если ICY не работает)
        /// </summary>
        private async Task UpdateMetadataFallback()
        {
            if (string.IsNullOrEmpty(_currentStreamUrl) || !_isStreamPlaying || _isMetadataLoaded)
                return;

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, _currentStreamUrl);
                request.Headers.Add("Icy-MetaData", "1");
                request.Headers.Add("User-Agent", "MusicPlayer/1.0");

                var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

                // Проверяем заголовки
                if (response.Headers.TryGetValues("icy-name", out var nameValues))
                {
                    var stationName = nameValues.FirstOrDefault();
                    if (!string.IsNullOrEmpty(stationName))
                    {
                        Dispatcher.Invoke(() =>
                        {
                            NowPlayingStation.Text = $"📻 {stationName}";
                        });
                    }
                }

                // Пытаемся получить метаданные из тела
                var content = await response.Content.ReadAsStringAsync();
                var match = Regex.Match(content, @"StreamTitle='([^']*)'");
                if (match.Success)
                {
                    var title = match.Groups[1].Value;
                    if (!string.IsNullOrEmpty(title))
                    {
                        Dispatcher.Invoke(() =>
                        {
                            NowPlayingTitle.Text = $"🎵 {title}";
                            StatusText.Text = $"🎵 {title}";
                            StreamStatusIndicator.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27c93f"));
                            _isMetadataLoaded = true;
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateMetadataFallback error: {ex.Message}");
            }
        }

        /// <summary>
        /// Тестовый метод для отладки метаданных
        /// </summary>
        private async Task TestMetadataAsync(string url)
        {
            try
            {
                var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(5);

                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("Icy-MetaData", "1");
                request.Headers.Add("User-Agent", "MusicPlayer/1.0");

                var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

                // Выводим заголовки в консоль для отладки
                System.Diagnostics.Debug.WriteLine("=== ICY HEADERS ===");
                foreach (var header in response.Headers)
                {
                    System.Diagnostics.Debug.WriteLine($"{header.Key}: {string.Join(", ", header.Value)}");
                }

                // Проверяем наличие ICY-заголовков
                if (response.Headers.TryGetValues("icy-metaint", out var metaInt))
                {
                    System.Diagnostics.Debug.WriteLine($"Meta Interval: {string.Join(", ", metaInt)}");
                }

                if (response.Headers.TryGetValues("icy-name", out var name))
                {
                    var stationName = string.Join(", ", name);
                    System.Diagnostics.Debug.WriteLine($"Station Name: {stationName}");
                    Dispatcher.Invoke(() =>
                    {
                        NowPlayingStation.Text = $"📻 {stationName}";
                    });
                }

                if (response.Headers.TryGetValues("icy-br", out var br))
                {
                    System.Diagnostics.Debug.WriteLine($"Bitrate: {string.Join(", ", br)}");
                }

                // Пытаемся прочитать первый блок данных
                var stream = await response.Content.ReadAsStreamAsync();
                var buffer = new byte[4096];
                var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);

                // Ищем метаданные в данных
                var data = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                var match = Regex.Match(data, @"StreamTitle='([^']*)'");
                if (match.Success)
                {
                    var title = match.Groups[1].Value;
                    System.Diagnostics.Debug.WriteLine($"Found Title: {title}");
                    Dispatcher.Invoke(() =>
                    {
                        if (!string.IsNullOrEmpty(title))
                        {
                            NowPlayingTitle.Text = $"🎵 {title}";
                            StatusText.Text = $"🎵 {title}";
                            StreamStatusIndicator.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27c93f"));
                            _isMetadataLoaded = true;
                        }
                    });
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("No StreamTitle found in first chunk");

                    // Пробуем найти альтернативный формат
                    var altMatch = Regex.Match(data, @"<title>(.*?)</title>", RegexOptions.IgnoreCase);
                    if (altMatch.Success)
                    {
                        var title = altMatch.Groups[1].Value;
                        System.Diagnostics.Debug.WriteLine($"Found Alternative Title: {title}");
                        Dispatcher.Invoke(() =>
                        {
                            if (!string.IsNullOrEmpty(title))
                            {
                                NowPlayingTitle.Text = $"🎵 {title}";
                                StatusText.Text = $"🎵 {title}";
                                StreamStatusIndicator.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27c93f"));
                                _isMetadataLoaded = true;
                            }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TestMetadataAsync error: {ex.Message}");
                Dispatcher.Invoke(() =>
                {
                    StatusText.Text = $"⚠️ Метаданные не доступны: {ex.Message}";
                });
            }
        }

        #endregion

        #region === Потоковое воспроизведение ===

        private async void PlayStream(string url, string stationName = "")
        {
            try
            {
                if (string.IsNullOrEmpty(url))
                {
                    StatusText.Text = "❌ URL не может быть пустым";
                    return;
                }

                if (!Uri.TryCreate(url, UriKind.Absolute, out _))
                {
                    StatusText.Text = "❌ Некорректный URL";
                    return;
                }

                // Останавливаем локальное воспроизведение
                _viewModel.StopCommand.Execute(null);

                // Останавливаем текущий поток
                StreamMediaElement.Stop();
                StreamMediaElement.Close();

                // Останавливаем ICY-сервис
                _metadataService?.Stop();
                _metadataService?.Dispose();
                _metadataService = null;
                _isMetadataLoaded = false;

                // Обновляем информацию
                _currentStationName = stationName;
                NowPlayingStation.Text = string.IsNullOrEmpty(stationName) ? "🌐 Интернет-радио" : $"📻 {stationName}";
                NowPlayingTitle.Text = "🎵 Загрузка информации о треке...";
                StreamStatusIndicator.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFD700"));
                StatusText.Text = "⏳ Подключение к потоку...";

                // Запускаем MediaElement
                StreamMediaElement.Source = new Uri(url);
                StreamMediaElement.Play();

                _currentStreamUrl = url;
                _isStreamPlaying = true;
                StatusText.Text = $"🎵 Воспроизведение потока: {url}";

                // Запускаем ICY-сервис для получения метаданных
                _metadataService = new IcyMetadataService();
                _metadataService.MetadataUpdated += (s, e) =>
                {
                    UpdateMetadataFromIcy(e.Title, e.Artist, e.FullTitle, e.StationName);
                };

                var connected = await _metadataService.StartAsync(url, stationName);
                if (connected)
                {
                    StatusText.Text = $"✅ Подключено к {_metadataService.StationName}";
                    // Запускаем fallback-таймер на случай, если ICY не даёт данных
                    _metadataFallbackTimer?.Start();
                }
                else
                {
                    StatusText.Text = "⚠️ Метаданные не доступны (используется fallback)";
                    _metadataFallbackTimer?.Start();
                }

                // Отправляем тестовый запрос для отладки (только в режиме разработки)
#if DEBUG
                await TestMetadataAsync(url);
#endif

                // Если через 10 секунд метаданные не загрузились, пробуем fallback
                await Task.Delay(10000);
                if (!_isMetadataLoaded && _isStreamPlaying)
                {
                    await TestMetadataAsync(url);
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"❌ Ошибка воспроизведения потока: {ex.Message}";
                StreamStatusIndicator.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ff5f56"));
            }
        }

        private void StopStream()
        {
            StreamMediaElement.Stop();
            StreamMediaElement.Close();
            _isStreamPlaying = false;
            _currentStreamUrl = string.Empty;
            _isMetadataLoaded = false;
            _metadataService?.Stop();
            _metadataService?.Dispose();
            _metadataService = null;
            _metadataFallbackTimer?.Stop();
            StatusText.Text = "⏹️ Поток остановлен";
            NowPlayingTitle.Text = "🎵 Ожидание воспроизведения...";
            NowPlayingStation.Text = "Выберите радиостанцию или введите URL";
            StreamStatusIndicator.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ff5f56"));
        }

        private void PauseStream()
        {
            if (_isStreamPlaying)
            {
                StreamMediaElement.Pause();
                _isStreamPlaying = false;
                _metadataService?.Stop();
                _metadataFallbackTimer?.Stop();
                StatusText.Text = "⏸️ Поток на паузе";
                StreamStatusIndicator.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFD700"));
            }
        }

        private void ResumeStream()
        {
            if (!_isStreamPlaying && !string.IsNullOrEmpty(_currentStreamUrl))
            {
                StreamMediaElement.Play();
                _isStreamPlaying = true;
                _isMetadataLoaded = false;
                StatusText.Text = $"🎵 Возобновлено: {_currentStreamUrl}";
                StreamStatusIndicator.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27c93f"));

                // Перезапускаем получение метаданных
                _metadataService?.StartAsync(_currentStreamUrl, _currentStationName);
                _metadataFallbackTimer?.Start();
            }
        }

        private void ToggleStreamPlayPause()
        {
            if (_isStreamPlaying)
            {
                PauseStream();
            }
            else
            {
                ResumeStream();
            }
        }

        #endregion

        #region === Обработчики событий MediaElement ===

        private void StreamMediaElement_MediaOpened(object sender, RoutedEventArgs e)
        {
            StatusText.Text = "✅ Поток успешно загружен";
            _isStreamPlaying = true;
            StreamStatusIndicator.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27c93f"));
        }

        private void StreamMediaElement_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            StatusText.Text = $"❌ Ошибка воспроизведения потока: {e.ErrorException?.Message ?? "Неизвестная ошибка"}";
            _isStreamPlaying = false;
            _isMetadataLoaded = false;
            StreamStatusIndicator.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ff5f56"));
            _metadataService?.Stop();
            _metadataFallbackTimer?.Stop();
            NowPlayingTitle.Text = "🎵 Ошибка воспроизведения";
        }

        private void StreamMediaElement_MediaEnded(object sender, RoutedEventArgs e)
        {
            StatusText.Text = "⏹️ Поток завершён";
            _isStreamPlaying = false;
            _isMetadataLoaded = false;
            StreamStatusIndicator.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ff5f56"));
            _metadataService?.Stop();
            _metadataFallbackTimer?.Stop();
            NowPlayingTitle.Text = "🎵 Поток завершён";
        }

        #endregion

        #region === Обработчики URL-потока ===

        private void PlayStreamButton_Click(object sender, RoutedEventArgs e)
        {
            var url = StreamUrlTextBox.Text.Trim();
            if (string.IsNullOrEmpty(url) || url == "Введите URL аудио-потока...")
            {
                StatusText.Text = "❌ Введите URL аудио-потока";
                return;
            }
            PlayStream(url);
        }

        private void StreamUrlTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (StreamUrlTextBox.Text == "Введите URL аудио-потока...")
            {
                StreamUrlTextBox.Text = string.Empty;
                StreamUrlTextBox.Foreground = new SolidColorBrush(Colors.White);
            }
        }

        private void StreamUrlTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(StreamUrlTextBox.Text))
            {
                StreamUrlTextBox.Text = "Введите URL аудио-потока...";
                StreamUrlTextBox.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#a0a0b0"));
            }
        }

        #endregion

        #region === Меню ===

        private void OpenFileMenuItem_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.OpenFileCommand.Execute(null);
        }

        private void OpenFolderMenuItem_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.OpenFolderCommand.Execute(null);
        }

        private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void AboutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "🎵 Music Player v1.0\n\n" +
                "Музыкальный плеер на WPF\n" +
                "Поддерживает локальные файлы и интернет-радио\n" +
                "Автоматически определяет название трека для большинства радиостанций\n" +
                "Использует NAudio для воспроизведения\n" +
                "и TagLib# для чтения метаданных\n\n" +
                "© 2026",
                "О программе",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void ClearPlaylistMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.CurrentPlaylist != null)
            {
                var result = MessageBox.Show(
                    "Вы уверены, что хотите очистить текущий плейлист?",
                    "Подтверждение",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    _viewModel.CurrentPlaylist.Songs.Clear();
                    _viewModel.StopCommand.Execute(null);
                }
            }
        }

        // ===== Интернет-радио =====

        private void AddStreamUrlMenuItem_Click(object sender, RoutedEventArgs e)
        {
            StreamUrlTextBox.Focus();
            StreamUrlTextBox.SelectAll();
        }

        private void RadioRecord_Click(object sender, RoutedEventArgs e)
        {
            PlayStream("http://air2.radiorecord.ru:9003/rr_320", "Radio Record");
        }

        private void EuropaPlus_Click(object sender, RoutedEventArgs e)
        {
            PlayStream("http://ep128.hostingradio.ru:8030/ep128", "Europa Plus");
        }

        private void RusskoeRadio_Click(object sender, RoutedEventArgs e)
        {
            PlayStream("http://rusradio.hostingradio.ru/rusradio128.mp3", "Русское Радио");
        }

        private void RockFM_Click(object sender, RoutedEventArgs e)
        {
            PlayStream("http://rockfm.hostingradio.ru/rockfm128.mp3", "Rock FM");
        }

        private void DFM_Click(object sender, RoutedEventArgs e)
        {
            PlayStream("http://dfm.hostingradio.ru/dfm128.mp3", "DFM");
        }

        private void LoveRadio_Click(object sender, RoutedEventArgs e)
        {
            PlayStream("http://loveradio.hostingradio.ru/loveradio128.mp3", "Love Radio");
        }

        private void HitFM_Click(object sender, RoutedEventArgs e)
        {
            PlayStream("http://hitfm.hostingradio.ru/hitfm128.mp3", "Хит FM");
        }

        #endregion

        #region === Методы для отладки ===

        /// <summary>
        /// Метод для тестирования URL радиостанции (можно вызвать из консоли отладки)
        /// </summary>
        private async Task DebugStream(string url)
        {
            await TestMetadataAsync(url);
        }

        #endregion
    }
}