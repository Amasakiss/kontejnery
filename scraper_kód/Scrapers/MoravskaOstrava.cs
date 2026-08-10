using AngleSharp.Common;
using AngleSharp.Dom;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;
using static System.Runtime.InteropServices.JavaScript.JSType;
using static UglyToad.PdfPig.Core.PdfSubpath;

namespace KontejneryScraper.Scrapers
{
    internal class MoravskaOstrava
    {
        private readonly string url = "https://moap.ostrava.cz/cs/obcan/sberne-dvory/vk";

        private readonly string jsonName = "MOaP.json";

        public List<Kontejner>? Kontejnery { get; set; }

        WebScraper scraper = new WebScraper();


        public async Task Scrape()
        {
            /*
            var html = await scraper.GetHtmlAsync(url);
            HtmlDocument document = scraper.ParseHtml(html);

            //find all the internal links that are in the div content-main__content (měl by tam být snad právě jeden a to ten co ukazuje na ten pdf dokument s umistenim kontejneru)
            var nodes = document.DocumentNode.SelectNodes("//div[contains(@class,'content-main__content')]//*[contains(@class,'internal-link')]");

            if (nodes == null)
            {
                Console.WriteLine("No nodes found.");
                return;
            }
            string href;
            foreach (var node in nodes)
            {
                try
                {
                    href = node.GetAttributeValue("href", "");

                    if (!href.Contains("srpen2026.pdf")) continue;

                    Uri pdfUri = new Uri(new Uri(url), href);

                    await scraper.DownloadPdfAsync(pdfUri.ToString(), Path.GetFileName(pdfUri.LocalPath));

                    Console.WriteLine($"Downladed: {href}");

                    await Task.Delay(500);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
            */
            //pak odstranit
            string href = "srpen 2026.pdf";

            var Kontejnery = await ProcessPdf(href);

            foreach (Kontejner kontejner in Kontejnery)
            {
                Console.WriteLine
                (
                    "DateStart: " +
                    kontejner.DateStart + "\n" +
                    "DateEnd " +
                    kontejner.DateEnd + "\n" +
                    "Street " +
                    kontejner.Street + "\n" +
                    "Longitude " +
                    kontejner.Longitude ?? "NULL" + "\n" +
                    "Latitude " +
                    kontejner.Latitude ?? "NULL" +
                    "\n" + "\n"
                );
            }

            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            string json = JsonSerializer.Serialize(Kontejnery, options);

            await File.WriteAllTextAsync("kontejneryMOaP.json", json);
        }

        public async Task<List<Kontejner>>? ProcessPdf(string filePath)
        {
            DateTime? date = null;

            List<Kontejner>? kontejners = [];

            List<string> streets = [];

            StringBuilder sb = new StringBuilder();

            using (PdfDocument document = PdfDocument.Open(filePath))
            {
                foreach (Page page in document.GetPages())
                {
                    string text = ContentOrderTextExtractor.GetText(page);

                    string[] splitText = text.Split(new string[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);


                    foreach (var item in splitText.Select((value, index) => (value, index)))
                    {

                        //přeskakujeme poslední line, protože už tam nejsou adresy, tohle asi není moc dlouhodobé řešení, ale nedaří se mi to zgeneralizovat :/
                        if (item.index >= splitText.Length - 6)
                            continue;

                        string line = item.value.Trim();
                      
                        //if the word is NUMBERS DOT NUMBERS or NUMBERS DOT NUMBERS DOT NUMBERS aka dates
                        if (Regex.IsMatch(line.ToString(), @"^\d+\.\d+\.$") || Regex.IsMatch(line.ToString(), @"^\d+\.\d+\.\d+\.$"))
                        {
                            //tady to zpracuju ještě před změnou data
                            streets = ProcessStreets(sb.ToString());
                            sb.Clear();
                        
                            //přeměň všechny adresy na Kontejnery a přidej do kontejners
                            foreach (string street in streets)
                            {
                                
                                //date becomes 1 jan 1970 if null!!!!
                                Kontejner? kontejner = await ConvertStreetToKontejner(street, date);

                                if (kontejner is not null)
                                {
                                    kontejners.Add(kontejner);
                                }

                                await Task.Delay(1001); //wait 1 sec so that we dont get timed out
                            }

                            string[] partsOfDate = line.Split(".");

                            date = new DateTime(DateTime.Now.Year, int.Parse(partsOfDate[1]), int.Parse(partsOfDate[0]), 7, 0, 0);

                            /*
                            //předpokládám že to stáhnu jednou a pak tento proces budu dělat až když se ta stranka zmeni, jinak by tohle rozbilo čas
                            if (date < DateTime.Now)
                            {
                                date = date.Value.AddYears(1);
                            }*/

                            continue;
                        }

                        if (date.HasValue)
                        {
                            sb.Append(" ");
                            sb.Append(line);
                        }
                    }

                    //tady musim zpracovat ještě posledni varku
                    streets = ProcessStreets(sb.ToString());
                    sb.Clear();

                    foreach (string street in streets)
                    {

                        //date becomes 1 jan 1970 if null!!!!
                        Kontejner? kontejner = await ConvertStreetToKontejner(street, date);

                        if (kontejner is not null)
                        {
                            kontejners.Add(kontejner);
                        }

                        await Task.Delay(1001); //wait 1 sec so that we dont get timed out
                    }
                }
            }

            return kontejners;
        }
        public List<string> ProcessStreets(string bunchOfStreets)
        {
            string[] streetsSplit = bunchOfStreets.Split(",");

            List<string> result = [];

            string currentStreet = "";

            foreach (string street in streetsSplit)
            {
                //jestlize se nekde v ulici nachazi pismeno, je to validni ulice
                if (street.Any(char.IsLetter))
                {
                    currentStreet = street;
                }
                else //jestlize ne, je to dalsi cislo na te same ulici, musim odendat cislo a nahradit ho tim novym
                {
                    currentStreet = Regex.Split(currentStreet, @"\d+")[0] + " " + street;
                }

                //remove multiple whitespaces
                currentStreet = string.Join(" ", currentStreet.Split(new char[0], StringSplitOptions.RemoveEmptyEntries));

                //remove everything after (  (street 5 (6))
                currentStreet = currentStreet.Split('(')[0].Trim();

                //remove everything after dashes (street number 17 - 19) 
                currentStreet = currentStreet.Replace('–', '-'); // en dash → normal dash
                currentStreet = currentStreet.Replace('—', '-'); // em dash → normal dash
                currentStreet = currentStreet.Split('-')[0].Trim();
                currentStreet = currentStreet.Split('-')[0].Trim();

                if (currentStreet.EndsWith("."))
                {
                    currentStreet = currentStreet.Substring(0, currentStreet.Length - 1);
                }

                result.Add(currentStreet);
            }

            return result;
        }
        public async Task<Kontejner>? ConvertStreetToKontejner(string street, DateTime? date)
        {

            Console.WriteLine($"zpracovávám: {street} {date}");
            if (string.IsNullOrEmpty(street) || !date.HasValue)
            {
                //throw error
                return null;
            }

            var coords = await scraper.GetCoordinates(street);
            double? latitude = null;
            double? longitude = null;

            if (coords.HasValue)
            {
                latitude = coords.Value.lat;
                longitude = coords.Value.lon;
            }
            else
            {
                Console.WriteLine($"Address not found ({street})");
            }

            return new Kontejner(date ?? new DateTime(), street, longitude, latitude);
        }
    }
}

/* Webscraper kod vychazi z tohohle tutorialu https://scrape.do/blog/c-sharp-web-scraping/ */

/*
 * TODO:
 * 
 * změnit public List<Kontejner>? Kontejnery { get; set; } na vlastni classu, která bude mít metodu add(Kontejner) ale hlavne add(street, date), která bude prakticky dělat toto                        
 * Kontejner? kontejner = ConvertStreetToKontejner(street, date).Result;
    if(kontejner is not null)
    {
        kontejners.Add(kontejner);
    }
 * 
 * zpracovat adrsy na long lat
 * prevest vsechno na Kontejner
 * prevest vysledny Kontejner na Json
 * 
 * error handling
 * 
 * Udelat to aby se to delo kazdy den ()github actions
 * ale zaroven aby to zpracovavalo to pdf jen pokud se zmeni ta stranka/pdfko
 * 
 * poslat na telegram zpravy co se stalo, pripadne jak co udelat, kontrola, pri kazdem scrapingu
 * 
 * udelat na mape timeline, vpred a pripadne i vzad s tim ze se to bude dat posouvat a podle toho se budou ukazovat kde jsou kontejnery
 * 
 * pridelat moznost pridat ke konkretnimu kontejneru fotku 
 * 
 * prokonzultovat to s mestem (zaprve jestli by to nejak nechteli koupit me sluzby nebo neco takoveho a pokud ne, tak se alespon pokusit je presvedcit o sjednoceni dat)
 * 
 * zautomatizovat i dalsi casti Ostravy
 * 
 *  
 * 
 * 
 * 
 * later to support BOTH addresses + intersections, Use TWO datasets:
 * 
 * Dataset B — street graph (for intersections)

This downloads all named streets with full geometry:

[out:json][timeout:180];

area["name"="Ostrava"]["boundary"="administrative"]->.ostrava;

way(area.ostrava)["highway"]["name"];

out geom;


This gives you:

every street polyline

every coordinate along it
compute the cross sections. manually

 * 
 * 
 * 
 * 
 * 
 */