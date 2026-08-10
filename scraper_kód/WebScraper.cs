using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace KontejneryScraper
{
    internal class WebScraper
    {
        private readonly HttpClient _httpClient;
        private readonly CookieContainer _cookieContainer;

        public WebScraper()
        {
            _cookieContainer = new CookieContainer();
            var handler = new HttpClientHandler { CookieContainer = _cookieContainer };
            _httpClient = new HttpClient(handler);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("MyKontejnerScraper/1.0 (contact: amos.bartos@gmail.com)");
        }

        public void AddCookie(string name, string value, string domain)
        {
            _cookieContainer.Add(new Uri($"https://{domain}"), new Cookie(name, value));
        }

        public void AddHeader(string name, string value)
        {
            _httpClient.DefaultRequestHeaders.Add(name, value);
        }

        public async Task<string> GetHtmlAsync(string url, int maxRetries = 3, int delayMs = 1000)
        {
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    var response = await _httpClient.GetAsync(url);
                    response.EnsureSuccessStatusCode();
                    return await response.Content.ReadAsStringAsync();
                }
                catch (HttpRequestException e)
                {
                    Console.WriteLine($"Attempt {i + 1} failed: {e.Message}");
                    if (i == maxRetries - 1) throw;
                    await Task.Delay(delayMs);
                }
            }
            return null; // This line should never be reached due to the throw in the catch block
        }
        public async Task<string> GetLargeHtmlAsync(string url)
        {
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync();
        }

        public HtmlDocument ParseHtml(string html)
        {
            var htmlDocument = new HtmlDocument();
            htmlDocument.LoadHtml(html);
            return htmlDocument;
        }

        public List<string> ExtractTextWithXPath(HtmlDocument document, string xpathQuery)
        {
            var nodes = document.DocumentNode.SelectNodes(xpathQuery);
            return nodes?.Select(node => node.InnerText.Trim()).ToList() ?? new List<string>();
        }

        public List<HtmlNode> GetChildNodes(HtmlDocument document, string xpathQuery)
        {
            var parentNode = document.DocumentNode.SelectSingleNode(xpathQuery);
            return parentNode?.ChildNodes.Where(n => n.NodeType == HtmlNodeType.Element).ToList() ?? new List<HtmlNode>();
        }

        public List<string> ExtractAttributeValues(HtmlDocument document, string xpathQuery, string attributeName)
        {
            var nodes = document.DocumentNode.SelectNodes(xpathQuery);
            return nodes?.Select(node => node.GetAttributeValue(attributeName, string.Empty)).Where(attr => !string.IsNullOrEmpty(attr)).ToList() ?? new List<string>();
        }

        public async Task DownloadPdfAsync(string pdfUrl, string outputPath)
        {
            var bytes = await _httpClient.GetByteArrayAsync(pdfUrl);
            await File.WriteAllBytesAsync(outputPath, bytes);
        }

        public async Task<(double lat, double lon)?> GetCoordinates(string address)
        {
            string url = $"https://nominatim.openstreetmap.org/search?street={Uri.EscapeDataString(address)}&city=Ostrava&countrycodes=cz&format=json&limit=1";

            for (int attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    var response = await _httpClient.GetAsync(url);

                    if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                    {
                        Console.WriteLine($"forbidden {attempt + 1}");
                        await Task.Delay(2000 * (attempt + 1)); // backoff
                        continue;
                    }

                    response.EnsureSuccessStatusCode();

                    var jsonText = await response.Content.ReadAsStringAsync();
                    var json = JsonDocument.Parse(jsonText).RootElement;

                    if (json.GetArrayLength() == 0)
                        return null;

                    double lat = double.Parse(json[0].GetProperty("lat").GetString());
                    double lon = double.Parse(json[0].GetProperty("lon").GetString());

                    return (lat, lon);
                }
                catch (HttpRequestException)
                {
                    Console.WriteLine($"exception {attempt + 1}");
                    await Task.Delay(2000 * (attempt + 1));
                }
            }

            return null;
        }
    }
}

