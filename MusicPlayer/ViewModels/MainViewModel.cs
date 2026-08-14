using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using System.Windows.Threading;
using MusicPlayer.Models;
using MusicPlayer.Services;

namespace MusicPlayer.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly AudioPlayerService _audioPlayer;
        private readonly PlaylistService _playlistService;
        private readonly MetadataService _metadataService;

        private Playlist _currentPlaylist;
        private Song _currentSong;
        private bool _isPlaying;
        private double _volume = 0.8;
        private double _progress;
        private string _currentTime = "00:00";
        private string _totalTime = "00:00";
        private string _searchQuery;
        private bool _isRepeatAll;
        private bool _isRepeatOne;
        private bool _isShuffle;
        private DispatcherTimer _progressTimer;

        public ObservableCollection<Playlist> Playlists { get; } = new ObservableCollection<Playlist>();

        public Playlist CurrentPlaylist
        {
            get => _currentPlaylist;
            set => SetField(ref _currentPlaylist, value);
        }

        public Song CurrentSong
        {
            get => _currentSong;
            set => SetField(ref _currentSong, value);
        }

        public bool IsPlaying
        {
            get => _isPlaying;
            set => SetField(ref _isPlaying, value);
        }

        public double Volume
        {
            get => _volume;
            set
            {
                if (SetField(ref _volume, value))
                {
                    _audioPlayer.SetVolume(value);
                }
            }
        }

        public double Progress
        {
            get => _progress;
            set => SetField(ref _progress, value);
        }

        public string CurrentTime
        {
            get => _currentTime;
            set => SetField(ref _currentTime, value);
        }

        public string TotalTime
        {
            get => _totalTime;
            set => SetField(ref _totalTime, value);
        }

        public string SearchQuery
        {
            get => _searchQuery;
            set => SetField(ref _searchQuery, value);
        }

        public bool IsRepeatAll
        {
            get => _isRepeatAll;
            set => SetField(ref _isRepeatAll, value);
        }

        public bool IsRepeatOne
        {
            get => _isRepeatOne;
            set => SetField(ref _isRepeatOne, value);
        }

        public bool IsShuffle
        {
            get => _isShuffle;
            set => SetField(ref _isShuffle, value);
        }

        // ===== КОМАНДЫ =====
        public ICommand PlayCommand { get; }
        public ICommand PauseCommand { get; }
        public ICommand PlayPauseCommand { get; }  // 👈 Добавлено
        public ICommand StopCommand { get; }       // 👈 Добавлено
        public ICommand NextCommand { get; }       // 👈 Добавлено
        public ICommand PreviousCommand { get; }   // 👈 Добавлено
        public ICommand OpenFileCommand { get; }
        public ICommand OpenFolderCommand { get; }
        public ICommand CreatePlaylistCommand { get; }
        public ICommand DeletePlaylistCommand { get; }
        public ICommand RemoveSongCommand { get; }
        public ICommand PlaySongCommand { get; }
        public ICommand ToggleRepeatCommand { get; }
        public ICommand ToggleShuffleCommand { get; }
        public ICommand SeekCommand { get; }

        public MainViewModel()
        {
            _audioPlayer = new AudioPlayerService();
            _playlistService = new PlaylistService();
            _metadataService = new MetadataService();

            // ===== ИНИЦИАЛИЗАЦИЯ КОМАНД =====
            PlayCommand = new RelayCommand(_ => Play());
            PauseCommand = new RelayCommand(_ => Pause());
            PlayPauseCommand = new RelayCommand(_ => PlayPause());  // 👈 Добавлено
            StopCommand = new RelayCommand(_ => Stop());            // 👈 Добавлено
            NextCommand = new RelayCommand(_ => Next());            // 👈 Добавлено
            PreviousCommand = new RelayCommand(_ => Previous());    // 👈 Добавлено
            OpenFileCommand = new RelayCommand(_ => OpenFile());
            OpenFolderCommand = new RelayCommand(_ => OpenFolder());
            CreatePlaylistCommand = new RelayCommand(_ => CreatePlaylist());
            DeletePlaylistCommand = new RelayCommand(DeletePlaylist);
            RemoveSongCommand = new RelayCommand(RemoveSong);
            PlaySongCommand = new RelayCommand(PlaySong);
            ToggleRepeatCommand = new RelayCommand(_ => ToggleRepeat());
            ToggleShuffleCommand = new RelayCommand(_ => ToggleShuffle());
            SeekCommand = new RelayCommand(Seek);

            // Подписка на события
            _audioPlayer.PlaybackStopped += OnPlaybackStopped;
            _audioPlayer.PlaybackFinished += OnPlaybackFinished;

            // Загрузка данных
            LoadPlaylists();
        }

        #region === Методы управления воспроизведением ===

        private void Play()
        {
            if (CurrentSong == null)
            {
                if (CurrentPlaylist?.Songs.Count > 0)
                {
                    CurrentSong = CurrentPlaylist.Songs[0];
                }
                else
                {
                    return;
                }
            }

            _audioPlayer.Play(CurrentSong.FilePath);
            IsPlaying = true;
            StartProgressTimer();
        }

        private void Pause()
        {
            _audioPlayer.Pause();
            IsPlaying = false;
        }

        private void PlayPause()
        {
            if (IsPlaying)
            {
                Pause();
            }
            else
            {
                Play();
            }
        }

        private void Stop()
        {
            _audioPlayer.Stop();
            IsPlaying = false;
            Progress = 0;
            CurrentTime = "00:00";
            StopProgressTimer();
        }

        private void Next()
        {
            if (CurrentPlaylist == null || CurrentPlaylist.Songs.Count == 0) return;

            var index = CurrentPlaylist.Songs.IndexOf(CurrentSong ?? CurrentPlaylist.Songs[0]);
            index = (index + 1) % CurrentPlaylist.Songs.Count;
            CurrentSong = CurrentPlaylist.Songs[index];
            Play();
        }

        private void Previous()
        {
            if (CurrentPlaylist == null || CurrentPlaylist.Songs.Count == 0) return;

            var index = CurrentPlaylist.Songs.IndexOf(CurrentSong ?? CurrentPlaylist.Songs[0]);
            index = index - 1 < 0 ? CurrentPlaylist.Songs.Count - 1 : index - 1;
            CurrentSong = CurrentPlaylist.Songs[index];
            Play();
        }

        #endregion

        #region === Методы работы с файлами и плейлистами ===

        private void LoadPlaylists()
        {
            var playlists = _playlistService.LoadPlaylists();
            foreach (var playlist in playlists)
            {
                Playlists.Add(playlist);
            }

            if (Playlists.Count > 0)
            {
                CurrentPlaylist = Playlists[0];
            }
        }

        private void OpenFile()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Аудио файлы (*.mp3;*.wav;*.flac;*.aac;*.ogg)|*.mp3;*.wav;*.flac;*.aac;*.ogg|Все файлы (*.*)|*.*",
                Multiselect = true
            };

            if (dialog.ShowDialog() == true)
            {
                var playlist = CurrentPlaylist ?? CreateDefaultPlaylist();
                foreach (var file in dialog.FileNames)
                {
                    var song = _metadataService.ReadMetadata(file);
                    playlist.Songs.Add(song);
                }

                if (CurrentPlaylist == null)
                {
                    CurrentPlaylist = playlist;
                    Playlists.Add(playlist);
                }

                _playlistService.SavePlaylists(Playlists.ToList());
            }
        }

        private void OpenFolder()
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var files = Directory.GetFiles(dialog.SelectedPath, "*.*")
                    .Where(f => f.EndsWith(".mp3") || f.EndsWith(".wav") || f.EndsWith(".flac") ||
                               f.EndsWith(".aac") || f.EndsWith(".ogg"))
                    .ToArray();

                var playlist = CurrentPlaylist ?? CreateDefaultPlaylist();
                foreach (var file in files)
                {
                    var song = _metadataService.ReadMetadata(file);
                    playlist.Songs.Add(song);
                }

                if (CurrentPlaylist == null)
                {
                    CurrentPlaylist = playlist;
                    Playlists.Add(playlist);
                }

                _playlistService.SavePlaylists(Playlists.ToList());
            }
        }

        private Playlist CreateDefaultPlaylist()
        {
            var playlist = new Playlist { Name = "Моя музыка" };
            Playlists.Add(playlist);
            CurrentPlaylist = playlist;
            return playlist;
        }

        private void CreatePlaylist()
        {
            var name = "Новый плейлист " + (Playlists.Count + 1);
            var playlist = new Playlist { Name = name };
            Playlists.Add(playlist);
            CurrentPlaylist = playlist;
            _playlistService.SavePlaylists(Playlists.ToList());
        }

        private void DeletePlaylist(object parameter)
        {
            if (parameter is Playlist playlist)
            {
                if (Playlists.Contains(playlist))
                {
                    Playlists.Remove(playlist);
                    _playlistService.SavePlaylists(Playlists.ToList());
                }
            }
        }

        private void RemoveSong(object parameter)
        {
            if (parameter is Song song && CurrentPlaylist != null)
            {
                CurrentPlaylist.Songs.Remove(song);
                _playlistService.SavePlaylists(Playlists.ToList());
            }
        }

        private void PlaySong(object parameter)
        {
            if (parameter is Song song)
            {
                CurrentSong = song;
                Play();
            }
        }

        #endregion

        #region === Режимы воспроизведения ===

        private void ToggleRepeat()
        {
            if (IsRepeatOne)
            {
                IsRepeatOne = false;
                IsRepeatAll = true;
            }
            else if (IsRepeatAll)
            {
                IsRepeatAll = false;
            }
            else
            {
                IsRepeatOne = true;
            }
        }

        private void ToggleShuffle()
        {
            IsShuffle = !IsShuffle;
            if (IsShuffle && CurrentPlaylist != null)
            {
                var shuffled = CurrentPlaylist.Songs.OrderBy(x => Guid.NewGuid()).ToList();
                CurrentPlaylist.Songs.Clear();
                foreach (var song in shuffled)
                {
                    CurrentPlaylist.Songs.Add(song);
                }
            }
        }

        #endregion

        #region === Прогресс и таймер ===

        private void Seek(object parameter)
        {
            if (parameter is double position)
            {
                _audioPlayer.Seek(position);
            }
        }

        private void StartProgressTimer()
        {
            StopProgressTimer();
            _progressTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _progressTimer.Tick += (s, e) => UpdateProgress();
            _progressTimer.Start();
        }

        private void StopProgressTimer()
        {
            _progressTimer?.Stop();
            _progressTimer = null;
        }

        private void UpdateProgress()
        {
            var position = _audioPlayer.GetCurrentPosition();
            var total = _audioPlayer.GetTotalDuration();

            if (total > TimeSpan.Zero)
            {
                Progress = position.TotalSeconds / total.TotalSeconds;
                CurrentTime = FormatTime(position);
                TotalTime = FormatTime(total);
            }
        }

        private string FormatTime(TimeSpan time)
        {
            return $"{time.Minutes:D2}:{time.Seconds:D2}";
        }

        #endregion

        #region === Обработчики событий AudioPlayer ===

        private void OnPlaybackStopped(object sender, EventArgs e)
        {
            IsPlaying = false;
            StopProgressTimer();
        }

        private void OnPlaybackFinished(object sender, EventArgs e)
        {
            if (IsRepeatOne)
            {
                Play();
            }
            else
            {
                Next();
            }
        }

        #endregion
    }
}
