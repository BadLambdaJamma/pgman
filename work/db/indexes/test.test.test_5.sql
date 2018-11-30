/*

Object Type	: indexes
Name		: test.test.test_5
Copyright	: � Corp 2018
Comments	:

==============================================================================================================

*/

CREATE INDEX test_5 ON test.test USING btree (test DESC, customerid varchar_ops) WITH (fillfactor='80');


/*
<index schema="test" tablename="test" indexname="test_5">
  <columns>
    <column columnname="test" ordinalposition="1" sortorder="DESC" nullorder="NULLS FIRST" collation="" indexpredictae="" indexexpression="test" />
    <column columnname="customerid" ordinalposition="2" sortorder="ASC" nullorder="NULLS LAST" collation="pg_catalog.&quot;default&quot;" indexpredictae="" indexexpression="customerid" />
  </columns>
</index>
*/
