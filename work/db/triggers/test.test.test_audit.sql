/*

Object Type	: triggers
Name		: test.test.test_audit
Copyright	: � Corp 2018
Comments	:

==============================================================================================================

*/

CREATE TRIGGER test_audit AFTER INSERT OR DELETE OR UPDATE ON test.test FOR EACH ROW EXECUTE PROCEDURE audit.if_modified_func();

