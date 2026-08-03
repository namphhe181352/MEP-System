const sqlite3 = require('sqlite3').verbose();
const path = require('path');
const dbPath = path.join(__dirname, 'mep_database.db');
const db = new sqlite3.Database(dbPath);

db.serialize(() => {
  db.all("SELECT BranchId, BranchName, IsActive FROM Branches", (err, rows) => {
    console.log("Branches:", rows);
  });
  db.all("SELECT BranchId, COUNT(*) as count FROM BranchProducts GROUP BY BranchId", (err, rows) => {
    console.log("Products per Branch:", rows);
  });
  db.all("SELECT UserId, Username, DefaultBranchId, Roles.RoleName FROM Users JOIN UserRoles ON Users.UserId = UserRoles.UserId JOIN Roles ON UserRoles.RoleId = Roles.RoleId WHERE Username LIKE '%sale%' OR Username LIKE '%owner%'", (err, rows) => {
    console.log("Users:", rows);
  });
});
db.close();
