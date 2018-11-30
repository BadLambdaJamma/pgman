using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace pg.scm.lib
{
    public interface IPGAudit
    {
        void InitAuditing();
        void RmAuditing();
    }
    public class PGAudit : IPGAudit
    {
        private readonly ILogger<PGAudit> _logger;
        private readonly IConfiguration _config;
        private string _connString;

        public PGAudit(ILogger<PGAudit> logger, IConfiguration config)
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
        private string createAuditTable = @"
            DROP TABLE IF EXISTS audit.logged_actions;
            DROP FUNCTION IF EXISTS audit.if_modified_func();
            DROP SCHEMA IF EXISTS audit;

            CREATE schema audit;
            REVOKE CREATE ON schema audit FROM public;
 
            CREATE TABLE audit.logged_actions (
                schema_name text NOT NULL,
                TABLE_NAME text NOT NULL,
                user_name text,
                action_tstamp TIMESTAMP WITH TIME zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                action TEXT NOT NULL CHECK (action IN ('I','D','U')),
                original_data text,
                new_data text,
                query text
            ) WITH (fillfactor=100);
 
            REVOKE ALL ON audit.logged_actions FROM public;
            GRANT SELECT ON audit.logged_actions TO public;
 
            CREATE INDEX logged_actions_schema_table_idx 
            ON audit.logged_actions(((schema_name||'.'||TABLE_NAME)::TEXT));
 
            CREATE INDEX logged_actions_action_tstamp_idx 
            ON audit.logged_actions(action_tstamp);
 
            CREATE INDEX logged_actions_action_idx 
            ON audit.logged_actions(action);";

        private string createAuditTriggerFunction = @"
            DROP FUNCTION IF EXISTS audit.if_modified_func();

            CREATE OR REPLACE FUNCTION audit.if_modified_func() RETURNS TRIGGER AS $body$
            DECLARE
                v_old_data TEXT;
                v_new_data TEXT;
            BEGIN
                  IF (TG_OP = 'UPDATE') THEN
                    v_old_data := ROW(OLD.*);
                    v_new_data := ROW(NEW.*);
                    INSERT INTO audit.logged_actions (schema_name,table_name,user_name,action,original_data,new_data,query) 
                    VALUES (TG_TABLE_SCHEMA::TEXT,TG_TABLE_NAME::TEXT,session_user::TEXT,substring(TG_OP,1,1),v_old_data,v_new_data, current_query());
                    RETURN NEW;
                ELSIF (TG_OP = 'DELETE') THEN
                    v_old_data := ROW(OLD.*);
                    INSERT INTO audit.logged_actions (schema_name,table_name,user_name,action,original_data,query)
                    VALUES (TG_TABLE_SCHEMA::TEXT,TG_TABLE_NAME::TEXT,session_user::TEXT,substring(TG_OP,1,1),v_old_data, current_query());
                    RETURN OLD;
                ELSIF (TG_OP = 'INSERT') THEN
                    v_new_data := ROW(NEW.*);
                    INSERT INTO audit.logged_actions (schema_name,table_name,user_name,action,new_data,query)
                    VALUES (TG_TABLE_SCHEMA::TEXT,TG_TABLE_NAME::TEXT,session_user::TEXT,substring(TG_OP,1,1),v_new_data, current_query());
                    RETURN NEW;
                ELSE
                    RAISE WARNING '[AUDIT.IF_MODIFIED_FUNC] - Other action occurred: %, at %',TG_OP,now();
                    RETURN NULL;
                END IF;
 
            EXCEPTION
                WHEN data_exception THEN
                    RAISE WARNING '[AUDIT.IF_MODIFIED_FUNC] - UDF ERROR [DATA EXCEPTION] - SQLSTATE: %, SQLERRM: %',SQLSTATE,SQLERRM;
                    RETURN NULL;
                WHEN unique_violation THEN
                    RAISE WARNING '[AUDIT.IF_MODIFIED_FUNC] - UDF ERROR [UNIQUE] - SQLSTATE: %, SQLERRM: %',SQLSTATE,SQLERRM;
                    RETURN NULL;
                WHEN OTHERS THEN
                    RAISE WARNING '[AUDIT.IF_MODIFIED_FUNC] - UDF ERROR [OTHER] - SQLSTATE: %, SQLERRM: %',SQLSTATE,SQLERRM;
                    RETURN NULL;
            END;
            $body$
            LANGUAGE plpgsql
            SECURITY DEFINER
            SET search_path = pg_catalog, audit;";


        private string bindAuditTriggerSql = @"
            DROP TRIGGER IF EXISTS {{TABLENAME}}_audit ON {{SCHEMANAME}}.{{TABLENAME}};
            CREATE TRIGGER {{TABLENAME}}_audit
            AFTER INSERT OR UPDATE OR DELETE ON {{SCHEMANAME}}.{{TABLENAME}}
            FOR EACH ROW EXECUTE PROCEDURE audit.if_modified_func();";

        private string unbindAuditTriggerSql = @"
            DROP TRIGGER IF EXISTS {{TABLENAME}}_audit ON {{SCHEMANAME}}.{{TABLENAME}};";


        private string getTableSql = @"
           SELECT * FROM information_schema.tables
           WHERE 
                table_schema <> 'audit' AND 
                table_schema <> 'pg_catalog' AND 
                table_schema <> 'information_schema' AND 
                table_type = 'BASE TABLE';";
        
        private string auditTearDownSQL = @"
            DROP TABLE IF EXISTS audit.logged_actions;
            DROP FUNCTION IF EXISTS audit.if_modified_func();
            DROP SCHEMA IF EXISTS audit;
            ";
        #endregion

        public void InitAuditing()
        {
            RmAuditing();

            using (var conn = new NpgsqlConnection(_connString))
            {
                conn.Open();
                using (var cmd = new NpgsqlCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandText = createAuditTable;
                    cmd.ExecuteNonQuery();
                    cmd.CommandText = createAuditTriggerFunction;
                    cmd.ExecuteNonQuery();
                }
                conn.Close();
            }
            attachAuditTriggers();
        }


        public void RmAuditing()
        {
            detachAuditTriggers();
            using (var conn = new NpgsqlConnection(_connString))
            {
                conn.Open();
                using (var cmd = new NpgsqlCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandText = auditTearDownSQL;
                    cmd.ExecuteNonQuery();
                }
                conn.Close();
            }
            
        }

        public void detachAuditTriggers()
        {
            using (var conn = new NpgsqlConnection(_connString))
            {
                conn.Open();
                using (var cmd = new NpgsqlCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandText = getTableSql;
                    using (NpgsqlDataReader dr = cmd.ExecuteReader())
                    {
                        var drRow = dr.Read();
                        while (drRow)
                        {
                            var schema = dr[1].ToString();
                            var name = dr[2].ToString();
                            detachAuditTrigger(schema, name);
                            drRow = dr.Read();
                        }
                        dr.Close();
                    }
                }
                conn.Close();
            }
        }

        public void detachAuditTrigger(string schemaName, string tableName)
        {
            using (var conn = new NpgsqlConnection(_connString))
            {
                conn.Open();
                using (var cmd = new NpgsqlCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandText = unbindAuditTriggerSql
                        .Replace("{{TABLENAME}}", tableName)
                        .Replace("{{SCHEMANAME}}", schemaName);
                    cmd.ExecuteNonQuery();
                }
                conn.Close();
            }

        }


        public void attachAuditTriggers()
        {
            using (var conn = new NpgsqlConnection(_connString))
            {
                conn.Open();
                using (var cmd = new NpgsqlCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandText = getTableSql;
                    using (NpgsqlDataReader dr = cmd.ExecuteReader())
                    {
                        var drRow = dr.Read();
                        while (drRow)
                        {
                            var schema = dr[1].ToString();
                            var name = dr[2].ToString();
                            attachAuditTrigger(schema, name);
                            drRow = dr.Read();
                        }
                        dr.Close();
                    }
                }
                conn.Close();
            }
        }

        public void attachAuditTrigger(string schemaName, string tableName)
        {
            using (var conn = new NpgsqlConnection(_connString))
            {
                conn.Open();
                using (var cmd = new NpgsqlCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandText = bindAuditTriggerSql
                        .Replace("{{TABLENAME}}", tableName)
                        .Replace("{{SCHEMANAME}}",schemaName);
                    cmd.ExecuteNonQuery();
                }
                conn.Close();
            }
        } 
    }
}
