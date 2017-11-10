using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Search.Dialogs
{
    public class RootObject
    {
        public string type { get; set; }
        public string data { get; set; }
    }




    static class StringBuilderExtension
    {
        public static StringBuilder AppendLineWithMarkdown(this StringBuilder sb, string value)
        {
            return sb.Append(value + "\n\n");
        }
    }

}
