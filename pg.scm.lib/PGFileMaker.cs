using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NLog;

namespace pg.scm.lib
{

    public interface IPGFileMaker
    {
        void MakeFiles(List<PGDbObject> db);
    }

    /// <summary>
    /// Handles making files on disk that describe a PG database
    /// and an .Xml manifest to rebuild a datbase from script
    /// </summary>
    public class PGFileMaker : IPGFileMaker
    {
        private readonly ILogger<PGFileMaker> _logger;
        private readonly IConfiguration _config;

        public PGFileMaker(ILogger<PGFileMaker> logger, IConfiguration config)
        {
            _logger = logger;
            _config = config;
        }

        /// <summary>
        /// Given a list of database objects, layout all those objects onto the file system
        /// </summary>
        /// <param name="db">a list of the objects</param>
        public void MakeFiles(List<PGDbObject> db)
        {
            var parsetarget = Path.Combine(_config["PGScmSettings:dumpdb:workingfolder"], "db");
            var manifestPath = Path.Combine(_config["PGScmSettings:dumpdb:workingfolder"], "db", "db.xml");
            var dbtarget = "";
            var copyright = _config["PGScmSettings:dumpdb:copyright"];
            // create the root folder for files if it does not exist
            var rootFolder = new DirectoryInfo(parsetarget);
            if (!rootFolder.Exists) rootFolder.Create();

            // schemas
            var manifest = XElement.Parse(string.Format("<db target=\"pg\" connection=\"{0}\" />",dbtarget));
            var names = (from xschemas in db where xschemas.Symbol == "CREATE SCHEMA"
                               select xschemas.Name).ToList<String>();
            var allparts = (from yschemas in db where (yschemas.Symbol == "CREATE SCHEMA" || 
                            yschemas.Symbol == "ALTER SCHEMA" || yschemas.Symbol == "COMMENT ON SCHEMA")
                            select yschemas).ToList<PGDbObject>();
            makeFile(parsetarget, "schemas", manifest, names, allparts, copyright);


            // extensions
            names = (from xext in db where xext.Symbol == "CREATE EXTENSION"
                                select xext.Name).ToList<String>();
            allparts = (from yext in db where (yext.Symbol == "CREATE EXTENSION" || yext.Symbol == "COMMENT ON EXTENSION")
                            select yext).ToList<PGDbObject>();
            makeFile(parsetarget, "extensions", manifest, names, allparts, copyright);


            // Types
            names = (from xtype in db where xtype.Symbol == "CREATE TYPE"
                                  select xtype.Name).ToList<String>();
            allparts = (from ytype in db where (ytype.Symbol == "CREATE TYPE" || ytype.Symbol == "ALTER TYPE")
                            select ytype).ToList<PGDbObject>();
            makeFile(parsetarget, "types", manifest, names, allparts, copyright);


            // Tables
            names = (from xtable in db where xtable.Symbol == "CREATE TABLE"
                        select xtable.Name).ToList<String>();
            allparts = (from ytable in db where (ytable.Symbol == "CREATE TABLE" || ytable.Symbol == "ALTER TABLE" || ytable.Symbol == "ALTER TABLE ONLY")
                               && (ytable.Text.IndexOf("FOREIGN KEY") == -1)
                            select ytable).ToList<PGDbObject>();
            makeFile(parsetarget, "tables", manifest, names, allparts, copyright);

            /// Sequences
            names = (from xseq in db
                     where xseq.Symbol == "CREATE SEQUENCE"
                     select xseq.Name).ToList<String>();
            allparts = (from yseq in db
                        where (yseq.Symbol == "CREATE SEQUENCE" || yseq.Symbol == "ALTER SEQUENCE")
                        select yseq).ToList<PGDbObject>();
            makeFile(parsetarget, "sequences", manifest, names, allparts, copyright);


            /// functions
            names = (from xfunc in db where xfunc.Symbol == "CREATE FUNCTION"
                     select xfunc.Name).ToList<String>();
            allparts = (from yfunc in db where (yfunc.Symbol == "CREATE FUNCTION" || yfunc.Symbol == "ALTER FUNCTION") 
                            select yfunc).ToList<PGDbObject>();
            makeFile(parsetarget, "functions", manifest, names, allparts, copyright);


            // views
            names = (from xview in db  where xview.Symbol == "CREATE VIEW"
                        select xview.Name).ToList<String>();
            allparts = (from yview in db where (yview.Symbol == "CREATE VIEW") 
                            select yview).ToList<PGDbObject>();
            makeFile(parsetarget, "views", manifest, names, allparts, copyright);


            // indexes
            names = (from x in db where x.Symbol == "CREATE INDEX"
                            select x.Name).ToList<String>();
            allparts = (from x in db where (x.Symbol == "CREATE INDEX" || x.Symbol == "ALTER INDEX")
                                select x).ToList<PGDbObject>();
            makeFile(parsetarget, "indexes", manifest, names, allparts, copyright);


            // foreign keys
            names = (from x in db where x.Symbol == "CREATE TABLE"
                          select x.Name).ToList<String>();
            allparts = (from x in db where x.Symbol == "ALTER TABLE ONLY" && x.Text.IndexOf("FOREIGN KEY") > -1
                              select x).ToList<PGDbObject>();
            makeFile(parsetarget, "foreignkeys", manifest, names, allparts, copyright );


            // trigger functions
            names = (from x in db where x.Symbol == "CREATE TRIGGER" select x.Name).ToList<String>();
            allparts = (from x in db where x.Symbol == "CREATE TRIGGER" select x).ToList<PGDbObject>();
            makeFile(parsetarget, "triggers", manifest, names, allparts, copyright);

            manifest.ToString().ToFile(manifestPath);
            return;
        }

        // logic to create a single group of files based on the type of database object
        private void makeFile(string parsetarget, string objtype, XElement manifest, List<String> uniqueParts, List<PGDbObject> allParts, string copyright)
        {
            var typeFolder = Path.Combine(parsetarget, objtype);
            var typeDir = new DirectoryInfo(typeFolder);
            if (!typeDir.Exists) typeDir.Create();

            _logger.LogDebug(String.Format("creating files for: {0}", objtype));
            manifest.Add(new XComment(string.Format("begin {0} definition", objtype)));
            foreach (var name in uniqueParts)
            {
                var objectParts = from x in allParts
                                  where x.Name == name
                                  select x;

                var dbObjectfileName = Path.Combine(parsetarget, objtype, String.Format("{0}.sql", name));
                manifest.Add(new XElement("dbobject", new XAttribute("path", Path.Combine(objtype, String.Format("{0}.sql", name))), new XAttribute("type", objtype)));
                if (File.Exists(dbObjectfileName)) File.Delete(dbObjectfileName);

                // read standard header
                string header = "standardheader.txt";
                var headerContent = header.GetContent();
                headerContent = headerContent.Replace("{ObjectType}", objtype);
                headerContent = headerContent.Replace("{Name}", name);
                headerContent = headerContent.Replace("{Copyright}", copyright);

                StringBuilder combined = new StringBuilder(headerContent);
                foreach (var part in objectParts)
                {
                    combined.AppendLine(part.Text);
                }
                _logger.LogDebug(String.Format("create file:{0}", dbObjectfileName));

                // add the metadata to the end of the file - if it exists
                if (objtype == "tables" || objtype == "indexes")
                {
                    combined.AppendLine("");
                    combined.AppendLine("/*");
                    var firstObject = objectParts.FirstOrDefault<PGDbObject>();
                    if (firstObject != null)
                    {
                        combined.AppendLine(objectParts.FirstOrDefault<PGDbObject>().Metadata.ToString());
                    }
                    combined.AppendLine("*/");
                }
                combined.ToString().ToFile(dbObjectfileName);
            }
            manifest.Add(new XComment(string.Format("end {0} definition", objtype)));

        }
    
    }
}
