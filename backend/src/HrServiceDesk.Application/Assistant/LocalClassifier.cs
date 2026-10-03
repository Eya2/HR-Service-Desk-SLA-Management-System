using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using HrServiceDesk.Domain.Catalog;

namespace HrServiceDesk.Application.Assistant;

/// <summary>
/// Offline request routing used when no language model is configured (or it fails): French and English
/// keywords are mapped to the catalog's vocabulary, then each request type is scored on its name, description,
/// category and form labels. It also pre-fills obvious answers (choice options, pay period, amount).
/// </summary>
internal static partial class LocalClassifier
{
    /// <summary>Everyday words → words found in request type names and forms.</summary>
    private static readonly Dictionary<string, string[]> Hints = new(StringComparer.Ordinal)
    {
        ["attestation"] = ["certificate"],
        ["certificat"] = ["certificate"],
        ["travail"] = ["work", "employment"],
        ["emploi"] = ["employment"],
        ["visa"] = ["visa", "certificate"],
        ["ambassade"] = ["visa", "certificate"],
        ["embassy"] = ["visa", "certificate"],
        ["logement"] = ["housing", "certificate"],
        ["landlord"] = ["housing", "certificate"],
        ["proprietaire"] = ["housing", "certificate"],
        ["paie"] = ["payslip", "payroll"],
        ["bulletin"] = ["payslip"],
        ["fiche"] = ["payslip"],
        ["salaire"] = ["salary", "payroll", "payslip"],
        ["paye"] = ["payslip", "payroll"],
        ["pay"] = ["payslip", "payroll"],
        ["paid"] = ["payslip", "payroll"],
        ["heures"] = ["hours", "overtime"],
        ["sup"] = ["overtime"],
        ["supplementaires"] = ["overtime"],
        ["prime"] = ["bonus"],
        ["retenue"] = ["deduction"],
        ["deduit"] = ["deduction"],
        ["erreur"] = ["error", "correction", "wrong"],
        ["mistake"] = ["error", "correction", "wrong"],
        ["missing"] = ["missing", "correction"],
        ["manque"] = ["missing", "correction"],
        ["manquent"] = ["missing", "correction"],
        ["oublie"] = ["missing", "correction"],
        ["oubliee"] = ["missing", "correction"],
        ["apparait"] = ["missing"],
        ["apparaissent"] = ["missing"],
        ["conge"] = ["leave"],
        ["conges"] = ["leave"],
        ["vacances"] = ["leave", "annual"],
        ["holiday"] = ["leave", "annual"],
        ["vacation"] = ["leave", "annual"],
        ["malade"] = ["leave", "sick"],
        ["maladie"] = ["leave", "sick"],
        ["sick"] = ["leave", "sick"],
        ["absence"] = ["leave", "absence"],
        ["maternite"] = ["leave", "parental"],
        ["paternite"] = ["leave", "parental"],
        ["naissance"] = ["leave", "parental"],
        ["banque"] = ["bank"],
        ["bancaire"] = ["bank"],
        ["rib"] = ["bank"],
        ["iban"] = ["bank"],
        ["compte"] = ["bank", "account"],
        ["avance"] = ["advance"],
        ["acompte"] = ["advance"],
        ["pret"] = ["advance"],
        ["loan"] = ["advance"],
        ["formation"] = ["training", "course"],
        ["cours"] = ["training", "course"],
        ["certification"] = ["training", "course"],
        ["conference"] = ["training"],
        ["harcelement"] = ["harassment"],
        ["harceler"] = ["harassment"],
        ["harasse"] = ["harassment"],
        ["bullying"] = ["harassment"],
        ["discrimination"] = ["harassment", "discrimination"],
        ["insulte"] = ["harassment"],
        ["remarques"] = ["harassment"],
        ["remboursement"] = ["expense"],
        ["frais"] = ["expense"],
        ["note"] = ["expense"],
        ["mutuelle"] = ["benefits", "health"],
        ["contrat"] = ["contract"],
    };

    private static readonly HashSet<string> StopWords =
    [
        "the", "and", "for", "with", "from", "that", "this", "have", "has", "was", "are", "not", "but", "you", "your", "need", "please", "would", "like", "want", "can", "could",
        "les", "des", "une", "pour", "avec", "dans", "sur", "pas", "est", "que", "qui", "mon", "mes", "suis", "veux", "voudrais", "besoin", "merci", "bonjour", "vous", "nous", "aux", "par", "mais",
    ];

    private static readonly Dictionary<string, int> Months = new(StringComparer.Ordinal)
    {
        ["january"] = 1, ["janvier"] = 1, ["february"] = 2, ["fevrier"] = 2, ["march"] = 3, ["mars"] = 3, ["april"] = 4, ["avril"] = 4,
        ["may"] = 5, ["mai"] = 5, ["june"] = 6, ["juin"] = 6, ["july"] = 7, ["juillet"] = 7, ["august"] = 8, ["aout"] = 8,
        ["september"] = 9, ["septembre"] = 9, ["october"] = 10, ["octobre"] = 10, ["november"] = 11, ["novembre"] = 11, ["december"] = 12, ["decembre"] = 12,
    };

    public sealed record Ranked(RequestType Type, double Score);

    /// <summary>Request types by relevance (best first); types with no evidence at all are left out.</summary>
    public static IReadOnlyList<Ranked> Rank(string text, IEnumerable<RequestType> types)
    {
        var words = Expand(Words(text));
        if (words.Count == 0)
            return [];

        var ranked = new List<Ranked>();
        foreach (var type in types)
        {
            var name = Words(type.Name);
            var description = Words(type.Description);
            var category = Words(Humanize(type.Category.ToString()));
            var form = type.Schema.Fields.SelectMany(f => Words(f.Label).Concat((f.Options ?? []).SelectMany(o => Words(o.Label)))).ToHashSet();

            double score = 0;
            foreach (var word in words)
            {
                if (name.Contains(word))
                    score += 3;
                if (description.Contains(word))
                    score += 1;
                if (category.Contains(word))
                    score += 1.5;
                if (form.Contains(word))
                    score += 0.5;
            }

            if (score > 0)
                ranked.Add(new Ranked(type, score));
        }

        return ranked.OrderByDescending(r => r.Score).ThenBy(r => r.Type.Name, StringComparer.Ordinal).ToList();
    }

    /// <summary>0 to 1: how clearly the best type wins.</summary>
    public static double Confidence(IReadOnlyList<Ranked> ranked)
    {
        if (ranked.Count == 0)
            return 0;
        var second = ranked.Count > 1 ? ranked[1].Score : 0;
        return Math.Round(Math.Clamp((ranked[0].Score - second + 1) / (ranked[0].Score + 2), 0.15, 0.95), 2);
    }

    /// <summary>Answers that can be read from the text: choice options, a pay period (yyyy-MM) and an amount.</summary>
    public static JsonObject Prefill(string text, RequestType type, DateTimeOffset now)
    {
        var values = new JsonObject();
        var words = Expand(Words(text));
        foreach (var field in type.Schema.Fields)
        {
            switch (field.Type)
            {
                case FormFieldType.Select when field.Options is { Count: > 0 } options:
                    var best = options
                        .Select(o => (Option: o, Score: Words(o.Label).Concat(Words(o.Value.Replace('_', ' '))).Distinct().Count(words.Contains)))
                        .Where(o => o.Score > 0)
                        .OrderByDescending(o => o.Score)
                        .FirstOrDefault();
                    if (best.Option is not null)
                        values[field.Key] = best.Option.Value;
                    break;
                case FormFieldType.Text when Words(field.Label).Contains("period") || field.Key.Contains("period", StringComparison.OrdinalIgnoreCase):
                    if (PayPeriod(text, now) is { } period)
                        values[field.Key] = period;
                    break;
                case FormFieldType.Number when AmountPattern().Match(Fold(text)) is { Success: true } amount:
                    if (decimal.TryParse(amount.Groups["n"].Value.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
                        && (field.Min is null || value >= field.Min) && (field.Max is null || value <= field.Max))
                        values[field.Key] = value;
                    break;
            }
        }

        return values;
    }

    /// <summary>A short title from the first sentence of the text.</summary>
    public static string Title(string text)
    {
        var first = SentenceEnd().Split(text.Trim())[0].Trim();
        if (first.Length > 90)
            first = first[..first.LastIndexOf(' ', 87)].TrimEnd(',', ';', ' ') + "…";
        return first.Length == 0 ? text.Trim() : char.ToUpper(first[0], CultureInfo.InvariantCulture) + first[1..];
    }

    private static readonly HashSet<string> FrenchWords =
    [
        "je", "j", "mon", "ma", "mes", "le", "la", "les", "des", "du", "un", "une", "pour", "avec", "pas", "est", "suis", "ai", "bonjour", "merci", "de", "et", "il", "elle", "sur", "dans", "mais", "ne",
    ];

    private static readonly HashSet<string> EnglishWords =
    [
        "i", "my", "the", "a", "an", "for", "with", "not", "is", "am", "have", "hello", "hi", "thanks", "of", "and", "it", "on", "in", "but", "to", "was",
    ];

    /// <summary>Whether the text reads as French rather than English (the two languages of the local fallback).</summary>
    public static bool LooksFrench(string text)
    {
        var words = WordPattern().Matches(Fold(text)).Select(m => m.Value).ToList();
        return words.Count(FrenchWords.Contains) > words.Count(EnglishWords.Contains);
    }

    private static string? PayPeriod(string text, DateTimeOffset now)
    {
        var folded = Fold(text);
        foreach (var (name, month) in Months)
        {
            var match = Regex.Match(folded, $@"\b{name}\b(?:\s+(?<y>20\d\d))?");
            if (!match.Success || (name == "may" && !match.Groups["y"].Success)) // "may" alone is usually the verb
                continue;
            var year = match.Groups["y"].Success
                ? int.Parse(match.Groups["y"].Value, CultureInfo.InvariantCulture)
                : month > now.Month ? now.Year - 1 : now.Year; // a past month by default
            return $"{year:D4}-{month:D2}";
        }

        var numeric = NumericPeriod().Match(folded);
        return numeric.Success ? $"{numeric.Groups["y"].Value}-{int.Parse(numeric.Groups["m"].Value, CultureInfo.InvariantCulture):D2}" : null;
    }

    private static HashSet<string> Words(string text) =>
        WordPattern().Matches(Fold(text)).Select(m => m.Value).Where(w => w.Length > 2 && !StopWords.Contains(w)).Select(Stem).ToHashSet();

    private static HashSet<string> Expand(HashSet<string> words)
    {
        var expanded = new HashSet<string>(words);
        foreach (var word in words)
        {
            if (Hints.TryGetValue(word, out var hints))
                expanded.UnionWith(hints);
        }

        return expanded;
    }

    /// <summary>Crude plural folding so "certificates" meets "certificate" and "congés" meets "congé".</summary>
    private static string Stem(string word) => word.Length > 4 && word.EndsWith('s') && !word.EndsWith("ss", StringComparison.Ordinal) ? word[..^1] : word;

    private static string Fold(string text)
    {
        var normalized = (text ?? string.Empty).Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                builder.Append(char.ToLowerInvariant(c));
        }

        return builder.ToString();
    }

    private static string Humanize(string value) => Regex.Replace(value, "(?<=[a-z])(?=[A-Z])", " ");

    [GeneratedRegex(@"[a-z0-9]+")]
    private static partial Regex WordPattern();

    [GeneratedRegex(@"(?<n>\d+(?:[.,]\d+)?)\s*(?:tnd|dt|dinars?|eur|euros?|€|usd|\$)")]
    private static partial Regex AmountPattern();

    [GeneratedRegex(@"\b(?<m>0?[1-9]|1[0-2])[/-](?<y>20\d\d)\b")]
    private static partial Regex NumericPeriod();

    [GeneratedRegex(@"(?<=[.!?])\s+|\n")]
    private static partial Regex SentenceEnd();
}
