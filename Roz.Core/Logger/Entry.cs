using System.Collections.Generic;
using System.Linq;

namespace Roz.Core.Logger
{

    public abstract class Entry
    {
        public Entry(double timestamp)
        {
            Timestamp = timestamp;
        }
        public double Timestamp;
        public abstract string Serialize();
    }

    public abstract class ProcessEntry : Entry
    {
        public ProcessEntry(int processID, int threadID, double timestamp)
            : base(timestamp)
        {
            ProcessID = processID;
            ThreadID = threadID;
            Args = null;
        }
        public int ProcessID;
        public int ThreadID;
        public List<(string key, string value)> Args;
        protected virtual string SerializeArgs()
        {
            if (Args == null || !Args.Any()) return "";
            return string.Join(", ", Args.Select(a => $@"""{a.key}"": ""{a.value}"""));
        } 
    }

    public class InstantEntry : Entry
    {
        public InstantEntry(string name, double timestamp)
            : base(timestamp)
        {
            Name = name;
        }
        public string Name;

        public override string Serialize() => $@"{{ ""name"":""{Name}"", ""ts"":{Timestamp}, ""ph"":""i"", ""s"": ""g"" }}";
    }

    public class CompleteEntry : ProcessEntry
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


    public class BeginEntry : ProcessEntry
    {
        public BeginEntry(int processID, int threadID, double timestamp, string name)
            : base(processID,threadID,timestamp)
        {
            Name = name;
        }
        public string Name;
        public override string Serialize() => $@"{{ ""pid"":{ProcessID}, ""tid"":{ThreadID}, ""ts"":{Timestamp}, ""ph"":""B"", ""name"":""{Name}"", ""args"":{{ {SerializeArgs()} }} }}";
    }

    public class EndEntry : ProcessEntry
    {
        public EndEntry(int processID, int threadID, double timestamp)
            : base(processID,threadID,timestamp)
        {
        }
        public override string Serialize() => $@"{{ ""pid"":{ProcessID}, ""tid"":{ThreadID}, ""ts"":{Timestamp}, ""ph"":""E"", ""args"":{{ {SerializeArgs()} }} }}";
   }
}