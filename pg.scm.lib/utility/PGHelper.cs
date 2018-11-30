using System.IO;

namespace pg.scm.lib
{
    public static class PGHelper
    {
        public static string PGConnectionString(string host, int port, string user, string password, string database)
        {
            return string.Format("server={0};port={1};user id={2};password={3};database={4}", host, port, user, password, database);
        }

        public static string DecodeXML(this string encodedXml)

        {
            return encodedXml.Replace("&lt;", "<").Replace("&gt;", ">").Replace("&apos;", "'").Replace("&amp;", "&").Replace("&quot","\"");
        }
        public static void ToFile(this string text, string path)
        {
            using (TextWriter tw = File.CreateText(path))
            {
                tw.Write(text);
                tw.Close();
                return;
            }
        }
        public static string GetContent(this string path)
        {
            using (TextReader tr = File.OpenText(path))
            {
                var result = tr.ReadToEnd();
                tr.Close();
                return result;
            }
        }

    }
}
