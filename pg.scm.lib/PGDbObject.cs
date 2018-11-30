using System;
using System.Xml.Linq;

namespace pg.scm.lib
{
    /// <summary>
    /// PGDbObject : represents a postgres database object (e.g. table, index, sequence, extension)
    /// </summary>
    public class PGDbObject
    {
        /// <summary>
        /// The start symbol for the objects creation type
        /// </summary>
        public String Symbol { get; set; }

        /// <summary>
        /// The parse name of the database object including the schema (2 part name schema.object)
        /// </summary>
        public String Name { get; set; }

        /// <summary>
        /// The SQL to create the object
        /// </summary>
        public String Text { get; set; }

        /// <summary>
        /// metadata for a database object derived from PG information schema tables
        /// </summary>
        public XElement Metadata { get; set; }

    }
}
