using System;
using System.IO;
using System.Xml.Linq;
using Npgsql;

namespace pg.scm.lib
{
    public static class PGRunner
    {
        static string[] searchList;
        static string[] replaceList;

        public static void Build(string manifest, string textsub)
        {
            var fi = new FileInfo(manifest);
            if (!fi.Exists)
            {
                throw new InvalidOperationException("file not found:" + manifest);
            }

            string[] split = textsub.Split(' ');
            searchList = new string[split.Length];
            replaceList = new string[split.Length];
            for (var i = 0; i < split.Length; i++)
            {
                var parts = split[i].Split(':');
                searchList[i] = parts[0];
                replaceList[i] = parts[1];
            }

            var baseDir = fi.Directory.FullName;
            var manifestDoc = XElement.Load(manifest);
            var connectionStr = manifestDoc.Attribute("connection").Value;
            using (NpgsqlConnection connection = new NpgsqlConnection(connectionStr))
            {
                connection.Open();
                foreach (var script in manifestDoc.Elements())
                {
                    var scriptfile = Path.Combine(baseDir, script.Attribute("path").Value);
                    var scripttype = script.Attribute("type").Value;
                    var scriptcontent = scriptfile.GetContent();
                    for(var i=0; i < searchList.Length; i++)
                    {
                        scriptcontent = scriptcontent.Replace(searchList[i], replaceList[i]);
                    }
                    var command = new NpgsqlCommand(scriptcontent,connection);

                    command.ExecuteNonQuery();
                }
                connection.Close();
            }
        }
    }
}
