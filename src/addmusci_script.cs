using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.IO;

public class CPHInline
{
    private static readonly HttpClient httpClient = new();
    private const string PORT = "26538";

    public bool Execute()
    {
        Task.Run(() => MonitorTrackLoop());
        return true;
    }

    private async Task MonitorTrackLoop()
    {
        string lastTrack = "";

        while (true)
        {
            try
            {
                var response = await httpClient.GetAsync($"http://localhost:{PORT}/api/v1/queue");
                var json = await response.Content.ReadAsStringAsync();
                var root = JObject.Parse(json);
                var items = root["items"] as JArray;

                if (items == null || items.Count == 0)
                {
                    SendTrackToWebSocket("⏹ Нет трека в очереди", "");
                    await Task.Delay(3000);
                    continue;
                }

                foreach (var item in items)
                {
                    var renderer = item["playlistPanelVideoRenderer"] as JObject;
                    if (renderer == null) continue;

                    bool isSelected = renderer["selected"]?.Value<bool>() ?? false;
                    if (isSelected)
                    {
                        string title = renderer["title"]?["runs"]?[0]?["text"]?.ToString() ?? "Без названия";
                        string artist = renderer["shortBylineText"]?["runs"]?[0]?["text"]?.ToString() ?? "Неизвестный";
                        string duration = renderer["lengthText"]?["runs"]?[0]?["text"]?.ToString() ?? "?:??";
                        string thumbnail = renderer["thumbnail"]?["thumbnails"]?[0]?["url"]?.ToString() ?? "";

                        string fullTrack = $"🎵 {title} — {artist} [{duration}]";

                        if (fullTrack != lastTrack)
                        {
                            SendTrackToWebSocket(fullTrack, thumbnail);
                            CPH.LogInfo($"🔄 Обновлён трек: {fullTrack}");
                            lastTrack = fullTrack;
                        }

                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                SendTrackToWebSocket("⚠ Ошибка загрузки трека", "");
                CPH.LogError($"Ошибка при обновлении трека: {ex.Message}");
            }

            await Task.Delay(3000); // каждые 3 секунды
        }
    }

    private void SendTrackToWebSocket(string track, string thumbnail)
    {
        var trackData = new
        {
            track = track,
            thumbnail = thumbnail
        };
        string json = JsonConvert.SerializeObject(trackData);
        CPH.LogInfo($"Отправлено в WebSocket: {json}");
        CPH.WebsocketBroadcastJson(json); // Отправка JSON всем подключенным WebSocket-клиентам
    }
}