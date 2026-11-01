using AuctionPurchase.Core.Models;
using Dapper;

namespace AuctionPurchase.Infrastructure.Data;

public class CompanyRepository(IDatabaseFactory factory)
{
    public async Task<List<Company>> GetAllAsync()
    {
        using var connection = factory.CreateConnection();
        var companies = (await connection.QueryAsync<Company>("SELECT * FROM Companies ORDER BY Name COLLATE NOCASE")).ToList();
        var links = (await connection.QueryAsync<(int CompanyId, int GardenId, string Name)>(
            @"SELECT cg.CompanyId, cg.GardenId, g.Name FROM CompanyGardens cg
              JOIN Gardens g ON g.GardenId = cg.GardenId ORDER BY g.Name COLLATE NOCASE")).ToList();
        foreach (var company in companies)
        {
            company.GardenIds = links.Where(x => x.CompanyId == company.CompanyId).Select(x => x.GardenId).ToList();
            company.GardenNames = string.Join(", ", links.Where(x => x.CompanyId == company.CompanyId).Select(x => x.Name));
        }
        return companies;
    }

    public async Task<int> SaveAsync(Company company)
    {
        company.Name = company.Name.Trim();
        if (company.Name.Length == 0) throw new InvalidOperationException("Enter a company name.");
        if (company.GardenIds.Count == 0) throw new InvalidOperationException("Select at least one garden.");
        using var connection = factory.CreateConnection();
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();
        var duplicate = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Companies WHERE lower(trim(Name)) = lower(@Name) AND CompanyId <> @CompanyId", company, transaction);
        if (duplicate > 0) throw new InvalidOperationException("A company with this name already exists.");
        var conflict = await connection.QueryFirstOrDefaultAsync<string>(
            @"SELECT g.Name || ' already belongs to ' || c.Name FROM CompanyGardens cg
              JOIN Companies c ON c.CompanyId = cg.CompanyId JOIN Gardens g ON g.GardenId = cg.GardenId
              WHERE cg.CompanyId <> @CompanyId AND cg.GardenId IN @Ids",
            new { company.CompanyId, Ids = company.GardenIds }, transaction);
        if (conflict != null) throw new InvalidOperationException(conflict + ". Edit that company to remove the garden first.");
        int id = company.CompanyId;
        if (id == 0)
            id = await connection.QuerySingleAsync<int>("INSERT INTO Companies (Name, IsActive) VALUES (@Name, @IsActive) RETURNING CompanyId", company, transaction);
        else
        {
            var changed = await connection.ExecuteAsync("UPDATE Companies SET Name=@Name, IsActive=@IsActive WHERE CompanyId=@CompanyId", company, transaction);
            if (changed == 0) throw new InvalidOperationException("This company no longer exists.");
        }
        await connection.ExecuteAsync("DELETE FROM CompanyGardens WHERE CompanyId=@Id", new { Id = id }, transaction);
        foreach (var gardenId in company.GardenIds.Distinct())
            await connection.ExecuteAsync("INSERT INTO CompanyGardens (CompanyId, GardenId) VALUES (@Id, @GardenId)", new { Id = id, GardenId = gardenId }, transaction);
        transaction.Commit();
        return id;
    }

    public async Task DeleteAsync(int id)
    {
        using var connection = factory.CreateConnection();
        await connection.ExecuteAsync("DELETE FROM Companies WHERE CompanyId=@Id", new { Id = id });
    }
}
