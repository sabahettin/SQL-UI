namespace SqlUI.Core;

// Her motorun hangi SSMS-benzeri ozellikleri desteklediğini işaretler; UI bu bayraklara
// bakarak örn. "Execution Plan" veya "Databases" düğümünü göstermeyi/gizlemeyi karar verir.
[Flags]
public enum DbProviderCapabilities
{
    None = 0,
    MultipleDatabases = 1 << 0,  // SQL Server/Postgres/MySQL: evet, SQLite: hayir (dosya = veritabani)
    Schemas = 1 << 1,            // SQL Server/Postgres: evet, SQLite/MySQL: hayir
    StoredProcedures = 1 << 2,   // SQL Server/Postgres/MySQL: evet, SQLite: hayir
    ExecutionPlan = 1 << 3,      // SQL Server/Postgres: evet
    ServerAuthentication = 1 << 4, // SQL Server/Postgres/MySQL: evet (kullanici/parola), SQLite: hayir
    DatabaseManagement = 1 << 5, // Yeni veritabani/backup/restore/rename/delete: su an sadece SQL Server
}
