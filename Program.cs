using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Newtonsoft.Json;
using RestSharp;

namespace NewlyReadAPI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var sourcesTimer = new System.Threading.Timer((e) =>
            {
                NewlyReadAPI.Controllers.v1Controller.setSources();
            }, null, TimeSpan.Zero, TimeSpan.FromMinutes(20));

            var host = new WebHostBuilder()
                .UseKestrel()
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseIISIntegration()
                .UseStartup<Startup>()
                .Build();

            host.Run();
        }
    }
}
