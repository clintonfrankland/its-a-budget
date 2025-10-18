using System;
using System.Data;
using System.Data.SqlClient;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ClintonFrankland
{


    /// <summary>
/// The SqlProvider class provides the devloper with a persistent connection to
/// a Microsoft SQL Server database, and easy methods of storing or retrieving 
/// data from that server using stored procedures.
/// </summary>
/// <remarks></remarks>
    public class SqlProvider : IDisposable
    {

        private SqlConnection __con;

        private SqlConnection _con
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get
            {
                return __con;
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            set
            {
                if (__con != null)
                {
                    __con.StateChange -= _con_StateChange;
                }

                __con = value;
                if (__con != null)
                {
                    __con.StateChange += _con_StateChange;
                }
            }
        }
        private string _conStr;
        private int _intCommandTimeout = 30;

        private bool _bolConnected = false;

        private bool _bolSuppressErrors = false;

        /// <summary>
    /// Create a new instance of the SqlProvider class, with a connection to 
    /// the Microsoft SQL Server database specified via the connection string.
    /// </summary>
    /// <param name="ConnectionString">A connection string to connect to a 
    /// Microsoft SQL Server.</param>
    /// <remarks></remarks>
        public SqlProvider(string ConnectionString, bool SuppressErrors = false)
        {
            _bolSuppressErrors = SuppressErrors;
            _conStr = ConnectionString;
            CreateConnection();
        }

        private void CreateConnection()
        {
            try
            {
                if (_bolConnected)
                {
                    _bolConnected = false;
                    _con.Close();
                }
            }
            catch (Exception ex)
            {
            }
            _con = new SqlConnection(_conStr);
            _con.Open();
            _bolConnected = true;
        }

        /// <summary>
    /// Execute a non-query stored procedure against the SQL Server database 
    /// </summary>
    /// <param name="Database"></param>
    /// <param name="Owner"></param>
    /// <param name="StoredProcedure"></param>
    /// <param name="Parameters"></param>
    /// <remarks></remarks>
        public void ExecuteNonQuery(string Database, string Owner, string StoredProcedure, params NamedValue[] Parameters)
        {
            try
            {
                if (!_bolConnected) CreateConnection();
                var cmd = _con.CreateCommand();
                cmd.CommandText = Database + "." + Owner + "." + StoredProcedure;
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = _intCommandTimeout;
                if (!(Parameters == null))
                {
                    foreach (var nv in Parameters)
                        cmd.Parameters.AddWithValue(nv.Name, nv.Value);
                }
                cmd.ExecuteNonQuery();
                cmd.Dispose();
            }
            catch (Exception ex)
            {
                _bolConnected = false;
                throw ex;
            }
        }

        /// <summary>
    /// Execute a stored procedure against the SQL Server database using the 
    /// supplied parameters, and return the value in the first column of the
    /// first row of the returned recordset.
    /// </summary>
    /// <param name="Database"></param>
    /// <param name="Owner"></param>
    /// <param name="StoredProcedure"></param>
    /// <param name="Parameters"></param>
    /// <returns></returns>
    /// <remarks></remarks>
        public object GetObject(string Database, string Owner, string StoredProcedure, params NamedValue[] Parameters)
        {
            try
            {
                object objReturn;
                if (!_bolConnected)
                    CreateConnection();
                var cmd = _con.CreateCommand();
                cmd.CommandText = Database + "." + Owner + "." + StoredProcedure;
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = _intCommandTimeout;
                if (!(Parameters == null))
                {
                    foreach (var nv in Parameters)
                    {
                        if (nv.IsBinary)
                        {
                            cmd.Parameters.AddWithValue(nv.Name, nv.Value).SqlDbType = SqlDbType.Image;
                        }
                        else
                        {
                            cmd.Parameters.AddWithValue(nv.Name, nv.Value);
                        }
                    }
                }
                objReturn = cmd.ExecuteScalar();
                cmd.Dispose();
                return objReturn;
            }
            catch (Exception ex)
            {
                _bolConnected = false;
                throw ex;
            }
        }

        /// <summary>
    /// Execute a stored procedure against the SQL Server database using the 
    /// supplied parameters, and return the integer in the first column of the
    /// first row of the returned recordset.
    /// </summary>
    /// <param name="database"></param>
    /// <param name="owner"></param>
    /// <param name="storedprocedure"></param>
    /// <param name="parameters"></param>
    /// <returns></returns>
    /// <remarks></remarks>
        public int GetInteger(string database, string owner, string storedprocedure, params NamedValue[] parameters)
        {
            return Convert.ToInt32(GetObject(database, owner, storedprocedure, parameters));
        }

        /// <summary>
    /// Execute a stored procedure against the SQL Server database using the 
    /// supplied parameters, and return the boolean in the first column of the
    /// first row of the returned recordset.
    /// </summary>
    /// <param name="database"></param>
    /// <param name="owner"></param>
    /// <param name="StoredProcedure"></param>
    /// <param name="Parameters"></param>
    /// <returns></returns>
    /// <remarks></remarks>
        public bool GetBoolean(string database, string owner, string StoredProcedure, params NamedValue[] Parameters)
        {
            return Convert.ToBoolean(GetObject(database, owner, StoredProcedure, Parameters));
        }

        /// <summary>
    /// Execute a stored procedure against the SQL Server database using the 
    /// supplied parameters, and return the string in the first column of the
    /// first row of the returned recordset.
    /// </summary>
    /// <param name="database"></param>
    /// <param name="owner"></param>
    /// <param name="StoredProcedure"></param>
    /// <param name="Parameters"></param>
    /// <returns></returns>
    /// <remarks></remarks>
        public string GetString(string database, string owner, string StoredProcedure, params NamedValue[] Parameters)
        {
            return Convert.ToString(GetObject(database, owner, StoredProcedure, Parameters));
        }

        /// <summary>
    /// Execute a stored procedure against the SQL Server database using the 
    /// supplied parameters, and return the bytes in the first column of the
    /// first row of the returned recordset.
    /// </summary>
    /// <param name="database"></param>
    /// <param name="owner"></param>
    /// <param name="StoredProcedure"></param>
    /// <param name="Parameters"></param>
    /// <returns></returns>
    /// <remarks></remarks>
        public byte[] GetBytes(string database, string owner, string StoredProcedure, params NamedValue[] Parameters)
        {
            return (byte[])GetObject(database, owner, StoredProcedure, Parameters);
        }

        /// <summary>
    /// Execute a stored procedure against the SQL Server database using the 
    /// supplied parameters, and return the decimal in the first column of the
    /// first row of the returned recordset.
    /// </summary>
    /// <param name="StoredProcedure"></param>
    /// <param name="Parameters"></param>
    /// <returns></returns>
    /// <remarks></remarks>
        public decimal GetDecimal(string database, string owner, string StoredProcedure, params NamedValue[] Parameters)
        {
            return Convert.ToDecimal(GetObject(database, owner, StoredProcedure, Parameters));
        }

        /// <summary>
    /// Executes a stored procedure against the SQL Server database using the 
    /// supplied parameters, and return multple record sets as dataTable 
    /// objects in a dataSet object.
    /// </summary>
    /// <param name="database"></param>
    /// <param name="owner"></param>
    /// <param name="StoredProcedure"></param>
    /// <param name="Parameters"></param>
    /// <returns></returns>
    /// <remarks></remarks>
        public DataSet GetDataSet(string database, string owner, string StoredProcedure, params NamedValue[] Parameters)
        {
            try
            {
                DataSet dsReturn;
                SqlDataReader rdr;
                if (!_bolConnected)
                    CreateConnection();
                var cmd = _con.CreateCommand();
                cmd.CommandText = database + "." + owner + "." + StoredProcedure;
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = _intCommandTimeout;
                if (!(Parameters == null))
                {
                    foreach (NamedValue nv in Parameters)
                        cmd.Parameters.AddWithValue(nv.Name, nv.Value);
                }
                rdr = cmd.ExecuteReader();
                dsReturn = convertDataReaderToDataSet(ref rdr);
                rdr.Close();
                cmd.Dispose();
                return dsReturn;
            }
            catch (Exception ex)
            {
                _bolConnected = false;
                throw ex;
            }
        }

        /// <summary>
    /// Executes a stored procedure against the SQL Server database using the 
    /// supplied parameters, and return the resulting record set as a 
    /// DataTable object
    /// </summary>
    /// <param name="database"></param>
    /// <param name="owner"></param>
    /// <param name="StoredProcedure"></param>
    /// <param name="Parameters"></param>
    /// <returns></returns>
    /// <remarks></remarks>
        public DataTable GetDataTable(string database, string owner, string StoredProcedure, params NamedValue[] Parameters)
        {
            try
            {
                return GetDataSet(database, owner, StoredProcedure, Parameters).Tables[0];
            }
            catch (Exception ex)
            {
                _bolConnected = false;
                throw ex;
            }
        }

        /// <summary>
    /// Executes a stored procedure against the sql server database using the
    /// supplied parameters, and returns the first row of the first tabel returned.
    /// </summary>
    /// <param name="database"></param>
    /// <param name="owner"></param>
    /// <param name="StoredProcedure"></param>
    /// <param name="Parameters"></param>
    /// <returns></returns>
    /// <remarks></remarks>
        public DataRow GetDataRow(string database, string owner, string StoredProcedure, params NamedValue[] Parameters)
        {
            try
            {
                return GetDataTable(database, owner, StoredProcedure, Parameters).Rows[0];
            }
            catch (Exception ex)
            {
                _bolConnected = false;
                throw ex;
            }
        }

        /// <summary>
    /// Execute a non-record set returning sql query against the SQL Server 
    /// database.
    /// </summary>
    /// <param name="Text">The sql query to execute</param>
    /// <remarks></remarks>
        public void ExecuteSqlText(string Text)
        {
            try
            {
                if (!_bolConnected)
                    CreateConnection();
                var cmd = _con.CreateCommand();
                cmd.CommandText = Text;
                cmd.CommandType = CommandType.Text;
                cmd.CommandTimeout = _intCommandTimeout;
                cmd.ExecuteNonQuery();
                cmd.Dispose();
            }
            catch (Exception ex)
            {
                _bolConnected = false;
                throw ex;
            }
        }

        /// <summary>
    /// Gets the names of a databases housed in an SQL Server.
    /// </summary>
    /// <returns>An arraylist filled with the names (string) of all databases 
    /// housed in the currently connected SQL Server.</returns>
    /// <remarks></remarks>
        public string[] GetDatabaseNames()
        {
            try
            {
                if (!_bolConnected)
                    CreateConnection();
                var cmd = _con.CreateCommand();
                cmd.CommandText = "SELECT [name] FROM [sys].[databases]";
                cmd.CommandType = CommandType.Text;
                var rdr = cmd.ExecuteReader();
                var dt = convertDataReaderToDataSet(ref rdr).Tables[0];
                cmd.Dispose();
                _con.Close();
                var dbs = new string[dt.Rows.Count];
                int i;
                var loopTo = dt.Rows.Count - 1;
                for (i = 0; i <= loopTo; i++)
                    dbs[i] = dt.Rows[i]["name"].ToString();
                return dbs;
            }
            catch (Exception ex)
            {
                _bolConnected = false;
                throw ex;
            }
        }

        /// <summary>
    /// The number of seconds to wait before failing while excuting an SQL 
    /// Server stored procedure.
    /// </summary>
    /// <value></value>
    /// <returns></returns>
    /// <remarks>2008-01-15 CF: Not currently implemented.</remarks>
        public int CommandTimeout
        {
            get
            {
                return _intCommandTimeout;
            }
            set
            {
                _intCommandTimeout = value;
            }
        }

        public bool Connected
        {
            get
            {
                return _bolConnected;
            }
        }

        private void _con_StateChange(object sender, StateChangeEventArgs e)
        {
            switch (e.CurrentState)
            {
                case ConnectionState.Broken:
                    {
                        _bolConnected = false;
                        break;
                    }
                case ConnectionState.Closed:
                    {
                        _bolConnected = false;
                        break;
                    }
                case ConnectionState.Connecting:
                    {
                        break;
                    }
                case ConnectionState.Executing:
                    {
                        break;
                    }
                case ConnectionState.Fetching:
                    {
                        break;
                    }
                case ConnectionState.Open:
                    {
                        break;
                    }
            }
        }

        private DataSet convertDataReaderToDataSet(ref SqlDataReader reader)
        {
            var dataSet = new DataSet();
            DataRow dataRow;
            string columnName;
            DataColumn column;
            DataTable schemaTable;
            var dataTable = new DataTable();

            try
            {

                do
                {
                    // Create new data table
                    schemaTable = reader.GetSchemaTable();
                    if (!(schemaTable == null))
                    {
                        // A query returning records was executed
                        int i;
                        var loopTo = schemaTable.Rows.Count - 1;
                        for (i = 0; i <= loopTo; i++)
                        {
                            dataRow = schemaTable.Rows[i];
                            // Create a column name that is unique in the data table
                            columnName = dataRow["ColumnName"].ToString();
                            // Add the column definition to the data table
                            column = new DataColumn(columnName, (Type)dataRow["DataType"]);
                            dataTable.Columns.Add(column);
                        }
                        dataSet.Tables.Add(dataTable);

                        // Fill the data table we just created
                        while (reader.Read())
                        {
                            dataRow = dataTable.NewRow();
                            var loopTo1 = reader.FieldCount - 1;
                            for (i = 0; i <= loopTo1; i++)
                                dataRow[i] = reader[i];
                            dataTable.Rows.Add(dataRow);
                        }
                    }

                    else
                    {
                        // No records were returned
                        dataSet.Tables.Add(dataTable);
                    }
                }
                while (reader.NextResult());
                return dataSet;
            }
            catch (Exception ex)
            {
                _bolConnected = false;
                throw ex;
            }
        }

        private bool disposedValue = false;        // To detect redundant calls
                                                   // IDisposable
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    try
                    {
                        if (_con.State != ConnectionState.Closed)
                            _con.Close();
                    }
                    catch (Exception ex)
                    {
                    }
                    _conStr = null;
                    _con.Dispose();
                }

                // TODO: free shared unmanaged resources
            }
            disposedValue = true;
        }

        public bool SuppressErrors
        {
            get
            {
                return _bolSuppressErrors;
            }
            set
            {
                _bolSuppressErrors = value;
            }
        }

        public static object DataRowToObject(DataRow dr, Type objecttype)
        {
            var obj = Activator.CreateInstance(objecttype);
            var props = objecttype.GetProperties();
            foreach (PropertyInfo prop in props)
            {
                try
                {
                    if (prop.PropertyType != dr[prop.Name].GetType() & prop.PropertyType == typeof(bool))
                    {
                        prop.SetValue(obj, (bool)dr[prop.Name]);
                    }
                    else if (prop.PropertyType != dr[prop.Name].GetType() & prop.PropertyType == typeof(int))
                    {
                        prop.SetValue(obj, (int)dr[prop.Name]);
                    }
                    else
                    {
                        prop.SetValue(obj, dr[prop.Name]);
                    }
                }
                catch (Exception ex)
                {
                }
            }
            return obj;
        }

        #region  IDisposable Support 
        // This code added by Visual Basic to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code.  Put cleanup code in Dispose(ByVal disposing As Boolean) above.
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion

    }
}