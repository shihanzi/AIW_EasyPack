using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AIW_EasyPack.TaskScheduler
{
    public class Task
    {
        public static bool IsScheduledRun()
        {
            return Environment.GetCommandLineArgs().Any(a => a.Equals("--silent", StringComparison.OrdinalIgnoreCase));
        }
    }
}
