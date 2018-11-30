/*

Object Type	: tables
Name		: test.test
Copyright	: � Corp 2018
Comments	:

==============================================================================================================

*/

CREATE TABLE test.test (
    test bigint NOT NULL,
    customerid character varying(20) NOT NULL
);

ALTER TABLE test.test OWNER TO postgres;

ALTER TABLE ONLY test.test
    ADD CONSTRAINT test_pkey PRIMARY KEY (test);


/*
<table name="test" schema="test">
  <primarykey constraint_name="test_pkey">
    <colunm column_name="test" ordinal_position="1" />
  </primarykey>
  <columns>
    <colunm column_name="test" ordinal_position="1" data_type="bigint" is_nullable="NO" numeric_precision="64" numeric_precision_radix="2" numeric_scale="0" />
    <colunm column_name="customerid" ordinal_position="2" data_type="character varying" is_nullable="NO" character_maximum_length="20" />
  </columns>
</table>
*/
