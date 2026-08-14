using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MusicPlayer.Services
{
    /// <summary>
    /// Сервис для получения ICY-метаданных от интернет-радиостанций
    /// </summary>
    public class IcyMetadataService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private CancellationTokenSource _cancellationTokenSource;
        private Task _metadataTask;
        private bool _isDisposed;

        /// <summary>
        /// Событие возникает при обновлении метаданных
        /// </summary>
        public event EventHandler<MetadataEventArgs> MetadataUpdated;

        /// <summary>
        /// Текущие метаданные
        /// </summary>
        public string CurrentTitle { get; private set; } = string.Empty;

        /// <summary>
        /// Текущий исполнитель
        /// </summary>
        public string CurrentArtist { get; private set; } = string.Empty;

        /// <summary>
        /// Полное название трека
        /// </summary>
        public string FullTitle { get; private set; } = string.Empty;

        /// <summary>
        /// Название станции
        /// </summary>
        public string StationName { get; private set; } = string.Empty;

        /// <summary>
        /// Активно ли получение метаданных
        /// </summary>
        public bool IsActive { get; private set; }

        public IcyMetadataService()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(10);
        }

        /// <summary>
        /// Начать получение метаданных
        /// </summary>
        public async Task<bool> StartAsync(string url, string stationName = "")
        {
            try
            {
                Stop();

                StationName = string.IsNullOrEmpty(stationName) ? "Интернет-радио" : stationName;

                // Запускаем фоновую задачу
                _cancellationTokenSource = new CancellationTokenSource();
                _metadataTask = Task.Run(() => FetchMetadataLoop(url, _cancellationTokenSource.Token));
                IsActive = true;

                return true;
            }
            catch (Exception)
            {
                IsActive = false;
                return false;
            }
        }

        /// <summary>
        /// Основной цикл получения метаданных
        /// </summary>
        private async Task FetchMetadataLoop(string url, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await FetchMetadataAsync(url);
                }
                catch (Exception)
                {
                    // Игнорируем ошибки
                }

                // Ждём 5 секунд перед следующим запросом
                await Task.Delay(5000, cancellationToken);
            }
        }

        /// <summary>
        /// Получение метаданных через HTTP-запрос
        /// </summary>
        private async Task FetchMetadataAsync(string url)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("Icy-MetaData", "1");
                request.Headers.Add("User-Agent", "MusicPlayer/1.0");

                var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

                // Получаем метаданные из заголовков
                if (response.Headers.TryGetValues("icy-name", out var nameValues))
                {
                    StationName = nameValues.FirstOrDefault() ?? StationName;
                }

                // Пытаемся получить метаданные из тела ответа (если есть)
                var content = await response.Content.ReadAsStringAsync();

                // Ищем метаданные в формате StreamTitle='...'
                var match = Regex.Match(content, @"StreamTitle='([^']*)'");
                if (match.Success)
                {
                    var fullTitle = match.Groups[1].Value;
                    if (!string.IsNullOrEmpty(fullTitle) && fullTitle != CurrentTitle)
                    {
                        ParseAndUpdateMetadata(fullTitle);
                    }
                }
                else
                {
                    // Ищем альтернативный формат
                    var altMatch = Regex.Match(content, @"<title>(.*?)</title>", RegexOptions.IgnoreCase);
                    if (altMatch.Success)
                    {
                        var title = altMatch.Groups[1].Value;
                        if (!string.IsNullOrEmpty(title) && title != CurrentTitle)
                        {
                            ParseAndUpdateMetadata(title);
                        }
                    }
                }
            }
            catch (HttpRequestException)
            {
                // Станция недоступна
            }
            catch (Exception)
            {
                // Игнорируем другие ошибки
            }
        }

        /// <summary>
        /// Парсинг и обновление метаданных
        /// </summary>
        private void ParseAndUpdateMetadata(string fullTitle)
        {
            CurrentTitle = fullTitle;
            FullTitle = fullTitle;

            // Пытаемся разделить исполнителя и название
            var artistMatch = Regex.Match(fullTitle, @"^(.*?)\s+[-–]\s+(.*)$");
            if (artistMatch.Success)
            {
                CurrentArtist = artistMatch.Groups[1].Value.Trim();
                CurrentTitle = artistMatch.Groups[2].Value.Trim();
                FullTitle = $"{CurrentArtist} - {CurrentTitle}";
            }
            else
            {
                // Проверяем формат "Исполнитель - Название" с разными разделителями
                var altMatch = Regex.Match(fullTitle, @"^(.*?)\s*[|:;]\s*(.*)$");
                if (altMatch.Success)
                {
                    CurrentArtist = altMatch.Groups[1].Value.Trim();
                    CurrentTitle = altMatch.Groups[2].Value.Trim();
                    FullTitle = $"{CurrentArtist} - {CurrentTitle}";
                }
                else
                {
                    CurrentArtist = string.Empty;
                    CurrentTitle = fullTitle;
                    FullTitle = fullTitle;
                }
            }

            // Вызываем событие обновления
            MetadataUpdated?.Invoke(this, new MetadataEventArgs
            {
                Title = CurrentTitle,
                Artist = CurrentArtist,
                FullTitle = FullTitle,
                StationName = StationName
            });
        }

        /// <summary>
        /// Остановить получение метаданных
        /// </summary>
        public void Stop()
        {
            IsActive = false;
            _cancellationTokenSource?.Cancel();
            _metadataTask?.Wait(1000);
            _metadataTask = null;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }

        /// <summary>
        /// Освобождение ресурсов
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            Stop();
            _httpClient.Dispose();
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// Аргументы события обновления метаданных
    /// </summary>
    public class MetadataEventArgs : EventArgs
    {
        public string Title { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        public string FullTitle { get; set; } = string.Empty;
        public string StationName { get; set; } = string.Empty;
    }
}