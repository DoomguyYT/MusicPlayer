using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using TagLib;
using MusicPlayer.Models;

namespace MusicPlayer.Services
{
    public class MetadataService
    {
        public Song ReadMetadata(string filePath)
        {
            var song = new Song { FilePath = filePath };

            try
            {
                 var file = TagLib.File.Create(filePath);
                song.Title = string.IsNullOrEmpty(file.Tag.Title)
                    ? Path.GetFileNameWithoutExtension(filePath)
                    : file.Tag.Title;
                song.Artist = string.IsNullOrEmpty(file.Tag.FirstPerformer)
                    ? "Неизвестный исполнитель"
                    : file.Tag.FirstPerformer;
                song.Album = string.IsNullOrEmpty(file.Tag.Album)
                    ? "Неизвестный альбом"
                    : file.Tag.Album;
                song.Genre = file.Tag.FirstGenre ?? "Неизвестный жанр";
                song.Year = (int)(file.Tag.Year > 0 ? file.Tag.Year : 0);
                song.Duration = file.Properties.Duration.ToString(@"mm\:ss");

                // Чтение обложки
                if (file.Tag.Pictures.Length > 0)
                {
                    var picture = file.Tag.Pictures[0];
                    var imageData = picture.Data.Data;
                    song.CoverArt = Convert.ToBase64String(imageData);
                }
            }
            catch (Exception)
            {
                // Если не удалось прочитать теги, используем имя файла
                song.Title = Path.GetFileNameWithoutExtension(filePath);
                song.Artist = "Неизвестный исполнитель";
                song.Album = "Неизвестный альбом";
                song.Duration = "00:00";
            }

            return song;
        }
    }
}
