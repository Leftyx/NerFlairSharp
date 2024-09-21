using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

namespace NerFlairSharp;

internal class Program
{
   static async Task Main(string[] args)
   {
      using var cancellationTokenSource = new CancellationTokenSource();
      var cancellationToken = cancellationTokenSource.Token;

      var builder = Host.CreateApplicationBuilder(args);

      builder.Logging.ClearProviders();

      builder.Services.AddSerilog(config =>
      {
         config.ReadFrom.Configuration(builder.Configuration);
      });

      if (args is { Length: > 0 })
      {
         builder.Configuration.AddCommandLine(args);
      }

      builder.Services.AddSingleton<Ner.Flair.INerService, Ner.Flair.Service>();

      builder.Services.AddHostedService<NerBackgroundService>();

      var host = builder.Build();

      Console.CancelKeyPress += (sender, e) =>
      {
         e.Cancel = false;
         cancellationTokenSource.Cancel();
      };

      await host.RunAsync(cancellationToken);
   }
}
