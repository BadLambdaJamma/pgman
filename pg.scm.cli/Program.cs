using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using pg.scm.lib;

namespace pg.scm.cli
{
    class Program
    {

        static void Main(string[] args)
        {
            // create a command line runner class with a logger and configuration injected
            var CLIServiceProvider = new DIFactory<CommandLineRunner>();
            var servicesProvider = CLIServiceProvider.ConfigureDI();

            // get the command line runner
            var runner = servicesProvider.GetRequiredService<CommandLineRunner>();

            // do the command line action
            runner.CommandLineAction(args);

            // make sure nlog ends well ;-)
            NLog.LogManager.Shutdown();

            Console.WriteLine("Press any key");
            Console.ReadLine();
            
        }     

        public class CommandLineRunner
        {
            private readonly ILogger<CommandLineRunner> _logger;
            private readonly IConfiguration _config;
            private readonly IPGDump _pgdump;
            private readonly IPGDumpFile _pgdumpfile;
            private readonly IPGFileMaker _pgfilemaker;
            private readonly IPGAudit _pgaudit;
            private readonly IPGDumpData _pgdumpdata;

           public CommandLineRunner(ILogger<CommandLineRunner> logger, IConfiguration config, 
                IPGDump pgdump, IPGDumpFile pgdumpfile, IPGFileMaker pgfilemaker, IPGAudit pgaudit, IPGDumpData pgdumpdata)
            {
                _logger = logger; _config = config; _pgdump = pgdump;
                _pgdumpfile = pgdumpfile; _pgfilemaker = pgfilemaker;
                _pgaudit = pgaudit;  _pgdumpdata = pgdumpdata;
            }

            public void CommandLineAction(string[] args)
            {
                _logger.LogDebug("Command Line Action");

                // pg_dump.exe a pg datbase and parse it to disk
                if (args[0] == "dump")
                {
                    _logger.LogDebug("dump command");
                    // save time by skipping the dump, it may be up to date.
                    if (_config["PGScmSettings:dumpdb:skipdump"] != "True")
                    {
                        _pgdump.Dump();
                    }
                    _pgfilemaker.MakeFiles(_pgdumpfile.ParseAll());
                    _pgdumpdata.DumpData();
                }

                // create a pg database from a set of parsed files
                if (args[0] == "build")
                {
                    _logger.LogDebug("build command");
                    var manifest = _config["PGScmSettings:build:manifestToBuild"];
                    var textsubowners = _config["PGScmSettings:build:textSubstitutions"];
                    PGRunner.Build(manifest, textsubowners);
                }

                // instrument the target db with audit triggers
                if (args[0] == "audit-init")
                {
                    _logger.LogDebug("initialize auditing");
                    _pgaudit.InitAuditing();
                }

                // instrument the target db with audit triggers
                if (args[0] == "audit-rm")
                {
                    _logger.LogDebug("remove auditing");
                    _pgaudit.RmAuditing();
                }

                



                _logger.LogDebug("command completed");
            }
        }
    }
}