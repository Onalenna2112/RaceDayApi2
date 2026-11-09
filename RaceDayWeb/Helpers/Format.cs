namespace RaceDayWeb.Helpers
{
    /// <summary>Small text helpers used by the views so every page formats things the same way.</summary>
    public static class Format
    {
        public static string Ordinal(int number)
        {
            var lastTwo = number % 100;
            var suffix = lastTwo is 11 or 12 or 13
                ? "th"
                : (number % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
            return number + suffix;
        }

        public static string Date(DateTime value) => value.ToString("dd MMM yyyy");

        public static string DateTimeText(DateTime value) => value.ToString("dd MMM yyyy, HH:mm");

        public static string Money(decimal amount) => amount <= 0 ? "Free" : "R" + amount.ToString("N2");

        public static string Km(decimal distance) => distance.ToString("0.##") + " km";

        public static string Initials(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "?";
            var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 1
                ? parts[0].Substring(0, 1).ToUpperInvariant()
                : (parts[0].Substring(0, 1) + parts[^1].Substring(0, 1)).ToUpperInvariant();
        }

        public static string TypeIcon(string? eventType)
        {
            return (eventType ?? string.Empty).ToLowerInvariant() switch
            {
                "run" => "bi-stopwatch",
                "walk" => "bi-person-walking",
                "cycle" => "bi-bicycle",
                _ => "bi-flag-fill"
            };
        }
    }
}
