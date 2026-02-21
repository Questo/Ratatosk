#!/bin/bash
set -euo pipefail

PG_HBA_FILE="${PGDATA}/pg_hba.conf"

# Allow password auth from any container on the docker network (IPv4 & IPv6).
if ! grep -q "0.0.0.0/0" "$PG_HBA_FILE"; then
  echo "host    all             all             0.0.0.0/0               scram-sha-256" >> "$PG_HBA_FILE"
fi
if ! grep -q "::/0" "$PG_HBA_FILE"; then
  echo "host    all             all             ::/0                    scram-sha-256" >> "$PG_HBA_FILE"
fi
