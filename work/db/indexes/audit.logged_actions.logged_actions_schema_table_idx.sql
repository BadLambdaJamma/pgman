/*

Object Type	: indexes
Name		: audit.logged_actions.logged_actions_schema_table_idx
Copyright	: � Corp 2018
Comments	:

==============================================================================================================

*/

CREATE INDEX logged_actions_schema_table_idx ON audit.logged_actions USING btree ((((schema_name || '.'::text) || table_name)));


/*
<index schema="audit" tablename="logged_actions" indexname="logged_actions_schema_table_idx">
  <columns>
    <column columnname="" ordinalposition="1" sortorder="ASC" nullorder="NULLS LAST" collation="pg_catalog.&quot;default&quot;" indexpredictae="" indexexpression="(((schema_name || '.'::text) || table_name))" />
  </columns>
</index>
*/
