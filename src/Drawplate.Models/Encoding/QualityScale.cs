namespace Drawplate.Models.Encoding;

public static class QualityScale
{
    public const int Default = 50;

    public static bool IsValid(int q) => q is >= 1 and <= 100;

    public static int ToCrf(int q)
    {
        if (!IsValid(q))
        {
            throw new System.ArgumentOutOfRangeException(nameof(q));
        }

        return 51 - q / 2;
    }

    public static bool TryParse(string? text, out int q)
    {
        q = 0;
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        foreach (var ch in text)
        {
            if (ch < '0' || ch > '9')
            {
                return false;
            }
        }

        if (!int.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, out q))
        {
            return false;
        }

        return IsValid(q);
    }
}
