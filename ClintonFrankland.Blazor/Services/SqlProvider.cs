using System.Data;
using System.Reflection;
using ClintonFrankland.Models;
using Microsoft.Data.SqlClient;

namespace ClintonFrankland.Services;

/// <summary>
/// The SqlProvider class provides the developer with a persistent connection to
/// a Microsoft SQL Server database, and easy methods of storing or retrieving 
/// data from that server using stored procedures.
/// </summary>
public class SqlProvider : IDisposable
{
    private SqlConnection? _connection;
    private readonly string _connectionString;
    private int _commandTimeout = 30;
    private bool _isConnected = false;
    private bool _suppressErrors = false;

    public SqlProvider(string connectionString, bool suppressErrors = false)
    {
        _suppressErrors = suppressErrors;
        _connectionString = connectionString;
        CreateConnection();
    }

    private void CreateConnection()
    {
        try
        {
            if (_isConnected && _connection != null)
            {
                _isConnected = false;
                _connection.Close();
            }
        }
        catch { }

        _connection = new SqlConnection(_connectionString);
        _connection.Open();
        _isConnected = true;
    }

    private void EnsureConnected()
    {
        if (!_isConnected || _connection?.State != ConnectionState.Open)
        {
            CreateConnection();
        }
    }

    public void ExecuteNonQuery(string database, string owner, string storedProcedure, params NamedValue[] parameters)
    {
        try
        {
            EnsureConnected();
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = $"{database}.{owner}.{storedProcedure}";
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandTimeout = _commandTimeout;

            if (parameters != null)
            {
                foreach (var nv in parameters)
                {
                    cmd.Parameters.AddWithValue(nv.Name, nv.Value ?? DBNull.Value);
                }
            }

            cmd.ExecuteNonQuery();
        }
        catch
        {
            _isConnected = false;
            throw;
        }
    }

    public object? GetObject(string database, string owner, string storedProcedure, params NamedValue[] parameters)
    {
        try
        {
            EnsureConnected();
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = $"{database}.{owner}.{storedProcedure}";
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandTimeout = _commandTimeout;

            if (parameters != null)
            {
                foreach (var nv in parameters)
                {
                    var param = cmd.Parameters.AddWithValue(nv.Name, nv.Value ?? DBNull.Value);
                    if (nv.IsBinary)
                    {
                        param.SqlDbType = SqlDbType.Image;
                    }
                }
            }

            return cmd.ExecuteScalar();
        }
        catch
        {
            _isConnected = false;
            throw;
        }
    }

    public int GetInteger(string database, string owner, string storedProcedure, params NamedValue[] parameters)
    {
        return Convert.ToInt32(GetObject(database, owner, storedProcedure, parameters));
    }

    public bool GetBoolean(string database, string owner, string storedProcedure, params NamedValue[] parameters)
    {
        return Convert.ToBoolean(GetObject(database, owner, storedProcedure, parameters));
    }

    public string GetString(string database, string owner, string storedProcedure, params NamedValue[] parameters)
    {
        return GetObject(database, owner, storedProcedure, parameters)?.ToString() ?? string.Empty;
    }

    public DataTable GetDataTable(string database, string owner, string storedProcedure, params NamedValue[] parameters)
    {
        try
        {
            EnsureConnected();
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = $"{database}.{owner}.{storedProcedure}";
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandTimeout = _commandTimeout;

            if (parameters != null)
            {
                foreach (var nv in parameters)
                {
                    var param = cmd.Parameters.AddWithValue(nv.Name, nv.Value ?? DBNull.Value);
                    if (nv.IsBinary)
                    {
                        param.SqlDbType = SqlDbType.Image;
                    }
                }
            }

            var dt = new DataTable();
            using var adapter = new SqlDataAdapter(cmd);
            adapter.Fill(dt);
            return dt;
        }
        catch
        {
            _isConnected = false;
            throw;
        }
    }

    public DataRow? GetDataRow(string database, string owner, string storedProcedure, params NamedValue[] parameters)
    {
        var dt = GetDataTable(database, owner, storedProcedure, parameters);
        return dt.Rows.Count > 0 ? dt.Rows[0] : null;
    }

    public static T? DataRowToObject<T>(DataRow? row) where T : class, new()
    {
        if (row == null) return null;

        var obj = new T();
        var type = typeof(T);

        foreach (DataColumn column in row.Table.Columns)
        {
            var property = type.GetProperty(column.ColumnName, 
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

            if (property != null && row[column] != DBNull.Value)
            {
                try
                {
                    var value = row[column];
                    var targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

                    if (targetType.IsEnum)
                    {
                        value = Enum.ToObject(targetType, value);
                    }
                    else if (targetType != value.GetType())
                    {
                        value = Convert.ChangeType(value, targetType);
                    }

                    property.SetValue(obj, value);
                }
                catch { }
            }
        }

        return obj;
    }

    public static object? DataRowToObject(DataRow? row, Type type)
    {
        if (row == null) return null;

        var obj = Activator.CreateInstance(type);
        if (obj == null) return null;

        foreach (DataColumn column in row.Table.Columns)
        {
            var property = type.GetProperty(column.ColumnName,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

            if (property != null && row[column] != DBNull.Value)
            {
                try
                {
                    var value = row[column];
                    var targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

                    if (targetType.IsEnum)
                    {
                        value = Enum.ToObject(targetType, value);
                    }
                    else if (targetType != value.GetType())
                    {
                        value = Convert.ChangeType(value, targetType);
                    }

                    property.SetValue(obj, value);
                }
                catch { }
            }
        }

        return obj;
    }

    public void Dispose()
    {
        try
        {
            _connection?.Close();
            _connection?.Dispose();
        }
        catch { }
    }
}
