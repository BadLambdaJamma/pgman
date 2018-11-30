/*

Object Type	: indexes
Name		: audit.logged_actions.logged_actions_action_tstamp_idx
Copyright	: � Corp 2018
Comments	:

==============================================================================================================

*/

CREATE INDEX logged_actions_action_tstamp_idx ON audit.logged_actions USING btree (action_tstamp);


/*
<index schema="audit" tablename="logged_actions" indexname="logged_actions_action_tstamp_idx">
  <columns>
    <column columnname="action_tstamp" ordinalposition="1" sortorder="ASC" nullorder="NULLS LAST" collation="" indexpredictae="" indexexpression="action_tstamp" />
  </columns>
</index>
*/
