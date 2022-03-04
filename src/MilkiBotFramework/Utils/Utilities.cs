using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
