using System.Collections.Generic;
using System.Linq;

namespace Roz.Core.Logger
{

    public abstract class Entry
    {
        public Entry(int processID, int threadID, double timestamp)
        {
            ProcessID = processID;
            ThreadID = threadID;
            Timestamp = timestamp;
            Args = null;
        }
        public int ProcessID;
        public int ThreadID;
        public double Timestamp;
        public List<(string key, string value)> Args;
        public abstract string Serialize();

        protected virtual string SerializeArgs()
        {
            if (Args == null || !Args.Any()) return "";
            return string.Join(", ", Args.Select(a => $@"""{a.key}"": ""{a.value}"""));
        } 
    }

    public class CompleteEntry : Entry
    {
        public CompleteEntry(int processID, int threadID, double timestamp, double duration, string name)
            : base(processID,threadID,timestamp)
        {
            Duration = duration;
            Name = name;
        }
        public double Duration;
        public string Name;

        public override string Serialize() => $@"{{ ""pid"":{ProcessID}, ""tid"":{ThreadID}, ""ts"":{Timestamp}, ""dur"":{Duration}, ""ph"":""X"", ""name"":""{Name}"", ""args"":{{ {SerializeArgs()} }} }}";
    }

    public class BeginEntry : Entry
    {
        public BeginEntry(int processID, int threadID, double timestamp, string name)
            : base(processID,threadID,timestamp)
        {
            Name = name;
        }
        public string Name;
        public override string Serialize() => $@"{{ ""pid"":{ProcessID}, ""tid"":{ThreadID}, ""ts"":{Timestamp}, ""ph"":""B"", ""name"":""{Name}"", ""args"":{{ {SerializeArgs()} }} }}";
    }

    public class EndEntry : Entry
    {
        public EndEntry(int processID, int threadID, double timestamp)
            : base(processID,threadID,timestamp)
        {
        }
        public override string Serialize() => $@"{{ ""pid"":{ProcessID}, ""tid"":{ThreadID}, ""ts"":{Timestamp}, ""ph"":""E"", ""args"":{{ {SerializeArgs()} }} }}";
   }
}