
CREATE TABLE IF NOT EXISTS settings (
    key   TEXT PRIMARY KEY,
    value TEXT
);

CREATE TABLE IF NOT EXISTS users (
    id            INTEGER PRIMARY KEY AUTOINCREMENT,
    username      TEXT NOT NULL UNIQUE COLLATE NOCASE,
    full_name     TEXT,
    password_hash TEXT NOT NULL,
    salt          TEXT NOT NULL,
    role          TEXT NOT NULL DEFAULT 'Cashier',
    is_active     INTEGER NOT NULL DEFAULT 1,
    created_at    TEXT
);

CREATE TABLE IF NOT EXISTS categories (
    id   INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL UNIQUE COLLATE NOCASE
);

CREATE TABLE IF NOT EXISTS products (
    id            INTEGER PRIMARY KEY AUTOINCREMENT,
    barcode       TEXT NOT NULL UNIQUE,
    name          TEXT NOT NULL,
    category_id   INTEGER REFERENCES categories(id) ON DELETE SET NULL,
    unit          TEXT NOT NULL DEFAULT 'pcs',
    cost_price    REAL NOT NULL DEFAULT 0,
    sale_price    REAL NOT NULL DEFAULT 0,
    stock         REAL NOT NULL DEFAULT 0,
    reorder_level REAL NOT NULL DEFAULT 5,
    is_active     INTEGER NOT NULL DEFAULT 1,
    created_at    TEXT
);
CREATE INDEX IF NOT EXISTS ix_products_name ON products(name);

CREATE TABLE IF NOT EXISTS sales (
    id             INTEGER PRIMARY KEY AUTOINCREMENT,
    invoice_no     TEXT NOT NULL UNIQUE,
    sale_date      TEXT NOT NULL,
    user_id        INTEGER REFERENCES users(id),
    customer_name  TEXT,
    sub_total      REAL NOT NULL,
    discount       REAL NOT NULL DEFAULT 0,
    total          REAL NOT NULL,
    paid           REAL NOT NULL,
    change_due     REAL NOT NULL DEFAULT 0,
    payment_method TEXT NOT NULL DEFAULT 'Cash'
);
CREATE INDEX IF NOT EXISTS ix_sales_date ON sales(sale_date);

CREATE TABLE IF NOT EXISTS sale_items (
    id           INTEGER PRIMARY KEY AUTOINCREMENT,
    sale_id      INTEGER NOT NULL REFERENCES sales(id) ON DELETE CASCADE,
    product_id   INTEGER NOT NULL REFERENCES products(id),
    barcode      TEXT,
    product_name TEXT NOT NULL,
    qty          REAL NOT NULL,
    unit_price   REAL NOT NULL,
    cost_price   REAL NOT NULL,
    line_total   REAL NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_sale_items_sale ON sale_items(sale_id);

CREATE TABLE IF NOT EXISTS stock_in (
    id         INTEGER PRIMARY KEY AUTOINCREMENT,
    product_id INTEGER NOT NULL REFERENCES products(id),
    qty        REAL NOT NULL,
    cost_price REAL NOT NULL DEFAULT 0,
    supplier   TEXT,
    note       TEXT,
    user_id    INTEGER REFERENCES users(id),
    created_at TEXT NOT NULL
);
