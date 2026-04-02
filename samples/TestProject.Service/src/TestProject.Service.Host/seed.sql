-- ============================================================
-- TestProject v1.5 — Database seed script
-- Run with: psql -U postgres -h localhost -p 5432 -f seed.sql
-- Idempotent: drops and recreates databases each run
-- ============================================================

-- Terminate active connections to target databases before dropping
SELECT pg_terminate_backend(pid)
FROM pg_stat_activity
WHERE datname IN ('testproject_default', 'testproject_alpha', 'testproject_bravo')
  AND pid <> pg_backend_pid();

DROP DATABASE IF EXISTS testproject_default;
DROP DATABASE IF EXISTS testproject_alpha;
DROP DATABASE IF EXISTS testproject_bravo;

CREATE DATABASE testproject_default;
CREATE DATABASE testproject_alpha;
CREATE DATABASE testproject_bravo;

-- ============================================================
-- Seed testproject_default
-- ============================================================
\c testproject_default

CREATE TABLE IF NOT EXISTS order_details (
    "Id"          BIGSERIAL PRIMARY KEY,
    "Name"        VARCHAR(200)  NOT NULL,
    "Description" VARCHAR(1000),
    "ContainerNo" VARCHAR(100),
    "CreatedAt"   TIMESTAMP NOT NULL DEFAULT NOW()
);

INSERT INTO order_details ("Name", "Description", "ContainerNo", "CreatedAt") VALUES
    ('DEFAULT-ORD-001', 'Default site order 1', 'DFSU1234567', NOW()),
    ('DEFAULT-ORD-002', 'Default site order 2', 'DFSU7654321', NOW()),
    ('DEFAULT-ORD-003', 'Default site order 3', 'DFSU1111111', NOW());

CREATE TABLE IF NOT EXISTS site_config (
    "Key"   VARCHAR(100) PRIMARY KEY,
    "Value" VARCHAR(500) NOT NULL
);

INSERT INTO site_config ("Key", "Value") VALUES
    ('SiteName',      'Default'),
    ('MaxContainers', '100'),
    ('EnableAudit',   'true');

-- ============================================================
-- Seed testproject_alpha
-- ============================================================
\c testproject_alpha

CREATE TABLE IF NOT EXISTS order_details (
    "Id"          BIGSERIAL PRIMARY KEY,
    "Name"        VARCHAR(300)  NOT NULL,
    "Description" VARCHAR(1000),
    "ContainerNo" VARCHAR(100),
    "CreatedAt"   TIMESTAMP NOT NULL DEFAULT NOW()
);

INSERT INTO order_details ("Name", "Description", "ContainerNo", "CreatedAt") VALUES
    ('ALPHA-ORD-001', 'Alpha site order 1', 'ALSU1234567', NOW()),
    ('ALPHA-ORD-002', 'Alpha site order 2 with longer name allowed', 'ALSU7654321', NOW());

CREATE TABLE IF NOT EXISTS site_config (
    "Key"   VARCHAR(100) PRIMARY KEY,
    "Value" VARCHAR(500) NOT NULL
);

INSERT INTO site_config ("Key", "Value") VALUES
    ('SiteName',      'Alpha'),
    ('MaxContainers', '200'),
    ('EnableAudit',   'true'),
    ('AlphaSpecific', 'alpha-only-value');

-- ============================================================
-- Seed testproject_bravo
-- ============================================================
\c testproject_bravo

CREATE TABLE IF NOT EXISTS order_details (
    "Id"          BIGSERIAL PRIMARY KEY,
    "Name"        VARCHAR(200)  NOT NULL,
    "Description" VARCHAR(1000),
    "ContainerNo" VARCHAR(100),
    "CreatedAt"   TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_order_details_container_no ON order_details ("ContainerNo");

INSERT INTO order_details ("Name", "Description", "ContainerNo", "CreatedAt") VALUES
    ('BRAVO-ORD-001', 'Bravo site order 1', 'BRSU1234567', NOW()),
    ('BRAVO-ORD-002', 'Bravo site order 2', 'BRSU7654321', NOW()),
    ('BRAVO-ORD-003', 'Bravo site order 3', 'BRSU2222222', NOW()),
    ('BRAVO-ORD-004', 'Bravo site order 4', 'BRSU3333333', NOW());

CREATE TABLE IF NOT EXISTS site_config (
    "Key"   VARCHAR(100) PRIMARY KEY,
    "Value" VARCHAR(500) NOT NULL
);

INSERT INTO site_config ("Key", "Value") VALUES
    ('SiteName',          'Bravo'),
    ('MaxContainers',     '500'),
    ('EnableAudit',       'false'),
    ('BravoSpecific',     'bravo-only-value'),
    ('CustomsRefRequired','true');

CREATE TABLE IF NOT EXISTS bravo_containers (
    "Id"          BIGSERIAL PRIMARY KEY,
    "ContainerNo" VARCHAR(20)  NOT NULL,
    "IsoCode"     VARCHAR(10)  NOT NULL,
    "CustomsRef"  VARCHAR(50),
    "Status"      VARCHAR(20)  NOT NULL DEFAULT 'Active',
    "CreatedAt"   TIMESTAMP NOT NULL DEFAULT NOW()
);

INSERT INTO bravo_containers ("ContainerNo", "IsoCode", "CustomsRef", "Status", "CreatedAt") VALUES
    ('BRSU1234567', '22G1', 'CR-001', 'Active',  NOW()),
    ('BRSU7654321', '40HC', 'CR-002', 'Active',  NOW()),
    ('BRSU2222222', '20GP', NULL,     'Pending', NOW());
