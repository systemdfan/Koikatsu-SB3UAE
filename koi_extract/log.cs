using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace logging
{
    internal class log
    {
        public static void DebugLog(string message)
        {
            if (Program.IsDebugMode)
            {
                Console.WriteLine(message);
            }
        }
    }
}
