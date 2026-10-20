Imports Microsoft.AspNetCore.Builder
Imports Microsoft.Extensions.DependencyInjection
Imports Microsoft.Extensions.Hosting
Imports Microsoft.Data.Sqlite
Imports Microsoft.AspNetCore.Http
Imports BCrypt.Net

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
            Dim userRepository = New UserRepository(connectionString)
            Dim storedPasswordHash = userRepository.GetPasswordHashByUsername(req.UserName)
            If storedPasswordHash IsNot Nothing AndAlso BCrypt.Net.BCrypt.Verify(req.Password, storedPasswordHash) Then
                Dim response = New With {Key .message = "ログイン成功", .userName = req.UserName}
                Return Results.Ok(response)
            Else
                Dim response = New With {Key .message = "ログイン失敗: ユーザー名またはパスワードが正しくありません。"}
                Return Results.Json(response, statusCode:=401)
            End If
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
            Dim constructionOvertimeHours As New Dictionary(Of Integer, Double)

            Using connection As New SqliteConnection(connectionString)
                connection.Open()
                Dim command = connection.CreateCommand()
                command.CommandText = "
                    SELECT
                        WorkDate,
                        OvertimeHours,
                        ConstructionId,
                        StartTime,
                        EndTime
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
                        Dim startTime = reader.GetString(3)
                        Dim endTime = reader.GetString(4)
                        If Not reader.IsDBNull(2) Then
                            Dim constructionId As Integer = reader.GetInt32(2)
                            Dim workTime As Double = (TimeSpan.Parse(endTime) - TimeSpan.Parse(startTime)).TotalHours - 1.0 + overtime
                            If constructionHours.ContainsKey(constructionId) Then
                                constructionHours(constructionId) += workTime
                            Else
                                constructionHours(constructionId) = workTime
                            End If
                            If constructionOvertimeHours.ContainsKey(constructionId) Then
                                constructionOvertimeHours(constructionId) += overTime
                            Else
                                constructionOvertimeHours(constructionId) = overTime
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
                        Dim overtimeHours As Double = 0.0
                        If constructionHours.ContainsKey(cId) Then
                            hours = constructionHours(cId)
                        End If
                        If constructionOvertimeHours.ContainsKey(cId) Then
                            overtimeHours = constructionOvertimeHours(cId)
                        End If
                        breakdownList.Add(New ConstructionDto With {
                            .ConstructionId = cId,
                            .ConstructionCode = cCode,
                            .ConstructionName = cName,
                            .TotalHours = hours,
                            .OvertimeHours = overtimeHours
                        })
                    End While
                End Using
            End Using
            Return Results.Json(New With { Key .totalWorkDays = totalWorkDays, Key .totalOvertimeHours = totalOvertimeHours, Key .constructionBreakdown = breakdownList})
        End Function)

        app.MapPost("/api/clock-in-out", Function(req As ClockInOut) As IResult
            Dim userId = req.UserId
            Dim clockType = req.ClockType
            Dim dt As DateTime = DateTime.Parse(req.Timestamp)
            Dim workDate As String = dt.ToString("yyyy-MM-dd")
            Dim clockTime As String = dt.ToString("HH:mm")
            Using connection As New SqliteConnection(connectionString)
                connection.Open()
                Dim command = connection.CreateCommand()
                command.CommandText = "
                    SELECT
                        COUNT(*)
                    FROM
                        WorkRecords
                    WHERE UserId = @userId AND WorkDate = @workDate
                "
                command.Parameters.AddWithValue("@userId", userId)
                command.Parameters.AddWithValue("@workDate", workDate)
                ' Dim result = command.ExecuteScalar()
                Dim count As Integer = Convert.ToInt32(command.ExecuteScalar())
                command.Parameters.Clear()
                If (count = 0 AndAlso clockType = "IN") Then
                    command.CommandText = "
                        INSERT INTO WorkRecords (UserId, WorkDate, StartTime) VALUES (@userId, @workDate, @startTime)
                    "
                    command.Parameters.AddWithValue("@userId", userId)
                    command.Parameters.AddWithValue("@workDate", workDate)
                    command.Parameters.AddWithValue("@startTime", clockTime)
                End If
                If (count > 0 AndAlso clockType = "IN") Then
                    command.CommandText = "
                        UPDATE  WorkRecords SET StartTime = @startTime WHERE UserId = @userId AND WorkDate = @workDate
                    "
                    command.Parameters.AddWithValue("@userId", userId)
                    command.Parameters.AddWithValue("@workDate", workDate)
                    command.Parameters.AddWithValue("@startTime", clockTime)
                End If
                If (count > 0 AndAlso clockType = "OUT") Then
                    command.CommandText = "
                        UPDATE  WorkRecords SET EndTime = @endTime WHERE UserId = @userId AND WorkDate = @workDate
                    "
                    command.Parameters.AddWithValue("@userId", userId)
                    command.Parameters.AddWithValue("@workDate", workDate)
                    command.Parameters.AddWithValue("@endTime", clockTime)
                End If
                Try
                    command.ExecuteNonQuery()
                    Return Results.Json(New With {Key .message = "打刻が完了しました。"})
                Catch ex As Exception
                    Return Results.BadRequest(New With {Key .message = "失敗姉妹sた。" & ex.Message})
                End Try
            End Using
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
