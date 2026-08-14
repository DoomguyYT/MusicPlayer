using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace MusicPlayer.Services
{
    public class AudioPlayerService : IDisposable
    {
        private WaveOutEvent _outputDevice;
        private AudioFileReader _audioFile;
        private bool _isPlaying;

        public event EventHandler PlaybackStopped;
        public event EventHandler PlaybackFinished;

        public void Play(string filePath)
        {
            try
            {
                Stop();

                _audioFile = new AudioFileReader(filePath);
                _outputDevice = new WaveOutEvent();
                _outputDevice.PlaybackStopped += OnPlaybackStopped;
                _outputDevice.Init(_audioFile);
                _outputDevice.Play();
                _isPlaying = true;
            }
            catch (Exception)
            {
                // Обработка ошибок
            }
        }

        public void Pause()
        {
            if (_outputDevice != null && _isPlaying)
            {
                _outputDevice.Pause();
                _isPlaying = false;
            }
        }

        public void Resume()
        {
            if (_outputDevice != null && !_isPlaying && _audioFile != null)
            {
                _outputDevice.Play();
                _isPlaying = true;
            }
        }

        public void Stop()
        {
            _outputDevice?.Stop();
            _outputDevice?.Dispose();
            _outputDevice = null;
            _audioFile?.Dispose();
            _audioFile = null;
            _isPlaying = false;
        }

        public void SetVolume(double volume)
        {
            if (_audioFile != null)
            {
                _audioFile.Volume = (float)volume;
            }
        }

        public void Seek(double progress)
        {
            if (_audioFile != null && _outputDevice != null)
            {
                var position = (long)(progress * _audioFile.Length);
                _audioFile.Position = position;
            }
        }

        public TimeSpan GetCurrentPosition()
        {
            return _audioFile?.CurrentTime ?? TimeSpan.Zero;
        }

        public TimeSpan GetTotalDuration()
        {
            return _audioFile?.TotalTime ?? TimeSpan.Zero;
        }

        private void OnPlaybackStopped(object sender, StoppedEventArgs e)
        {
            _isPlaying = false;
            PlaybackStopped?.Invoke(this, EventArgs.Empty);

            if (_audioFile != null && _audioFile.Position >= _audioFile.Length)
            {
                PlaybackFinished?.Invoke(this, EventArgs.Empty);
            }
        }

        public void Dispose()
        {
            Stop();
            _outputDevice?.Dispose();
            _audioFile?.Dispose();
        }
    }
}
