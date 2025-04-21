using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LuxRunner
{
    public static class Configuration
    {
        static IConfigurationRoot _config;
        public static double SapDrop05Threshold { get; }
        public static double SapDrop025Threshold { get; }
        public static double SapDrop10Threshold { get; }
        public static double SapDropUnknown { get; }
        static Configuration()
        {
            _config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            SapDrop05Threshold = double.Parse(_config["AppSettings:SapDrop05Threshols"],CultureInfo.InvariantCulture);
            SapDrop025Threshold = double.Parse(_config["AppSettings:SapDrop025Threshols"], CultureInfo.InvariantCulture);
            SapDrop10Threshold = double.Parse(_config["AppSettings:SapDrop10Threshols"], CultureInfo.InvariantCulture);
            SapDropUnknown = double.Parse(_config["AppSettings:SapDropUnknown"], CultureInfo.InvariantCulture);

        }
    }
}
