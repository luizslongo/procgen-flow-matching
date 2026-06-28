using System.Collections.Generic;
using Npgsql;

namespace c4_api.pcgFlowMatching;

// Postgres-backed generation store. Holds an injected connection string. Init()
// opens a connection (fail-fast if the database is unreachable) and creates the
// table if it does not exist. Every statement is parameterized to prevent SQL
// injection; the only string-built SQL is a fixed column list with no user input.
//
// Deviation: the kcg DataSchemaWrapperInterface (sql-client-abstraction standard)
// is not available in this standalone repo, so Npgsql is used directly. All raw
// SQL is confined to this single storage class, preserving the intent of that
// standard (one abstraction boundary, parameterized queries).
public class PostgresGenerationStore : GenerationStoreInterface
{
    public string ConnectionString;

    public void Init()
    {
        using (NpgsqlConnection connection = new NpgsqlConnection(ConnectionString))
        {
            connection.Open();
            string createSql =
                "CREATE TABLE IF NOT EXISTS generations (" +
                "id TEXT PRIMARY KEY, " +
                "created_at_unix_seconds BIGINT NOT NULL, " +
                "biome TEXT NOT NULL, " +
                "num_steps INTEGER NOT NULL, " +
                "is_repair_applied BOOLEAN NOT NULL, " +
                "total_violations INTEGER NOT NULL, " +
                "violation_rate DOUBLE PRECISION NOT NULL, " +
                "broken_pipe_horizontal_count INTEGER NOT NULL, " +
                "broken_pipe_top_left_count INTEGER NOT NULL, " +
                "broken_pipe_top_right_count INTEGER NOT NULL, " +
                "broken_bullet_bill_count INTEGER NOT NULL, " +
                "floating_enemy_count INTEGER NOT NULL, " +
                "discontinuous_ground_count INTEGER NOT NULL)";
            using (NpgsqlCommand command = new NpgsqlCommand(createSql, connection))
            {
                command.ExecuteNonQuery();
            }
        }
    }

    public void InsertGeneration(GenerationHttpOut record)
    {
        string insertSql =
            "INSERT INTO generations (id, created_at_unix_seconds, biome, num_steps, is_repair_applied, " +
            "total_violations, violation_rate, broken_pipe_horizontal_count, broken_pipe_top_left_count, " +
            "broken_pipe_top_right_count, broken_bullet_bill_count, floating_enemy_count, discontinuous_ground_count) " +
            "VALUES (@id, @created, @biome, @steps, @repair, @total, @rate, @bph, @bptl, @bptr, @bbb, @fe, @dg)";
        using (NpgsqlConnection connection = new NpgsqlConnection(ConnectionString))
        {
            connection.Open();
            using (NpgsqlCommand command = new NpgsqlCommand(insertSql, connection))
            {
                command.Parameters.AddWithValue("id", record.Id);
                command.Parameters.AddWithValue("created", record.CreatedAtUnixSeconds);
                command.Parameters.AddWithValue("biome", record.Biome);
                command.Parameters.AddWithValue("steps", record.NumSteps);
                command.Parameters.AddWithValue("repair", record.IsRepairApplied);
                command.Parameters.AddWithValue("total", record.TotalViolations);
                command.Parameters.AddWithValue("rate", record.ViolationRate);
                command.Parameters.AddWithValue("bph", record.BrokenPipeHorizontalCount);
                command.Parameters.AddWithValue("bptl", record.BrokenPipeTopLeftCount);
                command.Parameters.AddWithValue("bptr", record.BrokenPipeTopRightCount);
                command.Parameters.AddWithValue("bbb", record.BrokenBulletBillCount);
                command.Parameters.AddWithValue("fe", record.FloatingEnemyCount);
                command.Parameters.AddWithValue("dg", record.DiscontinuousGroundCount);
                command.ExecuteNonQuery();
            }
        }
    }

    public GenerationHttpOut GetGeneration(string id)
    {
        string selectSql = "SELECT " + ColumnList() + " FROM generations WHERE id = @id";
        using (NpgsqlConnection connection = new NpgsqlConnection(ConnectionString))
        {
            connection.Open();
            using (NpgsqlCommand command = new NpgsqlCommand(selectSql, connection))
            {
                command.Parameters.AddWithValue("id", id);
                using (NpgsqlDataReader reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return ReadRecord(reader);
                    }
                    return null;
                }
            }
        }
    }

    public List<GenerationHttpOut> ListGenerations()
    {
        List<GenerationHttpOut> results = new List<GenerationHttpOut>();
        string selectSql = "SELECT " + ColumnList() + " FROM generations ORDER BY created_at_unix_seconds DESC";
        using (NpgsqlConnection connection = new NpgsqlConnection(ConnectionString))
        {
            connection.Open();
            using (NpgsqlCommand command = new NpgsqlCommand(selectSql, connection))
            {
                using (NpgsqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        results.Add(ReadRecord(reader));
                    }
                }
            }
        }
        return results;
    }

    static string ColumnList()
    {
        return "id, created_at_unix_seconds, biome, num_steps, is_repair_applied, total_violations, " +
            "violation_rate, broken_pipe_horizontal_count, broken_pipe_top_left_count, " +
            "broken_pipe_top_right_count, broken_bullet_bill_count, floating_enemy_count, discontinuous_ground_count";
    }

    static GenerationHttpOut ReadRecord(NpgsqlDataReader reader)
    {
        GenerationHttpOut record = new GenerationHttpOut();
        record.Id = reader.GetString(0);
        record.CreatedAtUnixSeconds = reader.GetInt64(1);
        record.Biome = reader.GetString(2);
        record.NumSteps = reader.GetInt32(3);
        record.IsRepairApplied = reader.GetBoolean(4);
        record.TotalViolations = reader.GetInt32(5);
        record.ViolationRate = reader.GetDouble(6);
        record.BrokenPipeHorizontalCount = reader.GetInt32(7);
        record.BrokenPipeTopLeftCount = reader.GetInt32(8);
        record.BrokenPipeTopRightCount = reader.GetInt32(9);
        record.BrokenBulletBillCount = reader.GetInt32(10);
        record.FloatingEnemyCount = reader.GetInt32(11);
        record.DiscontinuousGroundCount = reader.GetInt32(12);
        return record;
    }
}
