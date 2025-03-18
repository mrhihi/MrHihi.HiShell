namespace MrHihi.HiShell;

public static class HiExtensions
{
    public static bool IsIn(this string str, bool ignoreCase = true, params string[] array)
    {
        foreach (var item in array)
        {
            if (ignoreCase ? (str.ToLower() == item.ToLower()) : (str == item))
            {
                return true;
            }
        }
        return false;
    }
    public static bool IsNumeric(this string Value)
    {
            return decimal.TryParse(Value, out _) || double.TryParse(Value, out _);
    }
}