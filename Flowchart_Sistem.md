# Flowchart Sistem KWH Monitoring

## 1. Arsitektur Sistem Keseluruhan

```mermaid
graph TB
    subgraph Devices["📱 Devices (ESP32/Modbus Meter)"]
        D1[Device 1]
        D2[Device 2]
        D3[Device N]
    end

    subgraph MQTT["📡 MQTT Broker"]
        MB[MQTT Broker<br/>Mosquitto/EMQX]
    end

    subgraph App["💻 ASP.NET Core Application"]
        MS[MqttService<br/>Subscriber]
        AC[AnomalyAnalysisService]
        NS[NotificationService]
        EA[EnergyAggregationService]
        API[API Controllers]
        MVC[Monitoring Controllers]
    end

    subgraph DB["🗄️ SQL Server Database"]
        KWH[KWHData<br/>Real-time]
        KWHH[KWHData_History<br/>Archived]
        AL[AnomalyLogs]
        HE[HourlyEnergy]
        DE[DailyEnergy]
        ME[MonthlyEnergy]
        YE[YearlyEnergy]
        DR[DeviceRegistry]
        AU[ApplicationUsers]
        AS[AppSettings]
    end

    subgraph UI["🖥️ User Interface"]
        DASH[Dashboard Monitoring]
        CHART[Charts]
        ANOM[Anomaly Center]
        STATS[Usage Statistics]
        SET[Settings]
    end

    subgraph NOTIFY["📧 Notification Channels"]
        EMAIL[Email SMTP]
        WA[WhatsApp Wablas]
    end

    D1 -->|MQTT Publish| MB
    D2 -->|MQTT Publish| MB
    D3 -->|MQTT Publish| MB
    
    MB -->|MQTT Subscribe| MS
    MS -->|Parse & Validate| AC
    AC -->|Anomaly Detected| NS
    AC -->|Save Log| AL
    
    MS -->|Save Data| KWH
    MS -->|Check Capacity| KWHH
    MS -->|Update Registry| DR
    
    EA -->|Aggregate| HE
    EA -->|Aggregate| DE
    EA -->|Aggregate| ME
    EA -->|Aggregate| YE
    
    API -->|Query| KWH
    API -->|Query| HE
    API -->|Query| DE
    API -->|Query| ME
    
    MVC -->|Render| DASH
    MVC -->|Render| CHART
    MVC -->|Render| ANOM
    MVC -->|Render| STATS
    
    NS -->|Send Alert| EMAIL
    NS -->|Send Alert| WA
    
    DASH -->|Real-time Polling| API
    CHART -->|AJAX Request| API
    ANOM -->|Filter & Sort| API
```

---

## 2. Flow Data MQTT ke Database

```mermaid
flowchart TD
    Start([MQTT Message Received]) --> Parse[Parse JSON Payload]
    Parse --> Validate{Valid JSON?}
    
    Validate -->|No| LogError[Log Error to AppLog]
    LogError --> End([End])
    
    Validate -->|Yes| Extract[Extract Fields:<br/>deviceId, groupName,<br/>voltage, current, power]
    Extract --> Lookup{Device in<br/>DeviceRegistry?}
    
    Lookup -->|No| Register[Auto-Register Device<br/>DeviceRegistry]
    Register --> SaveData
    Lookup -->|Yes| SaveData[Save to KWHData]
    
    SaveData --> UpdateLastSeen[Update LastSeen<br/>MessageCount++]
    UpdateLastSeen --> CheckCapacity{Total Records ><br/>MaxCapacity?}
    
    CheckCapacity -->|Yes| Archive[Archive Oldest Data<br/>to KWHData_History]
    Archive --> Delete[Delete from KWHData]
    Delete --> CheckThreshold
    CheckCapacity -->|No| CheckThreshold[Check Load Threshold]
    
    CheckThreshold -->|Power > 20kW| StatusHIGH[Set Status = HIGH]
    CheckThreshold -->|10kW < Power ≤ 20kW| StatusMED[Set Status = MEDIUM]
    CheckThreshold -->|Power ≤ 10kW| StatusNORM[Set Status = NORMAL]
    
    StatusHIGH --> AnomalyCheck
    StatusMED --> AnomalyCheck
    StatusNORM --> AnomalyCheck
    
    AnomalyCheck[Anomaly Detection] -->|Anomaly Found| SaveAnomaly[Save AnomalyLog]
    SaveAnomaly --> SendNotif[Send Notification]
    SendNotif --> End
    
    AnomalyCheck -->|No Anomaly| End
```

---

## 3. Flow Anomaly Detection

```mermaid
flowchart TD
    Start([New KWHData Entry]) --> LoadSettings[Load EMA Settings<br/>from AppSettingsCache]
    LoadSettings --> GetEMA[Get EMA Value]
    
    GetEMA --> CalcEMA{EMA Mode?}
    CalcEMA -->|manual| ManualEMA[EMA ± Threshold %<br/>Upper: +30%<br/>Lower: -50%]
    CalcEMA -->|fibonacci| FibEMA[EMA × Fibonacci<br/>Upper: ×1.618<br/>Lower: ×0.618]
    
    ManualEMA --> Compare
    FibEMA --> Compare[Compare Power vs Threshold]
    
    Compare --> IsOverload{Power ><br/>Upper Threshold?}
    IsOverload -->|Yes| AnomalyType[Anomaly Type = OVERLOAD]
    IsOverload -->|No| IsDrop{Power <br/>Lower Threshold?}
    
    IsDrop -->|Yes| AnomalyTypeDrop[Anomaly Type = DROP]
    IsDrop -->|No| Normal[No Anomaly - Normal Zone]
    Normal --> Reset[Reset Counters<br/>Clear Cooldown]
    Reset --> End([End])
    
    AnomalyType --> CheckCooldown{In Cooldown<br/>Period?}
    AnomalyTypeDrop --> CheckCooldown
    
    CheckCooldown -->|Yes| End
    CheckCooldown -->|No| CheckDedup{Server Dedup<br/>Last 5 min?}
    
    CheckDedup -->|Yes| End
    CheckDedup -->|No| CheckDowntime{Downtime Period<br/>22:00-06:00?}
    
    CheckDowntime -->|Yes & OVERLOAD| PowerAlert[Send Downtime Power Alert]
    PowerAlert --> SaveLog
    CheckDowntime -->|Yes & DROP| NormalDrop[DROP Normal saat Downtime]
    NormalDrop --> End
    
    CheckDowntime -->|No| SaveLog[Save AnomalyLog]
    SaveLog --> Snapshot[Create Chart Snapshot]
    Snapshot --> Notify[Send Notification<br/>Email + WhatsApp]
    Notify --> UpdateMonthly[Update Monthly Report]
    UpdateMonthly --> SetCooldown[Set Cooldown 5 min]
    SetCooldown --> End
```

---

## 4. Flow Authentication & Authorization

```mermaid
flowchart TD
    Start([User Access]) --> IsLoggedIn{Logged In?}
    
    IsLoggedIn -->|No| LoginPage[Show Login Page]
    LoginPage --> RegisterLink[Register Link]
    LoginPage --> ForgotPasswordLink[Forgot Password Link]
    
    RegisterLink --> RegisterPage[Register Page]
    RegisterPage --> InputReg[Input Email, Password,<br/>Display Name]
    InputReg --> ValidateReg{Valid Input?}
    ValidateReg -->|No| RegisterPage
    ValidateReg -->|Yes| HashPass[Hash Password PBKDF2]
    HashPass --> SaveUser[Save User Role=None]
    SaveUser --> GenToken[Generate Verification Token]
    GenToken --> HashToken[Hash Token SHA256]
    HashToken --> SaveToken[Save Token 24h Expiry]
    SaveToken --> SendVerifEmail[Send Verification Email]
    SendVerifEmail --> RegSuccess[Registration Success Page]
    RegSuccess --> End([End])
    
    ForgotPasswordLink --> ForgotPage[Forgot Password Page]
    ForgotPage --> InputEmail[Input Email]
    InputEmail --> LookupUser{User Exists?}
    LookupUser -->|No| ForgotConfirm[Forgot Confirmation Page]
    LookupUser -->|Yes| GenResetToken[Generate Reset Token]
    GenResetToken --> SaveResetToken[Save Token 1h Expiry]
    SaveResetToken --> SendResetEmail[Send Reset Email]
    SendResetEmail --> ForgotConfirm
    ForgotConfirm --> End
    
    IsLoggedIn -->|Yes| CheckRole{User Role?}
    
    CheckRole -->|None| PendingPage[Pending Approval Page]
    PendingPage --> End
    
    CheckRole -->|Viewer| ViewerAccess[Viewer Dashboard]
    CheckRole -->|Operator| OperatorAccess[Operator Dashboard<br/>+ Device Control]
    CheckRole -->|Admin| AdminAccess[Admin Dashboard<br/>+ Settings]
    CheckRole -->|Master Admin| MasterAccess[Full Access<br/>+ User Management]
    
    ViewerAccess --> End
    OperatorAccess --> End
    AdminAccess --> End
    MasterAccess --> End
    
    LoginPage --> InputLogin[Input Email + Password]
    InputLogin --> LookupLogin{User Exists?}
    LookupLogin -->|No| LoginError[Error: Invalid Credentials]
    LookupLogin -->|Yes| CheckLockout{Is Locked Out?}
    
    CheckLockout -->|Yes| LockoutError[Error: Account Locked<br/>until LockoutEnd]
    LockoutError --> LoginPage
    
    CheckLockout -->|No| CheckEmailConf{Email Confirmed?}
    CheckEmailConf -->|No| EmailError[Error: Email Not Verified]
    EmailError --> LoginPage
    
    CheckEmailConf -->|Yes| CheckActive{Is Active?}
    CheckActive -->|No| InactiveError[Error: Account Inactive]
    InactiveError --> LoginPage
    
    CheckActive -->|Yes| CheckRoleLogin{Role != None?}
    CheckRoleLogin -->|No| PendingError[Error: Pending Approval]
    PendingError --> LoginPage
    
    CheckRoleLogin -->|Yes| VerifyPass{Password Valid?}
    VerifyPass -->|No| IncFailed[AccessFailedCount++]
    IncFailed --> CheckMaxFail{Count >= 5?}
    CheckMaxFail -->|Yes| Lockout[Set LockoutEnd +15min]
    Lockout --> LoginError
    CheckMaxFail -->|No| LoginError
    
    VerifyPass -->|Yes| ResetFail[Reset AccessFailedCount=0<br/>Update LastLoginAt]
    ResetFail --> SignIn[SignIn with Claims<br/>Name, Email, Role]
    SignIn --> RememberMe{Remember Me?}
    RememberMe -->|Yes| Persist7d[Persistent Cookie 7 days]
    RememberMe -->|No| Session30m[Session Cookie 30 min]
    Persist7d --> Redirect[Redirect to Dashboard]
    Session30m --> Redirect
    Redirect --> End
```

---

## 5. Flow Energy Aggregation

```mermaid
flowchart TD
    Start([Timer Trigger<br/>Every 30 Seconds]) --> GetDevice[Get Next Device]
    GetDevice --> HasDevice{Has More<br/>Devices?}
    
    HasDevice -->|No| HourlyDone{Hourly<br/>Complete?}
    HasDevice -->|Yes| GetLastHour[Get Last Hour Data]
    
    GetLastHour --> CalcDelta[Calculate Delta<br/>Total_Energy_Wh]
    CalcDelta --> ConvertKWh[Convert to kWh<br/>÷ 1000]
    ConvertKWh --> SaveHourly[Save to HourlyEnergy]
    SaveHourly --> GetDevice
    
    HourlyDone -->|No| DailyTrigger{Hour = 00:00?}
    HourlyDone -->|Yes| End([End])
    
    DailyTrigger -->|Yes| SumHourly[Sum HourlyEnergy<br/>Today]
    SumHourly --> SaveDaily[Save to DailyEnergy]
    SaveDaily --> MonthlyTrigger{Day = 1st?}
    
    DailyTrigger -->|No| End
    
    MonthlyTrigger -->|Yes| SumDaily[Sum DailyEnergy<br/>This Month]
    SumDaily --> SaveMonthly[Save to MonthlyEnergy]
    SaveMonthly --> YearlyTrigger{Month = January?}
    
    MonthlyTrigger -->|No| End
    
    YearlyTrigger -->|Yes| SumMonthly[Sum MonthlyEnergy<br/>This Year]
    SumMonthly --> SaveYearly[Save to YearlyEnergy]
    SaveYearly --> End
    
    YearlyTrigger -->|No| End
```

---

## 6. Flow Notification System

```mermaid
flowchart TD
    Start([Anomaly Detected]) --> LoadSettings[Load Notification Settings]
    LoadSettings --> CheckEnabled{Email or WhatsApp<br/>Enabled?}
    
    CheckEnabled -->|No| End([End])
    CheckEnabled -->|Yes| CheckInstant{SendInstantAlert<br/>Enabled?}
    
    CheckInstant -->|No| End
    CheckInstant -->|Yes| IsDowntime{Downtime Period<br/>22:00-06:00?}
    
    IsDowntime -->|Yes & OVERLOAD| DowntimeAlert[Send Downtime Power Alert]
    DowntimeAlert --> GetPanelData[Get Latest Panel Data]
    
    IsDowntime -->|No| GetPanelData
    
    IsDowntime -->|Yes & DROP| NormalDrop[DROP Normal - No Alert]
    NormalDrop --> End
    
    GetPanelData --> GetRecentAnomalies[Get Anomalies Last 24h]
    GetRecentAnomalies --> BuildEmail[Build HTML Email]
    
    BuildEmail --> EmailHeader[Header: Gradient + Alert Icon]
    EmailHeader --> AlertCards[Alert Detail Cards:<br/>Power, Threshold, Deviation]
    AlertCards --> DeviceStatus[Device Status Table]
    DeviceStatus --> AnomalySummary[Anomaly Summary 24h]
    AnomalySummary --> EmailFooter[Footer with Timestamp]
    EmailFooter --> SendEmail{Email Enabled?}
    
    SendEmail -->|Yes| SMTPSend[Send via SMTP]
    SMTPSend --> BuildWA
    SendEmail -->|No| BuildWA
    
    BuildWA[Build WhatsApp Message] --> WAHeader[Header: Emoji + Alert Type]
    WAHeader --> WADetail[Alert Detail:<br/>Device, Power, Threshold]
    WADetail --> WADevice[Device Status:<br/>Voltage, Current, PF]
    WADevice --> WASummary[Anomaly Summary 24h]
    WASummary --> WAFooter[Footer: KWH Monitoring]
    WAFooter --> SendWA{WhatsApp Enabled?}
    
    SendWA -->|Yes| WablasPost[POST to Wablas API]
    WablasPost --> LogNotif[Log Notification]
    SendWA -->|No| LogNotif
    
    LogNotif --> End
```

---

## 7. Flow Relay Control dengan OTP

```mermaid
flowchart TD
    Start([User Click ON/OFF]) --> CheckAuth{Authenticated?}
    
    CheckAuth -->|No| LoginRedirect[Redirect to Login]
    LoginRedirect --> End([End])
    
    CheckAuth -->|Yes| IsOff{OFF Button?}
    
    IsOff -->|Yes| ShowOTP[Show OTP Modal]
    ShowOTP --> InputOTP[User Input 6-digit OTP]
    InputOTP --> ValidateOTP{OTP Valid?}
    
    ValidateOTP -->|No| OTPError[Show Error Message]
    OTPError --> ShowOTP
    
    ValidateOTP -->|Yes| RateLimitCheck
    IsOff -->|Yes| RateLimitCheck{Rate Limited?<br/>10 req/60s}
    
    RateLimitCheck -->|Yes| RateError[Return Error 429]
    RateError --> End
    
    RateLimitCheck -->|No| LogSecurity[Log Security Action<br/>RelayControl]
    LogSecurity --> BuildTopic[Build MQTT Topic:<br/>data/KWHAPP/{deviceId}/12345678]
    BuildTopic --> BuildPayload[Build JSON Payload:<br/>{_terminalTime, _groupName, RC}]
    BuildPayload --> CheckConnection{MQTT Connected?}
    
    CheckConnection -->|No| ConnectMQTT[Connect to Broker]
    ConnectMQTT --> Publish
    CheckConnection -->|Yes| Publish[Publish via MQTTnet<br/>QoS 1]
    
    Publish --> Success{Publish Success?}
    Success -->|Yes| LogSuccess[Log Success to RelayControls]
    Success -->|No| LogFail[Log Failure to AppLog]
    
    LogSuccess --> UpdateUI[Update UI Status]
    LogFail --> UpdateUI
    
    UpdateUI --> End
```

---

## 8. Flow User Management (Master Admin)

```mermaid
flowchart TD
    Start([User Management Page]) --> CheckMaster{Is Master Admin?}
    
    CheckMaster -->|No| AccessDenied[Show Access Denied]
    AccessDenied --> End([End])
    
    CheckMaster -->|Yes| LoadUsers[Load All Users<br/>Exclude Master Admin]
    LoadUsers --> RenderTable[Render User Table]
    
    RenderTable --> UserAction{User Action?}
    
    UserAction -->|Change Role| SelectRole[Select New Role]
    SelectRole --> ValidateRole{Valid Role?}
    ValidateRole -->|No| RoleError[Show Error]
    RoleError --> RenderTable
    ValidateRole -->|Yes| CheckMasterUser{Is Master Admin?}
    CheckMasterUser -->|Yes| MasterProtect[Error: Cannot Change Master Admin]
    MasterProtect --> RenderTable
    CheckMasterUser -->|No| UpdateRole[Update ApplicationUser.Role]
    UpdateRole --> LogRoleChange[Log RoleChanged to Audit]
    LogRoleChange --> SendEmail[Send Email Notification]
    SendEmail --> RenderTable
    
    UserAction -->|Toggle Active| ToggleBtn[Toggle Active Button]
    ToggleBtn --> CheckMasterUser2{Is Master Admin?}
    CheckMasterUser2 -->|Yes| MasterProtect2[Error: Cannot Change Master Admin]
    MasterProtect2 --> RenderTable
    CheckMasterUser2 -->|No| UpdateActive[Update ApplicationUser.IsActive]
    UpdateActive --> RenderTable
    
    UserAction -->|Delete User| ConfirmDel[Confirm Delete Dialog]
    ConfirmDel --> CheckMasterUser3{Is Master Admin?}
    CheckMasterUser3 -->|Yes| MasterProtect3[Error: Cannot Delete Master Admin]
    MasterProtect3 --> RenderTable
    CheckMasterUser3 -->|No| DeleteTokens[Delete EmailVerificationTokens]
    DeleteTokens --> DeleteUser[Delete ApplicationUser]
    DeleteUser --> RenderTable
    
    RenderTable --> Back[Back Button]
    Back --> End
```

---

## 9. Background Service Loop

```mermaid
flowchart TD
    Start([BackgroundService<br/>StartAsync]) --> Init[Initialize]
    Init --> LoopStart([Main Loop])
    
    LoopStart --> Wait[Wait 30 Seconds]
    Wait --> LoadDevices[Load All Active Devices<br/>from DeviceRegistry]
    LoadDevices --> NextDevice{Has Next Device?}
    
    NextDevice -->|No| LoopStart
    NextDevice -->|Yes| GetLastData[Get Last KWHData<br/>for Device]
    
    GetLastData --> HasData{Has Data?}
    HasData -->|No| NextDevice
    HasData -->|Yes| LoadAnomalySettings[Load Anomaly Settings]
    
    LoadAnomalySettings --> CalcEMA[Calculate EMA]
    CalcEMA --> DetermineType[Determine Anomaly Type<br/>OVERLOAD/DROP/NORMAL]
    
    DetermineType --> CheckCooldown{In Cooldown?}
    CheckCooldown -->|Yes| NextDevice
    CheckCooldown -->|No| CheckDedup{Server Dedup<br/>Last 5 min?}
    
    CheckDedup -->|Yes| NextDevice
    CheckDedup -->|No| IsAnomaly{Is Anomaly?}
    
    IsAnomaly -->|No| CheckActiveAnomaly{Active Anomaly<br/>for Device?}
    CheckActiveAnomaly -->|Yes| AutoAck[Auto-Acknowledge Anomaly]
    AutoAck --> ResetCounter[Reset Counters]
    ResetCounter --> NextDevice
    CheckActiveAnomaly -->|No| NextDevice
    
    IsAnomaly -->|Yes| SaveAnomaly[Save AnomalyLog]
    SaveAnomaly --> CreateSnapshot[Create Chart Snapshot]
    CreateSnapshot --> SendNotif[Send Notification]
    SendNotif --> UpdateMonthly[Update Monthly Report]
    UpdateMonthly --> SetCooldown[Set Cooldown 5 min]
    SetCooldown --> NextDevice
    
    ErrorHandle{Error Occurred?}
    ErrorHandle -->|Yes| LogError[Log Error to AppLog]
    LogError --> NextDevice
```

---

## 10. Sequence Diagram - End-to-End Flow

```mermaid
sequenceDiagram
    participant D as Device
    participant MQ as MQTT Broker
    participant MS as MqttService
    participant AC as AnomalyAnalysisService
    participant DB as Database
    participant NS as NotificationService
    participant EM as Email/WhatsApp
    participant UI as Dashboard UI
    participant U as User

    D->>MQ: MQTT Publish (JSON payload)
    MQ->>MS: MQTT Subscribe (callback)
    MS->>DB: Save to KWHData
    MS->>DB: Update DeviceRegistry
    MS->>AC: Analyze for Anomaly
    AC->>DB: Load EMA Settings
    AC->>AC: Calculate EMA
    AC->>AC: Compare vs Threshold
    
    alt Anomaly Detected
        AC->>DB: Save AnomalyLog
        AC->>DB: Create ChartSnapshot
        AC->>NS: Send Notification
        NS->>DB: Load Notification Settings
        NS->>EM: Send Email Alert
        NS->>EM: Send WhatsApp Alert
        NS->>DB: Update MonthlyReport
    else Normal
        AC->>DB: Check Active Anomalies
        alt Has Active Anomaly
            AC->>DB: Auto-Acknowledge
        end
    end
    
    U->>UI: Open Dashboard
    UI->>DB: Query Latest Data (via API)
    DB->>UI: Return Panel Data
    UI->>U: Display Dashboard
    
    loop Every 10 seconds
        UI->>DB: Poll for Updates (API)
        DB->>UI: Return Latest Data
        UI->>U: Auto-refresh Dashboard
    end
```

---

## 11. Database Entity Relationship

```mermaid
erDiagram
    KWHData ||--o{ KWHData_History : "archives to"
    KWHData ||--o| DeviceRegistry : "references"
    KWHData ||--o{ HourlyEnergy : "aggregates to"
    KWHData ||--o{ AnomalyLog : "triggers"
    
    AnomalyLog ||--o| AnomalyChartSnapshot : "has one"
    AnomalyLog ||--o{ AnomalyMonthlyReport : "included in"
    
    HourlyEnergy ||--o{ DailyEnergy : "aggregates to"
    DailyEnergy ||--o{ MonthlyEnergy : "aggregates to"
    MonthlyEnergy ||--o{ YearlyEnergy : "aggregates to"
    
    ApplicationUser ||--o{ EmailVerificationToken : "has many"
    ApplicationUser ||--o{ SecurityAuditLog : "generates"
    ApplicationUser ||--o{ AnomalyLog : "acknowledges/resolves"
    
    AppSettingsRecord ||--o| AppSettingsCache : "loads to"
    
    KWHData {
        int Id PK
        string DeviceKey FK
        string DeviceId
        string GroupName
        datetime Waktu_Device
        datetime Waktu_Server
        decimal PHASE_R
        decimal PHASE_S
        decimal PHASE_T
        decimal AMPERE_R
        decimal W
        decimal CosPhi
    }
    
    DeviceRegistry {
        int Id PK
        string DeviceKey UK
        string DeviceId
        string GroupName
        datetime FirstSeen
        datetime LastSeen
        bool IsActive
        int MessageCount
    }
    
    AnomalyLog {
        int Id PK
        string DeviceKey FK
        string AnomalyType
        decimal PowerValue
        decimal ThresholdValue
        decimal Deviation
        datetime DetectedTime
        bool Acknowledged
        bool IsResolved
        string Severity
    }
    
    ApplicationUser {
        int Id PK
        string Email UK
        string PasswordHash
        string Role
        bool EmailConfirmed
        bool IsActive
    }
```

---

## 12. Deployment Architecture

```mermaid
graph TB
    subgraph Client["🖥️ Client Layer"]
        Browser[Web Browser<br/>Chrome/Firefox/Edge]
        Mobile[Mobile Browser<br/>Responsive]
    end

    subgraph Web["🌐 Web Server Layer"]
        Nginx[Nginx Reverse Proxy<br/>HTTPS Termination]
        Kestrel[Kestrel Web Server<br/>ASP.NET Core 2.1]
    end

    subgraph App["💻 Application Layer"]
        AppInstance1[App Instance 1<br/>Background Service]
        AppInstance2[App Instance 2<br/>Background Service]
    end

    subgraph Data["🗄️ Data Layer"]
        SQL[(SQL Server<br/>Database)]
        Redis[(Redis Cache<br/>Optional)]
    end

    subgraph External["📡 External Services"]
        MQTT[MQTT Broker<br/>Mosquitto/EMQX]
        SMTP[SMTP Server<br/>Gmail/Office365]
        Wablas[Wablas API<br/>WhatsApp Gateway]
    end

    Browser -->|HTTPS| Nginx
    Mobile -->|HTTPS| Nginx
    Nginx -->|HTTP| Kestrel
    Kestrel --> AppInstance1
    Kestrel --> AppInstance2
    
    AppInstance1 -->|EF Core| SQL
    AppInstance2 -->|EF Core| SQL
    AppInstance1 -->|MemoryCache| Redis
    AppInstance2 -->|MemoryCache| Redis
    
    AppInstance1 -->|MQTT Subscribe| MQTT
    AppInstance2 -->|MQTT Subscribe| MQTT
    
    AppInstance1 -->|SMTP| SMTP
    AppInstance1 -->|HTTP POST| Wablas
```

---

## 13. Flow Export CSV

```mermaid
flowchart TD
    Start([User Click Export CSV]) --> CheckAuth{Authenticated?}
    
    CheckAuth -->|No| LoginRedirect[Redirect to Login]
    LoginRedirect --> End([End])
    
    CheckAuth -->|Yes| GetDeviceKey{DeviceKey<br/>Provided?}
    
    GetDeviceKey -->|No| GetAll[Get All Devices Data]
    GetDeviceKey -->|Yes| GetSingle[Get Single Device Data]
    
    GetAll --> QueryDB[Query KWHData<br/>ORDER BY Waktu_Server DESC<br/>TOP 50000]
    GetSingle --> QueryDB
    
    QueryDB --> BuildCSV[Build CSV String]
    BuildCSV --> Header[Add Header Row:<br/>Id,DeviceKey,DeviceId,...]
    Header --> LoopData[Loop Through Records]
    
    LoopData --> EscapeFields[CsvEscape Fields:<br/>DeviceKey, DeviceId, GroupName]
    EscapeFields --> FormatDateTime[Format DateTime:<br/>yyyy-MM-dd HH:mm:ss]
    FormatDateTime --> FormatNumbers[Format Numbers:<br/>CultureInfo.InvariantCulture]
    FormatNumbers --> AppendRow[Append CSV Row]
    AppendRow --> MoreRecords{More Records?}
    
    MoreRecords -->|Yes| LoopData
    MoreRecords -->|No| ConvertBytes[Convert to UTF-8 Bytes]
    ConvertBytes --> GenFilename[Generate Filename:<br/>KWH_Monitoring_{timestamp}.csv]
    GenFilename --> ReturnFile[Return FileResult<br/>text/csv]
    ReturnFile --> End
```

---

## 14. Flow Settings Management

```mermaid
flowchart TD
    Start([User Open Settings]) --> CheckAdmin{Is Admin?}
    
    CheckAdmin -->|No| AccessDenied[Access Denied]
    AccessDenied --> End([End])
    
    CheckAdmin -->|Yes| CheckMaster{Is Master Admin?}
    CheckMaster -->|Yes| ShowMaster[Show Master Admin Settings]
    CheckMaster -->|No| ShowNormal[Show Normal Admin Settings]
    
    ShowMaster --> RenderSettings[Render Settings Form]
    ShowNormal --> RenderSettings
    
    RenderSettings --> LoadFromDB[Load AppSettingsRecords]
    LoadFromDB --> PopulateForm[Populate Form Fields]
    PopulateForm --> UserEdit{User Edit?}
    
    UserEdit -->|Save| ValidateInput{Valid Input?}
    ValidateInput -->|No| ShowError[Show Validation Error]
    ShowError --> RenderSettings
    
    ValidateInput -->|Yes| UpdateDB[Update AppSettingsRecords]
    UpdateDB --> SaveChanges[SaveChangesAsync]
    SaveChanges --> TriggerHook[Raise AppSettingsChanged Hook]
    TriggerHook --> RefreshCache[AppSettingsCache Refresh]
    RefreshCache --> LogAudit[Log SettingsUpdated to Audit]
    LogAudit --> SendEmail[Send Email Notification]
    SendEmail --> ShowSuccess[Show Success Message]
    ShowSuccess --> End
    
    UserEdit -->|Cancel| CancelEdit[Discard Changes]
    CancelEdit --> End
    
    UserEdit -->|Test Notification| TestEmail[Send Test Email]
    TestEmail --> TestWA[Send Test WhatsApp]
    TestWA --> ShowTestResult[Show Test Result]
    ShowTestResult --> RenderSettings
```

---

Dokumentasi flowchart ini dibuat pada: **18 September 2026**  
Format: **Mermaid.js** (compatible dengan GitHub, GitLab, Notion, Obsidian)  
Total Diagram: **14 flowchart berbeda**  
Coverage: **End-to-end system flow**
