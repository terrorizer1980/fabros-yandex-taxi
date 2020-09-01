using System;
using System.Drawing;
using System.IO;
using System.Linq;
using MimeKit;
using Newtonsoft.Json;
using NReco.ImageGenerator;
using ImageFormat = System.Drawing.Imaging.ImageFormat;

namespace FabrosYandexTaxi
{
    internal class TaxiMessage
    {
        public static readonly string[] ValidStreetNames = {
            "praspiekt Pieramožcaŭ",
            "проспект Победителей"
        };

        private readonly string _bodyHtml;
        private readonly RootObject _rootObject;

        public TaxiMessage(MimeMessage message)
        {
            Date = message.Date;
            _bodyHtml = message.HtmlBody;
            _rootObject = LoadTaxiData(_bodyHtml);
        }

        public DateTimeOffset Date { get; }

        public double Cost => _rootObject.taxi.Sum(taxi => taxi.cost);

        public bool HasOfficeTaxi => _rootObject.taxi.Any(IsOfficeTaxi);

        public void GenerateImage(Stream stream)
        {
            var converter = new HtmlToImageConverter();
            if (Environment.OSVersion.Platform == PlatformID.Unix)
            {
                converter.WkHtmlToImageExeName = "wkhtmltoimage";
            }
            using var memoryStream = new MemoryStream();
            converter.GenerateImage(_bodyHtml, "png", memoryStream);
            memoryStream.Position = 0;
            using var image = Image.FromStream(memoryStream);
            image.Save(stream, ImageFormat.Png);
        }

        private static bool IsOfficeTaxi(Taxi taxi)
        {
            return IsOfficeAddress(taxi.arr) || IsOfficeAddress(taxi.dep);
        }

        private static bool IsOfficeAddress(string address)
        {
            var streetNumbers = new[] { "102", "104", "106", "108", "110" };
            return ValidStreetNames.Any(address.Contains) && streetNumbers.Any(address.Contains);
        }

        private static RootObject LoadTaxiData(string html)
        {
            const string targetLine = "<script type=\"application/ld+json\">";
            var lines = html.Split(
                new[] { Environment.NewLine },
                StringSplitOptions.None
            ).ToList();
            var index = lines.IndexOf(lines.Find(l => l.Contains(targetLine))) + 1;
            var json = lines[index].Trim();
            return JsonConvert.DeserializeObject<RootObject>(json);
        }
    }
}