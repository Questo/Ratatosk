CREATE TABLE IF NOT EXISTS events(
    event_id uuid PRIMARY KEY,
    stream_name text NOT NULL,
    event_type text NOT NULL,
    event_data text NOT NULL,
    created_at timestamptz NOT NULL,
    version integer NOT NULL,
    UNIQUE (stream_name, version)
);

CREATE INDEX IF NOT EXISTS idx_event_stream_name ON events(stream_name);

CREATE TABLE IF NOT EXISTS snapshots(
    aggregate_id uuid PRIMARY KEY,
    version integer NOT NULL,
    aggregate_type text NOT NULL,
    taken_at_utc timestamptz NOT NULL,
    UNIQUE (aggregate_id, version)
);

CREATE INDEX IF NOT EXISTS idx_snapshot_aggregate_id ON snapshots(aggregate_id);

CREATE TABLE IF NOT EXISTS product_read_models(
    id uuid PRIMARY KEY,
    name text NOT NULL,
    description text NOT NULL,
    price decimal NOT NULL,
    last_updated_utc timestamptz NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_product_read_models_id ON product_read_models(id);

