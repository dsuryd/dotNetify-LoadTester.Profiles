using System.IO;
using System.Threading.Tasks;
using DotNetify.LoadTester;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace TestRunner
{
   internal class Program
   {
      private static async Task Main(string[] args)
      {
         IConfigurationRoot configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .Build();

         ILoggerFactory loggerFactory = LoggerFactory.Create(configure => configure.AddConfiguration(configuration.GetSection("Logging")).AddConsole());

         await LoadTestRunner.RunAsync(args, loggerFactory);
      }
   }
}