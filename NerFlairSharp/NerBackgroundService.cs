using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Ner.Flair;

namespace NerFlairSharp;

internal sealed class NerBackgroundService : BackgroundService
{
   private readonly INerService _nerService;
   private readonly ILogger<NerBackgroundService> _logger;

   public NerBackgroundService(INerService nerService, ILogger<NerBackgroundService> logger)
   {
      _nerService = nerService;
      _logger = logger;
   }

   /// <summary>
   /// Executes when the service is ready to start.
   /// </summary>
   /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
   /// <returns><see cref="Task"/></returns>
   public override async Task StartAsync(CancellationToken cancellationToken)
   {
      _logger.LogInformation("Starting service {service}", nameof(NerBackgroundService));

      await _nerService.InitialiseAsync("flair/ner-french", cacheRoot: Path.Combine(AppContext.BaseDirectory, "cache"));

      await base.StartAsync(cancellationToken);
   }

   protected override Task ExecuteAsync(CancellationToken stoppingToken)
   {
      string sentence;

      _logger.LogInformation("PER      = person name");
      _logger.LogInformation("LOC      = location name");
      _logger.LogInformation("ORG      = organization name");
      _logger.LogInformation("MISC     = other name");

      while ((sentence = RequestUserInput()).Length > 0)
      {
         var entities = _nerService.Predict(sentence, ["PER", "LOC"]);

         foreach (var entity in entities)
         {
            _logger.LogInformation("{Tag} = {Text}", entity.Tag, entity.Text);
         }
      }

      _logger.LogWarning("Exiting");

      return Task.CompletedTask;
   }

   private static string RequestUserInput()
   {
      Console.Write("Insert sentence (empty to exit): ");

      return Console.ReadLine() ?? string.Empty;
   }

   /// <summary>
   /// Executes when the service is performing a graceful shutdown.
   /// </summary>
   /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
   /// <returns><see cref="Task"/></returns>
   public override Task StopAsync(CancellationToken cancellationToken)
   {
      _logger.LogInformation("Stopping service {service}", nameof(NerBackgroundService));

      _nerService?.Dispose();

      return base.StopAsync(cancellationToken);
   }
}
