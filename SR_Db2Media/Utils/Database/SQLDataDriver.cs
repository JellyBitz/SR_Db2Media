using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SqlClient;
using System.Threading.Tasks;

namespace SR_Db2Media.Utils.Database
{
    public class SQLDataDriver
    {
        #region Private Members
        private string ConnectionString { get; }
        private SqlConnection Connection { get; set; }
        #endregion

        #region Constructor
        public SQLDataDriver(string Host, string Username, string Password, string Database)
        {
            ConnectionString = $"Data Source={Host};Initial Catalog={Database};User ID={Username};Password={Password}";
        }
        #endregion

        #region Public Methods
        public List<string[]> GetTableResult(string Query)
        {
            using (SqlConnection conn = new SqlConnection(ConnectionString))
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(Query, conn))
                {
                    using (var dataReader = cmd.ExecuteReader())
                    {
                        // Rows
                        List<string[]> rows = new List<string[]>();
                        while (dataReader.Read())
                        {
                            // Columns
                            string[] columns = new string[dataReader.FieldCount];
                            for (int i = 0; i < dataReader.FieldCount; i++)
                                columns[i] = dataReader.GetValue(i).ToString();
                            rows.Add(columns);
                        }
                        // Return result
                        return rows;
                    }
                }
            }
        }
        #endregion
    }

}
