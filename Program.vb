Imports Microsoft.AspNetCore.Builder
Imports Microsoft.Extensions.DependencyInjection
Imports Microsoft.Extensions.Hosting
Imports Microsoft.Data.Sqlite
Imports Microsoft.AspNetCore.Http

Module Program
    Sub Main(args As String())
        Dim builder = WebApplication.CreateBuilder(args)
        Dim app = builder.Build()

        app.UseDefaultFiles()
        app.UseStaticFiles()

        Dim connectionString As String = "Data Source = work_management.db"

        app.MapGet("/api/health", Function() As IResult
            Try
            'DB初期化
                Using connection As New SqliteConnection(connectionString)
                    connection.Open()
                    Dim command = connection.CreateCommand()
                    command.CommandText = "
                        CREATE TABLE IF NOT EXISTS Roles (
                            RoleId INTEGER PRIMARY KEY AUTOINCREMENT,
                            RoleName TEXT NOT NULL
                        );
                        CREATE TABLE IF NOT EXISTS Users (
                            UserId INTEGER PRIMARY KEY AUTOINCREMENT,
                            UserName TEXT NOT NULL UNIQUE,
                            PasswordHash TEXT NOT NULL,
                            RoleId INTEGER REFERENCES Roles(RoleId)
                        );
                        CREATE TABLE IF NOT EXISTS ConstructionNumbers (
                            ConstructionId INTEGER PRIMARY KEY AUTOINCREMENT,
                            ConstructionCode TEXT NOT NULL UNIQUE,
                            ConstructionName TEXT NOT NULL,
                            IsActive INTEGER NOT NULL DEFAULT 1
                        );
                        CREATE TABLE IF NOT EXISTS WorkRecords (
                            RecordId INTEGER PRIMARY KEY AUTOINCREMENT,
                            UserId INTEGER REFERENCES Users(UserId),
                            WorkDate TEXT NOT NULL,
                            StartTime TEXT,
                            EndTime TEXT,
                            OvertimeHours REAL DEFAULT 0.0,
                            ConstructionId INTEGER REFERENCES ConstructionNumbers(ConstructionId),
                            Remarks TEXT
                        );
                    "
                    command.ExecuteNonQuery()
                End Using
                Dim response = New With {Key .message = "接続成功"}
                Return Results.Json(response)
            Catch ex As Exception
                Dim response = New  With {Key .message = "接続失敗:" & ex.Message}
                Return Results.Json(response)
            End Try
        End Function)

        app.Run()
    End Sub
End Module
