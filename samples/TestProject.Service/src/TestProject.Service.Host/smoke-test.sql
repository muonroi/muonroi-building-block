-- ============================================================
-- TestProject v1.5 — Smoke test script
-- Run with: psql -U postgres -h localhost -p 5432 -f smoke-test.sql
-- Expected: non-zero counts in all 3 databases
-- ============================================================

\c testproject_default
SELECT 'DEFAULT' AS site, COUNT(*) AS order_count   FROM order_details;
SELECT 'DEFAULT' AS site, COUNT(*) AS config_count  FROM site_config;

\c testproject_alpha
SELECT 'ALPHA'   AS site, COUNT(*) AS order_count   FROM order_details;
SELECT 'ALPHA'   AS site, COUNT(*) AS config_count  FROM site_config;

\c testproject_bravo
SELECT 'BRAVO'   AS site, COUNT(*) AS order_count     FROM order_details;
SELECT 'BRAVO'   AS site, COUNT(*) AS config_count    FROM site_config;
SELECT 'BRAVO'   AS site, COUNT(*) AS container_count FROM bravo_containers;
