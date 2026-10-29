using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace GVK.Koth.Services
{
    /// <summary>
    /// Posts plain messages to a Discord webhook. Runs on a background task; never touches game objects.
    /// </summary>
    public static class Discord
    {
        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

        public static void Post(string webhookUrl, string message)
        {
            if (string.IsNullOrWhiteSpace(webhookUrl) || string.IsNullOrWhiteSpace(message))
                return;

            // Discord rejects content over 2,000 characters.
            if (message.Length > 1990)
                message = message.Substring(0, 1990) + "...";

            string json = "{\"content\":\"" + Escape(message) + "\",\"allowed_mentions\":{\"parse\":[\"roles\"]}}";
            Task.Run(async () =>
            {
                try
                {
                    using (var body = new StringContent(json, Encoding.UTF8, "application/json"))
                    using (var response = await Http.PostAsync(webhookUrl, body).ConfigureAwait(false))
                    {
                        if (!response.IsSuccessStatusCode)
                            Plugin.Log.Warn($"GVK KOTH: Discord webhook returned {(int)response.StatusCode}.");
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Log.Warn(ex, "GVK KOTH: Discord webhook failed.");
                }
            });
        }

        private static string Escape(string text)
        {
            var sb = new StringBuilder(text.Length + 16);
            foreach (char c in text)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }
    }
}
