namespace MilkiBotFramework.Utils
{
    public static class Utilities
    {
        public static string GetRelativePath(string path)
        {
            var uri = new Uri(Environment.CurrentDirectory + "/").MakeRelativeUri(new Uri(path));
            return $"./{uri}";
        }
    }
}
