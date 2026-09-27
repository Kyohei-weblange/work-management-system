Imports Microsoft.AspNetCore.Builder
Imports Microsoft.Extensions.DependencyInjection
Imports Microsoft.Extensions.Hosting
Imports Microsoft.Data.Sqlite
Imports Microsoft.AspNetCore.Http
Imports BCrypt.Net

Public Class LoginRequest
    Public Property UserName As String
    Public Property Password As String
End Class

Module Program
    Sub Main(args As String())
        Dim builder = WebApplication.CreateBuilder(args)
        Dim app = builder.Build()

        app.UseDefaultFiles()
        app.UseStaticFiles()

        Dim connectionString As String = "Data Source = work_management.db"

        ' DB初期化処理（起動時に1回だけ自動実行）
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

            command.CommandText = "SELECT COUNT(*) FROM Users"
            Dim count As Integer = Convert.ToInt32(command.ExecuteScalar())
            If count = 0 Then
                Dim hash = BCrypt.Net.BCrypt.HashPassword("password123")
                command.CommandText = "INSERT INTO Roles (RoleName) VALUES ('Admin'), ('User')"
                command.ExecuteNonQuery()
                command.CommandText = "INSERT INTO Users (UserName, PasswordHash, RoleId) VALUES ('admin', @hash, 1)"
                command.Parameters.AddWithValue("@hash", hash)
                command.ExecuteNonQuery()
            End If
        End Using

        app.MapGet("/api/health", Function() As IResult
            Try
                Dim response = New With {Key .message = "接続成功"}
                Return Results.Json(response)
            Catch ex As Exception
                Dim response = New With {Key .message = "接続失敗:" & ex.Message}
                Return Results.Json(response)
            End Try
        End Function)

        app.MapPost("/api/login", Function(req As LoginRequest) As IResult
            Dim username = req.UserName
            Dim password = req.Password

            Using connection As New SqliteConnection(connectionString)
                connection.Open()
                Dim command = connection.CreateCommand()
                command.CommandText = "SELECT PasswordHash FROM Users WHERE UserName = @username"
                command.Parameters.AddWithValue("@username", username)

                Dim storedPasswordHash = command.ExecuteScalar()?.ToString()

                Console.WriteLine($"[DEBUG] DBのハッシュ: {storedPasswordHash}")

                If storedPasswordHash IsNot Nothing AndAlso BCrypt.Net.BCrypt.Verify(password, storedPasswordHash) Then
                    Dim response = New With {Key .message = "ログイン成功", .userName = username}
                    Return Results.Json(response)
                Else
                    Dim response = New With {Key .message = "ログイン失敗: ユーザー名またはパスワードが正しくありません。"}
                    Return Results.Json(response, statusCode:=401)
                End If
            End Using
        End Function)

        app.Run()
    End Sub
End Module
