using Microsoft.Extensions.Logging;
using Python.Runtime;

namespace Ner.Flair;

public sealed class Service : INerService
{
   private static readonly object _lock = new();
   private static IntPtr _threadState;
   private bool _disposed;

   private const string MODULE = "flair";
   private const string DEFAULT_MODEL_PATH = "flair/ner-english";

   private string _modelPath = DEFAULT_MODEL_PATH;
   private string? _cacheRoot;

   private readonly ILogger<Service> _logger;

   public Service(ILogger<Service> logger)
   {
      _logger = logger;

      Python.Included.Installer.InstallPath = AppContext.BaseDirectory;
      Python.Included.Installer.LogMessage += Console.WriteLine;
   }

   public async Task InitialiseAsync(string modelPath = DEFAULT_MODEL_PATH, string? cacheRoot = null)
   {
      await Python.Included.Installer.SetupPython();
      await Python.Included.Installer.TryInstallPip();

      await Python.Included.Installer.PipInstallModule(MODULE);

      if (!PythonEngine.IsInitialized)
      {
         PythonEngine.Initialize();
         _threadState = PythonEngine.BeginAllowThreads();
      }

      _modelPath = modelPath ?? DEFAULT_MODEL_PATH;

      if (!string.IsNullOrWhiteSpace(cacheRoot))
      {
         _cacheRoot = cacheRoot;
         Environment.SetEnvironmentVariable("FLAIR_CACHE_ROOT", _cacheRoot, EnvironmentVariableTarget.Process);
      }
   }

   public List<Entity> Predict(string text, string[]? filter)
   {
      var stopWatch = new System.Diagnostics.Stopwatch();
      stopWatch.Start();

      _logger.LogInformation("Predicting entities in text: {text}.", text);

      try
      {
         var result = ExecutePrediction(text, filter);

         stopWatch.Stop();

         _logger.LogInformation("Prediction took: {elapsed}.", stopWatch.Elapsed);

         return result;
      }
      catch (Exception exception)
      {
         _logger.LogError(exception, nameof(Predict));
      }

      return [];
   }

   private List<Entity> ExecutePrediction(string text, string[]? filter)
   {
      List<Entity> result = [];

      using (Py.GIL())
      {
         dynamic pathlib = Py.Import("pathlib");
         dynamic flair = Py.Import(MODULE);

         if (_cacheRoot != null)
         {
            flair.cache_root = pathlib.Path(_cacheRoot);
         }

         dynamic flairDataSentence = flair.data.Sentence;
         dynamic flairSequenceTagger = flair.models.SequenceTagger;

         dynamic tagger = flairSequenceTagger.load(_modelPath);

         dynamic sentence = flairDataSentence(text);

         tagger.predict(sentence);

         // Get spans (named entities) from the sentence
         dynamic spans = sentence.get_spans("ner");

         // Iterate over each span (NER result)
         foreach (var span in spans)
         {
            //// Extract properties of the span object
            string entityText = span.text;
            int startPos = span.start_position;
            int endPos = span.end_position;
            string tag = span.tag;
            float score = span.score;
            //dynamic labels = span.labels;

            if (filter == null || filter.Length == 0 || filter.Contains(tag))
            {
               result.Add(new Entity(entityText, startPos, endPos, tag, score));
            }

            span.Dispose();
         }

         flairDataSentence.Dispose();
         flairSequenceTagger.Dispose();
         tagger.Dispose();
         sentence.Dispose();

         flair.Dispose();
      }

      return result;
   }

   public void Shutdown()
   {
      lock (_lock)
      {
         AppContext.SetSwitch("System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization", true);

         PythonEngine.EndAllowThreads(_threadState);

         PythonEngine.Shutdown();

         _threadState = IntPtr.Zero;

         AppContext.SetSwitch("System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization", false);
      }
   }

   public void Dispose()
   {
      if (!_disposed)
      {
         Shutdown();

         _disposed = true;
      }
   }
}