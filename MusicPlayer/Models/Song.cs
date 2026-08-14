using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MusicPlayer.Models
{
    public class Song : INotifyPropertyChanged
    {
        private string _title = string.Empty;
        private string _artist = string.Empty;
        private string _album = string.Empty;
        private string _filePath = string.Empty;
        private string _duration = string.Empty;
        private string _genre = string.Empty;
        private int _year;
        private string _coverArt = string.Empty;

        public string Title
        {
            get => _title;
            set { _title = value; OnPropertyChanged(nameof(Title)); }
        }

        public string Artist
        {
            get => _artist;
            set { _artist = value; OnPropertyChanged(nameof(Artist)); }
        }

        public string Album
        {
            get => _album;
            set { _album = value; OnPropertyChanged(nameof(Album)); }
        }

        public string FilePath
        {
            get => _filePath;
            set { _filePath = value; OnPropertyChanged(nameof(FilePath)); }
        }

        public string Duration
        {
            get => _duration;
            set { _duration = value; OnPropertyChanged(nameof(Duration)); }
        }

        public string Genre
        {
            get => _genre;
            set { _genre = value; OnPropertyChanged(nameof(Genre)); }
        }

        public int Year
        {
            get => _year;
            set { _year = value; OnPropertyChanged(nameof(Year)); }
        }

        public string CoverArt
        {
            get => _coverArt;
            set { _coverArt = value; OnPropertyChanged(nameof(CoverArt)); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public override string ToString()
        {
            return $"{Artist} - {Title}";
        }
    }
}
