using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace YTStudio.Core.Data;

/// <summary>Usado só pelo <c>dotnet ef</c> para gerar migrações (banco sem chave, temporário).</summary>
internal sealed class DesignTimeFactory : IDesignTimeDbContextFactory<StudioDbContext>
{
    public StudioDbContext CreateDbContext(string[] args)
    {
        SQLitePCL.Batteries_V2.Init();
        return new StudioDbContext(new DbContextOptionsBuilder<StudioDbContext>().UseSqlite("Data Source=design.db").Options);
    }
}
