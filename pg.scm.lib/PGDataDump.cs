using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace pg.scm.lib
{

    public interface IPGDumpData
    {
        void DumpData();
    }
    public class PGDataDump : IPGDumpData
    {
        private readonly ILogger<PGAudit> _logger;
        private readonly IConfiguration _config;
        private readonly IPGInformationSchema _infoschema;
        private string _connString;
        private string _pgcommand = "psql.exe";
        private string _exe;
        private string _argsTemplate = "--command \\copy {0} TO '{1}' DELIMITER ',' CSV QUOTE '\"'"; 

        public PGDataDump(ILogger<PGAudit> logger, IConfiguration config, IPGInformationSchema infoschema)
        {
            _logger = logger;
            _config = config;
            _infoschema = infoschema;
            _connString = PGHelper.PGConnectionString(
                _config["PGScmSettings:dumpdb:host"],
                Int32.Parse(_config["PGScmSettings:dumpdb:port"]),
                _config["PGScmSettings:dumpdb:user"],
                _config["PGScmSettings:dumpdb:password"],
                _config["PGScmSettings:dumpdb:database"]);
            _exe = Path.Combine(_config["pgscmSettings:dumpdb:pgruntime"], _pgcommand);
        }

        public void DumpData()
        {
            var blacklist = _config["pgscmSettings:dumpdb:data:blacklist"].Split('|');
            var targetFolder = Path.Combine(_config["pgscmSettings:dumpdb:workingfolder"], "dbdata");

            foreach (var table in _infoschema.Tables().Elements())
            {
                var tablename = table.Attribute("tablename").Value;
                var schemaname = table.Attribute("schemaname").Value;
                var schemaWildcard = string.Format("{0}.*",schemaname);
                var isBlackListed = (from x in blacklist
                             where x == schemaWildcard
                             select x).SingleOrDefault();
                if (isBlackListed == null)
                {
                    runPGCommand(schemaname, tablename, targetFolder);
                }


            }

        }

        private void runPGCommand(string schemaName, string tableName, string targetFolder)
        {
            var recordset = string.Format("(SELECT * FROM {0}.{1})", schemaName, tableName);
            var targetFile = Path.Combine(targetFolder, string.Format("{0}.{1}.csv", schemaName, tableName));
            var arguments = string.Format(_argsTemplate, recordset, targetFile);
            Process process = new Process();
            StringBuilder outputStringBuilder = new StringBuilder();
            FileInfo fi = new FileInfo(_exe);
            if (!fi.Exists)
            {
                throw new InvalidOperationException(string.Format("can not find .exe utility:{0}", _exe));
            }

            // run the process
            try
            {
                process.StartInfo.FileName = fi.FullName;
                process.StartInfo.WorkingDirectory = fi.DirectoryName;
                process.StartInfo.Arguments = arguments;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardInput = true;
                process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.UseShellExecute = false;
                process.EnableRaisingEvents = true;
                process.OutputDataReceived += (sender, eventArgs) => _logger.LogDebug(eventArgs.Data);
                process.ErrorDataReceived += (sender, eventArgs) => _logger.LogDebug(eventArgs.Data);
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                var processExited = process.WaitForExit(1000 * 60 * 20);

                if (processExited == false)
                {
                    process.Kill();
                    _logger.LogError("pg exe timeout");
                    throw new Exception("ERROR: Process took too long to finish");
                }
                else if (process.ExitCode != 0)
                {

                    throw new Exception("Process exited with non-zero exit code of: " + process.ExitCode);
                }
            }
            finally
            {
                process.Close();
            }
        }
    }
}
