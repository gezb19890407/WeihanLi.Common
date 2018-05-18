﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;

#if NET45

using System.Data.SqlClient;

#endif

using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using JetBrains.Annotations;
using WeihanLi.Common;

// ReSharper disable once CheckNamespace
namespace WeihanLi.Extensions
{
    public static partial class DataExtension
    {
        #region DataTable

        public static DataTable ToDataTable<T>([NotNull]this IEnumerable<T> entities)
        {
            if (null == entities)
            {
                throw new ArgumentNullException(nameof(entities));
            }
            var properties = CacheUtil.TypePropertyCache.GetOrAdd(typeof(T), t => t.GetProperties()).Where(_ => _.CanRead).ToArray();
            var dataTable = new DataTable();
            dataTable.Columns.AddRange(properties.Select(p => new DataColumn(p.Name, p.PropertyType)).ToArray());
            foreach (var item in entities)
            {
                var row = dataTable.NewRow();
                foreach (var property in properties)
                {
                    row[property.Name] = property.GetValue(item);
                }
                dataTable.Rows.Add(row);
            }
            return dataTable;
        }

        /// <summary>
        ///     Enumerates to entities in this collection.
        /// </summary>
        /// <typeparam name="T">Generic type parameter.</typeparam>
        /// <param name="this">The @this to act on.</param>
        /// <returns>@this as an IEnumerable&lt;T&gt;</returns>
        public static IEnumerable<T> ToEntities<T>([NotNull]this DataTable @this) where T : new()
        {
            var type = typeof(T);
            var properties = CacheUtil.TypePropertyCache.GetOrAdd(type, t => t.GetProperties());

            foreach (DataRow dr in @this.Rows)
            {
                var entity = new T();

                foreach (var property in properties)
                {
                    if (@this.Columns.Contains(property.Name))
                    {
                        property.SetValue(entity, dr[property.Name].ToOrDefault(property.PropertyType), null);
                    }
                }

                yield return entity;
            }
        }

        /// <summary>
        ///     Enumerates to expando objects in this collection.
        /// </summary>
        /// <param name="this">The @this to act on.</param>
        /// <returns>@this as an IEnumerable&lt;dynamic&gt;</returns>
        public static IEnumerable<dynamic> ToExpandoObjects([NotNull]this DataTable @this)
        {
            foreach (DataRow dr in @this.Rows)
            {
                dynamic entity = new ExpandoObject();
                var expandoDict = (IDictionary<string, object>)entity;

                foreach (DataColumn column in @this.Columns)
                {
                    expandoDict.Add(column.ColumnName, dr[column]);
                }

                yield return entity;
            }
        }

        /// <summary>
        ///     Enumerates index column to list in this collection.
        /// </summary>
        /// <typeparam name="T">Generic type parameter.</typeparam>
        /// <param name="dataTable">The dataTable to act on.</param>
        /// <param name="index">column index</param>
        /// <returns>@this as an IEnumerable&lt;T&gt;</returns>
        public static IEnumerable<T> ColumnToList<T>(this DataTable dataTable, int index)
        {
            if (dataTable != null && dataTable.Rows.Count > index)
            {
                foreach (DataRow row in dataTable.Rows)
                {
                    yield return row[index].ToOrDefault<T>();
                }
            }
        }

        #endregion DataTable

        #region DataRow

        /// <summary>
        ///     A DataRow extension method that converts the @this to the entities.
        /// </summary>
        /// <typeparam name="T">Generic type parameter.</typeparam>
        /// <param name="this">The @this to act on.</param>
        /// <returns>@this as a T.</returns>
        public static T ToEntity<T>([NotNull]this DataRow @this) where T : new()
        {
            var type = typeof(T);
            var properties = CacheUtil.TypePropertyCache.GetOrAdd(type, t => t.GetProperties());

            var entity = new T();

            foreach (var property in properties)
            {
                if (@this.Table.Columns.Contains(property.Name))
                {
                    property.SetValue(entity, @this[property.Name].ToOrDefault(property.PropertyType), null);
                }
            }

            return entity;
        }

        /// <summary>A DataRow extension method that converts the @this to an expando object.</summary>
        /// <param name="this">The @this to act on.</param>
        /// <returns>@this as a dynamic.</returns>
        public static dynamic ToExpandoObject([NotNull]this DataRow @this)
        {
            dynamic entity = new ExpandoObject();
            var expandoDict = (IDictionary<string, object>)entity;

            foreach (DataColumn column in @this.Table.Columns)
            {
                expandoDict.Add(column.ColumnName, @this[column]);
            }

            return expandoDict;
        }

        #endregion DataRow

        #region IDataReader

        /// <summary>
        ///     An IDataReader extension method that converts the @this to a data table.
        /// </summary>
        /// <param name="this">The @this to act on.</param>
        /// <returns>@this as a DataTable.</returns>
        public static DataTable ToDataTable([NotNull]this IDataReader @this)
        {
            using (@this)
            {
                var dt = new DataTable();
                dt.Load(@this);
                return dt;
            }
        }

        /// <summary>
        ///     Enumerates to entities in this collection.
        /// </summary>
        /// <typeparam name="T">Generic type parameter.</typeparam>
        /// <param name="this">The @this to act on.</param>
        /// <returns>@this as an IEnumerable&lt;T&gt;</returns>
        public static IEnumerable<T> ToEntities<T>([NotNull]this IDataReader @this) where T : new()
        {
            var type = typeof(T);
            var properties = CacheUtil.TypePropertyCache.GetOrAdd(type, t => t.GetProperties());

            var hash = new HashSet<string>(Enumerable.Range(0, @this.FieldCount)
                .Select(@this.GetName));

            while (@this.Read())
            {
                var entity = new T();

                foreach (var property in properties)
                {
                    if (hash.Contains(property.Name))
                    {
                        property.SetValue(entity, @this[property.Name].ToOrDefault(property.PropertyType), null);
                    }
                }

                yield return entity;
            }
        }

        /// <summary>
        ///     An IDataReader extension method that converts the @this to an entity.
        /// </summary>
        /// <typeparam name="T">Generic type parameter.</typeparam>
        /// <param name="this">The @this to act on.</param>
        /// <returns>@this as a T.</returns>
        public static T ToEntity<T>([NotNull]this IDataReader @this) where T : new()
        {
            using (@this)
            {
                var type = typeof(T);
                var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);

                var entity = new T();

                var hash = new HashSet<string>(Enumerable.Range(0, @this.FieldCount)
                    .Select(@this.GetName));
                try
                {
                    foreach (var property in properties)
                    {
                        if (hash.Contains(property.Name))
                        {
                            property.SetValue(entity, @this[property.Name].ToOrDefault(property.PropertyType), null);
                        }
                    }
                    foreach (var field in fields)
                    {
                        if (hash.Contains(field.Name))
                        {
                            field.SetValue(entity, @this[field.Name].ToOrDefault(field.FieldType));
                        }
                    }

                    return entity;
                }
                catch (InvalidOperationException)
                {
                    return default(T);
                }
            }
        }

        /// <summary>
        ///     An IDataReader extension method that converts the @this to an expando object.
        /// </summary>
        /// <param name="this">The @this to act on.</param>
        /// <returns>@this as a dynamic.</returns>
        public static dynamic ToExpandoObject([NotNull]this IDataReader @this)
        {
            var columnNames = Enumerable.Range(0, @this.FieldCount)
                .Select(x => new KeyValuePair<int, string>(x, @this.GetName(x)))
                .ToDictionary(pair => pair.Key);

            dynamic entity = new ExpandoObject();
            var expandoDict = (IDictionary<string, object>)entity;

            Enumerable.Range(0, @this.FieldCount)
                .ToList()
                .ForEach(x => expandoDict.Add(columnNames[x].Value, @this[x]));

            return entity;
        }

        /// <summary>
        ///     Enumerates to expando objects in this collection.
        /// </summary>
        /// <param name="this">The @this to act on.</param>
        /// <returns>@this as an IEnumerable&lt;dynamic&gt;</returns>
        public static IEnumerable<dynamic> ToExpandoObjects([NotNull]this IDataReader @this)
        {
            var columnNames = Enumerable.Range(0, @this.FieldCount)
                .Select(x => new KeyValuePair<int, string>(x, @this.GetName(x)))
                .ToDictionary(pair => pair.Key);

            while (@this.Read())
            {
                dynamic entity = new ExpandoObject();
                var expandoDict = (IDictionary<string, object>)entity;

                Enumerable.Range(0, @this.FieldCount)
                    .ToList()
                    .ForEach(x => expandoDict.Add(columnNames[x].Value, @this[x]));

                yield return entity;
            }
        }

        #endregion IDataReader

        #region IDbConnection

        /// <summary>
        ///     An IDbConnection extension method that ensures that open.
        /// </summary>
        /// <param name="this">The @this to act on.</param>
        public static void EnsureOpen([NotNull]this IDbConnection @this)
        {
            if (@this.State == ConnectionState.Closed)
            {
                @this.Open();
            }
        }

        /// <summary>A DbConnection extension method that queries if a connection is open.</summary>
        /// <param name="this">The @this to act on.</param>
        /// <returns>true if a connection is open, false if not.</returns>
        public static bool IsConnectionOpen([NotNull]this IDbConnection @this)
        {
            return @this.State == ConnectionState.Open;
        }

        #endregion IDbConnection

        #region DbConnection

        /// <summary>
        ///     An DbConnection extension method that ensures that open.
        /// </summary>
        /// <param name="conn">The @this to act on.</param>
        public static async Task EnsureOpenAsync([NotNull]this DbConnection conn)
        {
            if (conn.State == ConnectionState.Closed)
            {
                await conn.OpenAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// 获取参数名称
        /// </summary>
        /// <param name="originName">原参数名</param>
        /// <returns>格式化后的参数名</returns>
        private static string GetParameterName(string originName)
        {
            if (!string.IsNullOrEmpty(originName))
            {
                switch (originName[0])
                {
                    case '@':
                    case ':':
                    case '?':
                        return originName.Substring(1);
                }
            }
            return originName;
        }

        #endregion DbConnection

#if NET45

        #region SqlConnection

        /// <summary>
        /// BulkCopy
        /// </summary>
        /// <param name="conn">数据库连接</param>
        /// <param name="dataTable">dataTable</param>
        /// <returns></returns>
        public static int BulkCopy([NotNull]this SqlConnection conn, DataTable dataTable) => BulkCopy(conn, dataTable, dataTable.TableName);

        /// <summary>
        /// BulkCopy
        /// </summary>
        /// <param name="conn">数据库连接</param>
        /// <param name="dataTable">dataTable</param>
        /// <param name="destinationTableName">目标表</param>
        /// <returns></returns>
        public static int BulkCopy([NotNull]this SqlConnection conn, DataTable dataTable, string destinationTableName) => BulkCopy(conn, dataTable, destinationTableName, 1000);

        /// <summary>
        /// BulkCopy
        /// </summary>
        /// <param name="conn">数据库连接</param>
        /// <param name="dataTable">dataTable</param>
        /// <param name="destinationTableName">目标表名称</param>
        /// <param name="batchSize">批量处理数量</param>
        /// <param name="columnMappings">columnMappings</param>
        /// <returns></returns>
        public static int BulkCopy([NotNull]this SqlConnection conn, DataTable dataTable, string destinationTableName, int batchSize, IDictionary<string, string> columnMappings = null)
        {
            conn.EnsureOpen();
            using (var bulkCopy = new SqlBulkCopy(conn))
            {
                if (null == columnMappings)
                {
                    for (var i = 0; i < dataTable.Columns.Count; i++)
                    {
                        bulkCopy.ColumnMappings.Add(dataTable.Columns[i].ColumnName, dataTable.Columns[i].ColumnName);
                    }
                }
                else
                {
                    foreach (var columnMapping in columnMappings)
                    {
                        bulkCopy.ColumnMappings.Add(columnMapping.Key, columnMapping.Value);
                    }
                }

                bulkCopy.BatchSize = batchSize;
                bulkCopy.DestinationTableName = destinationTableName;
                bulkCopy.WriteToServer(dataTable);
                return 1;
            }
        }

        /// <summary>
        /// BulkCopyAsync
        /// </summary>
        /// <param name="conn">数据库连接</param>
        /// <param name="dataTable">dataTable</param>
        /// <returns></returns>
        public static Task<int> BulkCopyAsync([NotNull]this SqlConnection conn, DataTable dataTable) => BulkCopyAsync(conn, dataTable, dataTable.TableName);

        /// <summary>
        /// BulkCopyAsync
        /// </summary>
        /// <param name="conn">数据库连接</param>
        /// <param name="dataTable">dataTable</param>
        /// <param name="destinationTableName">目标表</param>
        /// <returns></returns>
        public static Task<int> BulkCopyAsync([NotNull]this SqlConnection conn, DataTable dataTable, string destinationTableName) => BulkCopyAsync(conn, dataTable, destinationTableName, 1000);

        /// <summary>
        /// BulkCopyAsync
        /// </summary>
        /// <param name="conn">数据库连接</param>
        /// <param name="dataTable">dataTable</param>
        /// <param name="destinationTableName">目标表名称</param>
        /// <param name="batchSize">批量处理数量</param>
        /// <param name="columnMappings">columnMappings</param>
        /// <returns></returns>
        public static async Task<int> BulkCopyAsync([NotNull]this SqlConnection conn, DataTable dataTable, string destinationTableName, int batchSize, IDictionary<string, string> columnMappings = null)
        {
            await conn.EnsureOpenAsync();
            using (var bulkCopy = new SqlBulkCopy(conn))
            {
                if (null == columnMappings)
                {
                    for (var i = 0; i < dataTable.Columns.Count; i++)
                    {
                        bulkCopy.ColumnMappings.Add(dataTable.Columns[i].ColumnName, dataTable.Columns[i].ColumnName);
                    }
                }
                else
                {
                    foreach (var columnMapping in columnMappings)
                    {
                        bulkCopy.ColumnMappings.Add(columnMapping.Key, columnMapping.Value);
                    }
                }

                bulkCopy.BatchSize = batchSize;
                bulkCopy.DestinationTableName = destinationTableName;
                await bulkCopy.WriteToServerAsync(dataTable);
                return 1;
            }
        }

        #endregion SqlConnection

#endif

        #region DbCommand

        /// <summary>
        ///     A DbCommand extension method that executes the expando object operation.
        /// </summary>
        /// <param name="this">The @this to act on.</param>
        /// <returns>A dynamic.</returns>
        public static dynamic ExecuteExpandoObject([NotNull]this DbCommand @this)
        {
            using (IDataReader reader = @this.ExecuteReader())
            {
                reader.Read();
                return reader.ToExpandoObject();
            }
        }

        /// <summary>
        ///     Enumerates execute expando objects in this collection.
        /// </summary>
        /// <param name="this">The @this to act on.</param>
        /// <returns>
        ///     An enumerator that allows foreach to be used to process execute expando objects in this collection.
        /// </returns>
        public static IEnumerable<dynamic> ExecuteExpandoObjects([NotNull]this DbCommand @this)
        {
            using (IDataReader reader = @this.ExecuteReader())
            {
                return reader.ToExpandoObjects();
            }
        }

        /// <summary>
        /// ExecuteDataTableTo
        /// </summary>
        /// <typeparam name="T">Type</typeparam>
        /// <param name="this">db command</param>
        /// <param name="func">function</param>
        /// <returns></returns>
        public static T ExecuteDataTable<T>([NotNull]this DbCommand @this, Func<DataTable, T> func) => func(@this.ExecuteDataTable());

        /// <summary>
        /// ExecuteDataTableToAsync
        /// </summary>
        /// <typeparam name="T">Type</typeparam>
        /// <param name="this">db command</param>
        /// <param name="func">function</param>
        /// <returns></returns>
        public static Task<T> ExecuteDataTableAsync<T>([NotNull]this DbCommand @this, Func<DataTable, Task<T>> func) =>
            @this.ExecuteDataTableAsync().ContinueWith(result => func(result.Result)).Result;

        /// <summary>
        ///     A DbCommand extension method that executes the scalar to operation.
        /// </summary>
        /// <typeparam name="T">Generic type parameter.</typeparam>
        /// <param name="this">The @this to act on.</param>
        /// <returns>A T.</returns>
        public static T ExecuteScalarTo<T>([NotNull]this DbCommand @this) => @this.ExecuteScalar().To<T>();

        /// <summary>
        ///     A DbCommand extension method that executes the scalar to operation.
        /// </summary>
        /// <typeparam name="T">Generic type parameter.</typeparam>
        /// <param name="this">The @this to act on.</param>
        /// <returns>A T.</returns>
        public static async Task<T> ExecuteScalarToAsync<T>([NotNull]this DbCommand @this) => (await @this.ExecuteScalarAsync()).To<T>();

        /// <summary>
        ///     A DbCommand extension method that executes the scalar to operation.
        /// </summary>
        /// <typeparam name="T">Generic type parameter.</typeparam>
        /// <param name="this">The @this to act on.</param>
        /// <returns>A T.</returns>
        public static T ExecuteScalarToOrDefault<T>([NotNull]this DbCommand @this) => @this.ExecuteScalar().ToOrDefault<T>();

        /// <summary>
        ///     A DbCommand extension method that executes the scalar to operation.
        /// </summary>
        /// <typeparam name="T">Generic type parameter.</typeparam>
        /// <param name="this">The @this to act on.</param>
        /// <returns>A T.</returns>
        public static async Task<T> ExecuteScalarToOrDefaultAsync<T>([NotNull]this DbCommand @this) => (await @this.ExecuteScalarAsync()).ToOrDefault<T>();

        /// <summary>
        ///     A DbCommand extension method that executes the scalar to or default operation.
        /// </summary>
        /// <typeparam name="T">Generic type parameter.</typeparam>
        /// <param name="this">The @this to act on.</param>
        /// <param name="func">The default value factory.</param>
        /// <returns>A T.</returns>
        public static T ExecuteScalarTo<T>([NotNull]this DbCommand @this, Func<object, T> func)
        {
            return func(@this.ExecuteScalar());
        }

        #endregion DbCommand

        #region DbParameter

        public static bool ContainsParam([NotNull]this DbParameterCollection @this, string paramName)
        {
            var originName = GetParameterName(paramName);
            return @this.Contains(originName)
                   || @this.Contains("@" + originName)
                   || @this.Contains("?" + originName);
        }

        public static void AttachDbParameters([NotNull]this DbCommand command, object paramInfo)
        {
            if (paramInfo != null)
            {
                if (paramInfo.IsValueTuple())
                {
                    var fields = paramInfo.GetFields();
                    foreach (var field in fields)
                    {
                        if (command.Parameters.ContainsParam(field.Name)
                            || (command.CommandType == CommandType.Text && !Regex.IsMatch(command.CommandText, @"[?@:]" + field.Name + @"([^\p{L}\p{N}_]+|$)", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant)))
                        {
                            continue;
                        }
                        else
                        {
                            var param = command.CreateParameter();
                            param.ParameterName = GetParameterName(field.Name);
                            param.Value = field.GetValue(paramInfo);
                            param.DbType = field.FieldType.ToDbType();
                            command.Parameters.Add(param);
                        }
                    }
                }
                else if (paramInfo is IDictionary<string, object> paramDictionary)
                {
                    foreach (var property in paramDictionary.Keys)
                    {
                        if (command.Parameters.ContainsParam(property)
                            || (command.CommandType == CommandType.Text && !Regex.IsMatch(command.CommandText,
                                    @"[?@:]?" + property + @"([^\p{L}\p{N}_]+|$)",
                                    RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant)))
                        {
                            continue;
                        }
                        else
                        {
                            var param = command.CreateParameter();
                            param.ParameterName = GetParameterName(property);
                            param.Value = paramDictionary[property];
                            param.DbType = param.Value.GetType().ToDbType();
                            command.Parameters.Add(param);
                        }
                    }
                }
                else
                {
                    var properties = paramInfo.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                    foreach (var property in properties)
                    {
                        if (command.Parameters.ContainsParam(property.Name)
                            || (command.CommandType == CommandType.Text && !Regex.IsMatch(command.CommandText, @"[?@:]" + property.Name + @"([^\p{L}\p{N}_]+|$)", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant)))
                        {
                            continue;
                        }
                        else
                        {
                            var param = command.CreateParameter();
                            param.ParameterName = GetParameterName(property.Name);
                            param.Value = property.GetValue(paramInfo);
                            param.DbType = property.PropertyType.ToDbType();
                            command.Parameters.Add(param);
                        }
                    }
                }
            }
        }

        private static readonly Dictionary<Type, DbType> TypeMap = new Dictionary<Type, DbType>
        {
            [typeof(byte)] = DbType.Byte,
            [typeof(sbyte)] = DbType.SByte,
            [typeof(short)] = DbType.Int16,
            [typeof(ushort)] = DbType.UInt16,
            [typeof(int)] = DbType.Int32,
            [typeof(uint)] = DbType.UInt32,
            [typeof(long)] = DbType.Int64,
            [typeof(ulong)] = DbType.UInt64,
            [typeof(float)] = DbType.Single,
            [typeof(double)] = DbType.Double,
            [typeof(decimal)] = DbType.Decimal,
            [typeof(bool)] = DbType.Boolean,
            [typeof(string)] = DbType.String,
            [typeof(char)] = DbType.StringFixedLength,
            [typeof(Guid)] = DbType.Guid,
            [typeof(DateTime)] = DbType.DateTime2,
            [typeof(DateTimeOffset)] = DbType.DateTimeOffset,
            [typeof(TimeSpan)] = DbType.Time,
            [typeof(byte[])] = DbType.Binary,
            [typeof(byte?)] = DbType.Byte,
            [typeof(sbyte?)] = DbType.SByte,
            [typeof(short?)] = DbType.Int16,
            [typeof(ushort?)] = DbType.UInt16,
            [typeof(int?)] = DbType.Int32,
            [typeof(uint?)] = DbType.UInt32,
            [typeof(long?)] = DbType.Int64,
            [typeof(ulong?)] = DbType.UInt64,
            [typeof(float?)] = DbType.Single,
            [typeof(double?)] = DbType.Double,
            [typeof(decimal?)] = DbType.Decimal,
            [typeof(bool?)] = DbType.Boolean,
            [typeof(char?)] = DbType.StringFixedLength,
            [typeof(Guid?)] = DbType.Guid,
            [typeof(DateTime?)] = DbType.DateTime2,
            [typeof(DateTimeOffset?)] = DbType.DateTimeOffset,
            [typeof(TimeSpan?)] = DbType.Time,
            [typeof(object)] = DbType.Object
        };

        public static DbType ToDbType([NotNull]this Type type)
        {
            if (type.IsEnum() && !TypeMap.ContainsKey(type))
            {
                type = Enum.GetUnderlyingType(type);
            }
            if (TypeMap.TryGetValue(type, out var dbType))
            {
                return dbType;
            }
            if (type.FullName == "System.Data.Linq.Binary")
            {
                return DbType.Binary;
            }
            return DbType.Object;
        }

        #endregion DbParameter
    }
}
