/*

Object Type	: tables
Name		: audit.logged_actions
Copyright	: � Corp 2018
Comments	:

==============================================================================================================

*/

CREATE TABLE audit.logged_actions (
    schema_name text NOT NULL,
    table_name text NOT NULL,
    user_name text,
    action_tstamp timestamp with time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    action text NOT NULL,
    original_data text,
    new_data text,
    query text,
    CONSTRAINT logged_actions_action_check CHECK ((action = ANY (ARRAY['I'::text, 'D'::text, 'U'::text])))
)
WITH (fillfactor='100');

ALTER TABLE audit.logged_actions OWNER TO postgres;


/*
<table name="logged_actions" schema="audit">
  <primarykey />
  <columns>
    <colunm column_name="schema_name" ordinal_position="1" data_type="text" is_nullable="NO" />
    <colunm column_name="table_name" ordinal_position="2" data_type="text" is_nullable="NO" />
    <colunm column_name="user_name" ordinal_position="3" data_type="text" is_nullable="YES" />
    <colunm column_name="action_tstamp" ordinal_position="4" data_type="timestamp with time zone" is_nullable="NO" column_default="CURRENT_TIMESTAMP" />
    <colunm column_name="action" ordinal_position="5" data_type="text" is_nullable="NO" />
    <colunm column_name="original_data" ordinal_position="6" data_type="text" is_nullable="YES" />
    <colunm column_name="new_data" ordinal_position="7" data_type="text" is_nullable="YES" />
    <colunm column_name="query" ordinal_position="8" data_type="text" is_nullable="YES" />
  </columns>
</table>
*/
