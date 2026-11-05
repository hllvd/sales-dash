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
    }
}
