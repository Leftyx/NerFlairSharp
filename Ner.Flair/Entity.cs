namespace Ner.Flair;

public record Entity(string Text, int Start, int End, string Tag, float Score);