using System.Collections.Generic;
using CommandLine;

namespace Roz
{
    public class Options
    {
        [Option('o', "outfile", Required = false, HelpText = "The file to write results to.", Default = "Roz_Trace.json")]    
        public string Outfile { get; set; }
        
        [Option('w', "watch", Required = false, HelpText = "Process names to watch while the command is running.")]
        public IEnumerable<string> Watched { get; set; }

        [Option('c', "command", Required = false, HelpText = "Command to run, or if not specified Roz will just wait for Ctrl-C.")]
        public IEnumerable<string> Command { get; set; }
    }
}