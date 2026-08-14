using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;
using MusicPlayer.Models;

namespace MusicPlayer.Services
{
    public class PlaylistService
    {
        private readonly string _playlistsPath = "playlists.json";

        public List<Playlist> LoadPlaylists()
        {
            if (!File.Exists(_playlistsPath))
            {
                return new List<Playlist>
                {
                    new Playlist { Name = "Избранное" }
                };
            }

            try
            {
                var json = File.ReadAllText(_playlistsPath);
                return JsonConvert.DeserializeObject<List<Playlist>>(json) ?? new List<Playlist>();
            }
            catch
            {
                return new List<Playlist>();
            }
        }

        public void SavePlaylists(List<Playlist> playlists)
        {
            try
            {
                var json = JsonConvert.SerializeObject(playlists, new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented
                });
                File.WriteAllText(_playlistsPath, json);
            }
            catch
            {
                // Обработка ошибок
            }
        }
    }
}
