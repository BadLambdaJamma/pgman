/*

Object Type	: indexes
Name		: test.test.customer_idx
Copyright	: � Corp 2018
Comments	:

==============================================================================================================

*/

CREATE INDEX customer_idx ON test.test USING btree (customerid varchar_ops DESC NULLS LAST) WITH (fillfactor='80');
COMMENT ON INDEX test.customer_idx IS 'comment on index';


/*
<index schema="test" tablename="test" indexname="customer_idx">
  <columns>
    <column columnname="customerid" ordinalposition="1" sortorder="DESC" nullorder="NULLS LAST" collation="pg_catalog.&quot;default&quot;" indexpredictae="" indexexpression="customerid" />
  </columns>
</index>
*/
