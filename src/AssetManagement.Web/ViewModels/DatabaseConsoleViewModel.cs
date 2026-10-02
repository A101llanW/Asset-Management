using System.Collections.Generic;

namespace AssetManagement.Web.ViewModels
{
    public class DatabaseConsoleViewModel
    {
        public string Sql { get; set; }
        public string Message { get; set; }
        public bool IsError { get; set; }
        public long ExecutionTimeMs { get; set; }
        public int? RowsAffected { get; set; }
        public List<QueryResultSetViewModel> ResultSets { get; set; }
        public List<string> Tables { get; set; }
        public string DatabaseName { get; set; }

        public DatabaseConsoleViewModel()
        {
            ResultSets = new List<QueryResultSetViewModel>();
            Tables = new List<string>();
        }
    }

    public class QueryResultSetViewModel
    {
        public int Index { get; set; }
        public List<string> Columns { get; set; }
        public List<List<string>> Rows { get; set; }
        public int TotalRowCount { get; set; }
        public bool Truncated { get; set; }

        public QueryResultSetViewModel()
        {
            Columns = new List<string>();
            Rows = new List<List<string>>();
        }
    }
}
