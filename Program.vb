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

Public Class ConstructionDto
    Public Property ConstructionId As Integer
    Public Property ConstructionCode As String
    Public Property ConstructionName As String
End Class

Public Class WorkRecordRequest
    Public Property UserId As Integer
    Public Property WorkDate As String
    Public Property StartTime As String
    Public Property EndTime As String
    Public Property OvertimeHours As Double
    Public Property ConstructionId As Integer
    Public Property Remarks As String
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

            command.CommandText = "SELECT COUNT(*) FROM ConstructionNumbers"
            Dim countNum As Integer = Convert.ToInt32(command.ExecuteScalar())
            If countNum = 0 Then
                command.commandText = "INSERT INTO ConstructionNumbers (ConstructionCode, ConstructionName, IsActive) VALUES ('C101', 'A社工場耐震改修工事', 1)"
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

        ' 工事番号一覧取得API
        app.MapGet("/api/constructions", Function() As IResult
            Dim constructions As New List(Of ConstructionDto)
            Using connection As New SqliteConnection(connectionString)
                connection.Open()
                Dim command = connection.CreateCommand()
                command.CommandText = "
                    SELECT
                        ConstructionId,
                        ConstructionCode,
                        ConstructionName
                    FROM
                        ConstructionNumbers
                    WHERE
                        IsActive = 1
                "
                Using reader = command.ExecuteReader()
                    While reader.Read()
                        constructions.Add(New ConstructionDto With {
                            .ConstructionId = reader.GetInt32(0),
                            .ConstructionCode = reader.GetString(1),
                            .ConstructionName = reader.GetString(2)
                        })
                    End While
                End Using
            End Using
            Return Results.Json(constructions)
        End Function)

        ' 勤務データ登録API
        app.MapPost("/api/work-records", Function(req As WorkRecordRequest) As IResult
            Using connection As New SqliteConnection(connectionString)
                connection.Open()
                Dim command = connection.CreateCommand()
                command.CommandText = "
                    INSERT INTO WorkRecords (
                        UserId,
                        WorkDate,
                        StartTime,
                        EndTime,
                        OvertimeHours,
                        ConstructionId,
                        Remarks
                    )
                    VALUES (
                        @userId,
                        @workDate,
                        @startTime,
                        @endTime,
                        @overtimeHours,
                        @constructionId,
                        @remarks
                    )
                "
                command.Parameters.AddWithValue("@userId", req.UserId)
                command.Parameters.AddWithValue("@workDate", req.WorkDate)
                command.Parameters.AddWithValue("@startTime", req.StartTime)
                command.Parameters.AddWithValue("@endTime", req.EndTime)
                command.Parameters.AddWithValue("@overtimeHours", req.OvertimeHours)
                command.Parameters.AddWithValue("@constructionId", req.ConstructionId)
                command.Parameters.AddWithValue("@remarks", req.Remarks)
                Try
                    command.ExecuteNonQuery()
                    Return Results.Json(New With {Key .message = "勤務データを登録しました。"})
                Catch ex As Exception
                    Return Results.BadRequest(New With {Key .message = "登録失敗:" & ex.Message})
                End Try
            End Using
        End Function)

        app.Run()
    End Sub
End Module
