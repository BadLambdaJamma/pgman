using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace pg.scm.lib
{
    public interface IPGDump
    {
        bool Dump();
    }

    /// <summary>
    /// runs pg_dump.exe functions
    /// </summary>
    public class PGDump : IPGDump
    {
        private readonly ILogger<PGDump> _logger;
        private readonly IConfiguration _config;

        public PGDump(ILogger<PGDump> logger, IConfiguration config)
        {
            _logger = logger;
            _config = config;
        }

        /// <summary>
        /// calls pg_dump.exe to inspect the schema of a database (no data), file/folder locations sourced in config
        /// </summary>
        /// <returns></returns>
        public bool Dump()
        {
            var executablePath = Path.Combine(_config["pgscmSettings:dumpdb:pgruntime"], "pg_dump.exe");

            var password = (_config["PGScmSettings:dumpdb:usepassword"] == "True" ? 
                string.Format("--password {0}",_config["PGScmSettings:dumpdb:password"]) : 
                "--no-password");

            var dumpfilePath = Path.Combine(_config["PGScmSettings:dumpdb:workingfolder"], "db.sql");

            var arguments = String.Format(
                @" --file {0} --host {1} --port {2} --username {3} --no-password --verbose --format=p --schema-only ""{4}""",
                dumpfilePath,
                _config["PGScmSettings:dumpdb:host"],
                _config["PGScmSettings:dumpdb:port"],
                _config["PGScmSettings:dumpdb:user"],
                _config["PGScmSettings:dumpdb:database"]);

            Process process = new Process();
            StringBuilder outputStringBuilder = new StringBuilder();
            FileInfo fi = new FileInfo(executablePath);
            if (!fi.Exists)
            {
                throw new InvalidOperationException(string.Format("can not find pg_dump.exe utility:{0}", executablePath));
            }

            // run the process
            try
            {
                process.StartInfo.FileName = executablePath;
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
                var processExited = process.WaitForExit(1000*60*20);
            
                if (processExited == false) 
                {
                    process.Kill();
                    _logger.LogError("pg dump timeout");
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
            return true;
        }
    }
}
