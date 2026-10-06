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
    Public Property TotalHours As Double
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

Public Class CopyPrevious
    Public Property UserId As Integer
    Public Property TargetYearMonth As String
End Class

Public Class WorkRecordDetailDto
    Public Property WorkDate As String
    Public Property StartTime As String
    Public Property EndTime As String
    Public Property OvertimeHours As Double
    Public Property ConstructionCode As String
    Public Property ConstructionName As String
    Public Property Note As String
End Class

Public Class WorkRecordService
    Private ReadOnly _connectionString As String
    Public Sub New(connectionString As String)
        _connectionString = connectionString
    End Sub
    Public Function GetMonthlyRecords(userId As Integer, yearMonth As String) As List(Of WorkRecordDetailDto)
    Dim yearMonthDate As String = yearMonth & "%"
    Dim result As New List(Of WorkRecordDetailDto)()
    Using connection As New SqliteConnection(_connectionString)
        connection.Open()
        Dim command = connection.CreateCommand()
        command.CommandText = "
            SELECT
                WorkDate,
                StartTime,
                EndTime,
                OvertimeHours,
                ConstructionCode,
                ConstructionName
            FROM
                WorkRecords LEFT JOIN ConstructionNumbers ON WorkRecords.ConstructionId = ConstructionNumbers.ConstructionId
            WHERE
                WorkRecords.UserId = @userId AND WorkRecords.WorkDate LIKE @yearMonthDate ORDER BY WorkRecords.WorkDate ASC
        "
        command.Parameters.AddWithValue("@userId", userId)
        command.Parameters.AddWithValue("@yearMonthDate", yearMonthDate)
        Using reader = command.ExecuteReader()
            While reader.Read()
                result.Add(New WorkRecordDetailDto With {
                    .WorkDate  = reader.GetString(0),
                    .StartTime = reader.GetString(1),
                    .EndTime = reader.GetString(2),
                    .OvertimeHours = reader.GetDouble(3),
                    .ConstructionCode = If(reader.IsDBNull(4), "", reader.GetString(4)),
                    .ConstructionName = If(reader.IsDBNull(5), "", reader.GetString(5))
                })
            End While
        End Using
    End Using
    Return result
    End Function
End Class

Module Program
    Sub Main(args As String())

        Dim builder = WebApplication.CreateBuilder(args)
        Dim connectionString As String = "Data Source = work_management.db"
        builder.Services.AddScoped(Of WorkRecordService)(Function(sp)
            Return New WorkRecordService(connectionString)
        End Function)
        Dim app = builder.Build()

        app.UseDefaultFiles()
        app.UseStaticFiles()


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

        ' 前月の勤務内容をコピー
        app.MapPost("/api/work-records/copy-previous", Function(req As CopyPrevious) As IResult

            Dim userId = req.UserId
            Dim targetYearMonth = req.TargetYearMonth
            Dim targetDate As DateTime = DateTime.Parse(targetYearMonth & "-1")
            Dim prevMonthStr As String = targetDate.AddMonths(-1).ToString("yyyy-MM")
            Dim targetMonthPattern As String = targetYearMonth & "%"

            Using connection As New SqliteConnection(connectionString)
                connection.Open()
                Dim command = connection.CreateCommand()
                command.CommandText = "
                    DELETE FROM WorkRecords WHERE UserId = @userId AND WorkDate LIKE @targetMonthPattern;
                    INSERT INTO WorkRecords (UserId, WorkDate, StartTime, EndTime, OvertimeHours, ConstructionId, Remarks)
                    SELECT
                        UserId,
                        replace(WorkDate, @prevMonthStr, @targetYearMonth) AS WorkDate,
                        StartTime,
                        EndTime,
                        OvertimeHours,
                        ConstructionId,
                        Remarks
                    FROM WorkRecords
                    WHERE UserId = @userId AND WorkDate LIKE @prevMonthPattern
                "
                command.Parameters.AddWithValue("@userId", userId)
                command.Parameters.AddWithValue("@prevMonthStr", prevMonthStr)
                command.Parameters.AddWithValue("@targetYearMonth", targetYearMonth)
                command.Parameters.AddWithValue("@prevMonthPattern", prevMonthStr & "%")
                command.Parameters.AddWithValue("@targetMonthPattern", targetMonthPattern)
                Try
                    Dim copiedCount As Integer = command.ExecuteNonQuery()
                    Return Results.Json(New With {Key .message = "成功しました。", .copiedCount = copiedCount})
                Catch ex As Exception
                    Return Results.BadRequest(New With {Key .message = "失敗しました。" & ex.Message})
                End Try
            End Using
        End Function)

        app.MapGet("/api/work-records/summary", Function(userId As Integer, yearMonth As String) As IResult
            Dim totalWorkDays As Integer = 0
            Dim totalOvertimeHours As Double = 0.0
            Dim yearMonthPattern As String = yearMonth & "%"
            Dim constructionHours As New Dictionary(Of Integer, Double)
            Dim breakdownList As New List(Of ConstructionDto)

            Using connection As New SqliteConnection(connectionString)
                connection.Open()
                Dim command = connection.CreateCommand()
                command.CommandText = "
                    SELECT
                        WorkDate,
                        OvertimeHours,
                        ConstructionId
                    FROM
                        WorkRecords
                    WHERE
                        UserId = @userId
                    AND
                        WorkDate LIKE @yearMonthPattern
                "
                command.Parameters.AddWithValue("@userId", userId)
                command.Parameters.AddWithValue("@yearMonthPattern", yearMonthPattern)
                Using reader = command.ExecuteReader()
                    While reader.Read()
                        totalWorkDays += 1
                        totalOvertimeHours += reader.GetDouble(1)
                        Dim overtime As Double = reader.GetDouble(1)
                        If Not reader.IsDBNull(2) Then
                            Dim constructionId As Integer = reader.GetInt32(2)
                            If constructionHours.ContainsKey(ConstructionId) Then
                                constructionHours(ConstructionId) += overtime
                            Else
                                constructionHours(ConstructionId) = overtime
                            End If
                        End If
                    End While
                End Using

                command.Parameters.Clear()
                command.CommandText = "
                    SELECT ConstructionId, ConstructionCode, ConstructionName
                    FROM ConstructionNumbers
                    WHERE IsActive = 1
                "
                Using reader = command.ExecuteReader()
                    While reader.Read()
                        Dim cId As Integer = reader.GetInt32(0)
                        Dim cCode As String = reader.GetString(1)
                        Dim cName As String = reader.GetString(2)
                        Dim hours As Double = 0.0
                        If constructionHours.ContainsKey(cId) Then
                            hours = constructionHours(cId)
                        End If
                        breakdownList.Add(New ConstructionDto With {
                            .ConstructionId = cId,
                            .ConstructionCode = cCode,
                            .ConstructionName = cName,
                            .TotalHours = hours
                        })
                    End While
                End Using
            End Using
            Return Results.Json(New With { Key .totalWorkDays = totalWorkDays, Key .totalOvertimeHours = totalOvertimeHours, Key .constructionBreakdown = breakdownList})
        End Function)

        app.MapGet("/api/work-records/export-csv", Function(userId As Integer, yearMonth As String, context As HttpContext, service As WorkRecordService) As IResult
            context.Response.Headers("Content-Disposition") = $"attachment; filename=""work_records_{yearMonth}.csv"""
            Dim records = service.GetMonthlyRecords(userId, yearMonth)
            Dim csvLines As New List(Of String)()
            csvLines.Add("日付,出勤時間,退勤時間,残業時間,工事コード,工事名")
            For Each item In records
                Dim line As String = $"{item.WorkDate},{item.StartTime},{item.EndTime},{item.OvertimeHours},{item.ConstructionCode},{item.ConstructionName}"
                csvLines.Add(line)
            Next
            Dim csvData As String = String.Join(vbCrlf, csvLines)
            Return Results.Text(csvData, contentType:="text/csv; charset=utf-8", System.Text.Encoding.UTF8)
        End Function)

        app.Run()
    End Sub
End Module
