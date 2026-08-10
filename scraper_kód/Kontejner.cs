using System;
using System.Collections.Generic;
using System.Text;

namespace KontejneryScraper
{
    public class Kontejner
    {
        public DateTime DateStart { get; set; }
        public DateTime DateEnd { get; set; }
        public string Street { get; set; }
        public double? Longitude { get; set; }
        public double? Latitude { get; set; }

        public Kontejner(DateTime dateStart, string street, double? longitude = null, double? latitude = null)
        {
            DateStart = dateStart;
            DateEnd = DateStart.AddDays(1);
            Street = street;
            Longitude = longitude;
            Latitude = latitude;
        }
    }
}
