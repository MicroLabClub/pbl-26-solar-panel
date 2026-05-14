// Runs once, on first container startup, when /data/db is empty.
// Mongo executes this as the root user against the database
// named in MONGO_INITDB_DATABASE.
//
// We:
//   1. create an application-scoped user (readWrite + dbAdmin on that DB)
//   2. pre-create the collections the API expects
//   3. add a few indexes that match how the API queries

const dbName = process.env.MONGO_INITDB_DATABASE;
const appUser = process.env.MONGO_APP_USERNAME;
const appPass = process.env.MONGO_APP_PASSWORD;

if (!dbName || !appUser || !appPass) {
  print('[init] missing MONGO_INITDB_DATABASE / MONGO_APP_USERNAME / MONGO_APP_PASSWORD — skipping');
  quit(1);
}

const target = db.getSiblingDB(dbName);

print(`[init] creating application user "${appUser}" on db "${dbName}"`);
target.createUser({
  user: appUser,
  pwd: appPass,
  roles: [
    { role: 'readWrite', db: dbName },
    { role: 'dbAdmin',   db: dbName },
  ],
});

print('[init] creating collections');
['telemetry', 'alerts', 'predictions', 'problems'].forEach((c) => {
  target.createCollection(c);
});

print('[init] creating indexes');
target.telemetry.createIndex({ timestamp: -1 });
target.telemetry.createIndex({ source: 1, timestamp: -1 });
target.alerts.createIndex({ timestamp: -1 });
target.alerts.createIndex({ acknowledged: 1, timestamp: -1 });

print('[init] done');
