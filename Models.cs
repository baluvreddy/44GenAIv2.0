using System.Collections.Generic;

namespace MyWPFApp
{

    public class TestCase
    {
        public string testcaseid { get; set; }
        public string testdesc { get; set; }
        public string pretestid { get; set; }
        public string prereq { get; set; }
        public string[] tag { get; set; }
        public string[] projectid { get; set; }
    }

    public class ExecutionLog
    {
        public string exeid { get; set; }
        public string testcaseid { get; set; }
        public string scripttype { get; set; }
        public string datestamp { get; set; }
        public string exetime { get; set; }
        public string message { get; set; }
        public string output { get; set; }
        public string status { get; set; }
    }

    public class ExecutionResponse
    {
        public string testcaseid { get; set; }
        public string script_type { get; set; }
        public string status { get; set; }
        public ExecutionLogEntry[] logs { get; set; }
    }

    public class ExecutionLogEntry
    {
        public string timestamp { get; set; }
        public string message { get; set; }
        public string status { get; set; }
    }
    public class TestPlan
    {
        public Dictionary<string, Dictionary<string, string>> pretestid_steps { get; set; }
        public Dictionary<string, string> pretestid_scripts { get; set; }
        public string current_testid { get; set; }
        public Dictionary<string, string> current_bdd_steps { get; set; }
    }
}