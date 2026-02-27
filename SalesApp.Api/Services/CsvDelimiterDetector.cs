namespace SalesApp.Services
{
    /// <summary>
    /// Detects whether a CSV file uses ',' or ';' as its delimiter.
    ///
    /// Two independent methods are applied and cross-validated:
    ///   Method 1 – Frequency analysis: counts raw occurrences of ',' and ';'
    ///              in the header line (outside quoted fields).
    ///   Method 2 – Field-count consistency: which delimiter produces the same
    ///              number of fields in both the header and the first data row.
    ///
    /// If both methods agree → the delimiter is accepted.
    /// If they disagree  → an InvalidOperationException is thrown so the caller
    ///                      can reject the upload with a descriptive message.
    /// </summary>
    public static class CsvDelimiterDetector
    {
        private static readonly char[] Candidates = { ',', ';' };

        // ---------------------------------------------------------------
        // Public entry point
        // ---------------------------------------------------------------

        /// <summary>
        /// Reads the beginning of the CSV stream and returns the resolved delimiter.
        /// Resets the stream position to 0 afterwards so callers can re-read.
        /// </summary>
        public static char Detect(Stream stream)
        {
            // Read only the first two non-empty lines — enough for both methods.
            string? headerLine = null;
            string? firstDataLine = null;

            // We need a seekable copy so we can reset after peeking.
            // If the stream is already seekable use it directly; otherwise buffer it.
            Stream readStream = stream.CanSeek ? stream : CopyToMemory(stream);

            try
            {
                using var reader = new StreamReader(readStream, leaveOpen: true);
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    var trimmed = line.Trim();
                    if (string.IsNullOrWhiteSpace(trimmed)) continue;

                    // Skip the Excel sep= hint line — it is not data
                    if (trimmed.StartsWith("sep=", StringComparison.OrdinalIgnoreCase)) continue;

                    // Strip BOM if present on first real line
                    if (headerLine == null)
                    {
                        trimmed = trimmed.TrimStart('\uFEFF');
                        headerLine = trimmed;
                    }
                    else
                    {
                        firstDataLine = trimmed;
                        break;
                    }
                }
            }
            finally
            {
                if (stream.CanSeek) stream.Seek(0, SeekOrigin.Begin);
            }

            if (string.IsNullOrEmpty(headerLine))
                throw new InvalidOperationException("O arquivo CSV parece estar vazio.");

            var m1 = Method1_FrequencyAnalysis(headerLine);
            var m2 = Method2_FieldCountConsistency(headerLine, firstDataLine);

            return Resolve(m1, m2);
        }

        // ---------------------------------------------------------------
        // Method 1 — Frequency analysis on the header row
        // ---------------------------------------------------------------

        /// <summary>
        /// Counts unquoted occurrences of ',' and ';' in the header.
        /// Returns the winner, or null when they are equal (tie).
        /// </summary>
        public static char? Method1_FrequencyAnalysis(string headerLine)
        {
            var counts = CountUnquoted(headerLine);
            int commaCount = counts[','];
            int semiCount = counts[';'];

            if (commaCount > semiCount) return ',';
            if (semiCount > commaCount) return ';';
            return null; // tie
        }

        // ---------------------------------------------------------------
        // Method 2 — Field-count consistency
        // ---------------------------------------------------------------

        /// <summary>
        /// Tries splitting header and first-data-row by each candidate delimiter
        /// and picks the one that yields consistent (and > 1) field counts.
        /// Returns null when both or neither delimiter produces a match.
        /// </summary>
        public static char? Method2_FieldCountConsistency(string headerLine, string? firstDataLine)
        {
            // If there is no data row, fall back to the candidate that produces > 1 fields.
            if (string.IsNullOrEmpty(firstDataLine))
            {
                char? winner = null;
                foreach (var c in Candidates)
                {
                    if (SplitRespectingQuotes(headerLine, c).Count > 1)
                    {
                        if (winner != null) return null; // both produce > 1 — ambiguous
                        winner = c;
                    }
                }
                return winner;
            }

            char? result = null;
            foreach (var c in Candidates)
            {
                var headerFields = SplitRespectingQuotes(headerLine, c);
                var dataFields = SplitRespectingQuotes(firstDataLine, c);

                // Consistent means: same count in both rows AND more than 1 column.
                if (headerFields.Count == dataFields.Count && headerFields.Count > 1)
                {
                    if (result != null) return null; // both delimiters are consistent — ambiguous
                    result = c;
                }
            }
            return result;
        }

        // ---------------------------------------------------------------
        // Decision / cross-validation
        // ---------------------------------------------------------------

        private static char Resolve(char? m1, char? m2)
        {
            // Both agree on a concrete delimiter
            if (m1.HasValue && m2.HasValue)
            {
                if (m1.Value == m2.Value) return m1.Value;

                throw new InvalidOperationException(
                    $"Não foi possível determinar o delimitador do arquivo CSV com confiança: " +
                    $"a análise de frequência aponta '{m1.Value}', " +
                    $"mas a análise estrutural aponta '{m2.Value}'. " +
                    "Verifique se o arquivo está no formato correto (vírgula ou ponto-e-vírgula).");
            }

            // One method is conclusive and the other is a tie / null → accept the conclusive one
            if (m1.HasValue && !m2.HasValue) return m1.Value;
            if (!m1.HasValue && m2.HasValue) return m2.Value;

            // Both methods are ambiguous
            throw new InvalidOperationException(
                "Não foi possível determinar o delimitador do arquivo CSV: " +
                "o arquivo contém o mesmo número de vírgulas e ponto-e-vírgulas. " +
                "Por favor, salve o arquivo usando apenas um desses separadores.");
        }

        // ---------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------

        private static Dictionary<char, int> CountUnquoted(string line)
        {
            var counts = new Dictionary<char, int> { [','] = 0, [';'] = 0 };
            bool inQuotes = false;

            foreach (var ch in line)
            {
                if (ch == '"') { inQuotes = !inQuotes; continue; }
                if (inQuotes) continue;
                if (counts.ContainsKey(ch)) counts[ch]++;
            }
            return counts;
        }

        private static List<string> SplitRespectingQuotes(string line, char delimiter)
        {
            var fields = new List<string>();
            bool inQuotes = false;
            var current = new System.Text.StringBuilder();

            foreach (var ch in line)
            {
                if (ch == '"') { inQuotes = !inQuotes; continue; }
                if (!inQuotes && ch == delimiter)
                {
                    fields.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(ch);
                }
            }
            fields.Add(current.ToString());
            return fields;
        }

        private static MemoryStream CopyToMemory(Stream source)
        {
            var ms = new MemoryStream();
            source.CopyTo(ms);
            ms.Seek(0, SeekOrigin.Begin);
            return ms;
        }
    }
}
