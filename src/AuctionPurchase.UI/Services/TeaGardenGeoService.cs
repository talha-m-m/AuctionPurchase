using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using AuctionPurchase.Core.Models;

namespace AuctionPurchase.UI.Services
{
    public class GardenGeoLocation
    {
        public int GardenId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string District { get; set; } = string.Empty;
        public string Division { get; set; } = string.Empty;
        public string? MapLink { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public bool IsExactFromLink { get; set; }
        public string RegionGroup { get; set; } = "Other";
    }

    public static class TeaGardenGeoService
    {
        // Bangladesh Tea Hub regional centroids
        // Sreemangal / Moulvibazar (Primary hub, ~90+ tea gardens)
        private static readonly (double Lat, double Lon) SreemangalCoord = (24.3065, 91.7296);
        private static readonly (double Lat, double Lon) KamalganjCoord = (24.3644, 91.8647);
        private static readonly (double Lat, double Lon) KulauraCoord = (24.5167, 92.0333);
        private static readonly (double Lat, double Lon) BarlekhaCoord = (24.7000, 92.2000);
        private static readonly (double Lat, double Lon) JuriCoord = (24.5950, 92.1120);
        private static readonly (double Lat, double Lon) RajnagarCoord = (24.5333, 91.8667);
        private static readonly (double Lat, double Lon) MoulvibazarSadarCoord = (24.4826, 91.7774);

        // Habiganj Hub (~25+ gardens)
        private static readonly (double Lat, double Lon) ChunarughatCoord = (24.2167, 91.5000);
        private static readonly (double Lat, double Lon) BahubalCoord = (24.3542, 91.5433);
        private static readonly (double Lat, double Lon) MadhabpurCoord = (24.1000, 91.3000);
        private static readonly (double Lat, double Lon) NabiganjCoord = (24.5667, 91.5167);

        // Sylhet Hub (~20+ gardens)
        private static readonly (double Lat, double Lon) SylhetSadarCoord = (24.9100, 91.8700);
        private static readonly (double Lat, double Lon) JaintiapurCoord = (25.1333, 92.1167);
        private static readonly (double Lat, double Lon) GowainghatCoord = (25.1667, 92.0167);
        private static readonly (double Lat, double Lon) KanaighatCoord = (25.0000, 92.2667);
        private static readonly (double Lat, double Lon) FenchuganjCoord = (24.7000, 91.9500);

        // Chittagong Hub (~22+ gardens)
        private static readonly (double Lat, double Lon) FatikchhariCoord = (22.6924, 91.7854);
        private static readonly (double Lat, double Lon) RanguniaCoord = (22.4667, 92.0500);
        private static readonly (double Lat, double Lon) BanshkhaliCoord = (22.0333, 91.9500);
        private static readonly (double Lat, double Lon) ChittagongSadarCoord = (22.3475, 91.8123);

        // North Bengal / Panchagarh Hub (~10+ gardens)
        private static readonly (double Lat, double Lon) TetuliaCoord = (26.4912, 88.3582);
        private static readonly (double Lat, double Lon) PanchagarhSadarCoord = (26.3354, 88.5517);
        private static readonly (double Lat, double Lon) AtwariCoord = (26.3167, 88.4000);
        private static readonly (double Lat, double Lon) ThakurgaonCoord = (26.0336, 88.4617);

        // Hill Tracts / Rangamati Hub
        private static readonly (double Lat, double Lon) KaptaiCoord = (22.5000, 92.2167);
        private static readonly (double Lat, double Lon) BaghaichhariCoord = (23.1500, 92.2000);

        // Regex patterns for MapLink parsing
        private static readonly Regex LatLngRegex = new Regex(
            @"(-?\d{1,2}\.\d+)\s*,\s*(-?\d{1,3}\.\d+)",
            RegexOptions.Compiled);

        private static readonly Regex GoogleMapsAtRegex = new Regex(
            @"@(-?\d{1,2}\.\d+),(-?\d{1,3}\.\d+)",
            RegexOptions.Compiled);

        private static readonly Regex GoogleMapsQueryRegex = new Regex(
            @"[?&]q=(-?\d{1,2}\.\d+),(-?\d{1,3}\.\d+)",
            RegexOptions.Compiled);

        public static GardenGeoLocation ResolveLocation(Garden garden)
        {
            var loc = new GardenGeoLocation
            {
                GardenId = garden.GardenId,
                Name = garden.Name,
                FullName = garden.FullName ?? garden.Name,
                District = garden.District ?? "",
                Division = garden.Division ?? "",
                MapLink = garden.MapLink
            };

            // 1. Try parsing exact coordinates from MapLink if present
            if (!string.IsNullOrWhiteSpace(garden.MapLink))
            {
                var coord = TryParseCoordinates(garden.MapLink);
                if (coord.HasValue)
                {
                    loc.Latitude = coord.Value.Lat;
                    loc.Longitude = coord.Value.Lon;
                    loc.IsExactFromLink = true;
                    loc.RegionGroup = DetermineRegionGroup(loc.District, loc.Latitude, loc.Longitude);
                    return loc;
                }
            }

            // 2. Fall back to regional geographic centroid with deterministic spread
            var (baseLat, baseLon, regionGroup) = GetBaseCoordinates(garden);
            
            // Add deterministic slight offset based on GardenId so gardens in the same town don't overlap completely
            int seed = garden.GardenId * 37 + 11;
            double offsetX = ((seed % 17) - 8) * 0.009; // approx ~1km
            double offsetY = (((seed / 17) % 17) - 8) * 0.009;

            loc.Latitude = baseLat + offsetY;
            loc.Longitude = baseLon + offsetX;
            loc.IsExactFromLink = false;
            loc.RegionGroup = regionGroup;

            return loc;
        }

        public static (double Lat, double Lon)? TryParseCoordinates(string link)
        {
            if (string.IsNullOrWhiteSpace(link)) return null;

            // Check @lat,lon
            var match = GoogleMapsAtRegex.Match(link);
            if (match.Success &&
                double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double atLat) &&
                double.TryParse(match.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double atLon))
            {
                if (IsValidBangladeshLatLon(atLat, atLon)) return (atLat, atLon);
            }

            // Check ?q=lat,lon
            match = GoogleMapsQueryRegex.Match(link);
            if (match.Success &&
                double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double qLat) &&
                double.TryParse(match.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double qLon))
            {
                if (IsValidBangladeshLatLon(qLat, qLon)) return (qLat, qLon);
            }

            // Generic lat,lon
            match = LatLngRegex.Match(link);
            if (match.Success &&
                double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double gLat) &&
                double.TryParse(match.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double gLon))
            {
                if (IsValidBangladeshLatLon(gLat, gLon)) return (gLat, gLon);
            }

            return null;
        }

        private static bool IsValidBangladeshLatLon(double lat, double lon)
        {
            // Bangladesh latitude: approx 20.5 to 26.8, longitude: approx 88.0 to 92.8
            return lat >= 20.0 && lat <= 27.5 && lon >= 87.5 && lon <= 93.0;
        }

        private static (double Lat, double Lon, string Region) GetBaseCoordinates(Garden garden)
        {
            var text = (garden.District + " " + garden.Address + " " + garden.Name).ToLowerInvariant();

            // Sreemangal / Kamalganj / Kulaura (Moulvibazar)
            if (text.Contains("শ্রীমঙ্গল") || text.Contains("sreemangal"))
                return (SreemangalCoord.Lat, SreemangalCoord.Lon, "Moulvibazar / Sreemangal");
            if (text.Contains("কমলগঞ্জ") || text.Contains("kamalganj") || text.Contains("শমশেরনগর") || text.Contains("shamshernagar") || text.Contains("পাত্রখোলা"))
                return (KamalganjCoord.Lat, KamalganjCoord.Lon, "Moulvibazar / Kamalganj");
            if (text.Contains("কুলাউড়া") || text.Contains("kulaura") || text.Contains("লংলা") || text.Contains("রাঙ্গিরকুল"))
                return (KulauraCoord.Lat, KulauraCoord.Lon, "Moulvibazar / Kulaura");
            if (text.Contains("বড়লেখা") || text.Contains("barlekha") || text.Contains("দক্ষিণবাগ"))
                return (BarlekhaCoord.Lat, BarlekhaCoord.Lon, "Moulvibazar / Barlekha");
            if (text.Contains("জুরী") || text.Contains("juri"))
                return (JuriCoord.Lat, JuriCoord.Lon, "Moulvibazar / Juri");
            if (text.Contains("রাজনগর") || text.Contains("rajnagar"))
                return (RajnagarCoord.Lat, RajnagarCoord.Lon, "Moulvibazar / Rajnagar");
            if (text.Contains("মৌলভীবাজার") || text.Contains("moulvibazar") || text.Contains("moulvi"))
                return (MoulvibazarSadarCoord.Lat, MoulvibazarSadarCoord.Lon, "Moulvibazar");

            // Habiganj
            if (text.Contains("চুনারুঘাট") || text.Contains("chunarughat") || text.Contains("চাঁদপুর"))
                return (ChunarughatCoord.Lat, ChunarughatCoord.Lon, "Habiganj / Chunarughat");
            if (text.Contains("বাহুবল") || text.Contains("bahubal") || text.Contains("রশিদপুর"))
                return (BahubalCoord.Lat, BahubalCoord.Lon, "Habiganj / Bahubal");
            if (text.Contains("মাধবপুর") || text.Contains("madhabpur") || text.Contains("ইটাখোলা") || text.Contains("তেলিয়াপাড়া"))
                return (MadhabpurCoord.Lat, MadhabpurCoord.Lon, "Habiganj / Madhabpur");
            if (text.Contains("নবীগঞ্জ") || text.Contains("nabiganj"))
                return (NabiganjCoord.Lat, NabiganjCoord.Lon, "Habiganj");
            if (text.Contains("হবিগঞ্জ") || text.Contains("habiganj"))
                return (ChunarughatCoord.Lat, ChunarughatCoord.Lon, "Habiganj");

            // Sylhet
            if (text.Contains("জৈন্তাপুর") || text.Contains("jaintiapur") || text.Contains("লালাখাল") || text.Contains("শ্রীপুর"))
                return (JaintiapurCoord.Lat, JaintiapurCoord.Lon, "Sylhet / Jaintiapur");
            if (text.Contains("গোয়াইনঘাট") || text.Contains("gowainghat") || text.Contains("জাফলং") || text.Contains("jaflong"))
                return (GowainghatCoord.Lat, GowainghatCoord.Lon, "Sylhet / Jaflong");
            if (text.Contains("কানাইঘাট") || text.Contains("kanaighat") || text.Contains("লোভাছড়া"))
                return (KanaighatCoord.Lat, KanaighatCoord.Lon, "Sylhet / Kanaighat");
            if (text.Contains("ফেঞ্চুগঞ্জ") || text.Contains("fenchuganj"))
                return (FenchuganjCoord.Lat, FenchuganjCoord.Lon, "Sylhet / Fenchuganj");
            if (text.Contains("সিলেট") || text.Contains("sylhet") || text.Contains("লাক্কাতুরা") || text.Contains("মালনীছড়া") || text.Contains("খাদিম"))
                return (SylhetSadarCoord.Lat, SylhetSadarCoord.Lon, "Sylhet");

            // Chittagong
            if (text.Contains("ফটিকছড়ি") || text.Contains("fatikchhari") || text.Contains("নাজিরহাট") || text.Contains("দাঁতমারা") || text.Contains("ভূঁজপুর"))
                return (FatikchhariCoord.Lat, FatikchhariCoord.Lon, "Chittagong / Fatikchhari");
            if (text.Contains("রাঙ্গুনিয়া") || text.Contains("rangunia") || text.Contains("কোদালা") || text.Contains("ঠান্ডাছড়ি"))
                return (RanguniaCoord.Lat, RanguniaCoord.Lon, "Chittagong / Rangunia");
            if (text.Contains("বাঁশখালী") || text.Contains("banshkhali"))
                return (BanshkhaliCoord.Lat, BanshkhaliCoord.Lon, "Chittagong / Banshkhali");
            if (text.Contains("চট্টগ্রাম") || text.Contains("chittagong") || text.Contains("chattogram"))
                return (ChittagongSadarCoord.Lat, ChittagongSadarCoord.Lon, "Chittagong");

            // Panchagarh / North Bengal
            if (text.Contains("তেতুলিয়া") || text.Contains("tetulia") || text.Contains("বুড়াবুড়ি") || text.Contains("শালবাহান"))
                return (TetuliaCoord.Lat, TetuliaCoord.Lon, "Panchagarh / Tetulia");
            if (text.Contains("আটোয়ারী") || text.Contains("atwari"))
                return (AtwariCoord.Lat, AtwariCoord.Lon, "Panchagarh / Atwari");
            if (text.Contains("পঞ্চগড়") || text.Contains("panchagarh"))
                return (PanchagarhSadarCoord.Lat, PanchagarhSadarCoord.Lon, "Panchagarh");
            if (text.Contains("ঠাকুরগাঁও") || text.Contains("thakurgaon"))
                return (ThakurgaonCoord.Lat, ThakurgaonCoord.Lon, "Thakurgaon");

            // Rangamati
            if (text.Contains("বাঘাইছড়ি") || text.Contains("baghaichhari"))
                return (BaghaichhariCoord.Lat, BaghaichhariCoord.Lon, "Rangamati / Baghaichhari");
            if (text.Contains("কাপ্তাই") || text.Contains("kaptai") || text.Contains("রাঙ্গামাটি") || text.Contains("rangamati"))
                return (KaptaiCoord.Lat, KaptaiCoord.Lon, "Rangamati");

            // Default to central tea capital Sreemangal
            return (SreemangalCoord.Lat, SreemangalCoord.Lon, "Moulvibazar / Sreemangal");
        }

        private static string DetermineRegionGroup(string district, double lat, double lon)
        {
            if (district.Contains("মৌলভীবাজার") || district.ToLowerInvariant().Contains("moulvibazar"))
                return "Moulvibazar Region";
            if (district.Contains("হবিগঞ্জ") || district.ToLowerInvariant().Contains("habiganj"))
                return "Habiganj Region";
            if (district.Contains("সিলেট") || district.ToLowerInvariant().Contains("sylhet"))
                return "Sylhet Region";
            if (district.Contains("চট্টগ্রাম") || district.ToLowerInvariant().Contains("chittagong"))
                return "Chittagong Region";
            if (district.Contains("পঞ্চগড়") || district.ToLowerInvariant().Contains("panchagarh"))
                return "Panchagarh Region";
            if (district.Contains("রাঙ্গামাটি") || district.ToLowerInvariant().Contains("rangamati"))
                return "Rangamati Region";

            if (lat > 25.5) return "Northern Tea Region";
            if (lat < 23.0) return "Chittagong Tea Region";
            return "Surma Valley (Sylhet) Region";
        }
    }
}
