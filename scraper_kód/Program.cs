using KontejneryScraper.Scrapers;

namespace KontejneryScraper
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            MoravskaOstrava moravskaOstrava = new MoravskaOstrava();
            await moravskaOstrava.Scrape();
        }
    }
}
