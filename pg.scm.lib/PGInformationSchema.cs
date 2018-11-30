using System;
using System.Xml.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace pg.scm.lib
{ 
    public interface IPGInformationSchema
    {
        XElement GetTableXml(string schema, string table);
        XElement GetIndexXml(string schema, string table, string indexName);
        XElement Tables();
    }

    /// <summary>
    /// Gets information for a PG database using information schema tables
    /// </summary>
    public class PGInformationSchema : IPGInformationSchema
    {

        private readonly ILogger<PGInformationSchema> _logger;
        private readonly IConfiguration _config;
        private readonly string _connString;

        public PGInformationSchema(ILogger<PGInformationSchema> logger, IConfiguration config)
        {
            _logger = logger;
            _config = config;
            _connString = PGHelper.PGConnectionString(
                _config["PGScmSettings:dumpdb:host"],
                Int32.Parse(_config["PGScmSettings:dumpdb:port"]),
                _config["PGScmSettings:dumpdb:user"],
                _config["PGScmSettings:dumpdb:password"],
                _config["PGScmSettings:dumpdb:database"]);
        }
        
        #region Sql Templates
        private string getPKSQL =
            "select xmlelement(name colunm, xmlattributes(kc.column_name, ordinal_position, tc.constraint_name)) " +
            "from information_schema.table_constraints tc " +
            "join information_schema.key_column_usage kc " +
            "on kc.table_name = tc.table_name and kc.table_schema = tc.table_schema and kc.constraint_name = tc.constraint_name " +
            "where tc.constraint_type = 'PRIMARY KEY' " +
            "and kc.ordinal_position is not null " +
            "and tc.table_schema = @sp " +
            "and tc.table_name = @tp " +
            "order by " +
            "kc.position_in_unique_constraint";

        // primary keys will not be included
        private string getIndexSQL =
            @"
             SELECT n.nspname AS schemaname,
               ct.relname AS tablename,
               c.relname AS indexname,
               m.amname,
               s.indisunique, s.indisprimary, s.ord,
               a.attname,
               CASE WHEN con.nspname is not null
                    THEN format('%I.%I',con.nspname,co.collname)
               END AS coll,
               CASE WHEN oc.opcname is not null
                    THEN format('%I.%I',ocn.nspname,oc.opcname)
               END AS opclass,
			   
               CASE WHEN pg_indexam_has_property(c.relam, 'can_order')
			   THEN
                    CASE (option & 2) WHEN 2 THEN 'NULLS FIRST' ELSE 'NULLS LAST' END
               END AS NullOrder,
			   CASE WHEN pg_indexam_has_property(c.relam, 'can_order')
			   THEN
                    CASE (option & 1) WHEN 1 THEN 'DESC' ELSE 'ASC' END
               END AS SortOrder,
               pg_get_expr(s.indpred, s.indrelid) AS predicate,
               pg_get_indexdef(s.indexrelid, ord, false) AS expression
               FROM (SELECT *,
                           generate_series(1,array_length(i.indkey,1)) AS ord,
                           unnest(i.indkey) AS key,
                           unnest(i.indcollation) AS coll,
                           unnest(i.indclass) AS class,
                           unnest(i.indoption) AS option
                      FROM pg_index i) s
               JOIN pg_class c ON (c.oid=s.indexrelid)
               JOIN pg_class ct ON (ct.oid=s.indrelid)
               JOIN pg_namespace n ON (n.oid=c.relnamespace)
               JOIN pg_am m ON (m.oid=c.relam)
               LEFT JOIN pg_attribute a ON (a.attrelid=s.indrelid AND a.attnum=s.key)
               LEFT JOIN pg_collation co ON (co.oid=s.coll)
               LEFT JOIN pg_namespace con ON (con.oid=co.collnamespace)
               LEFT JOIN pg_opclass oc ON (oc.oid=s.class)
               LEFT JOIN pg_namespace ocn ON (ocn.oid=oc.opcnamespace) 
               WHERE n.nspname = @sp  AND ct.relname = @tp AND c.relname = @ip";

        private string getTableSql = @"
           SELECT * FROM information_schema.tables
           WHERE 
                table_schema <> 'audit' AND 
                table_schema <> 'pg_catalog' AND 
                table_schema <> 'information_schema' AND 
                table_type = 'BASE TABLE';";
        #endregion


        public XElement Tables()
        {
            var ele = new XElement("tables");
            using (var conn = new NpgsqlConnection(_connString))
            {
                conn.Open();
                using (var cmd = new NpgsqlCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandText = getTableSql;
                    using (NpgsqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            var tableEle = new XElement("table",
                                new XAttribute("schemaname", dr[1].ToString()),
                                new XAttribute("tablename", dr[2].ToString()));

                            ele.Add(tableEle);
                        }
                    }
                }
            }
            return ele;
        }
        /// <summary>
        /// Gets the Xml metadata for an index of a given table and schema
        /// </summary>
        /// <param name="schemaName">the schema name</param>
        /// <param name="tableName">the table name</param>
        /// <param name="indexName">the name of the index</param>
        /// <returns>XElement index metadata</returns>
        public XElement GetIndexXml(string schemaName, string tableName, string indexName)
        {
            var xe = new XElement("index", new XAttribute("schema", schemaName), new XAttribute("tablename", tableName), new XAttribute("indexname", indexName));
            var indexcols = new XElement("columns");

            using (var conn = new NpgsqlConnection(_connString))
            {
                conn.Open();
                using (var cmd = new NpgsqlCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandText = getIndexSQL;
                    cmd.Parameters.AddWithValue("sp", schemaName);
                    cmd.Parameters.AddWithValue("tp", tableName);
                    cmd.Parameters.AddWithValue("ip", indexName);
                    using (NpgsqlDataReader dr = cmd.ExecuteReader())
                    {
                        var drRow = dr.Read();
                        while (drRow)
                        {
                            var ordinalpos = int.Parse(dr[6].ToString());
                            var columnName = dr[7].ToString();
                            var collation = dr[8].ToString();
                            var operationClass = dr[9].ToString();
                            var nullOrder = dr[10].ToString();
                            var sortOrder = dr[11].ToString();
                            var indexPredicate = dr[12].ToString();
                            var indexExpression = dr[13].ToString();
                            var colElement = new XElement("column",
                                new XAttribute("columnname", columnName),
                                new XAttribute("ordinalposition", ordinalpos),
                                new XAttribute("sortorder", sortOrder),
                                new XAttribute("nullorder", nullOrder),
                                new XAttribute("collation", collation),
                                new XAttribute("indexpredictae", indexPredicate),
                                new XAttribute("indexexpression", indexExpression));
                            indexcols.Add(colElement);
                            //indexcols.Add(ele);
                            drRow = dr.Read();
                        }
                        dr.Close();
                        xe.Add(indexcols);
                    }
                }
                conn.Close();
            }
            return xe;
        }

        /// <summary>
        /// Gets the XML metadata for a table in a gievn schema
        /// </summary>
        /// <param name="schema">the shema name</param>
        /// <param name="table">the table name</param>
        /// <returns>XElement of MetaData description</returns>
        public XElement GetTableXml(string schema, string table)
        {

            XElement xe = XElement.Parse("<table/>");
            XElement cols = XElement.Parse("<columns/>");
            XElement pkcols = XElement.Parse("<primarykey/>");

            xe.Add(new XAttribute("name", table));
            xe.Add(new XAttribute("schema", schema));
           

            using (var conn = new NpgsqlConnection(_connString))
            {
                conn.Open();
                using (var cmd = new NpgsqlCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandText =   "SELECT xmlelement(name colunm, xmlattributes(column_name, ordinal_position, data_type, is_nullable, character_maximum_length, numeric_precision, numeric_precision_radix, numeric_scale, column_default)) FROM information_schema.columns WHERE table_schema = @sp and table_name = @tp";
                    cmd.Parameters.AddWithValue("sp", schema);
                    cmd.Parameters.AddWithValue("tp", table);
                    using (NpgsqlDataReader dr = cmd.ExecuteReader())
                    {
                        var drRow = dr.Read();
                        while (drRow)
                        {
                            var ele = XElement.Parse(dr[0].ToString());
                            cols.Add(ele);
                            drRow = dr.Read();
                        }
                        dr.Close();
                    }
                    cmd.CommandText = getPKSQL;
                    using (NpgsqlDataReader dr = cmd.ExecuteReader())
                    {

                        var drRow = dr.Read();
                        var firstrow = true;
                        while (drRow)
                        {   var ele = XElement.Parse(dr[0].ToString());
                            if (firstrow)
                            {
                                firstrow = false;
                                pkcols.Add(new XAttribute("constraint_name",ele.Attribute("constraint_name").Value));
                            }
                            ele.Attribute("constraint_name").Remove();
                            pkcols.Add(ele);
                            drRow = dr.Read();
                        }
 
                        dr.Close();
                    }
                    xe.Add(pkcols);
                    xe.Add(cols);
                }
                conn.Close();
            }
            return xe;
        }
    }
}
