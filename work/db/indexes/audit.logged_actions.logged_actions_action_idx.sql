/*

Object Type	: indexes
Name		: audit.logged_actions.logged_actions_action_idx
Copyright	: � Corp 2018
Comments	:

==============================================================================================================

*/

CREATE INDEX logged_actions_action_idx ON audit.logged_actions USING btree (action);


/*
<index schema="audit" tablename="logged_actions" indexname="logged_actions_action_idx">
  <columns>
    <column columnname="action" ordinalposition="1" sortorder="ASC" nullorder="NULLS LAST" collation="pg_catalog.&quot;default&quot;" indexpredictae="" indexexpression="action" />
  </columns>
</index>
*/
