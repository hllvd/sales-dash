namespace SalesApp.Models
{
    public enum Segment
    {
        Lar = 1,
        Motores = 2,
        Servicos = 3
    }

    public static class SegmentExtensions
    {
        public static string? ToDisplayName(int? segment)
        {
            return segment switch
            {
                1 => "Lar",
                2 => "Motores",
                3 => "Serviços",
                _ => null
            };
        }

        public static bool TryParseSegment(string? text, out int? segment)
        {
            segment = null;
            if (string.IsNullOrWhiteSpace(text))
            {
                return true;
            }

            var clean = text.Trim().ToLowerInvariant()
                .Replace("ç", "c")
                .Replace("ã", "a")
                .Replace("õ", "o")
                .Replace("é", "e")
                .Replace("ê", "e")
                .Replace("í", "i")
                .Replace("ó", "o")
                .Replace("ú", "u");

            if (clean == "lar" || clean == "1")
            {
                segment = 1;
                return true;
            }
            if (clean == "motor" || clean == "motores" || clean == "2")
            {
                segment = 2;
                return true;
            }
            if (clean == "servico" || clean == "servicos" || clean == "3")
            {
                segment = 3;
                return true;
            }

            return false;
        }
    }
}
