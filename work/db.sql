--
-- PostgreSQL database dump
--

-- Dumped from database version 10.5
-- Dumped by pg_dump version 10.4

-- Started on 2018-11-28 17:29:00

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET client_min_messages = warning;
SET row_security = off;

--
-- TOC entry 7 (class 2615 OID 109388)
-- Name: audit; Type: SCHEMA; Schema: -; Owner: postgres
--

CREATE SCHEMA audit;


ALTER SCHEMA audit OWNER TO postgres;

--
-- TOC entry 9 (class 2615 OID 21242)
-- Name: test; Type: SCHEMA; Schema: -; Owner: postgres
--

CREATE SCHEMA test;


ALTER SCHEMA test OWNER TO postgres;

--
-- TOC entry 1 (class 3079 OID 12924)
-- Name: plpgsql; Type: EXTENSION; Schema: -; Owner: 
--

CREATE EXTENSION IF NOT EXISTS plpgsql WITH SCHEMA pg_catalog;


--
-- TOC entry 2814 (class 0 OID 0)
-- Dependencies: 1
-- Name: EXTENSION plpgsql; Type: COMMENT; Schema: -; Owner: 
--

COMMENT ON EXTENSION plpgsql IS 'PL/pgSQL procedural language';


--
-- TOC entry 200 (class 1255 OID 109400)
-- Name: if_modified_func(); Type: FUNCTION; Schema: audit; Owner: postgres
--

CREATE FUNCTION audit.if_modified_func() RETURNS trigger
    LANGUAGE plpgsql SECURITY DEFINER
    SET search_path TO pg_catalog, audit
    AS $$
            DECLARE
                v_old_data TEXT;
                v_new_data TEXT;
            BEGIN
                  IF (TG_OP = 'UPDATE') THEN
                    v_old_data := ROW(OLD.*);
                    v_new_data := ROW(NEW.*);
                    INSERT INTO audit.logged_actions (schema_name,table_name,user_name,action,original_data,new_data,query) 
                    VALUES (TG_TABLE_SCHEMA::TEXT,TG_TABLE_NAME::TEXT,session_user::TEXT,substring(TG_OP,1,1),v_old_data,v_new_data, current_query());
                    RETURN NEW;
                ELSIF (TG_OP = 'DELETE') THEN
                    v_old_data := ROW(OLD.*);
                    INSERT INTO audit.logged_actions (schema_name,table_name,user_name,action,original_data,query)
                    VALUES (TG_TABLE_SCHEMA::TEXT,TG_TABLE_NAME::TEXT,session_user::TEXT,substring(TG_OP,1,1),v_old_data, current_query());
                    RETURN OLD;
                ELSIF (TG_OP = 'INSERT') THEN
                    v_new_data := ROW(NEW.*);
                    INSERT INTO audit.logged_actions (schema_name,table_name,user_name,action,new_data,query)
                    VALUES (TG_TABLE_SCHEMA::TEXT,TG_TABLE_NAME::TEXT,session_user::TEXT,substring(TG_OP,1,1),v_new_data, current_query());
                    RETURN NEW;
                ELSE
                    RAISE WARNING '[AUDIT.IF_MODIFIED_FUNC] - Other action occurred: %, at %',TG_OP,now();
                    RETURN NULL;
                END IF;
 
            EXCEPTION
                WHEN data_exception THEN
                    RAISE WARNING '[AUDIT.IF_MODIFIED_FUNC] - UDF ERROR [DATA EXCEPTION] - SQLSTATE: %, SQLERRM: %',SQLSTATE,SQLERRM;
                    RETURN NULL;
                WHEN unique_violation THEN
                    RAISE WARNING '[AUDIT.IF_MODIFIED_FUNC] - UDF ERROR [UNIQUE] - SQLSTATE: %, SQLERRM: %',SQLSTATE,SQLERRM;
                    RETURN NULL;
                WHEN OTHERS THEN
                    RAISE WARNING '[AUDIT.IF_MODIFIED_FUNC] - UDF ERROR [OTHER] - SQLSTATE: %, SQLERRM: %',SQLSTATE,SQLERRM;
                    RETURN NULL;
            END;
            $$;


ALTER FUNCTION audit.if_modified_func() OWNER TO postgres;

SET default_tablespace = '';

SET default_with_oids = false;

--
-- TOC entry 199 (class 1259 OID 109389)
-- Name: logged_actions; Type: TABLE; Schema: audit; Owner: postgres
--

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

--
-- TOC entry 198 (class 1259 OID 21243)
-- Name: test; Type: TABLE; Schema: test; Owner: postgres
--

CREATE TABLE test.test (
    test bigint NOT NULL,
    customerid character varying(20) NOT NULL
);


ALTER TABLE test.test OWNER TO postgres;

--
-- TOC entry 2681 (class 2606 OID 21247)
-- Name: test test_pkey; Type: CONSTRAINT; Schema: test; Owner: postgres
--

ALTER TABLE ONLY test.test
    ADD CONSTRAINT test_pkey PRIMARY KEY (test);


--
-- TOC entry 2682 (class 1259 OID 109399)
-- Name: logged_actions_action_idx; Type: INDEX; Schema: audit; Owner: postgres
--

CREATE INDEX logged_actions_action_idx ON audit.logged_actions USING btree (action);


--
-- TOC entry 2683 (class 1259 OID 109398)
-- Name: logged_actions_action_tstamp_idx; Type: INDEX; Schema: audit; Owner: postgres
--

CREATE INDEX logged_actions_action_tstamp_idx ON audit.logged_actions USING btree (action_tstamp);


--
-- TOC entry 2684 (class 1259 OID 109397)
-- Name: logged_actions_schema_table_idx; Type: INDEX; Schema: audit; Owner: postgres
--

CREATE INDEX logged_actions_schema_table_idx ON audit.logged_actions USING btree ((((schema_name || '.'::text) || table_name)));


--
-- TOC entry 2678 (class 1259 OID 21248)
-- Name: customer_idx; Type: INDEX; Schema: test; Owner: postgres
--

CREATE INDEX customer_idx ON test.test USING btree (customerid varchar_ops DESC NULLS LAST) WITH (fillfactor='80');


--
-- TOC entry 2816 (class 0 OID 0)
-- Dependencies: 2678
-- Name: INDEX customer_idx; Type: COMMENT; Schema: test; Owner: postgres
--

COMMENT ON INDEX test.customer_idx IS 'comment on index';


--
-- TOC entry 2679 (class 1259 OID 21249)
-- Name: test_5; Type: INDEX; Schema: test; Owner: postgres
--

CREATE INDEX test_5 ON test.test USING btree (test DESC, customerid varchar_ops) WITH (fillfactor='80');


--
-- TOC entry 2685 (class 2620 OID 109401)
-- Name: test test_audit; Type: TRIGGER; Schema: test; Owner: postgres
--

CREATE TRIGGER test_audit AFTER INSERT OR DELETE OR UPDATE ON test.test FOR EACH ROW EXECUTE PROCEDURE audit.if_modified_func();


--
-- TOC entry 2815 (class 0 OID 0)
-- Dependencies: 199
-- Name: TABLE logged_actions; Type: ACL; Schema: audit; Owner: postgres
--

GRANT SELECT ON TABLE audit.logged_actions TO PUBLIC;


-- Completed on 2018-11-28 17:29:01

--
-- PostgreSQL database dump complete
--

