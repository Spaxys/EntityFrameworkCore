// Copyright (c) Microsoft Open Technologies, Inc.
// All Rights Reserved
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// THIS CODE IS PROVIDED *AS IS* BASIS, WITHOUT WARRANTIES OR
// CONDITIONS OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING
// WITHOUT LIMITATION ANY IMPLIED WARRANTIES OR CONDITIONS OF
// TITLE, FITNESS FOR A PARTICULAR PURPOSE, MERCHANTABLITY OR
// NON-INFRINGEMENT.
// See the Apache 2 License for the specific language governing
// permissions and limitations under the License.

using System;
using Microsoft.Data.Migrations.Model;
using Microsoft.Data.Relational.Model;
using System.Collections.Generic;
using Xunit;

namespace Microsoft.Data.SqlServer.Tests
{
    public class SqlServerMigrationOperationSqlGeneratorTest
    {
        [Fact]
        public void Generate_when_create_database_operation()
        {
            Assert.Equal(
                @"IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = N'MyDatabase')
    CREATE DATABASE ""MyDatabase""",
                SqlServerMigrationOperationSqlGenerator.Generate(new CreateDatabaseOperation("MyDatabase"), generateIdempotentSql: true).Sql);
        }

        [Fact]
        public void Generate_when_drop_database_operation()
        {
            Assert.Equal(
                @"IF EXISTS (SELECT * FROM sys.databases WHERE name = N'MyDatabase')
    DROP DATABASE ""MyDatabase""",
                SqlServerMigrationOperationSqlGenerator.Generate(new DropDatabaseOperation("MyDatabase"), generateIdempotentSql: true).Sql);
        }

        [Fact]
        public void Generate_when_create_sequence_operation_and_idempotent()
        {
            Assert.Equal(
                @"IF NOT EXISTS (SELECT * FROM sys.sequences WHERE name = N'MySequence' AND schema_id = SCHEMA_ID(N'dbo'))
    CREATE SEQUENCE ""dbo"".""MySequence"" AS BIGINT START WITH 0 INCREMENT BY 1",
                SqlServerMigrationOperationSqlGenerator.Generate(new CreateSequenceOperation(new Sequence("dbo.MySequence")), generateIdempotentSql: true).Sql);
        }

        [Fact]
        public void Generate_when_drop_sequence_operation_and_idempotent()
        {
            Assert.Equal(
                @"IF EXISTS (SELECT * FROM sys.sequences WHERE name = N'MySequence' AND schema_id = SCHEMA_ID(N'dbo'))
    DROP SEQUENCE ""dbo"".""MySequence""",
                SqlServerMigrationOperationSqlGenerator.Generate(new DropSequenceOperation("dbo.MySequence"), generateIdempotentSql: true).Sql);
        }

        [Fact]
        public void Generate_when_create_table_operation()
        {
            Column foo, bar;
            var table = new Table("dbo.MyTable",
                new[]
                    {
                        foo = new Column("Foo", "int") { IsNullable = false, DefaultValue = 5 },
                        bar = new Column("Bar", "int") { IsNullable = true }
                    })
                {
                    PrimaryKey = new PrimaryKey("MyPK", new[] { foo, bar }, isClustered: false)
                };

            Assert.Equal(
                @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = N'MyTable' AND schema_id = SCHEMA_ID(N'dbo'))
    CREATE TABLE ""dbo"".""MyTable"" (
        ""Foo"" int NOT NULL DEFAULT 5,
        ""Bar"" int
        CONSTRAINT ""MyPK"" PRIMARY KEY NONCLUSTERED (""Foo"", ""Bar"")
    )",
                SqlServerMigrationOperationSqlGenerator.Generate(
                    new CreateTableOperation(table), generateIdempotentSql: true).Sql);
        }

        [Fact]
        public void Generate_when_drop_table_operation()
        {
            Assert.Equal(
                @"IF EXISTS (SELECT * FROM sys.tables WHERE name = N'MyTable' AND schema_id = SCHEMA_ID(N'dbo'))
    DROP TABLE ""dbo"".""MyTable""",
                SqlServerMigrationOperationSqlGenerator.Generate(new DropTableOperation("dbo.MyTable"), generateIdempotentSql: true).Sql);
        }

        [Fact]
        public void Generate_when_rename_table_operation()
        {
            Assert.Equal(
                @"IF EXISTS (SELECT * FROM sys.tables WHERE name = N'MyTable' AND schema_id = SCHEMA_ID(N'dbo'))
    EXECUTE sp_rename @objname = N'dbo.MyTable', @newname = N'MyTable2', @objtype = N'OBJECT'",
                SqlServerMigrationOperationSqlGenerator.Generate(new RenameTableOperation("dbo.MyTable", "MyTable2"), generateIdempotentSql: true).Sql);
        }

        [Fact]
        public void Generate_when_move_table_operation()
        {
            Assert.Equal(
                @"IF EXISTS (SELECT * FROM sys.tables WHERE name = N'MyTable' AND schema_id = SCHEMA_ID(N'dbo'))
    ALTER SCHEMA ""dbo2"" TRANSFER ""dbo"".""MyTable""",
                SqlServerMigrationOperationSqlGenerator.Generate(new MoveTableOperation("dbo.MyTable", "dbo2"), generateIdempotentSql: true).Sql);
        }

        [Fact]
        public void Generate_when_add_column_operation()
        {
            var column = new Column("Bar", "int") { IsNullable = false, DefaultValue = 5 };

            Assert.Equal(
                @"IF NOT EXISTS (SELECT * FROM sys.columns WHERE name = N'Bar' AND object_id = OBJECT_ID(N'dbo.MyTable'))
    ALTER TABLE ""dbo"".""MyTable"" ADD ""Bar"" int NOT NULL DEFAULT 5",
                SqlServerMigrationOperationSqlGenerator.Generate(new AddColumnOperation("dbo.MyTable", column), generateIdempotentSql: true).Sql);
        }

        [Fact]
        public void Generate_when_drop_column_operation()
        {
            Assert.Equal(
                @"IF EXISTS (SELECT * FROM sys.columns WHERE name = N'Foo' AND object_id = OBJECT_ID(N'dbo.MyTable'))
    ALTER TABLE ""dbo"".""MyTable"" DROP COLUMN ""Foo""",
                SqlServerMigrationOperationSqlGenerator.Generate(new DropColumnOperation("dbo.MyTable", "Foo"), generateIdempotentSql: true).Sql);
        }

        [Fact]
        public void Generate_when_alter_column_operation()
        {
            Assert.Equal(
                @"IF EXISTS (SELECT * FROM sys.columns WHERE name = N'Foo' AND object_id = OBJECT_ID(N'dbo.MyTable'))
    ALTER TABLE ""dbo"".""MyTable"" ALTER COLUMN ""Foo"" int NOT NULL",
                SqlServerMigrationOperationSqlGenerator.Generate(
                    new AlterColumnOperation("dbo.MyTable", new Column("Foo", "int") { IsNullable = false },
                        isDestructiveChange: false), generateIdempotentSql: true).Sql);
        }

        [Fact]
        public void Generate_when_add_default_constraint_operation()
        {
            Assert.Equal(
                @"IF NOT EXISTS (SELECT * FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.MyTable') AND COL_NAME(parent_object_id, parent_column_id) = N'Foo')
    ALTER TABLE ""dbo"".""MyTable"" ADD CONSTRAINT ""DF_dbo.MyTable_Foo"" DEFAULT 5 FOR ""Foo""",
                SqlServerMigrationOperationSqlGenerator.Generate(
                    new AddDefaultConstraintOperation("dbo.MyTable", "Foo", 5, null), generateIdempotentSql: true).Sql);
        }

        [Fact]
        public void Generate_when_drop_default_constraint_operation()
        {
            Assert.Equal(
                @"DECLARE @var0 nvarchar(128)
SELECT @var0 = name FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.MyTable') AND COL_NAME(parent_object_id, parent_column_id) = N'Foo'
IF @var0 IS NOT NULL
    EXECUTE('ALTER TABLE ""dbo"".""MyTable"" DROP CONSTRAINT ""' + @var0 + '""')",
                SqlServerMigrationOperationSqlGenerator.Generate(
                    new DropDefaultConstraintOperation("dbo.MyTable", "Foo"), generateIdempotentSql: true).Sql);
        }

        [Fact]
        public void Generate_when_rename_column_operation()
        {
            Assert.Equal(
                @"IF EXISTS (SELECT * FROM sys.columns WHERE name = N'Foo' AND object_id = OBJECT_ID(N'dbo.MyTable'))
    EXECUTE sp_rename @objname = N'dbo.MyTable.Foo', @newname = N'Foo2', @objtype = N'COLUMN'",
                SqlServerMigrationOperationSqlGenerator.Generate(
                    new RenameColumnOperation("dbo.MyTable", "Foo", "Foo2"), generateIdempotentSql: true).Sql);
        }

        [Fact]
        public void Generate_when_add_primary_key_operation()
        {
            Assert.Equal(
                @"IF NOT EXISTS (SELECT * FROM sys.key_constraints WHERE type = 'PK' AND parent_object_id = OBJECT_ID(N'dbo.MyTable'))
    ALTER TABLE ""dbo"".""MyTable"" ADD CONSTRAINT ""MyPK"" PRIMARY KEY NONCLUSTERED (""Foo"", ""Bar"")",
                SqlServerMigrationOperationSqlGenerator.Generate(
                    new AddPrimaryKeyOperation("dbo.MyTable", "MyPK", new[] { "Foo", "Bar" }, isClustered: false),
                    generateIdempotentSql: true).Sql);
        }

        [Fact]
        public void Generate_when_drop_primary_key_operation()
        {
            Assert.Equal(
                @"IF EXISTS (SELECT * FROM sys.key_constraints WHERE type = 'PK' AND name = N'MyPK' AND parent_object_id = OBJECT_ID(N'dbo.MyTable'))
    ALTER TABLE ""dbo"".""MyTable"" DROP CONSTRAINT ""MyPK""",
                SqlServerMigrationOperationSqlGenerator.Generate(new DropPrimaryKeyOperation("dbo.MyTable", "MyPK"), generateIdempotentSql: true).Sql);
        }

        [Fact]
        public void Generate_when_add_foreign_key_operation()
        {
            Assert.Equal(
                @"IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = N'MyFK' AND parent_object_id = OBJECT_ID(N'dbo.MyTable'))
    ALTER TABLE ""dbo"".""MyTable"" ADD CONSTRAINT ""MyFK"" FOREIGN KEY (""Foo"", ""Bar"") REFERENCES ""dbo"".""MyTable2"" (""Foo2"", ""Bar2"") ON DELETE CASCADE",
                SqlServerMigrationOperationSqlGenerator.Generate(
                    new AddForeignKeyOperation("dbo.MyTable", "MyFK", new[] { "Foo", "Bar" },
                        "dbo.MyTable2", new[] { "Foo2", "Bar2" }, cascadeDelete: true),
                    generateIdempotentSql: true).Sql);
        }

        [Fact]
        public void Generate_when_drop_foreign_key_operation()
        {
            Assert.Equal(
                @"IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = N'MyFK' AND parent_object_id = OBJECT_ID(N'dbo.MyTable2'))
    ALTER TABLE ""dbo"".""MyTable2"" DROP CONSTRAINT ""MyFK""",
                SqlServerMigrationOperationSqlGenerator.Generate(new DropForeignKeyOperation("dbo.MyTable2", "MyFK"), generateIdempotentSql: true).Sql);
        }

        [Fact]
        public void Generate_when_create_index_operation()
        {
            Assert.Equal(
                @"IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'MyIndex' AND object_id = OBJECT_ID(N'dbo.MyTable'))
    CREATE UNIQUE CLUSTERED INDEX ""MyIndex"" ON ""dbo"".""MyTable"" (""Foo"", ""Bar"")",
                SqlServerMigrationOperationSqlGenerator.Generate(
                    new CreateIndexOperation("dbo.MyTable", "MyIndex", new[] { "Foo", "Bar" },
                        isUnique: true, isClustered: true), generateIdempotentSql: true).Sql);
        }

        [Fact]
        public void Generate_when_drop_index_operation()
        {
            Assert.Equal(
                @"IF EXISTS (SELECT * FROM sys.indexes WHERE name = N'MyIndex' AND object_id = OBJECT_ID(N'dbo.MyTable'))
    DROP INDEX ""MyIndex"" ON ""dbo"".""MyTable""",
                SqlServerMigrationOperationSqlGenerator.Generate(new DropIndexOperation("dbo.MyTable", "MyIndex"), generateIdempotentSql: true).Sql);
        }

        [Fact]
        public void Generate_when_rename_index_operation()
        {
            Assert.Equal(
                @"IF EXISTS (SELECT * FROM sys.indexes WHERE name = N'MyIndex' AND object_id = OBJECT_ID(N'dbo.MyTable'))
    EXECUTE sp_rename @objname = N'dbo.MyTable.MyIndex', @newname = N'MyIndex2', @objtype = N'INDEX'",
                SqlServerMigrationOperationSqlGenerator.Generate(
                    new RenameIndexOperation("dbo.MyTable", "MyIndex", "MyIndex2"), generateIdempotentSql: true).Sql);
        }

        [Fact]
        public void GenerateDataType_for_string_thats_not_a_key()
        {
            Assert.Equal(
                "nvarchar(max)", 
                new SqlServerMigrationOperationSqlGenerator().GenerateDataType(CreateColumn(typeof(string))));
        }

        [Fact]
        public void GenerateDataType_for_string_key()
        {
            var sqlGenerator = new SqlServerMigrationOperationSqlGenerator();

            var column = new Column("Username", typeof(string));
            var table = new Table("dbo.Users");
            table.PrimaryKey = new PrimaryKey("PK_Users", new List<Column>() { column }.AsReadOnly());
            table.AddColumn(column);

            Assert.Equal("nvarchar(128)", sqlGenerator.GenerateDataType(column));
        }

        [Fact]
        public void GenerateDataType_for_DateTime()
        {
            Assert.Equal(
                "datetime2",
                new SqlServerMigrationOperationSqlGenerator().GenerateDataType(CreateColumn(typeof(DateTime))));
        }

        [Fact]
        public void GenerateDataType_for_decimal()
        {
            Assert.Equal(
                "decimal(18, 2)",
                new SqlServerMigrationOperationSqlGenerator().GenerateDataType(CreateColumn(typeof(decimal))));
        }

        [Fact]
        public void GenerateDataType_for_Guid()
        {
            Assert.Equal(
                "uniqueidentifier",
                new SqlServerMigrationOperationSqlGenerator().GenerateDataType(CreateColumn(typeof(Guid))));
        }

        [Fact]
        public void GenerateDataType_for_bool()
        {
            Assert.Equal(
                "bit",
                new SqlServerMigrationOperationSqlGenerator().GenerateDataType(CreateColumn(typeof(bool))));
        }

        [Fact]
        public void GenerateDataType_for_byte()
        {
            Assert.Equal(
                "tinyint",
                new SqlServerMigrationOperationSqlGenerator().GenerateDataType(CreateColumn(typeof(byte))));
        }

        [Fact]
        public void GenerateDataType_for_char()
        {
            Assert.Equal(
                "int",
                new SqlServerMigrationOperationSqlGenerator().GenerateDataType(CreateColumn(typeof(char))));
        }

        [Fact]
        public void GenerateDataType_for_double()
        {
            Assert.Equal(
                "float",
                new SqlServerMigrationOperationSqlGenerator().GenerateDataType(CreateColumn(typeof(double))));
        }

        [Fact]
        public void GenerateDataType_for_short()
        {
            Assert.Equal(
                "smallint",
                new SqlServerMigrationOperationSqlGenerator().GenerateDataType(CreateColumn(typeof(short))));
        }

        [Fact]
        public void GenerateDataType_for_long()
        {
            Assert.Equal(
                "bigint",
                new SqlServerMigrationOperationSqlGenerator().GenerateDataType(CreateColumn(typeof(long))));
        }

        [Fact]
        public void GenerateDataType_for_sbyte()
        {
            Assert.Equal(
                "smallint",
                new SqlServerMigrationOperationSqlGenerator().GenerateDataType(CreateColumn(typeof(sbyte))));
        }

        [Fact]
        public void GenerateDataType_for_float()
        {
            Assert.Equal(
                "real",
                new SqlServerMigrationOperationSqlGenerator().GenerateDataType(CreateColumn(typeof(float))));
        }

        [Fact]
        public void GenerateDataType_for_ushort()
        {
            Assert.Equal(
                "int",
                new SqlServerMigrationOperationSqlGenerator().GenerateDataType(CreateColumn(typeof(ushort))));
        }

        [Fact]
        public void GenerateDataType_for_uint()
        {
            Assert.Equal(
                "bigint",
                new SqlServerMigrationOperationSqlGenerator().GenerateDataType(CreateColumn(typeof(uint))));
        }

        [Fact]
        public void GenerateDataType_for_ulong()
        {
            Assert.Equal(
                "numeric(20, 0)",
                new SqlServerMigrationOperationSqlGenerator().GenerateDataType(CreateColumn(typeof(ulong))));
        }

        [Fact]
        public void GenerateDataType_for_DateTimeOffset()
        {
            Assert.Equal(
                "datetimeoffset",
                new SqlServerMigrationOperationSqlGenerator().GenerateDataType(CreateColumn(typeof(DateTimeOffset))));
        }

        [Fact]
        public void GenerateDataType_for_byte_array_that_is_not_a_concurrency_token_or_a_primary_key()
        {
            Assert.Equal(
                "varbinary(max)",
                new SqlServerMigrationOperationSqlGenerator().GenerateDataType(CreateColumn(typeof(byte[]))));
        }

        [Fact]
        public void GenerateDataType_for_byte_array_key()
        {
            var column = new Column("Username", typeof(byte[]));
            var table = new Table("dbo.Users") { PrimaryKey = new PrimaryKey("PK_Users", new[] { column }) };
            table.AddColumn(column);

            Assert.Equal("varbinary(128)", new SqlServerMigrationOperationSqlGenerator().GenerateDataType(column));
        }

        [Fact]
        public void GenerateDataType_for_byte_array_concurrency_token()
        {
            var column = new Column("Username", typeof(byte[])) { IsTimestamp = true };
            var table = new Table("dbo.Users");
            table.AddColumn(column);

            Assert.Equal("rowversion", new SqlServerMigrationOperationSqlGenerator().GenerateDataType(column));
        }

        private static Column CreateColumn(Type clrType)
        {
            var column = new Column("Username", clrType);
            var table = new Table("dbo.Users");
            table.AddColumn(column);
            return column;
        }

        [Fact]
        public void Delimit_identifier()
        {
            var sqlGenerator = new SqlServerMigrationOperationSqlGenerator();

            Assert.Equal("\"foo\"\"bar\"", sqlGenerator.DelimitIdentifier("foo\"bar"));
        }

        [Fact]
        public void Escape_identifier()
        {
            var sqlGenerator = new SqlServerMigrationOperationSqlGenerator();

            Assert.Equal("foo\"\"bar", sqlGenerator.EscapeIdentifier("foo\"bar"));
        }

        [Fact]
        public void Delimit_literal()
        {
            var sqlGenerator = new SqlServerMigrationOperationSqlGenerator();

            Assert.Equal("'foo''bar'", sqlGenerator.DelimitLiteral("foo'bar"));
        }

        [Fact]
        public void Escape_literal()
        {
            var sqlGenerator = new SqlServerMigrationOperationSqlGenerator();

            Assert.Equal("foo''bar", sqlGenerator.EscapeLiteral("foo'bar"));
        }
    }
}
