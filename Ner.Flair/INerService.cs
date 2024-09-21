namespace Ner.Flair
{
   public interface INerService : IDisposable
   {
      Task InitialiseAsync(string modelPath = "flair/ner-english", string? cacheRoot = null);

      List<Entity> Predict(string text, string[]? filter);

      void Shutdown();
   }
}
