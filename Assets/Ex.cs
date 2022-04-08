public static class Ex
{
    public static string Form(this string s, params object[] args)
    {
        return string.Format(s, args);
    }
}