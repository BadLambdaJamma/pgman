using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace pg.scm.lib
{

    public interface IPGDumpFile
    {
        List<PGDbObject> ParseAll();
    }

    /// <summary>
    /// Object parser for pgdump file in text format of a a pg database.
    /// </summary>
    public class PGDumpFile : IPGDumpFile
    {
        private readonly ILogger<PGDumpFile> _logger;
        private readonly IConfiguration _config;
        private readonly IPGInformationSchema _infoschema;

        public PGDumpFile(ILogger<PGDumpFile> logger, IConfiguration config, IPGInformationSchema infoschema)
        {
            _logger = logger;
            _config = config;
            _infoschema = infoschema;
        }

        /// <summary>
        /// Parses the entire db into a list of PGDbObjects
        /// </summary>
        /// <returns></returns>
        public List<PGDbObject> ParseAll()
        {
            var fileName = Path.Combine(_config["PGScmSettings:dumpdb:workingfolder"], "db.sql");
            var tokenize = _config["PGScmSettings:dumpdb:tokenkeys"];

            string filename;
            string tokens;

            string[] searchList;
            string[] replaceList;

            // all the starts symbols for the DDL in the dump file
            List<String> startsymbols = new List<String>();

            _logger.LogDebug(string.Format("process dump file:{0}  tokenizing values:{1}", fileName, tokenize));
            filename = fileName;

            // tokenize the reductions
            tokens = tokenize;
            string[] split = tokens.Split(' ');
            searchList = new string[split.Length];
            replaceList = new string[split.Length];
            for (var i = 0; i < split.Length; i++)
            {
                var parts = split[i].Split(':');
                searchList[i] = parts[0];
                replaceList[i] = parts[1];
                _logger.LogDebug(string.Format("token values, search:{0} replace:{1}", searchList[i], replaceList[i]));
            }

            // these are "true" BNF start symbols for the DDL required to create database objects in Postgres
            // eventually this tool will begin to support real LALR(1) BNF parsing of DDL statements
            var allStartSymbols = "CREATE SCHEMA|ALTER SCHEMA|COMMENT ON SCHEMA|CREATE EXTENSION|COMMENT ON EXTENSION|CREATE TRIGGER|" +
                "CREATE TYPE|ALTER TYPE|CREATE TABLE|ALTER TABLE ONLY|ALTER TABLE|CREATE FUNCTION|" +
                "ALTER FUNCTION|CREATE VIEW|REVOKE ALL ON TABLE|REVOKE SELECT ON TABLE|GRANT ALL ON TABLE|" +
                "GRANT SELECT ON TABLE|ALTER SEQUENCE|CREATE SEQUENCE|CREATE INDEX|CREATE UNIQUE INDEX|" +
                "REVOKE ALL ON SCHEMA|GRANT ALL ON SCHEMA|EOF";

            startsymbols.AddRange(allStartSymbols.Split('|'));
            _logger.LogTrace(string.Format("start symbols : {0}", allStartSymbols));

            StringBuilder sb = new StringBuilder(2000);
            string currentSymbol = "";
            string foundSymbol = "";
            var db = new List<PGDbObject>();
            
            using (TextReader tr = File.OpenText(filename))
            {
                string line = tr.ReadLine();
                while (line != null)
                {
                    // skip commnets and empty lines
                    if (!line.StartsWith("--") && line.Length > 0)
                    {
                        bool IsStartSymbol = false;
                        foreach (var symbol in startsymbols)
                        {
                            if (line.StartsWith(symbol))
                            {
                                foundSymbol = symbol;
                                IsStartSymbol = true;
                                break;
                            }
                        }

                        // tokenize the strings
                        for (var i = 0; i < searchList.Length; i++)
                        {
                            line = line.Replace(searchList[i], replaceList[i]);
                        }

                        // reduce when we get a new start symbol, unless there is none. (file header)
                        if (IsStartSymbol)
                        {
                            if (currentSymbol != "") // reduce
                            {
                                string objName = getDBObjectName(currentSymbol, sb.ToString());
                                 XElement doc = XElement.Parse("<Metadata/>");
                                // get information schema based data if this is a table
                                if (currentSymbol == "CREATE TABLE")
                                {
                                    _logger.LogDebug(String.Format("getting table meta data: {0}", objName));
                                    var objNameParts = objName.Split('.');
                                    doc = _infoschema.GetTableXml(objNameParts[0], objNameParts[1]);
                                }
                                if (currentSymbol == "CREATE INDEX")
                                {
                                    _logger.LogDebug(String.Format("getting index meta data: {0}", objName));
                                    var objNameParts = objName.Split('.');
                                    doc = _infoschema.GetIndexXml(objNameParts[0], objNameParts[1], objNameParts[2]);
                                }
                                _logger.LogDebug(String.Format("processing object:{0}",objName));
                                db.Add(new PGDbObject() { Name = objName, Symbol = currentSymbol, Text = sb.ToString(), Metadata = doc });
                            }
                            currentSymbol = foundSymbol;
                            sb.Clear();
                            sb.AppendLine(line);
                        }
                        else sb.AppendLine(line);
                    }
                    if (currentSymbol == "EOF")
                        break;
                    line = tr.ReadLine();
                    if (line == null)
                        line = "EOF";
                }

                // write final object
                tr.Close();
                return db;
            }
        }

        /// <summary>
        ///  gets an objects name as schema.object.object...
        /// </summary>
        /// <param name="symbol"></param>
        /// <param name="Text"></param>
        /// <returns></returns>
        private string getDBObjectName(string symbol, string Text)
        {
            var firstLine = Text.Substring(0, Text.IndexOf("\r\n"));
            var firstLineTrimmedSymbol = firstLine.Substring(symbol.Length).Trim();
            while(firstLineTrimmedSymbol.IndexOf("  ") > -1)
            {
                firstLineTrimmedSymbol.Replace("  ", " ");
            }
            var parts = firstLineTrimmedSymbol.Split(' ');
            for (var i = 0; i < parts.Length; i++)
            {
                parts[i] = parts[i].Replace(";", "");
            }

            switch (symbol)
            {
                case "CREATE SCHEMA": return string.Format("{0}", parts[0]);
                case "ALTER SCHEMA": return string.Format("{0}", parts[0]);
                case "COMMENT ON SCHEMA": return string.Format("{0}", parts[0]);
                case "CREATE EXTENSION": return string.Format("{0}", parts[3]);
                case "COMMENT ON EXTENSION": return string.Format("{0}", parts[0]);
                case "CREATE TYPE": return string.Format("{0}", parts[0]);
                case "ALTER TABLE ONLY": return string.Format("{0}", parts[0]);
                case "ALTER TYPE": return string.Format("{0}", parts[0]);
                case "CREATE TABLE": return string.Format("{0}", parts[0].Replace("\"",""));
                case "CREATE TRIGGER":
                    {
                        // ON <schema>.<table>
                        for(int i = 0; i < parts.Length; i++)
                        {
                            if (parts[i] == "ON")
                            {
                                var schematable = parts[i+1].Split('.');
                                return string.Format("{0}.{1}.{2}", schematable[0], schematable[1], parts[0]);
                            }
                        }
                        return "";
                        
                    }

                case "ALTER TABLE": return string.Format("{0}", parts[0]);
                case "CREATE FUNCTION": return string.Format("{0}", parts[0].Substring(0,parts[0].IndexOf("(")));
                case "ALTER FUNCTION": return string.Format("{0}", parts[0]);
                case "CREATE VIEW": return string.Format("{0}", parts[0]);
                case "CREATE SEQUENCE": return string.Format("{0}", parts[0]);
                case "CREATE INDEX":
                    {
                        var schematable = parts[2].Split('.');
                        return string.Format("{0}.{1}.{2}", schematable[0], schematable[1],parts[0]);
                    }
                case "CREATE UNIQUE INDEX": return string.Format("{0}", parts[0]);
                case "REVOKE ALL ON TABLE": return string.Format("{0}", parts[0]);
                case "REVOKE SELECT ON TABLE": return string.Format("{0}", parts[0]);
                case "GRANT ALL ON TABLE": return string.Format("{0}", parts[0]);
                case "GRANT SELECT ON TABLE": return string.Format("{0}", parts[0]);
                case "ALTER SEQUENCE": return string.Format("{0}", parts[0]);
                case "REVOKE ALL ON SCHEMA":  return string.Format("{0}", parts[0]);
                case "GRANT ALL ON SCHEMA": return string.Format("{0}", parts[0]);
                default: return "";
            }
        }
    }
}
